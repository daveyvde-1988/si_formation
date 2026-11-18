using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private const float StragglerTimeout=300;
        private sealed class StragglerWatch
        {
            internal WaypointMember Member;
            internal float OutsideSince=-1;
        }
        private static readonly Dictionary<Unit,StragglerWatch> stragglerWatches=new Dictionary<Unit,StragglerWatch>();
        private static float nextStragglerScan;

        private static bool StragglerHasOwnedIntent(WaypointMember member)
        {
            if(member.RestorePending||member.DeferredReturn||PendingWaypoint(member))return true;
            if(!member.Barrier.Open||!member.Barrier.Advance||member.Processor.Orders.Count>0)return false;
            var agent=member.Unit.AIAgent;
            // Forced queue advancement can finish the final order while its
            // native path is still running. Keep ownership of that exact goal.
            return agent&&agent.AgentPathfinding&&agent.HasMoveOrder&&
                FlatDistanceSq(agent.AgentPathfinding.AgentFinalInstructedDestination,member.Destination)<1;
        }

        private static bool TryWaypointMainBody(WaypointBarrier barrier,out Vector3 centre)
        {
            var living=barrier.Members.Where(member=>!member.Removed&&WaypointMemberOwned(member)).ToList();
            centre=Vector3.zero;
            if(living.Count==0)return false;
            centre=new Vector3(MedianCoordinate(living.Select(member=>member.Unit.transform.position.x).ToArray()),0,
                MedianCoordinate(living.Select(member=>member.Unit.transform.position.z).ToArray()));
            Vector3 median=centre;
            if(!living.Any(member=>FlatDistanceSq(member.Unit.transform.position,median)<=RegroupCutoff*RegroupCutoff))
                centre=Flat(living.OrderBy(member=>FlatDistanceSq(member.Unit.transform.position,barrier.Anchor))
                    .ThenBy(member=>member.Unit.GetInstanceID()).First().Unit.transform.position);
            return true;
        }

        private static void TrackStragglerGroup(WaypointBarrier barrier)
        {
            foreach(var member in barrier.Members)
            {
                if(member.Removed||!WaypointMemberOwned(member)||!StragglerHasOwnedIntent(member))continue;
                if(!stragglerWatches.TryGetValue(member.Unit,out var watch)||
                    watch.Member.Processor!=member.Processor||watch.Member.Barrier.Owner!=barrier.Owner||watch.Member.Barrier.Team!=barrier.Team)
                    stragglerWatches[member.Unit]=watch=new StragglerWatch();
                watch.Member=member;
                if(member.Included)watch.OutsideSince=-1;
                else if(watch.OutsideSince<0)StartStragglerTimer(watch,Time.time);
            }
        }

        private static void TransferStragglerWatch(WaypointMember member)
        {
            if(!stragglerWatches.TryGetValue(member.Unit,out var watch)||!ReferenceEquals(watch.Member,member))return;
            bool later=false;
            foreach(var barrier in waypointBarriers)
            {
                if(ReferenceEquals(barrier,member.Barrier)){later=true;continue;}
                if(!later)continue;
                foreach(var candidate in barrier.Members)
                    if(candidate.Unit==member.Unit&&!candidate.Removed&&candidate.Processor==member.Processor&&
                        barrier.Owner==member.Barrier.Owner&&barrier.Team==member.Barrier.Team&&WaypointMemberOwned(candidate)&&
                        (candidate.RestorePending||candidate.DeferredReturn||PendingWaypoint(candidate)))
                    {
                        watch.Member=candidate;
                        return;
                    }
            }
        }

        private static void StartStragglerTimer(StragglerWatch watch,float now)
        {
            watch.OutsideSince=now;
            var member=watch.Member;
            if(CohesionDebugEnabled||member.Barrier.Watched)
                MelonLogger.Msg("[Waypoint #"+member.Barrier.Id+"] straggler timer started unit="+MovementUnitName(member.Unit)+"; beyond 1000m main-body cutoff");
        }

        private static void TickStragglers()
        {
            if(stragglerWatches.Count==0||Time.time<nextStragglerScan)return;
            float now=Time.time;
            nextStragglerScan=now+1;
            var centres=new Dictionary<WaypointBarrier,Vector3?>();
            foreach(var watch in stragglerWatches.Values.ToArray())
            {
                var member=watch.Member;
                var unit=member.Unit;
                if(!stragglerWatches.TryGetValue(unit,out var current)||!ReferenceEquals(current,watch))continue;
                if(member.Removed||!WaypointMemberOwned(member)||!StragglerHasOwnedIntent(member))
                {
                    stragglerWatches.Remove(unit);
                    continue;
                }
                if(!centres.TryGetValue(member.Barrier,out var centre))
                {
                    centre=TryWaypointMainBody(member.Barrier,out var position)?position:(Vector3?)null;
                    centres[member.Barrier]=centre;
                }
                if(!centre.HasValue)
                {
                    stragglerWatches.Remove(unit);
                    continue;
                }
                bool outside=FlatDistanceSq(unit.transform.position,centre.Value)>RegroupCutoff*RegroupCutoff;
                if(!outside)
                {
                    if(watch.OutsideSince>=0&&(CohesionDebugEnabled||member.Barrier.Watched))
                        MelonLogger.Msg("[Waypoint #"+member.Barrier.Id+"] straggler rejoined unit="+MovementUnitName(unit)+"; five-minute timer reset");
                    watch.OutsideSince=-1;
                    continue;
                }
                if(watch.OutsideSince<0)
                    StartStragglerTimer(watch,now);
                if(now-watch.OutsideSince<StragglerTimeout)continue;
                stragglerWatches.Remove(unit);
                if(CohesionDebugEnabled||member.Barrier.Watched)
                    MelonLogger.Msg("[Waypoint #"+member.Barrier.Id+"] straggler orders cancelled unit="+MovementUnitName(unit)+"; continuously beyond 1000m for five minutes");
                if(CohesionCaps.TryGetValue(unit,out var paced))
                {
                    paced.Lost=true;
                    ClearCohesionCap(paced);
                    paced.CapReason="five-minute straggler cleanup";
                }
                // ClearOrders synchronizes the queue and releases waypoint recovery.
                // Stop also cancels the active native path, which queue clearing
                // alone leaves running. New commands can take ownership normally.
                member.Processor.ClearOrders();
                UnitCohesionGroup.Detach(unit);
                if(unit.AIAgent)unit.AIAgent.Stop(false);
            }
        }
    }
}
