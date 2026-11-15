using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private const float RegroupCutoff=1000,RegroupFreshPathWindow=3;
        private static bool PendingWaypoint(WaypointMember member)=>member.Unit&&member.Processor&&
            member.Unit.OrderAgent==member.Processor&&member.Order!=null&&member.Order.Id==member.Id&&
            member.Processor.Orders.Contains(member.Order);
        private static bool CurrentWaypoint(WaypointMember member)=>!member.Removed&&PendingWaypoint(member)&&
            member.Processor.Orders.Count>0&&ReferenceEquals(member.Processor.Orders[0],member.Order);
        private static bool HorizontalAirArrival(WaypointMember member)=>member.Unit&&member.Unit.AIAgent&&
            FlatDistanceSq(member.Unit.transform.position,member.Destination)<=
                Mathf.Pow(Mathf.Max(.1f,member.Unit.AIAgent.FinalWaypointThreshold),2);
        private static void MarkWaypointArrival(WaypointMember member,string reason)
        {
            member.Arrived=true;
            if(CohesionDebugEnabled||member.Barrier.Watched)
                MelonLogger.Msg("[Waypoint #"+member.Barrier.Id+"] "+reason+" unit="+MovementUnitName(member.Unit)+
                    " horizontalDistance="+MovementNumber(Mathf.Sqrt(FlatDistanceSq(member.Unit.transform.position,member.Destination)))+"m");
        }
        private static void RemoveWaypointMapping(WaypointMember member)
        {
            if(member.Order!=null&&waypointOrders.TryGetValue(member.Order,out var current)&&ReferenceEquals(current,member))
                waypointOrders.Remove(member.Order);
        }
        private static void CleanupWaypoint(WaypointBarrier barrier)
        {
            bool pending=false;
            foreach(var member in barrier.Members)
            {
                if(!barrier.Advance||member.Removed||!PendingWaypoint(member))RemoveWaypointMapping(member);
                if(member.Order!=null&&waypointOrders.TryGetValue(member.Order,out var current)&&ReferenceEquals(current,member))pending=true;
            }
            if(!pending)waypointBarriers.Remove(barrier);
        }
        private static float MedianCoordinate(float[] coordinates)
        {
            Array.Sort(coordinates);int middle=coordinates.Length/2;
            return coordinates.Length%2==0?(coordinates[middle-1]+coordinates[middle])*.5f:coordinates[middle];
        }
        private static void ActivateRegroup(WaypointBarrier barrier)
        {
            var living=barrier.Members.Where(member=>!member.Removed&&member.Unit).ToList();
            if(living.Count==0){OpenWaypoint(barrier,"no surviving members",false);return;}
            barrier.Activated=Time.time;
            barrier.Centre=new Vector3(MedianCoordinate(living.Select(member=>member.Unit.transform.position.x).ToArray()),0,
                MedianCoordinate(living.Select(member=>member.Unit.transform.position.z).ToArray()));
            // A split with no unit near its median has no majority main body.
            // Resolve that tie toward the commanded waypoint, never exclude all.
            if(!living.Any(member=>FlatDistanceSq(member.Unit.transform.position,barrier.Centre)<=RegroupCutoff*RegroupCutoff))
                barrier.Centre=Flat(living.OrderBy(member=>FlatDistanceSq(member.Unit.transform.position,barrier.Anchor))
                    .ThenBy(member=>member.Unit.GetInstanceID()).First().Unit.transform.position);
            foreach(var member in living)
            {
                member.Included=FlatDistanceSq(member.Unit.transform.position,barrier.Centre)<=RegroupCutoff*RegroupCutoff;
                member.NominalSpeed=RegroupModeSpeed(member);
                if(!member.Included&&(CohesionDebugEnabled||barrier.Watched))
                    MelonLogger.Msg("[Waypoint #"+barrier.Id+"] distance exclusion unit="+MovementUnitName(member.Unit)+
                        " fromMainBody="+MovementNumber(Mathf.Sqrt(FlatDistanceSq(member.Unit.transform.position,barrier.Centre)))+
                        "m; queued orders retained");
            }
            float fraction=Mathf.Clamp01(Mathf.Sqrt(FlatDistanceSq(barrier.Centre,barrier.Anchor))/
                Mathf.Max(1,CohesionValueFor(barrier.Team,"MaximumRange")));
            barrier.Grace=Mathf.Lerp(WaypointGraceNear,WaypointGraceFar,fraction);
            SetRegroupEstimate(barrier,false);
        }
        private static float RegroupModeSpeed(WaypointMember member)
        {
            var unit=member.Unit;var mode=member.Mode;
            float speed=member.Reverse&&unit is Vehicle vehicle?vehicle.ReverseNormalSpeed:
                mode==AgentMoveSpeed.Fast?unit.TopSpeed:unit.NormalSpeed;
            if(unit is Soldier soldier)
            {
                speed=mode==AgentMoveSpeed.Stealth?soldier.WalkSpeed:
                    mode==AgentMoveSpeed.Fast&&soldier.CanSprint?soldier.SprintSpeed:soldier.RunSpeed;
                if(soldier.IsCrouching)speed*=soldier.CrouchSpeedModifier;
            }
            else if(unit is CreatureDecapod creature)
                speed=IsFormationAir(unit)?(mode==AgentMoveSpeed.Stealth?creature.FlyCreepSpeed:creature.FlyMoveSpeed):
                    mode==AgentMoveSpeed.Stealth?creature.CreepSpeed:creature.MoveSpeed;
            // Nominal requested-mode speed: never velocity, custom speed caps or
            // transient native MoveSpeedScale from a previous queued segment.
            return float.IsNaN(speed)||float.IsInfinity(speed)?1:Mathf.Max(.1f,speed);
        }
        private static float RegroupRemaining(WaypointMember member,out bool fresh)
        {
            fresh=member.Arrived;
            if(member.Arrived)return 0;
            float direct=Mathf.Sqrt(FlatDistanceSq(member.Unit.transform.position,member.Destination));
            if(!CurrentWaypoint(member)||!member.Unit.AIAgent||!member.Unit.AIAgent.AgentPathfinding)return direct;
            var path=member.Unit.AIAgent.AgentPathfinding;
            fresh=!path.IsCalculatingPath&&path.HasShortPath&&FlatDistanceSq(path.AgentFinalInstructedDestination,member.Destination)<1;
            // Aircraft timing and arrival are horizontal: flight height cannot
            // inflate a cylindrical destination into a long vertical journey.
            if(IsFormationAir(member.Unit)||!fresh)return direct;
            float route=path.AgentRemainingPathLength;
            if(path.HasLongPath&&path.CurrentLongRangeWaypointIndex==0)
            {
                int count=Math.Min(path.CurrentLongRangePathLength,path.LongRangePathPoints.Count);
                if(count>512)return direct;
                for(int i=0;i<count-1;i++)route+=Vector3.Distance(path.LongRangePathPoints[i],path.LongRangePathPoints[i+1]);
            }
            return float.IsNaN(route)||float.IsInfinity(route)?direct:Mathf.Max(direct,route);
        }
        private static void SetRegroupEstimate(WaypointBarrier barrier,bool correction)
        {
            float longest=0,slowest=float.MaxValue;
            foreach(var member in barrier.Members.Where(member=>!member.Removed&&member.Included))
            {
                longest=Mathf.Max(longest,RegroupRemaining(member,out _));
                slowest=Mathf.Min(slowest,member.NominalSpeed);
            }
            barrier.Eta=Time.time+(slowest==float.MaxValue?0:longest/Mathf.Max(.1f,slowest));
            barrier.Deadline=barrier.Eta+barrier.Grace;
            barrier.GraceStarted=false;
            if(CohesionDebugEnabled||barrier.Watched)
                MelonLogger.Msg("[Waypoint #"+barrier.Id+"] "+(correction?"one-time fresh-path ETA":"activated ETA")+
                    " travelSeconds="+MovementNumber(barrier.Eta-Time.time)+" graceSeconds="+MovementNumber(barrier.Grace)+
                    " longestRemaining="+MovementNumber(longest)+"m slowestModeSpeed="+MovementNumber(slowest==float.MaxValue?0:slowest)+
                    "m/s; deadline fixed after initial path correction");
        }
        private static void RebaseRegroup(WaypointBarrier barrier)
        {
            if(barrier.Open||barrier.Rebased||barrier.Activated<0)return;
            bool ready=true;
            foreach(var member in barrier.Members.Where(member=>!member.Removed&&member.Included))
            {RegroupRemaining(member,out bool fresh);if(!fresh)ready=false;}
            if(ready||Time.time-barrier.Activated>=RegroupFreshPathWindow)
            {
                // Exactly one correction in the startup window. Future reroutes
                // never revise ETA or extend this waypoint's grace countdown.
                barrier.Rebased=true;SetRegroupEstimate(barrier,true);
            }
        }
        private static void ForgetClickLayout(IReadOnlyList<BaseGameObject> objects)
        {
            foreach(var unit in objects.OfType<Unit>().Where(unit=>unit))
                if(CohesionLayouts.TryGetValue(unit,out var layout))InvalidateCohesionLayout(layout);
        }
    }
}
