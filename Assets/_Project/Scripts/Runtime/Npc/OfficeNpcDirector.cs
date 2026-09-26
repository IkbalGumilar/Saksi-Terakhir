using System;
using System.Collections.Generic;
using SaksiTerakhir.Interaction;
using UnityEngine;

namespace SaksiTerakhir.Npc
{
    [DisallowMultipleComponent]
    public sealed class OfficeNpcDirector : MonoBehaviour
    {
        [SerializeField] private OfficeNpcAgent[] actors = Array.Empty<OfficeNpcAgent>();
        [SerializeField] private Transform buildingRoot;
        private DoorRecord[] doors = Array.Empty<DoorRecord>();
        private readonly RaycastHit[] sightHits = new RaycastHit[24];
        private float nextSocialCheck;
        private int socialCursor;

        private sealed class DoorRecord
        {
            public DoorInteractable Door;
            public Bounds ClosedBounds;
            public float ReadyAt;
        }

        private sealed class WaypointRecord
        {
            public NpcWaypointDefinition Definition;
            public NpcActivityPoint Point;
        }

        public OfficeNpcAgent[] Actors => actors;

        public void SetBuildingRoot(Transform root) => buildingRoot = root;

        public void Configure(OfficeNpcAgent[] officeActors)
        {
            actors = officeActors ?? Array.Empty<OfficeNpcAgent>();
            for (int i = 0; i < actors.Length; i++)
                if (actors[i] != null) actors[i].SetDirector(this);
        }

        private void Awake()
        {
            Configure(actors);
            BindProfileWaypoints();
            DoorInteractable[] sceneDoors = FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
            doors = new DoorRecord[sceneDoors.Length];
            for (int i = 0; i < sceneDoors.Length; i++)
            {
                Collider doorCollider = sceneDoors[i].GetComponent<Collider>();
                Bounds bounds = doorCollider != null ? doorCollider.bounds : new Bounds(sceneDoors[i].transform.position, Vector3.zero);
                // Keep the closed doorway bounds: opening the existing door disables its collider.
                doors[i] = new DoorRecord { Door = sceneDoors[i], ClosedBounds = bounds };
            }
        }

        private void BindProfileWaypoints()
        {
            if (buildingRoot == null)
            {
                GameObject building = GameObject.Find("Office_ArchiveHQ");
                if (building != null) buildingRoot = building.transform;
            }
            if (buildingRoot == null)
            {
                Debug.LogError("Office NPC building root is missing; profile waypoints cannot be placed.", this);
                return;
            }

            var points = new Dictionary<string, List<WaypointRecord>>(StringComparer.Ordinal);
            GameObject pointRoot = null;
            foreach (OfficeNpcAgent actor in actors)
            {
                if (actor == null || actor.Profile == null) continue;
                if (pointRoot == null)
                {
                    pointRoot = new GameObject("NPC Runtime Waypoints");
                    pointRoot.transform.SetParent(transform, false);
                }
                NpcProfile profile = actor.Profile;
                NpcActivityPoint home = GetOrCreate(profile.Home, profile.Id, points, pointRoot.transform);
                NpcWaypointDefinition[] definitionsForRoute = profile.Route ?? Array.Empty<NpcWaypointDefinition>();
                var route = new NpcActivityPoint[definitionsForRoute.Length];
                for (int i = 0; i < route.Length; i++)
                    route[i] = GetOrCreate(definitionsForRoute[i], profile.Id, points, pointRoot.transform);
                actor.BindWaypoints(home, route);
            }
        }

