using System.Reflection;
using System.Reflection.Emit;
namespace UnityEngine {
 public class Object { public string name="test-unit"; public static implicit operator bool(Object o)=>o!=null; }
 public class Transform { public Vector3 position; public Vector3 forward=Vector3.forward; }
 public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public static Vector2 zero=>default; public float sqrMagnitude=>x*x+y*y; public static float Dot(Vector2 a,Vector2 b)=>a.x*b.x+a.y*b.y; public static Vector2 operator -(Vector2 a,Vector2 b)=>new(a.x-b.x,a.y-b.y); public static Vector2 operator *(Vector2 a,float b)=>new(a.x*b,a.y*b); public float magnitude=>MathF.Sqrt(x*x+y*y); public static Vector2 operator +(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y); }
 public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} public static Vector3 up=>new(0,1,0); public static Vector3 forward=>new(0,0,1); public float sqrMagnitude=>x*x+y*y+z*z; public Vector3 normalized=>this*(1/MathF.Sqrt(sqrMagnitude)); public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z); public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z); public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b); public static Vector3 Cross(Vector3 a,Vector3 b)=>new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x); public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z; }
 public struct Bounds { public Vector3 extents; }
 public struct Quaternion { public static Quaternion LookRotation(Vector3 v)=>default; }
 public static class Mathf { public static float Clamp01(float v)=>Math.Clamp(v,0,1); public static float Max(float a,float b)=>MathF.Max(a,b); public static float Abs(float v)=>MathF.Abs(v); }
 public static class Time { public static float unscaledTime; }
 public enum QueryTriggerInteraction { Ignore }
 public static class Physics { public static bool Blocked; public static bool CheckBox(Vector3 p,Vector3 h,Quaternion q,int mask,QueryTriggerInteraction interaction)=>Blocked; }
}
namespace HarmonyLib {
 public class Harmony { public void Patch(MethodInfo m,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod transpiler=null){} public void UnpatchSelf(){} }
 public class HarmonyMethod:Attribute { public HarmonyMethod(Type t,string s){} }
 public static class AccessTools { public static MethodInfo Method(Type t,string s)=>t.GetMethod(s,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static); }
 public class CodeInstruction { public OpCode opcode; public object operand; public bool Calls(MethodInfo m)=>Equals(operand,m); }
}
namespace MelonLoader {
 [AttributeUsage(AttributeTargets.Assembly)] public class MelonInfoAttribute:Attribute {public MelonInfoAttribute(Type t,string n,string v,string a){}}
 [AttributeUsage(AttributeTargets.Assembly)] public class MelonGameAttribute:Attribute {public MelonGameAttribute(string a,string b){}}
 [AttributeUsage(AttributeTargets.Assembly)] public class MelonOptionalDependenciesAttribute:Attribute {public MelonOptionalDependenciesAttribute(string s){}}
 public class MelonMod { public HarmonyLib.Harmony HarmonyInstance=new(); public virtual void OnLateInitializeMelon(){} public virtual void OnUpdate(){} public virtual void OnSceneWasUnloaded(int i,string n){} public virtual void OnDeinitializeMelon(){} }
 public static class MelonLogger {public static void Msg(string s){} public static void Error(string s)=>Console.WriteLine(s);}
}
public class BaseGameObject:UnityEngine.Object {}
public class Target:UnityEngine.Object {}
public class Unit:BaseGameObject {
 public static List<Unit> Units=new(); public bool IsDestroyed,AIControlled=true,InCompartment,IsFlying; public Player ControlledBy; public Unit Driver; public UnityEngine.Transform transform=new(); public UnityEngine.Bounds PhysicalBounds=new(){extents=new(0.5f,0.5f,0.5f)}; public Silica.AI.AIGroup AIGroup; public Silica.AI.AIOrderProcessor OrderAgent; public Silica.AI.AIBaseAgent AIAgent=new();
 public Unit(){OrderAgent=new(){Owner=this};}
}
public class Player:UnityEngine.Object {public static List<Player> Players=new();public Unit ControlledUnit; public bool Admin=true;}
public static class Game {public static bool Server=true;public static bool GetIsServer()=>Server;}
public class GameMode:UnityEngine.Object {public static GameMode CurrentGameMode;public bool GetPlayerIsCommander(Player p)=>false;}
public static class GamePhysics {public const int TRACELAYERMASK_IGNOREDYNAMIC=0;}
public static class GameAI {public static bool Valid=true;public static Func<UnityEngine.Vector3,bool> Accept; public static bool GetPointValidForAI(UnityEngine.Vector3 p,int mask,out UnityEngine.Vector3 result,int a,int b,int c){result=p;return Valid&&(Accept==null||Accept(p));}}
namespace Silica.AI {
 public class AIGroup {public enum EIgnoreTaskReason {NONE,FOLLOWING_LEADER} public Unit Leader; public Dictionary<Unit,EIgnoreTaskReason> Reasons=new(); public bool IgnoresTask(Unit u,out EIgnoreTaskReason r)=>Reasons.TryGetValue(u,out r); public void IgnoreTask(Unit u,EIgnoreTaskReason reason)=>Reasons[u]=reason; public bool RemoveUnit(Unit u)=>Reasons.Remove(u); private void EvaluateLeaderFollow(){} }
 public struct OrderIssueParams { public bool BroadcastEvent,CalledByAI; public static OrderIssueParams Ai(int speed,bool broadcastEvent=true)=>new(){BroadcastEvent=broadcastEvent,CalledByAI=true}; }
 public class OrderDefinition {}
 public struct OrderTarget {}
 public class AIOrder {public uint Id;}
 public class AIMoveOrder:AIOrder {}
 public class AIOrderProcessor:UnityEngine.Object {public BaseGameObject Owner;public List<AIOrder> Orders=new();public List<UnityEngine.Vector3> Destinations=new();public bool Accept=true;public bool IssueResolvedOrder(UnityEngine.Vector3 p,Target target,in OrderIssueParams parameters){if(!Accept)return false;Destinations.Add(p);Orders.Clear();Orders.Add(new AIMoveOrder());typeof(Si_FollowFormation.FollowFormation).GetMethod("OtherOrder",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{this,true});return true;} public bool IssueOrder(OrderDefinition d,in OrderTarget t,in OrderIssueParams p)=>true;}
 public class AIBaseAgent:UnityEngine.Object {public AIPathfinding AgentPathfinding=new();public int MoveSpeed;}
 public class AIPathfinding:UnityEngine.Object {public Pathfinding.Seeker PathfindingSeeker=new();public bool IsCalculatingPath;public int Resets;public void CompleteMovementAndReset()=>Resets++;}
}
namespace Pathfinding {
 public enum PathCompleteState {Complete,Error,Partial}
 public class Path {public bool error;public PathCompleteState CompleteState=PathCompleteState.Complete;public List<UnityEngine.Vector3> vectorPath=new();}
 public class Constraint {public int graphMask,tags;public object traversalProvider;}
 public class Costs {public object traversalProvider,tagCostMultipliers,tagEntryCosts;}
 public class Seeker:UnityEngine.Object {public int Cancels; public void CancelCurrentPathRequest()=>Cancels++;public int graphMask,traversableTags;public object traversalProvider,tagCostMultipliers,tagEntryCosts;}
 public class ABPath:Path {public Action<Path> Callback;public Constraint traversalConstraint=new();public Costs traversalCosts=new();public static ABPath Construct(UnityEngine.Vector3 start,UnityEngine.Vector3 end,Action<Path> cb)=>new(){Callback=cb,vectorPath=new(){start,end}};}
 public static class AstarPath {public static Queue<ABPath> Pending=new();public static void StartPath(ABPath p)=>Pending.Enqueue(p);public static void Drain(bool fail=false){int limit=1000;while(Pending.Count>0){if(--limit==0)throw new Exception("Path callback loop");var p=Pending.Dequeue();p.error=fail;p.Callback(p);}}}
}
namespace SilicaAdminMod {
 public enum Power {Generic,Root,None}
 public static class PlayerExtension {public static bool IsAdmin(this Player p)=>p.Admin;public static Power GetAdminPowers(this Player p)=>p.Admin?Power.Generic:Power.None;}
 public static class AdminMethods {public static object AdminCommands=new();public static object FindAdminCommandFromString(string s)=>null;public static bool PowerInPowers(Power p,Power ps)=>ps!=Power.None;}
 public static class HelperMethods {public delegate void CommandCallback(Player p,string args);public static string LastReply;public static void RegisterAdminCommand(string s,CommandCallback c,Power p,string d){}public static void UnregisterAdminCommand(string s){}public static void SendChatMessageToPlayer(Player p,params string[] s)=>LastReply=string.Concat(s);}
}


