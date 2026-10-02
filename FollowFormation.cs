using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MelonLoader;
using Silica.AI;
using SilicaAdminMod;
using UnityEngine;

[assembly: MelonInfo(typeof(Si_Formation.Formations), "Si_Formation", "2.8.0", "Local")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Formation
{
    public sealed partial class Formations : MelonMod
    {
        internal const int GroupLimit=50, ProjectionAttempts=6;
        private static readonly FormationCatalog Catalog=new FormationCatalog();
        private sealed class Selection
        {
            internal Team Team;
            internal bool PersonalEnabled,CommanderRole;
            internal readonly Dictionary<string,FormationDefinition> Functions=new Dictionary<string,FormationDefinition>();
        }
        private static readonly Dictionary<Player,Selection> Selections=new Dictionary<Player,Selection>();
        private static readonly HashSet<Unit> Superseded=new HashSet<Unit>();
        private static bool Ready, Enabled, Faulted, debug;
        private static int generatedDepth, traceBudget=80;
        private static float nextCheck;
        private static MelonPreferences_Category prefs;

        private static Player networkSender;
        private readonly List<string> registered=new List<string>();
        private static bool Running=>Ready&&Enabled&&!Faulted&&Game.GetIsServer();

        public override void OnLateInitializeMelon()
        {
            try
            {
                if(AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="Si_FollowFormation"||a.GetName().Name=="Si_Formations"))
                    throw new InvalidOperationException("Remove Mods/Si_FollowFormation.dll and Mods/Si_Formations.dll before loading Si_Formation.dll; both must not run together.");
                if(AdminMethods.AdminCommands==null||PlayerMethods.PlayerCommands==null)
                    throw new InvalidOperationException("Si-AdminMod is unavailable.");
                foreach(string command in new[]{"formation","formationsize","formationsizeglobal","formationsizecommander","disband","followformation","moveformation","commanderformation","attackformation"})
                    if(AdminMethods.FindAdminCommandFromString(command)!=null||PlayerMethods.FindPlayerCommandFromString(command)!=null)
                        throw new InvalidOperationException("Command already registered: "+command);
                Catalog.Load(Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory,"Formations_cfg"),MelonLogger.Msg);
                serverThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
                prefs=MelonPreferences.CreateCategory("Si_Formation");
                prefs.SetFilePath(Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory,"Formations_cfg","si_formation.cfg"));
                enabledDefault=prefs.CreateEntry("Enabled",true,description:"Default master state restored each match; /formation on|off is match-only");
                groupLimitDefault=prefs.CreateEntry("GroupLimit",BuiltInLimit,description:"Default FPS group maximum including leader, 1..50; /formationsize overrides are match-only");
                debugDefault=prefs.CreateEntry("Debug",false,description:"Si_Formation bounded diagnostic logging");
                RegisterFormationDefaults();
                RegisterCommanderOptions();
                commanderOrderLimit=prefs.CreateEntry("CommanderOrderLimit",75,description:"Commander formation capacity, 1..100; oversized/unsupported orders stay native; independent of FPS GroupLimit");
                if(!ValidCommanderLimit(commanderOrderLimit.Value)){MelonLogger.Warning("Invalid CommanderOrderLimit; using 75.");commanderOrderLimit.Value=75;}
                ResetMatchSettings();
                Patch(typeof(Event_Chat),"FireOnRequestPlayerChatEvent",prefix:nameof(MenuChat));
                var hostPrefix=new HarmonyMethod(typeof(Formations),nameof(HostMenuChat)) {priority=HarmonyLib.Priority.First};
                HarmonyInstance.Patch(AccessTools.Method(typeof(Player),"SendChatMessage"),prefix:hostPrefix);
                PatchAdminInviteLimit();
                Patch(typeof(FPSCommanding),"InviteToGroupServer",prefix:nameof(RecruitAuthorized),transpiler:nameof(InviteLimit));
                Patch(typeof(AIGroup),"AddUnit",prefix:nameof(GroupAdding));
                Patch(typeof(AIGroup),"EvaluateLeaderFollow",transpiler:nameof(PatchFollow));
                Patch(typeof(AIOrderProcessor),"IssueOrder",prefix:nameof(BeforeOtherOrder),postfix:nameof(OtherOrder));
                Patch(typeof(Unit),"get_MoveSpeedLimit",postfix:nameof(LockedSpeedLimit));
                Patch(typeof(AIGroup),"IgnoreTask",postfix:nameof(FollowChanged));
                Patch(typeof(AIGroup),"RemoveUnit",prefix:nameof(GroupRemoving));
                Patch(typeof(AIGroup),"set_Task",prefix:nameof(GroupTaskChanging));
                Patch(typeof(FPSCommanding),"MoveServer",prefix:nameof(FpsMove));
                Patch(typeof(FPSCommanding),"AttackServer",prefix:nameof(FpsAttack));
                Patch(typeof(FPSCommanding),"StopAllServer",prefix:nameof(FpsToggleGroup));
                Patch(typeof(FPSCommanding),"FollowAllServer",prefix:nameof(FpsToggleGroup));
                foreach(string method in new[]{"StopUnitServer","UnitFollowServer","ProtectServer","KickFromGroupServer"})
                    Patch(typeof(FPSCommanding),method,prefix:nameof(FpsNewOrder));
                Patch(typeof(StrategyMode),"PerformMoveAttack",prefix:nameof(CommanderMove));
                Patch(typeof(NetworkLayer),"ProcessMessage",prefix:nameof(NetworkBegin),finalizer:nameof(NetworkEnd));
                GameEvents.OnPlayerChangedUnit+=PlayerChangedUnit;
                GameEvents.OnPlayerChangedTeam+=PlayerChangedTeam;
                GameEvents.OnGameStarted+=RoundStarted;
                GameEvents.OnGameEnded+=CommanderRoundEnded;
                PlayerMethods.RegisterPlayerCommand("formation",MasterCommand,true);
                PlayerMethods.RegisterPlayerCommand("formationsize",SizeCommand,true);
                PlayerMethods.RegisterPlayerCommand("formationsizeglobal",GlobalSizeCommand,true);
                PlayerMethods.RegisterPlayerCommand("disband",DisbandCommand,true);
                PlayerMethods.RegisterPlayerCommand("formationsizecommander",CommanderSizeCommand,true);
                registered.AddRange(new[]{"formation","formationsize","formationsizeglobal","formationsizecommander","disband"});
                foreach(string function in new[]{"move","follow","commander","attack"})
                {
                    string captured=function;
                    PlayerMethods.RegisterPlayerCommand(function+"formation",(p,a)=>SelectCommand(p,a,captured),true);
                    registered.Add(function+"formation");
                }
                Ready=true;
                MelonLogger.Msg("Si_Formation ready. Master "+(Enabled?"ON":"OFF")+"; team/role formation defaults configured in preferences. Cached JSON is read only at startup.");
            }
            catch(Exception e) { Fault(e); HarmonyInstance.UnpatchSelf(); }
        }
        private void Patch(Type type,string method,string prefix=null,string postfix=null,string transpiler=null,string finalizer=null)
        {
            var original=AccessTools.Method(type,method)??throw new MissingMethodException(type.FullName,method);
            HarmonyMethod H(string n)=>n==null?null:new HarmonyMethod(typeof(Formations),n);
            HarmonyInstance.Patch(original,H(prefix),H(postfix),H(transpiler),H(finalizer),null);
        }
        private static void NetworkBegin(NetworkID remoteID,bool isServer,out Player __state)
        { __state=networkSender; networkSender=isServer?Player.FindPlayer(remoteID):null; }
        private static Exception NetworkEnd(Exception __exception,Player __state)
        { networkSender=__state; return __exception; }

        private static string TeamName(Team team)
        {
            if(!team) return null;
            switch(team.name)
            {
                case "Team_Human_Sol":return "Sol";
                case "Team_Human_Centauri":return "Centauri";
                case "Team_Alien":return "Alien";
                default:return new[]{"Sol","Centauri","Alien"}.Contains(team.TeamShortName)?team.TeamShortName:null;
            }
        }
        private static Selection For(Player player)
        {
            if(!Selections.TryGetValue(player,out Selection s)||s.Team!=player.Team||s.CommanderRole!=player.IsCommander)
            {
                ReleasePlayerControl(player);Menus.Remove(player);
                Selections[player]=s=new Selection {Team=player.Team,CommanderRole=player.IsCommander};
                ApplyFormationDefaults(s);
            }
            return s;
        }
        private static bool FunctionActive(Player p,string function)
            =>p&&Running&&For(p).PersonalEnabled&&(p.IsCommander?function=="commander":function!="commander")&&Selected(p,function)!=null;
        private static FormationDefinition Selected(Player p,string function)
        {
            if(!p) return null;
            var s=For(p);
            return s.Functions.TryGetValue(function,out FormationDefinition f)&&f.Team==TeamName(p.Team)&&f.Function==function?f:null;
        }
        private static void RoundStarted(GameMode mode)
        {
            speedCapOverride=null;softDelayOverride=lockDelayOverride=speedLockDelayOverride=null;
            if(!Ready||!Game.GetIsServer())return;
            ClearFollow();ClearOrders();Selections.Clear();Menus.Clear();ResetMatchSettings();nextCheck=0;
            EnforceLimits();
            foreach(var p in Player.Players)if(p)For(p);
        }
        private static void MoveReply(Player p,string text)=>Trace(text);
        private static void Reply(Player p,string text)
        { if(p) HelperMethods.SendChatMessageToPlayer(p,"Formations: "+text); else MelonLogger.Msg("Formations: "+text); }
        private static void SelectCommand(Player p,string args,string function)
        {
            if(!Ready||!Game.GetIsServer()||!p) { Reply(p,"requires an initialized server and an issuing player."); return; }
            if((function=="commander")!=p.IsCommander){Reply(p,"this formation function is not available in your current role.");return;}
            if(!CommandArgument.TryParse(args,out string name,out bool quoted)) { Reply(p,"invalid name/quotes."); return; }
            if(function=="commander"&&!quoted&&name.Equals("status",StringComparison.OrdinalIgnoreCase))
            {CommanderStatus(p);return;}
            if(function=="commander"&&!quoted&&(name.Equals("on",StringComparison.OrdinalIgnoreCase)||name.Equals("off",StringComparison.OrdinalIgnoreCase)))
            {
                if(name.Equals("off",StringComparison.OrdinalIgnoreCase))DisablePersonal(p);
                else if(Selected(p,"commander")==null){Reply(p,"select a custom commander formation first.");return;}
                else For(p).PersonalEnabled=true;
                CommanderStatus(p); return;
            }
            if(name.Length==0) { Reply(p,"usage: /"+function+"formation \"name\""+(function=="commander"?" | on | off | status":"")); return; }
            string team=TeamName(p.Team);
            if(team==null||!Catalog.TryGet(team,function,name,out FormationDefinition f))
            {
                Reply(p,"missing "+function+" formation '"+name+"' for "+(team??"unsupported team")+". Available: "+
                    string.Join(", ",Catalog.Definitions.Values.Where(v=>v.Team==team&&v.Function==function).Select(v=>v.Name))); return;
            }
            ApplyCustom(p,function,f);
        }
        private static string SelectedName(Player p,string function)=>Selected(p,function)?.Name??"Default (native)";
        private static void CommanderStatus(Player p)
        {
            Reply(p,"commander personal "+(For(p).PersonalEnabled?"ON":"OFF")+"; effective "+(FunctionActive(p,"commander")?"ON":"OFF")+"; selected: "+SelectedName(p,"commander")+"; capacity "+CommanderOrderLimit);
        }
        private static void ReportMaster(Player p)
        {
            Reply(p,"Si_Formation global requested "+(Enabled?"ON":"OFF")+"; effective "+(Running?"ON":"OFF")+"; "+Catalog.Definitions.Count+" cached definitions"+
                (!Ready?"; initialization failed":Faulted?"; faulted (see server log)":!Game.GetIsServer()?"; server unavailable":""));
        }
        private static void MasterCommand(Player p,string args)
        {
            if(!CommandArgument.TryParse(args,out string action,out bool quoted)||quoted){Reply(p,"usage: /formation on|off|status");return;}
            if(action.Length==0){ToggleMenu(p);return;}
            if(action.Equals("lock",StringComparison.OrdinalIgnoreCase)||action.Equals("unlock",StringComparison.OrdinalIgnoreCase))
            {LockCommand(p,action);return;}
            if(action.Equals("status",StringComparison.OrdinalIgnoreCase)){PersonalStatus(p);return;}
            if(!action.Equals("on",StringComparison.OrdinalIgnoreCase)&&!action.Equals("off",StringComparison.OrdinalIgnoreCase))
            {HandleShortcut(p,action);return;}
            if(!IsAdministrator(p)){Reply(p,"permission denied");return;}
            if(!Ready||!Game.GetIsServer()){Reply(p,"not initialized as server");return;}
            adminGlobal=true;
            SetGlobal(action.Equals("on",StringComparison.OrdinalIgnoreCase));
            ReportMaster(p);
        }
        private static IEnumerable<CodeInstruction> PatchFollow(IEnumerable<CodeInstruction> instructions)
        {
            var code=instructions.ToList();
            var original=AccessTools.Method(typeof(AIOrderProcessor),"IssueResolvedOrder");
            var replacement=AccessTools.Method(typeof(Formations),nameof(VanillaFollow));
            int count=0;
            foreach(var i in code) if(i.Calls(original)) {i.opcode=OpCodes.Call;i.operand=replacement;count++;}
            if(count!=2) throw new InvalidOperationException("Expected two vanilla Follow repath sites.");
            return code;
        }
        private static bool VanillaFollow(AIOrderProcessor processor,Vector3 position,Target target,in OrderIssueParams parameters)
        {
            if(Running&&Enabled&&processor.Owner is Unit unit&&Followers.TryGetValue(unit,out Follower s)&&Current(s)&&s.Active) return true;
            generatedDepth++;
            try {return processor.IssueResolvedOrder(position,target,in parameters);}
            finally {generatedDepth--;}
        }
        private static void BeforeOtherOrder(AIOrderProcessor __instance)
        {
            // Restore before the replacement order initializes its own speed settings.
            if(generatedDepth==0&&__instance.Owner is Unit u)ReleaseLockedUnit(u);
        }
        private static void OtherOrder(AIOrderProcessor __instance,bool __result)
        {
            if(generatedDepth>0||!__result||!(__instance.Owner is Unit u)) return;
            CancelUnit(u);
            Followers.Remove(u);
            if(u.AIGroup!=null&&u.AIGroup.IgnoresTask(u,out var reason)&&reason==AIGroup.EIgnoreTaskReason.FOLLOWING_LEADER) Superseded.Add(u);
        }
        private static void FollowChanged(Unit unit)
        {
            if(generatedDepth>0||!unit) return;
            CancelUnit(unit); Followers.Remove(unit); Superseded.Remove(unit);
            // Other members retain their saved Follow slots; only this member changed task.
        }
        private static void GroupRemoving(Unit unit)
        {if(!unit)return;CancelUnit(unit);Followers.Remove(unit);Superseded.Remove(unit);}
        private static void GroupTaskChanging(AIGroup __instance)
        {
            if(generatedDepth>0) return;
            foreach(var u in __instance.Units.ToArray()) {CancelUnit(u);Followers.Remove(u);}
        }
        // Both server methods originate from the client's MultiTap toggle. Decide from the
        // authoritative group state, not potentially stale client ignore-task flags.
        private static bool FpsToggleGroup(Player player)
        {
            if(!FunctionActive(player,"follow")||generatedDepth>0||!player||player.IsCommander||player.Group==null||
                !player.ControlledUnit||player.Group.Leader!=player.ControlledUnit) return true;
            if(networkSender&&networkSender!=player)return true;
            var group=player.Group;
            bool regroup=group.AllStopped();
            CancelPlayer(player);
            ResetLeader(player.ControlledUnit);
            generatedDepth++;
            try
            {
                group.Task=null;
                foreach(var unit in group.Units.ToArray())
                {
                    if(!unit||unit.IsDestroyed||unit.PlayerControlled||(unit.Driver&&unit.Driver.ControlledBy)||!unit.AIAgent)continue;
                    Followers.Remove(unit);Superseded.Remove(unit);UnitCohesionGroup.Detach(unit);
                    var reason=regroup?AIGroup.EIgnoreTaskReason.FOLLOWING_LEADER:AIGroup.EIgnoreTaskReason.NONE;
                    bool repeated=group.IgnoresTask(unit,out var previous)&&previous==reason;
                    // Stop cancels pending path work before returning from this command, not on the next poll.
                    if(!regroup&&unit.AIAgent.AgentPathfinding)
                    {
                        unit.AIAgent.AgentPathfinding.PathfindingSeeker?.CancelCurrentPathRequest();
                        unit.AIAgent.AgentPathfinding.CompleteMovementAndReset();
                    }
                    group.IgnoreTask(unit,reason); // native flag synchronization and native Stop/Follow order
                    if(repeated&&unit.OrderAgent)
                    {
                        var parameters=OrderIssueParams.Commanded(unit.AIAgent.MoveSpeed);
                        if(regroup)unit.OrderAgent.IssueResolvedOrder(player.ControlledUnit.transform.position,null,in parameters);
                        else unit.OrderAgent.IssueOrder(OrderDefinitionRegistry.Stop,in OrderTarget.None,in parameters);
                    }
                }
                if(regroup)Frames[player.ControlledUnit]=new FollowFrame {LastSample=player.ControlledUnit.transform.position};
                Trace(regroup?"FPS all regroup: selected JSON follow armed":"FPS all stop: paths and formation state cleared");
            }
            finally {generatedDepth--;}
            return false;
        }
        private static void FpsNewOrder(Player player,Target target)
        {
            if(generatedDepth!=0||!Game.GetIsServer()||!player||(networkSender&&networkSender!=player))return;
            if(target&&target.Owner is Unit u&&u.Team==player.Team){CancelUnit(u);Followers.Remove(u);}
        }
        private static void PlayerChangedUnit(Player player,Unit oldUnit,Unit newUnit)
        {
            CancelPlayer(player); if(oldUnit) ResetLeader(oldUnit);
            if(newUnit)
            {
                CancelUnit(newUnit);Followers.Remove(newUnit);
                foreach(var o in Operations) o.Members.RemoveAll(p=>p.Unit&&p.Unit.Driver==newUnit);
                Operations.RemoveAll(o=>o.Members.Count==0&&!PlacementRetries.Any(b=>b.Attack==o&&b.Pending.Count>0)&&!SoftMotions.Any(m=>m.Batch.Attack==o));
                foreach(var s in Followers.Values.Where(s=>s.Unit&&s.Unit.Driver==newUnit).ToArray()) Followers.Remove(s.Unit);
            }
        }
        private static void PlayerChangedTeam(Player player,Team oldTeam,Team newTeam)
        {ReleasePlayerControl(player);Menus.Remove(player);Selections.Remove(player);}
        private static bool Available(Unit u)=>u&&!u.IsDestroyed&&u.AIControlled&&!u.ControlledBy&&!u.InCompartment&&!u.IsFlying&&
            !u.PlayerControlled&&!u.IsFlyingType&&!(u.Driver&&u.Driver.ControlledBy)&&u.OrderAgent&&u.AIAgent&&u.AIAgent.AgentPathfinding&&u.AIAgent.AgentPathfinding.PathfindingSeeker;
        private static Vector3 Flat(Vector3 v) {v.y=0;return v;}
        private static Vector3 Heading(Vector3 v)=>Flat(v).sqrMagnitude>0.01f?Flat(v).normalized:Vector3.forward;
        private static float DistanceSq(Vector3 a,Vector3 b)=>Flat(a-b).sqrMagnitude;
        private static void Trace(string text) {if(debug&&traceBudget-->0)MelonLogger.Msg("[Formations] "+text);}
        private static void Fault(Exception e)
        {
            Faulted=true;Enabled=false;ClearFollow();ClearOrders();
            MelonLogger.Error("Si_Formation disabled after error: "+e);
        }
        public override void OnUpdate()
        {
            try { ExportEditorFootprints(); TickLocks(); if(Running){TickPlacementRetries();TickSoftMotions();RefreshDepartureSpeeds();TickFollowSearches();TickFollowDepartures();} } catch(Exception e) { Fault(e); }
            if(Time.unscaledTime<nextCheck) return;
            nextCheck=Time.unscaledTime+1;
            foreach(var p in Menus.Keys.ToArray())if(!p||!Player.Players.Contains(p))Menus.Remove(p);
            if(!Running) return;
            try
            {
                foreach(var p in Selections.Keys.ToArray()) if(!p||!Player.Players.Contains(p)||Selections[p].Team!=p.Team||Selections[p].CommanderRole!=p.IsCommander) {ReleasePlayerControl(p);Selections.Remove(p);Menus.Remove(p);}
                EnforceLimits();PruneLockHeadings();
                TickOperations();
                if(Enabled) TickFollow();
            }
            catch(Exception e){Fault(e);}
        }
        public override void OnSceneWasInitialized(int buildIndex,string sceneName)
        {
            // GameEvents clears its static delegates during map initialization.
            GameEvents.OnPlayerChangedUnit-=PlayerChangedUnit;GameEvents.OnPlayerChangedUnit+=PlayerChangedUnit;
            GameEvents.OnPlayerChangedTeam-=PlayerChangedTeam;GameEvents.OnPlayerChangedTeam+=PlayerChangedTeam;
            GameEvents.OnGameStarted-=RoundStarted;GameEvents.OnGameStarted+=RoundStarted;
            GameEvents.OnGameEnded-=CommanderRoundEnded;GameEvents.OnGameEnded+=CommanderRoundEnded;
        }
        public override void OnSceneWasUnloaded(int buildIndex,string sceneName)
        {Followers.Clear();Frames.Clear();Superseded.Clear();Selections.Clear();ClearOrders();Menus.Clear();networkSender=null;}
        public override void OnDeinitializeMelon()
        {
            ClearFollow();ClearOrders();Selections.Clear();Menus.Clear();
            GameEvents.OnPlayerChangedUnit-=PlayerChangedUnit;GameEvents.OnPlayerChangedTeam-=PlayerChangedTeam;
            GameEvents.OnGameStarted-=RoundStarted;
            GameEvents.OnGameEnded-=CommanderRoundEnded;
            foreach(var cmd in registered)PlayerMethods.UnregisterPlayerCommand(cmd);
            HarmonyInstance.UnpatchSelf();Ready=false;
        }
    }
}
