using UnityEngine;

namespace SaksiTerakhir.Player
{
    /// <summary>Chooses a temporary, supported menu view using visible obstruction coverage.</summary>
    internal static class MenuViewPlanner
    {
        internal readonly struct View
        {
            internal readonly Vector3 Position;
            internal readonly float Yaw;
            internal readonly float ScanAngle;

            internal View(Vector3 position, float yaw, float scanAngle)
            { Position = position; Yaw = yaw; ScanAngle = scanAngle; }
        }

        private readonly struct Coverage
        {
            internal readonly float Score;
            internal readonly float NearFraction;
            internal readonly float CenterDistance;

            internal Coverage(float score, float nearFraction, float centerDistance)
            { Score = score; NearFraction = nearFraction; CenterDistance = centerDistance; }
        }

        private const float FarDistance = 16f;
        private const float ComfortableDistance = 3f;
        private const float CloseDistance = 1.25f;
        private const float MaximumFloorChange = 0.15f;
        // Invisible Ignore Raycast quest barriers still physically block relocation.
        private const int MovementLayers = ~0;
        private static readonly RaycastHit[] RayHits = new RaycastHit[32];
        private static readonly Collider[] Overlaps = new Collider[64];
        private static readonly float[] RowHeights = { -0.4f, 0f, 0.3f };
        private static readonly float[] RowWeights = { 0.35f, 1f, 0.8f };
        private static readonly float[] ColumnWeights = { 1f, 1.2f, 1.6f, 2f, 1.6f, 1.2f, 1f };

        internal static View Choose(CharacterController body, Transform eye, float initialYaw,
            float horizontalFieldOfView, float requestedScanAngle, bool allowRelocation)
        {
            Transform root = body.transform;
            Vector3 original = root.position;
            Vector3 eyeOffset = Quaternion.Inverse(Quaternion.Euler(0f, initialYaw, 0f)) * (eye.position - original);
            float halfFan = Mathf.Clamp(horizontalFieldOfView * 0.42f, 22f, 42f);
            Vector3 bestPosition = original;
            float bestYaw = initialYaw;
            Coverage best = BestHeading(root, original, eyeOffset, initialYaw, halfFan, out bestYaw);
            float bestScore = best.Score;

            // A nearby narrow post should not trigger relocation. Broad close coverage or a
            // cramped view in every direction can benefit from a small supported adjustment.
            if (allowRelocation && (best.NearFraction > 0.28f || best.Score < 0.72f || best.CenterDistance < 1.4f))
            {
                for (int ring = 1; ring <= 2; ring++)
                {
                    float distance = ring * 0.45f;
                    for (int direction = 0; direction < 8; direction++)
                    {
                        Vector3 offset = Quaternion.Euler(0f, direction * 45f, 0f) * Vector3.forward * distance;
                        Vector3 candidate = original + offset;
                        if (!TrySupportedPosition(body, original, ref candidate) || !ClearPath(body, original, candidate)) continue;
                        Coverage coverage = BestHeading(root, candidate, eyeOffset, initialYaw, halfFan, out float yaw);
                        float score = coverage.Score - distance * 0.035f;
                        if (score <= bestScore + 0.075f) continue;
                        bestScore = score;
                        best = coverage;
                        bestPosition = candidate;
                        bestYaw = yaw;
                    }
                }
            }

            float scan = Mathf.Clamp(requestedScanAngle, 0f, 10f);
            // Validate both limits of the scan, rather than letting an otherwise open heading
            // slowly turn back into a nearby wall. The frozen menu world needs no repeated search.
            while (scan > 0.25f)
            {
                Coverage left = Measure(root, bestPosition, eyeOffset, bestYaw - scan, halfFan);
                Coverage right = Measure(root, bestPosition, eyeOffset, bestYaw + scan, halfFan);
                if (left.CenterDistance > 0.6f && right.CenterDistance > 0.6f
                    && left.Score >= best.Score - 0.12f && right.Score >= best.Score - 0.12f) break;
                scan *= 0.5f;
            }
            if (scan <= 0.25f) scan = 0f;
            return new View(bestPosition, bestYaw, scan);
        }

        private static Coverage BestHeading(Transform root, Vector3 position, Vector3 eyeOffset,
            float initialYaw, float halfFan, out float yaw)
        {
            yaw = initialYaw;
            Coverage best = Measure(root, position, eyeOffset, yaw, halfFan);
            float bestScore = best.Score;
            // Search both sides in increasing turn order, keeping the existing view on ties.
            for (int step = 1; step <= 12; step++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    if (step == 12 && sign > 0) continue;
                    float candidateYaw = initialYaw + step * 15f * sign;
                    Coverage candidate = Measure(root, position, eyeOffset, candidateYaw, halfFan);
                    float score = candidate.Score - step * 15f * 0.00015f;
                    if (score <= bestScore + 0.002f) continue;
                    best = candidate;
                    bestScore = score;
                    yaw = candidateYaw;
                }
            }
            // A thin post may need only a few degrees of adjustment. Refining the best
            // coarse heading avoids turning an otherwise open view toward a side wall.
            float coarseYaw = yaw;
            for (int step = -5; step <= 5; step++)
            {
                if (step == 0) continue;
                float candidateYaw = coarseYaw + step * 2.5f;
                Coverage candidate = Measure(root, position, eyeOffset, candidateYaw, halfFan);
                float score = candidate.Score - Mathf.Abs(Mathf.DeltaAngle(initialYaw, candidateYaw)) * 0.00015f;
                if (score <= bestScore + 0.002f) continue;
                best = candidate;
                bestScore = score;
                yaw = candidateYaw;
            }
            return best;
        }

