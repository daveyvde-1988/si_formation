using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Silica.AI;
using SilicaAdminMod;
using UnityEngine;

[assembly: MelonInfo(typeof(Si_Formation.Formations), "Si_Formation", "3.17.0", "Local")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]
namespace Si_Formation
{
    public sealed partial class Formations : MelonMod
    {
        private sealed class Settings
        {
            internal int Spacing=DefaultSpacing, DragSpacing=DefaultDragSpacing, Rally=DefaultRally, SizeInfluence=DefaultSizeInfluence, AlienSpread=DefaultAlienSpread, FpsSpacing=DefaultFpsSpacing, FpsDistance=DefaultFpsDistance, FpsAllowance=DefaultFpsAllowance;
            internal int LesserSpawnAirSpacing=DefaultLesserSpawnAirSpacing;
            internal bool MovementFix=DefaultMovementFix;
        }
        private struct MoveState { internal bool Independent; internal LayoutContext Layout; internal CohesionDispatch Cohesion; internal MovementCommandDiagnostics Diagnostics; internal WaypointDispatch Waypoint; }
        private const int DefaultSpacing=35, DefaultDragSpacing=15, DefaultRally=20, DefaultSizeInfluence=0, DefaultAlienSpread=45, DefaultFpsSpacing=35, DefaultFpsDistance=10, DefaultFpsAllowance=15;
        private const int DefaultLesserSpawnAirSpacing=50,MaximumLesserSpawnAirSpacing=200;
        private const bool DefaultMovementFix=true;
        private const bool DefaultDestinationLocalCohesion=true;
        private const float DefaultHumanWaypointWait=5,DefaultAlienWaypointWait=7,DefaultAlienCombatWait=10;
        private static readonly Settings ServerSettings=new Settings();
        private static bool ready;
        private static bool Active=>ready&&ServerEnabled;
        private static bool activeIndependent;
        internal static float Multiplier(int percent)=>SilicaFormationShared.CommanderLineLayout.Multiplier(percent);
        private static Player Commander(Team team)=>Player.Players.FirstOrDefault(p=>p&&p.Team==team&&p.IsCommander);
        public override void OnLateInitializeMelon()
        {
            try
            {
                foreach(string name in new[]{"formation","formationspace","dragspace","rallyspace","scout","movementfix","pathshortcut","sizeinfluence","alienspread","lesserspawnairspace","fpsspace","fpsdistance","fpsallowance","groupcohesion","cohesion","aliengroupcohesion","aliencohesion","cohesiondebug","travelcoverdisabler","destinationcohesion","waypointwait"})
                    if(PlayerMethods.FindPlayerCommandFromString(name)!=null||AdminMethods.FindAdminCommandFromString(name)!=null)
                        throw new InvalidOperationException("Command already registered: "+name+". Remove the old formation mod.");
                LoadServerSettings();
                LoadTravelCoverSettings();
                LoadCohesionSettings();
                HarmonyInstance.Patch(AccessTools.Method(typeof(StrategyMode),"PerformMoveAttack"),
                    prefix:new HarmonyMethod(typeof(Formations),nameof(BeginMove)){priority=HarmonyLib.Priority.First},
                    finalizer:new HarmonyMethod(typeof(Formations),nameof(EndMove)));
                InstallRally();
                InstallMovementFix();
                InstallPathfindingShortcut();
                InstallSpacing();
                InstallFps();
                InstallCohesion();
                InstallReverseCommandRouting();
                InstallMovementDiagnostics();
                InstallTravelCover();
                InstallWaypointBarriers();
                RegisterCohesionCommands();
                // Shared callback keeps scout as a chat alias of the single player menu entry.
                PlayerMethods.RegisterPlayerCommand("formation",FormationCommand,true);
                PlayerMethods.RegisterPlayerCommand("scout",FormationCommand,true);
                AdminMethods.RegisterAdminCommand("formationspace",SpacingCommand,Power.Generic,"Server commander single-click spacing, 0..100%.");
                AdminMethods.RegisterAdminCommand("dragspace",(p,a)=>TuningCommand(p,a,"dragspace"),Power.Generic,"Server commander click-and-drag spacing, 0..100%.");
                AdminMethods.RegisterAdminCommand("rallyspace",RallyCommand,Power.Generic,"Server rally spacing, 0..100%.");
                AdminMethods.RegisterAdminCommand("movementfix",MovementFixCommand,Power.Generic,"Server grouped movement speed fix, on|off|status.");
                AdminMethods.RegisterAdminCommand("pathshortcut",PathfindingShortcutCommand,Power.Generic,"Native vehicle pathfinding shortcut pass, 0..2000 metres or status.");
                AdminMethods.RegisterAdminCommand("destinationcohesion",DestinationCohesionCommand,Power.Generic,
                    "Nearby destination cohesion fallback, on|off|status; ON by default.");
                AdminMethods.RegisterAdminCommand("waypointwait",WaypointWaitCommand,Power.Generic,
                    "Checkpoint wait: human|alien|combat <seconds>|status.");
                AdminMethods.RegisterAdminCommand("sizeinfluence",(p,a)=>TuningCommand(p,a,"sizeinfluence"),Power.Generic,"Server large-unit influence, 0..100%.");
                AdminMethods.RegisterAdminCommand("alienspread",(p,a)=>TuningCommand(p,a,"alienspread"),Power.Generic,"Server Alien spacing scale, 0..100%.");
                AdminMethods.RegisterAdminCommand("lesserspawnairspace",LesserSpawnAirSpacingCommand,Power.Generic,
                    "Lesser Spawn commander air spacing, 0..200 percent extra over native footprint spacing, or status.");
                AdminMethods.RegisterAdminCommand("fpsspace",(p,a)=>TuningCommand(p,a,"fpsspace"),Power.Generic,"Server FPS spacing, 0..100%.");
                AdminMethods.RegisterAdminCommand("fpsdistance",(p,a)=>TuningCommand(p,a,"fpsdistance"),Power.Generic,"Server FPS distance scale, 0..100%.");
                AdminMethods.RegisterAdminCommand("fpsallowance",(p,a)=>TuningCommand(p,a,"fpsallowance"),Power.Generic,"Server FPS movement allowance, 1..100m.");
                GameEvents.OnGameStarted+=ScoutRoundStarted;
                GameEvents.OnGameEnded+=ScoutRoundEnded;
                ready=true;
                RefreshPathfindingShortcutDistance(Game.GetIsServer());
                MelonLogger.Msg("Formation tuning ready: click/drag spacing, group cohesion, FPS layouts and reserved 3D rally destinations.");
            }
            catch(Exception e)
            {
                ready=false;
                RefreshPathfindingShortcutDistance(false);
                // Log the original failure first: a patch cleanup error must not
                // replace the evidence for why commands never became available.
                MelonLogger.Error("Formation initialization failed: "+e);
                ClearCohesionState("initialization failed");
                RemoveFormationPatches();
            }
        }
        private void RemoveFormationPatches()
        {
            try{HarmonyInstance.UnpatchSelf();}
            catch(Exception e){MelonLogger.Error("Formation patch cleanup failed: "+e);}
        }
        private static bool BeginMove(IReadOnlyList<BaseGameObject> objects,Vector3 worldPosition,Target target,AgentMoveSpeed moveSpeed,
            bool isAttack,bool remote,bool queueOrder,bool useLine,Vector3 lineEnd,OrderDefinition forcedOrder,ref bool __result,out MoveState __state)
        {
            __state=new MoveState{Independent=activeIndependent,Layout=activeLayout,Cohesion=activeCohesion,Diagnostics=activeMovementDiagnostics,Waypoint=activeWaypoint};
            activeIndependent=false;activeLayout=null;activeCohesion=null;activeMovementDiagnostics=null;activeWaypoint=null;
            if(!Active||!Game.GetIsServer()||objects==null)return true;
            var first=objects.FirstOrDefault(obj=>obj&&!obj.IsDestroyed);
            if(!first)return true;
            var commander=Commander(first.Team);
            activeMovementDiagnostics=BeginMovementDiagnostics(objects,commander,worldPosition,lineEnd,moveSpeed,isAttack,queueOrder,useLine,forcedOrder!=null,__state.Diagnostics);
            if(commander&&(remote||Player.CurrentPlayer==commander)&&!target&&!isAttack&&!queueOrder&&!forcedOrder)
            {
                var settings=ScoutFor(commander);
                if(settings.ScoutArmed&&settings.ScoutTeam==commander.Team)
                {
                    settings.ScoutArmed=false;
                    if(DispatchScout(commander,objects,worldPosition,moveSpeed,out bool success)){__result=success;return false;}
                }
            }
            if(commander&&(remote||Player.CurrentPlayer==commander)&&!target&&!isAttack&&(!forcedOrder||forcedOrder is ReverseOrderDefinition))
            {
                if(!useLine&&!forcedOrder)ForgetClickLayout(objects);
                activeWaypoint=PrepareWaypoint(objects,commander,useLine?(worldPosition+lineEnd)*.5f:worldPosition,
                    moveSpeed,queueOrder||forcedOrder is ReverseOrderDefinition);
            }
            if(forcedOrder)
            {
                if(forcedOrder is ReverseOrderDefinition&&commander&&(remote||Player.CurrentPlayer==commander)&&!target&&!isAttack)
                {
                    try
                    {
                        activeCohesion=PrepareReverseFormation(objects,commander,worldPosition,queueOrder,forcedOrder,moveSpeed);
                        if(activeCohesion!=null&&activeCohesion.Skip==null)activeIndependent=!activeCohesion.Unpaced;
                        else if(activeCohesion!=null)
                        {Reply(commander,"Reverse formation uses native movement: "+activeCohesion.Skip+".");activeCohesion=null;}
                    }
                    catch(Exception e){activeLayout=null;activeCohesion=null;MelonLogger.Warning("Reverse formation skipped: "+e.Message);}
                }
                return true;
            }
            var tuning=ServerSettings;
            // Adaptive ownership always replaces both native slowdown layers, even
            // when a scattered/unsupported selection takes the independent fallback.
            activeIndependent=tuning.MovementFix||CohesionEnabledFor(first.Team);
            var units=objects.OfType<Unit>().Where(FormationEligible).Distinct().ToList();
            var ground=units.Where(u=>!IsFormationAir(u)).ToList();
            var facing=ground.Count>0?ground:units;
            Vector3 origin=facing.Count==0?worldPosition:FormationOrigin(facing,queueOrder&&!isAttack&&!target);
            activeLayout=new LayoutContext{Settings=tuning,Team=first.Team,Origin=origin,
                Scale=Multiplier(useLine?tuning.DragSpacing:tuning.Spacing)*FactionScale(first.Team,tuning)};
            if(!useLine)
            {
                if(queueOrder||!(tuning.Spacing==0&&tuning.SizeInfluence==100&&FactionScale(first.Team,tuning)==1)||units.Any(IsFormationAir))
                {
                    activeLayout.Points=GridSlots(units,worldPosition,worldPosition-origin,tuning,activeLayout.Scale,out activeLayout.Pitch);
                }
            }
            if(queueOrder&&commander&&(remote||Player.CurrentPlayer==commander)&&!target&&!isAttack)
            {
                try{activeCohesion=PrepareCohesion(objects,commander,worldPosition,lineEnd,useLine,queueOrder);}
                catch(Exception e){activeCohesion=null;MelonLogger.Warning("Cohesion preparation skipped: "+e.Message);}
            }
            return true;
        }
        private static Exception EndMove(Exception __exception,MoveState __state,ref bool __result)
        {
            try
            {
                if(__exception==null)
                {
                    FinishCohesion(activeCohesion);
                    FinishWaypoint(activeWaypoint);
                    if(activeCohesion!=null&&activeCohesion.AcceptedReverseInfantryMoves>0)__result=true;
                }
            }
            catch(Exception e){MelonLogger.Warning("Cohesion dispatch released: "+e);}
            finally
            {
                FinishMovementDiagnostics(activeMovementDiagnostics,activeCohesion,__exception);
                activeIndependent=__state.Independent;activeLayout=__state.Layout;activeCohesion=__state.Cohesion;activeMovementDiagnostics=__state.Diagnostics;activeWaypoint=__state.Waypoint;
            }
            return __exception;
        }
        private static void Reply(Player p,string text){if(p)HelperMethods.SendChatMessageToPlayer(p,"Formations: "+text);}
        private static void SpacingCommand(Player p,string args)=>Set(p,args,false);
        private static void RallyCommand(Player p,string args)=>Set(p,args,true);
        private static void Set(Player p,string args,bool rally)
        {
            if(!p||!ready||!Game.GetIsServer())return;
            string text=(args??"").Trim();
            int split=text.IndexOfAny(new[]{' ','\t'});
            string value=split<0?"":text.Substring(split).Trim();
            var s=ServerSettings;string name=rally?"rallyspace":"formationspace";
            if(value.Length>0&&!value.Equals("status",StringComparison.OrdinalIgnoreCase))
            {
                if(!DemandAdmin(p))return;
                if(!int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int n)||n<0||n>100)
                {Reply(p,"/"+name+" <0..100> | status");return;}
                if(rally)s.Rally=n;else s.Spacing=n;
                if(!SaveSettings(p))return;
            }
            int percent=rally?s.Rally:s.Spacing;
            Reply(p,(rally?"Rally":"Single-click formation")+" spacing: "+percent+"% ("+Multiplier(percent).ToString("0.##",CultureInfo.InvariantCulture)+"x vanilla). Applies to new orders/production.");
        }
        public override void OnUpdate()
        {
            if(!ready)return;
            bool isServer=Game.GetIsServer();
            RefreshPathfindingShortcutDistance(isServer);
            if(!isServer)return;
            foreach(var p in Scouts.Keys.Where(p=>!p||!Player.Players.Contains(p)).ToArray())Scouts.Remove(p);
            foreach(var item in Scouts)if(!item.Key.IsCommander||item.Key.Team!=item.Value.ScoutTeam)item.Value.ScoutArmed=false;
            TickWaypointState();
            if(!ServerEnabled)return;
            TickRally();
            TickFps();
            TickCohesion();
        }
        public override void OnSceneWasUnloaded(int buildIndex,string sceneName)
        {ClearWaypointState();ClearMovementDiagnostics();ClearCohesionState("scene unload");CohesionGroups.Clear();ClearRallyState();ClearFpsStates();foreach(var settings in Scouts.Values)settings.ScoutArmed=false;}
        public override void OnDeinitializeMelon()
        {
            ready=false;RefreshPathfindingShortcutDistance(false);ClearWaypointState();ClearMovementDiagnostics();ClearCohesionState("mod unload");CohesionGroups.Clear();RemoveFormationPatches();Scouts.Clear();ClearRallyState();ClearFpsStates();
            GameEvents.OnGameStarted-=ScoutRoundStarted;GameEvents.OnGameEnded-=ScoutRoundEnded;
        }
    }
}
