using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SaksiTerakhir.Navigation
{
    /// <summary>Checks the physical route as well as the saved navigation data before scripted movement.</summary>
    public static class TerrainNavigationProbe
    {
        private const float Skin = 0.035f;
        private const float GroundSeam = 0.05f;
        private const float GroundInterval = 0.18f;
        private static readonly RaycastHit[] Hits = new RaycastHit[64];
        private static readonly Collider[] Overlaps = new Collider[64];
        private static readonly NavMeshPath LocalPath = new NavMeshPath();
        private static readonly Dictionary<int, int> DetourSides = new Dictionary<int, int>();
        private static readonly float[] DetourAngles = { 25f, 45f, 65f, 82f, 90f, 110f };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => DetourSides.Clear();

        public static void ClearDetour(Transform actor)
        {
            if (actor != null) DetourSides.Remove(actor.GetInstanceID());
        }

        public static bool TryResolveDirection(Transform actor, Vector3 desiredDirection,
            float probeDistance, float radius, float height, float stepHeight, float maxSlope,
            NavMeshQueryFilter filter, out Vector3 safeDirection, Predicate<Collider> ignoreCollider = null,
            bool requireNavigation = true, Predicate<Vector3> acceptGround = null, bool allowNarrowFootGap = false)
        {
            safeDirection = Vector3.zero;
            if (actor == null || !IsFinite(desiredDirection) || !IsFinite(actor.position)) return false;
            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude < 0.0001f) return false;
            radius = Mathf.Max(0.05f, radius);
            height = Mathf.Max(radius * 2f, height);
            stepHeight = Mathf.Max(0.02f, stepHeight);
            maxSlope = Mathf.Clamp(maxSlope, 0f, 75f);
            probeDistance = Mathf.Max(0.25f, probeDistance);
            Vector3 direction = desiredDirection.normalized;
            if (!TrySupportedGround(actor, actor.position, stepHeight + Skin, stepHeight + Skin,
                maxSlope, radius, allowNarrowFootGap, out Vector3 feet, ignoreCollider)) return false;
            if (IsRouteSafe(actor, feet, direction, probeDistance, radius, height, stepHeight, maxSlope, filter,
                    ignoreCollider, requireNavigation, acceptGround, allowNarrowFootGap, out _))
            {
                safeDirection = direction;
                return true;
            }

            int id = actor.GetInstanceID();
            int side = DetourSides.TryGetValue(id, out int previous) ? previous : 1;
            for (int sideAttempt = 0; sideAttempt < 2; sideAttempt++)
            {
                int candidateSide = sideAttempt == 0 ? side : -side;
                foreach (float angle in DetourAngles)
                {
                    Vector3 candidate = Quaternion.AngleAxis(angle * candidateSide, Vector3.up) * direction;
                    if (!IsRouteSafe(actor, feet, candidate, probeDistance, radius, height,
                        stepHeight, maxSlope, filter, ignoreCollider, requireNavigation, acceptGround, allowNarrowFootGap, out _)) continue;
                    if (DetourSides.Count > 256) DetourSides.Clear();
                    DetourSides[id] = candidateSide;
                    safeDirection = candidate;
                    return true;
                }
            }
            return false;
        }

        public static bool TryGetGroundPosition(Transform actor, Vector3 requestedFeet,
            float stepHeight, float maxSlope, out Vector3 groundFeet, Predicate<Collider> ignoreCollider = null)
        {
            groundFeet = requestedFeet;
            if (actor == null || !IsFinite(requestedFeet)) return false;
            float allowance = Mathf.Max(0.02f, stepHeight) + Skin;
            return TryGround(actor, requestedFeet, allowance, allowance, maxSlope, out groundFeet, ignoreCollider);
        }

        internal static bool TryGetLinkGroundPosition(Transform actor, Vector3 requested,
            float stepHeight, float slope, float radius, out Vector3 ground, Predicate<Collider> ignore)
            => TrySupportedGround(actor, requested, stepHeight + Skin, stepHeight + Skin,
                slope, radius, true, out ground, ignore);

        // A link already supplies the navigation connection. Its local detour still
        // needs continuous physical support, a full swept body and bounded ground heights.
        internal static bool TryGetSupportedSegment(Transform actor, Vector3 from, Vector3 to,
            float radius, float height, float stepHeight, float maxSlope, out Vector3 groundedEnd,
            Predicate<Collider> ignoreCollider, Predicate<Vector3> acceptGround, bool allowNarrowFootGap = false)
        {
            groundedEnd = to;
            if (actor == null || !IsFinite(from) || !IsFinite(to)
                || !TrySupportedGround(actor, from, stepHeight + Skin, stepHeight + Skin,
                    maxSlope, radius, allowNarrowFootGap, out Vector3 feet, ignoreCollider)) return false;
            Vector3 direction = Vector3.ProjectOnPlane(to - from, Vector3.up);
            float distance = direction.magnitude;
            if (distance < 0.001f) { groundedEnd = feet; return acceptGround == null || acceptGround(feet); }
            return IsRouteSafe(actor, feet, direction / distance, distance, radius, height,
                stepHeight, maxSlope, default, ignoreCollider, false, acceptGround, allowNarrowFootGap, out groundedEnd);
        }

        private static bool IsRouteSafe(Transform actor, Vector3 feet, Vector3 direction,
            float distance, float radius, float height, float stepHeight, float maxSlope,
            NavMeshQueryFilter filter, Predicate<Collider> ignoreCollider, bool requireNavigation,
            Predicate<Vector3> acceptGround, bool allowNarrowFootGap, out Vector3 groundedEnd)
        {
            groundedEnd = feet;
            Vector3 previous = feet;
            int segments = Mathf.Max(1, Mathf.CeilToInt(distance / GroundInterval));
            float segmentDistance = distance / segments;
            float slopeRise = segmentDistance * Mathf.Tan(maxSlope * Mathf.Deg2Rad);
            for (int i = 1; i <= segments; i++)
            {
                Vector3 requested = feet + direction * (segmentDistance * i);
                requested.y = previous.y;
                if (!TrySupportedGround(actor, requested, stepHeight + slopeRise + Skin,
                    stepHeight + slopeRise + Skin, maxSlope, radius, allowNarrowFootGap,
                    out Vector3 next, ignoreCollider)) return false;
                if (acceptGround != null && !acceptGround(next)) return false;

                // The whole footprint needs support, not just the centre ray at a roof edge.
                Vector3 across = Vector3.Cross(Vector3.up, direction) * (radius * 0.8f);
                float supportAllowance = stepHeight + radius * Mathf.Tan(maxSlope * Mathf.Deg2Rad) + Skin;
                if (!TrySupportedGround(actor, next + across, supportAllowance, supportAllowance,
                        maxSlope, radius, allowNarrowFootGap, out _, ignoreCollider)
                    || !TrySupportedGround(actor, next - across, supportAllowance, supportAllowance,
                        maxSlope, radius, allowNarrowFootGap, out _, ignoreCollider)) return false;
                if (!HasCapsuleClearance(actor, previous, next, radius, height, stepHeight, ignoreCollider)) return false;
                previous = next;
            }
            groundedEnd = previous;
            return !requireNavigation || HasLocalConnection(feet, previous, direction, distance, stepHeight, filter);
        }

        private static bool TrySupportedGround(Transform actor, Vector3 requested, float rise,
            float drop, float slope, float radius, bool allowNarrowFootGap, out Vector3 ground,
            Predicate<Collider> ignore)
        {
            if (TryGround(actor, requested, rise, drop, slope, out ground, ignore)) return true;
            if (!allowNarrowFootGap) return false;
            // Explicit links can bridge a seam narrower than a foot stance. Both
            // opposing supports are mandatory; one roof edge or a wide gap cannot qualify.
            float spread = radius * 0.7f;
            foreach (Vector3 axis in new[] { Vector3.right, Vector3.forward })
            {
                if (!TryGround(actor, requested + axis * spread, rise, drop, slope, out Vector3 a, ignore)
                    || !TryGround(actor, requested - axis * spread, rise, drop, slope, out Vector3 b, ignore)
                    || Mathf.Abs(a.y - b.y) > rise - Skin + 0.001f) continue;
                ground = requested;
                ground.y = (a.y + b.y) * 0.5f;
                return true;
            }
            return false;
        }

        private static bool HasLocalConnection(Vector3 feet, Vector3 end, Vector3 direction,
            float distance, float stepHeight, NavMeshQueryFilter filter)
        {
            // Complete local paths include authored links. Raycast would incorrectly reject their gaps.
            const float linkNeighborhood = 1.25f;
            // Stair polygons can sit above/below the physical step by the allowed
            // climb height. Physical support was checked before this projection.
            float floorTolerance = Mathf.Clamp(stepHeight + Skin, 0.18f, 0.35f);
            bool fromFound = NavMesh.SamplePosition(feet, out NavMeshHit from, linkNeighborhood, filter);
            bool toFound = NavMesh.SamplePosition(end, out NavMeshHit to, linkNeighborhood, filter);
            float connectionRange = linkNeighborhood;
            float projectionAllowance = 0f;
            if (!fromFound || !toFound || Mathf.Abs(from.position.y - feet.y) > floorTolerance
                || Mathf.Abs(to.position.y - end.y) > floorTolerance)
            {
                // In the middle of a sloped link, the nearest polygon is an endpoint at a different
                // height. Require a short complete path whose endpoint heights bracket the supported
                // segment; a disconnected floor above/below cannot satisfy this check.
                // Erosion can leave neither nearby polygon within reach at the middle of a link.
                // Extend the neighbourhood along the route, while keeping the vertical sample
                // radius small and requiring a bounded complete connection between both ends.
                connectionRange = 2f;
                Vector3 sourceProbe = feet - direction * connectionRange;
                Vector3 targetProbe = end + direction * connectionRange;
                if (!NavMesh.SamplePosition(sourceProbe, out from,
                    linkNeighborhood, filter)
                    || !NavMesh.SamplePosition(targetProbe, out to,
                        linkNeighborhood, filter)) return false;
                float low = Mathf.Min(from.position.y, to.position.y) - floorTolerance;
                float high = Mathf.Max(from.position.y, to.position.y) + floorTolerance;
                if (feet.y < low || feet.y > high || end.y < low || end.y > high) return false;
                // Sampling can project a probe onto a link endpoint beyond that probe. Include
                // these already bounded offsets instead of rejecting a legitimate longer span.
                projectionAllowance = Vector3.Distance(sourceProbe, from.position)
                    + Vector3.Distance(targetProbe, to.position);
            }
            if (!NavMesh.CalculatePath(from.position, to.position, filter, LocalPath)
                || LocalPath.status != NavMeshPathStatus.PathComplete) return false;
            Vector3[] corners = LocalPath.corners;
            float length = 0f;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length <= distance + connectionRange * 2f + projectionAllowance + 0.5f;
        }

        private static bool TryGround(Transform actor, Vector3 requested, float rise,
            float drop, float maxSlope, out Vector3 grounded, Predicate<Collider> ignoreCollider)
        {
            grounded = requested;
            int count = Physics.RaycastNonAlloc(requested + Vector3.up * (rise + Skin),
                Vector3.down, Hits, rise + drop + Skin * 2f, ~0,
                QueryTriggerInteraction.Ignore);
            // A saturated buffer cannot establish that the closest solid surface was examined.
            if (count == Hits.Length) return false;
            float nearestSolid = float.PositiveInfinity;
            float nearestWalkable = float.PositiveInfinity;
            Vector3 walkablePoint = requested;
            float minNormal = Mathf.Cos(Mathf.Clamp(maxSlope, 0f, 75f) * Mathf.Deg2Rad);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (IsIgnored(actor, hit.collider, ignoreCollider)) continue;
                nearestSolid = Mathf.Min(nearestSolid, hit.distance);
                if (hit.normal.y < minNormal || hit.distance >= nearestWalkable) continue;
                nearestWalkable = hit.distance;
                walkablePoint = hit.point;
            }
            if (float.IsInfinity(nearestWalkable) || nearestWalkable - nearestSolid > GroundSeam + 0.0001f)
                return false;
            // A thin ramp end cap can sit a few centimetres above its landing. Allow only this seam;
            // a genuinely steep surface cannot borrow a distant floor beneath it.
            grounded = walkablePoint;
            return true;
        }

        private static bool HasCapsuleClearance(Transform actor, Vector3 from, Vector3 to,
            float radius, float height, float stepHeight, Predicate<Collider> ignoreCollider)
        {
            // Leave the permitted step below the swept body; floor and step rays verify it separately.
            float lower = Mathf.Min(height - radius, radius + stepHeight) + Skin;
            float upper = Mathf.Max(lower, height - radius + Skin);
            Vector3 bottom = from + Vector3.up * lower;
            Vector3 top = from + Vector3.up * upper;
            Vector3 displacement = to - from;
            float distance = displacement.magnitude;
            if (distance > 0.0001f)
            {
                int hits = Physics.CapsuleCastNonAlloc(bottom, top, radius + Skin, displacement / distance,
                    Hits, distance, ~0, QueryTriggerInteraction.Ignore);
                if (hits == Hits.Length) return false;
                for (int i = 0; i < hits; i++) if (!IsIgnored(actor, Hits[i].collider, ignoreCollider)) return false;
            }
            int overlaps = Physics.OverlapCapsuleNonAlloc(to + Vector3.up * lower,
                to + Vector3.up * upper, radius + Skin, Overlaps,
                ~0, QueryTriggerInteraction.Ignore);
            if (overlaps == Overlaps.Length) return false;
            for (int i = 0; i < overlaps; i++) if (!IsIgnored(actor, Overlaps[i], ignoreCollider)) return false;
            return true;
        }

        private static bool IsIgnored(Transform actor, Collider collider, Predicate<Collider> ignoreCollider)
            => collider == null || collider.transform == actor || collider.transform.IsChildOf(actor)
                || (ignoreCollider != null && ignoreCollider(collider));

        private static bool IsFinite(Vector3 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.x)
            && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
    }
}
