using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Silica;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class ReverseCommandScope
        {
            internal MoveState Previous;
            internal ReverseCommandScope Parent;
            internal CohesionDispatch Context;
            internal MovementCommandDiagnostics Diagnostics;
        }
        private sealed class ReverseOrderCapture
        {
            internal Unit Unit;
            internal CohesionDispatch Context;
            internal MovementAttempt Attempt;
        }
        private static ReverseCommandScope activeReverseCommandScope;

        private void InstallReverseCommandRouting()
        {
            // Verified against the installed dedicated-server SilicaCore assembly:
            // generic command-card batches issue directly, without PerformMoveAttack.
            HarmonyInstance.Patch(AccessTools.Method(typeof(OrderTargetingController),"IssueToObjects"),
                prefix:new HarmonyMethod(typeof(ReverseCommandBatchObserver),nameof(ReverseCommandBatchObserver.Before)),
                finalizer:new HarmonyMethod(typeof(ReverseCommandBatchObserver),nameof(ReverseCommandBatchObserver.After)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(AIOrderProcessor),"IssueOrder"),
                prefix:new HarmonyMethod(typeof(ReverseCommandOrderObserver),nameof(ReverseCommandOrderObserver.Before)),
                postfix:new HarmonyMethod(typeof(ReverseCommandOrderObserver),nameof(ReverseCommandOrderObserver.After)));
        }

        // Each prefix/state pair has its own declaring type. The defense and FPS
        // observers patch these same native methods with unrelated Harmony states.
        private static class ReverseCommandBatchObserver
        {
            internal static void Before(OrderDefinition definition,IReadOnlyList<BaseGameObject> objects,
                IReadOnlyList<Vector3> points,Target clickObject,AgentMoveSpeed moveSpeed,bool queueOrder,
                out ReverseCommandScope __state)
            {
                __state=null;
                if(!Active||!Game.GetIsServer()||!(definition is ReverseOrderDefinition)||objects==null||
                    points==null||points.Count!=1||definition.TargetPointCount!=1||clickObject||
                    activeCohesion!=null&&activeCohesion.Reverse)return;
                var first=objects.FirstOrDefault(obj=>obj&&!obj.IsDestroyed);
                if(!first)return;
                var commander=Commander(first.Team);
                if(!commander)return;
                var sender=NetworkLayer.GetRequestSender();
                if(sender?sender!=commander:Player.CurrentPlayer!=commander)return;

                var scope=new ReverseCommandScope{Parent=activeReverseCommandScope,
                    Previous=new MoveState{Independent=activeIndependent,Layout=activeLayout,
                        Cohesion=activeCohesion,Diagnostics=activeMovementDiagnostics}};
                __state=scope;activeReverseCommandScope=scope;
                activeIndependent=false;activeLayout=null;activeCohesion=null;activeMovementDiagnostics=null;
                try
                {
                    scope.Diagnostics=activeMovementDiagnostics=BeginMovementDiagnostics(objects,commander,points[0],
                        Vector3.zero,moveSpeed,false,queueOrder,false,true,scope.Previous.Diagnostics);
                    var context=PrepareReverseFormation(objects,commander,points[0],queueOrder,definition,moveSpeed);
                    if(context!=null&&context.Skip==null)
                    {scope.Context=activeCohesion=context;activeIndependent=!context.Unpaced;}
                    else
                    {
                        activeLayout=null;
                        if(context!=null)Reply(commander,"Reverse formation uses native movement: "+context.Skip+".");
                    }
                }
                catch(Exception e)
                {
                    scope.Context=null;activeLayout=null;activeCohesion=null;activeIndependent=false;
                    MelonLogger.Warning("Reverse command preparation skipped: "+e.Message);
                }
            }

            internal static Exception After(Exception __exception,ReverseCommandScope __state)
            {
                if(__state==null)return __exception;
                try
                {
                    if(__exception==null&&__state.Context!=null&&ReferenceEquals(activeCohesion,__state.Context))
                        FinishCohesion(__state.Context);
                }
                catch(Exception e){MelonLogger.Warning("Reverse command coordination released: "+e);}
                finally
                {
                    FinishMovementDiagnostics(__state.Diagnostics,__state.Context,__exception);
                    activeIndependent=__state.Previous.Independent;activeLayout=__state.Previous.Layout;
                    activeCohesion=__state.Previous.Cohesion;activeMovementDiagnostics=__state.Previous.Diagnostics;
                    activeReverseCommandScope=__state.Parent;
                }
                return __exception;
            }
        }

        private static class ReverseCommandOrderObserver
        {
            internal static void Before(AIOrderProcessor __instance,OrderDefinition __0,ref OrderTarget __1,
                OrderIssueParams __2,out ReverseOrderCapture __state)
            {
                __state=null;
                var context=activeCohesion;
                if(!Active||!Game.GetIsServer()||!(__0 is ReverseOrderDefinition)||__2.CalledByAI||
                    context==null||!context.Reverse||context.Skip!=null||activeLayout==null||
                    !(__instance.Owner is Unit unit)||!context.Candidates.Contains(unit)||unit.Team!=context.Team||
                    !CohesionEligible(unit)||!activeLayout.Points.TryGetValue(unit,out var point))return;
                if(context.ReverseInfantryMoves!=null&&context.ReverseInfantryMoves.ContainsKey(unit))return;
                // Native received-order callbacks can issue another command while
                // this batch is active. Do not capture a different destination.
                if(FlatDistanceSq(__1.Position,context.Anchor)>1&&FlatDistanceSq(__1.Position,point)>1)return;
                var capture=new ReverseOrderCapture{Unit=unit,Context=context};
                if(activeReverseCommandScope!=null&&ReferenceEquals(activeReverseCommandScope.Context,context))
                    capture.Attempt=BeginMovementAttempt(unit,__1.Position,point,"native reverse command");
                __1=new OrderTarget(point,__1.Object,__1.Payload);
                __state=capture;
            }

            internal static void After(AIOrderProcessor __instance,bool __result,ReverseOrderCapture __state)
            {
                if(__state==null)return;
                try
                {
                    EndMovementAttempt(__state.Attempt,__result);
                    if(!__result||!ReferenceEquals(activeCohesion,__state.Context)||__instance.Orders.Count==0)return;
                    if(__instance.Orders[__instance.Orders.Count-1] is AIReverseOrder accepted)
                        CohesionIssuedOrder(__state.Unit,accepted.WorldPosition,accepted.MoveSpeed,true);
                }
                catch(Exception e){MelonLogger.Warning("Reverse order capture skipped: "+e.Message);}
            }
        }
    }
}
