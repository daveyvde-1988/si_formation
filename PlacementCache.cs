using System.Collections.Generic;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        // Request-local immutable geometry/preferences only. Navigation, current orders
        // and reservations are always checked live, including on later retry passes.
        private sealed class PlacementCache
        {
            private sealed class UnitFacts
            {
                internal string Id,Type;
                internal float Radius;
                internal readonly Dictionary<int,int> Ranks=new Dictionary<int,int>();
                internal readonly Dictionary<int,bool> Allowed=new Dictionary<int,bool>();
            }
            private readonly FormationDefinition definition;
            private readonly Vector3 centre,forward;
            private readonly bool mirrored;
            private readonly Dictionary<int,Vector3> points=new Dictionary<int,Vector3>();
            private readonly Dictionary<int,int> colours=new Dictionary<int,int>();
            private readonly Dictionary<Unit,UnitFacts> units=new Dictionary<Unit,UnitFacts>();
            internal PlacementCache(FormationDefinition f,Vector3 c,Vector3 heading,bool mirror)
            {definition=f;centre=c;forward=heading;mirrored=mirror;}
            private UnitFacts Facts(Unit u)
            {
                if(!units.TryGetValue(u,out var facts))units[u]=facts=new UnitFacts
                    {Id=UnitId(u),Type=PreferenceType(u),Radius=Radius(u,definition,null)};
                return facts;
            }
            internal float UnitRadius(Unit u)=>Facts(u).Radius;
            internal Vector3 Point(int slot)
            {
                if(!points.TryGetValue(slot,out var value))points[slot]=value=World(definition.Slots[slot],definition,centre,forward,mirrored);
                return value;
            }
            internal int Colour(int slot)
            {
                if(!colours.TryGetValue(slot,out int value))colours[slot]=value=FormationDefinition.Colour(definition.Slots[slot]);
                return value;
            }
            internal int Rank(Unit u,int slot)
            {
                var facts=Facts(u);
                if(!facts.Ranks.TryGetValue(slot,out int value))facts.Ranks[slot]=value=FormationDefinition.Match(definition.Slots[slot],facts.Id,facts.Type);
                return value;
            }
            internal bool Allows(Unit u,int slot)
            {
                var facts=Facts(u);
                if(!facts.Allowed.TryGetValue(slot,out bool value))facts.Allowed[slot]=value=FormationDefinition.Allows(definition.Slots[slot],facts.Id,facts.Type);
                return value;
            }
        }
    }
}
