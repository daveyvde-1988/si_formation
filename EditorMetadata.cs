using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Si_Formation
{
    internal static class EditorCategories
    {
        internal static readonly HashSet<string> Names=new HashSet<string>(new[]{"Infantry","Cavalry","Tank","Siege","Aircraft","Transport","Special","Repair","Anti-air","Scout","Artillery","Harvester"},StringComparer.Ordinal);
        private static readonly Dictionary<string,string> Overrides=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        static EditorCategories()
        {
            Add("Anti-air","AA Truck","Flak Truck");
            Add("Scout","Light Quad","Heavy Quad","Light Raider","Heavy Raider");
            Add("Cavalry","Light Striker","Heavy Striker","Strike Tank","Assault Car","Scorpion");
            Add("Tank","Hover Tank","Railgun Tank","Combat Tank","Heavy Tank","Behemoth");
            Add("Siege","Siege Tank","Crimson Tank","Goliath");
            Add("Artillery","Barrage Truck","Rocket Truck");Add("Special","Pulse Truck","Pyro Tank");
            Add("Transport","Platoon Hauler","Squad Transport");Add("Repair","Repair Rig","Repair Truck");
        }
        private static void Add(string category,params string[] labels)
        {
            foreach(var entry in FormationCatalog.UnitIds)
                if(labels.Contains(entry.Key.Substring(entry.Key.IndexOf('|')+1)))Overrides[entry.Value]=category;
        }
        internal static string For(ObjectInfo info)=>info?(Overrides.TryGetValue(info.name,out string category)?category:info.UnitType.ToString()):"None";
    }
    public sealed partial class Formations
    {
        private static bool footprintsExported;
        private static void ExportEditorFootprints()
        {
            if(footprintsExported||!Game.GetIsServer()||!ObjectInfo.AllInitialized)return;
            footprintsExported=true;
            try
            {
                var units=new Dictionary<string,object>();
                var infos=ObjectInfo.ObjectDatas;
                foreach(var entry in FormationCatalog.UnitIds)
                {
                    var info=infos.FirstOrDefault(i=>i&&i.name==entry.Value);
                    if(!info)continue;
                    var size=info.PhysicalBounds.size;
                    if(size.x<=0||size.z<=0||float.IsNaN(size.x)||float.IsNaN(size.z)||float.IsInfinity(size.x)||float.IsInfinity(size.z))continue;
                    units[entry.Key]=new {width=size.x,length=size.z,category=EditorCategories.For(info)};
                }
                if(units.Count==0){MelonLoader.MelonLogger.Warning("No initialized physical footprints available for editor export.");return;}
                string directory=Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory,"Formations_cfg","metadata");
                Directory.CreateDirectory(directory);
                string file=Path.Combine(directory,"footprints.json");
                File.WriteAllText(file,JsonConvert.SerializeObject(new {format="silica-unit-footprints",version=1,source="ObjectInfo.PhysicalBounds (local prefab bounds, metres)",units},Formatting.Indented));
                MelonLoader.MelonLogger.Msg("Exported "+units.Count+" physical footprints for formation editor: "+file);
            }
            catch(Exception e){MelonLoader.MelonLogger.Warning("Editor footprint export: "+e.Message);}
        }
    }
}
