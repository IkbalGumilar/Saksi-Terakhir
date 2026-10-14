using System;
using System.Collections.Generic;
using SaksiTerakhir.Persistence;
using UnityEngine;
using UnityEngine.AI;

namespace SaksiTerakhir.Npc
{
    /// <summary>Distance-based population, one occupant per point, and a shared actor render budget.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(9000)]
    public sealed class NpcPopulationManager : MonoBehaviour
    {
        public const int HardMaximumVisible = 15;
        [SerializeField] private Transform observer;
        [SerializeField] private Camera viewingCamera;
        [SerializeField] private GameObject npcPrefab;
        [SerializeField] private NpcSpawnPoint[] points = Array.Empty<NpcSpawnPoint>();
        [SerializeField] private OfficeNpcDirector director;
        [SerializeField, Range(1, HardMaximumVisible)] private int maximumVisible = HardMaximumVisible;
        [SerializeField, Min(5f)] private float spawnDistance = 55f;
        [SerializeField, Min(10f)] private float despawnDistance = 85f;
        [SerializeField, Min(10f)] private float renderDistance = 75f;
        [SerializeField, Min(0.1f)] private float populationInterval = 0.75f;
        private readonly List<NpcPopulationMember> active = new List<NpcPopulationMember>();
        private readonly Stack<NpcPopulationMember> pool = new Stack<NpcPopulationMember>();
        private readonly List<NpcPopulationMember> renderCandidates = new List<NpcPopulationMember>();
        private readonly List<NpcSpawnPoint> spawnCandidates = new List<NpcSpawnPoint>();
        private readonly Plane[] frustum = new Plane[6];
        private readonly Collider[] clearanceHits = new Collider[24];
        private readonly RaycastHit[] cameraSightHits = new RaycastHit[24];
        private readonly Vector3[] cameraSamples = new Vector3[9];
        private readonly HashSet<NpcSpawnPoint> blockedPoints = new HashSet<NpcSpawnPoint>();
        private readonly Dictionary<NpcSpawnPoint, float> respawnAfter = new Dictionary<NpcSpawnPoint, float>();
        private NavMeshPath spawnPath;
        private Transform poolRoot, activeRoot;
        private float nextPopulationUpdate;
        private int instantiatedCount;
        private bool warnedMissingNavigation;
        private Comparison<NpcSpawnPoint> spawnScoreComparison;
        private Comparison<NpcPopulationMember> renderPriorityComparison;
        public int VisibleNpcCount { get; private set; }
        public int ActivePopulationCount => active.Count;
        public int PooledCount => pool.Count;
        public int MaximumVisible => Mathf.Clamp(maximumVisible, 1, HardMaximumVisible);
        public NpcSpawnPoint[] Points => points;

        public void Configure(Transform player, Camera camera, GameObject template,
            NpcSpawnPoint[] spawnPoints, OfficeNpcDirector populationDirector, int maximum = HardMaximumVisible)
        {
            observer = player; viewingCamera = camera; npcPrefab = template;
            points = spawnPoints ?? Array.Empty<NpcSpawnPoint>(); director = populationDirector;
            maximumVisible = Mathf.Clamp(maximum, 1, HardMaximumVisible);
        }

        private void Awake()
        {
            spawnPath = new NavMeshPath();
            spawnScoreComparison = (a, b) => SpawnScore(a).CompareTo(SpawnScore(b));
            renderPriorityComparison = CompareRenderPriority;
            activeRoot = new GameObject("Active Population").transform;
            activeRoot.SetParent(transform, false);
            poolRoot = new GameObject("Inactive NPC Pool").transform;
            poolRoot.SetParent(transform, false);
            poolRoot.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || observer == null || npcPrefab == null || Time.time < nextPopulationUpdate) return;
            nextPopulationUpdate = Time.time + populationInterval;
            RefreshCamera();
            if (viewingCamera != null) GeometryUtility.CalculateFrustumPlanes(viewingCamera, frustum);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                NpcPopulationMember member = active[i];
                if (member == null) { active.RemoveAt(i); continue; }
                if (member.LifetimeProtected) continue;
                bool pointRemoved = member.SpawnPoint == null || !member.SpawnPoint.isActiveAndEnabled;
                float distanceSquared = (member.transform.position - observer.position).sqrMagnitude;
                // Hysteresis plus a minimum lifetime prevents repeated appearance at range boundaries.
                if (!pointRemoved && (Time.time - member.ActivatedAt < 8f || distanceSquared <= despawnDistance * despawnDistance)) continue;
                bool inView = viewingCamera != null && member.InFrustum(frustum) && !member.RenderSuppressed;
                if (!pointRemoved && inView && distanceSquared <= renderDistance * renderDistance) continue;
                ReturnToPool(member);
            }

