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
            internal int Slot=-1,Attempts;
            internal bool Active;
            internal float NextAttempt,Radius;
            internal Vector3 Destination;
            internal Vector3 Anchor,RequestedAnchor,RequestedForward;
            internal bool Refresh,RequestedMirrored;
            internal int RefreshAttempts;
            internal float RefreshAt;
            internal uint Order;
            internal float OutsideSince=-1;
            internal bool Recovering;
            internal float DepartAt;
            internal int Phase; // 0 waiting, 2 final order sent
            internal readonly HashSet<int> Rejected=new HashSet<int>();
        }
        private sealed class FollowFrame
        {
            internal Vector3 LastSample,Anchor,Forward;
            internal bool Holding,Mirrored;
            internal FormationDefinition HeadingDefinition;
            internal readonly Dictionary<Unit,int> Rows=new Dictionary<Unit,int>();
            internal float DepartureStart,DepartureDelay;
            internal IEnumerator<bool> Search;
        }
        private static readonly Dictionary<Unit,Follower> Followers=new Dictionary<Unit,Follower>();
        private static readonly Dictionary<Unit,FollowFrame> Frames=new Dictionary<Unit,FollowFrame>();
        private static float FollowRefreshDistance(Unit unit,bool facingLocked)
        {
            if(unit.ObjectInfo)
            {
                switch(unit.ObjectInfo.UnitType)
                {
                    case UnitType.Infantry:return facingLocked?2f:5f;
                    case UnitType.Siege:return facingLocked?15f:30f;
                }
            }
            return facingLocked?10f:20f;
        }
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
        private static bool Current(Follower s)=>Running&&Eligible(s.Unit,out Unit leader)&&leader==s.Leader&&FunctionActive(leader.ControlledBy,"follow");
        private static void ResetLeader(Unit leader)
        {
            if(!leader)return;
            foreach(var s in Followers.Values.Where(s=>s.Leader==leader).ToArray()){Release(s);Followers.Remove(s.Unit);}
            Frames.Remove(leader);
        }
        private static void ClearFollow()
        {
            foreach(var s in Followers.Values.ToArray())Release(s);
            Followers.Clear();Frames.Clear();Superseded.Clear();FollowDepartureCaps.Clear();
        }
        private static void Release(Follower s)
        {
            FpsSpeedCaps.Remove(s.Unit);
            FollowDepartureCaps.Remove(s.Unit);
            if(s.Active&&Available(s.Unit)&&Eligible(s.Unit,out Unit leader)&&leader==s.Leader)
            {s.Unit.AIAgent.AgentPathfinding.PathfindingSeeker.CancelCurrentPathRequest();s.Unit.AIAgent.AgentPathfinding.CompleteMovementAndReset();}
            s.Active=false;
        }
        private static void TickFollow()
        {
            Superseded.RemoveWhere(u=>!u||u.IsDestroyed||u.AIGroup==null||!u.AIGroup.IgnoresTask(u,out var r)||r!=AIGroup.EIgnoreTaskReason.FOLLOWING_LEADER);
            foreach(var s in Followers.Values.ToArray())if(!Current(s))
            {if(Frames.TryGetValue(s.Leader,out var oldFrame)){oldFrame.Search?.Dispose();oldFrame.Search=null;}Followers.Remove(s.Unit);}
            foreach(var p in Player.Players.ToArray())
            {
                if(!p||p.IsCommander||!p.ControlledUnit||p.Group==null||p.Group.Leader!=p.ControlledUnit)continue;
                if(!FunctionActive(p,"follow"))continue;
                var definition=Selected(p,"follow");
                if(definition==null)continue;
                Unit leader=p.ControlledUnit;
                var eligible=p.Group.Units.Where(u=>Eligible(u,out Unit l)&&l==leader).ToArray();
                if(eligible.Length==0){ResetLeader(leader);continue;}
                Vector3 position=leader.transform.position;
                if(!Frames.TryGetValue(leader,out FollowFrame frame))
                    Frames[leader]=frame=new FollowFrame {LastSample=position};
                float moved=DistanceSq(position,frame.LastSample);
                frame.LastSample=position;
                bool quiet=moved<=0.04f;
                Vector3 centre=eligible.Aggregate(Vector3.zero,(sum,u)=>sum+u.transform.position)/eligible.Length;
                Vector3 travel=Flat(position-centre);
                bool facingLocked=For(p).FollowFacingLock;
                Vector3 nextForward=facingLocked?Heading(leader.transform.forward):
                    travel.sqrMagnitude>1f?Heading(travel):frame.Holding?frame.Forward:Vector3.forward;
                float distance=eligible.Min(u=>FollowRefreshDistance(u,facingLocked));
                if(!frame.Holding||DistanceSq(position,frame.Anchor)>=distance*distance||
                    (quiet&&DistanceSq(position,frame.Anchor)>1f))
                {
                    frame.Mirrored=definition.DirectionSensitive&&frame.HeadingDefinition==definition&&(frame.Mirrored^Reversed(frame.Forward,nextForward));
                    frame.HeadingDefinition=definition;
                    frame.Anchor=position;frame.Forward=nextForward;frame.Holding=true;
                    frame.Search?.Dispose();frame.Search=null;
                    foreach(var s in Followers.Values.Where(s=>s.Leader==leader&&!s.Active))
                    {s.Attempts=0;s.NextAttempt=0;}
                    if(frame.Rows.Count==0)
                    {
                        foreach(var row in SoftDepartureBatches(eligible,Heading(position-centre)))frame.Rows[row.Key]=row.Value;
                        // Accepted slots can depart while remaining slots are searched.
                        // Moving anchors may cancel searches; never strand a departure at infinity.
                        frame.DepartureStart=Time.unscaledTime;frame.DepartureDelay=SoftDelay;
                    }
                }
                foreach(var unit in PlacementOrder(definition,eligible).OrderByDescending(u=>Followers.ContainsKey(u)&&Followers[u].Active))
                {
                    if(!Followers.TryGetValue(unit,out Follower s))
                    {
                        if(Followers.Values.Count(v=>v.Leader==leader)>=ConfiguredPlayerLimit(p)-1)continue;
                        Followers[unit]=s=new Follower {Unit=unit,Leader=leader,Definition=definition};
                    }
                    if(s.Definition!=definition){Release(s);s.Definition=definition;s.Slot=-1;s.Attempts=0;s.NextAttempt=0;s.Rejected.Clear();frame.Search?.Dispose();frame.Search=null;}
                    if(s.Active)
                    {
                        float threshold=FollowRefreshDistance(unit,facingLocked);
                        if(DistanceSq(position,s.Anchor)>=threshold*threshold||(quiet&&DistanceSq(position,s.Anchor)>1f))
                        {
                            s.Anchor=position;s.RequestedAnchor=position;
                            s.RequestedMirrored=s.Definition.DirectionSensitive&&(s.RequestedMirrored^Reversed(s.RequestedForward,nextForward));
                            s.RequestedForward=nextForward;s.Refresh=true;s.RefreshAttempts=0;s.RefreshAt=0;
                        }
                        if(!s.Refresh)SettleFollower(s,frame);
                    }
                }
                if(frame.Search==null)StartFollowSearch(leader,definition,frame);
            }
            foreach(var leader in Frames.Keys.ToArray())
                if(!leader||!leader.ControlledBy||!Player.Players.Contains(leader.ControlledBy)||leader.AIGroup==null||
                    !leader.AIGroup.Units.Any(u=>Eligible(u,out Unit l)&&l==leader))Frames.Remove(leader);
        }
        private static void SettleFollower(Follower s,FollowFrame frame)
        {
            if(s.Phase<2||s.Attempts>=3||Time.unscaledTime<s.NextAttempt)return;
            if(s.Active&&DistanceSq(s.Unit.transform.position,s.Destination)<=9&&Math.Abs(s.Unit.transform.position.y-s.Destination.y)<=3)return;
            s.Attempts++;s.NextAttempt=Time.unscaledTime+1.5f;
            // Retry the same destination without discarding the unit's assigned slot.
            if(s.Active)
            {
                IssueFixedMove(s,s.Destination);
                return;
            }
        }
        private static void StartFollowSearch(Unit leader,FormationDefinition definition,FollowFrame frame)
        {
            var pending=Followers.Values.Where(s=>s.Leader==leader&&!s.Active&&s.Attempts<3&&Time.unscaledTime>=s.NextAttempt).ToArray();
            if(pending.Length==0)return;
            foreach(var s in pending){s.Attempts++;s.NextAttempt=Time.unscaledTime+1.5f;}
            var reserved=Followers.Values.Where(s=>s.Leader==leader&&s.Active)
                .Select(s=>new Placement {Unit=s.Unit,Slot=s.Slot,Point=s.Destination,Radius=s.Radius}).ToList();
            bool Valid(Unit u)=>frame.Holding&&leader&&
                Followers.TryGetValue(u,out var s)&&!s.Active&&s.Definition==definition&&Current(s)&&Selected(leader.ControlledBy,"follow")==definition;
            bool Issue(Placement p)
            {
                if(!Valid(p.Unit))return false;
                var s=Followers[p.Unit];
                s.Phase=0;
                if(!frame.Rows.ContainsKey(p.Unit))frame.Rows[p.Unit]=frame.Rows.Count==0?0:Math.Min(4,frame.Rows.Values.Max()+1);
                s.DepartAt=frame.DepartureStart+frame.Rows[p.Unit]*frame.DepartureDelay;
                s.Slot=p.Slot;s.Destination=p.Point;s.Radius=p.Radius;s.Active=true;s.NextAttempt=Time.unscaledTime+1.5f;
                s.Anchor=frame.Anchor;s.RequestedForward=frame.Forward;s.RequestedMirrored=frame.Mirrored;s.Refresh=false;
                return true;
            }
            frame.Search=SearchRemembered(pending.Where(s=>s.Slot>=0).ToDictionary(s=>s.Unit,s=>s.Slot),definition,pending.Select(s=>s.Unit).ToArray(),frame.Anchor,frame.Forward,reserved,Valid,Issue,frame.Mirrored).GetEnumerator();
        }
        private static int followSearchCursor;
        private static void TickFollowSearches()
        {
            // Shared budget, round-robin between leaders; no world scans.
            var jobs=Frames.Values.Where(f=>f.Search!=null).ToArray();
            if(jobs.Length==0)return;
            var clock=System.Diagnostics.Stopwatch.StartNew();
            for(int work=0,steps=0;work<12&&steps<1024&&clock.ElapsedMilliseconds<2;steps++)
            {
                var frame=jobs[(followSearchCursor++&int.MaxValue)%jobs.Length];
                if(frame.Search==null)continue;
                if(!frame.Search.MoveNext())
                {
                    frame.Search.Dispose();frame.Search=null;
                    if(float.IsPositiveInfinity(frame.DepartureStart))frame.DepartureStart=Time.unscaledTime;
                    foreach(var s in Followers.Values.Where(s=>s.Active&&s.Phase==0&&Frames.TryGetValue(s.Leader,out var owner)&&owner==frame))
                        s.DepartAt=frame.DepartureStart+frame.Rows[s.Unit]*frame.DepartureDelay;
                }
                else if(frame.Search.Current)work++;
            }
        }
        private static void TickFollowDepartures()
        {
            int work=0;
            foreach(var s in Followers.Values.ToArray())
            {
                if(!s.Active||!Current(s))continue;
                if(s.Refresh&&s.RefreshAttempts>=3)s.Refresh=false;
                if(s.Refresh)
                {
                    if(Time.unscaledTime<s.RefreshAt)continue;
                    if(++work>12)break;
                    s.RefreshAttempts++;s.RefreshAt=Time.unscaledTime+1.5f;
                    var desired=World(s.Definition.Slots[s.Slot],s.Definition,s.RequestedAnchor,s.RequestedForward,s.RequestedMirrored);
                    if(!TerrainProjection.TryProject(s.Unit,desired,out var point))continue;
                    var reserved=Followers.Values.Where(o=>o!=s&&o.Leader==s.Leader&&o.Active)
                        .Select(o=>new Placement {Unit=o.Unit,Radius=o.Radius,Point=o.Refresh?
                            World(o.Definition.Slots[o.Slot],o.Definition,o.RequestedAnchor,o.RequestedForward,o.RequestedMirrored):o.Destination});
                    if(!Separated(point,s.Radius,reserved,s.Unit))continue;
                    var target=new OrderTarget(point);
                    if(!s.Unit.OrderAgent.CanIssueOrder(OrderDefinitionRegistry.Move,in target))continue;
                    // Keep the assigned slot on a blocked refresh; native navigation owns the route.
                    if(s.Phase>=2&&!IssueFixedMove(s,point))continue;
                    s.Destination=point;s.Refresh=false;s.Attempts=0;s.NextAttempt=Time.unscaledTime+1.5f;
                    continue;
                }
                if(s.Phase>=2||Time.unscaledTime<s.DepartAt)continue;

                if(++work>12)break;
                if(IssueFixedMove(s,s.Destination)){s.Phase=2;RefreshFollowDepartureCaps();}
                else
                {
                    s.Active=false;s.Slot=-1;s.NextAttempt=Time.unscaledTime+1.5f;
                    if(Frames.TryGetValue(s.Leader,out var frame)){frame.Search?.Dispose();frame.Search=null;}
                }
            }
            RefreshFollowDepartureCaps();
        }
        private static bool IssueFixedMove(Follower s,Vector3 point)
        {
            var target=new OrderTarget(point);
            var parameters=OrderIssueParams.Ai(s.Unit.AIAgent.MoveSpeed,broadcastEvent:false);
            generatedDepth++;
            try
            {
                UnitCohesionGroup.Detach(s.Unit);
                bool issued=s.Unit.OrderAgent.IssueOrder(OrderDefinitionRegistry.Move,in target,in parameters);
                if(issued)s.Order=s.Unit.OrderAgent.Orders.Count>0?s.Unit.OrderAgent.Orders[0].Id:0;
                return issued;
            }
            finally {generatedDepth--;}
        }
    }
}
