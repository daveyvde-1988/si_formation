using System;
using System.Collections.Generic;
using System.Linq;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class LockedMember
        {
            internal Unit Unit;
            internal AIGroup OriginalGroup;
            internal Vector3 Offset,LastDestination;
            internal float OriginalScale,Maximum,Radius;
            internal uint Order;
            internal bool HasDestination;
            internal float DepartAt;
            internal uint[] Previous;
        }
        private sealed class LockedGroup
        {
            internal Player Owner;
            internal Team Team;
            internal readonly List<LockedMember> Members=new List<LockedMember>();
            internal Vector3 Destination,TravelForward;
            internal float Angle,Baseline,NextTick;
            internal bool Traveling;
            internal AgentMoveSpeed Speed;
        }
        private static readonly List<LockedGroup> Locks=new List<LockedGroup>();
        private static readonly Dictionary<Unit,LockedGroup> LockedUnits=new Dictionary<Unit,LockedGroup>();
        private static readonly Dictionary<Unit,float> LockedSpeedCaps=new Dictionary<Unit,float>();
        private static void LockedSpeedLimit(Unit __instance,ref float __result)
        {
            // The vehicle controller reads this getter for throttle limiting AND braking.
            // Enforce the requested cap even if an AI order resets MoveSpeedScale between ticks.
            if(!Running||!Available(__instance))return;
            if(LockedSpeedCaps.TryGetValue(__instance,out float cap))
                __result=__result<0?cap:Math.Min(__result,cap);
            if(SoftSpeedCaps.TryGetValue(__instance,out float departureCap))
                __result=__result<0?departureCap:Math.Min(__result,departureCap);
            if(FollowDepartureCaps.TryGetValue(__instance,out float followCap))
                __result=__result<0?followCap:Math.Min(__result,followCap);
        }
        private static bool LockEligible(Unit u,Player p)=>Available(u)&&!u.IsFlyingType&&
            !u.PlayerControlled&&u.CanMove&&u.Team==p.Team&&u.NormalSpeed>0.01f&&u.TopSpeed>0.01f;
        private static void ReleaseLockedUnit(Unit u)
        {
            if(ReferenceEquals(u,null)||!LockedUnits.TryGetValue(u,out var group))return;
            for(int i=group.Members.Count-1;i>=0;i--)if(group.Members[i].Unit==u)
            {RestoreMember(group.Members[i]);group.Members.RemoveAt(i);}
            LockedUnits.Remove(u);RecalculateBaseline(group);
        }
        private static void RestoreMember(LockedMember m)
        {
            LockedSpeedCaps.Remove(m.Unit);
            if(!m.Unit)return;
            // Only our current order is changed; a replacement order owns its own parameters.
            if(m.Unit.OrderAgent)foreach(var order in m.Unit.OrderAgent.Orders)
                if(order.Id==m.Order)order.SpeedScale=m.OriginalScale;
            m.Unit.MoveSpeedScale=m.OriginalScale;
        }
        private static void ReleaseCommander(Player p)
        {
            foreach(var u in LockHeadings.Where(e=>e.Value.Owner==p).Select(e=>e.Key).ToArray())LockHeadings.Remove(u);
            for(int i=Locks.Count-1;i>=0;i--)if(Locks[i].Owner==p)ReleaseLockAt(i);
            if(!ReferenceEquals(p,null))CommanderSettings.Remove(p);
        }
        private static void ReleaseLockAt(int index)
        {
            foreach(var m in Locks[index].Members){RestoreMember(m);LockedUnits.Remove(m.Unit);}
            Locks.RemoveAt(index);
        }
        private static void ClearLocks()
        {for(int i=Locks.Count-1;i>=0;i--)ReleaseLockAt(i);LockedSpeedCaps.Clear();LockHeadings.Clear();CommanderSettings.Clear();Menus.Clear();moderatorTravel=moderatorTolerance=moderatorFalloff=null;}
        private static void CommanderRoundEnded(GameMode mode,Team winner)=>ClearOrders();
        private static void RecalculateBaseline(LockedGroup g)
        {
            g.Baseline=float.MaxValue;
            foreach(var m in g.Members)g.Baseline=Math.Min(g.Baseline,m.Maximum);
            if(g.Members.Count==0)g.Baseline=0;
        }
        private static Vector3 Centre(LockedGroup g)
        {Vector3 centre=Vector3.zero;foreach(var m in g.Members)centre+=m.Unit.transform.position;return centre/Math.Max(1,g.Members.Count);}
        private static Vector3 Slot(LockedGroup g,LockedMember m)=>Quaternion.Euler(0,g.Angle,0)*m.Offset;
        private static bool HandleLockedOrder(Player p,ref List<BaseGameObject> objects,Vector3 destination,Target target,
            AgentMoveSpeed speed,bool attack,bool queued)
        {
            bool ordinary=!target&&!attack&&!queued;
            if(!ordinary)
            {
                foreach(var obj in objects)if(obj is Unit u&&u.Team==p.Team)ReleaseLockedUnit(u);
                return false;
            }
            var settings=Options(p);
            if(settings.Pending==2)
            {
                settings.Pending=0;
                return ScoutSelection(p,ref objects,destination,speed);
            }
            var selected=objects.OfType<Unit>().Where(u=>u&&u.Team==p.Team).Distinct().ToArray();
            // A snapshot owns just this move. A subsequent order releases only its selected
            // members, allowing their ordinary soft-lock cache to resume.
            foreach(var u in selected)ReleaseLockedUnit(u);
            if(settings.Pending!=1){foreach(var u in selected)LockHeadings.Remove(u);return false;}
            settings.Pending=0;
            var candidates=selected.Where(u=>LockEligible(u,p)).ToArray();
            if(candidates.Length<3)
            {
                Reply(p,"Lock denied: select at least three AI-controlled movable ground units. Infantry, vehicles and alien ground units are supported; flying and player-controlled units are excluded.");
                return false;
            }
            int owned=Locks.Where(g=>g.Owner==p).Sum(g=>g.Members.Count);
            if(owned+candidates.Length>CommanderOrderLimit)
            {
                Reply(p,"Lock denied: commander formation capacity "+CommanderOrderLimit+" would be exceeded.");
                return false;
            }
            var group=new LockedGroup {Owner=p,Team=p.Team,NextTick=Time.unscaledTime};
            Vector3 centre=Vector3.zero;foreach(var u in candidates)centre+=u.transform.position;centre/=candidates.Length;
            Vector3 initialHeading=PreviousLockHeading(p,candidates);
            float previousAngle=Mathf.Atan2(initialHeading.x,initialHeading.z)*Mathf.Rad2Deg;
            Vector3 travel=DistanceSq(destination,centre)>0.01f?Heading(destination-centre):initialHeading;
            bool mirrored=Reversed(initialHeading,travel);
            group.Angle=Mathf.Atan2(travel.x,travel.z)*Mathf.Rad2Deg;
            foreach(var u in candidates)
            {
                // Cancel old motion, but retain a complete selection's JSON slot assignments.
                CancelUnit(u,true);Followers.Remove(u);Superseded.Remove(u);
                float original=u.AIAgent.CohesionGroup!=null?1f:u.MoveSpeedScale;
                UnitCohesionGroup.Detach(u);
                var m=new LockedMember {Unit=u,OriginalGroup=u.AIGroup,OriginalScale=original,Maximum=u.TopSpeed,
                    Radius=Math.Max(1,u.PhysicalRadius),Offset=Quaternion.Euler(0,-previousAngle,0)*Flat(u.transform.position-centre)};
                if(mirrored)m.Offset.x=-m.Offset.x;
                group.Members.Add(m);LockedUnits.Add(u,group);
            }
            RecalculateBaseline(group);Locks.Add(group);StartLockedMove(group,destination,speed);
            var handled=new HashSet<Unit>(group.Members.Select(m=>m.Unit));
            Reply(p,"Formation snapshot applied to this move only: "+handled.Count+" units. The next move uses normal formation handling.");
            if(handled.Count==0)return false;
            // Never mutate the RPC's shared selected-object scratch list.
            objects=objects.Where(obj=>!(obj is Unit u)||!handled.Contains(u)).ToList();
            return objects.Count==0;
        }
        private static void RefreshSpeedCaps(LockedGroup g)
        {
            foreach(var m in g.Members)if(m.Unit)m.Maximum=m.Unit.TopSpeed;
            RecalculateBaseline(g);
            foreach(var m in g.Members)
            {
                if(g.Traveling&&m.HasDestination&&SpeedCapEnabled&&CoordinationPolicy.NeedsSpeedCap(m.Maximum,g.Baseline))
                    LockedSpeedCaps[m.Unit]=g.Baseline;
                else LockedSpeedCaps.Remove(m.Unit); // Ordinary cap exempts tied slowest members.
            }
            ApplyCentreCaps(g);
        }
        private static void StartLockedMove(LockedGroup g,Vector3 destination,AgentMoveSpeed speed)
        {
            g.Destination=destination;g.TravelForward=Heading(destination-Centre(g));g.Speed=speed;g.Traveling=true;
            float delay=SpeedLockDelay>0?SpeedLockDelay:LockDelay;
            var rows=SpeedLockDelay>0?SpeedDepartureRows(g.Members.Select(m=>m.Unit)):DepartureRows(g.Members.Select(m=>m.Unit),g.TravelForward);
            foreach(var m in g.Members)
            {
                // A new destination cancels all old delayed departures.
                m.HasDestination=false;m.DepartAt=Time.unscaledTime+rows[m.Unit]*delay;
                m.Previous=m.Unit.OrderAgent.Orders.Select(o=>o.Id).ToArray();
            }
            RefreshSpeedCaps(g);
            IssueLockedDestinations(g);
            int failed=0;
            foreach(var m in g.Members.ToArray())if(!m.HasDestination&&Time.unscaledTime>=m.DepartAt){ReleaseLockedUnit(m.Unit);failed++;}
            RefreshSpeedCaps(g);
            if(failed>0)Reply(g.Owner,failed+" units released: no valid formation destination; original request remains available to native movement.");
            Reply(g.Owner,$"Locked move: centre-based pacing, baseline {g.Baseline:0.0} m/s; "+(SpeedLockDelay>0?"slowest-first departures.":LockDelay>0?"positional departures.":"simultaneous departure."));
        }
        private static void TickLocks()
        {
            if(!Running){if(Locks.Count>0)ClearLocks();return;}
            float now=Time.unscaledTime;
            for(int index=Locks.Count-1;index>=0;index--)
            {
                var g=Locks[index];
                bool departureDue=g.Traveling&&g.Members.Any(m=>!m.HasDestination&&now>=m.DepartAt);
                if(now<g.NextTick&&!departureDue)continue;g.NextTick=now+0.2f;
                if(!g.Owner||!Player.Players.Contains(g.Owner)||!g.Owner.IsCommander||g.Owner.Team!=g.Team)
                {ReleaseLockAt(index);continue;}
                for(int i=g.Members.Count-1;i>=0;i--)
                {
                    var m=g.Members[i];
                    if(!LockEligible(m.Unit,g.Owner)||m.Unit.AIGroup!=m.OriginalGroup||
                        (g.Traveling&&m.Unit.OrderAgent.Orders.Count>0&&
                         (m.HasDestination?(m.Unit.OrderAgent.Orders.Count!=1||m.Unit.OrderAgent.Orders[0].Id!=m.Order):
                            !m.Previous.SequenceEqual(m.Unit.OrderAgent.Orders.Select(o=>o.Id)))))ReleaseLockedUnit(m.Unit);
                }
                if(g.Members.Count==0){Locks.RemoveAt(index);continue;}
                if(!g.Traveling)continue;
                IssueLockedDestinations(g);
                foreach(var m in g.Members.ToArray())if(!m.HasDestination&&now>=m.DepartAt)ReleaseLockedUnit(m.Unit);
                bool arrived=true;
                foreach(var m in g.Members)
                {
                    float arrivalRadius=Mathf.Clamp(m.Radius*0.5f,3f,8f);
                    if(!m.HasDestination||DistanceSq(m.Unit.transform.position,m.LastDestination)>arrivalRadius*arrivalRadius)
                        arrived=false;
                }
                if(arrived)
                {ReleaseLockAt(index);continue;}
                RefreshSpeedCaps(g);
            }
        }
        private static void IssueLockedDestinations(LockedGroup g)
        {
            generatedDepth++;
            try
            {
                foreach(var m in g.Members)
                {
                    if(m.HasDestination||Time.unscaledTime<m.DepartAt)continue;
                    Vector3 desired=g.Destination+Slot(g,m);
                    if(!TerrainProjection.TryProject(m.Unit,desired,out var projected))continue;
                    UnitCohesionGroup.Detach(m.Unit);
                    var target=new OrderTarget(projected);
                    var parameters=OrderIssueParams.Commanded(g.Speed,speedScale:m.OriginalScale);
                    if(!m.Unit.OrderAgent.IssueOrder(OrderDefinitionRegistry.Move,in target,in parameters))continue;
                    m.Order=m.Unit.OrderAgent.Orders.Count>0?m.Unit.OrderAgent.Orders[0].Id:0;
                    m.LastDestination=projected;m.HasDestination=true;
                    LockHeadings[m.Unit]=new LockHeading {Owner=g.Owner,Team=g.Team,Group=m.Unit.AIGroup,Forward=g.TravelForward};
                }
            }
            finally{generatedDepth--;}
        }
    }
}
