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
        private sealed class ReverseInfantryMove
        {
            internal AIOrderProcessor Processor;
            internal MoveOrderDefinition Definition;
            internal Vector3 Destination;
            internal AgentMoveSpeed Mode;
            internal uint[] PreviousOrders;
        }
        private static CohesionDispatch PrepareReverseFormation(IReadOnlyList<BaseGameObject> objects,Player owner,
            Vector3 destination,bool queued,OrderDefinition definition,AgentMoveSpeed mode)
        {
            if(!owner)return null;
            var moveDefinition=OrderDefinitionRegistry.Move;
            var units=objects.OfType<Unit>().Where(unit=>unit&&!unit.IsDestroyed&&unit.Team==owner.Team&&
                unit.OrderAgent&&unit.OrderAgent.Receiver!=null&&
                (definition.GetAvailability(unit.OrderAgent.Receiver)==EOrderAvailability.Available||
                    ReverseInfantryCanMove(unit,moveDefinition))).Distinct().ToList();
            bool paced=CohesionEnabledFor(owner.Team);
            if(!paced&&!units.Any(unit=>ReverseInfantryCanMove(unit,moveDefinition)))return null;
            var context=new CohesionDispatch{Owner=owner,Team=owner.Team,Anchor=destination,Queued=queued,Reverse=true,Unpaced=!paced};
            if(units.Count==0){context.Skip="no eligible reverse or infantry movement";return context;}
            if(units.Count>(int)CohesionValueFor(owner.Team,"MaxUnits"))
            {context.Skip="reverse selection exceeds cohesion unit limit";return context;}
            if(units.Any(unit=>!(unit is Vehicle)&&!(unit is Soldier)||!CohesionEligible(unit)))
            {context.Skip="reverse selection includes unsupported or player-driven movement";return context;}
            var origins=new Dictionary<Unit,Vector3>();
            foreach(var unit in units)
            {
                Vector3 point=unit.transform.position;
                if(queued&&unit.OrderAgent.Orders.Count>0)
                {
                    var preceding=unit.OrderAgent.Orders[unit.OrderAgent.Orders.Count-1];
                    if(!(preceding is AIMoveOrder)&&!(preceding is AIReverseOrder))
                    {context.Skip="queued reverse predecessor is not move/reverse";return context;}
                    point=preceding.GetPosition();
                }
                origins[unit]=point;context.Origin+=point;
            }
            context.Origin/=units.Count;
            context.Heading=FormationHeading(units,Vector3.forward);
            if(CohesionLayouts.TryGetValue(units[0],out var precedingLayout)&&
                CohesionLayoutSelectionMatches(precedingLayout,units)&&
                (LayoutMovePending(precedingLayout)||Time.time-precedingLayout.Time<=CohesionValueFor(owner.Team,"Retention")))
            {
                if(precedingLayout.DragOrigin)
                {
                    context.RememberedDrag=true;
                    context.OriginalCount=System.Math.Max(precedingLayout.OriginalCount,precedingLayout.Offsets.Count);
                }
                if(queued&&precedingLayout.Forward.sqrMagnitude>.01f)context.Heading=precedingLayout.Forward;
            }
            var points=new Dictionary<Unit,Vector3>();
            foreach(var unit in units)
            {
                // Translate the current (or queued predecessor's) snapshot only.
                // Never turn its world-space offsets toward the clicked destination.
                Vector3 desired=destination+Flat(origins[unit]-context.Origin);
                if(!TerrainProjection.TryProject(unit,desired,out var projected)||FlatDistanceSq(desired,projected)>1)
                {context.Skip="reverse snapshot could not be projected without distortion";return context;}
                points[unit]=projected;
            }
            for(int i=0;i<units.Count;i++)for(int j=i+1;j<units.Count;j++)
            {
                float clearance=Radius(units[i])+Radius(units[j])+.5f;
                if(FlatDistanceSq(points[units[i]],points[units[j]])<clearance*clearance)
                {context.Skip="reverse snapshot would lose physical clearance";return context;}
            }
            foreach(var unit in units)context.Candidates.Add(unit);
            foreach(var unit in units)
            {
                if(definition.GetAvailability(unit.OrderAgent.Receiver)==EOrderAvailability.Available)continue;
                if(context.ReverseInfantryMoves==null)context.ReverseInfantryMoves=new Dictionary<Unit,ReverseInfantryMove>();
                context.ReverseInfantryMoves[unit]=new ReverseInfantryMove{Processor=unit.OrderAgent,Definition=moveDefinition,
                    Destination=points[unit],Mode=mode,PreviousOrders=unit.OrderAgent.Orders.Select(order=>order.Id).ToArray()};
            }
            if(paced)activeLayout=new LayoutContext{Settings=ServerSettings,Team=owner.Team,Points=points,
                Pitch=units.Min(Radius)*2,Scale=Multiplier(ServerSettings.Spacing)*FactionScale(owner.Team,ServerSettings)};
            return context;
        }
        private static bool ReverseInfantryCanMove(Unit unit,MoveOrderDefinition definition)=>unit is Soldier&&
            CohesionEligible(unit)&&definition&&unit.OrderAgent.Receiver!=null&&
            definition.GetAvailability(unit.OrderAgent.Receiver)==EOrderAvailability.Available;
        private static void IssueReverseInfantryMoves(CohesionDispatch context)
        {
            if(context.ReverseInfantryMoves==null)return;
            foreach(var item in context.ReverseInfantryMoves)
            {
                var unit=item.Key;var move=item.Value;
                if(!Active||!ReferenceEquals(activeCohesion,context))return;
                if(context.Issued.ContainsKey(unit)||!unit||unit.Team!=context.Team||unit.OrderAgent!=move.Processor||
                    !ReverseInfantryCanMove(unit,move.Definition))continue;
                // The native Reverse batch never orders these soldiers. Preserve
                // any intervening replacement or queued append from another owner.
                var orders=move.Processor.Orders;
                if(orders.Count!=move.PreviousOrders.Length)continue;
                bool unchanged=true;
                for(int i=0;i<orders.Count;i++)if(orders[i].Id!=move.PreviousOrders[i]){unchanged=false;break;}
                if(!unchanged)continue;
                var attempt=BeginMovementAttempt(unit,context.Anchor,move.Destination,"reverse infantry Move fallback");
                try
                {
                    if(!context.Queued)UnitCohesionGroup.Detach(unit);
                    bool accepted=move.Processor.IssueOrder(move.Definition,new OrderTarget(move.Destination),
                        OrderIssueParams.Commanded(move.Mode,context.Queued,1));
                    EndMovementAttempt(attempt,accepted);
                    if(!accepted)continue;
                    context.AcceptedReverseInfantryMoves++;
                    CohesionIssuedOrder(unit,move.Destination,move.Mode,true);
                }
                catch(Exception e)
                {
                    EndMovementAttempt(attempt,false);
                    MelonLogger.Warning("Reverse infantry move skipped: "+e.Message);
                }
            }
        }
        private static void ConfigureReverseCohesion(CohesionTuning tuning)
        {
            // Reverse explicitly requests strict snapshot pacing rather than the
            // distance-based relaxation used by ordinary journeys. Native combat,
            // path safety, order ownership and the existing lifetime bounds win.
            tuning.Range="Reverse";tuning.Strength=1;tuning.HoldStrength=1;
            tuning.MinimumSpeed=0;tuning.ArrivalWindow=.25f;tuning.MaxDelay=120;
            tuning.HoldError=.5f;tuning.HoldCorrectionInterval=.1f;
            tuning.HoldBreakPercent=100;tuning.ObstructedPercent=50;
        }
    }
}
