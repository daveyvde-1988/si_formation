using UnityEngine;

namespace Si_Formation
{
    internal static class TerrainProjection
    {
        internal static bool TryProject(Unit unit, Vector3 desired, out Vector3 point)
        {
            point = Vector3.zero;
            if (!unit)
                return false;

            Terrain terrain = Game.MainTerrain;
            if (!terrain || !terrain.terrainData)
                return false;

            Bounds bounds = new Bounds(
                terrain.transform.position + terrain.terrainData.bounds.center,
                terrain.terrainData.bounds.size);
            if (desired.x < bounds.min.x || desired.x > bounds.max.x ||
                desired.z < bounds.min.z || desired.z > bounds.max.z)
                return false;

            Vector3 projected = GamePhysics.GetTerrainPosition(desired, 0f, terrain);
            if (float.IsNaN(projected.y) || float.IsInfinity(projected.y))
                return false;

            // Navigation height is authoritative; terrain provides the local height seed.
            // Keep lateral snapping tight, while permitting terrain/nav-surface differences.
            return GameAI.GetPointValidForAI(projected,
                unit.AIAgent.AgentPathfinding.PathfindingSeeker.graphMask,
                out point, 75f, 5f, 30f);
        }
    }
}
