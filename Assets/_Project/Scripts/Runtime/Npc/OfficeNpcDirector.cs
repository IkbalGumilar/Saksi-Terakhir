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
        [SerializeField] private bool bindProfileWaypointsOnAwake = true;
        private DoorRecord[] doors = Array.Empty<DoorRecord>();
        private readonly RaycastHit[] sightHits = new RaycastHit[24];
        private float nextSocialCheck;
        private int socialCursor;
        private bool executiveRoomRestricted;
        private Collider executiveRoomZone;
        private readonly List<OfficeNpcAgent> evacuatedActors = new List<OfficeNpcAgent>();

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
        public void SetAutomaticWaypointBinding(bool enabled) => bindProfileWaypointsOnAwake = enabled;

        public void RegisterActor(OfficeNpcAgent actor)
        {
            if (actor == null || Array.IndexOf(actors, actor) >= 0) return;
            Array.Resize(ref actors, actors.Length + 1); actors[actors.Length - 1] = actor; actor.SetDirector(this);
        }

        public void UnregisterActor(OfficeNpcAgent actor)
        {
            int index = Array.IndexOf(actors, actor);
            if (index < 0) return;
            var remaining = new OfficeNpcAgent[actors.Length - 1];
            Array.Copy(actors, 0, remaining, 0, index);
            Array.Copy(actors, index + 1, remaining, index, actors.Length - index - 1);
            actors = remaining;
        }

        public void Configure(OfficeNpcAgent[] officeActors)
        {
            actors = officeActors ?? Array.Empty<OfficeNpcAgent>();
            for (int i = 0; i < actors.Length; i++)
                if (actors[i] != null) actors[i].SetDirector(this);
        }

        private void Awake()
        {
            Configure(actors);
            if (bindProfileWaypointsOnAwake && actors.Length > 0) BindProfileWaypoints();
            RefreshDoors();
        }

        public void RefreshDoors()
        {
            DoorInteractable[] sceneDoors = FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
            doors = new DoorRecord[sceneDoors.Length];
            for (int i = 0; i < sceneDoors.Length; i++)
            {
                Bounds bounds = sceneDoors[i].ClosedDoorwayBounds;
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
            for (int i = evacuatedActors.Count - 1; i >= 0; i--)
            {
                OfficeNpcAgent actor = evacuatedActors[i];
                if (actor == null || !actor.isActiveAndEnabled)
                {
                    evacuatedActors.RemoveAt(i);
                    continue;
                }
                if (!actor.StoryAtDestination) continue;
                actor.ReleaseStoryHold();
                evacuatedActors.RemoveAt(i);
            }
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
                    NpcContextDialogueLibrary.GetAmbientExchange(first, second, out string firstLine, out string secondLine);
                    first.BeginConversation(second, duration, firstLine);
                    second.BeginConversation(first, duration, secondLine);
                    socialCursor = (index + 1) % actors.Length;
                    return;
                }
            }
            socialCursor = (socialCursor + 1) % actors.Length;
        }

        public bool IsStoryRestricted(NpcActivityPoint point)
        {
            return executiveRoomRestricted && point != null
                && (point.name.StartsWith("executive_talk", StringComparison.Ordinal)
                    || executiveRoomZone != null
                    && executiveRoomZone.bounds.Contains(point.transform.position));
        }

        public void RestrictExecutiveRoom(Collider roomZone, Transform corridorExit,
            OfficeNpcAgent boss, OfficeNpcAgent colleagueA, OfficeNpcAgent colleagueB)
        {
            if (roomZone == null || corridorExit == null) return;
            executiveRoomRestricted = true;
            executiveRoomZone = roomZone;
            foreach (OfficeNpcAgent actor in actors)
            {
                if (actor == null || actor == boss || actor == colleagueA || actor == colleagueB
                    || !roomZone.bounds.Contains(actor.transform.position)
                    || evacuatedActors.Contains(actor)) continue;
                actor.HoldForStory();
                actor.TrySetStoryDestination(corridorExit.position,
                    actor.Profile != null ? actor.Profile.MovementSpeed : 2f);
                evacuatedActors.Add(actor);
            }
        }

        public bool TryOpenDoorAhead(OfficeNpcAgent actor, Vector3 nextPathCorner, out float waitUntil)
            => TryOpenDoorAhead(actor != null ? actor.transform : null, nextPathCorner, out waitUntil);

        public bool TryOpenDoorAhead(Transform actor, Vector3 nextPathCorner, out float waitUntil)
        {
            waitUntil = 0f;
            if (actor == null) return false;
            Vector3 direction = nextPathCorner - actor.position;
            direction.y = 0f;
            float segmentLength = Mathf.Min(direction.magnitude, 1.8f);
            if (segmentLength < 0.02f) return false;
            Ray ray = new Ray(actor.position + Vector3.up * 0.95f, direction.normalized);
            DoorRecord closest = null;
            float nearestDistance = segmentLength;
            for (int i = 0; i < doors.Length; i++)
            {
                DoorRecord record = doors[i];
                if (record.Door == null || !record.Door.isActiveAndEnabled || record.ClosedBounds.size.sqrMagnitude < 0.01f) continue;
                // The collider remains disabled during the closing swing. Do not
                // restart an opening while an NPC is already clearing that doorway.
                if (record.Door.IsClosing) continue;
                Bounds bounds = record.ClosedBounds;
                if (ray.origin.y < bounds.min.y || ray.origin.y > bounds.max.y) continue;
                bounds.Expand(new Vector3(0.25f, 0f, 0.25f));
                if (!bounds.IntersectRay(ray, out float distance) || distance > nearestDistance) continue;
                if (record.Door.IsOpen)
                {
                    // Story scripts may start the swing without this director. The
                    // remaining animation, not only our cached deadline, controls passage.
                    if (record.Door.OpeningTimeRemaining > 0f)
                        record.ReadyAt = Mathf.Max(record.ReadyAt,
                            Time.time + record.Door.OpeningTimeRemaining + 0.05f);
                    if (Time.time >= record.ReadyAt) continue;
                }
                nearestDistance = distance;
                closest = record;
            }
            if (closest == null) return false;
            if (!closest.Door.IsOpen)
            {
                if (!closest.Door.CanInteract(actor)) return false;
                closest.Door.Interact(actor);
                if (!closest.Door.IsOpen) return false;
                closest.ReadyAt = Time.time + Mathf.Max(0.65f, closest.Door.OpeningTimeRemaining + 0.05f);
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
    }
}
