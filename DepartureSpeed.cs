using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class DepartureMember
        {
            internal SoftMotion Motion;
            internal int Stage;
            internal bool Departed;
        }
        private sealed class DepartureSpeed
        {
            internal PlacementRetry Batch;
            internal int LastStage;
            internal readonly List<DepartureMember> Members=new List<DepartureMember>();
        }
        private static readonly List<DepartureSpeed> DepartureSpeeds=new List<DepartureSpeed>();
        private static readonly Dictionary<Unit,float> SoftSpeedCaps=new Dictionary<Unit,float>();
        private static readonly Dictionary<Unit,float> FollowDepartureCaps=new Dictionary<Unit,float>();
        private static void RefreshFollowDepartureCaps()
        {
            FollowDepartureCaps.Clear();
            foreach(var frame in Frames.Values)
            {
                if(!frame.Holding||frame.DepartureDelay<=0)continue;
                var members=Followers.Values.Where(s=>s.Active&&Current(s)&&Frames.TryGetValue(s.Leader,out var owner)&&owner==frame&&
                    DistanceSq(s.Leader.transform.position,frame.Anchor)<=FollowReleaseDistance(s.Leader)*FollowReleaseDistance(s.Leader)).ToArray();
                if(members.Length<2||members.All(s=>s.Phase>0))continue;
                int last=members.Max(s=>frame.Rows[s.Unit]);
                if(last==0)continue;
                int stage=0;
                while(stage<last&&members.Where(s=>frame.Rows[s.Unit]<=stage+1).All(s=>s.Phase>0))stage++;
                float baseline=members.Min(s=>Math.Max(0,s.Unit.TopSpeed));
                foreach(var s in members.Where(s=>s.Phase>0))
                {
                    float cap=baseline+Math.Max(0,s.Unit.TopSpeed-baseline)*stage/last;
                    if(cap<s.Unit.TopSpeed)FollowDepartureCaps[s.Unit]=cap;
                }
            }
        }
        private static void BeginDepartureSpeed(PlacementRetry batch,SoftMotion[] motions,Dictionary<Unit,int> rows)
        {
            if(SoftDelay<=0||motions.Length<2)return;
            var group=new DepartureSpeed {Batch=batch,LastStage=rows.Values.Max()};
            if(group.LastStage==0)return;
            foreach(var motion in motions)group.Members.Add(new DepartureMember {Motion=motion,Stage=rows[motion.Placement.Unit]});
            DepartureSpeeds.Add(group);
        }
        private static void DepartedSoftMotion(SoftMotion motion)
        {
            foreach(var group in DepartureSpeeds)
                foreach(var member in group.Members)if(member.Motion==motion)member.Departed=true;
            RefreshDepartureSpeeds(); // Last successful dispatch releases every cap synchronously.
        }
        private static void RemoveDepartureUnit(Unit unit)
        {
            foreach(var group in DepartureSpeeds)group.Members.RemoveAll(m=>m.Motion.Placement.Unit==unit);
            SoftSpeedCaps.Remove(unit);
            FollowDepartureCaps.Remove(unit);
            RefreshDepartureSpeeds();
        }
        private static void RefreshDepartureSpeeds()
        {
            SoftSpeedCaps.Clear();
            foreach(var group in DepartureSpeeds.ToArray())
            {
                if(!BatchCurrent(group.Batch)||!SoftGroups.Contains(group.Batch.Soft))
                {DepartureSpeeds.Remove(group);continue;}
                group.Members.RemoveAll(member=>
                {
                    var m=member.Motion;var u=m.Placement.Unit;
                    if(!Available(u)||u.Team!=group.Batch.Team||u.AIGroup!=m.Group)return true;
                    if(!member.Departed)return !SoftMotions.Contains(m);
                    return u.OrderAgent.Orders.Count!=1||u.OrderAgent.Orders[0].Id!=m.Placement.OrderId;
                });
                if(group.Members.Count==0||group.Members.All(m=>m.Departed))
                {DepartureSpeeds.Remove(group);continue;}
                // A partially dispatched stage does not raise the ceiling. This also handles
                // frame budgets spreading a single batch across multiple updates.
                int stage=0;
                while(stage<group.LastStage&&group.Members.Where(m=>m.Stage<=stage+1).All(m=>m.Departed))stage++;
                float baseline=group.Members.Min(m=>Math.Max(0,m.Motion.Placement.Unit.TopSpeed));
                float fraction=(float)stage/group.LastStage;
                foreach(var member in group.Members.Where(m=>m.Departed))
                {
                    var u=member.Motion.Placement.Unit;
                    float cap=baseline+Math.Max(0,u.TopSpeed-baseline)*fraction;
                    if(cap<u.TopSpeed)SoftSpeedCaps[u]=cap;
                }
            }
        }
    }
}
