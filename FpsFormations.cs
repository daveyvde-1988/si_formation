using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private enum FpsMode { Follow,Move,Attack }
        private sealed class FpsState
        {
            internal Player Owner;
            internal AIGroup Group;
            internal Unit Leader;
            internal Team Team;
            internal FpsMode Mode;
            internal AITaskBase Task;
            internal Target Target;
            internal Vector3 Anchor,LastSample,LastIssued,Heading,MotionOrigin;
            internal float LastMotion;
            internal bool Sampled,Convoy,Issued,Dirty=true;
            internal int SettingsHash;
            internal readonly Dictionary<Unit,Vector3> Slots=new Dictionary<Unit,Vector3>();
            internal readonly HashSet<Unit> Released=new HashSet<Unit>();
            internal readonly HashSet<Unit> LastMembers=new HashSet<Unit>();
        }
        private sealed class FpsCapture
        {
            internal Player Owner;
            internal AIGroup Group;
            internal Unit Leader;
            internal Team Team;
            internal Unit[] Units;
            internal AITaskBase PreviousTask;
        }
        private static readonly Dictionary<AIGroup,FpsState> FpsFollowers=new Dictionary<AIGroup,FpsState>();
        private static readonly Dictionary<AIGroup,FpsState> FpsTasks=new Dictionary<AIGroup,FpsState>();
        private static readonly HashSet<Unit> FpsIssuing=new HashSet<Unit>();
        private static Func<Unit,bool> nativeTransferring;
        private static float nextFpsTick;

        private void InstallFps()
        {
            nativeTransferring=(Func<Unit,bool>)Delegate.CreateDelegate(typeof(Func<Unit,bool>),
                AccessTools.Method(typeof(AIGroup),"GetIsTransferring")??throw new MissingMethodException("AIGroup.GetIsTransferring"));
            HarmonyInstance.Patch(AccessTools.Method(typeof(FPSCommanding),"MoveServer"),
                prefix:new HarmonyMethod(typeof(Formations),nameof(CaptureFps)),postfix:new HarmonyMethod(typeof(Formations),nameof(FpsMoveOrdered)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(FPSCommanding),"AttackServer"),
                prefix:new HarmonyMethod(typeof(Formations),nameof(CaptureFps)),postfix:new HarmonyMethod(typeof(Formations),nameof(FpsAttackOrdered)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(FPSCommanding),"FollowAllServer"),prefix:new HarmonyMethod(typeof(Formations),nameof(FpsFollowAll)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(FPSCommanding),"StopAllServer"),prefix:new HarmonyMethod(typeof(Formations),nameof(FpsStopAll)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(FPSCommanding),"UnitFollowServer"),prefix:new HarmonyMethod(typeof(Formations),nameof(FpsFollowUnit)));
            HarmonyInstance.Patch(AccessTools.PropertySetter(typeof(AIGroup),"Task"),
                prefix:new HarmonyMethod(typeof(Formations),nameof(FpsTaskChanging)),postfix:new HarmonyMethod(typeof(Formations),nameof(FpsTaskChanged)));
            foreach(string method in new[]{"EvaluateLeaderFollow","EvaluateMovingTask","IgnoreTask"})
                HarmonyInstance.Patch(AccessTools.Method(typeof(AIGroup),method),transpiler:new HarmonyMethod(typeof(Formations),nameof(FpsGroupRepaths)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(AIOrderProcessor),"IssueOrder"),
                prefix:new HarmonyMethod(typeof(Formations),nameof(FpsNativeOrder)),postfix:new HarmonyMethod(typeof(Formations),nameof(FpsObserveOrder)));
        }
        private static Player FpsOwner(AIGroup group)
        {
            var leader=group?.Leader;var p=leader?leader.ControlledBy:null;
            return p&&!p.IsCommander&&p.Group==group&&p.ControlledUnit==leader&&!leader.IsDestroyed&&leader.Team==p.Team?p:null;
        }
        private static bool FpsEligible(Unit unit,Player p)=>FormationEligible(unit)&&unit!=p.ControlledUnit&&unit.Team==p.Team&&
            unit.AIGroup==p.Group&&unit.AIAgent.AgentPathfinding.PathfindingSeeker&&(!unit.IsFlyingType||unit.AIAgent is AIAirVehicleAgent)&&
            !(unit is Vehicle vehicle&&vehicle.HasForceMove)&&!nativeTransferring(unit);
        private static bool ValidFps(FpsState state)=>state.Owner&&FpsOwner(state.Group)==state.Owner&&state.Owner.ControlledUnit==state.Leader&&state.Owner.Team==state.Team;
        private static FpsState NewFps(Player p,FpsMode mode)=>new FpsState{Owner=p,Group=p.Group,Leader=p.ControlledUnit,Team=p.Team,Mode=mode};
        private static void CaptureFps(Player player,out FpsCapture __state)
        {
            __state=null;
            if(!Active||!Game.GetIsServer()||!player||FpsOwner(player.Group)!=player)return;
            var group=player.Group;bool allStopped=group.AllStopped();
            var units=group.Units.Where(u=>FpsEligible(u,player)&&
                (allStopped||!group.IgnoresTask(u,out var reason)||reason==AIGroup.EIgnoreTaskReason.FOLLOWING_LEADER)).ToArray();
            __state=new FpsCapture{Owner=player,Group=group,Leader=group.Leader,Team=player.Team,Units=units,PreviousTask=group.Task};
        }
        private static bool CaptureStillValid(FpsCapture capture)=>capture!=null&&capture.Owner&&FpsOwner(capture.Group)==capture.Owner&&
            capture.Group.Leader==capture.Leader&&capture.Owner.Team==capture.Team&&capture.Group.Task!=null&&capture.Group.Task!=capture.PreviousTask;
        private static void FpsMoveOrdered(Vector3 position,FpsCapture __state)
        {
            if(!CaptureStillValid(__state))return;
            FpsFollowers.Remove(__state.Group);
            var state=NewFps(__state.Owner,FpsMode.Move);state.Task=state.Group.Task;state.Anchor=position;
            FpsTasks[state.Group]=state;
            var units=__state.Units.Where(u=>FpsEligible(u,state.Owner)).ToList();
            var points=FpsLayout(units,state,position,position-state.Leader.transform.position,FpsMode.Move,false);
            IssueFps(state,points);
        }
        private static void FpsAttackOrdered(Target target,FpsCapture __state)
        {
            if(!CaptureStillValid(__state)||!target||target.IsDestroyed)return;
            FpsFollowers.Remove(__state.Group);
            var state=NewFps(__state.Owner,FpsMode.Attack);state.Task=state.Group.Task;state.Target=target;
            FpsTasks[state.Group]=state;
            UpdateFpsMotion(state,__state.Units.Where(u=>FpsEligible(u,state.Owner)).ToList(),true);
        }
        private static void FpsFollowAll(Player player)
        {
            if(!Active||!Game.GetIsServer()||!player||FpsOwner(player.Group)!=player)return;
            FpsTasks.Remove(player.Group);FpsFollowers[player.Group]=NewFps(player,FpsMode.Follow);
        }
        private static void FpsStopAll(Player player)
        {
            if(!Active||!Game.GetIsServer()||!player)return;
            if(player.Group!=null){FpsTasks.Remove(player.Group);FpsFollowers.Remove(player.Group);}
        }
        private static void FpsFollowUnit(Player player,Target target)
        {
            if(!Active||!Game.GetIsServer()||!player||FpsOwner(player.Group)!=player||!target||!target.OwnerUnit||target.OwnerUnit.AIGroup!=player.Group)return;
            var unit=target.OwnerUnit;
            if(FpsTasks.TryGetValue(player.Group,out var task))task.Slots.Remove(unit);
            if(!FpsFollowers.TryGetValue(player.Group,out var state)||!ValidFps(state))FpsFollowers[player.Group]=state=NewFps(player,FpsMode.Follow);
            state.Released.Remove(unit);state.Dirty=true;
        }
        private static bool FollowsPlayer(Unit unit,Player p)
        {
            var group=p.Group;
            if(group.IgnoresTask(unit,out var reason))return reason==AIGroup.EIgnoreTaskReason.FOLLOWING_LEADER;
            if(FpsTasks.TryGetValue(group,out var task)&&task.Task==group.Task&&(task.Slots.ContainsKey(unit)||task.Released.Contains(unit)))return false;
            return group.Task==null||group.Task.State!=EAITaskState.Ongoing;
        }
        private static void FpsTaskChanging(AIGroup __instance,out AITaskBase __state){__state=__instance.Task;}
        private static void FpsTaskChanged(AIGroup __instance,AITaskBase value,AITaskBase __state)
        {
            if(!Active||!Game.GetIsServer()||value==__state)return;
            if(value!=null)FpsFollowers.Remove(__instance);
            if(!FpsTasks.TryGetValue(__instance,out var state))return;
            // Completion of the native marker must not turn a clicked destination
            // grid back into automatic follow. A new non-null task takes ownership.
            if(value==null&&state.Mode==FpsMode.Move)state.Task=null;
            else if(value!=state.Task)FpsTasks.Remove(__instance);
        }
        private static bool TryFpsSlot(Unit unit,out FpsState state,out Vector3 point)
        {
            state=null;point=Vector3.zero;
            if(!Active||!Game.GetIsServer()||!unit||unit.AIGroup==null)return false;
            var group=unit.AIGroup;
            if(FpsTasks.TryGetValue(group,out var task)&&ValidFps(task)&&task.Task==group.Task&&
                FpsEligible(unit,task.Owner)&&!group.IgnoresTask(unit,out _)&&task.Slots.TryGetValue(unit,out point))
            {state=task;return true;}
            if(FpsFollowers.TryGetValue(group,out var follow)&&ValidFps(follow)&&FpsEligible(unit,follow.Owner)&&
                FollowsPlayer(unit,follow.Owner)&&follow.Slots.TryGetValue(unit,out point))
            {state=follow;return true;}
            return false;
        }
        private static IEnumerable<CodeInstruction> FpsGroupRepaths(IEnumerable<CodeInstruction> source,MethodBase __originalMethod)
        {
            int count=0,expected=__originalMethod.Name=="IgnoreTask"?1:2;
            var original=AccessTools.Method(typeof(AIOrderProcessor),"IssueResolvedOrder");
            foreach(var instruction in source)
            {
                if(instruction.Calls(original))
                {instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(Formations),nameof(FpsGroupMove));count++;}
                yield return instruction;
            }
            if(count!=expected)throw new InvalidOperationException("FPS group repath signature changed: "+__originalMethod.Name+" expected "+expected+", found "+count);
        }
        private static bool FpsGroupMove(AIOrderProcessor processor,Vector3 position,Target target,in OrderIssueParams parameters)
        {
            if(processor&&processor.Owner is Unit unit)
            {
                if(TryFpsSlot(unit,out _,out _))return true;
                var group=unit.AIGroup;
                if(Active&&Game.GetIsServer()&&group!=null)
                {
                    if(FpsTasks.TryGetValue(group,out var task)&&ValidFps(task)&&task.Task==group.Task&&task.Released.Contains(unit))return true;
                    if(FpsFollowers.TryGetValue(group,out var follow)&&ValidFps(follow)&&follow.Released.Contains(unit)&&FollowsPlayer(unit,follow.Owner))return true;
                }
            }
            return processor.IssueResolvedOrder(position,target,in parameters);
        }
        private static bool FpsNativeOrder(AIOrderProcessor __instance,OrderDefinition __0,ref OrderTarget __1,OrderIssueParams __2,ref bool __result,out bool __state)
        {
            __state=false;
            if(!Active||!Game.GetIsServer()||!__2.CalledByAI||!(__instance.Owner is Unit unit)||FpsIssuing.Contains(unit))return true;
            var group=unit.AIGroup;
            if(group!=null&&FpsTasks.TryGetValue(group,out var released)&&ValidFps(released)&&released.Task==group.Task&&released.Released.Contains(unit)&&
                ((__0==OrderDefinitionRegistry.Attack&&released.Mode==FpsMode.Attack&&__1.Object==released.Target)||
                (released.Mode==FpsMode.Move&&!__1.Object&&FlatDistanceSq(__1.Position,released.Anchor)<4&&(__0==OrderDefinitionRegistry.Move||__0==OrderDefinitionRegistry.Attack))))
            {__state=true;__result=true;return false;}
            if(!TryFpsSlot(unit,out var state,out var point))return true;
            if(state.Mode==FpsMode.Attack&&state.Target&&__1.Object==state.Target&&__0==OrderDefinitionRegistry.Attack)
                __1=new OrderTarget(point,__1.Object,__1.Payload);
            else if(state.Mode==FpsMode.Move&&!__1.Object&&FlatDistanceSq(__1.Position,state.Anchor)<4&&
                (__0==OrderDefinitionRegistry.Move||__0==OrderDefinitionRegistry.Attack))
            {__state=true;__result=true;return false;}
            return true;
        }
        private static void FpsObserveOrder(AIOrderProcessor __instance,OrderTarget __1,OrderIssueParams __2,bool __result,bool __state)
        {
            if(__state||!__result||!(__instance.Owner is Unit unit)||FpsIssuing.Contains(unit)||!TryFpsSlot(unit,out var state,out var point))return;
            if(__2.CalledByAI&&FlatDistanceSq(__1.Position,point)<4&&(__1.Object==(state.Mode==FpsMode.Attack?state.Target:null)||!__1.Object))return;
            state.Slots.Remove(unit);state.Released.Add(unit); // accepted external orders own this member until a new player group/follow order
        }
        private static void IssueFps(FpsState state,Dictionary<Unit,Vector3> points)
        {
            foreach(var entry in points)
            {
                if(!ValidFps(state))break;
                if(state.Mode==FpsMode.Follow)
                {if(!FpsFollowers.TryGetValue(state.Group,out var current)||current!=state)break;}
                else if(!FpsTasks.TryGetValue(state.Group,out var current)||current!=state||state.Task!=state.Group.Task)break;
                var unit=entry.Key;if(!FpsEligible(unit,state.Owner)||state.Released.Contains(unit))continue;
                var target=new OrderTarget(entry.Value,state.Mode==FpsMode.Attack?state.Target:null);
                OrderDefinition definition=state.Mode==FpsMode.Attack?OrderDefinitionRegistry.Attack:OrderDefinitionRegistry.Move;
                if(!unit.OrderAgent.CanIssueOrder(definition,in target))continue;
                var speed=unit.AIAgent.MoveSpeed==AgentMoveSpeed.Stealth?AgentMoveSpeed.Stealth:
                    state.Mode==FpsMode.Follow?AgentMoveSpeed.Fast:AgentMoveSpeed.Normal;
                var parameters=OrderIssueParams.Commanded(speed,broadcastEvent:state.Mode!=FpsMode.Follow);
                FpsIssuing.Add(unit);
                try
                {
                    if(unit.OrderAgent.IssueOrder(definition,in target,in parameters))
                    {UnitCohesionGroup.Detach(unit);state.Slots[unit]=entry.Value;}
                }
                finally{FpsIssuing.Remove(unit);}
            }
        }
        private static void UpdateFpsMotion(FpsState state,List<Unit> units,bool force=false)
        {
            var p=state.Owner;var leader=state.Leader;float now=Time.unscaledTime;var position=leader.transform.position;
            Vector3 delta=state.Sampled?position-state.LastSample:leader.CurrentVelocity;delta.y=0;
            if(!state.Sampled){state.MotionOrigin=position;state.LastMotion=now-1;state.Heading=leader.transform.forward;state.Heading.y=0;state.Heading.Normalize();}
            if(delta.sqrMagnitude>.0025f){state.LastMotion=now;state.Heading=delta.normalized;}
            state.Sampled=true;state.LastSample=position;
            bool moving=now-state.LastMotion<.75f;
            var settings=ServerSettings;int hash=unchecked((((settings.FpsSpacing*101+settings.FpsDistance)*101+settings.SizeInfluence)*101+settings.AlienSpread)*101+settings.FpsAllowance);
            // Measure the whole departure from rest, independently of fan repaths.
            // First follow and small corrections stay fanned, even if already moving.
            if(!moving)state.MotionOrigin=position;
            float convoyDistance=Mathf.Max(10,settings.FpsAllowance*2);
            bool convoy=moving&&(state.Convoy||FlatDistanceSq(position,state.MotionOrigin)>convoyDistance*convoyDistance);
            bool changed=!state.LastMembers.SetEquals(units);
            bool update=force||state.Dirty||!state.Issued||state.Convoy!=convoy||hash!=state.SettingsHash||FlatDistanceSq(position,state.LastIssued)>settings.FpsAllowance*settings.FpsAllowance||changed;
            state.Convoy=convoy;if(!update)return;
            var direction=state.Heading;
            var points=FpsLayout(units,state,position,direction,state.Mode,convoy);
            IssueFps(state,points);
            state.LastIssued=position;state.Issued=true;state.Dirty=false;state.SettingsHash=hash;
            state.LastMembers.Clear();state.LastMembers.UnionWith(units);
        }
        private static void TickFps()
        {
            if(Time.unscaledTime<nextFpsTick)return;nextFpsTick=Time.unscaledTime+.25f;
            foreach(var item in FpsTasks.ToArray())
            {
                var state=item.Value;
                if(!ValidFps(state)||state.Task!=state.Group.Task||(state.Mode==FpsMode.Attack&&(!state.Target||state.Target.IsDestroyed)))
                {FpsTasks.Remove(item.Key);continue;}
                state.Released.RemoveWhere(u=>!u||u.IsDestroyed||u.AIGroup!=state.Group);
                foreach(var u in state.Slots.Keys.Where(u=>!FpsEligible(u,state.Owner)||state.Group.IgnoresTask(u,out _)).ToArray())
                {state.Slots.Remove(u);state.Released.Add(u);}
                if(state.Mode==FpsMode.Attack)
                    UpdateFpsMotion(state,state.Slots.Keys.Where(u=>!state.Released.Contains(u)).ToList());
            }
            foreach(var p in Player.Players)
            {
                if(!p||FpsOwner(p.Group)!=p)continue;
                if(!FpsFollowers.TryGetValue(p.Group,out var state)||!ValidFps(state))FpsFollowers[p.Group]=state=NewFps(p,FpsMode.Follow);
                state.Released.RemoveWhere(u=>!u||u.IsDestroyed||u.AIGroup!=state.Group);
                var units=p.Group.Units.Where(u=>FpsEligible(u,p)&&!state.Released.Contains(u)&&FollowsPlayer(u,p)).ToList();
                foreach(var u in state.Slots.Keys.Where(u=>!units.Contains(u)).ToArray())
                {state.Slots.Remove(u);if(!FpsEligible(u,p))state.Released.Add(u);}
                if(units.Count>0)UpdateFpsMotion(state,units);
            }
            foreach(var group in FpsFollowers.Where(e=>!ValidFps(e.Value)).Select(e=>e.Key).ToArray())FpsFollowers.Remove(group);
        }
        private static void ClearFpsStates(){FpsTasks.Clear();FpsFollowers.Clear();FpsIssuing.Clear();nextFpsTick=0;}
    }
}
