using System;
using System.Collections.Generic;
using System.Linq;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using SaksiTerakhir.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    public static class VehicleJourneySetup
    {
        private const string MainScene = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string RoadPrefabs = "Assets/ModularAsset/Lingkungan/Jalan/Prefabs/";
        private const string Asphalt = "Assets/ModularAsset/Perlengkapan/Prefabs/Parkiran/OAA_Parking_Asphalt_4x4m.prefab";
        private static float wheelGroundOffset = .01f;

        [MenuItem("Saksi Terakhir/Vehicles/Connect Office To SPBU Journey")]
        public static void ConfigureFromMenu() => Debug.Log(ConfigureActiveScene());

        public static string ConfigureStationDepartureActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != MainScene)
                throw new InvalidOperationException("Open the main scene outside Play Mode.");
            Transform[] all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform car = all.First(t => t.name == "SUV_Fleet_Black");
            AutomaticVehicleDriver driver = car.GetComponent<AutomaticVehicleDriver>();
            VehicleJourneyDirector journey = car.GetComponent<VehicleJourneyDirector>();
            ChapterOneDirector chapter = all.Select(t => t.GetComponent<ChapterOneDirector>())
                .First(c => c != null && c.isActiveAndEnabled);
            FuelStationChapterDirector station = chapter.GetComponent<FuelStationChapterDirector>();
            if (driver == null || journey == null || station == null)
                throw new InvalidOperationException("Configure the original journey and station chapter before its departure route.");

            var serialized = new SerializedObject(driver);
            SerializedProperty parking = serialized.FindProperty("parkingPoints");
            if (parking.arraySize < 2) throw new InvalidOperationException("The station parking route is missing.");
            Vector3 parkingEnd = parking.GetArrayElementAtIndex(parking.arraySize - 1).vector3Value;
            wheelGroundOffset = parkingEnd.y - GroundY(parkingEnd.x, parkingEnd.z, car);
            OpenStationExit(all);
            Physics.SyncTransforms();
            var points = new List<Vector3> { parkingEnd };
            var speeds = new List<float> { 1.6f };
            Point(points, speeds, 46.5f, 280f, 1.6f);
            Arc(points, speeds, 51.5f, 280f, 5f, 180f, 270f, 1.6f);
            Point(points, speeds, 56f, 275f, 1.6f);
            Arc(points, speeds, 56f, 269f, 6f, 90f, 0f, 1.6f);
            Point(points, speeds, 62f, 262f, 1.6f);
            // Facing south out of the forecourt, west is the right turn away from the office.
            Arc(points, speeds, 56f, 262f, 6f, 0f, -90f, 1.8f);
            speeds[speeds.Count - 1] = 4.8f;
            Point(points, speeds, -70f, 256f, 0f);
            Undo.RecordObject(driver, "Connect SPBU right-turn departure route");
            SetPoints(serialized.FindProperty("stationDeparturePoints"), points);
            SetSpeeds(serialized.FindProperty("stationDepartureSpeeds"), speeds);
            serialized.ApplyModifiedProperties();
            Record(driver);
            Bind(journey, "fuelStation", station);
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            return $"SPBU departure connected: {points.Count} points, exit at x=62, right turn west in lane z=256. Existing car and office route unchanged.";
        }

        public static string ConfigureActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != MainScene)
                throw new InvalidOperationException("Open the main scene outside Play Mode.");
            Transform[] all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform car = all.First(t => t.name == "SUV_Fleet_Black");
            Transform motor = all.First(t => t.name == "GSX_S_125");
            ChapterOneDirector chapter = all.Select(t => t.GetComponent<ChapterOneDirector>()).First(c => c != null && c.isActiveAndEnabled);
            StoryDialogueController dialogue = chapter.GetComponent<StoryDialogueController>();
            PlayerInputReader player = all.Select(t => t.GetComponent<PlayerInputReader>()).First(c => c != null && c.isActiveAndEnabled);
            PlaceVehicle(car, new Vector2(23.75f, -9.5f));
            wheelGroundOffset = car.position.y - GroundY(car.position.x, car.position.z, car);
            PlaceVehicle(motor, new Vector2(6.25f, -9.5f));
            foreach (Transform part in car.GetComponentsInChildren<Transform>(true))
            {
                Undo.RecordObject(part.gameObject, "Make travelling SUV dynamic");
                GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
                Record(part.gameObject);
            }
            Rigidbody body = car.GetComponent<Rigidbody>();
            Undo.RecordObject(body, "Park scripted SUV");
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            Record(body);
            foreach (WheelCollider wheel in car.GetComponentsInChildren<WheelCollider>(true))
            { Undo.RecordObject(wheel, "Park scripted wheels"); wheel.enabled = false; Record(wheel); }
            VehicleWheelVisualSync sync = car.GetComponent<VehicleWheelVisualSync>();
            if (sync != null) { Undo.RecordObject(sync, "Use scripted wheel visuals"); sync.enabled = false; Record(sync); }
            VehicleItemUseSetup.ConfigureActiveScene();

            OpenDriveways(all);
            Physics.SyncTransforms();
            AutomaticVehicleDriver driver = Ensure<AutomaticVehicleDriver>(car.gameObject);
            var road = new List<Vector3>();
            var roadSpeeds = new List<float>();
            Point(road, roadSpeeds, car.position.x, car.position.z, 2.2f);
            Point(road, roadSpeeds, 23.75f, -20, 2.2f);
            Shift(road, roadSpeeds, 23.75f, -20, 32, -35, 2.2f);
            Point(road, roadSpeeds, 32, -55, 2.2f);
            Arc(road, roadSpeeds, 37, -55, 5, 180, 270, 2.2f);
            roadSpeeds[roadSpeeds.Count - 1] = 4.8f;
            Point(road, roadSpeeds, 76, -60, 4.8f);
            Point(road, roadSpeeds, 82, -60, 2.5f);
            Arc(road, roadSpeeds, 82, -54, 6, -90, 0, 2.5f);
            roadSpeeds[roadSpeeds.Count - 1] = 4.8f;
            Point(road, roadSpeeds, 88, 243, 4.8f);
            Point(road, roadSpeeds, 88, 250, 2.5f);
            Arc(road, roadSpeeds, 82, 250, 6, 0, 90, 2.5f);
            roadSpeeds[roadSpeeds.Count - 1] = 4.8f;
            Point(road, roadSpeeds, -4, 256, 4.8f);
            Point(road, roadSpeeds, -10, 256, 2f);
            Arc(road, roadSpeeds, -10, 264, 8, -90, -180, 2f);
            Point(road, roadSpeeds, -18, 270, 1.7f);
            Shift(road, roadSpeeds, -18, 270, -12, 283, 1.7f);
            roadSpeeds[roadSpeeds.Count - 1] = 0;

            var parking = new List<Vector3>();
            var parkingSpeeds = new List<float>();
            Point(parking, parkingSpeeds, -12, 283, 1.6f);
            Point(parking, parkingSpeeds, -12, 286, 1.6f);
            Arc(parking, parkingSpeeds, -7, 286, 5, 180, 90, 1.6f);
            Point(parking, parkingSpeeds, 42.5f, 291, 2f);
            Arc(parking, parkingSpeeds, 42.5f, 287, 4, 90, 0, 1.4f);
            Point(parking, parkingSpeeds, 46.5f, 283, 0);
            SetRoute(driver, road, roadSpeeds, parking, parkingSpeeds);
            var serializedDriver = new SerializedObject(driver);
            SerializedProperty wheels = serializedDriver.FindProperty("wheels");
            Transform[] wheelVisuals = car.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("WheelVisual_")).ToArray();
            wheels.arraySize = wheelVisuals.Length;
            for (int i = 0; i < wheelVisuals.Length; i++) wheels.GetArrayElementAtIndex(i).objectReferenceValue = wheelVisuals[i];
            serializedDriver.ApplyModifiedProperties();

            VehicleJourneyDirector journey = Ensure<VehicleJourneyDirector>(car.gameObject);
            Bind(journey, "chapter", chapter);
            Bind(journey, "dialogue", dialogue);
            Bind(journey, "player", player.transform);
            Bind(journey, "nasiUduk", AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Items/Nasi_Uduk_Bungkus.asset"));
            Bind(journey, "departureMealComplete", AssetDatabase.LoadAssetAtPath<DialogueSequence>("Assets/_Project/Story/Dialogues/vehicle-meal-complete.asset"));
            Bind(journey, "departureMealSkipped", AssetDatabase.LoadAssetAtPath<DialogueSequence>("Assets/_Project/Story/Dialogues/vehicle-meal-skipped.asset"));
            DialogueSequence roadDialogue = BuildDialogue("travel-to-spbu", "chapter.travel_to_spbu", RoadLines());
            DialogueSequence fuelDialogue = BuildDialogue("spbu-arrival", "chapter.spbu_arrival", FuelLines());
            Bind(journey, "roadDialogue", roadDialogue);
            Bind(journey, "fuelDialogue", fuelDialogue);
            var serializedJourney = new SerializedObject(journey);
            serializedJourney.FindProperty("officePosition").vector3Value = car.position;
            serializedJourney.FindProperty("officeEuler").vector3Value = car.eulerAngles;
            serializedJourney.ApplyModifiedProperties();
            Record(journey);

            PlayerPauseController pause = Ensure<PlayerPauseController>(player.gameObject);
            Bind(pause, "input", player);
            Bind(pause, "movement", player.GetComponent<PlayerController>());
            Bind(pause, "cameraController", player.GetComponent<PlayerCameraController>());
            Bind(pause, "interactor", player.GetComponent<SaksiTerakhir.Interaction.PlayerInteractor>());
            Bind(pause, "inventory", player.GetComponent<InventoryPanelController>());
            Bind(pause, "dialogue", dialogue);
            ConfigureQuest(chapter, roadDialogue, fuelDialogue);
            AssetDatabase.SaveAssets();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            return $"Automatic journey configured: {road.Count} road points, {parking.Count} parking points, left lane, parking restored. Scene not saved; Play Mode not run.";
        }

        private static void PlaceVehicle(Transform vehicle, Vector2 center)
        {
            Undo.RecordObject(vehicle, "Place vehicle in parking stall");
            vehicle.SetPositionAndRotation(new Vector3(center.x, 0, center.y), Quaternion.Euler(0, 180, 0));
            Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float ground = GroundY(center.x, center.y, vehicle);
            vehicle.position += new Vector3(center.x - bounds.center.x, ground + .01f - bounds.min.y, center.y - bounds.center.z);
            Rigidbody body = vehicle.GetComponent<Rigidbody>();
            if (body != null) { Undo.RecordObject(body, "Place parked rigidbody"); body.position = vehicle.position; body.rotation = vehicle.rotation; Record(body); }
            Record(vehicle);
        }

        private static float GroundY(float x, float z, Transform ignore = null)
        {
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, 3, z), Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits.OrderBy(h => h.distance))
                if (hit.collider.attachedRigidbody == null && (ignore == null || !hit.transform.IsChildOf(ignore))
                    && Vector3.Dot(hit.normal, Vector3.up) > .7f) return hit.point.y;
            return 0;
        }

        private static void Point(List<Vector3> points, List<float> speeds, float x, float z, float speed)
        {
            Vector3 value = new Vector3(x, GroundY(x, z) + wheelGroundOffset, z);
            if (points.Count > 0 && Vector3.Distance(points[points.Count - 1], value) < .01f) return;
            points.Add(value); speeds.Add(speed);
        }

        private static void Arc(List<Vector3> points, List<float> speeds, float x, float z, float radius, float from, float to, float speed)
        {
            for (int i = 1; i <= 18; i++)
            {
                float angle = Mathf.Lerp(from, to, i / 18f) * Mathf.Deg2Rad;
                Point(points, speeds, x + Mathf.Cos(angle) * radius, z + Mathf.Sin(angle) * radius, speed);
            }
        }

        private static void Shift(List<Vector3> points, List<float> speeds, float x0, float z0, float x1, float z1, float speed)
        {
            for (int i = 1; i <= 16; i++)
            {
                float t = i / 16f;
                Point(points, speeds, Mathf.Lerp(x0, x1, Mathf.SmoothStep(0, 1, t)), Mathf.Lerp(z0, z1, t), speed);
            }
        }

        private static void SetRoute(AutomaticVehicleDriver driver, List<Vector3> road, List<float> speeds, List<Vector3> parking, List<float> parkingSpeeds)
        {
            Undo.RecordObject(driver, "Set automatic vehicle lane route");
            var serialized = new SerializedObject(driver);
            SetPoints(serialized.FindProperty("roadPoints"), road);
            SetPoints(serialized.FindProperty("parkingPoints"), parking);
            SetSpeeds(serialized.FindProperty("roadSpeeds"), speeds);
            SetSpeeds(serialized.FindProperty("parkingSpeeds"), parkingSpeeds);
            serialized.ApplyModifiedProperties();
            Record(driver);
        }

        private static void SetPoints(SerializedProperty field, List<Vector3> values)
        { field.arraySize = values.Count; for (int i = 0; i < values.Count; i++) field.GetArrayElementAtIndex(i).vector3Value = values[i]; }
        private static void SetSpeeds(SerializedProperty field, List<float> values)
        { field.arraySize = values.Count; for (int i = 0; i < values.Count; i++) field.GetArrayElementAtIndex(i).floatValue = values[i]; }

        private static void OpenDriveways(Transform[] all)
        {
            string[] blocked = {
                "OAA_Decor_0475_OAA_Parking_Curb_2m", "OAA_Decor_0476_OAA_Parking_Curb_2m",
                "OAA_Decor_0477_OAA_Parking_Curb_2m", "OAA_Decor_0478_OAA_Parking_Curb_2m",
                "OAR_Sidewalk_Paving_8x2m_0828", "OAR_Sidewalk_Paving_8x2m_0830",
                "OAR_Curb_Straight_8m_0829", "OAR_Curb_Straight_8m_0831",
                "OAR_Sidewalk_Paving_2x2m_0872", "OAR_Sidewalk_Paving_2x2m_0873",
                "OAR_Sidewalk_Paving_2x2m_0874", "OAR_Sidewalk_Paving_2x2m_0877" };
            foreach (string name in blocked)
            {
                Transform t = all.FirstOrDefault(n => n.name == name);
                if (t == null) throw new InvalidOperationException("Driveway obstruction not found: " + name);
                Undo.RecordObject(t.gameObject, "Open vehicle driveway");
                t.gameObject.SetActive(false); Record(t.gameObject);
            }
            Move(all, "Pohon_Lubang_060", new Vector3(-9, -.145f, 265));
            Move(all, "Lubang_Pohon_060", new Vector3(-9, 0, 265));
            Move(all, "Papan_MASUK", new Vector3(-22, .95f, 270.3f));
            Move(all, "MPS_Station_Sign_4m_431", new Vector3(-23.5f, 0, 271));
            Transform road = all.First(t => t.name == "Jaringan_Jalan_Kawasan");
            Transform crossing = road.Find("Akses_Mobil_SPBU");
            if (crossing == null)
            {
                GameObject created = new GameObject("Akses_Mobil_SPBU");
                Undo.RegisterCreatedObjectUndo(created, "Create SPBU vehicle access");
                created.transform.SetParent(road, false); crossing = created.transform;
            }
            PlacePrefab(crossing, "Trotoar_Barat", RoadPrefabs + "Trotoar/OAR_Sidewalk_Paving_4x2m.prefab", new Vector3(-26, 0, 262));
            PlacePrefab(crossing, "Trotoar_Timur", RoadPrefabs + "Trotoar/OAR_Sidewalk_Paving_4x2m.prefab", new Vector3(-14, 0, 262));
            PlacePrefab(crossing, "Kanstin_Barat", RoadPrefabs + "Kanstin/OAR_Curb_Straight_4m.prefab", new Vector3(-26, 0, 262));
            PlacePrefab(crossing, "Kanstin_Timur", RoadPrefabs + "Kanstin/OAR_Curb_Straight_4m.prefab", new Vector3(-14, 0, 262));
            PlacePrefab(crossing, "Aspal_Masuk_Barat", Asphalt, new Vector3(-22, 0, 262));
            PlacePrefab(crossing, "Aspal_Masuk_Timur", Asphalt, new Vector3(-18, 0, 262));
        }

        private static void OpenStationExit(Transform[] all)
        {
            string[] blocked = { "OAR_Sidewalk_Paving_8x2m_0850", "OAR_Curb_Straight_8m_0851",
                "OAR_Sidewalk_Paving_2x2m_0908", "OAR_Sidewalk_Paving_2x2m_0909" };
            foreach (string name in blocked)
            {
                Transform obstacle = all.FirstOrDefault(t => t.name == name);
                if (obstacle == null) throw new InvalidOperationException("Station exit obstruction not found: " + name);
                Undo.RecordObject(obstacle.gameObject, "Open SPBU exit driveway");
                obstacle.gameObject.SetActive(false);
                Record(obstacle.gameObject);
            }
            Transform treeHole = all.FirstOrDefault(t => t.name == "Lubang_Pohon_065");
            Transform tree = all.FirstOrDefault(t => t.name == "Pohon_Lubang_065");
            if (treeHole != null) Move(all, treeHole.name, new Vector3(55f, 0f, 265f));
            if (tree != null) Move(all, tree.name, new Vector3(55f, -.145f, 265f));
            Transform road = all.First(t => t.name == "Jaringan_Jalan_Kawasan");
            Transform exit = road.Find("Akses_Keluar_SPBU");
            if (exit == null)
            {
                var created = new GameObject("Akses_Keluar_SPBU");
                Undo.RegisterCreatedObjectUndo(created, "Create station exit driveway");
                created.transform.SetParent(road, false);
                exit = created.transform;
            }
            // Existing neighbouring paving stays in place; only fill its eight-metre opening.
            PlacePrefab(exit, "Aspal_Barat", Asphalt, new Vector3(60f, 0f, 262f));
            PlacePrefab(exit, "Aspal_Timur", Asphalt, new Vector3(64f, 0f, 262f));
        }

        private static void Move(Transform[] all, string name, Vector3 position)
        { Transform t = all.First(n => n.name == name); Undo.RecordObject(t, "Clear SPBU driveway"); t.position = position; Record(t); }

        private static void PlacePrefab(Transform parent, string name, string path, Vector3 position)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new InvalidOperationException("Missing road prefab: " + path);
                GameObject created = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                Undo.RegisterCreatedObjectUndo(created, "Build SPBU driveway"); created.name = name; t = created.transform;
            }
            Undo.RecordObject(t, "Place SPBU driveway");
            t.SetPositionAndRotation(position, Quaternion.identity); t.localScale = Vector3.one; Record(t);
        }

        private static DialogueSequence BuildDialogue(string filename, string id, DialogueLine[] lines)
        {
            string path = "Assets/_Project/Story/Dialogues/" + filename + ".asset";
            DialogueSequence asset = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<DialogueSequence>(); AssetDatabase.CreateAsset(asset, path); }
            Undo.RecordObject(asset, "Set chapter vehicle dialogue"); asset.Configure(id, lines); EditorUtility.SetDirty(asset); return asset;
        }

        private static void ConfigureQuest(ChapterOneDirector chapter, DialogueSequence road, DialogueSequence fuel)
        {
            const string path = "Assets/_Project/Story/Quests/travel-to-spbu.asset";
            QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (quest == null) { quest = ScriptableObject.CreateInstance<QuestDefinition>(); AssetDatabase.CreateAsset(quest, path); }
            Undo.RecordObject(quest, "Configure SPBU travel quest");
            quest.Configure("chapter.travel_to_spbu", QuestCategory.Main, "story.quest.travel_spbu", Array.Empty<string>());
            EditorUtility.SetDirty(quest);
            var serialized = new SerializedObject(chapter);
            SerializedProperty quests = serialized.FindProperty("quests");
            if (quests.arraySize < 11) quests.arraySize = 11;
            quests.GetArrayElementAtIndex(8).objectReferenceValue = null;
            quests.GetArrayElementAtIndex(9).objectReferenceValue = quest;
            quests.GetArrayElementAtIndex(10).objectReferenceValue = null;
            SerializedProperty sequences = serialized.FindProperty("sequences");
            foreach (DialogueSequence sequence in new[] { road, fuel })
            {
                bool exists = false;
                for (int i = 0; i < sequences.arraySize; i++) if (sequences.GetArrayElementAtIndex(i).objectReferenceValue == sequence) exists = true;
                if (!exists) { sequences.arraySize++; sequences.GetArrayElementAtIndex(sequences.arraySize - 1).objectReferenceValue = sequence; }
            }
            serialized.ApplyModifiedProperties(); Record(chapter);
            LocalizationTable table = AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/_Project/Localization/LocalizationTable.asset");
            Undo.RecordObject(table, "Add vehicle journey text");
            var entries = new List<LocalizedTextEntry>(table.entries);
            if (!entries.Any(e => e.key == "story.quest.travel_spbu")) entries.Add(new LocalizedTextEntry
            { key = "story.quest.travel_spbu", indonesian = "Menuju SPBU bersama Dadan dan Rafi", english = "Travel to the fuel station with Dadan and Rafi" });
            table.entries = entries.ToArray(); table.ClearLookupCache(); EditorUtility.SetDirty(table);
        }

        private static DialogueLine[] RoadLines() => new[] {
            new DialogueLine("NPC-003", "Mampir SPBU dulu. Isi penuh, sekalian beli dua jerigen buat cadangan. Belum tahu nanti kita bolak-balik sejauh apa."),
            new DialogueLine("NPC-004", "Baru makan siang masih bahas Bali, sekarang sudah berangkat. Untung kopernya belum saya keluarkan."),
            new DialogueLine("PLAYER", "Kalau menunggu besok, satu hari habis. Berangkat sekarang, masih ada waktu kalau penelusurannya molor. Saya juga mau pulang sebelum Jumat."),
            new DialogueLine("NPC-003", "Sampai wilayahnya nanti, cari keterangan setempat dulu. Jangan habis bensin mengelilingi gunung cuma karena berkasnya menulis kaki Cikuray."),
            new DialogueLine("NPC-004", "Aku sempat baca cerita Sancang. Prabu Siliwangi pergi bersama pengikutnya, lalu mereka berubah jadi maung. Ada juga cerita kerajaannya lenyap jadi hutan. Aku baru sadar, ayah yang dicari Kian Santang itu tokoh yang sama."),
            new DialogueLine("PLAYER", "Dalam kisah perjalanan Prabu Siliwangi juga ada persinggahan di kaki Cikuray. Cikuray punya kaitan dengan penulisan naskah lama. Nama Gunung Larang Srimanganti disebut dalam kajiannya."),
            new DialogueLine("NPC-004", "Srimanganti. Kalau kampungnya memang pakai nama lain, kita bisa saja lewat tanpa sadar."),
            new DialogueLine("PLAYER", "Ada riwayat kampung Srimanganti yang ditinggalkan penduduknya. Tapi jangan langsung disamakan dengan Cikapohon. Itu baru contoh bahwa nama dalam tulisan lama perlu ditelusuri."),
            new DialogueLine("NPC-003", "Bisa juga permukiman pindah, jalurnya berubah, atau nama lama cuma dipakai beberapa keluarga. Kita tanya orangnya, cocokkan tempatnya. Kalau beda keterangan, jangan dipaksa cocok."),
            new DialogueLine("NPC-004", "Aku tadinya membayangkan tinggal ketemu papan desa, foto, selesai. Ternyata nama gunung saja bisa punya riwayat sendiri."),
            new DialogueLine("PLAYER", "Makanya cari dari petunjuk yang bisa diperiksa. Cerita rakyat membantu kita bertanya, tapi belum membuktikan pengalaman penulis prasasti."),
            new DialogueLine("NPC-004", "Baik. Yang pasti bisa diperiksa sekarang: kita butuh air minum."),
            new DialogueLine("NPC-003", "Itu tokonya kelihatan. Bensinnya dulu, lalu kamu boleh urus air minum.") };

        private static DialogueLine[] FuelLines() => new[] {
            new DialogueLine("NPC-003", "Tolong isi penuh, Pak. Kami juga perlu dua jerigen untuk cadangan. Yang khusus buat bahan bakar."),
            new DialogueLine("Petugas SPBU", "Bisa. Wadahnya ada di toko. Ambil dulu, nanti saya bantu urus pengisiannya."),
            new DialogueLine("NPC-004", "Saya ambil, Pak. Sekalian air minum. Jangan sampai dua jerigen kebawa, minuman buat kita malah lupa."),
            new DialogueLine("PLAYER", "Kalau begitu saya meluruskan kaki sebentar. Nanti kalau sudah siap kabari, ya."),
            new DialogueLine("NPC-003", "Boleh. Dekat sini ada taman. Saya sama Rafi selesaikan yang di sini, nanti kami panggil kalau sudah siap.") };

        private static T Ensure<T>(GameObject owner) where T : Component
            => owner.GetComponent<T>() ?? Undo.AddComponent<T>(owner);
        private static void Bind(UnityEngine.Object component, string field, UnityEngine.Object value)
        {
            Undo.RecordObject(component, "Connect automatic vehicle journey");
            var serialized = new SerializedObject(component); serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedProperties(); Record(component);
        }
        private static void Record(UnityEngine.Object value)
        { EditorUtility.SetDirty(value); PrefabUtility.RecordPrefabInstancePropertyModifications(value); }
    }
}
