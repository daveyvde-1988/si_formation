using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Si_Formation;
using Silica.AI;
using SilicaAdminMod;
using UnityEngine;

static class Checks
{
    static int count;
    static readonly Formations Mod=new();
    static readonly Type T=typeof(Formations);
    static readonly Team Sol=new(){name="Team_Human_Sol",TeamShortName="Sol"};
    static void Assert(bool ok,string message){count++;if(!ok)throw new Exception(message);}
    static object Field(string name)=>T.GetField(name,BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
    static void Reset(){Mod.OnSceneWasUnloaded(0,"test");Player.Players.Clear();Player.CurrentPlayer=null;Time.unscaledTime=0;TerrainProjection.Valid=true;TerrainProjection.Checks=0;}
    static (Player p,Unit[] units) Squad(int n)
    {
        var p=new Player{Team=Sol,PlayerID=new NetworkID{Id=Player.Players.Count+1}};
        var leader=new Unit{Team=Sol,ControlledBy=p};var group=new AIGroup{Leader=leader};
        p.ControlledUnit=leader;leader.AIGroup=group;group.Units.Add(leader);Player.Players.Add(p);
        var units=Enumerable.Range(0,n).Select(i=>new Unit{Team=Sol,AIGroup=group,ObjectInfo=new ObjectInfo{name="ObjectInfo_Sol_Soldier_Rifleman"}}).ToArray();
        group.Units.AddRange(units);return(p,units);
    }
    static FormationDefinition Definition(string function,int n=100,string name="Test formation")
        =>new(name,"Sol",function,true,5,Enumerable.Range(0,n).Select(i=>new FormationSlot(i*20,0,"top",1,Array.Empty<string>())).ToArray());
    static void Select(Player p,FormationDefinition f)
    {
        ((FormationCatalog)Field("Catalog")).Definitions[FormationCatalog.Key(f.Team,f.Function,f.Name)]=f;
        Hooks.Call("SelectCommand",p,"/"+f.Function+"formation "+JsonConvert.SerializeObject(f.Name),f.Function);
    }
    static List<BaseGameObject> Objects(Unit[] units)=>units.Cast<BaseGameObject>().ToList();
    static bool Commander(Player p,Unit[] units)
    {
        Player.CurrentPlayer=p;
        return (bool)Hooks.Call("CommanderMove",Objects(units),new Vector3(200,0,100),null,AgentMoveSpeed.Normal,false,false,false);
    }
    static int Operations=>((IList)Field("Operations")).Count;
    static bool Stage(Player p,Target target)=>(bool)Hooks.Call("FpsAttack",p,target);
    static Target TargetAt()=>new(){Owner=new BaseGameObject{transform=new Transform{position=new Vector3(100,0,100)}}};
    public static void Main(string[] args)
    {
        string server=args[0];MelonLoader.Utils.MelonEnvironment.UserDataDirectory=Path.Combine(server,"UserData");
        Mod.OnLateInitializeMelon();
        Assert((bool)Field("Ready"),"initialization failed");
        var catalog=(FormationCatalog)Field("Catalog");
        Assert(catalog.Definitions.Count==3,"actual disk JSON has three formations");
        Assert(catalog.TryGet("Sol","move","circle",out var disk),"case-insensitive name selection");
        Assert(!catalog.TryGet("Sol","follow","Circle",out _),"wrong function must fail");
        Assert(!catalog.TryGet("Unknown","move","Circle",out _),"wrong team must fail");
        Assert(disk.Slots.Length==34&&disk.Scale==20,"actual schema/scale");
        var document=JObject.Parse(File.ReadAllText(Path.Combine(server,"UserData","Formations_cfg","Formations.json")));
        var invalid=(JObject)document.DeepClone();invalid["formations"][0]["directionSensitive"]=true;
        bool rejected=false;try{FormationCatalog.Parse(invalid.ToString());}catch(FormatException){rejected=true;}
        Assert(rejected,"coordinate-space mismatch accepted");
        string temp=Path.Combine(Path.GetTempPath(),"Si_Formation-check-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            File.WriteAllText(Path.Combine(temp,"a.json"),document.ToString());
            File.WriteAllText(Path.Combine(temp,"b.json"),document.ToString());
            File.WriteAllText(Path.Combine(temp,"broken.json"),"{");
            var messages=new List<string>();var duplicates=new FormationCatalog();duplicates.Load(temp,messages.Add);
            Assert(duplicates.Definitions.Count==0&&messages.Any(s=>s.Contains("duplicate"))&&messages.Any(s=>s.Contains("rejected")),"duplicate/malformed diagnostics");
        }
        finally {foreach(string file in Directory.GetFiles(temp))File.Delete(file);Directory.Delete(temp);}
        Assert(CommandArgument.TryParse("/moveformation \"large circle\"",out var name,out bool quoted)&&name=="large circle"&&quoted,"quoted name");
        Assert(CommandArgument.TryParse("/moveformation circle",out name,out quoted)&&name=="circle"&&!quoted,"bare name");
        Assert(!CommandArgument.TryParse("/moveformation \"bad",out _,out _),"broken quote accepted");
        Console.WriteLine("PASS JSON: actual file, strict function/team/name, malformed files, duplicate disabling, names.");

        Reset();var (p,units)=Squad(2);Select(p,Definition("move"));
        var (other,others)=Squad(1);
        Assert((bool)Hooks.Call("FpsMove",other,new Vector3(200,0,100)),"selection leaked to another player");
        Assert(!(bool)Hooks.Call("FpsMove",p,new Vector3(200,0,100)),"selected FPS move not intercepted");
        Assert(units.All(u=>u.OrderAgent.Issued.Count==1),"duplicate/recursive unit orders");
        Assert(TerrainProjection.Checks==2,"paths generated for unused candidates");
        Assert(units[0].OrderAgent.Issued[0].target.Position.x==200,"issued destination not formation centre");
        // Failed planning must leave every member unchanged.
        TerrainProjection.Valid=false;int before=units.Sum(u=>u.OrderAgent.Issued.Count);
        Assert((bool)Hooks.Call("FpsMove",p,new Vector3(300,0,100)),"failed plan should pass original");
        Assert(units.Sum(u=>u.OrderAgent.Issued.Count)==before,"partial orders on planning failure");
        Console.WriteLine("PASS FPS move: issuing-player isolation, centre, one order/unit, bounded projections, full fallback.");

        Reset();(p,units)=Squad(50);p.IsCommander=true;Select(p,Definition("commander"));
        Assert(Commander(p,units)&&units.All(u=>u.OrderAgent.Issued.Count==0),"selection silently enabled commander");
        Hooks.Call("SelectCommand",p,"/commanderformation on","commander");
        Assert(!Commander(p,units)&&units.All(u=>u.OrderAgent.Issued.Count==1),"50-unit group not applied exactly once");
        Assert(TerrainProjection.Checks==50,"candidate-dot path explosion");
        Hooks.Call("SelectCommand",p,"/commanderformation off","commander");
        Assert(Commander(p,units)&&units.All(u=>u.OrderAgent.Issued.Count==1),"commander off failed");
        Reset();(p,units)=Squad(51);p.IsCommander=true;Select(p,Definition("commander"));Hooks.Call("SelectCommand",p,"/commanderformation on","commander");
        Assert(Commander(p,units)&&units.All(u=>u.OrderAgent.Issued.Count==0)&&TerrainProjection.Checks==0,"51 must remain entirely vanilla");
        // Network context must be scoped and restored, including exceptions.
        object[] networkArgs={p.PlayerID,true,null};Hooks.Call("NetworkBegin",networkArgs);
        Assert(ReferenceEquals(Field("networkSender"),p),"authenticated sender missing");
        var error=new Exception("test");Assert(ReferenceEquals(Hooks.Call("NetworkEnd",error,networkArgs[2]),error)&&Field("networkSender")==null,"sender context leaked");
        Console.WriteLine("PASS commander: default off, explicit toggle, 50 accepted/51 wholly vanilla, scoped sender.");

        var route=new RouteHistory(Vector3.zero,Vector3.forward);route.Record(new Vector3(0,0,40));route.Record(new Vector3(40,0,40));
        Assert(route.Sample(20,10,out var rp)&&rp.x==10&&rp.z==20,"trailing corner slot not on old segment");
        Assert(route.Sample(60,10,out rp)&&rp.x==20&&rp.z==30,"local right after turn incorrect");
        for(int i=1;i<700;i++)route.Record(new Vector3(40+i*5,0,40));
        Assert(route.Count<=RouteHistory.MaxPoints&&!route.Sample(route.Distance-2001,0,out _),"route history unbounded");
        Reset();(p,units)=Squad(2);
        Select(p,new FormationDefinition("Curve","Sol","follow",true,5,new[]{new FormationSlot(-10,-30,"top",1,Array.Empty<string>()),new FormationSlot(10,-30,"top",1,Array.Empty<string>())}));
        foreach(var u in units)p.Group.IgnoreTask(u,AIGroup.EIgnoreTaskReason.FOLLOWING_LEADER);
        Hooks.Call("TickFollow");
        var followers=(IDictionary)Field("Followers");
        int Slot(Unit u)=>(int)followers[u].GetType().GetField("Slot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(followers[u]);
        int firstSlot=Slot(units[0]);
        Assert(units.All(u=>u.OrderAgent.Issued.Count==1),"follow did not issue");
        Assert(units[0].OrderAgent.Issued[0].target.Position.z==-15,"predicted centre must be +15 metres");
        foreach(var u in units)u.transform.position=u.OrderAgent.Issued.Last().target.Position;
        p.ControlledUnit.transform.position=new Vector3(0,0,20);Time.unscaledTime=1;Hooks.Call("TickFollow");
        foreach(var u in units)u.transform.position=u.OrderAgent.Issued.Last().target.Position;
        p.ControlledUnit.transform.position=new Vector3(20,0,20);Time.unscaledTime=2;Hooks.Call("TickFollow");
        Assert(Slot(units[0])==firstSlot,"turn rebuilt stable assignment");
        var a=units[0].OrderAgent.Issued.Last().target.Position;var b=units[1].OrderAgent.Issued.Last().target.Position;
        Assert((a-b).sqrMagnitude>=20,"bend destinations overlap");
        Console.WriteLine("PASS follow: arc-length corners, local right, +15m centre, bounded history, stable slots and separation.");

        Reset();(p,units)=Squad(2);Select(p,Definition("attack"));var target=TargetAt();
        Assert(!Stage(p,target)&&Operations==1,"attack staging failed");
        Hooks.Call("TickOperations");Assert(units.All(u=>u.OrderAgent.Issued.Count==1),"attack restored before arrival");
        foreach(var u in units)u.transform.position=u.OrderAgent.Issued.Last().target.Position+new Vector3(2,0,0);
        Hooks.Call("TickOperations");
        Assert(Operations==0&&units.All(u=>u.OrderAgent.Issued.Count==2&&u.OrderAgent.Issued.Last().definition==OrderDefinitionRegistry.Attack&&ReferenceEquals(u.OrderAgent.Issued.Last().target.Object,target)),"attack intent/arrival restore failed");
        Assert(!Stage(p,target),"restage");
        Time.unscaledTime=31;Hooks.Call("TickOperations");Assert(Operations==0&&units.All(u=>u.OrderAgent.Issued.Last().definition==OrderDefinitionRegistry.Attack),"timeout failed");
        Assert(!Stage(p,target),"restage2");
        Hooks.Call("FpsNewOrder",p);Time.unscaledTime=100;Hooks.Call("TickOperations");
        Assert(units.All(u=>u.OrderAgent.Issued.Last().definition==OrderDefinitionRegistry.Move)&&Operations==0,"newer player order overwritten");
        Assert(!Stage(p,target),"restage3");
        var newer=new OrderTarget(new Vector3(999,0,999));units[0].OrderAgent.IssueOrder(OrderDefinitionRegistry.Move,in newer,OrderIssueParams.Commanded(AgentMoveSpeed.Normal));
        Time.unscaledTime=140;Hooks.Call("TickOperations");
        Assert(units[0].OrderAgent.Issued.Last().target.Position.x==999&&units[1].OrderAgent.Issued.Last().definition==OrderDefinitionRegistry.Attack,"external order cancellation failed");
        Assert(!Stage(p,target),"restage4");Hooks.Call("PlayerChangedUnit",p,p.ControlledUnit,units[0]);
        Time.unscaledTime=180;Hooks.Call("TickOperations");Assert(Operations==0&&units.All(u=>u.OrderAgent.Issued.Last().definition==OrderDefinitionRegistry.Move),"direct control cancellation failed");
        Assert(!Stage(p,target),"restage5");target.Owner.IsDestroyed=true;Time.unscaledTime=220;Hooks.Call("TickOperations");
        Assert(Operations==0&&units.All(u=>u.OrderAgent.Issued.Last().definition==OrderDefinitionRegistry.Move),"dead target restored");
        target.Owner.IsDestroyed=false;Assert(!Stage(p,target),"restage6");units[0].IsDestroyed=true;
        units[1].transform.position=units[1].OrderAgent.Issued.Last().target.Position;Hooks.Call("TickOperations");
        Assert(Operations==0&&units[1].OrderAgent.Issued.Last().definition==OrderDefinitionRegistry.Attack,"dead member blocked restoration");
        
        Reset();(p,units)=Squad(2);Select(p,Definition("attack"));target=TargetAt();units[0].OrderAgent.CanAttack=false;
        Assert(!Stage(p,target),"support unit should not prevent staging");
        Time.unscaledTime=31;Hooks.Call("TickOperations");
        Assert(units[0].OrderAgent.Issued.Count==1&&units[1].OrderAgent.Issued.Last().definition==OrderDefinitionRegistry.Attack,"unsupported attack should not be invented for support unit");
        units[0].OrderAgent.CanAttack=true;
        var driver=new Unit{Team=Sol};units[0].Driver=driver;
        Assert(!Stage(p,target),"vehicle staging failed");
        var otherPlayer=new Player{Team=Sol};
        Hooks.Call("PlayerChangedUnit",otherPlayer,null,driver);
        Time.unscaledTime=70;Hooks.Call("TickOperations");
        Assert(units[0].OrderAgent.Issued.Last().definition==OrderDefinitionRegistry.Move&&units[1].OrderAgent.Issued.Last().definition==OrderDefinitionRegistry.Attack,"driver control did not cancel vehicle restoration");
        Console.WriteLine("PASS attack: target, tolerance, timeout, new orders, direct/driver control, target/member death and support units.");
        Console.WriteLine($"PASS {count} assertions. Game doubles exercise production algorithms; no live Unity/Harmony/pathfinding claim.");
    }
}
