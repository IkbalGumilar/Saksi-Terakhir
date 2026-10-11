using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.Navigation
{
    /// <summary>Finds a short physical detour around a railing inside an already owned navigation link.</summary>
    internal static class SupportedLinkDetour
    {
        private static readonly float[] Offsets = { 0.35f, 0.5f, 0.7f, 1.25f, 1.8f, 2.35f };

        internal static bool TryBuild(Transform actor, Vector3 start, Vector3 end,
            float radius, float height, float climb, float slope, Predicate<Collider> ignore,
            Predicate<Vector3> allowedGround, List<Vector3> result)
        {
            result.Clear();
            Vector3 origin = actor.position;
            if (!TerrainNavigationProbe.TryGetLinkGroundPosition(actor, end, climb, slope,
                radius, out end, ignore) || allowedGround != null && !allowedGround(end)) return false;
            if (TerrainNavigationProbe.TryGetSupportedSegment(actor, origin, end, radius, height,
                climb, slope, out Vector3 endpoint, ignore, allowedGround, true))
            { result.Add(endpoint); return true; }

            // Both sides and the source landing are included. A capsule beside a
            // jamb may have to retreat before it has enough room to turn sideways.
            var nodes = new List<Vector3> { origin, end };
            Vector3 along = Vector3.ProjectOnPlane(end - start, Vector3.up).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, along);
            foreach (Vector3 anchor in new[] { start, (start + end) * 0.5f, end })
                foreach (float offset in Offsets)
                    foreach (int side in new[] { -1, 1 })
                        AddGrounded(anchor + across * (offset * side));
            AddGrounded(start - along * 0.7f);
            AddGrounded(end + along * 0.7f);

            int count = nodes.Count;
            var costs = new float[count];
            var previous = new int[count];
            var visited = new bool[count];
            for (int i = 0; i < count; i++) { costs[i] = float.PositiveInfinity; previous[i] = -1; }
            costs[0] = 0f;
            for (int attempt = 0; attempt < count; attempt++)
            {
                int current = -1;
                for (int i = 0; i < count; i++)
                    if (!visited[i] && (current < 0 || costs[i] < costs[current])) current = i;
                if (current < 0 || float.IsInfinity(costs[current])) break;
                if (current == 1)
                {
                    for (int i = 1; i != 0; i = previous[i]) result.Add(nodes[i]);
                    result.Reverse();
                    return true;
                }
                visited[current] = true;
                for (int next = 1; next < count; next++)
                {
                    if (visited[next]) continue;
                    float cost = costs[current] + Vector3.Distance(nodes[current], nodes[next]);
                    if (cost >= costs[next]) continue;
                    if (!TerrainNavigationProbe.TryGetSupportedSegment(actor, nodes[current], nodes[next],
                        radius, height, climb, slope, out Vector3 ground, ignore, allowedGround, true)) continue;
                    // A steep projection must not join a different deck at this node.
                    if (Mathf.Abs(ground.y - nodes[next].y) > 0.08f) continue;
                    costs[next] = cost;
                    previous[next] = current;
                }
            }
            return false;

            void AddGrounded(Vector3 requested)
            {
                if (!TerrainNavigationProbe.TryGetLinkGroundPosition(actor, requested, climb, slope,
                    radius, out Vector3 ground, ignore) || allowedGround != null && !allowedGround(ground)) return;
                // Fifteen centimetres can decide whether a capsule clears a jamb.
                // Merge only actual duplicates, not neighbouring narrow-route options.
                foreach (Vector3 node in nodes) if ((node - ground).sqrMagnitude < 0.0001f) return;
                nodes.Add(ground);
            }
        }
    }
}
