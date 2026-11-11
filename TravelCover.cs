using System;
using HarmonyLib;
using MelonLoader;
using Silica.AI;
using SilicaAdminMod;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static MelonPreferences_Entry<bool> travelCoverPreference;
        private static bool TravelCoverPreferenceDisabler=true;

        private static void LoadTravelCoverSettings()
        {
            if(serverPreferences==null)throw new InvalidOperationException("Load server settings before travel cover settings.");
            travelCoverPreference=serverPreferences.CreateEntry("TravelCoverPreferenceDisabler",true,
                description:"Disable automatic cover-point substitution in infantry long-range paths. ON disables travel cover preference; combat cover decisions remain native.");
            TravelCoverPreferenceDisabler=travelCoverPreference.Value;
        }
        private static void SaveTravelCoverSettings()
        {
            if(travelCoverPreference!=null)travelCoverPreference.Value=TravelCoverPreferenceDisabler;
        }
        private void InstallTravelCover()
        {
            var target=AccessTools.Method(typeof(AISoldierAgent),nameof(AISoldierAgent.ProcessLongRangePath));
            var baseline=AccessTools.Field(typeof(AISoldierAgent),"m_longRangeAreaSideSize");
            if(target==null||baseline==null||baseline.FieldType!=typeof(float))
                throw new InvalidOperationException("Native infantry travel-cover path hook or movement-area baseline is unavailable.");
            HarmonyInstance.Patch(target,prefix:new HarmonyMethod(typeof(Formations),nameof(DisableTravelCoverPreference)));
            AdminMethods.RegisterAdminCommand("travelcoverdisabler",TravelCoverCommand,Power.Generic,
                "Travel cover preference disabler: on disables infantry travel cover; off restores native behavior; status.");
        }
        private static bool DisableTravelCoverPreference(AISoldierAgent __instance,float ___m_longRangeAreaSideSize)
        {
            if(!Active||!Game.GetIsServer()||!TravelCoverPreferenceDisabler)return true;
            // Preserve the native first statement, including the Stealth completion
            // area. The remaining method only substitutes/prunes long-range cover
            // waypoints. Combat cover selection, claims and behavior trees are not patched.
            __instance.AgentPathfinding.LongRangeAreaSideSize=__instance.MoveSpeed==AgentMoveSpeed.Stealth?
                ___m_longRangeAreaSideSize/2f:___m_longRangeAreaSideSize;
            return false;
        }
        private static void TravelCoverCommand(Player player,string args)
        {
            if(!ready||!Game.GetIsServer()||!player)return;
            string value=Argument(args);bool saved=true;
            if(value=="on"||value=="off")
            {
                if(!DemandAdmin(player))return;
                TravelCoverPreferenceDisabler=value=="on";
                SaveTravelCoverSettings();
                saved=SaveSettings(player);
            }
            else if(value!=""&&value!="status")
            {Reply(player,"/travelcoverdisabler on|off|status; ON disables infantry travel cover preference.");return;}
            Reply(player,"Travel cover preference disabler "+(TravelCoverPreferenceDisabler?"ON":"OFF")+
                "; effective infantry travel cover preference "+(Active&&TravelCoverPreferenceDisabler?"DISABLED.":"NATIVE.")+
                " Applies to new/recalculated long-range paths; combat cover decisions remain native."+(saved?"":" Save failed; see server log."));
        }
    }
}
