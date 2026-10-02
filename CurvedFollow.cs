using System;
using System.Collections.Generic;
using System.Linq;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class Follower
        {
            internal Unit Unit,Leader;
            internal FormationDefinition Definition;
            internal int Slot=-1,Failures;
            internal bool Active;
            internal float NextAttempt, Progress=float.NaN, Radius;
            internal Vector3 Destination, LastDesired;
        }
        private static readonly Dictionary<Unit,Follower> Followers=new Dictionary<Unit,Follower>();
        private static readonly Dictionary<Unit,RouteHistory> Routes=new Dictionary<Unit,RouteHistory>();
        private static bool Eligible(Unit u,out Unit leader)
        {
            leader=null;
            if(!Available(u)||Superseded.Contains(u)||u.AIGroup==null||
                !u.AIGroup.IgnoresTask(u,out var reason)||reason!=AIGroup.EIgnoreTaskReason.FOLLOWING_LEADER)return false;
            leader=u.AIGroup.Leader;
            if(!leader||leader==u||leader.IsDestroyed||!leader.ControlledBy||!Player.Players.Contains(leader.ControlledBy)||
                leader.ControlledBy.ControlledUnit!=leader||leader.ControlledBy.IsCommander||u.Team!=leader.Team)return false;
            var orders=u.OrderAgent.Orders;
            return orders.Count<=1&&(orders.Count==0||orders[0] is AIMoveOrder);
        }
        private static bool Current(Follower s)=>Running&&Enabled&&Eligible(s.Unit,out Unit leader)&&leader==s.Leader;
        private static void ResetLeader(Unit leader)
        {
            if(!leader)return;
            foreach(var s in Followers.Values.Where(s=>s.Leader==leader).ToArray()){Release(s);Followers.Remove(s.Unit);}
            Routes.Remove(leader);
        }
        private static void ClearFollow()
        {
            foreach(var s in Followers.Values.ToArray())Release(s);
            Followers.Clear();Routes.Clear();Superseded.Clear();
        }
        private static void Release(Follower s)
        {
            // Only release a path still owned by us; external orders must survive.
            if(s.Active&&Available(s.Unit)&&Eligible(s.Unit,out Unit leader)&&leader==s.Leader)
            {s.Unit.AIAgent.AgentPathfinding.PathfindingSeeker.CancelCurrentPathRequest();s.Unit.AIAgent.AgentPathfinding.CompleteMovementAndReset();}
            s.Active=false;
        }
        private static void TickFollow()
        {
            Superseded.RemoveWhere(u=>!u||u.IsDestroyed||u.AIGroup==null||!u.AIGroup.IgnoresTask(u,out var r)||r!=AIGroup.EIgnoreTaskReason.FOLLOWING_LEADER);
            foreach(var s in Followers.Values.ToArray())if(!Current(s))Followers.Remove(s.Unit);
            // Work from player squads, not every unit on the map.
            foreach(var p in Player.Players.ToArray())
            {
                if(!p||p.IsCommander||!p.ControlledUnit||p.Group==null||p.Group.Leader!=p.ControlledUnit)continue;
                var f=Selected(p,"follow");
                if(f==null)continue; // JSON only: missing defaults leave native Follow intact.
                Unit leader=p.ControlledUnit;
                var eligible=p.Group.Units.Where(u=>Eligible(u,out Unit l)&&l==leader).ToArray();
                if(eligible.Length==0)continue;
                if(!Routes.TryGetValue(leader,out RouteHistory route))Routes[leader]=route=new RouteHistory(leader.transform.position,leader.transform.forward);
                if(!route.Record(leader.transform.position))
                    foreach(var s in Followers.Values.Where(s=>s.Leader==leader)){Release(s);s.Progress=float.NaN;s.Failures=0;}
                // Retain all existing assignments first; only new arrivals compete for free candidate dots.
                foreach(var u in eligible.OrderByDescending(u=>Followers.ContainsKey(u)).ThenBy(u=>u.GetInstanceID()))
                {
                    if(!Followers.TryGetValue(u,out Follower s))
                    {
                        if(Followers.Values.Count(v=>v.Leader==leader)>=FollowLimit)continue;
                        Followers[u]=s=new Follower{Unit=u,Leader=leader,Definition=f};
                    }
                    if(s.Definition!=f){Release(s);s.Definition=f;s.Slot=-1;s.Progress=float.NaN;s.Failures=0;}
                    UpdateFollower(s,route);
                }
            }
            foreach(var leader in Routes.Keys.ToArray())if(!leader||!Followers.Values.Any(s=>s.Leader==leader))Routes.Remove(leader);
        }
        private static void Offset(Follower s,RouteHistory route,FormationSlot slot,out float lateral,out float trailing)
        {
            lateral=slot.X;trailing=slot.Z;
            if(!s.Definition.DirectionSensitive)
            {
                // Freeze the WORLD-XZ layout against the route's initial travel frame.
                // The editor arrow is ignored. Subsequent curvature follows the route, not the player's facing.
                var world=new Vector3(slot.X,0,slot.Z);
                lateral=Vector3.Dot(world,Vector3.Cross(Vector3.up,route.InitialForward));
                trailing=Vector3.Dot(world,route.InitialForward);
            }
        }
        private static void UpdateFollower(Follower s,RouteHistory route)
        {
            if(Time.unscaledTime<s.NextAttempt)return;
            var reserved=Followers.Values.Where(o=>o!=s&&o.Leader==s.Leader&&o.Active)
                .Select(o=>new Placement{Unit=o.Unit,Point=o.Destination,Radius=o.Radius}).ToList();
            var used=new HashSet<int>(Followers.Values.Where(o=>o!=s&&o.Leader==s.Leader&&o.Slot>=0).Select(o=>o.Slot));
            bool repair=s.Unit.AIAgent is AIVehicleAgent v&&v.CanRepair;
            var ranked=s.Definition.Ranked(s.Unit.ObjectInfo?s.Unit.ObjectInfo.name:"",repair);
            // Keep a successful slot; repair only this assignment after a failed projection, not the whole squad.
            IEnumerable<int> candidates=s.Slot>=0?new[]{s.Slot}.Concat(ranked.Where(i=>i!=s.Slot)):ranked;
            int checks=0;
            foreach(int index in candidates.Take(128))
            {
                if(used.Contains(index))continue;
                var slot=s.Definition.Slots[index];Offset(s,route,slot,out float lateral,out float z);
                float desired=route.Distance+15+z; // estimated player destination: travel heading +15m, sampled once/sec
                float progress=desired;
                if(z<0&&route.Count>1)
                {
                    float old=float.IsNaN(s.Progress)?Math.Min(desired,route.Nearest(s.Unit.transform.position)):s.Progress;
                    if(!s.Active||DistanceSq(s.Unit.transform.position,s.Destination)<=64)old+=20;
                    progress=Math.Min(desired,old); // sequential route waypoints keep turns in the travel path
                    progress=Math.Max(progress,route.Distance-RouteHistory.MaxLength);
                }
                float radius=Radius(s.Unit,s.Definition,slot);
                // Inside-bend compression: bounded backward spacing, preserving the same candidate reservation.
                bool separated=false;Vector3 point=Vector3.zero;
                for(int stretch=0;stretch<8;stretch++)
                {
                    if(!route.Sample(progress-stretch*Math.Max(4,2*radius),lateral,out point))break;
                    if(!Separated(point,radius,reserved))continue;
                    progress-=stretch*Math.Max(4,2*radius);separated=true;break;
                }
                if(!separated)continue;
                if(s.Active&&index==s.Slot&&DistanceSq(point,s.Destination)<9) {s.Progress=progress;return;}
                if(DistanceSq(point,s.LastDesired)>9)s.Failures=0;
                if(s.Failures>=3){Release(s);s.NextAttempt=Time.unscaledTime+3;return;}
                if(++checks>ProjectionAttempts)break;
                if(!TerrainProjection.TryProject(s.Unit,point,out Vector3 projected)||!Separated(projected,radius,reserved))continue;
                var target=new OrderTarget(projected);var parameters=OrderIssueParams.Ai(s.Unit.AIAgent.MoveSpeed,broadcastEvent:false);
                generatedDepth++;
                bool issued;
                try {issued=s.Unit.OrderAgent.IssueOrder(OrderDefinitionRegistry.Move,in target,in parameters);}
                finally {generatedDepth--;}
                if(!issued)continue;
                s.Slot=index;s.Destination=projected;s.LastDesired=point;s.Progress=progress;s.Radius=radius;s.Active=true;s.Failures=0;
                s.NextAttempt=Time.unscaledTime+1;return;
            }
            s.Failures++;s.NextAttempt=Time.unscaledTime+3;
            // Keep a separated old destination on transient failure. If none exists, vanilla Follow remains available.
            if(s.Failures>=3)Release(s);
        }
    }
}
