using System;
using System.Collections.Generic;
using System.Linq;
using Pathfinding;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static bool ScoutEligible(Unit u,Player p)=>u&&!u.IsDestroyed&&u.Team==p.Team&&u.AIControlled&&
            !u.PlayerControlled&&!u.ControlledBy&&!u.InCompartment&&!(u.Driver&&u.Driver.ControlledBy)&&
            u.CanMove&&u.OrderAgent&&u.AIAgent&&u.AIAgent.AgentPathfinding&&u.AIAgent.AgentPathfinding.PathfindingSeeker&&u.NormalSpeed>0.01f&&
            (!u.IsFlyingType||u.AIAgent is AIAirVehicleAgent)&&u.AIAgent.AgentPathfinding.PathfindingSeeker.traversableTags==-1;
        private static bool ScoutSelection(Player p,ref List<BaseGameObject> objects,Vector3 clicked,AgentMoveSpeed speed)
        {
            int skipped=objects.OfType<Unit>().Distinct().Count(u=>u&&LockedUnits.ContainsKey(u));
            // Filter before ANY normal-order processing. On denial even a mixed selection cannot reach a locked member.
            var eligible=objects.OfType<Unit>().Distinct().Where(u=>ScoutEligible(u,p)&&!LockedUnits.ContainsKey(u)).ToList();
            objects=eligible.Cast<BaseGameObject>().ToList();
            if(eligible.Count<5)
            {
                MoveReply(p,"Scout denied: fewer than 5 eligible units. Locked formation units are excluded.");
                NativeScoutFallback(objects,clicked,speed);objects.Clear();return true;
            }
            int excess=Math.Max(0,eligible.Count-CommanderOrderLimit);
            if(excess>0)eligible.RemoveRange(CommanderOrderLimit,excess);
            if(eligible.Count<5)
            {MoveReply(p,"Scout denied: commander capacity is below 5.");NativeScoutFallback(objects,clicked,speed);objects.Clear();return true;}
            var terrain=Game.MainTerrain;
            if(!terrain||!terrain.terrainData||!AstarPath.active)
            {MoveReply(p,"Scouting started: 0 units deployed; map navigation unavailable; "+eligible.Count+" failures; "+skipped+" locked units skipped.");return true;}
            var bounds=new Bounds(terrain.transform.position+terrain.terrainData.bounds.center,terrain.terrainData.bounds.size);
            int count=eligible.Count,columns=Math.Max(1,Mathf.CeilToInt(Mathf.Sqrt(count*bounds.size.x/Math.Max(1,bounds.size.z))));
            columns=Math.Min(count,columns);int rows=Mathf.CeilToInt((float)count/columns);
            var destinations=new List<Vector3>(count);int dispatched=0,failures=0;
            generatedDepth++;
            try
            {
                for(int sector=0;sector<count;sector++)
                {
                    int row=sector/columns,col=sector%columns,rowCount=Math.Min(columns,count-row*columns);
                    Vector3 nominal=new Vector3(bounds.min.x+(col+0.5f)*bounds.size.x/rowCount,0,bounds.min.z+(row+0.5f)*bounds.size.z/rows);
                    int nearest=0;float distance=float.MaxValue;
                    for(int i=0;i<eligible.Count;i++)
                    {float d=DistanceSq(eligible[i].transform.position,nominal);if(d<distance){distance=d;nearest=i;}}
                    Unit u=eligible[nearest];eligible.RemoveAt(nearest);
                    bool placed=false;
                    // Bounded nearest-node/connectivity checks; no synchronous full path searches or recurring scout work.
                    for(int attempt=0;attempt<13;attempt++)
                    {
                        float radius=attempt==0?0:attempt<=6?150:295;
                        float angle=(attempt-1)%6*Mathf.PI/3;
                        Vector3 candidate=nominal+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                        if(!ScoutDestination(u,candidate,nominal,bounds,out var destination))continue;
                        float separation=Math.Max(30,Math.Min(bounds.size.x/columns,bounds.size.z/rows)*0.35f);
                        if(destinations.Any(d=>DistanceSq(d,destination)<separation*separation))continue;
                        var target=new OrderTarget(destination);var parameters=OrderIssueParams.Commanded(speed);
                        if(!u.OrderAgent.CanIssueOrder(OrderDefinitionRegistry.Move,in target))continue;
                        CancelUnit(u);Followers.Remove(u);Superseded.Remove(u);UnitCohesionGroup.Detach(u);
                        if(u.OrderAgent.IssueOrder(OrderDefinitionRegistry.Move,in target,in parameters))
                        {dispatched++;destinations.Add(destination);placed=true;}
                        break;
                    }
                    if(!placed)failures++;
                }
            }
            finally{generatedDepth--;}
            MoveReply(p,$"Scouting started: {dispatched} units deployed; {skipped} locked units skipped; {failures} destination/order failures; {excess} over capacity left untouched.");
            return true;
        }
        private static void NativeScoutFallback(List<BaseGameObject> objects,Vector3 clicked,AgentMoveSpeed speed)
        {
            foreach(var obj in objects)if(obj is Unit u){CancelUnit(u);Followers.Remove(u);Superseded.Remove(u);}
            generatedDepth++;
            try{StrategyMode.PerformMoveAttack(objects,clicked,null,speed,false,false,false);}
            finally{generatedDepth--;}
        }
        private static bool ScoutDestination(Unit u,Vector3 candidate,Vector3 nominal,Bounds bounds,out Vector3 point)
        {
            point=Vector3.zero;
            if(candidate.x<bounds.min.x||candidate.x>bounds.max.x||candidate.z<bounds.min.z||candidate.z>bounds.max.z)return false;
            // AIAirVehicleAgent.ProcessLongRangePath converts terrain/nav destinations using FlyPreferredHeight.
            // Supplying an invented altitude here would be processed twice by native flight navigation.
            if(!TerrainProjection.TryProject(u,candidate,out point)||DistanceSq(point,nominal)>300*300)return false;
            var constraint=new NearestNodeConstraint {graphMask=u.AIAgent.AgentPathfinding.PathfindingSeeker.graphMask,
                area=-1,tags=-1,maxDistance=75};
            Vector3 startPosition=u.IsFlyingType?GamePhysics.GetTerrainPosition(u.transform.position,0f,Game.MainTerrain):u.transform.position;
            var start=AstarPath.active.GetNearest(startPosition,constraint).node;
            var end=AstarPath.active.GetNearest(point,constraint).node;
            return start!=null&&end!=null&&PathUtilities.IsPathPossible(start,end)&&
                point.x>=bounds.min.x&&point.x<=bounds.max.x&&point.z>=bounds.min.z&&point.z<=bounds.max.z;
        }
    }
}
