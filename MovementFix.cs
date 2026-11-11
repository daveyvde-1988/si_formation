using HarmonyLib;
using Silica.AI;
using System.Collections.Generic;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private void InstallMovementFix()
        {
            HarmonyInstance.Patch(AccessTools.Method(typeof(StrategyMode),"IssueFormationOrder"),
                prefix:new HarmonyMethod(typeof(Formations),nameof(IndependentSpeed)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(UnitCohesionGroup),"Create"),
                prefix:new HarmonyMethod(typeof(Formations),nameof(IndependentCohesion)));
        }
        private static void IndependentSpeed(ref float speedScale)
        {
            // Scoped to native grouped commander dispatch; other speed limits stay native.
            if(Active&&Game.GetIsServer()&&activeIndependent)speedScale=1;
        }
        private static bool IndependentCohesion(List<Unit> units,List<Vector3> slotTargets,ref float spacing,ref UnitCohesionGroup __result)
        {
            if(Active&&Game.GetIsServer()&&activeLayout!=null)
            {
                for(int i=0;i<System.Math.Min(units.Count,slotTargets.Count);i++)if(activeLayout.Points.TryGetValue(units[i],out var point))slotTargets[i]=point;
                if(activeLayout.Pitch>0)spacing=activeLayout.Pitch;
            }
            if(!Active||!Game.GetIsServer()||!activeIndependent)return true;
            __result=null;return false;
        }
        private static string Argument(string args)
        {
            string text=(args??"").Trim();int split=text.IndexOfAny(new[]{' ','\t'});
            return split<0?"":text.Substring(split).Trim().ToLowerInvariant();
        }
        private static void MovementFixCommand(Player p,string args)
        {
            if(!ready||!Game.GetIsServer()||!p)return;
            string value=Argument(args);var settings=ServerSettings;
            if(value=="on"||value=="off")
            {
                if(!DemandAdmin(p))return;
                settings.MovementFix=value=="on";
                if(!SaveSettings(p))return;
            }
            else if(value!=""&&value!="status"){Reply(p,"/movementfix on|off|status");return;}
            Reply(p,"Movement fix "+(settings.MovementFix?"ON":"OFF")+"; applies to new grouped move/attack orders. Explicit movement modes and physical obstacles still apply.");
        }
    }
}
