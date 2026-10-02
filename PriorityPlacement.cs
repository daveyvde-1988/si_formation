using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Silica.AI;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        // One yielded step performs at most one navigation projection. The scheduler shares
        // its frame budget between jobs. A budget boundary never advances a colour tier.
        private static IEnumerable<bool> SearchPositions(FormationDefinition f,Unit[] units,Vector3 centre,Vector3 forward,
            List<Placement> reserved,Func<Unit,bool> current,Func<Placement,bool> issue,bool mirrored=false,PlacementCache cache=null)
        {
            if(cache==null)cache=new PlacementCache(f,centre,forward,mirrored);
            var assigned=new HashSet<Unit>(reserved.Select(p=>p.Unit));
            var used=new HashSet<int>(reserved.Select(p=>p.Slot));
            for(int colour=0;colour<4;colour++)
            {
                var edges=new Dictionary<Unit,List<Placement>>();
                foreach(var u in units)
                {
                    if(assigned.Contains(u)||!current(u))continue;
                    var candidates=new List<Placement>();edges[u]=candidates;
                    for(int index=0;index<f.Slots.Length;index++)
                    {
                        var slot=f.Slots[index];
                        if(cache.Colour(index)!=colour||used.Contains(index)||!cache.Allows(u,index))continue;
                        // Cancellations can arrive between any two yields.
                        if(!current(u))break;
                        float radius=cache.UnitRadius(u);var desired=cache.Point(index);
                        if(Separated(desired,radius,reserved)&&TerrainProjection.TryProject(u,desired,out var point)&&Separated(point,radius,reserved))
                        {
                            var target=new OrderTarget(point);
                            bool canMove=u.OrderAgent.CanIssueOrder(OrderDefinitionRegistry.Move,in target);
                            if(canMove)candidates.Add(new Placement {Unit=u,Slot=index,Point=point,Radius=radius});
                        }
                        yield return true;
                    }
                    candidates.Sort((a,b)=>{int c=cache.Rank(u,a.Slot).CompareTo(cache.Rank(u,b.Slot));return c!=0?c:a.Slot.CompareTo(b.Slot);});
                }
                // Maximum bipartite matching within a colour prevents a flexible unit from
                // stranding a unit with only one reachable slot. Preferences order its edges.
                while(true)
                {
                    foreach(var u in edges.Keys.ToArray())
                    {
                        if(assigned.Contains(u)||!current(u)){edges.Remove(u);continue;}
                        edges[u].RemoveAll(p=>used.Contains(p.Slot)||!Separated(p.Point,p.Radius,reserved));
                        yield return false;
                    }
                    var owners=new Dictionary<int,Placement>();
                    foreach(var step in MatchPlacements(f,edges,owners,cache))yield return step;
                    if(owners.Count==0)break;
                    // Apply the matching, checking pairwise spacing as positions are reserved.
                    // Re-match only the residual group after failures or spacing conflicts.
                    foreach(var choice in owners.Values.OrderBy(p=>cache.Rank(p.Unit,p.Slot))
                        .ThenBy(p=>edges[p.Unit].Count).ThenBy(p=>p.Slot))
                    {
                        if(current(choice.Unit)&&Separated(choice.Point,choice.Radius,reserved)&&issue(choice))
                        {reserved.Add(choice);used.Add(choice.Slot);assigned.Add(choice.Unit);}
                        else edges[choice.Unit].Remove(choice);
                        yield return true;
                    }
                }
            }
        }
        private static IEnumerable<bool> MatchPlacements(FormationDefinition f,Dictionary<Unit,List<Placement>> edges,Dictionary<int,Placement> owners,PlacementCache cache)
        {
            // Successive shortest alternating paths: maximum cardinality first, then
            // lexicographically most explicit matches, category matches, unrestricted slots.
            // Reverse edges subtract the displaced assignment cost. Unlike ordinary DFS,
            // a flexible fallback cannot needlessly displace an explicit match.
            int basis=edges.Count+1;
            long Cost(Placement p)
            {
                int rank=cache.Rank(p.Unit,p.Slot);
                return rank==0?0:rank==1?basis*basis:rank==2?basis*basis+basis:basis*basis+basis+1;
            }
            while(true)
            {
                var matched=new HashSet<Unit>(owners.Values.Select(p=>p.Unit));
                var distance=edges.Keys.ToDictionary(u=>u,u=>matched.Contains(u)?long.MaxValue/4:0L);
                var previous=new Dictionary<Unit,Placement>();
                for(int pass=0;pass<edges.Count;pass++)
                {
                    bool changed=false;int checks=0;
                    foreach(var entry in edges)
                    {
                        if(distance[entry.Key]==long.MaxValue/4)continue;
                        foreach(var p in entry.Value)
                        {
                            if(owners.TryGetValue(p.Slot,out var old)&&old.Unit!=p.Unit)
                            {
                                long next=distance[p.Unit]+Cost(p)-Cost(old);
                                if(next<distance[old.Unit]){distance[old.Unit]=next;previous[old.Unit]=p;changed=true;}
                            }
                            if(++checks%64==0)yield return false;
                        }
                    }
                    yield return false;
                    if(!changed)break;
                }
                Placement end=null;long best=long.MaxValue/4;
                foreach(var entry in edges)
                {
                    if(distance[entry.Key]==long.MaxValue/4)continue;
                    foreach(var p in entry.Value)
                        if(!owners.ContainsKey(p.Slot)&&distance[p.Unit]+Cost(p)<best){best=distance[p.Unit]+Cost(p);end=p;}
                    yield return false;
                }
                if(end==null)yield break;
                while(true)
                {
                    owners[end.Slot]=end;
                    if(!previous.TryGetValue(end.Unit,out end))break;
                }
                yield return false;
            }
        }
    }
}
