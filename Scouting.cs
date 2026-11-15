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
        private const int ScoutTagUnitNodes=4096,ScoutTagCommandNodes=32768;
        private const double ScoutTagSearchSeconds=.008;
        private sealed class ScoutSearchBudget
        {
            internal int RemainingNodes=ScoutTagCommandNodes;
            internal long SearchTicks;
            internal bool Exhausted=>RemainingNodes<=0||SearchTicks>=System.Diagnostics.Stopwatch.Frequency*ScoutTagSearchSeconds;
        }
        private sealed class ScoutReachability
        {
            internal Seeker Seeker;
            internal GraphMask Graphs;
            internal int Tags;
            internal GraphNode Start;
            internal bool Limited;
            internal readonly HashSet<GraphNode> Reached=new HashSet<GraphNode>();
            internal readonly Queue<GraphNode> Frontier=new Queue<GraphNode>();
        }
        private static readonly Dictionary<Player,ScoutState> Scouts=new Dictionary<Player,ScoutState>();
        private static ScoutState ScoutFor(Player p)
        {
            if(!Scouts.TryGetValue(p,out var state))Scouts[p]=state=new ScoutState();
            return state;
        }
        private static void ScoutRoundStarted(GameMode mode){ClearWaypointState();ClearCohesionState("round changed");CohesionGroups.Clear();ClearFpsStates();ClearRallyState();ClearMovementDiagnostics();Scouts.Clear();}
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
            Reply(p,"Scouting armed. Select at least five scout-ready AI units and issue a non-queued move. Other ready units use the clicked destination; /scout off cancels.");
        }
        private static string ScoutMovementExclusion(Unit unit,Player player)
        {
            if(!unit||unit.IsDestroyed||!player||unit.Team!=player.Team)return "unavailable or not friendly";
            if(!unit.AIControlled||unit.PlayerControlled||unit.ControlledBy||unit.InCompartment||(unit.Driver&&unit.Driver.ControlledBy))
                return "player-driven, embarked or not AI-controlled";
            if(unit is Vehicle vehicle&&vehicle.HasForceMove)return "forced movement";
            if(!unit.CanMove)return "movement unavailable";
            if(!unit.OrderAgent||!unit.AIAgent||!unit.AIAgent.AgentPathfinding||!unit.AIAgent.AgentPathfinding.PathfindingSeeker||!AstarPath.active)
                return "navigation not ready";
            return null;
        }
        private static string ScoutExclusion(Unit unit,Player player)
        {
            string reason=ScoutMovementExclusion(unit,player);if(reason!=null)return reason;
            if(!(unit.NormalSpeed>.01f))return "no normal travel speed";
            if(IsFormationAir(unit)&&!(unit.AIAgent is AIAirVehicleAgent)&&!(unit.AIAgent is AIAlienAgent))return "unsupported flight agent";
            var seeker=unit.AIAgent.AgentPathfinding.PathfindingSeeker;
            if(seeker.traversableTags==0)return "no traversable navigation tags";
            // An arbitrary provider may forbid connections beyond graph/tag rules.
            // Leave those units to their own native movement instead of guessing.
            if(seeker.traversalProvider!=null)return "custom navigation provider";
            return null;
        }
        private static float FlatDistanceSq(Vector3 a,Vector3 b){var d=a-b;return d.x*d.x+d.z*d.z;}
        private static void CountScoutReason(Dictionary<string,int> reasons,string reason)
        {reason=reason??"unknown";reasons.TryGetValue(reason,out int count);reasons[reason]=count+1;}
        private static string ScoutReasons(Dictionary<string,int> reasons)=>string.Join(", ",reasons.OrderByDescending(item=>item.Value)
            .ThenBy(item=>item.Key,StringComparer.Ordinal).Select(item=>item.Key+" "+item.Value));
        private static bool DispatchScout(Player p,IReadOnlyList<BaseGameObject> objects,Vector3 clickedPosition,AgentMoveSpeed speed,out bool success)
        {
            success=false;
            var selected=objects.OfType<Unit>().Where(unit=>unit&&!unit.IsDestroyed&&unit.Team==p.Team).Distinct().ToList();
            var eligible=new List<Unit>();var exclusions=new Dictionary<string,int>();
            foreach(var unit in selected)
            {string reason=ScoutExclusion(unit,p);if(reason==null)eligible.Add(unit);else CountScoutReason(exclusions,reason);}
            int excluded=selected.Count-eligible.Count;
            if(eligible.Count<5)
            {
                Reply(p,"Scout requires at least five scout-ready units; "+eligible.Count+" eligible, "+excluded+" excluded. Using the normal move.");
                if(exclusions.Count>0)Reply(p,"Scout exclusions: "+ScoutReasons(exclusions)+".");
                return false;
            }
            const int capacity=75;
            int excess=Math.Max(0,eligible.Count-capacity);
            if(excess>0)eligible.RemoveRange(capacity,excess);
            var terrain=Game.MainTerrain;
            var deployed=new HashSet<Unit>();var failures=new Dictionary<string,int>();
            var budget=new ScoutSearchBudget();int failed=0;
            if(terrain&&terrain.terrainData&&AstarPath.active)
            {
                var bounds=new Bounds(terrain.transform.position+terrain.terrainData.bounds.center,terrain.terrainData.bounds.size);
                int count=eligible.Count,columns=Math.Min(count,Math.Max(1,Mathf.CeilToInt(Mathf.Sqrt(count*bounds.size.x/Math.Max(1,bounds.size.z)))));
                int rows=Mathf.CeilToInt((float)count/columns);var destinations=new List<Vector3>();
                for(int sector=0;sector<count;sector++)
                {
                    int row=sector/columns,col=sector%columns,rowCount=Math.Min(columns,count-row*columns);
                    var nominal=new Vector3(bounds.min.x+(col+.5f)*bounds.size.x/rowCount,0,bounds.min.z+(row+.5f)*bounds.size.z/rows);
                    int nearest=0;float distance=float.MaxValue;
                    for(int i=0;i<eligible.Count;i++)
                    {float d=FlatDistanceSq(eligible[i].transform.position,nominal);if(d<distance){distance=d;nearest=i;}}
                    var unit=eligible[nearest];eligible.RemoveAt(nearest);bool placed=false;string reason="no valid destination";
                    ScoutReachability reachable=null;
                    try
                    {
                        for(int attempt=0;attempt<13;attempt++)
                        {
                            float radius=attempt==0?0:attempt<=6?150:295,angle=(attempt-1)%6*Mathf.PI/3;
                            var candidate=nominal+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                            if(!ScoutDestination(unit,candidate,nominal,bounds,budget,ref reachable,out var destination,out reason))continue;
                            float separation=Math.Max(30,Math.Min(bounds.size.x/columns,bounds.size.z/rows)*.35f);
                            if(destinations.Any(d=>FlatDistanceSq(d,destination)<separation*separation))
                            {reason="destination separation";continue;}
                            if(TryScoutMove(unit,p,clickedPosition,destination,speed,"scout",out reason,out bool retry))
                            {deployed.Add(unit);destinations.Add(destination);placed=true;break;}
                            if(!retry)break;
                        }
                    }
                    catch(Exception e)
                    {reason="navigation exception";MelonLoader.MelonLogger.Warning("Scout planning failed for unit "+unit.GetInstanceID()+": "+e);}
                    if(!placed){failed++;CountScoutReason(failures,reason);}
                }
            }
            else
            {failed=eligible.Count;failures["map navigation not ready"]=failed;}
            var fallback=new List<Unit>();var protectedReasons=new Dictionary<string,int>();
            foreach(var unit in selected.Where(unit=>!deployed.Contains(unit)))
            {string reason=ScoutMovementExclusion(unit,p);if(reason==null)fallback.Add(unit);else CountScoutReason(protectedReasons,reason);}
            var points=new Dictionary<Unit,Vector3>();
            if(fallback.Count>0)
            {
                var ground=fallback.Where(unit=>!IsFormationAir(unit)).ToList();var facing=ground.Count>0?ground:fallback;
                Vector3 centre=facing.Aggregate(Vector3.zero,(sum,unit)=>sum+unit.transform.position)/facing.Count;
                try{points=GridSlots(fallback,clickedPosition,clickedPosition-centre,ServerSettings,
                    Multiplier(ServerSettings.Spacing)*FactionScale(p.Team,ServerSettings),out var pitch);}
                catch(Exception e){MelonLoader.MelonLogger.Warning("Scout fallback placement kept original click: "+e);}
            }
            int fallbackAccepted=0,fallbackRejected=0;var fallbackFailures=new Dictionary<string,int>();
            foreach(var unit in fallback)
            {
                Vector3 destination=points.TryGetValue(unit,out var point)?point:clickedPosition;
                if(TryScoutMove(unit,p,clickedPosition,destination,speed,"scout fallback",out string reason,out bool retry))fallbackAccepted++;
                else{fallbackRejected++;CountScoutReason(fallbackFailures,reason);}
            }
            success=deployed.Count+fallbackAccepted>0;
            Reply(p,$"Scouting: {deployed.Count} scout orders accepted; {failed} scout failures; {excluded} scout exclusions; {excess} over the 75-unit limit. Original-click moves: {fallbackAccepted} accepted, {fallbackRejected} rejected; {protectedReasons.Values.Sum()} protected/not-ready units unchanged.");
            if(exclusions.Count>0)Reply(p,"Scout exclusions: "+ScoutReasons(exclusions)+".");
            if(failures.Count>0)Reply(p,"Scout failures (last candidate reason): "+ScoutReasons(failures)+".");
            if(fallbackFailures.Count>0)Reply(p,"Original-click failures: "+ScoutReasons(fallbackFailures)+".");
            if(protectedReasons.Count>0)Reply(p,"Left unchanged: "+ScoutReasons(protectedReasons)+".");
            return true;
        }
        private static bool TryScoutMove(Unit unit,Player player,Vector3 clickedPosition,Vector3 destination,AgentMoveSpeed speed,
            string source,out string reason,out bool retry)
        {
            reason=ScoutMovementExclusion(unit,player);retry=false;if(reason!=null)return false;
            var trace=BeginMovementAttempt(unit,clickedPosition,destination,source);bool accepted=false;
            try
            {
                var target=new OrderTarget(destination);var parameters=OrderIssueParams.Commanded(speed);
                retry=true;
                if(!unit.OrderAgent.CanIssueOrder(OrderDefinitionRegistry.Move,in target)){reason="native Move unavailable";return false;}
                var previous=unit.AIAgent.CohesionGroup;
                accepted=unit.OrderAgent.IssueOrder(OrderDefinitionRegistry.Move,in target,in parameters);
                if(accepted)
                {
                    // Release only the old native group after acceptance. A failed
                    // issue must not detach the unit, nor should a callback-created
                    // replacement group be mistaken for the previous ownership.
                    if(previous!=null&&unit&&unit.AIAgent&&ReferenceEquals(unit.AIAgent.CohesionGroup,previous))UnitCohesionGroup.Detach(unit);
                    reason=null;return true;
                }
                reason="native Move rejected";return false;
            }
            catch(Exception e)
            {reason="native order exception";retry=false;MelonLoader.MelonLogger.Warning("Scout "+source+" issue failed: "+e);return accepted;}
            finally{EndMovementAttempt(trace,accepted);}
        }
        private static bool ScoutNodeAllowed(GraphNode node,ScoutReachability reachable)=>node!=null&&!node.Destroyed&&node.Walkable&&
            reachable.Graphs.Contains(node.GraphIndex)&&((reachable.Tags>>(int)node.Tag)&1)!=0;
        private static bool ScoutRestrictedPath(ScoutReachability reachable,GraphNode end,ScoutSearchBudget budget,out string reason)
        {
            reason=null;
            if(end==reachable.Start)return true;
            if(reachable.Reached.Contains(end))return true;
            if(reachable.Limited||budget.Exhausted){reason="tag preflight budget exhausted";return false;}
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                while(reachable.Frontier.Count>0&&!reachable.Limited)
                {
                    if(budget.RemainingNodes<=0||budget.SearchTicks+System.Diagnostics.Stopwatch.GetTimestamp()-started>=System.Diagnostics.Stopwatch.Frequency*ScoutTagSearchSeconds)
                    {reachable.Limited=true;break;}
                    reachable.Frontier.Dequeue().GetConnections(node=>
                    {
                        if(!ScoutNodeAllowed(node,reachable)||reachable.Reached.Contains(node))return;
                        if(reachable.Reached.Count>=ScoutTagUnitNodes||budget.RemainingNodes<=0){reachable.Limited=true;return;}
                        reachable.Reached.Add(node);reachable.Frontier.Enqueue(node);budget.RemainingNodes--;
                    });
                    if(reachable.Reached.Contains(end))return true;
                }
                reason=reachable.Limited?"tag preflight budget exhausted":"no route through allowed tags";return false;
            }
            finally{budget.SearchTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-started;}
        }
        private static bool ScoutDestination(Unit unit,Vector3 candidate,Vector3 nominal,Bounds bounds,ScoutSearchBudget budget,
            ref ScoutReachability reachable,out Vector3 point,out string reason)
        {
            point=Vector3.zero;reason="navigation not ready";
            if(!unit||!unit.AIAgent||!unit.AIAgent.AgentPathfinding||!unit.AIAgent.AgentPathfinding.PathfindingSeeker||!AstarPath.active)return false;
            var seeker=unit.AIAgent.AgentPathfinding.PathfindingSeeker;
            if(seeker.traversalProvider!=null){reason="custom navigation provider";return false;}
            if(candidate.x<bounds.min.x||candidate.x>bounds.max.x||candidate.z<bounds.min.z||candidate.z>bounds.max.z)
            {reason="outside map bounds";return false;}
            if(!TerrainProjection.TryProject(unit,candidate,out point)||FlatDistanceSq(point,nominal)>300*300)
            {reason="destination projection";return false;}
            var constraint=new NearestNodeConstraint{graphMask=seeker.graphMask,area=-1,tags=seeker.traversableTags,maxDistance=75};
            if(reachable==null||reachable.Seeker!=seeker||reachable.Graphs!=seeker.graphMask||reachable.Tags!=seeker.traversableTags)
            {
                Vector3 startPosition=unit.IsFlyingType?GamePhysics.GetTerrainPosition(unit.transform.position,0,Game.MainTerrain):unit.transform.position;
                reachable=new ScoutReachability{Seeker=seeker,Graphs=seeker.graphMask,Tags=seeker.traversableTags,
                    Start=AstarPath.active.GetNearest(startPosition,constraint).node};
                if(!ScoutNodeAllowed(reachable.Start,reachable)){reason="no allowed start node";return false;}
                if(reachable.Tags!=-1&&budget.Exhausted)reachable.Limited=true;
                else
                {
                    reachable.Reached.Add(reachable.Start);reachable.Frontier.Enqueue(reachable.Start);
                    if(reachable.Tags!=-1)budget.RemainingNodes--;
                }
            }
            if(!ScoutNodeAllowed(reachable.Start,reachable)){reason="no allowed start node";return false;}
            var nearest=AstarPath.active.GetNearest(point,constraint);var end=nearest.node;
            if(!ScoutNodeAllowed(end,reachable)||!PathUtilities.IsPathPossible(reachable.Start,end))
            {reason="no connected allowed destination";return false;}
            if(FlatDistanceSq(point,nearest.position)>25||Mathf.Abs(point.y-nearest.position.y)>75)
            {reason="allowed destination too far from projection";return false;}
            point=nearest.position;
            if(point.x<bounds.min.x||point.x>bounds.max.x||point.z<bounds.min.z||point.z>bounds.max.z||FlatDistanceSq(point,nominal)>300*300)
            {reason="projected destination outside sector";return false;}
            if(reachable.Tags!=-1&&!ScoutRestrictedPath(reachable,end,budget,out reason))return false;
            reason=null;return true;
        }
    }
}
