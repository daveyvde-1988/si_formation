using System.Collections.Generic;
using HarmonyLib;
using Silica.AI;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        // Run before Formation and UnitAI capture a selection or change order
        // ownership. A per-unit rejection alone is too late for native spacing
        // and UnitCohesionGroup.Detach in Move and Patrol.
        private const int CommanderSquadProtectionPriority=2000;

        private void InstallCommanderSquadProtection()
        {
            foreach(var method in new[]{AccessTools.Method(typeof(StrategyMode),"PerformMoveAttack"),
                AccessTools.Method(typeof(StrategyMode),"PerformPatrol"),
                AccessTools.Method(typeof(Silica.OrderTargetingController),"IssueToObjects")})
                HarmonyInstance.Patch(method,prefix:new HarmonyMethod(typeof(Formations),nameof(FilterCommanderSquads))
                    {priority=CommanderSquadProtectionPriority});
            HarmonyInstance.Patch(AccessTools.Method(typeof(StrategyMode),"IssueFormationOrder"),
                prefix:new HarmonyMethod(typeof(Formations),nameof(ProtectSquadFormationOrder))
                    {priority=CommanderSquadProtectionPriority});
            HarmonyInstance.Patch(AccessTools.Method(typeof(AIOrderProcessor),"IssueOrder"),
                prefix:new HarmonyMethod(typeof(Formations),nameof(ProtectSquadOrder))
                    {priority=CommanderSquadProtectionPriority});
        }

        private static Player SquadCommandSender()
        {
            if(!Active||!Game.GetIsServer())return null;
            var sender=NetworkLayer.GetRequestSender();
            return sender&&sender.IsCommander?sender:null;
        }

        private static bool IsPlayerSquadMember(BaseGameObject obj,Player commander)
        {
            if(!(obj is Unit unit)||!unit||unit.IsDestroyed||unit.Team!=commander.Team)return false;
            var group=unit.AIGroup;
            if(group==null)return false;
            // Native membership is authoritative, including stopped/ordered
            // squads. Formation's follow/task/slot ownership is more transient.
            foreach(var player in Player.Players)
                if(player&&!player.IsCommander&&player.Team==unit.Team&&player.Group==group)return true;
            return false;
        }

        private static void FilterCommanderSquads(ref IReadOnlyList<BaseGameObject> objects)
        {
            var commander=SquadCommandSender();
            if(!commander||objects==null)return;
            List<BaseGameObject> filtered=null;
            for(int i=0;i<objects.Count;i++)
            {
                if(IsPlayerSquadMember(objects[i],commander))
                {
                    if(filtered==null)
                    {
                        filtered=new List<BaseGameObject>(objects.Count);
                        for(int j=0;j<i;j++)filtered.Add(objects[j]);
                    }
                }
                else if(filtered!=null)filtered.Add(objects[i]);
            }
            if(filtered!=null)objects=filtered;
        }

        private static bool ProtectSquadFormationOrder(BaseGameObject obj,ref bool __result)
        {
            var commander=SquadCommandSender();
            if(!commander||!IsPlayerSquadMember(obj,commander))return true;
            __result=false;return false;
        }

        private static bool ProtectSquadOrder(AIOrderProcessor __instance,OrderIssueParams __2,ref bool __result)
        {
            if(__2.CalledByAI||!(__instance.Owner is Unit unit)||FpsIssuing.Contains(unit)||
                restoringWaypointOrders||issuingUnit==unit)return true;
            var commander=SquadCommandSender();
            if(!commander||!IsPlayerSquadMember(unit,commander))return true;
            __result=false;return false;
        }
    }
}
