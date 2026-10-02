using System.Linq;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static void PrepareInitialMove(PlacementRetry batch)
        {
            if(batch.Soft.Slots.Count!=0||batch.Pending.Count==0)return;
            batch.TwoStage=true;
            var rows=SoftDepartureBatches(batch.Pending.Select(p=>p.Unit),batch.Forward);
            var motions=batch.Pending.Select(p=>new SoftMotion {Batch=batch,Group=p.Group,Previous=p.Orders,
                Initial=true,Departure=Time.unscaledTime+rows[p.Unit]*SoftDelay,
                Placement=new Placement {Unit=p.Unit,Slot=-1,Point=batch.Centre}}).ToArray();
            SoftMotions.AddRange(motions);
            BeginDepartureSpeed(batch,motions,rows);
        }
        // One native batch per scheduler turn. The game computes genuine vanilla
        // placement; no approximate grid or custom-slot scan precedes this dispatch.
        private static int TickInitialMove(PlacementRetry batch)
        {
            var waiting=SoftMotions.Where(m=>m.Batch==batch&&m.Initial).ToArray();
            foreach(var m in waiting.Where(m=>!PendingCurrent(batch,m.Placement.Unit)))
            {SoftMotions.Remove(m);RemoveDepartureUnit(m.Placement.Unit);}
            waiting=waiting.Where(m=>SoftMotions.Contains(m)).ToArray();
            if(waiting.Length==0)return 0;
            float due=waiting.Min(m=>m.Departure);
            if(Time.unscaledTime<due)return -1;
            var row=waiting.Where(m=>m.Departure==due).ToArray();
            var objects=row.Select(m=>(BaseGameObject)m.Placement.Unit).ToList();
            generatedDepth++;
            try {StrategyMode.PerformMoveAttack(objects,batch.Centre,null,batch.Speed,false,false,false);}
            finally {generatedDepth--;}
            foreach(var m in row)
            {
                var unit=m.Placement.Unit;
                var pending=batch.Pending.FirstOrDefault(p=>p.Unit==unit);
                if(pending!=null&&Available(unit))
                {
                    // Only the internally generated native replacement is adopted.
                    pending.Orders=unit.OrderAgent.Orders.Select(o=>o.Id).ToArray();
                    m.Placement.OrderId=pending.Orders.Length==1?pending.Orders[0]:0;
                    DepartedSoftMotion(m);
                }
                else RemoveDepartureUnit(unit);
                SoftMotions.Remove(m);
            }
            return row.Length;
        }
    }
}
