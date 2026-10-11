using System;
using UnityEngine;

namespace SaksiTerakhir.Npc
{
    public enum NpcDuty { Wander, SeatedWork, Typing, Cleaning, CarryDocuments, Stationary }

    /// <summary>One population slot, with a fixed work post or an authored pedestrian route.</summary>
    [DisallowMultipleComponent]
    public sealed class NpcSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string id;
        [SerializeField] private NpcProfile[] profiles = Array.Empty<NpcProfile>();
        [SerializeField] private NpcActivityPoint home;
        [SerializeField] private NpcActivityPoint[] route = Array.Empty<NpcActivityPoint>();
        [SerializeField] private bool indoors;
        [SerializeField] private NpcDuty duty;
        [SerializeField] private Transform seatAnchor;
        [SerializeField] private GameObject equipmentPrefab;
        [SerializeField] private bool greetOnce;
        [SerializeField, Range(0f, 1f)] private float occupancyChance = 1f;
        [SerializeField, Min(0.5f)] private float routeHalfWidth = 1.6f;
        private NpcPopulationMember occupant;
        private bool occupancyChosen, occupiedThisRun;
        private NpcProfile selectedProfile;

        public string Id => id;
        public NpcProfile[] Profiles => profiles;
        public NpcActivityPoint Home => home;
        public NpcActivityPoint[] Route => route;
        public bool Indoors => indoors;
        public NpcDuty Duty => duty;
        public Transform SeatAnchor => seatAnchor;
        public GameObject EquipmentPrefab => equipmentPrefab;
        public bool GreetOnce => greetOnce;
        public NpcPopulationMember Occupant => occupant;
        public Vector3 Position => home != null ? home.transform.position : transform.position;

        public void Configure(string stableId, NpcProfile[] choices, NpcActivityPoint homePoint,
            NpcActivityPoint[] patrol, bool inside, NpcDuty work, Transform seat = null,
            GameObject equipment = null, bool oneWayWelcome = false, float chance = 1f)
        {
            id = stableId; profiles = choices ?? Array.Empty<NpcProfile>(); home = homePoint;
            route = patrol ?? Array.Empty<NpcActivityPoint>(); indoors = inside; duty = work;
            seatAnchor = seat; equipmentPrefab = equipment; greetOnce = oneWayWelcome;
            occupancyChance = Mathf.Clamp01(chance);
        }

        public bool TryChooseProfile(out NpcProfile result)
        {
            if (!occupancyChosen)
            {
                occupancyChosen = true;
                occupiedThisRun = UnityEngine.Random.value < occupancyChance;
                if (profiles.Length > 0) selectedProfile = profiles[UnityEngine.Random.Range(0, profiles.Length)];
            }
            result = selectedProfile;
            return isActiveAndEnabled && occupant == null && occupiedThisRun && result != null && home != null;
        }

        internal bool Claim(NpcPopulationMember member)
        {
            if (member == null || occupant != null && occupant != member) return false;
            occupant = member;
            return true;
        }

        internal void Release(NpcPopulationMember member) { if (occupant == member) occupant = null; }

        public void SetRouteHalfWidth(float width) => routeHalfWidth = Mathf.Max(0.5f, width);
        public bool AllowsPosition(Vector3 position) => route.Length == 0 || NearRoute(position);

        public bool AllowsPath(Vector3[] corners)
        {
            if (home == null || route.Length == 0) return true;
            for (int i = 1; i < corners.Length; i++)
            {
                float length = Vector3.Distance(corners[i - 1], corners[i]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / 1.5f));
                for (int step = 0; step <= steps; step++)
                    if (!NearRoute(Vector3.Lerp(corners[i - 1], corners[i], step / (float)steps))) return false;
            }
            return true;
        }

        private bool NearRoute(Vector3 position)
        {
            NpcActivityPoint previous = home;
            foreach (NpcActivityPoint next in route)
            {
                if (next == null) continue;
                if (NearSegment(position, previous.transform.position, next.transform.position)) return true;
                previous = next;
            }
            return NearSegment(position, previous.transform.position, home.transform.position);
        }

        private bool NearSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 segment = b - a;
            float t = segment.sqrMagnitude > 0.001f ? Mathf.Clamp01(Vector3.Dot(point - a, segment) / segment.sqrMagnitude) : 0f;
            Vector3 nearest = a + segment * t;
            return Mathf.Abs(point.y - nearest.y) <= 0.6f
                && Vector3.ProjectOnPlane(point - nearest, Vector3.up).sqrMagnitude <= routeHalfWidth * routeHalfWidth;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = indoors ? Color.cyan : Color.green;
            Gizmos.DrawWireSphere(Position, 0.45f);
            NpcActivityPoint previous = home;
            foreach (NpcActivityPoint point in route)
            {
                if (previous != null && point != null) Gizmos.DrawLine(previous.transform.position, point.transform.position);
                if (point != null) previous = point;
            }
        }
    }
}
