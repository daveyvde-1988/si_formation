using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Si_Formation
{
    internal sealed class FormationSlot
    {
        internal readonly float X, Z, Size;
        internal readonly string Role;
        internal readonly int? Preference;
        internal readonly bool ForceExclusive;
        internal readonly HashSet<string> Preferred;
        internal readonly HashSet<string> PreferredTypes;
        internal FormationSlot(float x, float z, string role, float size, IEnumerable<string> preferred, int? preference=null,IEnumerable<string> preferredTypes=null,bool forceExclusive=false)
        { ForceExclusive=forceExclusive; X=x; Z=z; Role=role; Size=size; Preference=preference; Preferred=new HashSet<string>(preferred, StringComparer.OrdinalIgnoreCase); PreferredTypes=new HashSet<string>(preferredTypes??Array.Empty<string>(),StringComparer.Ordinal); }
    }

    internal sealed class FormationDefinition
    {
        internal readonly string Name, Team, Function;
        internal readonly bool DirectionSensitive;
        internal readonly float Scale;
        internal readonly FormationSlot[] Slots;
        internal int MenuOrder;
        internal string Source;
        private readonly Dictionary<string, int[]> ranks = new Dictionary<string, int[]>(StringComparer.Ordinal);
        private readonly Dictionary<string,Tuple<int,int>> specificity=new Dictionary<string,Tuple<int,int>>(StringComparer.Ordinal);
        internal Tuple<int,int> Specificity(string id,string type)
        {
            string key=id+"|"+type;
            if(!specificity.TryGetValue(key,out var value))
            {
                int best=4,count=0;
                foreach(var slot in Slots){int match=Match(slot,id,type);if(match<best){best=match;count=1;}else if(match==best)count++;}
                specificity[key]=value=Tuple.Create(best,count);
            }
            return value;
        }
        internal FormationDefinition(string name, string team, string function, bool sensitive, float scale, FormationSlot[] slots, int menuOrder=0)
        { Name=name; Team=team; Function=function; DirectionSensitive=sensitive; Scale=scale; Slots=slots; MenuOrder=menuOrder; }
        // Pure comparisons only: no terrain/path queries while ranking candidate dots.
        internal int[] Ranked(string unitId, string unitType)
        {
            string key=unitId+"|"+unitType;
            if (!ranks.TryGetValue(key, out int[] result))
            {
                result=Enumerable.Range(0,Slots.Length).Where(i=>Allows(Slots[i],unitId,unitType)).OrderBy(i=>Rank(Slots[i],unitId,unitType)).ThenBy(i=>i).ToArray();
                ranks.Add(key,result);
            }
            return result;
        }
        internal static bool Allows(FormationSlot s,string id,string unitType)
            =>!s.ForceExclusive||s.Preferred.Contains(id)||s.PreferredTypes.Contains(unitType);
        internal static int Match(FormationSlot s,string id,string unitType)
            =>s.Preferred.Contains(id)?0:s.PreferredTypes.Contains(unitType)?1:s.Preferred.Count==0&&s.PreferredTypes.Count==0?2:3;
        internal static int Colour(FormationSlot s)=>Math.Min(3,s.Preference??(s.Role=="top"?0:s.Role=="backup"?1:2));
        private static int Rank(FormationSlot s, string id, string unitType)
        {
            int role=Colour(s);
            // Backup is an alternative, never an extra order. Explicit matching preferences win within each tier.
            return role*4+Match(s,id,unitType);
        }
    }

    internal sealed class FormationCatalog
    {
        internal readonly Dictionary<string, FormationDefinition> Definitions = new Dictionary<string, FormationDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal static string Key(string team,string function,string name)=>team+"|"+function+"|"+name.Trim();
        internal bool TryGet(string team,string function,string name,out FormationDefinition definition)
            => Definitions.TryGetValue(Key(team,function,name),out definition);
        internal void Load(string directory, Action<string> report)
        {
            Definitions.Clear(); duplicates.Clear();
            if (!Directory.Exists(directory)) { report("No JSON directory: "+directory); return; }
            var files=Directory.GetFiles(directory,"*.json").OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ThenBy(p=>p,StringComparer.Ordinal).ToArray();
            if(files.Length>128) { report("Too many JSON files (maximum 128); no formations loaded."); return; }
            var ordered=new List<FormationDefinition>();
            foreach(string file in files)
            {
                try
                {
                    if(new FileInfo(file).Length>2*1024*1024) throw new FormatException("file exceeds 2 MiB");
                    foreach(var f in Parse(File.ReadAllText(file)))
                    {
                        f.Source=file;ordered.Add(f);
                        string key=Key(f.Team,f.Function,f.Name);
                        if(duplicates.Contains(key)||Definitions.ContainsKey(key))
                        { Definitions.Remove(key); duplicates.Add(key); report(file+": duplicate "+key+"; ALL copies disabled"); }
                        else Definitions.Add(key,f);
                    }
                }
                catch(Exception e) { report(file+": rejected: "+e.Message); }
            }
            // Reserve every explicit number before assigning legacy numbers, including disputed ones.
            var reserved=new Dictionary<string,HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            foreach(var group in ordered.GroupBy(f=>f.Team+"|"+f.Function))
                reserved[group.Key]=new HashSet<int>(group.Where(f=>f.MenuOrder!=0).Select(f=>f.MenuOrder));
            foreach(var group in ordered.Where(f=>f.MenuOrder!=0).GroupBy(f=>f.Team+"|"+f.Function+"|"+f.MenuOrder))
                if(group.Count()>1)
                {
                    report("Duplicate menu number "+group.Key+": "+string.Join("; ",group.Select(f=>f.Source+" ["+f.Name+"]"))+"; ALL conflicting definitions disabled; number remains reserved.");
                    foreach(var f in group)Definitions.Remove(Key(f.Team,f.Function,f.Name));
                }
            foreach(var f in ordered.Where(f=>f.MenuOrder==0))
            {
                string key=Key(f.Team,f.Function,f.Name);
                if(!Definitions.TryGetValue(key,out var kept)||!ReferenceEquals(kept,f))continue;
                var used=reserved[f.Team+"|"+f.Function];int number=2;while(number<=9999&&used.Contains(number))number++;
                if(number>9999){Definitions.Remove(key);report(f.Source+": no free menu number for "+key);continue;}
                f.MenuOrder=number;used.Add(number);
            }
            report("Cached "+Definitions.Count+" definitions from "+files.Length+" JSON files (startup only).");
        }
        internal static FormationDefinition[] Parse(string json)
        {
            using(var reader=new JsonTextReader(new StringReader(json)) { MaxDepth=32, DateParseHandling=DateParseHandling.None })
            {
                var document=JObject.Load(reader,new JsonLoadSettings { DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error });
                if(reader.Read()) throw new FormatException("trailing JSON data");
                if(Text(document,"format")!="silica-formations" || Number(document,"version")!=3)
                    throw new FormatException("requires silica-formations version 3; re-export older files in editor");
                if(document.TryGetValue("menu",out JToken menuToken))
                {
                    if(!(menuToken is JObject menu)||menu["defaultOption"]?.Type!=JTokenType.Integer||menu["defaultOption"].ToString()!="1"||menu["defaultLabel"]?.Type!=JTokenType.String||(string)menu["defaultLabel"]!="default")
                        throw new FormatException("menu must reserve defaultOption 1 with defaultLabel default");
                }
                if(!(document["formations"] is JArray entries)||entries.Count==0||entries.Count>128)
                    throw new FormatException("formations must contain 1..128 entries");
                var result=new List<FormationDefinition>();
                foreach(var entry in entries)
                {
                    if(!(entry is JObject f)) throw new FormatException("formation must be an object");
                    string name=Text(f,"name"), team=Text(f,"team"), function=Text(f,"function");
                    if(name!=name.Trim()||name.Length>128||name.Contains("|")||name.Any(char.IsControl)) throw new FormatException("invalid formation name");
                    if(!new[]{"Sol","Centauri","Alien"}.Contains(team)||!new[]{"follow","move","commander","attack"}.Contains(function))
                        throw new FormatException(name+": invalid team/function");
                    int menuOrder=0;
                    if(f.TryGetValue("menuOrder",out JToken menuNumber)&&
                        (menuNumber.Type!=JTokenType.Integer||!int.TryParse(menuNumber.ToString(),out menuOrder)||menuOrder<2||menuOrder>9999))
                        throw new FormatException(name+": menuOrder must be an integer 2..9999; /1 is virtual Default");
                    float scale=Number(f,"metresPerNode");
                    if(scale<=0||scale>1000) throw new FormatException(name+": scale must be >0 and <=1000");
                    if(f["directionSensitive"]?.Type!=JTokenType.Boolean) throw new FormatException(name+": missing directionSensitive");
                    bool sensitive=(bool)f["directionSensitive"];
                    Number(f,"directionDegrees"); // drawing angle only; DO NOT rotate exported local coordinates again.
                    if(Text(f,"coordinateSpace")!=(sensitive?"local-right-forward":"world-xz")) throw new FormatException(name+": coordinateSpace mismatch");
                    if(!(f["origin"] is JObject origin)||Number(origin,"x")!=0||Number(origin,"z")!=0) throw new FormatException(name+": origin must be zero");
                    if(!(f["slots"] is JArray slots)||slots.Count==0||slots.Count>4096) throw new FormatException(name+": slots must contain 1..4096 candidates");
                    var parsed=new List<FormationSlot>();
                    foreach(var token in slots)
                    {
                        if(!(token is JObject s)) throw new FormatException("slot must be object");
                        float x=Number(s,"x"),z=Number(s,"z"),size=Number(s,"sizeScale");
                        if(Math.Abs(x)>10000||Math.Abs(z)>10000) throw new FormatException(name+": offset exceeds 10000 metres");
                        string role=Text(s,"role");
                        if(!new[]{"top","repair","purple","backup","last"}.Contains(role)) throw new FormatException(name+": unknown priority "+role);
                        if(size!=0.5f&&size!=1&&size!=2) throw new FormatException(name+": invalid sizeScale");
                        if(!(s["preferredUnits"] is JArray preferences)) throw new FormatException(name+": missing preferredUnits");
                        var ids=new List<string>();
                        foreach(var preference in preferences)
                        {
                            if(preference.Type!=JTokenType.String||!UnitIds.TryGetValue(team+"|"+(string)preference,out string id))
                                throw new FormatException(name+": unknown preferred unit "+preference+" for "+team);
                            ids.Add(id);
                        }
                        int? numericPreference=null;
                        var types=new List<string>();
                        if(s.TryGetValue("preferredTypes",out JToken typesToken))
                        {
                            if(!(typesToken is JArray typeArray))throw new FormatException(name+": preferredTypes must be an array");
                            foreach(var t in typeArray)
                            {
                                if(t.Type!=JTokenType.String||!EditorCategories.Names.Contains((string)t))
                                    throw new FormatException(name+": unknown preferred type "+t);
                                if((string)t!="Harvester")types.Add((string)t);
                            }
                        }
                        // Migrate old repair roles to ordinary preferences, not a special runtime rule.
                        if(role=="repair"||role=="purple")
                        {types.Add("Repair");role=role=="repair"?"top":"backup";}
                        if(s.TryGetValue("preference",out JToken preferenceToken))
                        {
                            if(preferenceToken.Type!=JTokenType.Integer||!int.TryParse(preferenceToken.ToString(),out int value)||value<0||value>4)
                                throw new FormatException(name+": preference must be an integer from 0 to 4");
                            numericPreference=value;
                        }
                        bool exclusive=false;
                        if(s.TryGetValue("forceExclusive",out JToken exclusiveToken))
                        {
                            if(exclusiveToken.Type!=JTokenType.Boolean)throw new FormatException(name+": forceExclusive must be boolean");
                            exclusive=(bool)exclusiveToken;
                        }
                        parsed.Add(new FormationSlot(x,z,role,size,ids,numericPreference,types,exclusive));
                    }
                    result.Add(new FormationDefinition(name,team,function,sensitive,scale,parsed.ToArray(),menuOrder));
                }
                return result.ToArray(); // A malformed entry rejects the entire file atomically.
            }
        }
        private static string Text(JObject o,string key)
        {
            if(o[key]?.Type!=JTokenType.String||string.IsNullOrWhiteSpace((string)o[key])) throw new FormatException("missing/invalid "+key);
            return (string)o[key];
        }
        private static float Number(JObject o,string key)
        {
            if(o[key]?.Type!=JTokenType.Integer&&o[key]?.Type!=JTokenType.Float) throw new FormatException("missing/invalid "+key);
            float v=(float)o[key];
            if(float.IsNaN(v)||float.IsInfinity(v)) throw new FormatException("nonfinite "+key);
            return v;
        }
        // Generated from the installed UnitBalance dump; two editor labels retain explicit legacy aliases.
        internal static readonly Dictionary<string,string> UnitIds = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Sol|Scout", "ObjectInfo_Sol_Soldier_Scout" },
            { "Sol|Rifleman", "ObjectInfo_Sol_Soldier_Rifleman" },
            { "Sol|Heavy", "ObjectInfo_Sol_Soldier_Heavy" },
            { "Sol|Sniper", "ObjectInfo_Sol_Soldier_Sniper" },
            { "Sol|Commando", "ObjectInfo_Sol_Soldier_Commando" },
            { "Sol|Light Quad", "ObjectInfo_Sol_Light_LightQuad" },
            { "Sol|Heavy Quad", "ObjectInfo_Sol_Light_HeavyQuad" },
            { "Sol|Light Striker", "ObjectInfo_Sol_Light_LightStriker" },
            { "Sol|Heavy Striker", "ObjectInfo_Sol_Light_HeavyStriker" },
            { "Sol|AA Truck", "ObjectInfo_Sol_Light_AATruck" },
            { "Sol|Repair Rig", "ObjectInfo_Sol_Light_RepairRig" },
            { "Sol|Platoon Hauler", "ObjectInfo_Sol_Light_PlatoonHauler" },
            { "Sol|Hover Tank", "ObjectInfo_Sol_Heavy_HoverTank" },
            { "Sol|Barrage Truck", "ObjectInfo_Sol_Heavy_BarrageTruck" },
            { "Sol|Railgun Tank", "ObjectInfo_Sol_Heavy_RailgunTank" },
            { "Sol|Pulse Truck", "ObjectInfo_Sol_Heavy_PulseTruck" },
            { "Sol|Siege Tank", "ObjectInfo_Sol_UltraHeavy_SiegeTank" },
            { "Sol|Gunship", "ObjectInfo_Sol_Air_Gunship" },
            { "Sol|Dropship", "ObjectInfo_Sol_Air_Dropship" },
            { "Sol|Fighter", "ObjectInfo_Sol_Air_Fighter" },
            { "Sol|Bomber", "ObjectInfo_Sol_Air_Bomber" },
            { "Centauri|Militia", "ObjectInfo_Cent_Soldier_Militia" },
            { "Centauri|Trooper", "ObjectInfo_Cent_Soldier_Trooper" },
            { "Centauri|Marksman", "ObjectInfo_Cent_Soldier_Marksman" },
            { "Centauri|Juggernaut", "ObjectInfo_Cent_Soldier_Juggernaut" },
            { "Centauri|Templar", "ObjectInfo_Cent_Soldier_Templar" },
            { "Centauri|Light Raider", "ObjectInfo_Cent_Light_LightRaider" },
            { "Centauri|Heavy Raider", "ObjectInfo_Cent_Light_HeavyRaider" },
            { "Centauri|Assault Car", "ObjectInfo_Cent_Light_AssaultCar" },
            { "Centauri|Repair Truck", "ObjectInfo_Cent_Light_RepairTruck" },
            { "Centauri|Strike Tank", "ObjectInfo_Cent_Light_StrikeTank" },
            { "Centauri|Squad Transport", "ObjectInfo_Cent_Light_SquadTransport" },
            { "Centauri|Combat Tank", "ObjectInfo_Cent_Heavy_CombatTank" },
            { "Centauri|Heavy Tank", "ObjectInfo_Cent_Heavy_HeavyTank" },
            { "Centauri|Pyro Tank", "ObjectInfo_Cent_Heavy_PyroTank" },
            { "Centauri|Crimson Tank", "ObjectInfo_Cent_UltraHeavy_CrimsonTank" },
            { "Centauri|Dreadnought", "ObjectInfo_Cent_Air_Dreadnought" },
            { "Centauri|Interceptor", "ObjectInfo_Cent_Air_Interceptor" },
            { "Centauri|Shuttle", "ObjectInfo_Cent_Air_Shuttle" },
            { "Centauri|Freighter", "ObjectInfo_Cent_Air_Freighter" },
            { "Alien|Crab", "ObjectInfo_Alien_Crab" },
            { "Alien|Horned Crab", "ObjectInfo_Alien_CrabHorned" },
            { "Alien|Shocker", "ObjectInfo_Alien_Shocker" },
            { "Alien|Wasp", "ObjectInfo_Alien_Wasp" },
            { "Alien|Dragonfly", "ObjectInfo_Alien_Dragonfly" },
            { "Alien|Squid", "ObjectInfo_Alien_Squid" },
            { "Alien|Hunter", "ObjectInfo_Alien_Hunter" },
            { "Alien|Behemoth", "ObjectInfo_Alien_Behemoth" },
            { "Alien|Scorpion", "ObjectInfo_Alien_Scorpion" },
            { "Alien|Firebug", "ObjectInfo_Alien_Firebug" },
            { "Alien|Goliath", "ObjectInfo_Alien_Goliath" },
            { "Alien|Defiler", "ObjectInfo_Alien_Defiler" },
            { "Alien|Colossus", "ObjectInfo_Alien_Colossus" },
            { "Centauri|Flak Truck", "ObjectInfo_Cent_Light_FlakCar" },
            { "Centauri|Rocket Truck", "ObjectInfo_Cent_Heavy_RocketTank" },
        };
    }

    internal static class CommandArgument
    {
        internal static bool TryParse(string args,out string name,out bool quoted)
        {
            name=""; quoted=false;
            string text=(args??"").Trim();
            int space=text.IndexOfAny(new[]{' ','\t'});
            if(space<0) return true;
            text=text.Substring(space).Trim();
            if(text.StartsWith("\""))
            {
                quoted=true;
                try { name=JsonConvert.DeserializeObject<string>(text); return !string.IsNullOrWhiteSpace(name); }
                catch { return false; }
            }
            if(text.Contains("\"")) return false;
            name=text; return true;
        }
    }
}
