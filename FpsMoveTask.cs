using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class FpsMoveTask
        {
            internal Player Owner;
            internal AITaskBase Task;
            internal readonly HashSet<Unit> Units=new HashSet<Unit>();
        }
        private static readonly Dictionary<AIGroup,FpsMoveTask> FpsMoveTasks=new Dictionary<AIGroup,FpsMoveTask>();
        private static void ClearFpsMoveTask(Player player)
        {
            foreach(var entry in FpsMoveTasks.Where(e=>e.Value.Owner==player).ToArray())
            {
                FpsMoveTasks.Remove(entry.Key);
                if(entry.Key.Task!=entry.Value.Task)continue;
                generatedDepth++;
                try {entry.Key.Task=null;}
                finally {generatedDepth--;}
            }
        }
        private static void ClearFpsMoveTasks()
        {
            foreach(var p in FpsMoveTasks.Values.Select(v=>v.Owner).Distinct().ToArray())ClearFpsMoveTask(p);
        }
        private static IEnumerable<CodeInstruction> PatchFpsMoveTask(IEnumerable<CodeInstruction> instructions)
        {
            int count=0;
            var original=AccessTools.Method(typeof(AIOrderProcessor),"IssueResolvedOrder");
            foreach(var i in instructions)
            {
                if(i.Calls(original))
                {i.opcode=OpCodes.Call;i.operand=AccessTools.Method(typeof(Formations),nameof(FpsTaskMove));count++;}
                yield return i;
            }
            if(count!=2)throw new InvalidOperationException("Expected two FPS group-task repath sites.");
        }
        private static bool FpsTaskMove(AIOrderProcessor processor,Vector3 position,Target target,in OrderIssueParams parameters)
        {
            // The native task supplies the networked FPS diamond. Individual orders own
            // these members' movement, including after a manual override of their slots.
            if(Running&&processor.Owner is Unit u&&u.AIGroup!=null&&
                FpsMoveTasks.TryGetValue(u.AIGroup,out var marker)&&marker.Task==u.AIGroup.Task&&
                marker.Owner&&marker.Owner.Group==u.AIGroup&&marker.Units.Contains(u))return true;
            return processor.IssueResolvedOrder(position,target,in parameters);
        }
        private static void FollowOrientationCommand(Player p,string args)
        {
            if(!Ready||!Game.GetIsServer()||!p||p.IsCommander||!p.ControlledUnit||p.Group==null||p.Group.Leader!=p.ControlledUnit)
            {Reply(p,"followformation orientation is only available to FPS squad leaders.");return;}
            if(networkSender&&networkSender!=p)return;
            if(!CommandArgument.TryParse(args,out string action,out bool quoted)||quoted||
                !new[]{"lock","unlock","status"}.Contains(action,StringComparer.OrdinalIgnoreCase))
            {Reply(p,"usage: /followformation lock|unlock|status");return;}
            var selection=For(p);
            if(!action.Equals("status",StringComparison.OrdinalIgnoreCase))
            {
                selection.FollowFacingLock=action.Equals("lock",StringComparison.OrdinalIgnoreCase);
                if(!selection.FollowFacingLock)ClearFpsSpeedCaps(p);
                Menus.Remove(p);
            }
            Reply(p,selection.FollowFacingLock?
                "Formation lock ON: Move, Follow and Attack staging coordinate speed. Follow captures your facing at movement updates.":
                "Formation lock OFF: Follow uses squad travel direction and the normal moving-slot system.");
        }
    }
}
