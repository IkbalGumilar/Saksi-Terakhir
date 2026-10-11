using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    /// <summary>Authors population markers; the population manager creates actors only during gameplay.</summary>
    public static class WorldNpcSceneSetup
    {
        private const string MainScene = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string SystemName = "World NPC System";
        private const string ProfileFolder = "Assets/_Project/Data/Npc/Population";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Npc";
        private const string NpcPrefabPath = PrefabFolder + "/NPC_Population_Base.prefab";
        private const string DocumentPrefabPath = PrefabFolder + "/NPC_Map_Dokumen.prefab";
        private const string MopPrefabPath = "Assets/ModularAsset/Perlengkapan/Prefabs/Operasional/OAA_Cleaning_Mop.prefab";
        private const string MaterialFolder = "Assets/_Project/Art/Generated/Npc";
        private static readonly string[] Names =
        {
            "Aditya", "Laras", "Farhan", "Nisa", "Dian", "Eko", "Irfan", "Rani", "Tari", "Dika",
            "Aldi", "Hani", "Wahyu", "Reno", "Nina", "Gilang", "Sari", "Ilham", "Dita", "Fikri"
        };

        private sealed class BuildContext
        {
            public Transform Root;
            public Transform Office;
            public NavMeshQueryFilter Filter;
            public bool HasNavigation;
            public GameObject DocumentPrefab;
            public GameObject MopPrefab;
            public readonly List<NpcSpawnPoint> Points = new List<NpcSpawnPoint>();
            public readonly List<string> Skipped = new List<string>();
            public readonly HashSet<string> Ids = new HashSet<string>(StringComparer.Ordinal);
            public int Seats;
            public int Indoor;
            public int Outdoor;
        }

        [MenuItem("Saksi Terakhir/NPC/Set Up World Population")]
        public static void BuildFromMenu() => Debug.Log(BuildForActiveScene());

        public static string BuildForActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("NPC markers must be authored outside Play Mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != MainScene)
                throw new InvalidOperationException("Open Regional Archive Office before authoring its NPC population.");
            GameObject existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == SystemName);
            if (existing != null)
            {
                NpcPopulationManager manager = existing.GetComponent<NpcPopulationManager>();
                NpcSpawnPoint[] points = existing.GetComponentsInChildren<NpcSpawnPoint>(true);
                if (manager == null || points.Length == 0)
                    throw new InvalidOperationException("A partial World NPC System exists. Preserve it and inspect its markers before rebuilding.");
                return $"World NPC System already exists with {points.Length} markers; no duplicate actors or markers were created.";
            }

            Transform environment = FindSceneTransform(scene, "Lingkungan_Kawasan_Arsip");
            Transform office = FindDescendant(environment, "Kantor_Arsip_Modular_60x20_4L_Rooftop");
            if (environment == null || office == null)
                throw new InvalidOperationException("The migrated office environment is missing from the active scene.");
            PlayerController player = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PlayerController>(true))
                .FirstOrDefault(candidate => candidate.gameObject.activeInHierarchy);
            if (player == null) throw new InvalidOperationException("An active MC is required as the population observer.");
            Camera camera = player.GetComponentInChildren<Camera>(true)
                ?? scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .FirstOrDefault(candidate => candidate.enabled && candidate.CompareTag("MainCamera"));
            if (camera == null) throw new InvalidOperationException("The MC camera is required for the population budget.");

            EnsureFolder(ProfileFolder);
            EnsureFolder(PrefabFolder);
            GameObject npcPrefab = EnsureNpcPrefab();
            GameObject documents = EnsureDocumentPrefab();
            NavMeshSurface surface = environment.GetComponentInChildren<NavMeshSurface>(true);
            if (surface == null || surface.navMeshData == null)
                throw new InvalidOperationException("The authored environment NavMesh is unavailable; no NPC markers were installed.");
            var context = new BuildContext
            {
                Office = office,
                HasNavigation = true,
                Filter = new NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = NavMesh.AllAreas },
                DocumentPrefab = documents,
                MopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MopPrefabPath)
            };
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Author world NPC population");
            GameObject system = new GameObject(SystemName);
            Undo.RegisterCreatedObjectUndo(system, "Create world NPC population");
            SceneManager.MoveGameObjectToScene(system, scene);
            context.Root = system.transform;
            try
            {
                BuildCampus(context);
                BuildStreetFrontages(context);
                BuildOffice(context);
                BuildWarehouses(context, environment);
                BuildBusinesses(context, environment);
                BuildPublicSeats(context, environment);
                if (context.Points.Count == 0)
                    throw new InvalidOperationException("No usable population markers could be positioned on the existing navigation data.");

                OfficeNpcDirector director = system.AddComponent<OfficeNpcDirector>();
                director.SetBuildingRoot(office);
                director.SetAutomaticWaypointBinding(false);
                director.Configure(Array.Empty<OfficeNpcAgent>());
                NpcPopulationManager population = system.AddComponent<NpcPopulationManager>();
                population.Configure(player.transform, camera, npcPrefab, context.Points.ToArray(), director, 15);
                EditorUtility.SetDirty(population);
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("The world NPC marker scene could not be saved.");
                Undo.CollapseUndoOperations(undo);
                string report = $"Created {context.Points.Count} NPC spawn/work markers ({context.Outdoor} outdoors, "
                    + $"{context.Indoor} indoors), including {context.Seats} optional seat/typing posts. Maximum visible NPCs: 15. "
                    + "NPCs were not spawned; navigation and occlusion were not baked.";
                if (context.Skipped.Count > 0)
                    report += $" Skipped {context.Skipped.Count} unavailable posts: " + string.Join("; ", context.Skipped.Take(12));
                return report;
            }
            catch
            {
                if (system != null) UnityEngine.Object.DestroyImmediate(system);
                throw;
            }
        }

        private static void BuildCampus(BuildContext context)
        {
            Transform group = Group(context.Root, "01_Halaman_Kantor_dan_Gudang");
            AddPost(context, group, "campus.security.welcome", new Vector3(32f, 0f, -4f),
                NpcBuildingContext.OfficeArchive, NpcOccupation.Security, NpcDepartment.Security,
                NpcDuty.Stationary, false, null, null, true, 1f, true);
            Vector3[][] routes =
            {
                new[] { new Vector3(20,0,-6), new Vector3(42,0,-6), new Vector3(42,0,-15), new Vector3(20,0,-15) },
                new[] { new Vector3(8,0,-14), new Vector3(8,0,-32), new Vector3(23,0,-32), new Vector3(23,0,-14) },
                new[] { new Vector3(47,0,-14), new Vector3(47,0,-32), new Vector3(55,0,-32), new Vector3(55,0,-14) },
                new[] { new Vector3(-10,0,-6), new Vector3(-10,0,24), new Vector3(-10,0,52), new Vector3(-10,0,24) },
                new[] { new Vector3(69,0,4), new Vector3(69,0,24), new Vector3(69,0,54), new Vector3(69,0,24) },
                new[] { new Vector3(25,0,36), new Vector3(25,0,54) },
                new[] { new Vector3(25,0,70), new Vector3(25,0,86) },
                new[] { new Vector3(25,0,102), new Vector3(25,0,118) },
                new[] { new Vector3(-4,0,57), new Vector3(10,0,57), new Vector3(20,0,57) },
                new[] { new Vector3(40,0,89), new Vector3(50,0,89), new Vector3(64,0,89) }
            };
            for (int i = 0; i < routes.Length; i++)
            {
                bool cleaner = i == 1 || i == 4;
                bool documents = i >= 5;
                AddPost(context, group, $"campus.route.{i + 1:00}", routes[i][0], NpcBuildingContext.OfficeArchive,
                    cleaner ? NpcOccupation.Cleaner : documents ? NpcOccupation.Courier : NpcOccupation.Security,
                    cleaner ? NpcDepartment.Facilities : documents ? NpcDepartment.Logistics : NpcDepartment.Security,
                    cleaner ? NpcDuty.Cleaning : documents ? NpcDuty.CarryDocuments : NpcDuty.Wander,
                    false, routes[i], null, false, 1f, i % 3 == 0);
                if (i >= 5 && context.Points.Count > 0 && context.Points[context.Points.Count - 1].Id == $"campus.route.{i + 1:00}")
                    context.Points[context.Points.Count - 1].SetRouteHalfWidth(.65f);
            }
        }

        private static void BuildStreetFrontages(BuildContext context)
        {
            Transform group = Group(context.Root, "02_Pejalan_Kaki_Trotoar");
            // The road is Z=-62, X=90. These strips are the walking paving, not the planted tree strip.
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -57f : -67f;
                for (int i = 0; i < 11; i++)
                {
                    float x = -200f + i * 24f;
                    // Stop before the gate approaches: a route never shortcuts across the traffic lanes.
                    float end = Mathf.Min(x + 20f, 18f);
                    if (x >= 10f) end = x - 16f;
                    Vector3[] route = { new Vector3(x, .15f, z), new Vector3(end, .15f, z) };
                    AddPost(context, group, $"street.front.{side}.{i + 1:00}", route[0],
                        NpcBuildingContext.Outdoors, NpcOccupation.Visitor, NpcDepartment.General,
                        NpcDuty.Wander, false, route, null, false, .85f, i % 4 == 0);
                }
            }
            for (int i = 0; i < 6; i++)
            {
                float z = -8f + i * 24f;
                Vector3[] route = { new Vector3(85f, .15f, z), new Vector3(85f, .15f, z + 18f) };
                AddPost(context, group, $"street.east.{i + 1:00}", route[0], NpcBuildingContext.Outdoors,
                    NpcOccupation.Visitor, NpcDepartment.General, NpcDuty.Wander, false, route,
                    null, false, .75f, i % 3 == 0);
            }
        }

        private static void BuildOffice(BuildContext context)
        {
            Transform group = Group(context.Root, "03_Kantor_Arsip");
            Transform[] levels = context.Office.GetComponentsInChildren<Transform>(true)
                .Where(t => t.parent == context.Office && t.name.Contains("Lantai_0"))
                .OrderBy(t => t.position.y).ToArray();
            int floor = 0;
            foreach (Transform level in levels)
            {
                floor++;
                Transform floorGroup = Group(group, $"Lantai_{floor:00}");
                Vector3[] cleanerRoute = { At(level,10,10), At(level,24,10), At(level,38,10), At(level,50,10) };
                AddPost(context, floorGroup, $"office.floor{floor}.cleaner", cleanerRoute[0],
                    NpcBuildingContext.OfficeArchive, NpcOccupation.Cleaner, NpcDepartment.Facilities,
                    NpcDuty.Cleaning, true, cleanerRoute, null, false, 1f, floor % 2 == 0, floor);
                Vector3[] courierRoute = { At(level,14,10), At(level,28,10), At(level,42,10), At(level,54,10) };
                AddPost(context, floorGroup, $"office.floor{floor}.courier", courierRoute[2],
                    NpcBuildingContext.OfficeArchive, NpcOccupation.Courier, NpcDepartment.Logistics,
                    NpcDuty.CarryDocuments, true, courierRoute, null, false, 1f, true, floor);
                Transform guard = FindDescendant(level, $"Pos_Penjaga_Lantai_{floor}");
                if (guard != null)
                    AddPost(context, floorGroup, $"office.floor{floor}.archiveguard", guard.position,
                        NpcBuildingContext.OfficeArchive, NpcOccupation.Archivist, NpcDepartment.Archives,
                        NpcDuty.Stationary, true, null, null, false, 1f, true, floor);
                if (floor == 1)
                {
                    Transform receptionist = FindDescendant(level, "Resepsionis");
                    if (receptionist != null)
                        AddPost(context, floorGroup, "office.reception.helper", receptionist.position,
                            NpcBuildingContext.OfficeArchive, NpcOccupation.Receptionist, NpcDepartment.Reception,
                            NpcDuty.Stationary, true, null, null, false, 1f, true, floor);
                }

                Transform rooms = FindDescendant(level, "07_Isi_Ruangan");
                if (rooms == null) continue;
                foreach (Transform room in rooms)
                {
                    if (room.name.Contains("Kepala") || room.name.Contains("Kerja Raka")
                        || room.name.Contains("MC dan Sinta") || room.name.Contains("Toilet")
                        || room.name.Contains("Utilitas")) continue;
                    Transform[] desks = FindOutermostMatches(room,
                        t => t.name == "Meja_Kerja_Arsip" || t.name.StartsWith("Meja_Makan", StringComparison.Ordinal))
                        .ToArray();
                    Transform[] chairs = FindOutermostMatches(room,
                        t => t.name == "Kursi_Kantor" || t.name == "Kursi_Makan")
                        .OrderBy(t => t.position.x).ThenBy(t => t.position.z).ToArray();
                    int seat = 0;
                    foreach (Transform chair in chairs)
                    {
                        Transform desk = desks.OrderBy(candidate => (candidate.position - chair.position).sqrMagnitude).FirstOrDefault();
                        if (desk == null || Vector3.Distance(desk.position, chair.position) > 2.1f) continue;
                        bool dining = room.name.Contains("Kantin") || room.name.Contains("Istirahat");
                        NpcDepartment department = room.name.Contains("Administrasi") || room.name.Contains("Sekretaris")
                            ? NpcDepartment.Administration : room.name.Contains("Pengolahan") || room.name.Contains("Pemeriksaan")
                                ? NpcDepartment.Digitization : NpcDepartment.Archives;
                        if (!AddSeat(context, floorGroup, $"office.floor{floor}.{Slug(room.name)}.seat{seat + 1}",
                            chair, desk, dining ? NpcBuildingContext.Canteen : NpcBuildingContext.OfficeArchive,
                            dining ? NpcOccupation.Visitor : department == NpcDepartment.Administration
                                ? NpcOccupation.Clerk : NpcOccupation.Archivist,
                            department, !dining, true, floor)) continue;
                        if (++seat >= 2) break;
                    }
                }
            }
            Transform rooftop = context.Office.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.parent == context.Office && t.name.Contains("Rooftop"));
            if (rooftop != null)
            {
                Vector3[] roofRoute = { At(rooftop,18,10), At(rooftop,30,10), At(rooftop,42,10) };
                AddPost(context, group, "office.rooftop.maintenance", roofRoute[0], NpcBuildingContext.OfficeArchive,
                    NpcOccupation.Technician, NpcDepartment.Facilities, NpcDuty.Wander, true,
                    roofRoute, null, false, 1f, false, 5);
            }
        }

        private static void BuildWarehouses(BuildContext context, Transform environment)
        {
            Transform warehouseRoot = FindDescendant(environment, "Enam_Gudang_Arsip");
            if (warehouseRoot == null) return;
            Transform group = Group(context.Root, "04_Gudang_Arsip");
            Transform[] warehouses = warehouseRoot.Cast<Transform>().OrderBy(t => t.position.z).ThenBy(t => t.position.x).ToArray();
            int warehouseNumber = 0;
            foreach (Transform warehouse in warehouses)
            {
                Transform anchors = FindDescendant(warehouse, "10_Titik_Penempatan");
                if (anchors == null) continue;
                warehouseNumber++;
                Transform localGroup = Group(group, $"Gudang_{warehouseNumber:00}");
                Vector3[] route = { At(warehouse,15,4.5f), At(warehouse,15,11), At(warehouse,15,18.5f) };
                AddPost(context, localGroup, $"warehouse.{warehouseNumber:00}.courier", route[0],
                    NpcBuildingContext.Warehouse, NpcOccupation.Courier, NpcDepartment.Logistics,
                    NpcDuty.CarryDocuments, true, route, null, false, 1f, warehouseNumber % 2 == 0);
                Vector3[] cleaningRoute = { At(warehouse,9.7f,4.5f), At(warehouse,15,4.5f), At(warehouse,20.3f,4.5f) };
                AddPost(context, localGroup, $"warehouse.{warehouseNumber:00}.cleaner", cleaningRoute[0],
                    NpcBuildingContext.Warehouse, NpcOccupation.Cleaner, NpcDepartment.Facilities,
                    NpcDuty.Cleaning, true, cleaningRoute, null, false, 1f, false);
                Transform[] desks = FindOutermostMatches(warehouse,
                    t => t.name == "Meja_Kerja_Arsip" || t.name.StartsWith("Meja_Petugas_", StringComparison.Ordinal)).ToArray();
                Transform[] chairs = FindOutermostMatches(warehouse,
                    t => t.name == "Kursi_Kantor" || t.name.StartsWith("Kursi_Petugas_", StringComparison.Ordinal)).OrderBy(t => t.position.x).ToArray();
                foreach (Transform chair in chairs.Take(1))
                {
                    Transform desk = desks.OrderBy(t => (t.position - chair.position).sqrMagnitude).FirstOrDefault();
                    if (desk != null)
                        AddSeat(context, localGroup, $"warehouse.{warehouseNumber:00}.clerk", chair, desk,
                            NpcBuildingContext.Warehouse, NpcOccupation.Clerk, NpcDepartment.Logistics, true, true, 0);
                }
            }
        }

        private static void BuildBusinesses(BuildContext context, Transform environment)
        {
            Transform group = Group(context.Root, "05_Usaha_dan_Teras");
            Transform neighborhood = FindDescendant(environment, "Kawasan_Perumahan_dan_Perdagangan");
            Transform shops = FindDescendant(neighborhood, "01_Toko_Dua_Sisi_Jalan");
            if (shops != null)
            {
                foreach (Transform shop in shops)
                {
                    NpcBuildingContext building = ContextForShop(shop.name);
                    string id = "business." + Slug(shop.name);
                    // Shop interiors have authored closed doors; their social posts stay on the public terrace.
                    float streetZ = shop.position.z > -62f ? -53.5f : -70.5f;
                    Vector3 center = new Vector3(shop.position.x + 6f, .15f, streetZ);
                    Vector3[] customerRoute = { center + Vector3.left * 2f, center + Vector3.right * 2f };
                    AddPost(context, group, id + ".merchant", center, building, NpcOccupation.Merchant,
                        NpcDepartment.Sales, NpcDuty.Stationary, false, null, null, false, .85f, true);
                    AddPost(context, group, id + ".customer", customerRoute[0], building, NpcOccupation.Visitor,
                        NpcDepartment.Sales, NpcDuty.Wander, false, customerRoute, null, false, .55f, false);
                }
            }
            Transform canteen = FindDescendant(neighborhood, "Rumah_Makan_Selatan");
            if (canteen != null)
            {
                Vector3[] servingRoute = { At(canteen,10,8), At(canteen,14,8), At(canteen,14,5) };
                AddPost(context, group, "canteen.cook", servingRoute[0], NpcBuildingContext.Canteen,
                    NpcOccupation.Cook, NpcDepartment.Sales, NpcDuty.Wander, true, servingRoute,
                    null, false, 1f, true);
                Transform[] tables = FindOutermostMatches(canteen,
                    t => t.name.StartsWith("Meja_Makan", StringComparison.Ordinal)).ToArray();
                Transform[] chairs = FindOutermostMatches(canteen,
                    t => t.name == "Kursi_Makan").OrderBy(t => t.position.x).ThenBy(t => t.position.z).ToArray();
                int number = 0;
                foreach (Transform chair in chairs)
                {
                    Transform table = tables.OrderBy(t => (t.position - chair.position).sqrMagnitude).FirstOrDefault();
                    if (table != null && AddSeat(context, group, $"canteen.customer.seat{number + 1}", chair, table,
                        NpcBuildingContext.Canteen, NpcOccupation.Visitor, NpcDepartment.General, false, true, 0))
                        number++;
                    if (number >= 4) break;
                }
                AddPost(context, group, "canteen.terrace.merchant", At(canteen,12,-3), NpcBuildingContext.Canteen,
                    NpcOccupation.Merchant, NpcDepartment.Sales, NpcDuty.Stationary, false,
                    null, null, false, 1f, true);
            }

            Transform expansion = FindDescendant(environment, "Pengembangan_Kawasan_Kenanga");
            Transform spbu = FindDescendant(expansion, "08_SPBU_dan_Supermarket");
            if (spbu != null)
            {
                Vector3[] serviceRoute = { new Vector3(6,0,280), new Vector3(22,0,280), new Vector3(38,0,280) };
                AddPost(context, group, "spbu.attendant", serviceRoute[0], NpcBuildingContext.GeneralStore,
                    NpcOccupation.Technician, NpcDepartment.Sales, NpcDuty.Wander, false,
                    serviceRoute, null, false, 1f, true);
                AddPost(context, group, "spbu.supermarket.merchant", new Vector3(56,.15f,291), NpcBuildingContext.GeneralStore,
                    NpcOccupation.Merchant, NpcDepartment.Sales, NpcDuty.Stationary, false,
                    null, null, false, 1f, true);
            }
        }

        private static void BuildPublicSeats(BuildContext context, Transform environment)
        {
            Transform group = Group(context.Root, "06_Bangku_Taman_Opsional");
            foreach (string environmentName in new[] { "Kawasan_Perumahan_dan_Perdagangan", "Pengembangan_Kawasan_Kenanga" })
            {
                Transform area = FindDescendant(environment, environmentName);
                if (area == null) continue;
                Transform[] benches = FindOutermostMatches(area,
                    t => t.name == "Bangku_Taman").OrderBy(t => t.position.x).ThenBy(t => t.position.z).ToArray();
                for (int i = 0; i < benches.Length; i += 2)
                    AddSeat(context, group, $"park.{Slug(environmentName)}.bench{i + 1:00}", benches[i], null,
                        NpcBuildingContext.Outdoors, NpcOccupation.Visitor, NpcDepartment.General, false, false, 0);
            }
        }

        private static bool AddSeat(BuildContext context, Transform group, string id, Transform chair, Transform desk,
            NpcBuildingContext building, NpcOccupation occupation, NpcDepartment department, bool typing, bool indoors, int floor)
        {
            Vector3 forward = desk != null ? desk.position - chair.position : chair.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            forward.Normalize();
            // Furniture pivots are usually on the floor. A few prototype benches already have a .45m pivot.
            float floorY = indoors ? FindFloorHeight(chair) : chair.position.y > .3f ? chair.position.y - .45f : chair.position.y;
            Vector3 floorPosition = new Vector3(chair.position.x, floorY, chair.position.z);
            Vector3 approach = floorPosition - forward * .9f;
            if (!TryPosition(context, approach, out Vector3 home))
            {
                context.Skipped.Add(id + " (no clear chair approach)");
                return false;
            }
            Transform pointGroup = Group(group, id);
            pointGroup.position = home;
            Transform seat = Group(pointGroup, "Seat_Anchor");
            seat.position = floorPosition + Vector3.up * .46f;
            seat.rotation = Quaternion.LookRotation(forward, Vector3.up);
            bool added = AddPost(context, pointGroup, id, home, building, occupation, department,
                typing ? NpcDuty.Typing : NpcDuty.SeatedWork, indoors, null, seat, false, .45f,
                StableHash(id) % 3 == 0, floor);
            if (added) context.Seats++;
            return added;
        }

        private static bool AddPost(BuildContext context, Transform group, string id, Vector3 requested,
            NpcBuildingContext building, NpcOccupation occupation, NpcDepartment department, NpcDuty duty,
            bool indoors, Vector3[] requestedRoute, Transform seat = null, bool greetOnce = false,
            float chance = 1f, bool interactable = false, int floor = 0)
        {
            if (!TryPosition(context, requested, out Vector3 position))
            {
                context.Skipped.Add(id + " (no nearby walkable surface)");
                return false;
            }
            if (!context.Ids.Add(id)) throw new InvalidOperationException("Duplicate population post: " + id);
            Transform pointRoot = seat != null ? group : Group(group, id);
            pointRoot.position = position;
            NpcActivityKind activityKind = duty == NpcDuty.Typing || duty == NpcDuty.SeatedWork
                ? NpcActivityKind.Work : duty == NpcDuty.Stationary ? NpcActivityKind.Idle : NpcActivityKind.Patrol;
            NpcZone zone = indoors ? floor == 5 ? NpcZone.Roof : NpcZone.Interior : NpcZone.Yard;
            NpcActivityPoint home = Group(pointRoot, "Home").gameObject.AddComponent<NpcActivityPoint>();
            home.transform.position = position;
            home.Configure(id, activityKind, zone, floor, duty == NpcDuty.Stationary
                ? new Vector2(20,35) : new Vector2(4,10));
            var route = new List<NpcActivityPoint>();
            if (requestedRoute != null)
            {
                for (int i = 0; i < requestedRoute.Length; i++)
                {
                    if (!TryPosition(context, requestedRoute[i], out Vector3 routePosition))
                    {
                        context.Skipped.Add(id + $"/route{i} (unavailable waypoint)");
                        continue;
                    }
                    NpcActivityPoint waypoint = Group(pointRoot, $"Waypoint_{i + 1:00}").gameObject.AddComponent<NpcActivityPoint>();
                    waypoint.transform.position = routePosition;
                    waypoint.Configure(id + "/" + i, activityKind, zone, floor, new Vector2(4,12));
                    route.Add(waypoint);
                }
            }
            // A walking job needs at least two distinct authored waypoints. An unavailable route remains a stationary post.
            if (requestedRoute != null && route.Count < 2) duty = NpcDuty.Stationary;
            var profiles = new NpcProfile[2];
            for (int i = 0; i < profiles.Length; i++)
            {
                string profileId = "npc.population." + id + "." + i;
                string path = ProfileFolder + "/" + profileId.Replace('.', '_') + ".asset";
                NpcProfile profile = AssetDatabase.LoadAssetAtPath<NpcProfile>(path);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<NpcProfile>();
                    AssetDatabase.CreateAsset(profile, path);
                }
                int hash = StableHash(profileId);
                NpcRole role = occupation == NpcOccupation.Security ? NpcRole.Security
                    : occupation == NpcOccupation.Receptionist ? NpcRole.Receptionist
                    : occupation == NpcOccupation.Archivist ? NpcRole.ArchiveGuard : NpcRole.Worker;
                profile.Configure(profileId, Names[hash % Names.Length], 22 + hash % 29, 2f + (hash % 5) * .1f,
                    role, duty == NpcDuty.Stationary || duty == NpcDuty.Typing || duty == NpcDuty.SeatedWork,
                    null, Array.Empty<NpcWaypointDefinition>(), Array.Empty<string>(), Array.Empty<string>());
                profile.SetContext(building, occupation, department);
                profile.SetInteractable(interactable);
                profile.SetTrafficPriority(occupation == NpcOccupation.Security ? 3 : 1);
                EditorUtility.SetDirty(profile);
                profiles[i] = profile;
            }
            NpcSpawnPoint point = pointRoot.gameObject.AddComponent<NpcSpawnPoint>();
            GameObject equipment = duty == NpcDuty.Cleaning ? context.MopPrefab
                : duty == NpcDuty.CarryDocuments ? context.DocumentPrefab : null;
            point.Configure(id, profiles, home, route.ToArray(), indoors, duty, seat, equipment, greetOnce, chance);
            point.SetRouteHalfWidth(id.StartsWith("street.", StringComparison.Ordinal) ? .65f
                : indoors ? 2f : 6f);
            context.Points.Add(point);
            if (indoors) context.Indoor++; else context.Outdoor++;
            return true;
        }

        private static bool TryPosition(BuildContext context, Vector3 requested, out Vector3 position)
        {
            // This positions markers against existing data; it does not create agents, paths, or navigation data.
            if (context.HasNavigation && NavMesh.SamplePosition(requested + Vector3.up * .05f,
                out NavMeshHit hit, 1.1f, context.Filter) && Mathf.Abs(hit.position.y - requested.y) <= .45f)
            {
                position = hit.position;
                return true;
            }
            position = default;
            return false;
        }

        private static float FindFloorHeight(Transform furniture)
        {
            for (Transform parent = furniture.parent; parent != null; parent = parent.parent)
                if (parent.name.Contains("Lantai_0") || parent.name.StartsWith("Gudang_", StringComparison.Ordinal)
                    || parent.name.Contains("Rumah_Makan")) return parent.position.y;
            return furniture.position.y;
        }

        private static Vector3 At(Transform root, float x, float z) => root.TransformPoint(new Vector3(x, 0f, z));
        private static Transform Group(Transform parent, string name)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            return holder.transform;
        }

        private static Transform FindSceneTransform(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!root.activeInHierarchy) continue;
                Transform found = FindDescendant(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindDescendant(Transform root, string name) => root == null ? null
            : root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        private static IEnumerable<Transform> FindOutermostMatches(Transform scope, Func<Transform, bool> predicate)
        {
            if (scope == null) yield break;
            Transform[] matches = scope.GetComponentsInChildren<Transform>(true).Where(predicate).ToArray();
            var matchingTransforms = new HashSet<Transform>(matches);
            foreach (Transform candidate in matches)
            {
                // A furniture prefab and its imported visual may share a name; keep the physical root once.
                bool nestedMatch = false;
                for (Transform parent = candidate.parent; parent != null && parent != scope.parent; parent = parent.parent)
                {
                    if (!matchingTransforms.Contains(parent)) continue;
                    nestedMatch = true;
                    break;
                }
                if (!nestedMatch) yield return candidate;
            }
        }

        private static NpcBuildingContext ContextForShop(string name)
        {
            if (name.Contains("Elektronik")) return NpcBuildingContext.ElectronicsStore;
            if (name.Contains("Material")) return NpcBuildingContext.HardwareStore;
            if (name.Contains("Kopi") || name.Contains("Kedai")) return NpcBuildingContext.Canteen;
            return NpcBuildingContext.GeneralStore;
        }

        private static string Slug(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        private static int StableHash(string value)
        {
            uint hash = 2166136261;
            foreach (char character in value) hash = (hash ^ character) * 16777619;
            return (int)(hash & 0x7fffffff);
        }

        private static GameObject EnsureNpcPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            if (existing != null) return existing;
            var root = new GameObject("NPC_Population_Base");
            try
            {
                NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
                agent.radius = .3f;
                agent.height = 1.9f;
                agent.speed = 2f;
                agent.acceleration = 5f;
                agent.angularSpeed = 240f;
                agent.stoppingDistance = .22f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0,.95f,0);
                collider.height = 1.9f;
                collider.radius = .34f;
                Rigidbody rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                NavMeshModifier modifier = root.AddComponent<NavMeshModifier>();
                modifier.ignoreFromBuild = true;
                root.AddComponent<OfficeNpcAgent>();
                root.AddComponent<NpcInteractable>();
                root.AddComponent<NpcPopulationMember>();
                root.AddComponent<NpcWorkRoutine>();
                root.AddComponent<NpcGreetingTrigger>().enabled = false;
                Material worker = MaterialFor("NPC_Worker", new Color(.45f,.76f,.42f));
                CreatePrimitive("Capsule Visual", PrimitiveType.Capsule, root.transform,
                    new Vector3(0,.95f,0), new Vector3(.7f,.95f,.7f), worker);
                CreatePrimitive("Facing Marker", PrimitiveType.Cube, root.transform,
                    new Vector3(0,1.45f,.34f), new Vector3(.14f,.14f,.05f),
                    MaterialFor("NPC_Boss", new Color(.24f,.19f,.31f)));
                Transform trigger = Group(root.transform, "NPC Interaction Trigger");
                trigger.gameObject.layer = 6;
                CapsuleCollider interaction = trigger.gameObject.AddComponent<CapsuleCollider>();
                interaction.center = collider.center;
                interaction.height = collider.height;
                interaction.radius = .36f;
                interaction.isTrigger = true;
                Transform labels = Group(root.transform, "NPC Label");
                labels.localPosition = Vector3.up * 2.15f;
                NpcLabel label = labels.gameObject.AddComponent<NpcLabel>();
                TextMeshPro name = CreateText(labels, "Name", Color.white, 0f);
                TextMeshPro speech = CreateText(labels, "Speech", new Color(1f,.9f,.45f), .28f);
                label.Configure(name, speech);
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                GameObject result = PrefabUtility.SaveAsPrefabAsset(root, NpcPrefabPath);
                if (result == null) throw new InvalidOperationException("Could not create the population NPC prefab.");
                return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureDocumentPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DocumentPrefabPath);
            if (existing != null) return existing;
            var root = new GameObject("NPC_Map_Dokumen");
            try
            {
                Material folder = MaterialFor("NPC_Map_Dokumen", new Color(.38f,.51f,.43f));
                Material paper = MaterialFor("NPC_Kertas_Dokumen", new Color(.9f,.88f,.78f));
                CreatePrimitive("Map", PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(.25f,.012f,.18f), folder);
                CreatePrimitive("Kertas", PrimitiveType.Cube, root.transform, new Vector3(0,.012f,0), new Vector3(.23f,.012f,.16f), paper);
                return PrefabUtility.SaveAsPrefabAsset(root, DocumentPrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CreatePrimitive(string name, PrimitiveType shape, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(shape);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            item.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static TextMeshPro CreateText(Transform parent, string name, Color color, float height)
        {
            Transform holder = Group(parent, name);
            holder.localPosition = Vector3.up * height;
            holder.localScale = Vector3.one * .1f;
            TextMeshPro text = holder.gameObject.AddComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 3.2f;
            text.rectTransform.sizeDelta = new Vector2(5f,.8f);
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.text = string.Empty;
            text.raycastTarget = false;
            return text;
        }

        private static Material MaterialFor(string name, Color color)
        {
            EnsureFolder(MaterialFolder);
            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("A supported material shader is required.");
            var material = new Material(shader) { name = name, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
