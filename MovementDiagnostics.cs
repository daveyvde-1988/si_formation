using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class MovementCommandDiagnostics
        {
            internal int Id;
            internal Player Owner;
            internal Vector3 Clicked,LineEnd;
            internal AgentMoveSpeed Mode;
            internal bool Watched,Drag,Queued,Attack,Forced;
            internal readonly Dictionary<Unit,MovementUnitDiagnostics> Units=new Dictionary<Unit,MovementUnitDiagnostics>();
        }
        private sealed class MovementUnitDiagnostics
        {
            internal Unit Unit;
            internal bool Selected;
            internal string Eligibility;
            internal Vector3 InitialPosition;
            internal int Attempts,Accepted,Rejected;
            internal MovementAttempt Last,LastAccepted;
        }
        private sealed class MovementAttempt
        {
            internal MovementCommandDiagnostics Command;
            internal MovementUnitDiagnostics Record;
            internal Vector3 Raw,Projected,Target;
            internal string Source,OrderType;
            internal bool Finished,Accepted,HasOrder;
            internal uint OrderId;
        }
        private static MovementCommandDiagnostics activeMovementDiagnostics;
        private static readonly Dictionary<CohesionTravel,int> movementSampleCursors=new Dictionary<CohesionTravel,int>();
        private static int nextMovementDiagnosticId=1;

        private void InstallMovementDiagnostics()
        {
            // Both observer prefixes and the postfix intentionally share this one
            // state type. No other IssueFormationOrder patch uses this declaring type.
            var original=AccessTools.Method(typeof(StrategyMode),"IssueFormationOrder");
            HarmonyInstance.Patch(original,
                prefix:new HarmonyMethod(typeof(NativeMovementOrderObserver),nameof(NativeMovementOrderObserver.Before)){priority=HarmonyLib.Priority.First},
                postfix:new HarmonyMethod(typeof(NativeMovementOrderObserver),nameof(NativeMovementOrderObserver.After)));
            HarmonyInstance.Patch(original,
                prefix:new HarmonyMethod(typeof(NativeMovementOrderObserver),nameof(NativeMovementOrderObserver.AfterPlacement)){priority=HarmonyLib.Priority.Last});
        }
        private static class NativeMovementOrderObserver
        {
            internal static void Before(BaseGameObject obj,Vector3 worldPosition,out MovementAttempt __state)
            {__state=obj is Unit unit?BeginMovementAttempt(unit,worldPosition,worldPosition,"native formation"):null;}
            internal static void AfterPlacement(Vector3 worldPosition,MovementAttempt __state)
            {if(__state!=null)__state.Projected=__state.Target=worldPosition;}
            internal static void After(Vector3 worldPosition,bool __result,MovementAttempt __state)
            {if(__state!=null)__state.Target=worldPosition;EndMovementAttempt(__state,__result);}
        }
        private static string MovementNumber(float value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
        private static string MovementPoint(Vector3 point)=>"("+MovementNumber(point.x)+","+MovementNumber(point.y)+","+MovementNumber(point.z)+")";
        private static string MovementUnitName(Unit unit)
        {
            if(ReferenceEquals(unit,null))return "null";
            return unit.GetInstanceID()+":"+(unit&&unit.ObjectInfo?unit.ObjectInfo.name:unit.GetType().Name);
        }
        private static string MovementEligibility(Unit unit)
        {
            if(!unit||unit.IsDestroyed)return "destroyed/missing";
            if(unit.PlayerControlled||unit.ControlledBy)return "player controlled";
            if(unit.InCompartment)return "embarked";
            if(unit.Driver&&unit.Driver.ControlledBy)return "player-driven vehicle";
            if(!unit.AIControlled)return "not AI controlled";
            if(!unit.CanMove)return "cannot move";
            if(!unit.OrderAgent)return "no order processor";
            if(!unit.AIAgent||!unit.AIAgent.AgentPathfinding)return "no AI navigation";
            if(IsFormationAir(unit))return "air: independent of ground cohesion";
            if(unit is Vehicle vehicle&&vehicle.HasForceMove)return "forced vehicle movement";
            if(unit is Soldier soldier&&(soldier.IsJumping||soldier.IsClimbing||soldier.IsFalling))return "temporary infantry traversal";
            if(unit is CreatureDecapod creature&&(creature.IsSubmerged||creature.IsSubmergeTransition||creature.IsInFly||creature.IsWallClimbing||creature.IsJumping))return "temporary alien traversal";
            return "eligible ground AI";
        }
        private static MovementCommandDiagnostics BeginMovementDiagnostics(IReadOnlyList<BaseGameObject> objects,Player owner,
            Vector3 clicked,Vector3 lineEnd,AgentMoveSpeed mode,bool attack,bool queued,bool drag,bool forced,
            MovementCommandDiagnostics parent)
        {
            try
            {
                bool watched=owner&&(CohesionDebugNext.Contains(owner)||(parent!=null&&parent.Watched&&parent.Owner==owner));
                if(!CohesionDebugEnabled&&!watched)return null;
                var command=new MovementCommandDiagnostics{Id=nextMovementDiagnosticId++,Owner=owner,Clicked=clicked,LineEnd=lineEnd,
                    Mode=mode,Watched=watched,Attack=attack,Queued=queued,Drag=drag,Forced=forced};
                if(nextMovementDiagnosticId==int.MaxValue)nextMovementDiagnosticId=1;
                foreach(var unit in objects.OfType<Unit>().Where(unit=>unit).Distinct())
                    command.Units[unit]=new MovementUnitDiagnostics{Unit=unit,Selected=true,Eligibility=MovementEligibility(unit),InitialPosition=unit.transform.position};
                return command;
            }
            catch(Exception e){MelonLogger.Warning("Movement diagnostics unavailable: "+e.Message);return null;}
        }
        // Scouting uses the same observer record around its direct native order calls.
        private static MovementAttempt BeginMovementAttempt(Unit unit,Vector3 raw,Vector3 target,string source)
        {
            var command=activeMovementDiagnostics;
            if(command==null||ReferenceEquals(unit,null))return null;
            try
            {
                if(!command.Units.TryGetValue(unit,out var record))
                    command.Units[unit]=record=new MovementUnitDiagnostics{Unit=unit,Eligibility=MovementEligibility(unit),InitialPosition=unit?unit.transform.position:Vector3.zero};
                var attempt=new MovementAttempt{Command=command,Record=record,Raw=raw,Projected=target,Target=target,Source=source};
                record.Attempts++;record.Last=attempt;return attempt;
            }
            catch(Exception e){MelonLogger.Warning("Movement attempt diagnostics unavailable: "+e.Message);return null;}
        }
        private static void EndMovementAttempt(MovementAttempt attempt,bool accepted)
        {
            if(attempt==null||attempt.Finished)return;
            attempt.Finished=true;attempt.Accepted=accepted;
            try
            {
                var record=attempt.Record;
                if(accepted)
                {
                    record.Accepted++;record.LastAccepted=attempt;
                    var processor=record.Unit?record.Unit.OrderAgent:null;
                    if(processor&&processor.Orders.Count>0)
                    {
                        var order=processor.Orders[processor.Orders.Count-1];
                        attempt.HasOrder=true;attempt.OrderId=order.Id;attempt.OrderType=order.GetType().Name;
                    }
                }
                else record.Rejected++;
            }
            catch(Exception e){MelonLogger.Warning("Movement result diagnostics unavailable: "+e.Message);}
        }
        private static string MovementHead(Unit unit)
        {
            var processor=unit?unit.OrderAgent:null;
            if(!processor||processor.Orders.Count==0)return "none";
            var head=processor.Orders[0];
            return head.GetType().Name+"#"+head.Id+(head is AIMoveOrder move?"/"+move.MoveSpeed:"")+" queue="+processor.Orders.Count;
        }
        private static string MovementEnrollment(Unit unit,CohesionDispatch cohesion)
        {
            if(cohesion==null)return "not enrolled: no custom move cohort";
            if(cohesion.Skip!=null)return "not enrolled: "+cohesion.Skip;
            if(!cohesion.Issued.TryGetValue(unit,out var member))return cohesion.Candidates.Contains(unit)?
                "candidate without captured accepted Move":"not enrolled: excluded from ground cohort";
            return member.Group==null?"accepted cohort move; no controller":"group#"+member.Group.Id+"/"+member.Group.Phase;
        }
        private static void FinishMovementDiagnostics(MovementCommandDiagnostics command,CohesionDispatch cohesion,Exception failure)
        {
            if(command==null)return;
            try
            {
                var selected=command.Units.Values.Where(record=>record.Selected).ToArray();
                MelonLogger.Msg("[Move #"+command.Id+"] selected="+selected.Length+" attemptedUnits="+selected.Count(record=>record.Attempts>0)+
                    " nativeAttempts="+command.Units.Values.Sum(record=>record.Attempts)+" accepted="+command.Units.Values.Sum(record=>record.Accepted)+
                    " rejected="+command.Units.Values.Sum(record=>record.Rejected)+" missingAttempt="+selected.Count(record=>record.Attempts==0)+
                    " eligibleGround="+selected.Count(record=>record.Eligibility=="eligible ground AI")+" mode="+command.Mode+
                    " drag="+command.Drag+" queued="+command.Queued+" attack="+command.Attack+" forced="+command.Forced+
                    " clicked="+MovementPoint(command.Clicked)+(command.Drag?" lineEnd="+MovementPoint(command.LineEnd):"")+
                    (failure==null?"":" exception="+failure.GetType().Name+": "+failure.Message));
                foreach(var record in command.Units.Values)
                {
                    var unit=record.Unit;var attempt=record.Last;var accepted=record.LastAccepted;
                    string result=attempt==null?"no native attempt":!attempt.Finished?"attempt did not return":attempt.Accepted?"accepted":"rejected";
                    var processor=unit?unit.OrderAgent:null;
                    if(accepted!=null&&accepted.HasOrder&&processor&&!processor.Orders.Any(order=>order.Id==accepted.OrderId))
                        result+="; accepted order no longer queued (completed/cleared/replaced)";
                    float distance=unit&&attempt!=null?Vector3.Distance(Flat(unit.transform.position),Flat(attempt.Target)):-1;
                    float threshold=unit&&unit.AIAgent?unit.AIAgent.FinalWaypointThreshold:-1;
                    MelonLogger.Msg("[Move #"+command.Id+" unit "+MovementUnitName(unit)+"] "+(record.Selected?"selected":"added during native dispatch")+
                        " eligibility="+record.Eligibility+" initial="+MovementPoint(record.InitialPosition)+" status="+result+
                        " attempts="+record.Attempts+" accepted="+record.Accepted+" rejected="+record.Rejected+
                        (attempt==null?"":" source="+attempt.Source+" rawNative="+MovementPoint(attempt.Raw)+" projected="+MovementPoint(attempt.Projected)+" finalTarget="+MovementPoint(attempt.Target))+
                        (accepted==null?"":" acceptedOrder="+(accepted.HasOrder?accepted.OrderType+"#"+accepted.OrderId:"none observed"))+
                        " head="+MovementHead(unit)+" distance="+MovementNumber(distance)+" arrivalThreshold="+MovementNumber(threshold)+
                        " cohesion="+MovementEnrollment(unit,cohesion));
                }
                if(command.Watched&&command.Owner)CohesionDebugNext.Remove(command.Owner);
            }
            catch(Exception e){MelonLogger.Warning("Movement summary diagnostics unavailable: "+e.Message);}
        }
        private static void TraceCohesionMember(CohesionMember member,string reason)
        {
            if(member==null||member.Group==null||(!CohesionDebugEnabled&&!member.Group.Watched))return;
            try
            {
                string flags=MovementNativeFlags(member.Unit);
                string state=reason+"|"+flags;
                if(member.DiagnosticState==state)return;
                member.DiagnosticState=state;
                MelonLogger.Msg("[Cohesion #"+member.Group.Id+" unit "+MovementUnitName(member.Unit)+"] state="+reason+
                    " head="+MovementHead(member.Unit)+" requestedCap="+MovementNumber(member.Cap)+" lastAppliedCap="+MovementNumber(member.AppliedCap)+
                    " capReason="+member.CapReason+" healthReady="+member.HealthReady+" suspended="+member.Suspended+flags);
            }
            catch(Exception e){MelonLogger.Warning("Cohesion member trace unavailable: "+e.Message);}
        }
        private static string MovementNativeFlags(Unit unit)
        {
            var agent=unit?unit.AIAgent:null;var path=agent?agent.AgentPathfinding:null;
            if(!path)return " no navigation";
            string traversal=unit is Soldier soldier?" jumping="+soldier.IsJumping+" climbing="+soldier.IsClimbing+" falling="+soldier.IsFalling:
                unit is CreatureDecapod creature?" jumping="+creature.IsJumping+" wallClimbing="+creature.IsWallClimbing+" submerged="+creature.IsSubmerged+" flying="+creature.IsInFly:"";
            return " short="+path.HasShortPath+" calculating="+path.IsCalculatingPath+" paused="+path.IsPathPaused+
                " engagement="+path.EngagementHold+" moveProblem="+agent.IsMoveProblem+" nativeStuck="+agent.IsStuck+
                " aim="+(agent.AgentAiming!=null&&agent.AgentAiming.AimTarget!=null)+traversal;
        }
        private static void SampleCohesionMembers(CohesionTravel group)
        {
            if(group==null||(!CohesionDebugEnabled&&!group.Watched)||group.Members.Count==0)return;
            try
            {
                foreach(var stale in movementSampleCursors.Keys.Where(item=>!CohesionGroups.Contains(item)).ToArray())movementSampleCursors.Remove(stale);
                movementSampleCursors.TryGetValue(group,out int cursor);
                int count=Math.Min(group.Members.Count,group.Watched?256:32);
                for(int index=0;index<count;index++)
                {
                    var member=group.Members[(cursor+index)%group.Members.Count];var unit=member.Unit;
                    var agent=unit?unit.AIAgent:null;var path=agent?agent.AgentPathfinding:null;
                    float distance=unit?Vector3.Distance(Flat(unit.transform.position),Flat(member.Target)):-1;
                    string native=(path?" pathGoal="+MovementPoint(path.AgentFinalInstructedDestination):"")+MovementNativeFlags(unit);
                    MelonLogger.Msg("[Cohesion #"+group.Id+" unit "+MovementUnitName(unit)+"] sample phase="+group.Phase+
                        " turn="+MovementNumber(group.TurnAngle)+" arrivalETA="+MovementNumber(group.ArrivalTime-Time.time)+
                        " arrivalStrength="+MovementNumber(group.Tuning.Strength)+" holdStrength="+MovementNumber(group.Tuning.HoldStrength)+
                        " minimumFraction="+MovementNumber(group.Tuning.MinimumSpeed)+
                        " head="+MovementHead(unit)+" ownedOrder="+member.OrderId+" mode="+member.Mode+
                        " target="+MovementPoint(member.Target)+" distance="+MovementNumber(distance)+" remaining="+MovementNumber(member.Remaining)+
                        " freeSpeed="+MovementNumber(member.FreeSpeed)+" requestedCap="+MovementNumber(member.Cap)+
                        " lastAppliedCap="+MovementNumber(member.AppliedCap)+" appliedAge="+(member.LastCapApplied<0?"never":MovementNumber(Time.time-member.LastCapApplied))+ 
                        " capReason="+member.CapReason+" routeReady="+member.RouteReady+" routeInitialized="+member.RouteInitialized+
                        " healthReady="+member.HealthReady+" suspended="+member.Suspended+
                        " freshPathAge="+(member.FreshPathAt<0?"never":MovementNumber(Time.time-member.FreshPathAt))+
                        " lost="+member.Lost+" arrived="+member.Arrived+" free="+member.Free+" lag="+member.Lagging+
                        " obstructed="+member.Obstructed+" stuck="+member.Stuck+" delay="+MovementNumber(member.DelaySpent)+
                        " velocity="+(unit?MovementNumber(Flat(unit.CurrentVelocity).magnitude):"missing")+native);
                }
                movementSampleCursors[group]=(cursor+count)%group.Members.Count;
            }
            catch(Exception e){MelonLogger.Warning("Cohesion member sample unavailable: "+e.Message);}
        }
        private static void ClearMovementDiagnostics()
        {activeMovementDiagnostics=null;movementSampleCursors.Clear();}
    }
}
