using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Silica.AI;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class FpsTravelMember
        {
            internal Placement Placement;
            internal AIGroup Group;
            internal float OutsideSince=-1;
            internal bool Recovering;
        }
        private sealed class FpsTravel
        {
            internal PlacementRetry Batch;
            internal readonly List<FpsTravelMember> Members=new List<FpsTravelMember>();
        }
        private sealed class PaceSample
        {
            internal Unit Unit;
            internal float Position,Offset,Radius,OutsideSince=-1;
            internal bool Recovering;
            internal Action<bool,float> Save;
        }
        private static readonly List<FpsTravel> FpsTravels=new List<FpsTravel>();
        private static readonly Dictionary<Unit,float> FpsSpeedCaps=new Dictionary<Unit,float>();
        private static float nextFpsPace;

        private static void RegisterFpsTravel(PlacementRetry batch,Placement placement)
        {
            if(batch.Commander)return;
            var group=FpsTravels.FirstOrDefault(g=>g.Batch==batch);
            if(group==null){group=new FpsTravel {Batch=batch};FpsTravels.Add(group);}
            group.Members.RemoveAll(m=>m.Placement.Unit==placement.Unit);
            group.Members.Add(new FpsTravelMember {Placement=placement,Group=placement.Unit.AIGroup});
        }
        private static void RemoveFpsTravelUnit(Unit unit)
        {
            FpsSpeedCaps.Remove(unit);
            foreach(var group in FpsTravels)group.Members.RemoveAll(m=>m.Placement.Unit==unit);
        }
        private static void ClearFpsSpeedCaps(Player owner)
        {
            foreach(var group in FpsTravels.Where(g=>g.Batch.Player==owner))
                foreach(var member in group.Members)FpsSpeedCaps.Remove(member.Placement.Unit);
            foreach(var s in Followers.Values.Where(s=>s.Leader&&s.Leader.ControlledBy==owner))FpsSpeedCaps.Remove(s.Unit);
        }
        private static void ClearFpsTravel(Player owner=null)
        {
            if(ReferenceEquals(owner,null)){FpsTravels.Clear();FpsSpeedCaps.Clear();return;}
            ClearFpsSpeedCaps(owner);FpsTravels.RemoveAll(g=>g.Batch.Player==owner);
        }
        private static bool OwnedMove(Unit unit,uint order)
            =>Available(unit)&&order!=0&&unit.OrderAgent.Orders.Count==1&&
                unit.OrderAgent.Orders[0] is Silica.AI.AIMoveOrder&&unit.OrderAgent.Orders[0].Id==order;

        private static void TickFpsCoordination()
        {
            if(Time.unscaledTime<nextFpsPace)return;
            nextFpsPace=Time.unscaledTime+0.2f;
            FpsSpeedCaps.Clear();
            foreach(var group in FpsTravels.ToArray())
            {
                var b=group.Batch;
                if(!BatchCurrent(b)){FpsTravels.Remove(group);continue;}
                group.Members.RemoveAll(m=>!OwnedMove(m.Placement.Unit,m.Placement.OrderId)||m.Placement.Unit.AIGroup!=m.Group);
                if(group.Members.Count==0){FpsTravels.Remove(group);continue;}
                bool waiting=PlacementRetries.Contains(b)||SoftMotions.Any(m=>m.Batch==b);
                if(!waiting&&group.Members.All(m=>DistanceSq(m.Placement.Unit.transform.position,m.Placement.Point)<=9))
                {FpsTravels.Remove(group);continue;}
                if(!For(b.Player).FollowFacingLock)continue;
                var samples=group.Members.Select(m=>new PaceSample
                {
                    Unit=m.Placement.Unit,Radius=m.Placement.Radius,
                    Position=Vector3.Dot(Flat(m.Placement.Unit.transform.position),b.TravelForward),
                    Offset=Vector3.Dot(Flat(m.Placement.Point-b.Centre),b.TravelForward),
                    OutsideSince=m.OutsideSince,Recovering=m.Recovering,
                    Save=(recovering,since)=>{m.Recovering=recovering;m.OutsideSince=since;}
                }).ToArray();
                ApplyFpsPace(samples);
            }
            foreach(var entry in Frames.ToArray())
            {
                var leader=entry.Key;var frame=entry.Value;
                if(!leader||!leader.ControlledBy||!For(leader.ControlledBy).FollowFacingLock)continue;
                var members=Followers.Values.Where(s=>s.Leader==leader&&s.Active&&s.Phase>=2&&Current(s)&&OwnedMove(s.Unit,s.Order)).ToArray();
                if(members.Length==0)continue;
                var samples=members.Select(s=>new PaceSample
                {
                    Unit=s.Unit,Radius=s.Radius,
                    Position=Vector3.Dot(Flat(s.Unit.transform.position),frame.Forward),
                    Offset=Vector3.Dot(Flat(World(s.Definition.Slots[s.Slot],s.Definition,Vector3.zero,frame.Forward,frame.Mirrored)),frame.Forward),
                    OutsideSince=s.OutsideSince,Recovering=s.Recovering,
                    Save=(recovering,since)=>{s.Recovering=recovering;s.OutsideSince=since;}
                }).ToArray();
                ApplyFpsPace(samples);
            }
        }

        private static void ApplyFpsPace(PaceSample[] members)
        {
            if(members.Length==0)return;
            var referenceMembers=members.Where(m=>!m.Recovering).ToArray();
            if(referenceMembers.Length==0)referenceMembers=members;
            var centres=referenceMembers.Select(m=>m.Position-m.Offset).OrderBy(v=>v).ToArray();
            float reference=centres[centres.Length/2];
            float baseline=referenceMembers.Min(m=>Math.Max(0.1f,m.Unit.TopSpeed));
            foreach(var m in members)
            {
                float error=reference+m.Offset-m.Position;
                float tolerance=Math.Max(3,m.Radius*1.5f);
                float outside=Math.Max(25,tolerance*4);
                if(error>outside)
                {
                    if(m.OutsideSince<0)m.OutsideSince=Time.unscaledTime;
                    if(Time.unscaledTime-m.OutsideSince>=5)m.Recovering=true;
                }
                else if(error<outside*0.6f){m.Recovering=false;m.OutsideSince=-1;}
                m.Save(m.Recovering,m.OutsideSince);
                if(m.Recovering)continue;
                float correction=error>tolerance?(error-tolerance)*0.5f:error< -tolerance?(error+tolerance)*0.5f:0;
                FpsSpeedCaps[m.Unit]=Mathf.Clamp(baseline+correction,0,m.Unit.TopSpeed);
            }
        }
    }
}
