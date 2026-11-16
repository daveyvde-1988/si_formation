using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Pathfinding;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private const float NativePathfindingShortcutDistance = 200f;
        private static volatile float effectivePathfindingShortcutDistance = NativePathfindingShortcutDistance;

        private void InstallPathfindingShortcut()
        {
            var target = AccessTools.Method(typeof(AIPathfinding), "RemoveOpenCorners", new[]
            {
                typeof(NavmeshBase), typeof(List<Vector3>), typeof(TraversalConstraint).MakeByRefType()
            });
            if (target == null || !target.IsStatic || target.ReturnType != typeof(void))
                throw new MissingMethodException("Native AIPathfinding.RemoveOpenCorners signature is unavailable.");

            HarmonyInstance.Patch(target,
                transpiler: new HarmonyMethod(typeof(Formations), nameof(PathfindingShortcutTranspiler)));
        }

        // Publish on the main thread. The native edge-offset job also runs on path workers.
        private static void RefreshPathfindingShortcutDistance(bool isServer)
        {
            float distance = isServer && Active ? PathfindingShortcutDistance : NativePathfindingShortcutDistance;
            effectivePathfindingShortcutDistance = distance > 0f ? distance : -1f;
        }

        private static float GetPathfindingShortcutDistance()
        {
            return effectivePathfindingShortcutDistance;
        }

        private static IEnumerable<CodeInstruction> PathfindingShortcutTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var distanceMethod = AccessTools.Method(typeof(GameMath), nameof(GameMath.Distance2D),
                new[] { typeof(Vector3), typeof(Vector3) });
            var configuredDistance = AccessTools.Method(typeof(Formations), nameof(GetPathfindingShortcutDistance));
            if (distanceMethod == null || configuredDistance == null)
                throw new MissingMethodException("Pathfinding shortcut distance methods are unavailable.");

            int matchCount = 0;
            int matchIndex = -1;

            for (int i = 1; i < code.Count - 1; i++)
            {
                if (code[i].opcode == OpCodes.Ldc_R4 && code[i].operand is float distance &&
                    distance == NativePathfindingShortcutDistance && code[i - 1].Calls(distanceMethod) &&
                    (code[i + 1].opcode == OpCodes.Bgt || code[i + 1].opcode == OpCodes.Bgt_S))
                {
                    matchCount++;
                    matchIndex = i;
                }
            }

            if (matchCount != 1)
                throw new InvalidOperationException("Native pathfinding shortcut distance guard was not uniquely recognized.");

            // Retain labels and exception blocks on the replaced constant.
            code[matchIndex].opcode = OpCodes.Call;
            code[matchIndex].operand = configuredDistance;
            return code;
        }
    }
}
