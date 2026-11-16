using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using SilicaAdminMod;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static MelonPreferences_Category serverPreferences;
        private static MelonPreferences_Entry<bool> enabledPreference,movementFixPreference,destinationLocalCohesionPreference;
        private static bool DestinationLocalCohesionEnabled;
        private static MelonPreferences_Entry<float> humanWaypointWaitPreference,alienWaypointWaitPreference,alienCombatWaitPreference;
        private static float HumanWaypointWait=DefaultHumanWaypointWait,AlienWaypointWait=DefaultAlienWaypointWait,AlienCombatWait=DefaultAlienCombatWait;
        private static readonly Dictionary<string,MelonPreferences_Entry<int>> tuningPreferences=new Dictionary<string,MelonPreferences_Entry<int>>();
        private static bool ServerEnabled=>enabledPreference?.Value??true;

        private static void LoadServerSettings()
        {
            string directory=Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory,"cfg","Formation");
            Directory.CreateDirectory(directory);
            serverPreferences=MelonPreferences.CreateCategory("Si_Formation_Native");
            serverPreferences.SetFilePath(Path.Combine(directory,"si_formation_native.cfg"));
            enabledPreference=serverPreferences.CreateEntry("Enabled",true,description:"Server-wide formation tuning master switch.");
            movementFixPreference=serverPreferences.CreateEntry("MovementFix",DefaultMovementFix,description:"New grouped move/attack orders use each unit's own speed without cohesion slowdown.");
            destinationLocalCohesionPreference=serverPreferences.CreateEntry("DestinationLocalCohesion",DefaultDestinationLocalCohesion,
                description:"Enable the destination-local cohesion fallback for scattered or oversized selections. Uses each faction's DestinationRadius (600m by default); ON by default.");
            DestinationLocalCohesionEnabled=destinationLocalCohesionPreference.Value;
            // A sentinel distinguishes an older saved near-wait value from a
            // fresh configuration with independently tuned faction defaults.
            var previousWaitPreference=serverPreferences.CreateEntry("WaypointGraceNear",-1f);
            bool migrateWait=previousWaitPreference.Value!=-1;
            float previousWait=migrateWait?ReadWaypointGrace(previousWaitPreference,DefaultHumanWaypointWait):DefaultHumanWaypointWait;
            previousWaitPreference.Value=previousWait;
            humanWaypointWaitPreference=serverPreferences.CreateEntry("HumanWaypointWait",migrateWait?previousWait:DefaultHumanWaypointWait,description:"Human checkpoint wait after shared ETA expires. Seconds, 0..120.");
            alienWaypointWaitPreference=serverPreferences.CreateEntry("AlienWaypointWait",migrateWait?previousWait:DefaultAlienWaypointWait,description:"Alien checkpoint wait after shared ETA expires. Seconds, 0..120.");
            alienCombatWaitPreference=serverPreferences.CreateEntry("AlienCombatWait",DefaultAlienCombatWait,description:"Additional bounded Alien checkpoint wait when an included member engages in combat. Applied at most once per waypoint. Seconds, 0..120.");
            HumanWaypointWait=ReadWaypointGrace(humanWaypointWaitPreference,DefaultHumanWaypointWait);
            AlienWaypointWait=ReadWaypointGrace(alienWaypointWaitPreference,DefaultAlienWaypointWait);
            AlienCombatWait=ReadWaypointGrace(alienCombatWaitPreference,DefaultAlienCombatWait);
            ServerSettings.MovementFix=movementFixPreference.Value;
            ServerSettings.Spacing=LoadTuning("Spacing",DefaultSpacing,0,"Commander single-click spacing percentage: 0=baseline, 30=3x, 100=10x.");
            ServerSettings.DragSpacing=LoadTuning("DragSpacing",DefaultDragSpacing,0,"Commander click-and-drag spacing percentage: 0=compact baseline, 30=3x, 100=10x. Drag length sets frontage; physical clearance is retained.");
            ServerSettings.Rally=LoadTuning("Rally",DefaultRally,0,"Production rally spacing percentage: 0=baseline, 30=3x, 100=10x; physical clearance is retained.");
            ServerSettings.SizeInfluence=LoadTuning("SizeInfluence",DefaultSizeInfluence,0,"Large-unit footprint influence, 0..100 percent.");
            ServerSettings.AlienSpread=LoadTuning("AlienSpread",DefaultAlienSpread,0,"Alien spacing scale, 0..100 percent; physical clearance is retained.");
            ServerSettings.FpsSpacing=LoadTuning("FpsSpacing",DefaultFpsSpacing,0,"FPS unit spacing percentage: 0=baseline, 30=3x, 100=10x.");
            ServerSettings.FpsDistance=LoadTuning("FpsDistance",DefaultFpsDistance,0,"FPS distance-to-player scale, 0..100 percent. 10=1.667x the 5m plus player-radius baseline; physical clearance is retained.");
            ServerSettings.FpsAllowance=LoadTuning("FpsAllowance",DefaultFpsAllowance,1,"FPS movement allowance, 1..100 metres; convoy threshold is twice this value, with a 10m minimum.");
        }
        private static int LoadTuning(string name,int defaultValue,int minimum,string description)
        {
            var entry=serverPreferences.CreateEntry(name,defaultValue,description:description);
            tuningPreferences[name]=entry;
            if(entry.Value>=minimum&&entry.Value<=100)return entry.Value;
            MelonLogger.Warning("Formation setting "+name+" must be "+minimum+"..100; using "+defaultValue+".");
            return defaultValue;
        }
        private static bool DemandAdmin(Player p)
        {
            if(!ready||!Game.GetIsServer()||!p)return false;
            if(p.IsAdmin()&&AdminMethods.PowerInPowers(Power.Generic,p.GetAdminPowers()))return true;
            Reply(p,"permission denied: admin Generic power required.");
            return false;
        }
        private static bool SaveSettings(Player p)
        {
            if(!DemandAdmin(p))return false;
            movementFixPreference.Value=ServerSettings.MovementFix;
            destinationLocalCohesionPreference.Value=DestinationLocalCohesionEnabled;
            humanWaypointWaitPreference.Value=HumanWaypointWait;
            alienWaypointWaitPreference.Value=AlienWaypointWait;
            alienCombatWaitPreference.Value=AlienCombatWait;
            tuningPreferences["Spacing"].Value=ServerSettings.Spacing;
            tuningPreferences["DragSpacing"].Value=ServerSettings.DragSpacing;
            tuningPreferences["Rally"].Value=ServerSettings.Rally;
            tuningPreferences["SizeInfluence"].Value=ServerSettings.SizeInfluence;
            tuningPreferences["AlienSpread"].Value=ServerSettings.AlienSpread;
            tuningPreferences["FpsSpacing"].Value=ServerSettings.FpsSpacing;
            tuningPreferences["FpsDistance"].Value=ServerSettings.FpsDistance;
            tuningPreferences["FpsAllowance"].Value=ServerSettings.FpsAllowance;
            SaveCohesionSettings();
            SaveTravelCoverSettings();
            try{serverPreferences.SaveToFile(false);return true;}
            catch(Exception e)
            {
                MelonLogger.Error("Saving formation settings: "+e);
                Reply(p,"Server settings updated; saving failed, see server log.");
                return false;
            }
        }
        private static bool SetServerEnabled(bool enabled,Player p)
        {
            if(!DemandAdmin(p))return false;
            enabledPreference.Value=enabled;
            return SaveSettings(p);
        }
        private static float ReadWaypointGrace(MelonPreferences_Entry<float> preference,float fallback)
        {
            float value=preference.Value;
            if(float.IsNaN(value)||float.IsInfinity(value)||value<0||value>120)
            {MelonLogger.Warning("Invalid waypoint grace; using "+fallback+" seconds.");value=fallback;}
            preference.Value=value;return value;
        }
        private static void WaypointWaitCommand(Player p,string args)
        {
            if(!ready||!Game.GetIsServer()||!p)return;
            string[] tokens=Argument(args).Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
            bool saved=true;
            if(tokens.Length>0&&tokens[0]!="status")
            {
                if(!DemandAdmin(p))return;
                if(tokens.Length!=2||(tokens[0]!="human"&&tokens[0]!="alien"&&tokens[0]!="combat"&&tokens[0]!="near"&&tokens[0]!="far")||
                    !float.TryParse(tokens[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var seconds)||
                    float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0||seconds>120)
                {Reply(p,"/waypointwait human|alien|combat <0..120 seconds> | status");return;}
                switch(tokens[0])
                {
                    case "human": HumanWaypointWait=seconds; break;
                    case "alien": AlienWaypointWait=seconds; break;
                    case "combat": AlienCombatWait=seconds; break;
                    default: HumanWaypointWait=seconds;AlienWaypointWait=seconds;break;
                }
                saved=SaveSettings(p);
            }
            Reply(p,"Overdue checkpoint wait: Human "+CohesionNumber(HumanWaypointWait)+"s, Alien "+CohesionNumber(AlienWaypointWait)+
                "s; Alien combat extension "+CohesionNumber(AlienCombatWait)+"s; main-body cutoff 1000m, orders cancelled after 300s outside. New waypoints capture these settings."+(saved?"":" Save failed; see server log."));
        }
        private static void DestinationCohesionCommand(Player p,string args)
        {
            if(!ready||!Game.GetIsServer()||!p)return;
            string value=Argument(args);bool saved=true;
            if(value=="on"||value=="off")
            {
                if(!DemandAdmin(p))return;
                DestinationLocalCohesionEnabled=value=="on";
                if(!DestinationLocalCohesionEnabled)
                    foreach(var group in CohesionGroups)
                        if(group.DestinationLocal&&group.Phase!=CohesionPhase.Released&&group.Phase!=CohesionPhase.Complete)
                            ReleaseCohesion(group,"destination-local cohesion switched off",false);
                saved=SaveSettings(p);
            }
            else if(value!=""&&value!="status"){Reply(p,"/destinationcohesion on|off|status");return;}
            Reply(p,"Nearby destination cohesion "+(DestinationLocalCohesionEnabled?"ON":"OFF")+
                "; Human radius "+CohesionNumber(CohesionFactionValue(false,"DestinationRadius"))+"m, Alien radius "+
                CohesionNumber(CohesionFactionValue(true,"DestinationRadius"))+"m. Applies to scattered/oversized selections."+
                (saved?"":" Save failed; see server log."));
        }
    }
}
