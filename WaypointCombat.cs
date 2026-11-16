using System;
using System.Linq;
using MelonLoader;
using Silica.AI;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static bool restoringWaypointOrders;

        private static bool WaypointMemberOwned(WaypointMember member)
        {
            var unit=member.Unit;
            var barrier=member.Barrier;
            return unit&&!unit.IsDestroyed&&member.Processor&&unit.OrderAgent==member.Processor&&
                barrier.Owner&&barrier.Owner.IsCommander&&barrier.Owner.Team==barrier.Team&&unit.Team==barrier.Team&&
                unit.AIControlled&&!unit.PlayerControlled&&!unit.ControlledBy&&!unit.InCompartment&&
                !(unit.Driver&&unit.Driver.ControlledBy)&&!(unit is Vehicle vehicle&&vehicle.HasForceMove);
        }

        private static bool AICombatOrder(AIOrder order)=>order!=null&&order.CalledByAI&&
            (order is AIAttackOrder||order is AIAttackMoveOrder);

        private static bool AlienWaypointCombat(WaypointMember member)
        {
            var agent=member.Unit.AIAgent;
            if(!agent)return false;
            var orders=member.Processor.Orders;
            if(orders.Count>0&&AICombatOrder(orders[0]))return true;
            var aiming=agent.AgentAiming;
            var target=aiming?aiming.AimTargetIgnorePaused:null;
            return target&&!target.IsDestroyed&&member.Unit.Target&&member.Unit.Target.GetIsEnemy(target.Team)&&
                (aiming.IsTargetVisible||aiming.CanShoot);
        }

        private static void RefreshWaypointCombat(WaypointMember member)
        {
            if(!member.Barrier.Regroup||!IsAlienTeam(member.Barrier.Team)||!WaypointMemberOwned(member))return;
            bool relevant=member.Barrier.Activated>=0||CurrentWaypoint(member)||CombatOrderBeforeWaypoint(member);
            bool combat=AlienWaypointCombat(member);
            if(relevant&&combat!=member.InCombat&&(CohesionDebugEnabled||member.Barrier.Watched))
                MelonLogger.Msg("[Waypoint #"+member.Barrier.Id+"] unit="+MovementUnitName(member.Unit)+
                    (combat?" combat started":" combat ended; returning to waypoint"));
            member.InCombat=combat;
            if(!combat||!relevant)return;
            member.LastCombatAt=UnityEngine.Time.time;
            member.CombatInterrupted=true;
            member.ReturnAfterCombat=true;
            if(!HorizontalWaypointArrival(member))member.Arrived=false;
        }

        private static bool ReadyAfterCombatOrder(WaypointMember member)
            =>member.InCombat&&CombatOrderBeforeWaypoint(member);

        private static bool CombatOrderBeforeWaypoint(WaypointMember member)
        {
            if(!PendingWaypoint(member))return false;
            foreach(var order in member.Processor.Orders)
            {
                if(ReferenceEquals(order,member.Order))return true;
                if(!AICombatOrder(order))return false;
            }
            return false;
        }

        private static bool HasLaterWaypoint(WaypointMember member)
        {
            bool after=false;
            foreach(var order in member.Processor.Orders)
            {
                if(ReferenceEquals(order,member.Order)){after=true;continue;}
                if(after&&waypointOrders.TryGetValue(order,out var candidate)&&candidate.Id==order.Id&&
                    candidate.Unit==member.Unit&&!candidate.Removed)return true;
            }
            return false;
        }

        private static void ResumeAlienWaypoint(WaypointMember member)
        {
            float now=UnityEngine.Time.time;
            if(!member.CombatInterrupted||member.InCombat||!CurrentWaypoint(member)||
                now-member.LastCombatAt<1||now<member.NextResumeAt)return;
            if(HorizontalWaypointArrival(member))
            {
                member.ReturnAfterCombat=false;
                return;
            }
            if(member.Barrier.Open&&HasLaterWaypoint(member))return;
            var agent=member.Unit.AIAgent;
            var path=agent?agent.AgentPathfinding:null;
            if(!path)return;
            bool displaced=!path.IsCalculatingPath&&
                (!agent.HasMoveOrder||FlatDistanceSq(path.AgentFinalInstructedDestination,member.Destination)>1);
            if(!member.ReturnAfterCombat&&!displaced)return;
            member.NextResumeAt=now+2;
            // Re-execute the captured head, retaining its ID and all queued orders.
            member.Order.Execute();
            member.ReturnAfterCombat=false;
            member.Arrived=false;
            if(CohesionDebugEnabled||member.Barrier.Watched)
                MelonLogger.Msg("[Waypoint #"+member.Barrier.Id+"] resumed after combat unit="+MovementUnitName(member.Unit)+
                    " target="+MovementPoint(member.Destination));
        }

        private static void PreserveAlienCombatWaypoints(AIOrderProcessor processor,Unit unit)
        {
            if(!IsAlienTeam(unit.Team)||processor.Orders.Count==0)return;
            bool combat=AICombatOrder(processor.Orders[processor.Orders.Count-1]);
            foreach(var barrier in waypointBarriers)
                foreach(var member in barrier.Members)
                {
                    if(member.Unit!=unit||member.Removed||!barrier.Regroup||member.Order==null||PendingWaypoint(member))continue;
                    RemoveWaypointMapping(member);
                    member.Order=null;
                    if(!combat){member.Removed=true;continue;}
                    member.RestorePending=true;
                    member.CombatInterrupted=barrier.Activated>=0;
                    member.ReturnAfterCombat=member.CombatInterrupted;
                    member.Arrived=false;
                    member.LastCombatAt=UnityEngine.Time.time;
                }
            if(combat)AppendAlienWaypoints(processor,unit);
        }

        private static void AppendAlienWaypoints(AIOrderProcessor processor,Unit unit)
        {
            var members=waypointBarriers.SelectMany(barrier=>barrier.Members)
                .Where(member=>member.Unit==unit&&!member.Removed&&member.RestorePending&&WaypointMemberOwned(member)).ToList();
            if(members.Count==0)return;
            foreach(var member in members)member.NextResumeAt=UnityEngine.Time.time+2;
            // A new manual or utility order takes ownership before any restoration.
            if(processor.Orders.Any(order=>!AICombatOrder(order)&&
                !(waypointOrders.TryGetValue(order,out var tracked)&&tracked.Id==order.Id&&tracked.Unit==unit)))
            {
                foreach(var member in members)member.Removed=true;
                return;
            }
            restoringWaypointOrders=true;
            try
            {
                foreach(var member in members)
                {
                    if(!processor.CanEnqueueOrder()||!member.Definition)break;
                    var parameters=OrderIssueParams.Commanded(member.Mode,true,1,false);
                    parameters.LongRange=member.LongRange;
                    if(!processor.IssueOrder(member.Definition,new OrderTarget(member.Destination),parameters))break;
                    if(processor.Orders.Count==0)break;
                    var restored=processor.Orders[processor.Orders.Count-1];
                    if(!(restored is AIMoveOrder)&&!(restored is AIReverseOrder)||
                        FlatDistanceSq(restored.GetPosition(),member.Destination)>1)break;
                    member.Order=restored;
                    member.Id=restored.Id;
                    member.RestorePending=false;
                    waypointOrders[restored]=member;
                    if(CohesionDebugEnabled||member.Barrier.Watched)
                        MelonLogger.Msg("[Waypoint #"+member.Barrier.Id+"] queued after native AI combat unit="+MovementUnitName(unit));
                }
            }
            catch(Exception exception){MelonLogger.Warning("Restoring Alien waypoints after combat: "+exception.Message);}
            finally{restoringWaypointOrders=false;}
        }
    }
}