        private NpcActivityPoint GetOrCreate(NpcWaypointDefinition definition, string profileId,
            Dictionary<string, List<WaypointRecord>> points, Transform pointRoot)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Key))
            {
                Debug.LogError("NPC profile contains an empty waypoint definition.", this);
                return null;
            }
            if (!points.TryGetValue(definition.Key, out List<WaypointRecord> records))
            {
                records = new List<WaypointRecord>();
                points.Add(definition.Key, records);
            }
            foreach (WaypointRecord record in records)
                if (Matches(record.Definition, definition)) return record.Point;

            // Identical authored points share their reservation. A profile-specific edit gets
            // its own runtime point, so moving one NPC's route never changes another's.
            string objectName = records.Count == 0 ? definition.Key : $"{definition.Key} [{profileId}]";
            var holder = new GameObject(objectName);
            holder.transform.SetParent(pointRoot);
            holder.transform.position = buildingRoot.TransformPoint(definition.LocalPosition);
            NpcActivityPoint point = holder.AddComponent<NpcActivityPoint>();
            point.Configure(definition.Label, definition.Activity, definition.Zone,
                definition.Floor, definition.DwellSeconds);
            records.Add(new WaypointRecord { Definition = definition, Point = point });
            return point;
        }

        private static bool Matches(NpcWaypointDefinition first, NpcWaypointDefinition second)
        {
            return first.Label == second.Label && first.Activity == second.Activity
                && first.Zone == second.Zone && first.Floor == second.Floor
                && (first.LocalPosition - second.LocalPosition).sqrMagnitude <= 0.000001f
                && (first.DwellSeconds - second.DwellSeconds).sqrMagnitude <= 0.000001f;
        }

        private void Update()
        {
            if (Time.time < nextSocialCheck || actors.Length < 2) return;
            nextSocialCheck = Time.time + 1.5f;
            for (int offset = 0; offset < actors.Length; offset++)
            {
                int index = (socialCursor + offset) % actors.Length;
                OfficeNpcAgent first = actors[index];
                if (first == null || !first.CanConverse) continue;
                for (int secondIndex = 0; secondIndex < actors.Length; secondIndex++)
                {
                    OfficeNpcAgent second = actors[secondIndex];
                    if (second == null || second == first || !second.CanConverse) continue;
                    Vector3 separation = second.transform.position - first.transform.position;
                    if (Mathf.Abs(separation.y) > 0.45f || separation.sqrMagnitude > 2.6f * 2.6f
                        || separation.sqrMagnitude < 0.7f * 0.7f || !HasClearSight(first, second)) continue;
                    float duration = UnityEngine.Random.Range(4.5f, 7f);
                    first.BeginConversation(second, duration, PickAmbientLine(first));
                    second.BeginConversation(first, duration, PickAmbientLine(second));
                    socialCursor = (index + 1) % actors.Length;
                    return;
                }
            }
            socialCursor = (socialCursor + 1) % actors.Length;
        }

        public bool TryOpenDoorAhead(OfficeNpcAgent actor, Vector3 nextPathCorner, out float waitUntil)
        {
            waitUntil = 0f;
            Vector3 direction = nextPathCorner - actor.transform.position;
            direction.y = 0f;
            float segmentLength = Mathf.Min(direction.magnitude, 1.8f);
            if (segmentLength < 0.02f) return false;
            Ray ray = new Ray(actor.transform.position + Vector3.up * 0.95f, direction.normalized);
            DoorRecord closest = null;
            float nearestDistance = segmentLength;
            for (int i = 0; i < doors.Length; i++)
            {
                DoorRecord record = doors[i];
                if (record.Door == null || !record.Door.isActiveAndEnabled || record.ClosedBounds.size.sqrMagnitude < 0.01f) continue;
                Bounds bounds = record.ClosedBounds;
                if (ray.origin.y < bounds.min.y || ray.origin.y > bounds.max.y) continue;
                bounds.Expand(new Vector3(0.25f, 0f, 0.25f));
                if (!bounds.IntersectRay(ray, out float distance) || distance > nearestDistance) continue;
                if (record.Door.IsOpen && Time.time >= record.ReadyAt) continue;
                nearestDistance = distance;
                closest = record;
            }
            if (closest == null) return false;
            if (!closest.Door.IsOpen)
            {
                if (!closest.Door.CanInteract(actor.transform)) return false;
                closest.Door.Interact(actor.transform);
                if (!closest.Door.IsOpen) return false;
                closest.ReadyAt = Time.time + 0.65f;
            }
            waitUntil = closest.ReadyAt;
            return waitUntil > Time.time;
        }

        private bool HasClearSight(OfficeNpcAgent first, OfficeNpcAgent second)
        {
            Vector3 origin = first.transform.position + Vector3.up * 1.35f;
            Vector3 offset = second.transform.position + Vector3.up * 1.35f - origin;
            int count = Physics.RaycastNonAlloc(origin, offset.normalized, sightHits, offset.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Transform hit = sightHits[i].transform;
                if (hit == null || hit.IsChildOf(first.transform) || hit.IsChildOf(second.transform)) continue;
                return false;
            }
            return true;
        }

        private static string PickAmbientLine(OfficeNpcAgent actor)
        {
            string[] lines = actor.Profile != null ? actor.Profile.AmbientLines : null;
            if (lines != null && lines.Length > 0)
            {
                string selected = lines[UnityEngine.Random.Range(0, lines.Length)];
                if (!string.IsNullOrWhiteSpace(selected)) return selected;
            }
            return "Baik, saya lanjut bekerja.";
        }
    }
}
