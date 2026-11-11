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
        private static MelonPreferences_Entry<bool> enabledPreference,movementFixPreference;
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
            ServerSettings.MovementFix=movementFixPreference.Value;
            ServerSettings.Spacing=LoadTuning("Spacing",DefaultSpacing,0,"Commander click/drag spacing percentage: 0=baseline, 30=3x, 100=10x.");
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
            tuningPreferences["Spacing"].Value=ServerSettings.Spacing;
            tuningPreferences["Rally"].Value=ServerSettings.Rally;
            tuningPreferences["SizeInfluence"].Value=ServerSettings.SizeInfluence;
            tuningPreferences["AlienSpread"].Value=ServerSettings.AlienSpread;
            tuningPreferences["FpsSpacing"].Value=ServerSettings.FpsSpacing;
            tuningPreferences["FpsDistance"].Value=ServerSettings.FpsDistance;
            tuningPreferences["FpsAllowance"].Value=ServerSettings.FpsAllowance;
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
    }
}
