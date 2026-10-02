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
            internal Vector3 Destination,Approach;
            internal float DepartAt;
            internal int Phase; // 0 waiting, 1 approaching, 2 final order sent
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
        private static float FollowReleaseDistance(Unit leader)
        {
            if(leader.ObjectInfo)
            {
                switch(leader.ObjectInfo.UnitType)
                {
                    case UnitType.Infantry:return 5f;
                    case UnitType.Siege:return 20f;
                }
            }
            return 35f;
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
                if(eligible.Length<=2){ResetLeader(leader);continue;}
                Vector3 position=leader.transform.position;
                if(!Frames.TryGetValue(leader,out FollowFrame frame))
                {Frames[leader]=new FollowFrame {LastSample=position};continue;}
                float moved=DistanceSq(position,frame.LastSample);
                frame.LastSample=position;
                float releaseDistance=FollowReleaseDistance(leader);
                if(frame.Holding&&DistanceSq(position,frame.Anchor)>releaseDistance*releaseDistance)
                {
                    foreach(var s in Followers.Values.Where(s=>s.Leader==leader))
                    {Release(s);s.Attempts=0;s.NextAttempt=0;s.Rejected.Clear();}
                    frame.Holding=false;frame.Search=null;
                    // Existing FOLLOWING_LEADER flags re-enable native Follow immediately.
                    // Capture a new actual-position frame only after a subsequent quiet sample.
                    continue;
                }
                if(!frame.Holding)
                {
                    if(moved>1f)continue;
                    Vector3 nextForward=Heading(leader.transform.forward);
                    frame.Mirrored=definition.DirectionSensitive&&frame.HeadingDefinition==definition&&(frame.Mirrored^Reversed(frame.Forward,nextForward));
                    frame.HeadingDefinition=definition;
                    frame.Anchor=position;frame.Forward=nextForward;frame.Holding=true;
                    frame.Rows.Clear();
                    foreach(var row in SoftDepartureBatches(eligible,Heading(position-eligible.Aggregate(Vector3.zero,(sum,u)=>sum+u.transform.position)/eligible.Length)))frame.Rows[row.Key]=row.Value;
                    frame.DepartureStart=float.PositiveInfinity;frame.DepartureDelay=SoftDelay;
                }
                foreach(var unit in PlacementOrder(definition,eligible).OrderByDescending(u=>Followers.ContainsKey(u)&&Followers[u].Active))
                {
                    if(!Followers.TryGetValue(unit,out Follower s))
                    {
                        if(Followers.Values.Count(v=>v.Leader==leader)>=ConfiguredPlayerLimit(p)-1)continue;
                        Followers[unit]=s=new Follower {Unit=unit,Leader=leader,Definition=definition};
                    }
                    if(s.Definition!=definition){Release(s);s.Definition=definition;s.Slot=-1;s.Attempts=0;s.NextAttempt=0;s.Rejected.Clear();frame.Search?.Dispose();frame.Search=null;}
                    if(s.Active)SettleFollower(s,frame);
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
            // A held slot remains fixed inside the leader's unit-type release distance.
            if(s.Active)
            {
                if(!IssueFixedMove(s,s.Destination)){s.Active=false;s.Slot=-1;frame.Search?.Dispose();frame.Search=null;}
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
            bool Valid(Unit u)=>frame.Holding&&leader&&DistanceSq(leader.transform.position,frame.Anchor)<=FollowReleaseDistance(leader)*FollowReleaseDistance(leader)&&
                Followers.TryGetValue(u,out var s)&&!s.Active&&s.Definition==definition&&Current(s)&&Selected(leader.ControlledBy,"follow")==definition;
            bool Issue(Placement p)
            {
                if(!Valid(p.Unit))return false;
                var s=Followers[p.Unit];
                s.Approach=p.Approach;s.Phase=0;
                if(!frame.Rows.ContainsKey(p.Unit))frame.Rows[p.Unit]=frame.Rows.Count==0?0:Math.Min(4,frame.Rows.Values.Max()+1);
                s.DepartAt=frame.DepartureStart+frame.Rows[p.Unit]*frame.DepartureDelay;
                s.Slot=p.Slot;s.Destination=p.Point;s.Radius=p.Radius;s.Active=true;s.NextAttempt=Time.unscaledTime+1.5f;
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
                if(!s.Active||s.Phase>=2||Time.unscaledTime<s.DepartAt||!Current(s))continue;
                if(s.Phase==1&&(DistanceSq(s.Unit.transform.position,s.Approach)>9||Math.Abs(s.Unit.transform.position.y-s.Approach.y)>3))continue;
                if(++work>12)break;
                if(IssueFixedMove(s,s.Phase==0?s.Approach:s.Destination)){s.Phase++;RefreshFollowDepartureCaps();}
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
            try {return s.Unit.OrderAgent.IssueOrder(OrderDefinitionRegistry.Move,in target,in parameters);}
            finally {generatedDepth--;}
        }
    }
}
