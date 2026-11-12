using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Silica.AI;
using SilicaAdminMod;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private enum CohesionPhase { Waiting, Turning, Holding, Arrival, Released, Complete }
        private sealed class CohesionLayout
        {
            internal Team Team;
            internal float Time;
            internal Vector3 Forward;
            internal bool Invalidated,DragOrigin;
            internal readonly Dictionary<Unit,Vector3> Offsets=new Dictionary<Unit,Vector3>();
            internal readonly Dictionary<Unit,CohesionMember> Orders=new Dictionary<Unit,CohesionMember>();
        }
        private sealed class CohesionDispatch
        {
            internal Player Owner;
            internal Team Team;
            internal Vector3 Anchor,Origin,Heading;
            internal bool Drag,Queued,Turned,Reform,DestinationLocal,RememberedDrag,Unpaced;
            internal float TurnAngle;
            internal int IgnoredGround;
            internal Dictionary<Unit,Vector3> LocalPoints;
            internal string Skip;
            internal readonly HashSet<Unit> Candidates=new HashSet<Unit>();
            internal readonly Dictionary<Unit,CohesionMember> Issued=new Dictionary<Unit,CohesionMember>();
        }
        private sealed class CohesionMember
        {
            internal Unit Unit;
            internal AIOrderProcessor Processor;
            internal uint OrderId;
            internal AgentMoveSpeed Mode;
            internal CohesionTravel Group;
            internal Vector3 Target,Offset,LastPosition;
            internal float Direct,Remaining,InitialRemaining,FreeSpeed,Cap=-1,LastProgress,LastRemaining,DelaySpent,FreeArrival,StartedAt=-1,FreshPathAt=-1;
            internal float AppliedCap=-1,LastCapApplied=-1;
            internal string CapReason="not evaluated",DiagnosticState;
            internal bool Started,Arrived,Lost,Free,RouteReady,RouteInitialized,Obstructed,Stuck,Lagging,Suspended,HealthReady;
        }
        private struct CohesionOrderStamp { internal bool Captured,HadOrder; internal uint HeadId; internal CohesionLayout Layout; }
        // Harmony keys __state by the patch method's declaring type. This pair
        // must have its own type: the FPS IssueOrder pair already uses a bool
        // __state on Formations. Sharing that slot generates invalid Mono IL.
        private static class CohesionOrderObserver
        {
            internal static void Before(AIOrderProcessor __instance,out CohesionOrderStamp __state)
            {
                __state=new CohesionOrderStamp{Captured=true,HadOrder=__instance.Orders.Count>0,HeadId=__instance.Orders.Count>0?__instance.Orders[0].Id:0};
                if(__instance.Owner is Unit unit)CohesionLayouts.TryGetValue(unit,out __state.Layout);
            }
            internal static void After(AIOrderProcessor __instance,OrderIssueParams __2,bool __result,CohesionOrderStamp __state)
            {ObserveCohesionOrder(__instance,__result,__2.CalledByAI,__state);}
        }
        private sealed class CohesionTravel
        {
            internal int Id;
            internal Player Owner;
            internal Team Team;
            internal Vector3 Anchor,Origin,Forward;
            internal bool Drag,Queued,Watched,Turned,Reform,ArrivalRebased;
            internal CohesionTuning Tuning;
            internal CohesionLayout Layout;
            internal CohesionPhase Phase=CohesionPhase.Waiting;
            internal float Created,Started=-1,FirstActive=-1,Ended,InitialRadius,ArrivalTime,LastTick,NextTick,NextDebug,NextHold,TurnUntil,HoldBadSince=-1,SpreadSince=-1;
            internal float Spread,LostPercent,UnhealthyPercent,TurnAngle;
            internal string Reason="waiting for move segment";
            internal readonly List<CohesionMember> Members=new List<CohesionMember>();
            internal readonly Queue<string> History=new Queue<string>();
        }
        private static CohesionDispatch activeCohesion;
        private static readonly List<CohesionTravel> CohesionGroups=new List<CohesionTravel>();
        private static readonly Dictionary<Unit,CohesionMember> CohesionCaps=new Dictionary<Unit,CohesionMember>();
        private static readonly Dictionary<Unit,CohesionLayout> CohesionLayouts=new Dictionary<Unit,CohesionLayout>();
        private static readonly HashSet<Player> CohesionDebugNext=new HashSet<Player>();
        private static bool CohesionDebugEnabled;
        private static int nextCohesionId=1;
        private static float nextCohesionTick;
        private const int CohesionGroupLimit=64;

        private void InstallCohesion()
        {
            HarmonyInstance.Patch(AccessTools.PropertyGetter(typeof(Unit),nameof(Unit.MoveSpeedLimit)),
                postfix:new HarmonyMethod(typeof(Formations),nameof(CohesionSpeedCap)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(StrategyMode),"IssueFormationOrder"),
                postfix:new HarmonyMethod(typeof(Formations),nameof(CohesionIssuedOrder)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(AIOrderProcessor),"IssueOrder"),
                prefix:new HarmonyMethod(typeof(CohesionOrderObserver),nameof(CohesionOrderObserver.Before)),
                postfix:new HarmonyMethod(typeof(CohesionOrderObserver),nameof(CohesionOrderObserver.After)));
            AdminMethods.RegisterAdminCommand("cohesiondebug",CohesionDebugCommand,Power.Generic,
                "Group trace: on|off|status|groups|next|<group id>.");
        }
        private static Vector3 Flat(Vector3 point){point.y=0;return point;}
        private static bool IsAlienTeam(Team team)=>team&&(string.Equals(team.name,"Team_Alien",StringComparison.OrdinalIgnoreCase)||
            string.Equals(team.TeamShortName,"Alien",StringComparison.OrdinalIgnoreCase));
        private static float CohesionControl(CohesionTravel group,string key)=>CohesionValueFor(group.Team,key);
        private static Vector3 FormationHeading(IEnumerable<Unit> units,Vector3 fallback)
        {
            Vector3 sum=Vector3.zero;int count=0;
            foreach(var unit in units){sum+=Flat(unit.transform.forward).normalized;count++;}
            if(count>0&&sum.magnitude/count>.2f)return sum.normalized;
            return fallback.sqrMagnitude>.01f?fallback.normalized:Vector3.forward;
        }
        private static bool CohesionGroundMember(Unit unit)=>FormationEligible(unit)&&!unit.IsFlyingType&&!(unit is VehicleAir);
        private static bool CohesionTraversalPaused(Unit unit)
        {
            if(unit is Vehicle vehicle&&vehicle.HasForceMove)return true;
            if(unit is CreatureDecapod creature&&(creature.IsSubmerged||creature.IsSubmergeTransition||creature.IsInFly||creature.IsWallClimbing||creature.IsJumping))return true;
            return unit is Soldier soldier&&(soldier.IsJumping||soldier.IsClimbing||soldier.IsFalling);
        }
        private static bool CohesionEligible(Unit unit)=>CohesionGroundMember(unit)&&!CohesionTraversalPaused(unit);
        private static float CohesionModeSpeed(Unit unit,AgentMoveSpeed mode)
        {
            float speed=mode==AgentMoveSpeed.Fast?unit.TopSpeed:unit.NormalSpeed;
            if(unit is Soldier soldier)
            {
                speed=mode==AgentMoveSpeed.Stealth?soldier.WalkSpeed:mode==AgentMoveSpeed.Fast&&soldier.CanSprint?soldier.SprintSpeed:soldier.RunSpeed;
                if(soldier.IsCrouching)speed*=soldier.CrouchSpeedModifier;
            }
            else if(unit is CreatureDecapod creature)
            {
                speed=mode==AgentMoveSpeed.Stealth?creature.CreepSpeed:creature.MoveSpeed;
                if(creature.IsSlowed)speed*=creature.HitSlowSpeedPct;
            }
            // This field belongs to native orders/other mods. Never write it or derive
            // free speed from our own throttled velocity/getter.
            if(unit.MoveSpeedScale<1)speed=Mathf.Min(speed,unit.TopSpeed*Mathf.Max(0,unit.MoveSpeedScale));
            return Mathf.Max(.05f,speed);
        }
        private static bool ResetChangedCohesionLayouts(List<Unit> selection)
        {
            var touched=new HashSet<CohesionLayout>();
            foreach(var unit in selection)if(CohesionLayouts.TryGetValue(unit,out var layout))touched.Add(layout);
            bool reform=false;
            foreach(var layout in touched)
            {
                bool complete=layout.DragOrigin&&!layout.Invalidated&&layout.Offsets.Count==selection.Count&&selection.All(unit=>
                    CohesionLayouts.TryGetValue(unit,out var current)&&ReferenceEquals(current,layout));
                if(complete)continue;
                reform=true;
                InvalidateCohesionLayout(layout);
            }
            return reform;
        }
        private static void InvalidateCohesionLayout(CohesionLayout layout)
        {
            if(layout==null||layout.Invalidated)return;
            layout.Invalidated=true;layout.Time=Time.time;layout.Forward=Vector3.zero;
            // Leave a bounded marker on survivors, never a shape containing holes.
            // Existing move targets remain native; only future reuse is discarded.
            layout.Offsets.Clear();layout.Orders.Clear();
        }
        private static CohesionDispatch PrepareCohesion(IReadOnlyList<BaseGameObject> objects,Player owner,Vector3 position,
            Vector3 lineEnd,bool drag,bool queued)
        {
            if(!owner||!CohesionEnabledFor(owner.Team))return null;
            var selected=objects.OfType<Unit>().Where(u=>u&&u.Team==owner.Team).Distinct().ToList();
            // Aircraft have their own destination band and never set a ground
            // group's footprint, speed, casualty denominator or admission status.
            var ground=selected.Where(u=>!IsFormationAir(u)).ToList();
            var units=ground;
            var context=new CohesionDispatch{Owner=owner,Team=owner.Team,Anchor=drag?(position+lineEnd)*.5f:position,Drag=drag,Queued=queued};
            // Detect splits before even a single-unit/unsupported selection exits.
            // This changes cached identity only, never an unselected unit's order.
            context.Reform=ResetChangedCohesionLayouts(units);
            if(units.Count<2){context.Skip="fewer than two units";return context;}
            int maximum=(int)CohesionValueFor(owner.Team,"MaxUnits");
            Vector3 selectionCentre=units.Aggregate(Vector3.zero,(sum,u)=>sum+u.transform.position)/units.Count;
            float initialSpread=units.Max(u=>Mathf.Max(0,Vector3.Distance(Flat(u.transform.position),Flat(selectionCentre))-Radius(u)));
            float bodyAllowance=units.Max(Radius)*Mathf.Sqrt(units.Count);
            bool dispersed=initialSpread>CohesionValueFor(owner.Team,"InitialSpread")+bodyAllowance;
            if(dispersed||units.Count>maximum)
            {
                float radius=CohesionValueFor(owner.Team,"DestinationRadius");
                if(radius<=0){context.Skip=dispersed?"initial selection is too dispersed":"selection exceeds unit limit";return context;}
                units=units.Where(u=>CohesionGroundMember(u)&&FlatDistanceSq(u.transform.position,context.Anchor)<=radius*radius)
                    .OrderBy(u=>FlatDistanceSq(u.transform.position,context.Anchor)).ThenBy(u=>u.GetInstanceID()).Take(maximum).ToList();
                context.DestinationLocal=true;context.IgnoredGround=ground.Count-units.Count;context.Reform=true;
                if(units.Count<2){context.Skip="fewer than two eligible units inside destination arrival radius";return context;}
            }
            else if(units.Any(u=>!CohesionGroundMember(u))){context.Skip="selection includes unsupported or player-driven movement";return context;}
            context.Origin=units.Aggregate(Vector3.zero,(sum,u)=>sum+u.transform.position)/units.Count;
            if(queued)
            {
                // Plan this segment from its predecessor, rather than from an earlier
                // segment's starting position. Activation is still checked by order ID.
                Vector3 planned=Vector3.zero;
                foreach(var unit in units)
                {
                    var orders=unit.OrderAgent.Orders;
                    if(orders.Count>0&&!(orders[orders.Count-1] is AIMoveOrder))
                    {context.Skip="queued predecessor is not an ordinary move";return context;}
                    planned+=orders.Count>0?orders[orders.Count-1].GetPosition():unit.transform.position;
                }
                context.Origin=planned/units.Count;
            }
            float smallest=units.Min(Radius);
            Vector3 travel=Flat(context.Anchor-context.Origin);
            context.Unpaced=travel.magnitude>CohesionValueFor(context.Team,"MaximumRange");
            context.Heading=drag?Vector3.Cross(Flat(lineEnd-position).normalized,Vector3.up):FormationHeading(units,travel);
            if(context.Heading.sqrMagnitude<.01f)context.Heading=FormationHeading(units,travel);
            if(drag)
            {
                CohesionLayouts.TryGetValue(units[0],out var preceding);
                Vector3 oldHeading=preceding!=null&&!preceding.Invalidated&&preceding.DragOrigin?preceding.Forward:FormationHeading(units,travel);
                context.TurnAngle=Mathf.Abs(Vector3.SignedAngle(oldHeading,context.Heading,Vector3.up));
                context.Turned=context.TurnAngle>5;
            }
            foreach(var unit in units)context.Candidates.Add(unit);
            if(context.DestinationLocal)
            {
                if(!BuildDestinationArrivalLayout(context,units,ground,selected.Where(u=>IsFormationAir(u)&&FormationEligible(u)).ToList(),position,lineEnd))
                {context.Skip="destination arrival layout has insufficient valid slots";context.Candidates.Clear();}
            }
            else if(!drag&&activeLayout!=null)
            {
                // Only a deliberately dragged, intact layout may be translated.
                // Ordinary clicks and rally parking never snapshot current gaps.
                CohesionLayouts.TryGetValue(units[0],out var remembered);
                bool reuse=!context.Unpaced&&!context.Reform&&remembered!=null&&remembered.DragOrigin&&!remembered.Invalidated&&remembered.Team==context.Team&&
                    (LayoutMovePending(remembered)||Time.time-remembered.Time<=CohesionValueFor(context.Team,"Retention"))&&
                    remembered.Offsets.Count==units.Count&&units.All(u=>CohesionLayouts.TryGetValue(u,out var found)&&ReferenceEquals(found,remembered));
                if(!reuse)
                {
                    context.Reform=true;
                    context.Heading=travel.sqrMagnitude<.001f?Vector3.forward:travel.normalized;
                    return context;
                }
                context.RememberedDrag=true;
                Vector3 oldHeading=remembered.Forward;
                if(oldHeading.sqrMagnitude<.01f)oldHeading=FormationHeading(units,travel);
                context.Heading=oldHeading;
                if(travel.sqrMagnitude>=1)context.Heading=travel.normalized;
                float yaw=Vector3.SignedAngle(oldHeading,context.Heading,Vector3.up);
                Quaternion turn=Quaternion.AngleAxis(yaw,Vector3.up);
                context.TurnAngle=Mathf.Abs(yaw);
                context.Turned=Mathf.Abs(yaw)>5;
                var points=new Dictionary<Unit,Vector3>();
                foreach(var unit in units)
                {
                    Vector3 offset=remembered.Offsets[unit];
                    if(!TerrainProjection.TryProject(unit,context.Anchor+turn*Flat(offset),out var target))
                    {context.Skip="preserved layout could not be projected";context.Candidates.Clear();return context;}
                    points[unit]=target;
                }
                // Terrain projection can snap sideways. Retain physical clearance.
                for(int i=0;i<units.Count;i++)for(int j=i+1;j<units.Count;j++)
                {
                    float clearance=Radius(units[i])+Radius(units[j]);
                    if(FlatDistanceSq(points[units[i]],points[units[j]])<clearance*clearance)
                    {context.Skip="preserved layout loses physical clearance";context.Candidates.Clear();return context;}
                }
                AddAirFormationSlots(selected.Where(u=>IsFormationAir(u)&&FormationEligible(u)).ToList(),points,context.Anchor,
                    context.Heading,activeLayout.Settings,activeLayout.Scale);
                activeLayout.Points=points;
                activeLayout.Pitch=Mathf.Max(1,smallest*2);
            }
            return context;
        }
        private static void CohesionIssuedOrder(BaseGameObject obj,Vector3 worldPosition,AgentMoveSpeed moveSpeed,bool __result)
        {
            var context=activeCohesion;
            if(!__result||context==null||context.Skip!=null||!(obj is Unit unit)||!context.Candidates.Contains(unit))return;
            var processor=unit.OrderAgent;
            if(!processor||processor.Orders.Count==0)return;
            // A successful resolved order may be a utility action rather than Move.
            if(!(processor.Orders[processor.Orders.Count-1] is AIMoveOrder order)||FlatDistanceSq(order.WorldPosition,worldPosition)>1)return;
            context.Issued[unit]=new CohesionMember{Unit=unit,Processor=processor,OrderId=order.Id,Target=order.WorldPosition,
                Mode=order.MoveSpeed,LastPosition=unit.transform.position};
        }
        private static void FinishCohesion(CohesionDispatch context)
        {
            if(context==null)return;
            float now=Time.time;
            PruneCohesion(now);
            var travel=new CohesionTravel{Id=nextCohesionId++,Owner=context.Owner,Team=context.Team,Anchor=context.Anchor,
                Origin=context.Origin,Drag=context.Drag,Queued=context.Queued,Turned=context.Turned,Reform=context.Reform,Created=now,LastTick=now,
                TurnAngle=context.Turned?context.TurnAngle:0,
                Forward=Flat(context.Anchor-context.Origin).normalized,Tuning=CaptureCohesionTuning(Vector3.Distance(Flat(context.Anchor),Flat(context.Origin)),context.Team)};
            float rotation=Mathf.Clamp01((travel.TurnAngle-15)/75);
            travel.Tuning.Strength*=1-.85f*rotation;
            travel.Tuning.HoldStrength*=1-rotation;
            travel.Tuning.MinimumSpeed=Mathf.Max(travel.Tuning.MinimumSpeed,.8f*rotation);
            if(travel.Forward.sqrMagnitude<.01f)travel.Forward=Vector3.forward;
            if(nextCohesionId==int.MaxValue)nextCohesionId=1;
            travel.Watched=CohesionDebugNext.Remove(context.Owner);
            string skip=context.Skip;
            if(CohesionGroups.Count>=CohesionGroupLimit)
            {
                var old=CohesionGroups.FirstOrDefault(g=>g.Phase==CohesionPhase.Released||g.Phase==CohesionPhase.Complete);
                if(old!=null)CohesionGroups.Remove(old);
                else{if(CohesionDebugEnabled||travel.Watched)MelonLogger.Msg("[Cohesion] skipped: active group capacity reached.");return;}
            }
            CohesionGroups.Add(travel);
            foreach(var member in context.Issued.Values)
            {member.Group=travel;member.Offset=Flat(member.Target-travel.Anchor);travel.Members.Add(member);}
            TraceCohesion(travel,"created: "+travel.Members.Count+" accepted moves, "+travel.Tuning.Range+", "+
                Vector3.Distance(Flat(travel.Anchor),Flat(travel.Origin)).ToString("0",CultureInfo.InvariantCulture)+"m"+(travel.Queued?", queued":"")+
                (travel.Drag?", drag reform":"")+(travel.Reform&&!context.DestinationLocal?", membership changed: fresh formation":"")+
                (travel.Turned?", turn "+travel.TurnAngle.ToString("0",CultureInfo.InvariantCulture)+"deg, rotation relief "+rotation.ToString("0.00",CultureInfo.InvariantCulture):"")+
                (context.DestinationLocal?", destination-radius cohort, "+context.IgnoredGround+" ground units independent":""));
            if(skip!=null||travel.Members.Count<2){ReleaseCohesion(travel,skip??"fewer than two accepted moves",false);return;}
            Vector3 centre=travel.Members.Aggregate(Vector3.zero,(sum,m)=>sum+Flat(m.Unit.transform.position))/travel.Members.Count;
            travel.InitialRadius=travel.Members.Max(m=>Vector3.Distance(Flat(m.Unit.transform.position),centre));
            if(!context.DestinationLocal&&(context.Drag||context.RememberedDrag)&&travel.Members.Count==context.Candidates.Count&&
                travel.Members.All(member=>FormationEligible(member.Unit)&&member.Unit.Team==travel.Team))
            {
                var layout=new CohesionLayout{Team=travel.Team,Time=now,Forward=context.Heading,DragOrigin=true};
                travel.Layout=layout;
                foreach(var member in travel.Members){layout.Offsets[member.Unit]=member.Offset;layout.Orders[member.Unit]=member;}
                foreach(var member in travel.Members)CohesionLayouts[member.Unit]=layout;
            }
            else if(context.RememberedDrag)travel.Reform=true;
            if(travel.Watched&&travel.Owner)Reply(travel.Owner,"Watching cohesion group #"+travel.Id+"; use /cohesiondebug "+travel.Id+" for its history.");
            if(Vector3.Distance(Flat(context.Anchor),Flat(context.Origin))>CohesionValueFor(context.Team,"MaximumRange"))
            {ReleaseCohesion(travel,"move exceeds maximum cohesion range",false);return;}
            // Seed caps immediately: a short Fast move can finish before the next
            // ordinary controller tick. Queued orders still wait for their IDs.
            try{TickCohesionGroup(travel,now);travel.NextTick=now+CohesionControl(travel,"UpdateInterval");}
            catch(Exception e){ReleaseCohesion(travel,"initial coordination failed",false);MelonLogger.Warning("Cohesion #"+travel.Id+": "+e);}
        }
        private static bool CurrentCohesionOrder(CohesionMember member)
        {
            var unit=member.Unit;var processor=member.Processor;
            return unit&&processor&&unit.OrderAgent==processor&&processor.Orders.Count>0&&processor.Orders[0] is AIMoveOrder order&&
                order.Id==member.OrderId&&order.MoveSpeed==member.Mode&&FlatDistanceSq(order.WorldPosition,member.Target)<1;
        }
        private static bool PendingCohesionOrder(CohesionMember member)
        {
            if(!member.Unit||!member.Processor||member.Unit.OrderAgent!=member.Processor)return false;
            foreach(var entry in member.Processor.Orders)
                if(entry is AIMoveOrder move&&move.Id==member.OrderId&&FlatDistanceSq(move.WorldPosition,member.Target)<1)return true;
            return false;
        }
        private static void ClearCohesionCap(CohesionMember member)
        {
            if(!ReferenceEquals(member.Unit,null)&&CohesionCaps.TryGetValue(member.Unit,out var owner)&&ReferenceEquals(owner,member))CohesionCaps.Remove(member.Unit);
            member.Cap=-1;member.AppliedCap=-1;
        }
        private static void CohesionSpeedCap(Unit __instance,ref float __result)
        {
            if(!Active||!Game.GetIsServer()||!CohesionCaps.TryGetValue(__instance,out var member)||member.Cap<0||!CohesionEnabledFor(member.Group.Team))return;
            member.AppliedCap=-1;
            if(member.Lost||member.Arrived||member.Free||__instance.Team!=member.Group.Team||!CohesionGroundMember(__instance)||!CurrentCohesionOrder(member))
            {member.CapReason="order or membership changed";return;}
            if(CohesionTraversalPaused(__instance)){member.CapReason="temporary traversal";return;}
            var agent=__instance.AIAgent;var path=agent.AgentPathfinding;
            if(!CohesionPathReady(member)){member.CapReason="new path pending";return;}
            if(path.IsPathPaused||path.EngagementHold||agent.IsMoveProblem||agent.IsStuck)
            {member.CapReason="native movement hold";return;}
            if(agent.AgentAiming!=null&&agent.AgentAiming.AimTarget!=null){member.CapReason="combat aiming";return;}
            __result=__result>=0?Mathf.Min(__result,member.Cap):member.Cap;
            member.AppliedCap=__result;member.LastCapApplied=Time.time;member.CapReason="applied";
        }
        private static void ObserveCohesionOrder(AIOrderProcessor processor,bool accepted,bool calledByAI,CohesionOrderStamp stamp)
        {
            if(!stamp.Captured||!accepted||!(processor.Owner is Unit unit))return;
            bool replaced=!stamp.HadOrder||processor.Orders.Count==0||processor.Orders[0].Id!=stamp.HeadId;
            // Layout intention outlives transient native AI replans and speed
            // ownership. A real replacement invalidates only the snapshot it saw.
            if(replaced&&!calledByAI&&CohesionLayouts.TryGetValue(unit,out var current)&&ReferenceEquals(current,stamp.Layout))
                InvalidateCohesionLayout(current);
            if(!CohesionCaps.TryGetValue(unit,out var member)||CurrentCohesionOrder(member))return;
            // A queued append leaves the head ID intact and therefore retains ownership.
            member.Lost=true;ClearCohesionCap(member);
            member.CapReason="replacement order";TraceCohesionMember(member,"released: replacement order");
            TraceCohesion(member.Group,"member released by replacement order");
        }
        private static bool CohesionPathReady(CohesionMember member)
        {
            if(!CurrentCohesionOrder(member))return false;
            var path=member.Unit.AIAgent.AgentPathfinding;
            // A new head order can coexist with the previous move's native path.
            // Its instructed destination changes only when queued FindPath runs.
            return !path.IsCalculatingPath&&path.HasShortPath&&FlatDistanceSq(path.AgentFinalInstructedDestination,member.Target)<1;
        }
        private static float CohesionRoute(CohesionMember member,out bool ready)
        {
            var path=member.Unit.AIAgent.AgentPathfinding;
            ready=CohesionPathReady(member);
            if(!ready)return member.Direct;
            float remaining=path.AgentRemainingPathLength;
            // The native getter omits long segments at waypoint index zero.
            if(path.HasLongPath&&path.CurrentLongRangeWaypointIndex==0)
            {
                int count=Math.Min(path.CurrentLongRangePathLength,path.LongRangePathPoints.Count);
                if(count>512){ready=false;return member.Direct;}
                for(int i=0;i<count-1;i++)remaining+=Vector3.Distance(path.LongRangePathPoints[i],path.LongRangePathPoints[i+1]);
            }
            return float.IsNaN(remaining)||float.IsInfinity(remaining)?member.Direct:Mathf.Max(member.Direct,remaining);
        }
        private static void TickCohesion()
        {
            if(!CohesionEnabled&&!AlienCohesionEnabled){if(CohesionCaps.Count>0)ClearCohesionState("switch off");return;}
            float now=Time.time;
            if(now<nextCohesionTick||Game.IsLoading)return;
            nextCohesionTick=now+CohesionTickInterval;
            PruneCohesion(now);
            foreach(var group in CohesionGroups.ToArray())
            {
                if(group.Phase==CohesionPhase.Released||group.Phase==CohesionPhase.Complete)continue;
                if(!CohesionEnabledFor(group.Team)){ReleaseCohesion(group,"faction cohesion switched off",false);continue;}
                if(now<group.NextTick)continue;
                group.NextTick=now+CohesionControl(group,"UpdateInterval");
                try{TickCohesionGroup(group,now);}
                catch(Exception e){ReleaseCohesion(group,"controller error; released",false);MelonLogger.Warning("Cohesion #"+group.Id+": "+e);}
            }
        }
        private static void TickCohesionGroup(CohesionTravel group,float now)
        {
            float dt=Mathf.Clamp(now-group.LastTick,0,2);group.LastTick=now;
            if(group.Layout!=null)group.Layout.Time=now;
            if(!group.Owner||!group.Owner.IsCommander||group.Owner.Team!=group.Team)
            {ReleaseCohesion(group,"commander/team changed",false);return;}
            if(now-group.Created>CohesionControl(group,"MaximumDuration")){ReleaseCohesion(group,"maximum move duration",false);return;}
            var active=new List<CohesionMember>();int pending=0,lost=0,arrived=0;
            foreach(var member in group.Members)
            {
                var unit=member.Unit;
                if(member.Arrived){arrived++;continue;}
                if(member.Lost){lost++;continue;}
                bool eligible=CohesionGroundMember(unit)&&unit.Team==group.Team;
                if(!eligible)
                {
                    member.Lost=true;ClearCohesionCap(member);member.CapReason="membership invalid";
                    TraceCohesionMember(member,"released: destroyed, control/team changed or navigation unavailable");lost++;continue;
                }
                bool current=CurrentCohesionOrder(member);
                if(!member.Started&&!current)
                {
                    ClearCohesionCap(member);
                    if(PendingCohesionOrder(member)){pending++;continue;}
                    member.Lost=true;member.CapReason="captured move disappeared";
                    TraceCohesionMember(member,"released: captured move no longer pending");lost++;continue;
                }
                member.Direct=Vector3.Distance(Flat(unit.transform.position),Flat(member.Target));
                float arrival=Mathf.Max(.5f,unit.AIAgent.FinalWaypointThreshold);
                if(member.Direct<=arrival)
                {
                    member.Arrived=true;ClearCohesionCap(member);member.CapReason="within native arrival threshold";
                    TraceCohesionMember(member,"arrived within native threshold "+arrival.ToString("0.##",CultureInfo.InvariantCulture)+"m");arrived++;continue;
                }
                if(!current)
                {
                    ClearCohesionCap(member);
                    member.Lost=true;member.CapReason="move replaced or changed";
                    TraceCohesionMember(member,"released: current order, destination or mode changed");lost++;continue;
                }
                if(group.FirstActive<0)group.FirstActive=now;
                if(!member.Started){member.Started=true;member.StartedAt=now;member.LastPosition=unit.transform.position;member.LastProgress=now;member.LastRemaining=member.Direct;}
                member.Remaining=CohesionRoute(member,out bool routeReady);
                member.FreeSpeed=CohesionModeSpeed(unit,member.Mode);
                member.RouteReady=routeReady;
                if(routeReady&&!member.RouteInitialized)
                {
                    member.RouteInitialized=true;member.FreshPathAt=now;member.FreeArrival=now+member.Remaining/member.FreeSpeed;
                    member.InitialRemaining=member.Remaining;member.LastProgress=now;member.LastRemaining=member.Remaining;
                }
                bool wasSuspended=member.Suspended;
                member.Suspended=CohesionTraversalPaused(unit);
                if(member.Suspended)
                {
                    member.FreshPathAt=-1;member.HealthReady=false;
                    member.Stuck=false;member.Lagging=false;member.Obstructed=false;member.LastProgress=now;
                    ClearCohesionCap(member);member.CapReason="temporary traversal";
                    TraceCohesionMember(member,"suspended: temporary traversal; membership retained");active.Add(member);continue;
                }
                if(wasSuspended)
                {
                    member.LastProgress=now;member.LastPosition=unit.transform.position;member.LastRemaining=member.Remaining;
                    TraceCohesionMember(member,"resumed: ordinary movement");
                }
                if(routeReady&&member.FreshPathAt<0)member.FreshPathAt=now;
                member.HealthReady=routeReady&&member.FreshPathAt>=0&&now-member.FreshPathAt>=CohesionControl(group,"StartGrace");
                var agent=unit.AIAgent;var path=agent.AgentPathfinding;
                float progressDistance=CohesionControl(group,"ProgressDistance");
                bool moving=FlatDistanceSq(unit.transform.position,member.LastPosition)>=progressDistance*progressDistance;
                bool progress=member.Remaining<member.LastRemaining-progressDistance;
                if(moving||progress){member.LastProgress=now;member.LastPosition=unit.transform.position;member.LastRemaining=member.Remaining;}
                member.Obstructed=path.IsPathPaused||path.EngagementHold||agent.IsMoveProblem||agent.IsStuck||
                    (agent.AgentAiming!=null&&agent.AgentAiming.AimTarget!=null)||
                    (routeReady&&member.Remaining>Mathf.Max(10,member.Direct)*CohesionControl(group,"PathDetourRatio"));
                // Deliberate slow pacing must not look like an obstruction.
                float expected=member.Cap>=0?Mathf.Min(member.FreeSpeed,member.Cap):member.FreeSpeed;
                float watchdog=Mathf.Max(group.Tuning.StuckSeconds,progressDistance/Mathf.Max(.05f,expected)*2);
                member.Stuck=member.HealthReady&&(group.Phase!=CohesionPhase.Turning||now>=group.TurnUntil)&&now-member.LastProgress>watchdog;
                if(!routeReady||member.Obstructed||member.Stuck||member.Free)
                {
                    ClearCohesionCap(member);
                    member.CapReason=member.Free?"delay budget expired":!routeReady?"new path pending":member.Stuck?"no progress":"native obstruction or combat";
                    TraceCohesionMember(member,"cap bypass: "+member.CapReason+(member.HealthReady?"":"; startup grace"));
                }
                active.Add(member);
            }
            group.LostPercent=100f*lost/group.Members.Count;
            if(arrived+lost==group.Members.Count){ReleaseCohesion(group,arrived>0?"move finished":"all members lost",arrived>0);return;}
            if(group.LostPercent>=group.Tuning.BreakPercent)
            {ReleaseCohesion(group,"casualties/disbanded "+group.LostPercent.ToString("0")+"%",false);return;}
            if(pending>0)
            {
                if(group.FirstActive>=0&&now-group.FirstActive>CohesionControl(group,"StartGrace"))
                    ReleaseCohesion(group,"queued segments started too far apart",false);
                return;
            }
            if(active.Count<2){foreach(var member in active)ClearCohesionCap(member);return;}
            if(group.Started<0)
            {
                group.Started=now;
                group.InitialRadius=RadiusOfCohesion(active);
                var paceSet=active.Where(m=>!m.Suspended&&!m.Obstructed).ToList();
                group.ArrivalTime=now+(paceSet.Count>0?paceSet:active).Max(m=>m.Remaining/m.FreeSpeed);
                foreach(var member in active)
                {
                    member.InitialRemaining=member.Remaining;
                    if(member.FreeArrival<=0)member.FreeArrival=now+member.Remaining/member.FreeSpeed;
                }
                group.Phase=group.Tuning.HoldStrength>0&&group.Layout!=null&&!group.Layout.Invalidated&&!group.Drag&&!group.Reform?
                    CohesionPhase.Holding:CohesionPhase.Arrival;
                if(group.Phase==CohesionPhase.Holding&&group.Turned&&CohesionControl(group,"TurnGrace")>0)
                {group.Phase=CohesionPhase.Turning;group.TurnUntil=now+CohesionControl(group,"TurnGrace");}
                TraceCohesion(group,group.Phase==CohesionPhase.Turning?"turning formation toward travel; arrival pacing started":
                    group.Phase==CohesionPhase.Holding?"formation holding started":"arrival coordination started");
            }
            if((group.Phase==CohesionPhase.Holding||group.Phase==CohesionPhase.Turning)&&
                (group.Layout==null||group.Layout.Invalidated))
            {
                group.Phase=CohesionPhase.Arrival;
                TraceCohesion(group,"drag formation membership changed; arrival coordination retained");
            }
            var fresh=active.Where(m=>m.RouteReady&&!m.Suspended&&!m.Obstructed&&!m.Free).ToList();
            bool initialPathsReady=active.Where(m=>!m.Suspended).All(m=>m.RouteReady);
            if(!group.ArrivalRebased&&fresh.Count>=2&&(initialPathsReady||now-group.Started>=CohesionControl(group,"StartGrace")))
            {
                // Replace the provisional straight-line ETA once, using only paths
                // associated with this segment. Never chase a failing rear member.
                group.ArrivalTime=now+fresh.Max(m=>m.Remaining/m.FreeSpeed);group.ArrivalRebased=true;
                foreach(var member in fresh)
                {
                    member.FreeArrival=now+member.Remaining/member.FreeSpeed;
                    member.InitialRemaining=member.Remaining;member.LastProgress=now;
                }
                TraceCohesion(group,"arrival estimate rebased once from fresh native paths");
            }
            float median=MedianCohesion(active.Select(m=>m.Remaining-m.InitialRemaining).ToArray());
            foreach(var member in active)member.Lagging=member.HealthReady&&!member.Suspended&&member.Remaining-member.InitialRemaining-median>group.Tuning.LagDistance;
            int unhealthy=active.Count(m=>m.HealthReady&&(m.Stuck||m.Lagging||m.Obstructed||m.Free));
            group.UnhealthyPercent=100f*unhealthy/active.Count;
            group.Spread=Mathf.Max(0,RadiusOfCohesion(active)-group.InitialRadius);
            bool spreadBad=now-group.Started>=CohesionControl(group,"StartGrace")&&active.Any(m=>m.HealthReady)&&group.Spread>group.Tuning.MaxSpread;
            if(spreadBad){if(group.SpreadSince<0)group.SpreadSince=now;}else group.SpreadSince=-1;
            bool persistentSpread=group.SpreadSince>=0&&now-group.SpreadSince>=CohesionControl(group,"HoldGrace");
            if(group.UnhealthyPercent>=group.Tuning.BreakPercent||
                (persistentSpread&&((group.Phase!=CohesionPhase.Holding&&group.Phase!=CohesionPhase.Turning)||group.Spread>group.Tuning.MaxSpread*2)))
            {ReleaseCohesion(group,persistentSpread?"group spread exceeded":"unhealthy native movement/progress/budget "+group.UnhealthyPercent.ToString("0")+"%",false);return;}
            Vector3 reference=Vector3.zero;
            foreach(var member in active)reference+=Flat(member.Unit.transform.position)-member.Offset;
            reference/=active.Count;
            var holdSet=active.Where(m=>m.HealthReady&&!m.Suspended).ToList();
            if(group.Phase==CohesionPhase.Turning&&now>=group.TurnUntil)
            {
                int displaced=holdSet.Count(m=>Vector3.Distance(Flat(m.Unit.transform.position),reference+m.Offset)>
                    Mathf.Max(group.Tuning.HoldError,Radius(m.Unit)*1.5f));
                bool settled=holdSet.Count>=2&&100f*displaced/holdSet.Count<group.Tuning.HoldBreakPercent&&group.LostPercent<group.Tuning.HoldBreakPercent&&
                    100f*holdSet.Count(m=>m.Obstructed||m.Stuck)/holdSet.Count<group.Tuning.ObstructedPercent&&!persistentSpread;
                group.Phase=settled?CohesionPhase.Holding:CohesionPhase.Arrival;
                TraceCohesion(group,settled?"formation turn settled; holding started":"formation turn allowance ended; arrival coordination retained");
            }
            if(group.Phase==CohesionPhase.Holding&&holdSet.Count>=2&&now>=group.NextHold)
            {
                group.NextHold=now+group.Tuning.HoldCorrectionInterval;
                int displaced=holdSet.Count(m=>Vector3.Distance(Flat(m.Unit.transform.position),reference+m.Offset)>
                    Mathf.Max(group.Tuning.HoldError,Radius(m.Unit)*1.5f));
                bool bad=100f*displaced/holdSet.Count>=group.Tuning.HoldBreakPercent||group.LostPercent>=group.Tuning.HoldBreakPercent||
                    100f*holdSet.Count(m=>m.Obstructed||m.Stuck)/holdSet.Count>=group.Tuning.ObstructedPercent||persistentSpread;
                if(bad)
                {
                    if(group.HoldBadSince<0)group.HoldBadSince=now;
                    if(now-group.HoldBadSince>=CohesionControl(group,"HoldGrace"))
                    {group.Phase=CohesionPhase.Arrival;TraceCohesion(group,"formation holding cancelled; arrival coordination retained");}
                }
                else group.HoldBadSince=-1;
            }
            // Start from direct-distance estimates while initial native paths are
            // pending: a five-metre move must not spend a three-second grace
            // waiting before coordination starts. The getter still refuses to cap
            // paused/calculating paths; later replans use the normal readiness gate.
            var healthy=active.Where(m=>!m.Suspended&&!m.Stuck&&!m.Lagging&&!m.Obstructed&&!m.Free&&(m.RouteReady||!m.RouteInitialized)).ToList();
            if(healthy.Count<2){foreach(var member in active)ClearCohesionCap(member);return;}
            float pace=healthy.Min(m=>m.FreeSpeed);
            var healthySet=new HashSet<CohesionMember>(healthy);
            foreach(var member in active)
            {
                if(!healthySet.Contains(member)){ClearCohesionCap(member);continue;}
                if(member.Cap>=0&&member.AppliedCap>=0&&member.LastCapApplied>=now-dt-.1f)
                    member.DelaySpent+=dt*Mathf.Clamp01(1-Mathf.Min(member.Cap,member.FreeSpeed)/member.FreeSpeed);
                if(member.DelaySpent>=group.Tuning.MaxDelay||group.ArrivalRebased&&member.RouteInitialized&&now>member.FreeArrival+group.Tuning.MaxDelay)
                {
                    member.Free=true;ClearCohesionCap(member);member.CapReason="delay budget expired";
                    TraceCohesionMember(member,"released from pacing: delay budget expired");continue;
                }
                float remainingTime=group.ArrivalTime-now-group.Tuning.ArrivalWindow;
                float desired=remainingTime>.05f?Mathf.Min(member.FreeSpeed,member.Remaining/remainingTime):member.FreeSpeed;
                desired=Mathf.Lerp(member.FreeSpeed,desired,group.Tuning.Strength);
                if(group.Phase==CohesionPhase.Holding)
                {
                    float error=Vector3.Dot(reference+member.Offset-Flat(member.Unit.transform.position),group.Forward);
                    float tolerance=Mathf.Max(group.Tuning.HoldError,Radius(member.Unit)*1.5f);
                    float correction=Mathf.Sign(error)*Mathf.Max(0,Mathf.Abs(error)-tolerance)*.5f;
                    float hold=Mathf.Lerp(member.FreeSpeed,Mathf.Clamp(pace+correction,.05f,member.FreeSpeed),group.Tuning.HoldStrength);
                    desired=Mathf.Min(desired,hold);
                }
                desired=Mathf.Clamp(desired,member.FreeSpeed*group.Tuning.MinimumSpeed,member.FreeSpeed);
                if(desired>=member.FreeSpeed-.001f)
                {ClearCohesionCap(member);member.CapReason="independent speed meets pacing target";TraceCohesionMember(member,"independent speed meets pacing target");continue;}
                // Cap easing is bounded; removing a cap always restores native motion immediately.
                member.Cap=member.Cap>=0?Mathf.MoveTowards(member.Cap,desired,member.FreeSpeed*dt):desired;
                CohesionCaps[member.Unit]=member;
                TraceCohesionMember(member,"pacing cap requested");
            }
            if((CohesionDebugEnabled||group.Watched)&&now>=group.NextDebug)
            {
                group.NextDebug=now+CohesionControl(group,"DebugInterval");
                TraceCohesion(group,"tick: "+active.Count+" moving, "+arrived+" arrived, spread +"+group.Spread.ToString("0.0")+
                    "m, lost "+group.LostPercent.ToString("0")+"%, unhealthy "+group.UnhealthyPercent.ToString("0")+"%, "+
                    CohesionCaps.Values.Count(m=>ReferenceEquals(m.Group,group))+" caps requested");
                SampleCohesionMembers(group);
            }
        }
        private static float RadiusOfCohesion(List<CohesionMember> members)
        {
            Vector3 centre=Vector3.zero;foreach(var member in members)centre+=Flat(member.Unit.transform.position);
            centre/=members.Count;float radius=0;
            foreach(var member in members)radius=Mathf.Max(radius,Vector3.Distance(Flat(member.Unit.transform.position),centre));
            return radius;
        }
        private static float MedianCohesion(float[] values){Array.Sort(values);return values[values.Length/2];}
        private static void TraceCohesion(CohesionTravel group,string message)
        {
            group.Reason=message;
            string line="[Cohesion #"+group.Id+" "+(IsAlienTeam(group.Team)?"Alien":"Human")+" "+group.Tuning.Range+" "+group.Phase+"] +"+(Time.time-group.Created).ToString("0.0",CultureInfo.InvariantCulture)+"s "+message;
            group.History.Enqueue(line);
            while(group.History.Count>(int)CohesionControl(group,"TraceHistory"))group.History.Dequeue();
            if(CohesionDebugEnabled||group.Watched)MelonLogger.Msg(line);
        }
        private static void ReleaseCohesion(CohesionTravel group,string reason,bool completed)
        {
            foreach(var member in group.Members)ClearCohesionCap(member);
            if(group.Layout!=null)group.Layout.Time=Time.time;
            group.Phase=completed?CohesionPhase.Complete:CohesionPhase.Released;group.Ended=Time.time;
            TraceCohesion(group,reason+(completed?"":"; destination ASAP"));
        }
        private static void PruneCohesion(float now)
        {
            // A cancelled speed controller does not mean its move finished.
            // Keep the immutable intended layout while any captured move is
            // still current/queued, then apply normal post-move retention.
            foreach(var layout in CohesionLayouts.Values.Distinct())
            {
                if(!layout.Invalidated&&layout.Offsets.Keys.Any(unit=>!FormationEligible(unit)||unit.Team!=layout.Team))
                    InvalidateCohesionLayout(layout);
                if(LayoutMovePending(layout))layout.Time=now;
            }
            CohesionGroups.RemoveAll(g=>(g.Phase==CohesionPhase.Released||g.Phase==CohesionPhase.Complete)&&now-g.Ended>CohesionControl(g,"Retention"));
            foreach(var unit in CohesionLayouts.Keys.Where(u=>!u||u.IsDestroyed||now-CohesionLayouts[u].Time>CohesionValueFor(CohesionLayouts[u].Team,"Retention")).ToArray())CohesionLayouts.Remove(unit);
            foreach(var player in CohesionDebugNext.Where(p=>!p||!p.IsCommander).ToArray())CohesionDebugNext.Remove(player);
        }
        private static bool LayoutMovePending(CohesionLayout layout)=>!layout.Invalidated&&layout.Orders.Values.Any(member=>
            member.Unit&&!member.Unit.IsDestroyed&&member.Unit.Team==layout.Team&&PendingCohesionOrder(member));
        private static void ClearCohesionState(string reason)
        {
            foreach(var group in CohesionGroups)
                if(group.Phase!=CohesionPhase.Released&&group.Phase!=CohesionPhase.Complete)ReleaseCohesion(group,reason,false);
            CohesionCaps.Clear();CohesionLayouts.Clear();CohesionDebugNext.Clear();activeCohesion=null;nextCohesionTick=0;
        }
        private static void ClearFactionCohesion(bool alien,string reason)
        {
            foreach(var group in CohesionGroups)
                if(IsAlienTeam(group.Team)==alien&&group.Phase!=CohesionPhase.Released&&group.Phase!=CohesionPhase.Complete)ReleaseCohesion(group,reason,false);
            foreach(var unit in CohesionLayouts.Keys.Where(u=>IsAlienTeam(CohesionLayouts[u].Team)==alien).ToArray())CohesionLayouts.Remove(unit);
            foreach(var player in CohesionDebugNext.Where(p=>!p||IsAlienTeam(p.Team)==alien).ToArray())CohesionDebugNext.Remove(player);
            if(activeCohesion!=null&&IsAlienTeam(activeCohesion.Team)==alien)activeCohesion=null;
            nextCohesionTick=0;
        }
        private static string CohesionSummary(bool alien=false)=>(alien?"Alien":"Human")+" group cohesion "+
            ((alien?AlienCohesionEnabled:CohesionEnabled)?"ON":"OFF")+"; "+
            CohesionGroups.Count(g=>IsAlienTeam(g.Team)==alien&&g.Phase!=CohesionPhase.Released&&g.Phase!=CohesionPhase.Complete)+" active groups, "+
            CohesionCaps.Values.Count(m=>IsAlienTeam(m.Group.Team)==alien)+" speed caps.";
        private static void CohesionDebugCommand(Player player,string args)
        {
            if(!DemandAdmin(player))return;
            string value=Argument(args);
            if(value=="on"||value=="off")
            {
                CohesionDebugEnabled=value=="on";
                if(!CohesionDebugEnabled){foreach(var group in CohesionGroups)group.Watched=false;CohesionDebugNext.Clear();}
                Reply(player,"Cohesion global trace "+value+"; output is in the MelonLoader server log.");return;
            }
            if(value=="next")
            {
                if(!player.IsCommander){Reply(player,"Commander role required to watch your next move.");return;}
                CohesionDebugNext.Add(player);Reply(player,"The next grouped commander move will be traced, including any skip reason.");return;
            }
            if(value=="groups")
            {
                foreach(var group in CohesionGroups.TakeLast(8))Reply(player,"#"+group.Id+" "+group.Tuning.Range+" "+group.Phase+": "+group.Reason);
                if(CohesionGroups.Count==0)Reply(player,"No retained cohesion groups.");return;
            }
            if(int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out int id))
            {
                var group=CohesionGroups.FirstOrDefault(g=>g.Id==id);
                if(group==null){Reply(player,"Unknown or expired cohesion group #"+id+".");return;}
                group.Watched=true;foreach(string line in group.History.TakeLast(8))Reply(player,line);return;
            }
            if(value==""||value=="status")
            {Reply(player,CohesionSummary()+" "+CohesionSummary(true)+" Global trace "+(CohesionDebugEnabled?"ON":"OFF")+", "+CohesionGroups.Count(g=>g.Watched)+" watched, "+CohesionDebugNext.Count+" armed; /cohesiondebug next | groups | <id> | on | off.");return;}
            Reply(player,"/cohesiondebug on|off|status|groups|next|<group id>");
        }
    }
}
