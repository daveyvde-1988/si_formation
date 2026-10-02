using System.Reflection;
using Si_FollowFormation;
using Silica.AI;
using UnityEngine;
using Pathfinding;

static class Program
{
 static readonly Type ModType=typeof(FollowFormation);
 static void Invoke(string name,params object[] args)=>ModType.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
 static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
 static object State(Unit unit){var states=ModType.GetField("States",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);return states.GetType().GetProperty("Item").GetValue(states,new object[]{unit});}
 static T StateValue<T>(Unit unit,string name)=>(T)State(unit).GetType().GetField(name).GetValue(State(unit));
 static (FollowFormation mod,Unit leader,List<Unit> followers) Setup(int count=1)
 {
  Invoke("Disable"); AstarPath.Pending.Clear();Unit.Units.Clear();Player.Players.Clear();Time.unscaledTime=0;Physics.Blocked=false;GameAI.Valid=true;GameAI.Accept=null;
  ModType.GetField("Faulted",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
  var suppressed=ModType.GetField("Superseded",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null);suppressed?.GetType().GetMethod("Clear").Invoke(suppressed,null);
  var mod=new FollowFormation();mod.OnLateInitializeMelon();Invoke("Command",null,"!followformation on");
  var player=new Player();var leader=new Unit{ControlledBy=player,AIControlled=false};player.ControlledUnit=leader;Player.Players.Add(player);Unit.Units.Add(leader);
  var group=new AIGroup{Leader=leader};var followers=new List<Unit>();
  for(int i=0;i<count;i++){var unit=new Unit{AIGroup=group};unit.transform.position=new((i%2==0?-1:1)*36,0,-12);group.Reasons[unit]=AIGroup.EIgnoreTaskReason.FOLLOWING_LEADER;Unit.Units.Add(unit);followers.Add(unit);}
  return(mod,leader,followers);
 }
 static void Tick(FollowFormation mod,float time){Time.unscaledTime=time;mod.OnUpdate();}
 static void Run(string name,Action test){test();Console.WriteLine("PASS "+name);}
 static void Main()
 {
  Run("quiet three-second stop scatters once immediately",()=>{var(m,l,f)=Setup();Tick(m,0);Assert(f[0].OrderAgent.Destinations.Count==0,"new state scattered early");Tick(m,2.99f);Assert(f[0].OrderAgent.Destinations.Count==0,"checked early");Tick(m,3);Assert(f[0].OrderAgent.Destinations.Count==1,"quiet stop not scattered");Assert(AstarPath.Pending.Count==0,"A* request was queued");Tick(m,6);Assert(f[0].OrderAgent.Destinations.Count==1,"stationary stop retried");});
  Run("assignment heading uses leader facing at the stop",()=>{var(m,l,f)=Setup();Tick(m,0);l.transform.forward=new(1,0,0);Tick(m,3);var p=f[0].OrderAgent.Destinations[0];Assert(p.x<0&&MathF.Abs(MathF.Abs(p.z)-5)<0.02f,"scatter did not use heading at quiet stop");l.transform.forward=new(0,0,-1);Tick(m,6);Assert(f[0].OrderAgent.Destinations.Count==1,"rotation after assignment reissued");});
  Run("eight slots use ten-metre columns and full row range",()=>{var(m,l,f)=Setup(8);Tick(m,0);Tick(m,3);var positions=f.SelectMany(u=>u.OrderAgent.Destinations).ToArray();Assert(positions.Length==8,"did not assign all eight units");for(int i=0;i<8;i++){float expectedZ=8.660254f+10*(i/2);Assert(MathF.Abs(-positions[i].z-expectedZ)<0.02f,"wrong column/row spacing");Assert(MathF.Abs(MathF.Abs(positions[i].x)-5)<0.02f,"wrong column offset");}Assert(MathF.Sqrt(positions.Max(p=>p.sqrMagnitude))>38.9f,"farthest row was capped");});
  Run("strict ten metres retains assignment and greater movement releases",()=>{var(m,l,f)=Setup();Tick(m,0);Tick(m,3);l.transform.position=new(0,0,10);Tick(m,6);Assert(StateValue<bool>(f[0],"Active"),"exactly ten metres released assignment");Assert(f[0].AIAgent.AgentPathfinding.Resets==0,"exact threshold reset movement");l.transform.position=new(0,0,10.01f);Tick(m,9);Assert(!StateValue<bool>(f[0],"Active"),"movement above ten metres did not release");Assert(f[0].AIAgent.AgentPathfinding.Resets==1,"released assignment not reset");Assert(!StateValue<bool>(f[0],"Attempted"),"replacement stop attempted too soon");Tick(m,12);Assert(StateValue<bool>(f[0],"Attempted"),"new quiet stop not attempted");});
  Run("small ongoing travel increments never scatter",()=>{var(m,l,f)=Setup();Tick(m,0);Tick(m,3);for(int i=1;i<=8;i++){l.transform.position=new(0,0,10+i*1.1f);Tick(m,3+i*3);Assert(!StateValue<bool>(f[0],"Active"),"ongoing travel retained formation assignment");Assert(f[0].OrderAgent.Destinations.Count==1,"ongoing travel scattered again");}Assert(!StateValue<bool>(f[0],"Attempted"),"ongoing travel attempted a stop");});
  Run("projection failure leaves vanilla Follow and never retries while stopped",()=>{var(m,l,f)=Setup();GameAI.Valid=false;Tick(m,0);Tick(m,3);Assert(StateValue<bool>(f[0],"Attempted"),"failed scatter not recorded");Assert(AstarPath.Pending.Count==0,"projection failure queued A*");var p=OrderIssueParams.Ai(0);Invoke("VanillaFollow",f[0].OrderAgent,l.transform.position,null,p);Assert(f[0].OrderAgent.Destinations.Count==1,"native Follow suppressed after failure");Tick(m,6);Assert(f[0].OrderAgent.Destinations.Count==1,"stationary stop retried");});
  Run("blocked physics does not prevent projected orders",()=>{var(m,l,f)=Setup();Physics.Blocked=true;Tick(m,0);Tick(m,3);Assert(f[0].OrderAgent.Destinations.Count==1,"physics blocking prevented order");Assert(AstarPath.Pending.Count==0,"A* request was queued");});
  Run("disable releases assigned route immediately",()=>{var(m,l,f)=Setup();Tick(m,0);Tick(m,3);Invoke("Command",null,"!followformation off");Assert(f[0].AIAgent.AgentPathfinding.Resets==1,"active route not reset");Assert(f[0].AIAgent.AgentPathfinding.PathfindingSeeker.Cancels==1,"active path not cancelled");Tick(m,6);Assert(f[0].OrderAgent.Destinations.Count==1,"disabled update issued another order");});
  Run("ineligible and player-driven units untouched",()=>{var(m,l,f)=Setup(3);f[0].AIGroup.Reasons[f[0]]=AIGroup.EIgnoreTaskReason.NONE;f[1].ControlledBy=new Player();f[2].Driver=new Unit{ControlledBy=new Player()};Tick(m,0);Tick(m,3);Assert(f.All(u=>u.OrderAgent.Destinations.Count==0),"ineligible unit moved");});
  Run("unauthorized runtime command denied",()=>{var(m,l,f)=Setup();Invoke("Command",new Player{Admin=false},"!followformation off");Tick(m,0);Tick(m,3);Assert(f[0].OrderAgent.Destinations.Count==1,"unauthorized off accepted");});
  Run("external Move supersedes formation until renewed Follow",()=>{var(m,l,f)=Setup();Tick(m,0);Tick(m,3);var p=OrderIssueParams.Ai(0);f[0].OrderAgent.IssueResolvedOrder(new(40,0,40),null,in p);Tick(m,6);Assert(f[0].OrderAgent.Destinations.Count==2,"external order overwritten or follow failed");Invoke("FollowChanged",f[0]);Tick(m,9);Tick(m,12);Assert(f[0].OrderAgent.Destinations.Count==3,"renewed Follow not accepted");});
  Invoke("Disable");Console.WriteLine("All headless source-linked checks passed. Stubs do not validate Unity, Harmony IL, network replication, or real pathfinding.");
 }
}
