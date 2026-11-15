using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Silica;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class WaypointMember
        {
            internal Unit Unit;
            internal AIOrderProcessor Processor;
            internal AIOrder Order;
            internal uint Id;
            internal uint[] Before;
            internal Vector3 Destination;
            internal bool Arrived,Removed,Included=true,Reverse;
            internal AgentMoveSpeed Mode;
            internal float NominalSpeed;
            internal WaypointBarrier Barrier;
        }
        private sealed class WaypointDispatch
        {
            internal Player Owner;
            internal Vector3 Anchor;
            internal AgentMoveSpeed Mode;
            internal bool Watched;
            internal readonly Dictionary<Unit,WaypointMember> Members=new Dictionary<Unit,WaypointMember>();
        }
        private sealed class WaypointBarrier
        {
            internal int Id,LastWaiting=-1;
            internal Player Owner;
            internal Team Team;
            internal bool Watched,Open,Advance,Rebased,GraceStarted;
            internal float NextSweep;
            internal float Activated=-1,Eta,Deadline,Grace;
            internal Vector3 Anchor,Centre;
            internal string Reason;
            internal List<WaypointMember> Members;
        }
        private static WaypointDispatch activeWaypoint;
        private static readonly List<WaypointBarrier> waypointBarriers=new List<WaypointBarrier>();
        private static readonly Dictionary<AIOrder,WaypointMember> waypointOrders=new Dictionary<AIOrder,WaypointMember>();
        private static int nextWaypointId=1;

        private void InstallWaypointBarriers()
        {
            HarmonyInstance.Patch(AccessTools.Method(typeof(AIMoveOrder),nameof(AIMoveOrder.Evaluate)),
                postfix:new HarmonyMethod(typeof(WaypointEvaluation),nameof(WaypointEvaluation.Move)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(AIReverseOrder),nameof(AIReverseOrder.Evaluate)),
                postfix:new HarmonyMethod(typeof(WaypointEvaluation),nameof(WaypointEvaluation.Reverse)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(AIOrderProcessor),nameof(AIOrderProcessor.IssueOrder)),
                postfix:new HarmonyMethod(typeof(WaypointOrderObserver),nameof(WaypointOrderObserver.After)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(OrderTargetingController),"IssueToObjects"),
                prefix:new HarmonyMethod(typeof(WaypointBatchScope),nameof(WaypointBatchScope.Before)){priority=1000},
                finalizer:new HarmonyMethod(typeof(WaypointBatchScope),nameof(WaypointBatchScope.End)){priority=HarmonyLib.Priority.Last});
        }
        private static WaypointDispatch PrepareWaypoint(IReadOnlyList<BaseGameObject> objects,Player owner,Vector3 anchor,AgentMoveSpeed mode)
        {
            if(!owner||!owner.IsCommander)return null;
            var dispatch=new WaypointDispatch{Owner=owner,Anchor=anchor,Mode=mode,Watched=CohesionDebugNext.Contains(owner)};
            foreach(var unit in objects.OfType<Unit>().Where(unit=>unit&&!unit.IsDestroyed&&unit.Team==owner.Team&&unit.AIControlled&&
                !unit.PlayerControlled&&!unit.ControlledBy&&!unit.InCompartment&&!(unit.Driver&&unit.Driver.ControlledBy)&&
                unit.OrderAgent&&(unit.OrderAgent.Receiver is IMoveOrderReceiver||unit.OrderAgent.Receiver is IReverseOrderReceiver)&&
                !(unit is Vehicle vehicle&&vehicle.HasForceMove)).Distinct())
                dispatch.Members[unit]=new WaypointMember{Unit=unit,Processor=unit.OrderAgent,Destination=anchor,Mode=mode,
                    Before=unit.OrderAgent.Orders.Select(order=>order.Id).ToArray()};
            return dispatch.Members.Count>=2||dispatch.Members.Count==1&&IsFormationAir(dispatch.Members.Keys.First())?dispatch:null;
        }
        private static class WaypointBatchScope
        {
            internal static void Before(OrderDefinition definition,IReadOnlyList<BaseGameObject> objects,
                IReadOnlyList<Vector3> points,Target clickObject,AgentMoveSpeed moveSpeed,out WaypointDispatch __state)
            {
                __state=activeWaypoint;activeWaypoint=null;
                if(!Active||!Game.GetIsServer()||objects==null||points==null||points.Count!=1||clickObject||
                    !(definition is MoveOrderDefinition)&&!(definition is ReverseOrderDefinition))return;
                var first=objects.FirstOrDefault(obj=>obj&&!obj.IsDestroyed);var owner=first?Commander(first.Team):null;
                var sender=NetworkLayer.GetRequestSender();
                if(!owner||(sender?sender!=owner:Player.CurrentPlayer!=owner))return;
                if(definition is MoveOrderDefinition)ForgetClickLayout(objects);
                activeWaypoint=PrepareWaypoint(objects,owner,points[0],moveSpeed);
            }
            internal static Exception End(Exception __exception,WaypointDispatch __state)
            {
                try{if(__exception==null)FinishWaypoint(activeWaypoint);}
                catch(Exception e){MelonLogger.Warning("Waypoint capture failed: "+e.Message);}
                finally{activeWaypoint=__state;}
                return __exception;
            }
        }
        private static class WaypointOrderObserver
        {
            internal static void After(AIOrderProcessor __instance,OrderTarget __1,OrderIssueParams __2,bool __result)
            {
                if(!Active||!Game.GetIsServer()||!__result||__2.CalledByAI||!(__instance.Owner is Unit unit))return;
                foreach(var barrier in waypointBarriers)
                    foreach(var pending in barrier.Members)
                        if(pending.Unit==unit&&(pending.Order==null||!__instance.Orders.Any(order=>
                            ReferenceEquals(order,pending.Order)&&order.Id==pending.Id)))pending.Removed=true;
                var dispatch=activeWaypoint;
                if(dispatch==null||!dispatch.Members.TryGetValue(unit,out var member)||member.Processor!=__instance||__instance.Orders.Count==0)return;
                var order=__instance.Orders[__instance.Orders.Count-1];
                if(!(order is AIMoveOrder)&&!(order is AIReverseOrder))return;
                bool ownPoint=FlatDistanceSq(__1.Position,dispatch.Anchor)<1||
                    activeLayout!=null&&activeLayout.Points.TryGetValue(unit,out var slot)&&FlatDistanceSq(__1.Position,slot)<1||
                    activeCohesion?.LocalPoints!=null&&activeCohesion.LocalPoints.TryGetValue(unit,out var local)&&FlatDistanceSq(__1.Position,local)<1||
                    activeCohesion?.ReverseInfantryMoves!=null&&activeCohesion.ReverseInfantryMoves.TryGetValue(unit,out var infantry)&&
                        FlatDistanceSq(__1.Position,infantry.Destination)<1;
                if(!ownPoint||FlatDistanceSq(order.GetPosition(),__1.Position)>1)return;
                member.Order=order;member.Id=order.Id;member.Destination=order.GetPosition();
                member.Reverse=order is AIReverseOrder;
                member.Mode=order is AIMoveOrder move?move.MoveSpeed:((AIReverseOrder)order).MoveSpeed;
            }
        }
        private static void FinishWaypoint(WaypointDispatch dispatch)
        {
            if(dispatch==null||!Active)return;
            var barrier=new WaypointBarrier{Id=nextWaypointId++,Owner=dispatch.Owner,Team=dispatch.Owner.Team,
                Watched=dispatch.Watched,Anchor=dispatch.Anchor,Members=dispatch.Members.Values.ToList()};
            if(nextWaypointId==int.MaxValue)nextWaypointId=1;
            foreach(var member in barrier.Members)
            {
                member.Barrier=barrier;
                if(member.Order!=null)waypointOrders[member.Order]=member;
                else
                {
                    var orders=member.Processor.Orders;
                    // A different intervening command takes precedence; a rejected
                    // waypoint with an unchanged queue remains a blocking member.
                    member.Removed=orders.Count!=member.Before.Length;
                    for(int i=0;!member.Removed&&i<orders.Count;i++)if(orders[i].Id!=member.Before[i])member.Removed=true;
                }
            }
            waypointBarriers.Add(barrier);
            int missing=barrier.Members.Count(member=>!member.Removed&&member.Order==null);
            if(missing>0)Reply(dispatch.Owner,"Waypoint #"+barrier.Id+" waits for "+missing+
                " members without an accepted move, subject to the regroup deadline.");
            SweepWaypoint(barrier,true);
        }
        private static class WaypointEvaluation
        {
            internal static void Move(AIMoveOrder __instance,ref bool __result)=>EvaluateWaypoint(__instance,ref __result);
            internal static void Reverse(AIReverseOrder __instance,ref bool __result)=>EvaluateWaypoint(__instance,ref __result);
        }
        private static void EvaluateWaypoint(AIOrder order,ref bool result)
        {
            if(!Active||!Game.GetIsServer()||!waypointOrders.TryGetValue(order,out var member)||order.Id!=member.Id)return;
            if(!CurrentWaypoint(member))return;
            bool arrived=IsFormationAir(member.Unit)?HorizontalAirArrival(member):result;
            bool changed=arrived&&!member.Arrived;
            if(changed)MarkWaypointArrival(member,IsFormationAir(member.Unit)?"horizontal air arrival":"native completion");
            SweepWaypoint(member.Barrier,changed);
            if(member.Removed)return;
            if(member.Barrier.Open)
            {
                if(member.Barrier.Advance)result=true;
                RemoveWaypointMapping(member);
            }
            else result=false;
        }
        private static void SweepWaypoint(WaypointBarrier barrier,bool force)
        {
            if(barrier.Open||!force&&Time.time<barrier.NextSweep)return;
            barrier.NextSweep=Time.time+.1f;
            if(!barrier.Owner||!barrier.Owner.IsCommander||barrier.Owner.Team!=barrier.Team)
            {OpenWaypoint(barrier,"ownership released",false);return;}
            int waiting=0,retained=0;WaypointMember last=null;
            foreach(var member in barrier.Members)
            {
                var unit=member.Unit;var processor=member.Processor;
                if(member.Removed)continue;
                if(!unit||unit.IsDestroyed||!processor||unit.Team!=barrier.Team||!unit.AIControlled||unit.PlayerControlled||unit.ControlledBy||
                    unit.InCompartment||unit.Driver&&unit.Driver.ControlledBy||unit.OrderAgent!=processor||
                    unit is Vehicle vehicle&&vehicle.HasForceMove)
                {member.Removed=true;continue;}
                if(member.Order!=null&&!processor.Orders.Any(order=>ReferenceEquals(order,member.Order)&&order.Id==member.Id))
                {
                    // Native AI interruption is not an arrival or a casualty.
                    // Keep this living member blocking the other owned queues;
                    // a commanded replacement is handled by the order observer.
                    continue;
                }
                if(member.Order!=null)retained++;
                if(!member.Arrived&&CurrentWaypoint(member)&&IsFormationAir(unit)&&HorizontalAirArrival(member))
                    MarkWaypointArrival(member,"horizontal air arrival");
            }
            if(retained==0){OpenWaypoint(barrier,"no owned waypoint orders",false);return;}
            if(barrier.Activated<0)
            {
                if(!barrier.Members.Any(CurrentWaypoint))return;
                ActivateRegroup(barrier);
                if(barrier.Open)return;
            }
            RebaseRegroup(barrier);
            if(!barrier.GraceStarted&&Time.time>=barrier.Eta)
            {
                barrier.GraceStarted=true;
                if(CohesionDebugEnabled||barrier.Watched)MelonLogger.Msg("[Waypoint #"+barrier.Id+"] regroup countdown started; remaining="+
                    MovementNumber(Mathf.Max(0,barrier.Deadline-Time.time))+"s");
            }
            foreach(var member in barrier.Members)
                if(!member.Removed&&member.Included&&!member.Arrived){waiting++;last=member;}
            if(waiting!=barrier.LastWaiting&&(CohesionDebugEnabled||barrier.Watched))
                MelonLogger.Msg("[Waypoint #"+barrier.Id+"] waiting="+waiting+"/"+barrier.Members.Count+
                    (last!=null?" last="+MovementUnitName(last.Unit)+" target="+MovementPoint(last.Destination)+
                        " accepted="+(last.Order!=null):"; included members completed"));
            barrier.LastWaiting=waiting;
            if(waiting==0)OpenWaypoint(barrier,"native completion of included members",true);
            else if(Time.time>=barrier.Deadline)OpenWaypoint(barrier,"regroup timeout",true);
        }
        private static void OpenWaypoint(WaypointBarrier barrier,string reason,bool advance)
        {
            barrier.Open=true;barrier.Reason=reason;barrier.Advance=advance;
            if(CohesionDebugEnabled||barrier.Watched)
                MelonLogger.Msg("[Waypoint #"+barrier.Id+"] release="+reason+" outstanding="+
                    barrier.Members.Count(member=>!member.Removed&&member.Included&&!member.Arrived)+
                    " excluded="+barrier.Members.Count(member=>!member.Removed&&!member.Included));
            CleanupWaypoint(barrier);
        }
        private static void TickWaypointState()
        {
            if(!Active){ClearWaypointState();return;}
            foreach(var barrier in waypointBarriers.ToArray())
                if(barrier.Open)CleanupWaypoint(barrier);else SweepWaypoint(barrier,false);
        }
        private static void ClearWaypointState(){waypointOrders.Clear();waypointBarriers.Clear();activeWaypoint=null;}
    }
}
