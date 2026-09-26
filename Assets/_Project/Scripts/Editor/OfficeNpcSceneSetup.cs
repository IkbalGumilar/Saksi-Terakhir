using System;
using System.Collections.Generic;
using SaksiTerakhir.Npc;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    /// <summary>Creates the office's authored capsule cast on the baked Office Capsule navigation mesh.</summary>
    public static class OfficeNpcSceneSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string MaterialFolder = "Assets/_Project/Art/Generated/Npc";
        private const int ExpectedActors = 25;

        private readonly struct PointSpec
        {
            public readonly string Key;
            public readonly string Label;
            public readonly Vector3 LocalPosition;
            public readonly NpcActivityKind Activity;
            public readonly NpcZone Zone;
            public readonly int Floor;
            public readonly Vector2 Dwell;

            public PointSpec(string key, string label, float x, float y, float z,
                NpcActivityKind activity, NpcZone zone, int floor, float minDwell = 5f, float maxDwell = 12f)
            {
                Key = key;
                Label = label;
                LocalPosition = new Vector3(x, y, z);
                Activity = activity;
                Zone = zone;
                Floor = floor;
                Dwell = new Vector2(minDwell, maxDwell);
            }
        }

        private readonly struct ActorSpec
        {
            public readonly string Name;
            public readonly NpcRole Role;
            public readonly string Home;
            public readonly bool Stationary;
            public readonly string[] Route;

            public ActorSpec(string name, NpcRole role, string home, bool stationary, params string[] route)
            {
                Name = name;
                Role = role;
                Home = home;
                Stationary = stationary;
                Route = route;
            }
        }

        private static readonly PointSpec[] Points =
        {
            new PointSpec("yard_guard", "Pos satpam halaman", -6.7f, 0f, -0.75f, NpcActivityKind.Patrol, NpcZone.Yard, 0),
            new PointSpec("yard_west", "Patroli halaman barat", -7.5f, 0f, -3f, NpcActivityKind.Patrol, NpcZone.Yard, 0),
            new PointSpec("yard_east", "Patroli halaman timur", -5.6f, 0f, -5f, NpcActivityKind.Patrol, NpcZone.Yard, 0),
            new PointSpec("yard_far", "Pemeriksaan halaman", -8.3f, 0f, -6.8f, NpcActivityKind.Patrol, NpcZone.Yard, 0),

            new PointSpec("lobby_reception", "Meja resepsionis", -8f, 0f, -13f, NpcActivityKind.Work, NpcZone.Interior, 1, 10f, 22f),
            new PointSpec("lobby_guard", "Penjaga arsip lantai 1", -7f, 0f, -13f, NpcActivityKind.InspectArchive, NpcZone.Interior, 1, 9f, 18f),
            new PointSpec("lobby_colleague", "Ruang kerja rekan A", -9f, 0f, -14f, NpcActivityKind.Work, NpcZone.Interior, 1),
            new PointSpec("lobby_corridor", "Koridor depan", -7f, 0f, -10f, NpcActivityKind.Socialize, NpcZone.Interior, 1),
            new PointSpec("lobby_talk", "Percakapan lobi", -8.2f, 0f, -13.4f, NpcActivityKind.Socialize, NpcZone.Interior, 1),
            new PointSpec("archive_1a", "Pemeriksaan arsip lantai 1 A", -3f, 0f, -17f, NpcActivityKind.InspectArchive, NpcZone.Interior, 1),
            new PointSpec("archive_1b", "Pemeriksaan arsip lantai 1 B", -9f, 0f, -18f, NpcActivityKind.InspectArchive, NpcZone.Interior, 1),
            new PointSpec("work_1", "Meja kerja lantai 1", -10f, 0f, -21f, NpcActivityKind.Work, NpcZone.Interior, 1),
            new PointSpec("work_1b", "Pemeriksaan dokumen lantai 1", -5f, 0f, -24f, NpcActivityKind.Work, NpcZone.Interior, 1),

            new PointSpec("archive_2front", "Penjaga arsip lantai 2", -7f, 4.2f, -13f, NpcActivityKind.InspectArchive, NpcZone.Interior, 2, 9f, 18f),
            new PointSpec("archive_2a", "Pemeriksaan arsip lantai 2 A", -9f, 4.2f, -11f, NpcActivityKind.InspectArchive, NpcZone.Interior, 2),
            new PointSpec("archive_2b", "Pemeriksaan arsip lantai 2 B", -3f, 4.2f, -17f, NpcActivityKind.InspectArchive, NpcZone.Interior, 2),
            new PointSpec("archive_2route", "Rak arsip lantai 2", -8f, 4.2f, -13f, NpcActivityKind.InspectArchive, NpcZone.Interior, 2),
            new PointSpec("colleague_b", "Ruang kerja rekan B", -10f, 4.2f, -20f, NpcActivityKind.Work, NpcZone.Interior, 2),
            new PointSpec("work_2", "Meja kerja lantai 2", -6f, 4.2f, -23f, NpcActivityKind.Work, NpcZone.Interior, 2),
            new PointSpec("work_2b", "Pemeriksaan dokumen pekerja lantai 2", -6f, 4.2f, -21.5f, NpcActivityKind.Work, NpcZone.Interior, 2),

            new PointSpec("archive_3front", "Penjaga arsip lantai 3", -7f, 8f, -13f, NpcActivityKind.InspectArchive, NpcZone.Interior, 3, 9f, 18f),
            new PointSpec("archive_3a", "Pemeriksaan rak arsip lantai 3 A", -6f, 8f, -18f, NpcActivityKind.InspectArchive, NpcZone.Interior, 3),
            new PointSpec("archive_3b", "Pemeriksaan rak arsip lantai 3 B", -3f, 8f, -20f, NpcActivityKind.InspectArchive, NpcZone.Interior, 3),
            new PointSpec("archive_3route", "Rak arsip lantai 3", -8f, 8f, -14f, NpcActivityKind.InspectArchive, NpcZone.Interior, 3),
            new PointSpec("archive_3talk", "Percakapan lantai 3", -7f, 8f, -14f, NpcActivityKind.Socialize, NpcZone.Interior, 3),
            new PointSpec("supervisor", "Ruang atasan", -9f, 8f, -22f, NpcActivityKind.Work, NpcZone.Interior, 3),
            new PointSpec("work_3", "Meja kerja lantai 3", -5f, 8f, -14f, NpcActivityKind.Work, NpcZone.Interior, 3),

            new PointSpec("archive_4front", "Penjaga arsip lantai 4", -7f, 11.8f, -13f, NpcActivityKind.InspectArchive, NpcZone.Interior, 4, 9f, 18f),
            new PointSpec("archive_4a", "Pemeriksaan rak arsip lantai 4 A", -6f, 11.8f, -17f, NpcActivityKind.InspectArchive, NpcZone.Interior, 4),
            new PointSpec("archive_4b", "Pemeriksaan rak arsip lantai 4 B", -7f, 11.8f, -21f, NpcActivityKind.InspectArchive, NpcZone.Interior, 4),
            new PointSpec("boss", "Ruang bos", -5f, 11.8f, -16f, NpcActivityKind.Work, NpcZone.Interior, 4, 12f, 25f),
            new PointSpec("executive_talk", "Percakapan ruang pimpinan", -5.8f, 11.8f, -16f, NpcActivityKind.Socialize, NpcZone.Interior, 4),

            new PointSpec("roof_1", "Pekerja rooftop barat", -9f, 15.6f, -17f, NpcActivityKind.Patrol, NpcZone.Roof, 5),
            new PointSpec("roof_2", "Pekerja rooftop tengah", -8f, 15.6f, -17f, NpcActivityKind.Patrol, NpcZone.Roof, 5),
            new PointSpec("roof_lookout", "Pemeriksaan rooftop", -7f, 15.6f, -10f, NpcActivityKind.Patrol, NpcZone.Roof, 5),
            new PointSpec("roof_east", "Patroli rooftop timur", -5f, 15.6f, -20f, NpcActivityKind.Patrol, NpcZone.Roof, 5),
            new PointSpec("roof_west", "Patroli rooftop barat", -10f, 15.6f, -20f, NpcActivityKind.Patrol, NpcZone.Roof, 5),
        };

        private static readonly ActorSpec[] Cast =
        {
            new ActorSpec("Satpam", NpcRole.Security, "yard_guard", false, "yard_west", "yard_far", "lobby_corridor", "yard_east", "yard_guard"),
            new ActorSpec("Resepsionis", NpcRole.Receptionist, "lobby_reception", true),
            new ActorSpec("Rekan A", NpcRole.ColleagueA, "lobby_colleague", false, "archive_1b", "archive_2route", "archive_3route", "lobby_talk"),
            new ActorSpec("Rekan B", NpcRole.ColleagueB, "colleague_b", false, "archive_2route", "archive_3route", "archive_4a", "lobby_talk"),
            new ActorSpec("Atasan", NpcRole.Supervisor, "supervisor", false, "archive_3route", "executive_talk", "archive_2route", "lobby_talk"),
            new ActorSpec("Bos", NpcRole.Boss, "boss", true),

            new ActorSpec("Penjaga Arsip L1-1", NpcRole.ArchiveGuard, "lobby_guard", true),
            new ActorSpec("Penjaga Arsip L1-2", NpcRole.ArchiveGuard, "archive_1a", false, "archive_1b", "lobby_talk", "archive_1a"),
            new ActorSpec("Penjaga Arsip L1-3", NpcRole.ArchiveGuard, "archive_1b", false, "archive_1a", "work_1b", "archive_1b"),
            new ActorSpec("Penjaga Arsip L2-1", NpcRole.ArchiveGuard, "archive_2front", true),
            new ActorSpec("Penjaga Arsip L2-2", NpcRole.ArchiveGuard, "archive_2a", false, "archive_2route", "archive_2b", "archive_2a"),
            new ActorSpec("Penjaga Arsip L2-3", NpcRole.ArchiveGuard, "archive_2b", false, "archive_2a", "archive_2route", "archive_2b"),
            new ActorSpec("Penjaga Arsip L3-1", NpcRole.ArchiveGuard, "archive_3front", true),
            new ActorSpec("Penjaga Arsip L3-2", NpcRole.ArchiveGuard, "archive_3a", false, "archive_3route", "archive_3b", "archive_3a"),
            new ActorSpec("Penjaga Arsip L3-3", NpcRole.ArchiveGuard, "archive_3b", false, "archive_3a", "archive_3talk", "archive_3b"),
            new ActorSpec("Penjaga Arsip L4-1", NpcRole.ArchiveGuard, "archive_4front", true),
            new ActorSpec("Penjaga Arsip L4-2", NpcRole.ArchiveGuard, "archive_4a", false, "executive_talk", "archive_4b", "archive_4a"),
            new ActorSpec("Penjaga Arsip L4-3", NpcRole.ArchiveGuard, "archive_4b", false, "archive_4a", "executive_talk", "archive_4b"),

            new ActorSpec("Pekerja 01", NpcRole.Worker, "yard_west", false, "yard_far", "lobby_corridor", "yard_west"),
            new ActorSpec("Pekerja 02", NpcRole.Worker, "yard_east", false, "yard_guard", "yard_far", "yard_east"),
            new ActorSpec("Pekerja 03", NpcRole.Worker, "roof_1", false, "roof_west", "roof_lookout", "archive_4front", "roof_1"),
            new ActorSpec("Pekerja 04", NpcRole.Worker, "roof_2", false, "roof_east", "roof_lookout", "roof_2"),
            new ActorSpec("Pekerja 05", NpcRole.Worker, "work_1", false, "work_1b", "archive_2route", "lobby_talk"),
            new ActorSpec("Pekerja 06", NpcRole.Worker, "work_2", false, "work_2b", "archive_2route", "archive_3route"),
            new ActorSpec("Pekerja 07", NpcRole.Worker, "work_3", false, "archive_3route", "archive_2route", "work_3"),
        };

        [MenuItem("Saksi Terakhir/NPC/Place 25 Office Capsules")]
        public static void PlaceFromMenu() => Debug.Log(Build());

        public static string Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject building = GameObject.Find("Office_ArchiveHQ");
            GameObject system = GameObject.Find("Office NPC System");
            if (building == null || system == null)
                throw new InvalidOperationException("Office building or Office NPC System was not found in the scene.");
            OfficeNpcAgent[] existing = system.GetComponentsInChildren<OfficeNpcAgent>(true);
            int assignedProfiles = 0;
            foreach (OfficeNpcAgent actor in existing)
                if (actor.Profile != null) assignedProfiles++;
            if (assignedProfiles == ExpectedActors && existing.Length == ExpectedActors)
                return "The 25 NPCs already use ScriptableObject profiles; legacy waypoint placement was skipped.";
            if (assignedProfiles != 0)
                throw new InvalidOperationException("Some NPC profiles are assigned; do not recreate legacy waypoints.");
            if (existing.Length == ExpectedActors && system.GetComponent<OfficeNpcDirector>() != null)
                return UpgradeExistingWorker06(scene, building.transform, system, existing);
            if (existing.Length > 0 || system.transform.Find("NPC Activity Points") != null)
                throw new InvalidOperationException("A partial NPC setup exists; inspect it before rebuilding.");
            if (Cast.Length != ExpectedActors)
                throw new InvalidOperationException($"Expected {ExpectedActors} actors; authored {Cast.Length}.");

            NavMeshSurface surface = system.GetComponentInChildren<NavMeshSurface>(true);
            if (surface == null || surface.navMeshData == null)
                throw new InvalidOperationException("The saved Office Capsule NavMesh must exist before placing NPCs.");
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = NavMesh.AllAreas };
            Vector3 anchorRequested = building.transform.TransformPoint(new Vector3(-7f, 0f, -13f));
            if (!TrySample(anchorRequested, filter, out Vector3 anchor))
                throw new InvalidOperationException("The ground-floor navigation anchor is missing.");

            var sampled = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            foreach (PointSpec point in Points)
            {
                Vector3 requested = building.transform.TransformPoint(point.LocalPosition);
                if (!TrySample(requested, filter, out Vector3 position))
                    throw new InvalidOperationException($"No Office Capsule NavMesh at {point.Key} ({requested}).");
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(anchor, position, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException($"No complete path from lobby to {point.Key}.");
                if (!sampled.TryAdd(point.Key, position))
                    throw new InvalidOperationException($"Duplicate NPC point key: {point.Key}.");
            }
            foreach (ActorSpec actor in Cast)
            {
                if (!sampled.ContainsKey(actor.Home)) throw new InvalidOperationException($"Missing home: {actor.Name}.");
                foreach (string key in actor.Route)
                    if (!sampled.ContainsKey(key)) throw new InvalidOperationException($"Missing route point: {actor.Name}/{key}.");
            }

            GameObject pointsRoot = null;
            GameObject actorsRoot = null;
            try
            {
                EnsureMaterialFolder();
                pointsRoot = new GameObject("NPC Activity Points");
                pointsRoot.transform.SetParent(system.transform);
                actorsRoot = new GameObject("Capsule NPCs (25)");
                actorsRoot.transform.SetParent(system.transform);
                var points = new Dictionary<string, NpcActivityPoint>(StringComparer.Ordinal);
                foreach (PointSpec spec in Points)
                {
                    var holder = new GameObject(spec.Key);
                    holder.transform.SetParent(pointsRoot.transform);
                    holder.transform.position = sampled[spec.Key];
                    NpcActivityPoint activity = holder.AddComponent<NpcActivityPoint>();
                    activity.Configure(spec.Label, spec.Activity, spec.Zone, spec.Floor, spec.Dwell);
                    points.Add(spec.Key, activity);
                }

                var actors = new List<OfficeNpcAgent>(ExpectedActors);
                foreach (ActorSpec spec in Cast)
                {
                    NpcActivityPoint[] route = Array.ConvertAll(spec.Route, key => points[key]);
                    actors.Add(CreateActor(spec, points[spec.Home], route, sampled[spec.Home], surface.agentTypeID, actorsRoot.transform));
                }
                OfficeNpcDirector director = system.AddComponent<OfficeNpcDirector>();
                director.Configure(actors.ToArray());
                EditorUtility.SetDirty(system);
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the office scene.");
                return $"Placed {actors.Count} office capsule NPCs, {points.Count} activity points, 12 archive guards (three on each floor), and saved {ScenePath}.";
            }
            catch
            {
                OfficeNpcDirector director = system.GetComponent<OfficeNpcDirector>();
                if (director != null) UnityEngine.Object.DestroyImmediate(director);
                if (actorsRoot != null) UnityEngine.Object.DestroyImmediate(actorsRoot);
                if (pointsRoot != null) UnityEngine.Object.DestroyImmediate(pointsRoot);
                throw;
            }
        }

        private static string UpgradeExistingWorker06(Scene scene, Transform building, GameObject system,
            OfficeNpcAgent[] actors)
        {
            Transform pointsRoot = system.transform.Find("NPC Activity Points");
            if (pointsRoot == null) throw new InvalidOperationException("Existing NPC activity points are missing.");
            Transform existingPoint = pointsRoot.Find("work_2b");
            if (existingPoint != null)
                return "The 25 office capsules and dedicated worker waypoint are already present; no duplicates were created.";
            OfficeNpcAgent worker = Array.Find(actors, actor => actor.DisplayName == "Pekerja 06");
            if (worker == null || worker.Home == null)
                throw new InvalidOperationException("Pekerja 06 or its home point is missing.");
            NavMeshSurface surface = system.GetComponentInChildren<NavMeshSurface>(true);
            if (surface == null || surface.navMeshData == null)
                throw new InvalidOperationException("The Office Capsule NavMesh is missing.");
            PointSpec spec = Array.Find(Points, point => point.Key == "work_2b");
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = NavMesh.AllAreas };
            Vector3 requested = building.TransformPoint(spec.LocalPosition);
            if (!TrySample(requested, filter, out Vector3 sampled))
                throw new InvalidOperationException($"No Office Capsule NavMesh at work_2b ({requested}).");
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(worker.Home.transform.position, sampled, filter, path)
                || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Pekerja 06 cannot reach its dedicated waypoint.");
            GameObject holder = new GameObject(spec.Key);
            holder.transform.SetParent(pointsRoot);
            holder.transform.position = sampled;
            NpcActivityPoint activity = holder.AddComponent<NpcActivityPoint>();
            activity.Configure(spec.Label, spec.Activity, spec.Zone, spec.Floor, spec.Dwell);
            NpcActivityPoint[] route = { activity, FindPoint(pointsRoot, "archive_2route"), FindPoint(pointsRoot, "archive_3route") };
            worker.Configure(worker.DisplayName, worker.Role, worker.Home, route, false);
            EditorUtility.SetDirty(worker);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the updated worker route.");
            return "Updated Pekerja 06 with a dedicated work waypoint; 25 NPCs retained.";
        }

        private static NpcActivityPoint FindPoint(Transform root, string key)
        {
            Transform child = root.Find(key);
            if (child == null || !child.TryGetComponent(out NpcActivityPoint point))
                throw new InvalidOperationException($"Existing NPC point {key} is missing.");
            return point;
        }

        private static bool TrySample(Vector3 requested, NavMeshQueryFilter filter, out Vector3 position)
        {
            if (NavMesh.SamplePosition(requested, out NavMeshHit hit, 0.6f, filter)
                && Mathf.Abs(hit.position.y - requested.y) <= 0.4f)
            {
                position = hit.position;
                return true;
            }
            position = default;
            return false;
        }

        private static OfficeNpcAgent CreateActor(ActorSpec spec, NpcActivityPoint home, NpcActivityPoint[] route,
            Vector3 position, int agentType, Transform parent)
        {
            var root = new GameObject(spec.Name);
            root.transform.SetParent(parent);
            root.transform.position = position;
            NavMeshAgent navigation = root.AddComponent<NavMeshAgent>();
            navigation.agentTypeID = agentType;
            navigation.radius = 0.3f;
            navigation.height = 1.9f;
            navigation.baseOffset = 0f;
            navigation.speed = spec.Role == NpcRole.Security ? 1.65f : 1.35f;
            navigation.acceleration = 5f;
            navigation.angularSpeed = 240f;
            navigation.stoppingDistance = 0.22f;
            navigation.autoTraverseOffMeshLink = true;
            navigation.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            navigation.avoidancePriority = 50 + (Mathf.Abs(spec.Name.GetHashCode()) % 30);

            CapsuleCollider body = root.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 0.95f, 0f);
            body.height = 1.9f;
            body.radius = 0.34f;
            Rigidbody rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Capsule Visual";
            capsule.transform.SetParent(root.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            capsule.transform.localScale = new Vector3(0.7f, 0.95f, 0.7f);
            UnityEngine.Object.DestroyImmediate(capsule.GetComponent<Collider>());
            capsule.GetComponent<Renderer>().sharedMaterial = MaterialFor(spec.Role);
            var facing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            facing.name = "Facing Marker";
            facing.transform.SetParent(root.transform, false);
            facing.transform.localPosition = new Vector3(0f, 1.45f, 0.34f);
            facing.transform.localScale = new Vector3(0.14f, 0.14f, 0.05f);
            UnityEngine.Object.DestroyImmediate(facing.GetComponent<Collider>());
            facing.GetComponent<Renderer>().sharedMaterial = MaterialFor(NpcRole.Boss);

            var labelRoot = new GameObject("NPC Label");
            labelRoot.transform.SetParent(root.transform, false);
            labelRoot.transform.localPosition = new Vector3(0f, 2.18f, 0f);
            NpcLabel label = labelRoot.AddComponent<NpcLabel>();
            TextMeshPro name = CreateText("Name", spec.Name, labelRoot.transform, 0f, Color.white);
            TextMeshPro speech = CreateText("Speech", string.Empty, labelRoot.transform, 0.28f, new Color(1f, 0.9f, 0.45f));
            label.Configure(name, speech);

            OfficeNpcAgent actor = root.AddComponent<OfficeNpcAgent>();
            actor.Configure(spec.Name, spec.Role, home, route, spec.Stationary);
            return actor;
        }

        private static TextMeshPro CreateText(string objectName, string content, Transform parent, float offsetY, Color color)
        {
            var holder = new GameObject(objectName, typeof(TextMeshPro));
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = new Vector3(0f, offsetY, 0f);
            holder.transform.localScale = Vector3.one * 0.1f;
            TextMeshPro text = holder.GetComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = new Vector2(5f, 0.8f);
            text.fontSize = 3.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.text = content;
            return text;
        }

        private static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Generated"))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Generated");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Generated", "Npc");
        }

        private static Material MaterialFor(NpcRole role)
        {
            string path = $"{MaterialFolder}/NPC_{role}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No supported shader for capsule NPC materials.");
            material = new Material(shader) { name = $"NPC_{role}" };
            Color color = RoleColor(role);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Color RoleColor(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.Security: return new Color(0.18f, 0.27f, 0.38f);
                case NpcRole.Receptionist: return new Color(0.23f, 0.72f, 0.7f);
                case NpcRole.ColleagueA: return new Color(0.26f, 0.56f, 0.91f);
                case NpcRole.ColleagueB: return new Color(0.47f, 0.54f, 0.92f);
                case NpcRole.Supervisor: return new Color(0.67f, 0.38f, 0.75f);
                case NpcRole.Boss: return new Color(0.24f, 0.19f, 0.31f);
                case NpcRole.ArchiveGuard: return new Color(0.92f, 0.64f, 0.27f);
                default: return new Color(0.45f, 0.76f, 0.42f);
            }
        }
    }
}
