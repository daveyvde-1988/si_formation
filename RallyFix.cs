using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class Ticket
        {
            internal Unit Unit;
            internal Team Team;
            internal Vector3 Anchor,NativeAnchor;
            internal float Scale,NextSearch;
            internal int Attempt;
        }
        private sealed class Spot
        {
            internal Vector3 Point,GroundPoint;
            internal float Radius,Spacing,OriginalHeight,AssignedHeight;
            internal bool Air;
            internal Team Team;
        }
        private static readonly Dictionary<Unit,Ticket> Pending=new Dictionary<Unit,Ticket>();
        private static readonly Dictionary<Unit,Spot> Parked=new Dictionary<Unit,Spot>();
        private static Structure production;
        private static int exitDepth;
        private static Unit issuingUnit;
        private static bool rallyOrderAccepted;
        private static float nextRallyPrune;
        // Resource workers retain their native rally/auto-harvest pipeline, including Shrimp.
        // The unit type also covers production before an agent has finished initializing.
        private static bool RallyExcluded(Unit unit)=>unit&&((unit.ObjectInfo&&unit.ObjectInfo.UnitType==UnitType.Harvester)||
            unit.AIAgent is AIHarvesterAgent||unit.AIAgent is AIAlienHarvesterAgent);
        private static bool Eligible(Unit u)=>!RallyExcluded(u)&&FormationEligible(u)&&(!u.IsFlyingType||u.AIAgent is AIAirVehicleAgent)&&u.AIAgent.AgentPathfinding.PathfindingSeeker;
        private void InstallRally()
        {
            HarmonyInstance.Patch(AccessTools.Method(typeof(Structure),"ConstructNonPlaceableInstant"),prefix:new HarmonyMethod(typeof(Formations),nameof(ProductionBegin)),transpiler:new HarmonyMethod(typeof(Formations),nameof(ObserveProduced)),finalizer:new HarmonyMethod(typeof(Formations),nameof(ProductionEnd)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(ConstructionSite),"SpawnObject"),new HarmonyMethod(typeof(Formations),nameof(SiteBegin)),new HarmonyMethod(typeof(Formations),nameof(SiteProduced)),new HarmonyMethod(typeof(Formations),nameof(ObserveProduced)),new HarmonyMethod(typeof(Formations),nameof(ProductionEnd)),null);
            foreach(var type in new[]{typeof(Unit),typeof(Vehicle)})
                HarmonyInstance.Patch(AccessTools.Method(type,"RallyTo"),prefix:new HarmonyMethod(typeof(Formations),nameof(NativeRally)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(AIOrderProcessor),"IssueOrder"),postfix:new HarmonyMethod(typeof(Formations),nameof(OtherOrder)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(Vehicle),"ClearForceMove"),prefix:new HarmonyMethod(typeof(Formations),nameof(ExitBegin)),finalizer:new HarmonyMethod(typeof(Formations),nameof(ExitEnd)));
        }
        private static void ProductionBegin(Structure __instance,out Structure __state){__state=production;production=__instance;}
        private static void SiteBegin(ConstructionSite __instance,out Structure __state){__state=production;production=__instance.ParentStructure;}
        private static Exception ProductionEnd(Exception __exception,Structure __state){production=__state;return __exception;}
        private static IEnumerable<CodeInstruction> ObserveProduced(IEnumerable<CodeInstruction> source)
        {
            int count=0;
            foreach(var instruction in source)
            {
                if(instruction.Calls(AccessTools.Method(typeof(Unit),nameof(Unit.OnProduced))))
                {instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(Formations),nameof(ProducedCall));count++;}
                yield return instruction;
            }
            if(count!=1)throw new InvalidOperationException("Native production signature changed: expected one OnProduced call.");
        }
        private static void ProducedCall(Unit unit,ConstructionSite site){unit.OnProduced(site);Produced(unit,site);}
        private static void SiteProduced(ConstructionSite __instance)
        {
            // Native SpawnObject skips OnProduced entirely without a forced-exit transform.
            if(__instance.SpawnedGameObject is Unit unit&&!Pending.ContainsKey(unit))Produced(unit,__instance);
        }
        private static void ExitBegin(){exitDepth++;}
        private static Exception ExitEnd(Exception __exception){exitDepth--;return __exception;}
        private static void Produced(Unit __instance,ConstructionSite site)
        {
            if(!Active||!Game.GetIsServer()||!__instance||__instance.IsDestroyed)return;
            if(RallyExcluded(__instance)){ReleaseRally(__instance);return;}
            if(__instance.IsFlyingType&&!(__instance.AIAgent is AIAirVehicleAgent))return;
            var factory=site?site.ParentStructure:production;
            if(!factory||factory.Team!=__instance.Team)return;
            Vector3 nativeAnchor=factory.RallyPoint,anchor=nativeAnchor;
            if(anchor==Vector3.zero||factory.ProductionUnitMoveToTransform&&(anchor-factory.ProductionUnitMoveToTransform.position).sqrMagnitude<1)
            {
                Vector3 forward=factory.ProductionUnitMoveToTransform?factory.ProductionUnitMoveToTransform.position-factory.transform.position:factory.transform.forward;forward.y=0;
                if(forward.sqrMagnitude<1)forward=factory.transform.forward;
                anchor=factory.transform.position+forward.normalized*(75+factory.PhysicalRadius+__instance.PhysicalRadius);
            }
            var settings=ServerSettings;
            Pending[__instance]=new Ticket{Unit=__instance,Team=__instance.Team,Anchor=anchor,NativeAnchor=nativeAnchor,
                Scale=Multiplier(settings.Rally)*FactionScale(factory.Team,settings)};
        }
        // Production's native rally is postponed until a free destination is chosen;
        // factory forced exit stays untouched, including the vehicle's native exit path.
        private static bool NativeRally(Unit __instance)
        {
            if(RallyExcluded(__instance)){ReleaseRally(__instance);return true;}
            return !Active||issuingUnit==__instance||!Game.GetIsServer()||!Pending.ContainsKey(__instance);
        }
        private static void ReleaseRally(Unit unit)
        {
            Pending.Remove(unit);
            if(!Parked.TryGetValue(unit,out var spot))return;
            Parked.Remove(unit);
            if(spot.Air&&unit&&unit.AIAgent&&unit.AIAgent.AgentPathfinding)
            {
                var path=unit.AIAgent.AgentPathfinding;
                if(Mathf.Abs(path.FlyPreferredHeight-spot.AssignedHeight)<.01f)path.FlyPreferredHeight=spot.OriginalHeight;
            }
        }
        private static void ClearRallyState()
        {
            foreach(var unit in Parked.Keys.ToArray())ReleaseRally(unit);
            Pending.Clear();production=null;nextRallyPrune=0;
        }
        private static void OtherOrder(AIOrderProcessor __instance,OrderTarget __1,OrderIssueParams __2,bool __result)
        {
            if(!ready||!__result||!(__instance.Owner is Unit u))return;
            if(RallyExcluded(u)){ReleaseRally(u);return;}
            if(issuingUnit==u){rallyOrderAccepted=true;return;}
            if(!__2.CalledByAI){ReleaseRally(u);return;}
            if(production||exitDepth>0)return;
            if(u is Vehicle v&&v.HasForceMove)return;
            // Internal replans toward the same captured rally destination retain ownership.
            if(Parked.TryGetValue(u,out var spot)&&!__1.Object&&FlatDistanceSq(__1.Position,spot.GroundPoint)<4)return;
            ReleaseRally(u);
        }
        private static void TickRally()
        {
            if(Time.unscaledTime>=nextRallyPrune)
            {
                nextRallyPrune=Time.unscaledTime+1;
                foreach(var u in Parked.Keys.Where(u=>!Eligible(u)||u.Team!=Parked[u].Team||!Unit.Units.Contains(u)).ToArray())ReleaseRally(u);
            }
            if(Pending.Count==0)return;
            var budget=System.Diagnostics.Stopwatch.StartNew();
            int processed=0;
            foreach(var t in Pending.Values.ToArray())
            {
                var u=t.Unit;
                if(!u||u.IsDestroyed||RallyExcluded(u)||u.Team!=t.Team||!Unit.Units.Contains(u)){ReleaseRally(u);continue;}
                if(!Eligible(u))
                {
                    if(u.PlayerControlled||u.ControlledBy||u.InCompartment||(u.Driver&&u.Driver.ControlledBy))ReleaseRally(u);
                    continue; // agents can finish initialization/forced exits later; do not drop the ticket on a timer
                }
                if(u is Vehicle v&&v.HasForceMove)continue;
                if(Time.unscaledTime<t.NextSearch)continue;
                if(++processed>4)break;
                float radius=Radius(u),spacing=Mathf.Max(radius*2+.5f,(radius+2)*t.Scale);
                bool air=u.IsFlyingType&&u.AIAgent is AIAirVehicleAgent;
                float originalHeight=air?u.AIAgent.AgentPathfinding.FlyPreferredHeight:0,assignedHeight=originalHeight;
                bool found=false;Vector3 point=Vector3.zero;
                for(int i=0;i<24&&t.Attempt<2048;i++)
                {
                    if(budget.Elapsed.TotalMilliseconds>2)return;
                    int ordinal=t.Attempt++,n=air?ordinal/4:ordinal,layer=air?ordinal%4:0;
                    float angle=n*2.39996323f;
                    float distance=spacing*Mathf.Sqrt(n);
                    var desired=t.Anchor+new Vector3(Mathf.Cos(angle)*distance,0,Mathf.Sin(angle)*distance);
                    if(!Game.MainTerrain)break;
                    if(!TerrainProjection.TryProject(u,desired,out point))continue;
                    assignedHeight=air?originalHeight+layer*Mathf.Max(radius*2+1,spacing):0;
                    Vector3 actual=point+Vector3.up*assignedHeight;
                    if(Parked.Any(p=>p.Key!=u&&RallyConflict(actual,air,p.Value.Point,p.Value.Air,Mathf.Max(Mathf.Max(spacing,p.Value.Spacing),radius+p.Value.Radius+1))))continue;
                    if(Unit.Units.Any(other=>other&&other!=u&&!other.IsDestroyed&&!other.InCompartment&&RallyConflict(actual,air,other.transform.position,other.IsFlyingType,radius+Radius(other)+1)))continue;
                    if(!air&&Structure.Structures.Any(s=>s&&!s.IsDestroyed&&FlatDistanceSq(s.transform.position,point)<Mathf.Pow(radius+s.PhysicalRadius+1,2)))continue;
                    if(!air&&ConstructionSite.ConstructionSites.Any(s=>s&&FlatDistanceSq(s.transform.position,point)<Mathf.Pow(radius+s.PhysicalRadius+1,2)))continue;
                    found=true;break;
                }
                // Never send exhausted searches to the common marker. Keep a bounded,
                // resumable search alive and retry if the surrounding area later clears.
                if(!found)
                {if(t.Attempt>=2048){t.Attempt=0;t.NextSearch=Time.unscaledTime+2;}continue;}
                var reservation=new Spot{GroundPoint=point,Point=point+Vector3.up*assignedHeight,Radius=radius,Spacing=spacing,
                    Air=air,OriginalHeight=originalHeight,AssignedHeight=assignedHeight,Team=u.Team};
                Parked[u]=reservation; // reserve BEFORE dispatch: other facilities see units still en route
                if(air)u.AIAgent.AgentPathfinding.FlyPreferredHeight=assignedHeight;
                issuingUnit=u;rallyOrderAccepted=false;
                try
                {
                    u.RallyTo(point);
                    if(rallyOrderAccepted)Pending.Remove(u);
                    else {ReleaseRally(u);Pending[u]=t;t.NextSearch=Time.unscaledTime+.5f;}
                }
                catch(Exception e)
                {ReleaseRally(u);Pending[u]=t;t.NextSearch=Time.unscaledTime+2;MelonLoader.MelonLogger.Warning("Rally placement retry: "+e.Message);}
                finally{issuingUnit=null;}
            }
        }
        private static bool RallyConflict(Vector3 a,bool airA,Vector3 b,bool airB,float clearance)
        {
            if(airA||airB)return (a-b).sqrMagnitude<clearance*clearance;
            return FlatDistanceSq(a,b)<clearance*clearance;
        }
    }
}
