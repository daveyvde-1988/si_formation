using System;
using System.Collections.Generic;
using System.Linq;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class SoftGroup
        {
            internal Player Player;
            internal Team Team;
            internal bool Commander,Mirrored;
            internal Vector3 Forward;
            internal string Function;
            internal FormationDefinition Definition;
            internal readonly Dictionary<Unit,int> Slots=new Dictionary<Unit,int>();
            internal readonly Dictionary<Unit,AIGroup> Groups=new Dictionary<Unit,AIGroup>();
        }
        private sealed class SoftMotion
        {
            internal PlacementRetry Batch;
            internal Placement Placement;
            internal AIGroup Group;
            internal uint[] Previous;
            internal float Departure=float.PositiveInfinity;
            internal bool Approaching;
        }
        private static readonly List<SoftGroup> SoftGroups=new List<SoftGroup>();
        private static readonly List<SoftMotion> SoftMotions=new List<SoftMotion>();
        private static void ClearFormationSelection(IEnumerable<Unit> units)
        {
            foreach(var u in units)
            {
                CancelUnit(u);Followers.Remove(u);Superseded.Remove(u);
            }
        }
        private static void PrepareFormationSelection(Player player,Unit[] units)
        {
            var selected=new HashSet<Unit>(units);
            // Include pending members, not just those whose slot search has finished.
            bool partial=SoftGroups.Where(g=>g.Player==player).Any(g=>
                g.Groups.Keys.Any(selected.Contains)&&g.Groups.Keys.Any(u=>Available(u)&&u.Team==player.Team&&!selected.Contains(u)))||
                Locks.Where(g=>g.Owner==player).Any(g=>g.Members.Any(m=>selected.Contains(m.Unit))&&
                    g.Members.Any(m=>LockEligible(m.Unit,player)&&!selected.Contains(m.Unit)));
            if(!partial)return;
            // Clear the entire new selection's cache; do not change unselected units or
            // their AIGroup membership, destinations, or scheduled departures.
            foreach(var u in units)ForgetSoftUnit(u);
        }
        private static void ForgetSoftUnit(Unit u)
        {
            foreach(var g in SoftGroups){g.Slots.Remove(u);g.Groups.Remove(u);}
            SoftGroups.RemoveAll(g=>g.Slots.Count==0&&!PlacementRetries.Any(b=>b.Soft==g)&&!SoftMotions.Any(m=>m.Batch.Soft==g));
        }
        private static SoftGroup RememberSelection(Player player,string function,FormationDefinition f,Unit[] units,Vector3 forward)
        {
            var selected=new HashSet<Unit>(units);
            var old=SoftGroups.Where(g=>g.Player==player&&g.Definition==f&&g.Function==function&&
                    g.Groups.Keys.All(u=>!Available(u)||selected.Contains(u)))
                .OrderByDescending(g=>units.Count(u=>g.Slots.ContainsKey(u))).FirstOrDefault();
            var next=new SoftGroup {Player=player,Team=player.Team,Commander=player.IsCommander,Function=function,Definition=f,Forward=forward,
                Mirrored=f.DirectionSensitive&&old!=null&&(old.Mirrored^Reversed(old.Forward,forward))};
            foreach(var u in units)
            {
                if(old!=null&&old.Groups.TryGetValue(u,out var oldGroup)&&u.AIGroup==oldGroup&&old.Slots.TryGetValue(u,out int slot))next.Slots[u]=slot;
                next.Groups[u]=u.AIGroup;
            }
            foreach(var u in units)ForgetSoftUnit(u);
            SoftGroups.Add(next);return next;
        }
        private static bool ApproachPoint(Unit u,Vector3 point,Vector3 forward,out Vector3 approach)
        {
            if(!TerrainProjection.TryProject(u,point-forward*10,out approach))return false;
            // Reject navigation snaps that erase or reverse the final forward approach.
            if(Vector3.Dot(Flat(point-approach),forward)<5)return false;
            var target=new OrderTarget(approach);
            return u.OrderAgent.CanIssueOrder(OrderDefinitionRegistry.Move,in target);
        }
        private static IEnumerable<bool> SearchRemembered(Dictionary<Unit,int> saved,FormationDefinition f,Unit[] units,
            Vector3 centre,Vector3 forward,List<Placement> reserved,Func<Unit,bool> current,Func<Placement,bool> issue,bool mirrored=false)
        {
            foreach(var u in units)
            {
                if(!current(u)||reserved.Any(p=>p.Unit==u)||!saved.TryGetValue(u,out int index))continue;
                if(index<0||index>=f.Slots.Length||reserved.Any(p=>p.Slot==index)){saved.Remove(u);continue;}
                var slot=f.Slots[index];
                if(!FormationDefinition.Allows(slot,UnitId(u),PreferenceType(u))){saved.Remove(u);continue;}
                float radius=Radius(u,f,slot);
                bool valid=TerrainProjection.TryProject(u,World(slot,f,centre,forward,mirrored),out var point)&&Separated(point,radius,reserved);
                yield return true;
                if(!current(u))continue;
                var target=new OrderTarget(point);
                Vector3 approach=point;
                valid=valid&&u.OrderAgent.CanIssueOrder(OrderDefinitionRegistry.Move,in target)&&
                    ApproachPoint(u,point,f.DirectionSensitive?forward:Vector3.forward,out approach);
                yield return true;
                if(!current(u))continue;
                var p=new Placement {Unit=u,Slot=index,Point=point,Approach=approach,Radius=radius};
                if(valid&&issue(p))reserved.Add(p);else saved.Remove(u);
                yield return true;
            }
            foreach(var step in SearchPositions(f,units,centre,forward,reserved,current,issue,mirrored))yield return step;
        }
        private static void StartSoftDepartures(PlacementRetry batch)
        {
            var waiting=SoftMotions.Where(m=>m.Batch==batch&&float.IsPositiveInfinity(m.Departure)).ToArray();
            var rows=SoftDepartureBatches(waiting.Select(m=>m.Placement.Unit),batch.Forward);
            foreach(var m in waiting)m.Departure=Time.unscaledTime+rows[m.Placement.Unit]*SoftDelay;
            BeginDepartureSpeed(batch,waiting,rows);
            if(batch.Attack!=null)batch.Attack.Deadline=Time.unscaledTime+30;
        }
        private static void RetrySoftMotion(SoftMotion m)
        {
            SoftMotions.Remove(m);
            RemoveDepartureUnit(m.Placement.Unit);
            var b=m.Batch;var u=m.Placement.Unit;
            b.Soft.Slots.Remove(u);b.Reserved.Remove(m.Placement);
            b.Reserved.RemoveAll(p=>!Available(p.Unit)||!b.Soft.Slots.ContainsKey(p.Unit));
            b.Search?.Dispose();b.Search=null;
            if(b.Attempt>=3||!Available(u))return;
            if(!b.Pending.Any(p=>p.Unit==u))b.Pending.Add(new RetryUnit {Unit=u,Group=u.AIGroup,Orders=u.OrderAgent.Orders.Select(o=>o.Id).ToArray()});
            b.NextAttempt=Time.unscaledTime+1.5f;
            if(!PlacementRetries.Contains(b))PlacementRetries.Add(b);
        }
        private static int motionCursor;
        private static void TickSoftMotions()
        {
            foreach(var g in SoftGroups.ToArray())
            {
                if(!g.Player||!Player.Players.Contains(g.Player)||g.Player.Team!=g.Team||g.Player.IsCommander!=g.Commander||
                    !FunctionActive(g.Player,g.Function)||Selected(g.Player,g.Function)!=g.Definition){SoftGroups.Remove(g);continue;}
                foreach(var u in g.Slots.Keys.ToArray())if(!Available(u)||u.PlayerControlled||u.IsFlyingType||u.Team!=g.Team||
                    !g.Groups.TryGetValue(u,out var group)||u.AIGroup!=group){g.Slots.Remove(u);g.Groups.Remove(u);}
            }
            var clock=System.Diagnostics.Stopwatch.StartNew();
            int count=SoftMotions.Count;
            for(int step=0,work=0;step<count&&work<12&&clock.ElapsedMilliseconds<2&&SoftMotions.Count>0;step++)
            {
                var m=SoftMotions[(motionCursor++&int.MaxValue)%SoftMotions.Count];var p=m.Placement;var u=p.Unit;
                if(!SoftGroups.Contains(m.Batch.Soft)||!BatchCurrent(m.Batch)||!Available(u)||u.PlayerControlled||u.IsFlyingType||u.Team!=m.Batch.Team||u.AIGroup!=m.Group||
                    (u.OrderAgent.Orders.Count>0&&(m.Approaching?
                        u.OrderAgent.Orders.Count!=1||u.OrderAgent.Orders[0].Id!=p.OrderId:
                        !m.Previous.SequenceEqual(u.OrderAgent.Orders.Select(o=>o.Id)))))
                {SoftMotions.Remove(m);m.Batch.Reserved.Remove(p);m.Batch.Soft.Slots.Remove(u);continue;}
                if(Time.unscaledTime<m.Departure)continue;
                if(m.Approaching&&(DistanceSq(u.transform.position,p.Approach)>9||Math.Abs(u.transform.position.y-p.Approach.y)>3))continue;
                work++;
                var target=new OrderTarget(m.Approaching?p.Point:p.Approach);
                var parameters=OrderIssueParams.Commanded(m.Batch.Speed);
                bool issued;
                generatedDepth++;
                try
                {
                    UnitCohesionGroup.Detach(u);
                    issued=u.OrderAgent.IssueOrder(OrderDefinitionRegistry.Move,in target,in parameters);
                }
                finally{generatedDepth--;}
                if(!issued){RetrySoftMotion(m);continue;}
                p.OrderId=u.OrderAgent.Orders.Count>0?u.OrderAgent.Orders[0].Id:0;
                if(!m.Approaching){m.Approaching=true;DepartedSoftMotion(m);continue;}
                SoftMotions.Remove(m);
                if(m.Batch.Attack!=null)
                {
                    m.Batch.Attack.Members.Add(p);
                    m.Batch.Attack.Deadline=Time.unscaledTime+30;
                }
            }
        }
    }
}
