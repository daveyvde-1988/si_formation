using System;
using System.Collections.Generic;
using System.Linq;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class RetryUnit
        {
            internal Unit Unit;
            internal AIGroup Group;
            internal uint[] Orders;
        }
        private sealed class PlacementRetry
        {
            internal Player Player;
            internal Team Team;
            internal AIGroup Group;
            internal bool Commander;
            internal string Function;
            internal FormationDefinition Definition;
            internal Vector3 Centre,Forward;
            internal AgentMoveSpeed Speed;
            internal int Attempt;
            internal float NextAttempt;
            internal AttackOperation Attack;
            internal SoftGroup Soft;
            internal IEnumerator<bool> Search;
            internal readonly List<RetryUnit> Pending=new List<RetryUnit>();
            internal readonly List<Placement> Reserved=new List<Placement>();
        }
        private static readonly List<PlacementRetry> PlacementRetries=new List<PlacementRetry>();
        private static string UnitId(Unit u)=>u&&u.ObjectInfo?u.ObjectInfo.name:"";
        private static string PreferenceType(Unit u)=>u?EditorCategories.For(u.ObjectInfo):"None";
        private static IEnumerable<Unit> PlacementOrder(FormationDefinition f,IEnumerable<Unit> units)
            =>units.Select(u=>new {Unit=u,Match=f.Specificity(UnitId(u),PreferenceType(u))})
                .OrderBy(v=>v.Match.Item1).ThenBy(v=>v.Match.Item2)
                .ThenByDescending(v=>v.Unit.PhysicalRadius).ThenBy(v=>v.Unit.GetInstanceID()).Select(v=>v.Unit);
        private static void QueuePlacementRetries(Player player,string function,FormationDefinition f,Unit[] units,
            List<Placement> issued,Vector3 centre,Vector3 forward,AgentMoveSpeed speed,AttackOperation attack=null)
        {
            foreach(var u in units)foreach(var old in PlacementRetries)old.Pending.RemoveAll(p=>p.Unit==u);
            var batch=new PlacementRetry {Player=player,Team=player.Team,Group=player.Group,Commander=player.IsCommander,
                Function=function,Definition=f,Centre=centre,Forward=forward,Speed=speed,Attack=attack};
            batch.Soft=RememberSelection(player,function,f,units,forward);
            batch.Reserved.AddRange(issued);
            foreach(var unit in units.Where(u=>!issued.Any(p=>p.Unit==u)))
                batch.Pending.Add(new RetryUnit {Unit=unit,Group=unit.AIGroup,Orders=unit.OrderAgent.Orders.Select(o=>o.Id).ToArray()});
            if(batch.Pending.Count>0)PlacementRetries.Add(batch);
        }
        private static void ClearOrders(){ClearLocks();Operations.Clear();PlacementRetries.Clear();SoftGroups.Clear();SoftMotions.Clear();DepartureSpeeds.Clear();SoftSpeedCaps.Clear();}
        private static bool BatchCurrent(PlacementRetry b)=>b.Player&&Player.Players.Contains(b.Player)&&b.Player.Team==b.Team&&b.Player.IsCommander==b.Commander&&
            (b.Commander||b.Player.Group==b.Group)&&FunctionActive(b.Player,b.Function)&&Selected(b.Player,b.Function)==b.Definition&&
            (b.Attack==null||Operations.Contains(b.Attack));
        private static bool PendingCurrent(PlacementRetry b,Unit unit)
        {
            var p=b.Pending.FirstOrDefault(v=>v.Unit==unit);
            return p!=null&&Available(unit)&&unit.Team==b.Team&&unit.AIGroup==p.Group&&
                (unit.OrderAgent.Orders.Count==0||p.Orders.SequenceEqual(unit.OrderAgent.Orders.Select(o=>o.Id)));
        }
        private static int placementCursor;
        private static void TickPlacementRetries()
        {
            if(PlacementRetries.Count==0)return;
            var clock=System.Diagnostics.Stopwatch.StartNew();
            for(int work=0,steps=0;work<12&&steps<1024&&clock.ElapsedMilliseconds<2&&PlacementRetries.Count>0;steps++)
            {
                var batch=PlacementRetries[(placementCursor++&int.MaxValue)%PlacementRetries.Count];
                if(!BatchCurrent(batch)){PlacementRetries.Remove(batch);continue;}
                if(batch.Reserved.RemoveAll(p=>!Available(p.Unit)||p.Unit.Team!=batch.Team)>0)
                {batch.Search?.Dispose();batch.Search=null;batch.Attempt=Math.Max(0,batch.Attempt-1);}
                batch.Pending.RemoveAll(p=>!PendingCurrent(batch,p.Unit));
                if(batch.Pending.Count==0){FinishPlacement(batch);continue;}
                if(Time.unscaledTime<batch.NextAttempt)continue;
                if(batch.Search==null)
                {
                    batch.Attempt++;
                    batch.Search=SearchRemembered(batch.Soft.Slots,batch.Definition,batch.Pending.Select(p=>p.Unit).ToArray(),batch.Centre,batch.Forward,batch.Reserved,
                        u=>PendingCurrent(batch,u),p=>IssuePlacement(batch,p),batch.Soft.Mirrored).GetEnumerator();
                }
                if(batch.Search.MoveNext()){if(batch.Search.Current)work++;continue;}
                batch.Search.Dispose();batch.Search=null;
                if(batch.Pending.Count==0||batch.Attempt>=3)FinishPlacement(batch);
                else batch.NextAttempt=Time.unscaledTime+1.5f;
            }
        }
        private static bool IssuePlacement(PlacementRetry batch,Placement p)
        {
            if(!PendingCurrent(batch,p.Unit))return false;
            batch.Soft.Slots[p.Unit]=p.Slot;
            SoftMotions.Add(new SoftMotion {Batch=batch,Placement=p,Group=p.Unit.AIGroup,
                Previous=p.Unit.OrderAgent.Orders.Select(o=>o.Id).ToArray()});
            batch.Pending.RemoveAll(v=>v.Unit==p.Unit);
            return true;
        }
        private static void FinishPlacement(PlacementRetry batch)
        {
            PlacementRetries.Remove(batch);batch.Search?.Dispose();batch.Search=null;
            StartSoftDepartures(batch);
            if(batch.Pending.Count>0)Reply(batch.Player,batch.Reserved.Count+" units placed; "+batch.Pending.Count+" could not use any remaining position after three complete colour passes.");
        }
    }
}