        private static Coverage Measure(Transform root, Vector3 position, Vector3 eyeOffset, float yaw, float halfFan)
        {
            Vector3 eye = position + Quaternion.Euler(0f, yaw, 0f) * eyeOffset;
            float totalWeight = 0f, distanceScore = 0f, closeWeight = 0f;
            float center = FarDistance;
            for (int row = 0; row < RowHeights.Length; row++)
            {
                Vector3 origin = eye + Vector3.up * RowHeights[row];
                for (int column = 0; column < ColumnWeights.Length; column++)
                {
                    Vector3 direction = Quaternion.Euler(0f, yaw + (column - 3) * halfFan / 3f, 0f) * Vector3.forward;
                    float distance = Clearance(root, origin, direction, FarDistance);
                    float weight = RowWeights[row] * ColumnWeights[column];
                    totalWeight += weight;
                    distanceScore += weight * (0.75f * Mathf.Min(distance / ComfortableDistance, 1f)
                        + 0.25f * distance / FarDistance);
                    if (distance < CloseDistance) closeWeight += weight;
                    if (row == 1 && column == 3) center = distance;
                }
            }
            return new Coverage(distanceScore / totalWeight, closeWeight / totalWeight, center);
        }

        private static float Clearance(Transform root, Vector3 origin, Vector3 direction, float distance)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, RayHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = RayHits;
            if (count == RayHits.Length)
            {
                hits = Physics.RaycastAll(origin, direction, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            for (int index = 0; index < count; index++)
                if (IsObstacle(hits[index].collider, root)) distance = Mathf.Min(distance, hits[index].distance);
            return distance;
        }

        private static bool TrySupportedPosition(CharacterController body, Vector3 original, ref Vector3 candidate)
        {
            float scaleY = Mathf.Abs(body.transform.lossyScale.y);
            float feetOffset = body.center.y * scaleY - body.height * scaleY * 0.5f;
            float originalFeet = original.y + feetOffset;
            float radius = WorldRadius(body) * 0.85f;
            float slope = Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad);
            float ground = 0f;
            for (int sample = 0; sample < 5; sample++)
            {
                Vector3 edge = sample == 0 ? Vector3.zero
                    : Quaternion.Euler(0f, (sample - 1) * 90f, 0f) * Vector3.forward * radius;
                Vector3 origin = new Vector3(candidate.x, originalFeet + 0.3f, candidate.z) + edge;
                RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 0.6f,
                    MovementLayers, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                float height = 0f;
                foreach (RaycastHit hit in hits)
                {
                    if (!IsObstacle(hit.collider, body.transform) || hit.distance >= nearest || hit.normal.y < slope) continue;
                    nearest = hit.distance;
                    height = hit.point.y;
                }
                if (float.IsInfinity(nearest) || Mathf.Abs(height - originalFeet) > MaximumFloorChange) return false;
                if (sample == 0) ground = height;
                else if (Mathf.Abs(height - ground) > 0.08f) return false;
            }
            candidate.y = ground - feetOffset;
            return ClearCapsule(body, candidate);
        }

        private static bool ClearPath(CharacterController body, Vector3 start, Vector3 end)
        {
            Vector3 motion = end - start;
            Capsule(body, start, out Vector3 bottom, out Vector3 top, out float radius);
            foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius, motion.normalized,
                motion.magnitude, MovementLayers, QueryTriggerInteraction.Ignore))
                if (IsObstacle(hit.collider, body.transform)) return false;
            // Check support and overlap along the short path as well as at its destination.
            for (int sample = 1; sample <= 3; sample++)
            {
                Vector3 point = Vector3.Lerp(start, end, sample * 0.25f);
                if (!TrySupportedPosition(body, start, ref point)) return false;
            }
            return true;
        }

        private static bool ClearCapsule(CharacterController body, Vector3 position)
        {
            Capsule(body, position, out Vector3 bottom, out Vector3 top, out float radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, Overlaps,
                MovementLayers, QueryTriggerInteraction.Ignore);
            Collider[] colliders = Overlaps;
            if (count == Overlaps.Length)
            {
                colliders = Physics.OverlapCapsule(bottom, top, radius, MovementLayers, QueryTriggerInteraction.Ignore);
                count = colliders.Length;
            }
            for (int index = 0; index < count; index++)
                if (IsObstacle(colliders[index], body.transform)) return false;
            return true;
        }

        private static void Capsule(CharacterController body, Vector3 position,
            out Vector3 bottom, out Vector3 top, out float radius)
        {
            float scaleY = Mathf.Abs(body.transform.lossyScale.y);
            float fullRadius = WorldRadius(body);
            float skin = Mathf.Max(0.01f, body.skinWidth * scaleY);
            radius = Mathf.Max(0.02f, fullRadius - skin);
            Vector3 center = position + body.transform.TransformVector(body.center);
            float half = Mathf.Max(0f, body.height * scaleY * 0.5f - fullRadius);
            bottom = center - Vector3.up * half;
            top = center + Vector3.up * half;
        }

        private static float WorldRadius(CharacterController body) => body.radius
            * Mathf.Max(Mathf.Abs(body.transform.lossyScale.x), Mathf.Abs(body.transform.lossyScale.z));

        private static bool IsObstacle(Collider collider, Transform root) => collider != null
            && !collider.transform.IsChildOf(root);
    }
}