            int nearbyExternal = CountNearbyExternalActors();
            int ambientBudget = Mathf.Max(0, MaximumVisible - nearbyExternal);
            int nearbyManaged = CountNearbyManagedActors();
            spawnCandidates.Clear();
            foreach (NpcSpawnPoint point in points)
            {
                if (point == null || point.Occupant != null || !point.isActiveAndEnabled || blockedPoints.Contains(point)) continue;
                if (respawnAfter.TryGetValue(point, out float readyAt) && Time.time < readyAt) continue;
                Vector3 delta = point.Position - observer.position;
                float range = point.Indoors ? Mathf.Min(spawnDistance, 38f) : spawnDistance;
                if (delta.sqrMagnitude > range * range || Mathf.Abs(delta.y) > (point.Indoors ? 3.3f : 5f)) continue;
                if (!point.TryChooseProfile(out _)) continue;
                spawnCandidates.Add(point);
            }
            spawnCandidates.Sort(spawnScoreComparison);
            int createdThisUpdate = 0;
            foreach (NpcSpawnPoint point in spawnCandidates)
            {
                if (createdThisUpdate >= 2 || ambientBudget <= 0) break;
                if (nearbyManaged >= ambientBudget)
                {
                    if (!TryRebalanceFor(point)) continue;
                    nearbyManaged = CountNearbyManagedActors();
                    if (nearbyManaged >= ambientBudget) continue;
                }
                if (point.TryChooseProfile(out NpcProfile profile) && Spawn(point, profile, false) != null)
                { createdThisUpdate++; nearbyManaged++; }
            }
        }

        private float SpawnScore(NpcSpawnPoint point)
        {
            // Nearby work posts/greetings are useful before filling more distant pedestrian slots.
            float score = (point.Position - observer.position).sqrMagnitude;
            return score - (point.GreetOnce ? 100f : point.Indoors ? 30f : 0f);
        }

        private bool TryRebalanceFor(NpcSpawnPoint target)
        {
            NpcPopulationMember replace = null;
            float worstScore = SpawnScore(target) + 64f;
            foreach (NpcPopulationMember member in active)
            {
                if (member == null || member.LifetimeProtected || member.SpawnPoint == null
                    || Time.time - member.ActivatedAt < 8f) continue;
                // Pool only unseen actors: a worker in front of the player is never removed for a better post.
                if (viewingCamera != null && member.InFrustum(frustum) && !member.RenderSuppressed
                    && HasClearCameraSight(member)) continue;
                Vector3 separation = member.transform.position - observer.position;
                bool wrongFloor = member.SpawnPoint.Indoors && Mathf.Abs(separation.y) > 3.3f;
                float score = separation.sqrMagnitude + (wrongFloor ? 10000f : 0f)
                    - (member.SpawnPoint.Indoors ? 30f : 0f);
                if (score <= worstScore) continue;
                worstScore = score; replace = member;
            }
            if (replace == null) return false;
            ReturnToPool(replace); return true;
        }

        private bool HasClearCameraSight(NpcPopulationMember member)
        {
            Vector3 origin = viewingCamera.transform.position;
            Vector3 center = member.transform.position;
            float radius = member.Actor.Navigation != null ? member.Actor.Navigation.radius : 0.34f;
            float height = member.Actor.Navigation != null ? member.Actor.Navigation.height : 1.9f;
            cameraSamples[0] = center + Vector3.up * (height * 0.5f);
            int sampleIndex = 1;
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    cameraSamples[sampleIndex++] = center + new Vector3(x * radius, 0.08f, z * radius);
                    cameraSamples[sampleIndex++] = center + new Vector3(x * radius, height - 0.08f, z * radius);
                }
            foreach (Vector3 sample in cameraSamples)
            {
                Vector3 delta = sample - origin;
                int count = Physics.RaycastNonAlloc(origin, delta.normalized, cameraSightHits,
                    delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count == cameraSightHits.Length) return true;
                bool occluded = false;
                for (int i = 0; i < count; i++)
                {
                    Transform hit = cameraSightHits[i].transform;
                    if (hit == null || hit.IsChildOf(member.transform) || observer != null && hit.IsChildOf(observer)) continue;
                    Renderer surface = hit.GetComponent<Renderer>() ?? hit.GetComponentInParent<Renderer>();
                    if (surface == null || !surface.enabled || surface.sharedMaterial == null
                        || surface.sharedMaterial.renderQueue > 2500 || surface.sharedMaterial.IsKeywordEnabled("_ALPHATEST_ON")) continue;
                    occluded = true; break;
                }
                // A visible head or edge is enough to retain the whole NPC.
                if (!occluded) return true;
            }
            return false;
        }

        private int CountNearbyExternalActors()
        {
            int count = 0;
            foreach (OfficeNpcAgent actor in OfficeNpcAgent.ActiveActors)
            {
                if (actor == null || actor.gameObject.scene != gameObject.scene) continue;
                NpcPopulationMember member = actor.GetComponent<NpcPopulationMember>();
                if (member != null && member.IsManaged) continue;
                if ((actor.transform.position - observer.position).sqrMagnitude <= renderDistance * renderDistance) count++;
            }
            return count;
        }

        private int CountNearbyManagedActors()
        {
            int count = 0;
            foreach (NpcPopulationMember member in active)
                if (member != null && (!member.LifetimeProtected
                    || (member.transform.position - observer.position).sqrMagnitude <= renderDistance * renderDistance)) count++;
            return count;
        }

        private NpcPopulationMember Spawn(NpcSpawnPoint point, NpcProfile profile, bool restoringMission)
        {
            if (point == null || profile == null || point.Home == null || point.Occupant != null) return point != null ? point.Occupant : null;
            NavMeshAgent templateAgent = npcPrefab.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = templateAgent != null ? templateAgent.agentTypeID : 0,
                areaMask = templateAgent != null ? templateAgent.areaMask : NavMesh.AllAreas };
            if (!NavMesh.SamplePosition(point.Position, out NavMeshHit hit, 0.6f, filter)
                || Mathf.Abs(hit.position.y - point.Position.y) > 0.4f)
            {
                if (!warnedMissingNavigation)
                {
                    warnedMissingNavigation = true;
                    Debug.LogWarning("Some NPC posts have no walkable NavMesh; those posts are skipped until navigation is updated.", this);
                }
                blockedPoints.Add(point); return null;
            }
            if (!restoringMission && !HasSpawnClearance(hit.position, templateAgent)) return null;
            foreach (NpcActivityPoint destination in point.Route)
            {
                if (destination == null) continue;
                if (!NavMesh.SamplePosition(destination.transform.position, out NavMeshHit target, 0.6f, filter)
                    || Mathf.Abs(target.position.y - destination.transform.position.y) > 0.4f
                    || !NavMesh.CalculatePath(hit.position, target.position, filter, spawnPath)
                    || spawnPath.status != NavMeshPathStatus.PathComplete) return null;
            }

            NpcPopulationMember member = TakeFromPool(restoringMission);
            if (member == null) return null;
            if (!point.Claim(member)) { pool.Push(member); return null; }
            member.transform.SetParent(activeRoot, false);
            member.transform.SetPositionAndRotation(hit.position, point.Home.transform.rotation);
            var navigation = member.GetComponent<NavMeshAgent>();
            navigation.enabled = true;
            member.Actor.PrepareForPopulationSpawn(profile, point.Home, point.Route, director);
            NpcWorkRoutine work = member.GetComponent<NpcWorkRoutine>();
            if (work == null) work = member.gameObject.AddComponent<NpcWorkRoutine>();
            work.Configure(point.Duty, point.SeatAnchor, point.EquipmentPrefab);
            member.Assign(point);
            NpcGreetingTrigger greeting = member.GetComponent<NpcGreetingTrigger>();
            if (greeting == null) greeting = member.gameObject.AddComponent<NpcGreetingTrigger>();
            greeting.Configure("welcome:" + point.Id, observer, 4f);
            greeting.enabled = point.GreetOnce;
            member.gameObject.name = "NPC [" + point.Id + "] " + profile.DisplayName;
            member.gameObject.SetActive(true);
            active.Add(member); director?.RegisterActor(member.Actor);
            return member;
        }

        private NpcPopulationMember TakeFromPool(bool restoringMission)
        {
            while (pool.Count > 0)
            {
                NpcPopulationMember reused = pool.Pop();
                if (reused != null) return reused;
            }
            // Protected actors may require additional objects, but never additional visible slots.
            if (!restoringMission && instantiatedCount >= MaximumVisible + CountPinnedManaged()) return null;
            GameObject instance = Instantiate(npcPrefab, poolRoot, false);
            instance.SetActive(false);
            NpcPopulationMember member = instance.GetComponent<NpcPopulationMember>();
            if (member == null) member = instance.AddComponent<NpcPopulationMember>();
            instantiatedCount++; return member;
        }

        private int CountPinnedManaged()
        {
            int count = 0;
            foreach (NpcPopulationMember member in active) if (member != null && member.LifetimeProtected) count++;
            return count;
        }

        private bool HasSpawnClearance(Vector3 position, NavMeshAgent template)
        {
            float radius = template != null ? template.radius : 0.3f;
            float height = template != null ? template.height : 1.8f;
            int count = Physics.OverlapCapsuleNonAlloc(position + Vector3.up * (radius + 0.08f),
                position + Vector3.up * (height - radius), radius * 0.95f, clearanceHits,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == clearanceHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider collider = clearanceHits[i];
                if (collider == null || collider.bounds.max.y <= position.y + 0.1f) continue;
                return false;
            }
            foreach (OfficeNpcAgent actor in OfficeNpcAgent.ActiveActors)
                if (actor != null && Mathf.Abs(actor.transform.position.y - position.y) < 1.5f
                    && Vector3.ProjectOnPlane(actor.transform.position - position, Vector3.up).sqrMagnitude < 1.2f * 1.2f) return false;
            return observer == null || Vector3.ProjectOnPlane(observer.position - position, Vector3.up).sqrMagnitude > 2f * 2f;
        }

        private void ReturnToPool(NpcPopulationMember member)
        {
            if (member == null || member.LifetimeProtected) return;
            director?.UnregisterActor(member.Actor);
            if (member.SpawnPoint != null) respawnAfter[member.SpawnPoint] = Time.time + 12f;
            member.gameObject.SetActive(false); member.ClearAssignment();
            member.transform.SetParent(poolRoot, false); active.Remove(member); pool.Push(member);
        }

        private void RefreshCamera()
        {
            if (viewingCamera == null || !viewingCamera.isActiveAndEnabled) viewingCamera = Camera.main;
        }

        private void LateUpdate()
        {
            RefreshCamera(); renderCandidates.Clear(); VisibleNpcCount = 0;
            if (viewingCamera != null) GeometryUtility.CalculateFrustumPlanes(viewingCamera, frustum);
            // Every active actor participates, including existing mission actors outside our pool.
            foreach (OfficeNpcAgent actor in OfficeNpcAgent.ActiveActors)
            {
                if (actor == null || actor.gameObject.scene != gameObject.scene) continue;
                NpcPopulationMember member = actor.PopulationMember;
                if (member == null)
                {
                    member = actor.gameObject.AddComponent<NpcPopulationMember>();
                    actor.PopulationMember = member;
                }
                Vector3 delta = actor.transform.position - (viewingCamera != null ? viewingCamera.transform.position : transform.position);
                bool essential = member.IsMissionPinned || actor.HasStoryOrder || actor.IsDialogueBusy;
                bool eligible = viewingCamera != null && (essential || delta.sqrMagnitude <= renderDistance * renderDistance)
                    && member.InFrustum(frustum);
                member.SetRenderSuppressed(!eligible);
                if (eligible) renderCandidates.Add(member);
            }
            renderCandidates.Sort(renderPriorityComparison);
            foreach (NpcPopulationMember member in renderCandidates)
            {
                bool allowed = VisibleNpcCount < MaximumVisible;
                member.SetRenderSuppressed(!allowed);
                if (allowed) VisibleNpcCount++;
            }
        }

        private int CompareRenderPriority(NpcPopulationMember first, NpcPopulationMember second)
        {
            int Priority(NpcPopulationMember member) => member.Actor.CurrentState == NpcState.PlayerConversation ? 0
                : member.IsMissionPinned || member.Actor.HasStoryOrder ? 1 : member.Actor.IsDialogueBusy ? 2
                : member.SpawnPoint != null && member.SpawnPoint.Indoors ? 3 : 4;
            int comparison = Priority(first).CompareTo(Priority(second));
            if (comparison != 0) return comparison;
            Vector3 origin = viewingCamera != null ? viewingCamera.transform.position : transform.position;
            return (first.transform.position - origin).sqrMagnitude.CompareTo((second.transform.position - origin).sqrMagnitude);
        }

        public OfficeNpcAgent AcquireMissionParticipant(string pointId, string reason)
        {
            foreach (NpcSpawnPoint point in points)
            {
                if (point == null || point.Id != pointId) continue;
                NpcPopulationMember member = point.Occupant;
                if (member == null)
                {
                    NpcProfile profile = point.Profiles.Length > 0 ? point.Profiles[0] : null;
                    member = Spawn(point, profile, true);
                }
                if (member == null) return null;
                member.AcquireMissionPin(reason); return member.Actor;
            }
            return null;
        }

        public void ReleaseMissionParticipant(string pointId, string reason)
        {
            foreach (NpcSpawnPoint point in points)
                if (point != null && point.Id == pointId) point.Occupant?.ReleaseMissionPin(reason);
        }

        public void RestorePinnedActors(SavedNpcPose[] savedActors)
        {
            if (savedActors == null) return;
            foreach (SavedNpcPose pose in savedActors)
            {
                if (pose == null || string.IsNullOrEmpty(pose.spawnPointId)) continue;
                foreach (NpcSpawnPoint point in points)
                {
                    if (point == null || point.Id != pose.spawnPointId) continue;
                    NpcProfile profile = Array.Find(point.Profiles, candidate => candidate != null && candidate.Id == pose.id);
                    NpcPopulationMember member = point.Occupant ?? Spawn(point, profile, true);
                    if (member == null) continue;
                    foreach (string reason in pose.missionPins ?? Array.Empty<string>()) member.AcquireMissionPin(reason);
                    if (pose.storyHeld) member.Actor.HoldForStory();
                    break;
                }
            }
        }

        private void OnDisable()
        {
            foreach (OfficeNpcAgent actor in OfficeNpcAgent.ActiveActors)
                if (actor != null && actor.gameObject.scene == gameObject.scene)
                    actor.GetComponent<NpcPopulationMember>()?.SetRenderSuppressed(false);
        }

        private void OnValidate()
        {
            maximumVisible = Mathf.Clamp(maximumVisible, 1, HardMaximumVisible);
            despawnDistance = Mathf.Max(spawnDistance + 10f, despawnDistance);
            renderDistance = Mathf.Clamp(renderDistance, spawnDistance, despawnDistance);
        }
    }
}
