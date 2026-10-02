using System.Reflection;
using System.Reflection.Emit;
using Si_Formation;
internal static class Hooks
{
    internal static object Call(string name,params object[] args)=>typeof(Formations).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
}
namespace UnityEngine
{
    public class Object
    {
        private static int next;private readonly int id=++next;public string name="";
        public int GetInstanceID()=>id;
        public static implicit operator bool(Object o)=>o!=null;
    }
    public class Transform {public Vector3 position;public Vector3 forward=Vector3.forward;}
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>default;public static Vector3 up=>new(0,1,0);public static Vector3 forward=>new(0,0,1);
        public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>MathF.Sqrt(sqrMagnitude);public Vector3 normalized=>this/magnitude;
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
        public static Vector3 operator /(Vector3 a,float b)=>new(a.x/b,a.y/b,a.z/b);
        public static Vector3 Cross(Vector3 a,Vector3 b)=>new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
    }
    public static class Time {public static float unscaledTime;}
}
namespace HarmonyLib
{
    public class Harmony
    {public void Patch(MethodBase m,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod transpiler=null,HarmonyMethod finalizer=null,HarmonyMethod ilmanipulator=null){}public void UnpatchSelf(){}}
    public class HarmonyMethod {public HarmonyMethod(Type t,string n){}}
    public class CodeInstruction {public OpCode opcode;public object operand;public bool Calls(MethodInfo m)=>Equals(operand,m);}
    public static class AccessTools {public static MethodInfo Method(Type t,string s)=>t.GetMethod(s,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static);}
}
namespace MelonLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]public class MelonInfoAttribute:Attribute {public MelonInfoAttribute(Type t,string n,string v,string a){}}
    [AttributeUsage(AttributeTargets.Assembly)]public class MelonGameAttribute:Attribute {public MelonGameAttribute(string a,string b){}}
    [AttributeUsage(AttributeTargets.Assembly)]public class MelonOptionalDependenciesAttribute:Attribute {public MelonOptionalDependenciesAttribute(string a){}}
    public class MelonMod {public HarmonyLib.Harmony HarmonyInstance=new();public virtual void OnLateInitializeMelon(){}public virtual void OnUpdate(){}public virtual void OnSceneWasInitialized(int i,string n){}public virtual void OnSceneWasUnloaded(int i,string n){}public virtual void OnDeinitializeMelon(){}}
    public static class MelonLogger {public static void Msg(string s){}public static void Error(string s)=>Console.WriteLine(s);}
    public class MelonPreferences_Entry<T> {public T Value;}
    public class MelonPreferences_Category {public MelonPreferences_Entry<T> CreateEntry<T>(string k,T v,string description=null)=>new(){Value=v};public void SaveToFile(bool b){}}
    public static class MelonPreferences {public static MelonPreferences_Category CreateCategory(string n)=>new();}
}
namespace MelonLoader.Utils {public static class MelonEnvironment {public static string UserDataDirectory="";}}
public class Team:UnityEngine.Object {public string TeamShortName;}
public class ObjectInfo:UnityEngine.Object {}
public struct NetworkID {public int Id;}
public class NetworkComponent:UnityEngine.Object {public bool IsValid=true;}
public class BaseGameObject:UnityEngine.Object
{
    public bool IsDestroyed;public Team Team;public UnityEngine.Transform transform=new();public ObjectInfo ObjectInfo=new();
    public Silica.AI.AIOrderProcessor OrderAgent;public NetworkComponent NetworkComponent=new();
}
public class Target:UnityEngine.Object
{
    public BaseGameObject Owner=new();public UnityEngine.Transform transform=>Owner.transform;public bool IsDestroyed=>Owner.IsDestroyed;public NetworkComponent NetworkComponent=>Owner.NetworkComponent;
}
public class Unit:BaseGameObject
{
    public bool AIControlled=true,InCompartment,IsFlying;public Player ControlledBy;public Unit Driver;public bool PlayerControlled=>ControlledBy;
    public float PhysicalRadius=1;public Silica.AI.AIBaseAgent AIAgent=new();public Silica.AI.AIGroup AIGroup;
    public Unit(){OrderAgent=new(){Owner=this};}
}
public class Player:UnityEngine.Object
{
    public static List<Player> Players=new();public static Player CurrentPlayer;
    public NetworkID PlayerID;public Unit ControlledUnit;public bool IsCommander,Admin=true;public Team Team;
    public Silica.AI.AIGroup Group=>ControlledUnit?.AIGroup;
    public static Player FindPlayer(NetworkID id)=>Players.FirstOrDefault(p=>p.PlayerID.Id==id.Id);
}
public static class Game {public static bool GetIsServer()=>true;}
public static class GameEvents {public static Action<Player,Unit,Unit> OnPlayerChangedUnit;public static Action<Player,Team,Team> OnPlayerChangedTeam;}
public static class NetworkLayer {private static void ProcessMessage(NetworkID remoteID,bool isServer){}}
public class StrategyMode {public static void PerformMoveAttack(List<BaseGameObject> objects,UnityEngine.Vector3 worldPosition,Target target,Silica.AI.AgentMoveSpeed moveSpeed,bool isAttack,bool remote=false,bool queueOrder=false){}}
public class FPSCommanding
{
    private void MoveServer(Player player,UnityEngine.Vector3 position){} private void AttackServer(Player player,Target target){}
    private void StopAllServer(Player player){} private void StopUnitServer(Player player,Target target){}
    private void FollowAllServer(Player player){} private void UnitFollowServer(Player player,Target target){}
    private void ProtectServer(Player player,Target target){} private void KickFromGroupServer(Player player,Target target){}
}
namespace Silica.AI
{
    public enum AgentMoveSpeed {Normal,Fast,Stealth}
    public class AIBaseAgent:UnityEngine.Object {public AIPathfinding AgentPathfinding=new();public AgentMoveSpeed MoveSpeed;}
    public class AIVehicleAgent:AIBaseAgent {public bool CanRepair;}
    public class AIPathfinding:UnityEngine.Object {public Seeker PathfindingSeeker=new();public int Resets;public void CompleteMovementAndReset()=>Resets++;}
    public class Seeker:UnityEngine.Object {public void CancelCurrentPathRequest(){}}
    public class AIGroup
    {
        public enum EIgnoreTaskReason {NONE,FOLLOWING_LEADER}
        public List<Unit> Units=new();public Unit Leader;public Dictionary<Unit,EIgnoreTaskReason> Reasons=new();
        private object task;public object Task {get=>task;set{Hooks.Call("GroupTaskChanging",this);task=value;}}
        public bool IgnoresTask(Unit u,out EIgnoreTaskReason reason)=>Reasons.TryGetValue(u,out reason);
        public bool AllStopped()=>Units.Where(u=>!u.PlayerControlled).All(u=>Reasons.TryGetValue(u,out var r)&&r==EIgnoreTaskReason.NONE);
        public void StopIgnoringTask(Unit u)=>Reasons.Remove(u);
        public void IgnoreTask(Unit unit,EIgnoreTaskReason reason){Reasons[unit]=reason;Hooks.Call("FollowChanged",unit);}
        public void RemoveUnit(Unit unit){Hooks.Call("GroupRemoving",unit);Units.Remove(unit);}
        private void EvaluateLeaderFollow(){}
    }
    public static class UnitCohesionGroup {public static void Detach(Unit u){}}
    public class OrderDefinition:UnityEngine.Object {}
    public static class OrderDefinitionRegistry {public static OrderDefinition Move=new(),Attack=new();}
    public readonly struct OrderTarget {public readonly UnityEngine.Vector3 Position;public readonly Target Object;public OrderTarget(UnityEngine.Vector3 p,Target target=null){Position=p;Object=target;}}
    public struct OrderIssueParams
    {
        public bool CalledByAI,BroadcastEvent;public AgentMoveSpeed MoveSpeed;
        public static OrderIssueParams Ai(AgentMoveSpeed speed,bool broadcastEvent=true)=>new(){MoveSpeed=speed,CalledByAI=true,BroadcastEvent=broadcastEvent};
        public static OrderIssueParams Commanded(AgentMoveSpeed speed)=>new(){MoveSpeed=speed,BroadcastEvent=true};
    }
    public class AIOrder {public uint Id;}
    public class AIMoveOrder:AIOrder {}
    public class AIAttackOrder:AIOrder {}
    public class AIOrderProcessor:UnityEngine.Object
    {
        public BaseGameObject Owner;public List<AIOrder> Orders=new();public List<(OrderDefinition definition,OrderTarget target)> Issued=new();
        public bool Accept=true,CanAttack=true;private uint next;
        public bool CanIssueOrder(OrderDefinition definition,in OrderTarget target)=>Accept&&(definition!=OrderDefinitionRegistry.Attack||CanAttack);
        public bool IssueOrder(OrderDefinition definition,in OrderTarget target,in OrderIssueParams issueParams)
        {
            if(!Accept)return false;
            Orders.Clear();AIOrder o=definition==OrderDefinitionRegistry.Move?new AIMoveOrder():new AIAttackOrder();o.Id=++next;Orders.Add(o);
            Issued.Add((definition,target));Hooks.Call("OtherOrder",this,true);return true;
        }
        public bool IssueResolvedOrder(UnityEngine.Vector3 p,Target t,in OrderIssueParams par)=>IssueOrder(OrderDefinitionRegistry.Move,new OrderTarget(p,t),par);
    }
}
namespace Si_Formation
{
    internal static class TerrainProjection
    {
        internal static int Checks;internal static bool Valid=true;
        internal static bool TryProject(Unit u,UnityEngine.Vector3 desired,out UnityEngine.Vector3 point){Checks++;point=desired;return Valid;}
    }
}
namespace SilicaAdminMod
{
    public enum Power {Generic}
    public static class PlayerExtension {public static bool IsAdmin(this Player p)=>p.Admin;public static Power GetAdminPowers(this Player p)=>Power.Generic;}
    public static class AdminMethods {public static object AdminCommands=new();public static object FindAdminCommandFromString(string s)=>null;public static bool PowerInPowers(Power a,Power b)=>true;}
    public static class HelperMethods
    {
        public delegate void CommandCallback(Player p,string args);public static string LastReply;
        public static void RegisterAdminCommand(string s,CommandCallback cb,Power p,string d){}public static void UnregisterAdminCommand(string s){}
        public static void SendChatMessageToPlayer(Player p,string text)=>LastReply=text;
    }
    public static class PlayerMethods
    {
        public static object PlayerCommands=new();public static object FindPlayerCommandFromString(string n)=>null;
        public static void RegisterPlayerCommand(string s,HelperMethods.CommandCallback cb,bool b){}public static void UnregisterPlayerCommand(string s){}
    }
}
