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
        // Per-commander pending orders are transient action state, never configuration.
        private sealed class ScoutState { internal bool ScoutArmed; internal Team ScoutTeam; }
        private static readonly Dictionary<Player,ScoutState> Scouts=new Dictionary<Player,ScoutState>();
        private static ScoutState ScoutFor(Player p)
        {
            if(!Scouts.TryGetValue(p,out var state))Scouts[p]=state=new ScoutState();
            return state;
        }
        private static void ScoutRoundStarted(GameMode mode){ClearFpsStates();ClearRallyState();Scouts.Clear();}
        private static void ScoutRoundEnded(GameMode mode,Team winner){ScoutRoundStarted(mode);}
        private static void ScoutCommand(Player p,string args)
        {
            if(!ready||!Game.GetIsServer()||!p)return;
            if(!p.IsCommander||!p.Team){Reply(p,"Commander role required.");return;}
            string value=Argument(args);
            if(value=="status")
            {
                Scouts.TryGetValue(p,out var existing);
                Reply(p,"Scouting "+(existing!=null&&existing.ScoutArmed&&existing.ScoutTeam==p.Team?"armed":"not armed")+".");return;
            }
            if(!Active){Reply(p,"Formation mod is disabled.");return;}
            var settings=ScoutFor(p);
            if(value=="off"){settings.ScoutArmed=false;Reply(p,"Scouting cancelled.");return;}
            if(value!=""&&value!="on"){Reply(p,"/scout [on|off|status]");return;}
            settings.ScoutArmed=true;settings.ScoutTeam=p.Team;
            Reply(p,"Scouting armed. Select at least five AI units and issue a non-queued move. The click position is ignored; /scout off cancels.");
        }
        private static bool ScoutEligible(Unit u,Player p)=>u&&!u.IsDestroyed&&u.Team==p.Team&&u.AIControlled&&
            !u.PlayerControlled&&!u.ControlledBy&&!u.InCompartment&&!(u.Driver&&u.Driver.ControlledBy)&&
            u.CanMove&&u.OrderAgent&&u.AIAgent&&u.AIAgent.AgentPathfinding&&u.AIAgent.AgentPathfinding.PathfindingSeeker&&u.NormalSpeed>.01f&&
            (!u.IsFlyingType||u.AIAgent is AIAirVehicleAgent)&&u.AIAgent.AgentPathfinding.PathfindingSeeker.traversableTags==-1;
        private static float FlatDistanceSq(Vector3 a,Vector3 b){var d=a-b;return d.x*d.x+d.z*d.z;}
        private static bool DispatchScout(Player p,IReadOnlyList<BaseGameObject> objects,AgentMoveSpeed speed,out bool success)
        {
            success=false;
            var eligible=objects.OfType<Unit>().Distinct().Where(u=>ScoutEligible(u,p)).ToList();
            if(eligible.Count<5){Reply(p,"Scout requires at least five eligible units; using the normal move.");return false;}
            const int capacity=75;
            int excess=Math.Max(0,eligible.Count-capacity);
            if(excess>0)eligible.RemoveRange(capacity,excess);
            var terrain=Game.MainTerrain;
            if(!terrain||!terrain.terrainData||!AstarPath.active)
            {Reply(p,"Scouting unavailable: map navigation not ready; selected orders left unchanged.");return true;}
            var bounds=new Bounds(terrain.transform.position+terrain.terrainData.bounds.center,terrain.terrainData.bounds.size);
            int count=eligible.Count,columns=Math.Min(count,Math.Max(1,Mathf.CeilToInt(Mathf.Sqrt(count*bounds.size.x/Math.Max(1,bounds.size.z)))));
            int rows=Mathf.CeilToInt((float)count/columns),deployed=0,failed=0;
            var destinations=new List<Vector3>();
            for(int sector=0;sector<count;sector++)
            {
                int row=sector/columns,col=sector%columns,rowCount=Math.Min(columns,count-row*columns);
                var nominal=new Vector3(bounds.min.x+(col+.5f)*bounds.size.x/rowCount,0,bounds.min.z+(row+.5f)*bounds.size.z/rows);
                int nearest=0;float distance=float.MaxValue;
                for(int i=0;i<eligible.Count;i++)
                {float d=FlatDistanceSq(eligible[i].transform.position,nominal);if(d<distance){distance=d;nearest=i;}}
                var unit=eligible[nearest];eligible.RemoveAt(nearest);bool placed=false;
                for(int attempt=0;attempt<13;attempt++)
                {
                    float radius=attempt==0?0:attempt<=6?150:295,angle=(attempt-1)%6*Mathf.PI/3;
                    var candidate=nominal+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                    if(!ScoutDestination(unit,candidate,nominal,bounds,out var destination))continue;
                    float separation=Math.Max(30,Math.Min(bounds.size.x/columns,bounds.size.z/rows)*.35f);
                    if(destinations.Any(d=>FlatDistanceSq(d,destination)<separation*separation))continue;
                    var target=new OrderTarget(destination);var parameters=OrderIssueParams.Commanded(speed);
                    if(!unit.OrderAgent.CanIssueOrder(OrderDefinitionRegistry.Move,in target))continue;
                    UnitCohesionGroup.Detach(unit);
                    if(unit.OrderAgent.IssueOrder(OrderDefinitionRegistry.Move,in target,in parameters))
                    {deployed++;destinations.Add(destination);placed=true;}
                    break;
                }
                if(!placed)failed++;
            }
            success=deployed>0;
            Reply(p,$"Scouting: {deployed} units deployed; {failed} destination/order failures; {excess} over the 75-unit scout limit left untouched.");
            return true;
        }
        private static bool ScoutDestination(Unit unit,Vector3 candidate,Vector3 nominal,Bounds bounds,out Vector3 point)
        {
            point=Vector3.zero;
            if(candidate.x<bounds.min.x||candidate.x>bounds.max.x||candidate.z<bounds.min.z||candidate.z>bounds.max.z)return false;
            if(!TerrainProjection.TryProject(unit,candidate,out point)||FlatDistanceSq(point,nominal)>300*300)return false;
            var constraint=new NearestNodeConstraint{graphMask=unit.AIAgent.AgentPathfinding.PathfindingSeeker.graphMask,area=-1,tags=-1,maxDistance=75};
            Vector3 startPosition=unit.IsFlyingType?GamePhysics.GetTerrainPosition(unit.transform.position,0,Game.MainTerrain):unit.transform.position;
            var start=AstarPath.active.GetNearest(startPosition,constraint).node;
            var end=AstarPath.active.GetNearest(point,constraint).node;
            return start!=null&&end!=null&&PathUtilities.IsPathPossible(start,end)&&
                point.x>=bounds.min.x&&point.x<=bounds.max.x&&point.z>=bounds.min.z&&point.z<=bounds.max.z;
        }
    }
}
