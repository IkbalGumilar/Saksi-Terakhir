using System;
using System.Collections.Generic;
using System.IO;
using SaksiTerakhir.Npc;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.Editor
{
    /// <summary>Scene and Play Mode checks for the office's authored NPC population.</summary>
    public static class OfficeNpcVerification
    {
        private const float MaximumHorizontalSnap = 0.75f;
        private const float MaximumVerticalSnap = 0.45f;
        private static readonly Collider[] OverlapBuffer = new Collider[128];
        private static Observation activeObservation;

        [Serializable]
        public sealed class InspectionReport
        {
            public string Scene;
            public bool Passed;
            public int NpcCount;
            public int SpecialCount;
            public int ArchiveGuardCount;
            public int WorkerCount;
            public int[] RoleCounts = new int[8];
            public int[] ArchiveGuardsByFloor = new int[6];
            public int ProfileCount;
            public int LegacyScenePointCount;
            public int InteractionTriggerCount;
            public int ActivityPointCount;
            public int YardPointCount;
            public int RoofPointCount;
            public int[] ActivityPointsByFloor = new int[6];
            public int HomeToRoutePathsChecked;
            public int YardToFloorPathsChecked;
            public string[] Issues;
            public ActorInspection[] Actors;
        }

        [Serializable]
        public sealed class ActorInspection
        {
            public string Id;
            public string Name;
            public string Role;
            public int Age;
            public float MovementSpeed;
            public int DialogueLineCount;
            public int AmbientLineCount;
            public int HomeFloor = -1;
            public bool Stationary;
            public bool HasNavMeshAgent;
            public bool OnNavMesh;
            public bool HasVisibleCapsule;
            public bool HasBodyCollider;
            public bool HasNpcInteractable;
            public bool HasInteractionTrigger;
            public int RouteLength;
        }

        [Serializable]
        public sealed class PlayReport
        {
            public string Scene;
            public bool CompletedFullDuration;
            public bool Passed;
            public float RequestedSeconds;
            public float ObservedSeconds;
            public int NpcCount;
            public float TotalObservedTravelMeters;
            public int CompletedActivities;
            public int ConversationStarts;
            public int ErrorLogCount;
            public string[] ErrorLogs;
            public string[] Issues;
            public ActorObservation[] Actors;
        }

        [Serializable]
        public sealed class ActorObservation
        {
            public string Name;
            public string Role;
            public bool Stationary;
            public bool StartedOnNavMesh;
            public bool EndedOnNavMesh;
            public float ObservedTravelMeters;
            public float MovingSeconds;
            public float ActivitySeconds;
            public float ConversationSeconds;
            public float OffNavMeshSeconds;
            public float LongestMotionlessMovingSeconds;
            public int CompletedActivities;
            public int ConversationStarts;
            public int FailedPathAttempts;
            public int VisitedFloorMask;
            public string FinalState;
        }

        private sealed class Sample
        {
            public Vector3 Position;
            public bool Valid;
        }

        private sealed class Observation
        {
            public OfficeNpcAgent[] Actors;
            public ActorObservation[] Results;
            public Vector3[] PreviousPositions;
            public Vector3[] LastProgressPositions;
            public float[] CurrentMotionlessMovingSeconds;
            public int[] StartingActivities;
            public int[] StartingConversations;
            public int[] StartingFailedPaths;
            public float RequestedSeconds;
            public float ElapsedSeconds;
            public double PreviousEditorTime;
            public string ReportPath;
            public int ErrorLogCount;
            public readonly List<string> ErrorLogs = new List<string>();
        }

        /// <summary>Return a JSON inspection of the active scene. Safe to call outside Play Mode.</summary>
        public static string Inspect()
        {
            Scene scene = SceneManager.GetActiveScene();
            var issues = new List<string>();
            OfficeNpcAgent[] actors = FindInScene<OfficeNpcAgent>(scene);
            OfficeNpcDirector[] directors = FindInScene<OfficeNpcDirector>(scene);
            NpcActivityPoint[] scenePoints = FindInScene<NpcActivityPoint>(scene);
            GameObject buildingObject = GameObject.Find("Office_ArchiveHQ");
            Transform building = buildingObject != null ? buildingObject.transform : null;
            var report = new InspectionReport
            {
                Scene = scene.path,
                NpcCount = actors.Length,
                Actors = new ActorInspection[actors.Length]
            };

            if (!scene.IsValid() || !scene.isLoaded) issues.Add("No loaded active scene.");
            if (building == null || building.gameObject.scene != scene)
                issues.Add("Office_ArchiveHQ building root is missing from the active scene.");
            if (actors.Length != 25) issues.Add("Expected 25 NPC capsules; found " + actors.Length + ".");
            if (directors.Length != 1) issues.Add("Expected one OfficeNpcDirector; found " + directors.Length + ".");
            else
            {
                OfficeNpcAgent[] assigned = directors[0].Actors;
                if (assigned == null || assigned.Length != 25)
                    issues.Add("OfficeNpcDirector must reference all 25 NPCs.");
                else
                {
                    var uniqueActors = new HashSet<OfficeNpcAgent>(assigned);
                    if (uniqueActors.Count != 25 || uniqueActors.Contains(null))
                        issues.Add("OfficeNpcDirector has duplicate or missing NPC references.");
                    foreach (OfficeNpcAgent actor in actors)
                        if (!uniqueActors.Contains(actor)) issues.Add("Director is missing " + actor.name + ".");
                }
            }

            foreach (NpcActivityPoint point in scenePoints)
            {
                if (EditorApplication.isPlaying && point.transform.parent != null
                    && point.transform.parent.name == "NPC Runtime Waypoints") continue;
                report.LegacyScenePointCount++;
            }
            if (report.LegacyScenePointCount != 0)
                issues.Add("Legacy scene waypoint objects remain; waypoint definitions should live in NPC profiles.");

            var profileAssets = new HashSet<NpcProfile>();
            var profileIds = new HashSet<string>(StringComparer.Ordinal);
            var profileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var definitions = new Dictionary<string, NpcWaypointDefinition>(StringComparer.Ordinal);
            NavMeshQueryFilter commonFilter = FindFilter(actors);
            Physics.SyncTransforms();
            for (int i = 0; i < actors.Length; i++)
            {
                OfficeNpcAgent actor = actors[i];
                NpcProfile profile = actor.Profile;
                NavMeshAgent navigation = actor.GetComponent<NavMeshAgent>();
                CapsuleCollider body = actor.GetComponent<CapsuleCollider>();
                NpcInteractable interaction = actor.GetComponent<NpcInteractable>();
                bool hasTrigger = HasInteractionTrigger(actor);
                var item = new ActorInspection
                {
                    Id = profile != null ? profile.Id : string.Empty,
                    Name = actor.DisplayName,
                    Role = actor.Role.ToString(),
                    Age = profile != null ? profile.Age : 0,
                    MovementSpeed = profile != null ? profile.MovementSpeed : 0f,
                    DialogueLineCount = profile != null && profile.DialogueLines != null ? profile.DialogueLines.Length : 0,
                    AmbientLineCount = profile != null && profile.AmbientLines != null ? profile.AmbientLines.Length : 0,
                    HomeFloor = profile != null && profile.Home != null ? profile.Home.Floor : -1,
                    Stationary = actor.Stationary,
                    HasNavMeshAgent = navigation != null && navigation.enabled,
                    OnNavMesh = EditorApplication.isPlaying && navigation != null && navigation.enabled && navigation.isOnNavMesh,
                    HasVisibleCapsule = HasVisibleCapsule(actor),
                    HasBodyCollider = body != null && body.enabled && !body.isTrigger,
                    HasNpcInteractable = interaction != null && interaction.isActiveAndEnabled,
                    HasInteractionTrigger = hasTrigger,
                    RouteLength = profile != null && profile.Route != null ? profile.Route.Length : 0
                };
                report.Actors[i] = item;
                CountProfileRole(profile, report, issues);

                if (!actor.isActiveAndEnabled) issues.Add(actor.name + " is inactive.");
                if (!item.HasVisibleCapsule) issues.Add(actor.name + " has no enabled visible capsule mesh.");
                if (!item.HasBodyCollider) issues.Add(actor.name + " has no enabled root CapsuleCollider.");
                if (!item.HasNpcInteractable) issues.Add(actor.name + " has no active root NpcInteractable.");
                if (!hasTrigger) issues.Add(actor.name + " has no enabled child interaction trigger on layer 6.");
                else report.InteractionTriggerCount++;
                CheckColliderConflicts(actor, body, issues);

                if (navigation == null || !navigation.enabled)
                    issues.Add(actor.name + " has no enabled NavMeshAgent.");
                else
                {
                    if (EditorApplication.isPlaying && !navigation.isOnNavMesh)
                        issues.Add(actor.name + " is not attached to the NavMesh in Play Mode.");
                    if (!TrySample(actor.transform.position, FilterFor(navigation)).Valid)
                        issues.Add(actor.name + " starts away from the NavMesh on its floor.");
                }

                if (profile == null)
                {
                    issues.Add(actor.name + " has no NPC profile asset.");
                    continue;
                }
                if (!profileAssets.Add(profile)) issues.Add("NPC profile asset reused by " + actor.name + ".");
                string assetPath = AssetDatabase.GetAssetPath(profile);
                if (string.IsNullOrEmpty(assetPath)) issues.Add(actor.name + " profile is not a saved project asset.");
                if (string.IsNullOrWhiteSpace(profile.Id) || !profileIds.Add(profile.Id))
                    issues.Add(actor.name + " has an empty or duplicate NPC ID.");
                if (string.IsNullOrWhiteSpace(profile.DisplayName) || !profileNames.Add(profile.DisplayName))
                    issues.Add(actor.name + " has an empty or duplicate NPC name.");
                if (profile.Age < 1) issues.Add(actor.name + " has an invalid age.");
                float authoredSpeed = new SerializedObject(profile).FindProperty("movementSpeed").floatValue;
                if (float.IsNaN(authoredSpeed) || float.IsInfinity(authoredSpeed)
                    || authoredSpeed < 2f || authoredSpeed > 5f)
                    issues.Add(actor.name + " profile movement speed must be 2-5 m/s.");
                if (navigation != null && Mathf.Abs(navigation.speed - profile.MovementSpeed) > 0.02f)
                    issues.Add(actor.name + " NavMeshAgent speed differs from its profile.");
                if (item.DialogueLineCount < 3 || HasBlankLine(profile.DialogueLines))
                    issues.Add(actor.name + " needs at least three nonempty interaction dialogue lines.");
                if (item.AmbientLineCount < 2 || HasBlankLine(profile.AmbientLines))
                    issues.Add(actor.name + " needs at least two nonempty ambient dialogue lines.");
                if (profile.Home == null) issues.Add(actor.name + " has no home waypoint in its profile.");
                else RegisterWaypoint(profile.Home, definitions, issues, actor.name);
                if (!profile.Stationary && item.RouteLength < 2)
                    issues.Add(actor.name + " moves but has fewer than two route waypoints.");
                bool routeLeavesHome = false;
                if (profile.Route != null)
                    foreach (NpcWaypointDefinition point in profile.Route)
                    {
                        RegisterWaypoint(point, definitions, issues, actor.name);
                        if (point != null && profile.Home != null && point.Key != profile.Home.Key)
                            routeLeavesHome = true;
                    }
                if (!profile.Stationary && !routeLeavesHome)
                    issues.Add(actor.name + " route never leaves its home waypoint.");
                if (profile.Stationary && item.RouteLength != 0)
                    issues.Add(actor.name + " is stationary but has a movement route.");
            }
            report.ProfileCount = profileAssets.Count;
            if (report.ProfileCount != 25) issues.Add("Expected 25 distinct NPC profile assets; found " + report.ProfileCount + ".");

            report.ActivityPointCount = definitions.Count;
            if (report.ActivityPointCount < 35)
                issues.Add("Expected roughly 37 distinct waypoint definitions in profiles; found " + report.ActivityPointCount + ".");
            var sampledPoints = new Dictionary<string, Sample>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, NpcWaypointDefinition> pair in definitions)
            {
                NpcWaypointDefinition point = pair.Value;
                if (string.IsNullOrWhiteSpace(point.Label)) issues.Add("Waypoint " + pair.Key + " has no label.");
                if (point.Floor < 0 || point.Floor > 5)
                    issues.Add("Waypoint " + pair.Key + " has an unsupported floor.");
                else report.ActivityPointsByFloor[point.Floor]++;
                if ((point.Zone & NpcZone.Yard) != 0) report.YardPointCount++;
                if ((point.Zone & NpcZone.Roof) != 0) report.RoofPointCount++;
                if (point.DwellSeconds.x < 1f || point.DwellSeconds.y < point.DwellSeconds.x)
                    issues.Add("Waypoint " + pair.Key + " has invalid dwell time.");
                Sample sample = building != null ? TrySample(building.TransformPoint(point.LocalPosition), commonFilter) : new Sample();
                sampledPoints[pair.Key] = sample;
                if (!sample.Valid) issues.Add("Profile waypoint is too far from its own NavMesh floor: " + pair.Key + ".");
            }
            if (report.YardPointCount == 0) issues.Add("No yard waypoint in NPC profiles.");
            if (report.RoofPointCount == 0) issues.Add("No roof waypoint in NPC profiles.");
            for (int floor = 1; floor <= 4; floor++)
                if (report.ActivityPointsByFloor[floor] == 0)
                    issues.Add("No profile waypoint on floor " + floor + ".");

            foreach (OfficeNpcAgent actor in actors)
            {
                NpcProfile profile = actor.Profile;
                if (profile == null || profile.Home == null) continue;
                NavMeshAgent navigation = actor.GetComponent<NavMeshAgent>();
                if (navigation == null) continue;
                if (!sampledPoints.TryGetValue(profile.Home.Key, out Sample home) || !home.Valid) continue;
                if (!EditorApplication.isPlaying && Vector3.Distance(actor.transform.position, home.Position) > 0.85f)
                    issues.Add(actor.name + " does not start at its profile home waypoint.");
                if (profile.Route == null) continue;
                foreach (NpcWaypointDefinition point in profile.Route)
                {
                    if (point == null || !sampledPoints.TryGetValue(point.Key, out Sample target)) continue;
                    report.HomeToRoutePathsChecked++;
                    if (!target.Valid || !HasCompletePath(home.Position, target.Position, FilterFor(navigation)))
                        issues.Add(actor.name + " cannot reach route waypoint " + point.Key + " from home.");
                }
            }

            if (report.SpecialCount != 6) issues.Add("Expected 6 named special roles; found " + report.SpecialCount + ".");
            for (int roleIndex = (int)NpcRole.Security; roleIndex <= (int)NpcRole.Boss; roleIndex++)
                if (report.RoleCounts[roleIndex] != 1)
                    issues.Add("Expected one " + ((NpcRole)roleIndex) + "; found " + report.RoleCounts[roleIndex] + ".");
            if (report.ArchiveGuardCount != 12) issues.Add("Expected 12 archive guards; found " + report.ArchiveGuardCount + ".");
            if (report.WorkerCount != 7) issues.Add("Expected 7 workers; found " + report.WorkerCount + ".");
            for (int floor = 1; floor <= 4; floor++)
                if (report.ArchiveGuardsByFloor[floor] != 3)
                    issues.Add("Floor " + floor + " needs 3 archive guards; found " + report.ArchiveGuardsByFloor[floor] + ".");
            CheckProfileYardConnections(definitions, sampledPoints, commonFilter, report, issues);
            report.Issues = issues.ToArray();
            report.Passed = issues.Count == 0;
            return JsonUtility.ToJson(report, true);
        }

        /// <summary>Observe the running scene for the requested number of unpaused seconds.</summary>
        public static string StartPlayObservation(float seconds = 120f, string reportPath = "/tmp/saksi-npc-play-report.json")
        {
            if (!EditorApplication.isPlaying)
                return "{\"Started\":false,\"Reason\":\"Enter Play Mode first.\"}";
            if (activeObservation != null) FinishObservation(false);
            OfficeNpcAgent[] actors = FindInScene<OfficeNpcAgent>(SceneManager.GetActiveScene());
            Observation observation = new Observation
            {
                Actors = actors,
                Results = new ActorObservation[actors.Length],
                PreviousPositions = new Vector3[actors.Length],
                LastProgressPositions = new Vector3[actors.Length],
                CurrentMotionlessMovingSeconds = new float[actors.Length],
                StartingActivities = new int[actors.Length],
                StartingConversations = new int[actors.Length],
                StartingFailedPaths = new int[actors.Length],
                RequestedSeconds = Mathf.Max(1f, seconds),
                ReportPath = reportPath,
                PreviousEditorTime = EditorApplication.timeSinceStartup
            };
            for (int i = 0; i < actors.Length; i++)
            {
                OfficeNpcAgent actor = actors[i];
                observation.PreviousPositions[i] = actor.transform.position;
                observation.LastProgressPositions[i] = actor.transform.position;
                observation.StartingActivities[i] = actor.CompletedActivities;
                observation.StartingConversations[i] = actor.Conversations;
                observation.StartingFailedPaths[i] = actor.FailedPathAttempts;
                observation.Results[i] = new ActorObservation
                {
                    Name = actor.DisplayName,
                    Role = actor.Role.ToString(),
                    Stationary = actor.Stationary,
                    StartedOnNavMesh = actor.Navigation != null && actor.Navigation.isOnNavMesh
                };
            }
            activeObservation = observation;
            EditorApplication.update += TickObservation;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Application.logMessageReceived += OnLogMessage;
            return "{\"Started\":true,\"Count\":" + actors.Length + ",\"ReportPath\":\"" + EscapeJson(reportPath) + "\"}";
        }

        public static string ObservationStatus()
        {
            if (activeObservation == null) return "{\"Running\":false}";
            return "{\"Running\":true,\"ElapsedSeconds\":"
                + activeObservation.ElapsedSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                + ",\"RequestedSeconds\":"
                + activeObservation.RequestedSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "}";
        }

        public static string StopPlayObservation()
        {
            if (activeObservation == null) return "{\"Stopped\":false,\"Reason\":\"No observation is running.\"}";
            string path = activeObservation.ReportPath;
            FinishObservation(false);
            return "{\"Stopped\":true,\"ReportPath\":\"" + EscapeJson(path) + "\"}";
        }

        private static T[] FindInScene<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded) return Array.Empty<T>();
            List<T> found = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) found.AddRange(roots[i].GetComponentsInChildren<T>(true));
            return found.ToArray();
        }

        private static void CountProfileRole(NpcProfile profile, InspectionReport report, List<string> issues)
        {
            if (profile == null) return;
            int roleIndex = (int)profile.Role;
            if (roleIndex < 0 || roleIndex >= report.RoleCounts.Length)
            {
                issues.Add(profile.name + " has an unsupported NPC role.");
                return;
            }
            report.RoleCounts[roleIndex]++;
            switch (profile.Role)
            {
                case NpcRole.Security:
                case NpcRole.Receptionist:
                case NpcRole.ColleagueA:
                case NpcRole.ColleagueB:
                case NpcRole.Supervisor:
                case NpcRole.Boss:
                    report.SpecialCount++;
                    break;
                case NpcRole.ArchiveGuard:
                    report.ArchiveGuardCount++;
                    if (profile.Home != null && profile.Home.Floor >= 0
                        && profile.Home.Floor < report.ArchiveGuardsByFloor.Length)
                        report.ArchiveGuardsByFloor[profile.Home.Floor]++;
                    break;
                case NpcRole.Worker:
                    report.WorkerCount++;
                    break;
            }
        }

        private static bool HasBlankLine(string[] lines)
        {
            if (lines == null) return true;
            foreach (string line in lines)
                if (string.IsNullOrWhiteSpace(line)) return true;
            return false;
        }

        private static void RegisterWaypoint(NpcWaypointDefinition point,
            Dictionary<string, NpcWaypointDefinition> definitions, List<string> issues, string actorName)
        {
            if (point == null || string.IsNullOrWhiteSpace(point.Key))
            {
                issues.Add(actorName + " has an empty waypoint definition.");
                return;
            }
            if (!definitions.TryGetValue(point.Key, out NpcWaypointDefinition original))
            {
                definitions.Add(point.Key, point);
                return;
            }
            if ((original.LocalPosition - point.LocalPosition).sqrMagnitude > 0.0001f
                || original.Label != point.Label || original.Floor != point.Floor
                || original.Zone != point.Zone || original.Activity != point.Activity
                || (original.DwellSeconds - point.DwellSeconds).sqrMagnitude > 0.0001f)
                issues.Add(actorName + " has a conflicting definition for waypoint " + point.Key + ".");
        }

        private static bool HasInteractionTrigger(OfficeNpcAgent actor)
        {
            Collider[] colliders = actor.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                if (collider.transform == actor.transform || !collider.enabled || !collider.isTrigger
                    || collider.gameObject.layer != 6 || !collider.gameObject.activeInHierarchy) continue;
                return true;
            }
            return false;
        }

        private static void CheckProfileYardConnections(Dictionary<string, NpcWaypointDefinition> definitions,
            Dictionary<string, Sample> samples, NavMeshQueryFilter filter,
            InspectionReport report, List<string> issues)
        {
            for (int floor = 1; floor <= 5; floor++)
            {
                bool reachable = false;
                foreach (KeyValuePair<string, NpcWaypointDefinition> yard in definitions)
                {
                    if ((yard.Value.Zone & NpcZone.Yard) == 0 || !samples[yard.Key].Valid) continue;
                    foreach (KeyValuePair<string, NpcWaypointDefinition> target in definitions)
                    {
                        bool matches = floor == 5 ? (target.Value.Zone & NpcZone.Roof) != 0
                            : target.Value.Floor == floor && (target.Value.Zone & NpcZone.Interior) != 0;
                        if (!matches || !samples[target.Key].Valid) continue;
                        report.YardToFloorPathsChecked++;
                        if (!HasCompletePath(samples[yard.Key].Position, samples[target.Key].Position, filter)) continue;
                        reachable = true;
                        break;
                    }
                    if (reachable) break;
                }
                if (!reachable) issues.Add("No complete NavMesh route from the yard to "
                    + (floor == 5 ? "the roof" : "floor " + floor) + ".");
            }
        }

        private static bool HasVisibleCapsule(OfficeNpcAgent actor)
        {
            MeshRenderer[] renderers = actor.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (renderer.enabled && renderer.gameObject.activeInHierarchy && filter != null
                    && filter.sharedMesh != null && filter.sharedMesh.bounds.size.y >= 0.8f)
                    return true;
            }
            return false;
        }

        private static void CheckColliderConflicts(OfficeNpcAgent actor, CapsuleCollider body, List<string> issues)
        {
            Rigidbody[] rigidbodies = actor.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
                if (!rigidbodies[i].isKinematic)
                    issues.Add(actor.DisplayName + " has a non-kinematic Rigidbody fighting the NavMeshAgent.");
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy) return;
            int hitCount = Physics.OverlapBoxNonAlloc(body.bounds.center, body.bounds.extents, OverlapBuffer,
                Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            if (hitCount == OverlapBuffer.Length)
                issues.Add(actor.DisplayName + " collider check filled the overlap buffer.");
            for (int i = 0; i < hitCount; i++)
            {
                Collider other = OverlapBuffer[i];
                OverlapBuffer[i] = null;
                if (other == null || other == body || other.isTrigger || other.transform.IsChildOf(actor.transform)) continue;
                if (!Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    other, other.transform.position, other.transform.rotation, out Vector3 direction, out float distance)) continue;
                // Ignore the tiny contact tolerance where a capsule meets the floor.
                if (distance <= 0.08f) continue;
                if (direction.y > 0.7f && distance <= 0.16f) continue;
                issues.Add(actor.DisplayName + " starts inside collider " + other.name + " by " + distance.ToString("0.00") + " m.");
            }
        }

        private static NavMeshQueryFilter FindFilter(OfficeNpcAgent[] actors)
        {
            for (int i = 0; i < actors.Length; i++)
            {
                NavMeshAgent agent = actors[i].GetComponent<NavMeshAgent>();
                if (agent != null) return FilterFor(agent);
            }
            return new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
        }

        private static NavMeshQueryFilter FilterFor(NavMeshAgent agent)
        {
            return new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        }

        private static Sample TrySample(Vector3 source, NavMeshQueryFilter filter)
        {
            Sample result = new Sample();
            if (!NavMesh.SamplePosition(source, out NavMeshHit hit, 1.3f, filter)) return result;
            Vector3 horizontal = hit.position - source;
            horizontal.y = 0f;
            result.Valid = horizontal.magnitude <= MaximumHorizontalSnap
                && Mathf.Abs(hit.position.y - source.y) <= MaximumVerticalSnap;
            result.Position = hit.position;
            return result;
        }

        private static bool HasCompletePath(Vector3 from, Vector3 to, NavMeshQueryFilter filter)
        {
            NavMeshPath path = new NavMeshPath();
            return NavMesh.CalculatePath(from, to, filter, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        private static void TickObservation()
        {
            Observation observation = activeObservation;
            if (observation == null) return;
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Max(0f, (float)(now - observation.PreviousEditorTime));
            observation.PreviousEditorTime = now;
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            observation.ElapsedSeconds += delta;
            for (int i = 0; i < observation.Actors.Length; i++)
            {
                OfficeNpcAgent actor = observation.Actors[i];
                if (actor == null) continue;
                ActorObservation result = observation.Results[i];
                Vector3 position = actor.transform.position;
                float displacement = Vector3.Distance(position, observation.PreviousPositions[i]);
                if (displacement < 2f) result.ObservedTravelMeters += displacement;
                observation.PreviousPositions[i] = position;
                switch (actor.CurrentState)
                {
                    case NpcState.Moving:
                        result.MovingSeconds += delta;
                        // Editor update can sample several times per game frame. Measure actual
                        // progress over a short distance instead of movement per update tick.
                        if (Vector3.Distance(position, observation.LastProgressPositions[i]) >= 0.08f)
                        {
                            observation.LastProgressPositions[i] = position;
                            observation.CurrentMotionlessMovingSeconds[i] = 0f;
                        }
                        else observation.CurrentMotionlessMovingSeconds[i] += Mathf.Min(delta, 0.2f);
                        result.LongestMotionlessMovingSeconds = Mathf.Max(
                            result.LongestMotionlessMovingSeconds, observation.CurrentMotionlessMovingSeconds[i]);
                        break;
                    case NpcState.Activity:
                        result.ActivitySeconds += delta;
                        observation.CurrentMotionlessMovingSeconds[i] = 0f;
                        observation.LastProgressPositions[i] = position;
                        break;
                    case NpcState.Conversation:
                        result.ConversationSeconds += delta;
                        observation.CurrentMotionlessMovingSeconds[i] = 0f;
                        observation.LastProgressPositions[i] = position;
                        break;
                    case NpcState.OffNavMesh:
                        result.OffNavMeshSeconds += delta;
                        observation.CurrentMotionlessMovingSeconds[i] = 0f;
                        observation.LastProgressPositions[i] = position;
                        break;
                    default:
                        observation.CurrentMotionlessMovingSeconds[i] = 0f;
                        observation.LastProgressPositions[i] = position;
                        break;
                }
            }
            if (observation.ElapsedSeconds >= observation.RequestedSeconds) FinishObservation(true);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) FinishObservation(false);
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            Observation observation = activeObservation;
            if (observation == null || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            observation.ErrorLogCount++;
            if (observation.ErrorLogs.Count < 20)
                observation.ErrorLogs.Add(message.Length > 500 ? message.Substring(0, 500) : message);
        }

        private static void FinishObservation(bool completedFullDuration)
        {
            Observation observation = activeObservation;
            if (observation == null) return;
            activeObservation = null;
            EditorApplication.update -= TickObservation;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Application.logMessageReceived -= OnLogMessage;
            List<string> issues = new List<string>();
            PlayReport report = new PlayReport
            {
                Scene = SceneManager.GetActiveScene().path,
                CompletedFullDuration = completedFullDuration,
                RequestedSeconds = observation.RequestedSeconds,
                ObservedSeconds = observation.ElapsedSeconds,
                NpcCount = observation.Actors.Length,
                ErrorLogCount = observation.ErrorLogCount,
                ErrorLogs = observation.ErrorLogs.ToArray(),
                Actors = observation.Results
            };
            if (!completedFullDuration) issues.Add("Observation ended before the requested duration.");
            if (report.NpcCount != 25) issues.Add("Play Mode did not contain 25 NPCs.");
            for (int i = 0; i < observation.Actors.Length; i++)
            {
                OfficeNpcAgent actor = observation.Actors[i];
                ActorObservation result = report.Actors[i];
                if (actor == null)
                {
                    issues.Add(result.Name + " was destroyed during observation.");
                    continue;
                }
                result.EndedOnNavMesh = actor.Navigation != null && actor.Navigation.isOnNavMesh;
                result.CompletedActivities = actor.CompletedActivities - observation.StartingActivities[i];
                result.ConversationStarts = actor.Conversations - observation.StartingConversations[i];
                result.FailedPathAttempts = actor.FailedPathAttempts - observation.StartingFailedPaths[i];
                result.VisitedFloorMask = actor.VisitedFloorMask;
                result.FinalState = actor.CurrentState.ToString();
                report.TotalObservedTravelMeters += result.ObservedTravelMeters;
                report.CompletedActivities += result.CompletedActivities;
                report.ConversationStarts += result.ConversationStarts;
                if (!result.StartedOnNavMesh || !result.EndedOnNavMesh || result.OffNavMeshSeconds > 1f)
                    issues.Add(result.Name + " was detached from the NavMesh.");
                if (!result.Stationary && result.ObservedTravelMeters < 0.5f)
                    issues.Add(result.Name + " did not move at least 0.5 m.");
                if (result.LongestMotionlessMovingSeconds > 15f)
                    issues.Add(result.Name + " was motionless while moving for over 15 seconds.");
            }
            if (report.CompletedActivities == 0) issues.Add("No NPC completed an activity during observation.");
            if (report.ConversationStarts == 0) issues.Add("No NPC conversations started during observation.");
            if (report.ErrorLogCount > 0) issues.Add("Unity logged " + report.ErrorLogCount + " errors or exceptions.");
            report.Issues = issues.ToArray();
            report.Passed = issues.Count == 0;
            string json = JsonUtility.ToJson(report, true);
            try
            {
                string directory = Path.GetDirectoryName(observation.ReportPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(observation.ReportPath, json);
                Debug.Log("Office NPC play observation written to " + observation.ReportPath
                    + " (passed=" + report.Passed + ").");
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not write Office NPC observation: " + exception);
            }
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
