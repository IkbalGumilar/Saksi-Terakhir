using System;
using System.Collections.Generic;
using System.Linq;
using SaksiTerakhir.Environment;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SaksiTerakhir.EditorTools
{
    // References and gameplay markers only. Authored UI rectangles and environment
    // prefabs stay owned by their designers; the caller decides when to save.
    public static class ChapterOneWorldSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string Content = "Assets/_Project/Story/";
        private const string Profiles = "Assets/_Project/Npc/Profiles/";
        private const string PopulationPrefab = "Assets/_Project/Prefabs/Npc/NPC_Population_Base.prefab";

        [Serializable]
        private sealed class Report
        {
            public int permanentCast, dialogues, mainQuests, activities, shops, locationTriggers;
            public int bossDoorLeaves, reservedPopulationPosts, uiReferences;
            public bool vehicleDepartureEnabled;
            public string[] unsampledAnchors;
            public string scene;
        }

        [MenuItem("Saksi Terakhir/Chapter One/Configure Active World")]
        public static void ConfigureFromMenu() => Debug.Log(ConfigureActiveScene());

        public static string ConfigureActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid()
                || !scene.isLoaded || scene.path != ScenePath)
                throw new InvalidOperationException("Bab 1 requires the active Regional Archive Office scene outside Play Mode.");
            Transform[] all = scene.GetRootGameObjects().Where(go => go.activeInHierarchy)
                .SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform Find(string name) => all.FirstOrDefault(t => t.name == name
                && !HasAncestor(t, "LegacyScene_Backup"));
            Transform office = Require(Find("Kantor_Arsip_Modular_60x20_4L_Rooftop"), "Active modular office");
            PlayerInteractor interactor = Require(all.Select(t => t.GetComponent<PlayerInteractor>())
                .FirstOrDefault(c => c != null && c.gameObject.activeInHierarchy), "Player interactor");
            Transform player = interactor.transform;
            PlayerController movement = Require(player.GetComponent<PlayerController>(), "Player movement");
            CharacterController body = Require(player.GetComponent<CharacterController>(), "Player CharacterController");
            PlayerCameraController camera = Require(player.GetComponent<PlayerCameraController>(), "Player camera controller");
            OfficeNpcDirector npcDirector = Require(all.Select(t => t.GetComponent<OfficeNpcDirector>())
                .FirstOrDefault(c => c != null && c.gameObject.activeInHierarchy), "World NPC director");
            NpcPopulationManager population = Require(all.Select(t => t.GetComponent<NpcPopulationManager>())
                .FirstOrDefault(c => c != null && c.gameObject.activeInHierarchy), "World NPC population manager");
            GameObject template = Asset<GameObject>(PopulationPrefab);
            int agentType = Require(template.GetComponent<NavMeshAgent>(), "NPC prefab navigation").agentTypeID;
            NpcProfile[] profiles = new[] { "NPC-002-Resepsionis", "NPC-003-Rekan-A", "NPC-004-Rekan-B",
                "NPC-006-Bos", "NPC-021-Pekerja-03", "NPC-022-Pekerja-04", "NPC-019-Pekerja-01" }
                .Select(file => Asset<NpcProfile>(Profiles + file + ".asset")).ToArray();
            QuestDefinition[] quests = new[] { "rooftop", "call", "boss-first", "find-colleagues", "escort",
                "boss-final", "car-key", "vehicle" }.Select(file => Asset<QuestDefinition>(Content + "Quests/" + file + ".asset")).ToArray();
            QuestDefinition morning = Asset<QuestDefinition>(Content + "Quests/morning-routine.asset");
            DialogueSequence[] sequences = AssetDatabase.FindAssets("t:DialogueSequence", new[] { Content + "Dialogues" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<DialogueSequence>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(sequence => sequence != null && sequence.Id.StartsWith("chapter.", StringComparison.Ordinal)).ToArray();
            if (sequences.Length < 31) throw new InvalidOperationException("Generate canonical Bab 1 content before wiring the world.");
            ItemDefinitionCatalog items = Asset<ItemDefinitionCatalog>("Assets/_Project/Resources/ItemDefinitionCatalog.asset");
            QuestJournalCatalog journal = Asset<QuestJournalCatalog>("Assets/_Project/Resources/QuestJournalCatalog.asset");
            TimeOfDayPreset morningTime = Asset<TimeOfDayPreset>("Assets/_Project/Environment/TimeOfDay_Pagi.asset");
            TimeOfDayPreset lunchTime = Asset<TimeOfDayPreset>("Assets/_Project/Environment/TimeOfDay_Siang_11.asset");
            TimeOfDayPreset preparationTime = Asset<TimeOfDayPreset>("Assets/_Project/Environment/TimeOfDay_Siang_13.asset");
            TimeOfDayApplier time = Require(all.Select(t => t.GetComponent<TimeOfDayApplier>())
                .FirstOrDefault(c => c != null && c.gameObject.activeInHierarchy), "Time of day applier");
            Transform canvas = Require(Find("Canvas"), "Canvas");
            Transform safe = Require(canvas.Find("Save Area"), "Authored safe area");
            GameObject inventoryPanel = Require(safe.Find("Inventory Panel"), "Inventory panel").gameObject;
            if (!InventoryView.CanBind(inventoryPanel)) throw new InvalidOperationException("Authored inventory needs both slot templates.");
            Transform tracker = Require(safe.Find("Chapter One Quest Tracker"), "Quest tracker");
            TMP_Text title = Text(tracker, "Quest Title"), objectives = Text(tracker, "Quest Objectives");
            Image dot = Require(Require(tracker.Find("Main Quest Dot"), "Main quest dot").GetComponent<Image>(), "Main quest dot image");
            Transform dialoguePanel = Require(safe.Find("Chapter One Dialogue Panel"), "Dialogue panel");
            TMP_Text speaker = Text(dialoguePanel, "Speaker"), line = Text(dialoguePanel, "Dialogue Line");
            Transform action = Require(safe.Find("Action"), "Interaction action panel");
            TMP_Text actionLabel = Text(action, "Action Text");
            Transform genericDialogue = Require(canvas.Find("NPC Dialogue Panel"), "NPC dialogue panel");
            TMP_Text npcName = Text(genericDialogue, "NPC Name"), npcLine = Text(genericDialogue, "NPC Line");
            Transform notice = Require(safe.Find("Notificatuion"), "Notification panel");
            TMP_Text noticeLabel = Text(notice, "text notificatuion");
            Transform autosave = Require(notice.Find("Icon"), "Autosave icon");
            Transform bindings = Require(safe.Find("Chapter One UI Bindings"), "Authored story UI bindings");
            Transform bossFrame = office.GetComponentsInChildren<Transform>(true).FirstOrDefault(t =>
                t.name == "Pintu_Ganda_Terbuka" && Vector3.Distance(t.position, office.TransformPoint(new Vector3(12f, 8f, 12f))) < .15f);
            Require(bossFrame, "Boss double door frame");
            DoorInteractable[] bossDoors = bossFrame.GetComponentsInChildren<DoorInteractable>(true);
            if (bossDoors.Length != 2) throw new InvalidOperationException("Configure world doors first; the boss entrance requires both leaves.");
            foreach (string id in new[] { "item.flashlight", "item.battery_18650", "item.power_bank", "item.agenda_book",
                "item.nasi_uduk", "item.fiber_rope_10m", "item.nylon_rope_10m", "item.first_aid_kit" })
                if (items.Find(id) == null || items.Find(id).ModelPrefab == null)
                    throw new InvalidOperationException("Missing modeled shop item: " + id);

            var report = new Report { scene = scene.path, dialogues = sequences.Length, mainQuests = quests.Length + 1,
                permanentCast = profiles.Length, bossDoorLeaves = bossDoors.Length };
            var unsampled = new List<string>();
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Chapter One Gameplay");
            if (root == null) root = new GameObject("Chapter One Gameplay");
            root.SetActive(true);
            Transform anchors = Child(root.transform, "Anchors");
            Vector3 Ground(string label, Vector3 position)
            {
                var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
                if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1.4f, filter)
                    && Mathf.Abs(hit.position.y - position.y) <= .6f) return hit.position;
                unsampled.Add(label);
                return position;
            }
            Transform Anchor(string name, Vector3 position, float yaw = 0f, bool ground = false)
            {
                Transform anchor = Child(anchors, name);
                anchor.SetPositionAndRotation(ground ? Ground(name, position) : position, Quaternion.Euler(0f, yaw, 0f));
                return anchor;
            }
            Transform rakaSeat = Anchor("Raka Meeting Seat", new Vector3(14f, 8.45f, 15.85f));
            Transform sintaSeat = Anchor("Sinta Meeting Seat", new Vector3(14.8f, 8.45f, 15.85f));
            Transform playerSeat = Anchor("Player Meeting Seat", new Vector3(13.2f, 8.45f, 15.85f));
            Transform bossChair = Anchor("Boss Chair", new Vector3(17f, 8.45f, 19.2f), 180f);
            Transform bossLeft = Anchor("Boss Meeting Left", new Vector3(12.5f, 8.45f, 13f), 180f);
            Transform bossRight = Anchor("Boss Meeting Right", new Vector3(15.5f, 8.45f, 13f), 180f);
            Transform doorWait = Anchor("Boss Door Wait", new Vector3(14f, 8.04f, 10f), 0f, true);
            Transform corridorExit = Anchor("Boss Corridor Exit", new Vector3(17.5f, 8.04f, 10f), 90f, true);
            Transform meetingApproach = Anchor("Meeting Approach", new Vector3(11.7f, 8.04f, 15.8f), 90f, true);
            Transform bossWalk = Anchor("Boss Walk Start", new Vector3(17f, 8.04f, 16.8f), 180f, true);
            Transform mealRaka = Anchor("Raka Meal Seat", new Vector3(40.85f, .45f, 5.5f), 90f);
            Transform mealSinta = Anchor("Sinta Meal Seat", new Vector3(43.15f, .45f, 5.5f), 270f);
            Transform mealPlayer = Anchor("Player Meal Seat", new Vector3(42f, .45f, 6.45f), 180f);
            Transform mealStand = Anchor("Player Meal Stand", new Vector3(42f, .04f, 7.2f), 180f, true);
            Transform mealRakaApproach = Anchor("Raka Meal Approach", new Vector3(40.6f, .04f, 6.8f), 90f, true);
            Transform mealSintaApproach = Anchor("Sinta Meal Approach", new Vector3(43.4f, .04f, 6.8f), 270f, true);
            Transform lunchOne = Anchor("Worker Lunch One", new Vector3(11.5f, 4.24f, 4f), 270f, true);
            Transform lunchTwo = Anchor("Worker Lunch Two", new Vector3(14f, 4.24f, 4f), 270f, true);
            Transform roofOne = Anchor("Worker Roof One", new Vector3(22f, 15.64f, 7f), 90f, true);
            Transform roofTwo = Anchor("Worker Roof Two", new Vector3(26f, 15.64f, 7f), 270f, true);
            Transform parkingResume = Anchor("Parking Resume", new Vector3(30f, .04f, -26f), 0f, true);
            Transform roofResume = Anchor("Rooftop Resume", new Vector3(24f, 15.64f, 9f), 180f, true);
            Transform searchResume = Anchor("Colleague Search Resume", new Vector3(16.4f, 4.24f, 10f), 180f, true);
            Component<ChapterOneWorldLayout>(root).Configure(parkingResume, roofResume, doorWait,
                searchResume, meetingApproach, playerSeat);

            Transform actorRoot = Child(root.transform, "Permanent Cast");
            Transform homes = Child(root.transform, "Cast Home Points");
            Vector3[] homePositions = { new Vector3(32f, .04f, 5.6f), new Vector3(22f, 4.24f, 3.9f),
                new Vector3(16.4f, 4.24f, 3.9f), new Vector3(17f, 8.04f, 17f),
                lunchOne.position, lunchTwo.position, new Vector3(11.5f, 4.24f, 7f) };
            float[] homeYaw = { 180f, 180f, 180f, 180f, 270f, 270f, 90f };
            var cast = new OfficeNpcAgent[profiles.Length];
            for (int i = 0; i < cast.Length; i++)
            {
                Transform existing = actorRoot.Find(profiles[i].Id);
                GameObject actorObject = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(template, actorRoot);
                actorObject.name = profiles[i].Id;
                actorObject.SetActive(true);
                Vector3 position = Ground(profiles[i].Id + " home", homePositions[i]);
                actorObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, homeYaw[i], 0f));
                NpcActivityPoint home = Component<NpcActivityPoint>(Child(homes, profiles[i].Id).gameObject);
                home.transform.SetPositionAndRotation(position, actorObject.transform.rotation);
                home.Configure(profiles[i].DisplayName, NpcActivityKind.Work, NpcZone.Interior,
                    position.y > 7f ? 3 : position.y > 3f ? 2 : 1, new Vector2(30f, 60f));
                OfficeNpcAgent actor = Require(actorObject.GetComponent<OfficeNpcAgent>(), "Permanent NPC agent");
                actor.SetProfile(profiles[i]);
                actor.BindWaypoints(home, new[] { home });
                actor.SetDirector(npcDirector);
                Component<NpcPopulationMember>(actorObject);
                actorObject.GetComponent<NavMeshAgent>().speed = profiles[i].MovementSpeed;
                npcDirector.RegisterActor(actor);
                foreach (Component changed in actorObject.GetComponents<Component>())
                {
                    EditorUtility.SetDirty(changed);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
                }
                cast[i] = actor;
            }
            npcDirector.SetBuildingRoot(office);
            npcDirector.SetAutomaticWaypointBinding(false);
            Set(population, "maximumVisible", 15);
            StoryDialogueController dialogue = Component<StoryDialogueController>(root);
            ChapterOneDirector chapter = Component<ChapterOneDirector>(root);
            InventoryPanelController inventory = Component<InventoryPanelController>(player.gameObject);
            inventory.BindPanel(inventoryPanel);
            Set(inventory, "itemCatalog", items);
            Set(inventory, "input", player.GetComponent<PlayerInputReader>());
            StoryPlayerCinematicMover mover = Component<StoryPlayerCinematicMover>(player.gameObject);
            StoryPlayerSeating seating = Component<StoryPlayerSeating>(player.gameObject);
            mover.Configure(movement, body, agentType);
            seating.Configure(movement, body, player.GetComponentInChildren<Animator>(true));
            Set(seating, "playerCamera", camera);
            Set(dialogue, "playerMovement", movement);
            Set(dialogue, "playerCamera", camera);
            Set(interactor, "storyDialogue", dialogue);
            Set(interactor, "storyDirector", chapter);
            Set(interactor, "playerMovement", movement);
            Set(interactor, "interactionRange", 2f);
            Set(interactor, "probeLayers", 1 << 6);
            Set(interactor, "aimColliderLayers", Physics.DefaultRaycastLayers);
            GameObject zoneObject = Child(root.transform, "Boss Room Zone").gameObject;
            zoneObject.transform.position = new Vector3(14f, 9.4f, 16f);
            BoxCollider zone = Component<BoxCollider>(zoneObject);
            zone.isTrigger = true; zone.size = new Vector3(11.6f, 2.8f, 7.6f);
            GameObject barrierObject = Child(root.transform, "Boss Player Barrier").gameObject;
            barrierObject.layer = 2;
            barrierObject.transform.position = new Vector3(14f, 9.35f, 12f);
            BoxCollider barrier = Component<BoxCollider>(barrierObject);
            barrier.isTrigger = false; barrier.size = new Vector3(4f, 2.7f, .22f); barrier.enabled = false;
            BossOfficeGate gate = Component<BossOfficeGate>(bossDoors[0].gameObject);
            gate.ConfigureScene(zone, barrier, corridorExit, player, npcDirector, cast[3], cast[1], cast[2]);
            foreach (DoorInteractable door in bossDoors)
            {
                door.BindAccessGate(gate);
                EditorUtility.SetDirty(door);
                PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            }
            chapter.ConfigureScene(dialogue, player, gate, cast, rakaSeat, sintaSeat, doorWait, quests, sequences,
                bossChair, bossLeft, bossRight, playerSeat, mover, seating, camera, meetingApproach, bossWalk);
            Set(chapter, "includeMorningRoutine", true);
            Set(chapter, "vehicleDepartureEnabled", false);
            Set(chapter, "morningQuest", morning);
            Set(chapter, "playerInventory", inventory);
            Set(chapter, "timeOfDay", time);
            Set(chapter, "morningTime", morningTime);
            Set(chapter, "lunchTime", lunchTime);
            Set(chapter, "preparationTime", preparationTime);
            Set(chapter, "rakaMealSeat", mealRaka); Set(chapter, "sintaMealSeat", mealSinta);
            Set(chapter, "rakaMealApproach", mealRakaApproach); Set(chapter, "sintaMealApproach", mealSintaApproach);
            Set(chapter, "workerLunchOne", lunchOne); Set(chapter, "workerLunchTwo", lunchTwo);
            Set(chapter, "workerRoofOne", roofOne); Set(chapter, "workerRoofTwo", roofTwo);
            foreach (OfficeNpcAgent actor in cast) actor.GetComponent<NpcInteractable>()?.SetStoryDirector(chapter);
            // Wulan's activity owns this post's E prompt; ambient NPC conversations remain available.
            NpcInteractable courierPrompt = cast[6].GetComponent<NpcInteractable>();
            if (courierPrompt != null)
            {
                courierPrompt.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(courierPrompt);
            }
            Component<QuestJournalController>(root).Configure(chapter, journal);
            InteractionPromptView prompt = Component<InteractionPromptView>(safe.gameObject);
            Set(prompt, "interactor", interactor); Set(prompt, "promptRoot", action.gameObject); Set(prompt, "actionLabel", actionLabel);
            prompt.ConfigureStoryVisibility(action.gameObject, genericDialogue.gameObject);
            prompt.ConfigureNpcDialogue(genericDialogue.gameObject, npcName, npcLine);
            Component<QuestTrackerView>(bindings.gameObject).Configure(chapter, tracker.gameObject, title, objectives, dot);
            Component<StoryDialogueView>(bindings.gameObject).Configure(dialogue, dialoguePanel.gameObject, speaker, line, prompt, profiles);
            StoryNotificationView notification = Component<StoryNotificationView>(bindings.gameObject);
            notification.Configure(chapter, notice.gameObject, noticeLabel);
            notification.ConfigureAutosave(autosave.gameObject);
            report.uiReferences = CountObjectReferences(prompt)
                + bindings.GetComponents<MonoBehaviour>().Sum(CountObjectReferences);

            Transform activities = Child(root.transform, "Activities");
            ChapterOneActivityInteractable Activity(string name, ChapterOneActivityType type, Vector3 position, Vector3 size)
            {
                GameObject target = Marker(activities, name, position, size);
                ChapterOneActivityInteractable activity = Component<ChapterOneActivityInteractable>(target);
                activity.Configure(chapter, type, dialogue);
                report.activities++;
                return activity;
            }
            Activity("Morning Desk", ChapterOneActivityType.MorningDesk, new Vector3(10f, 4.95f, 2.3f), new Vector3(1.55f, 1.5f, .8f));
            Activity("Desk Preparation", ChapterOneActivityType.DeskPreparation, new Vector3(10f, 4.95f, 2.3f), new Vector3(1.55f, 1.5f, .8f));
            Activity("Breakfast Counter", ChapterOneActivityType.Breakfast, new Vector3(38.5f, .85f, 6.8f), new Vector3(1.5f, 1.7f, .8f));
            Activity("Wrong Route Offer", ChapterOneActivityType.WrongRouteOffer, cast[6].transform.position + Vector3.up * .9f, new Vector3(.8f, 1.8f, .8f));
            Activity("Digitalization Delivery", ChapterOneActivityType.WrongRouteDelivery, new Vector3(38f, 5f, 2.3f), new Vector3(1.5f, 1.6f, .8f));
            Activity("Shared Meal", ChapterOneActivityType.SharedMeal, new Vector3(42f, .75f, 5.5f), new Vector3(2.6f, 1.5f, 2.5f))
                .Configure(chapter, ChapterOneActivityType.SharedMeal, dialogue, mealPlayer, mealStand);
            ChapterOneShopOffer Offer(string id, int price) => new ChapterOneShopOffer(items.Find(id), price);
            ChapterOneShopOffer[] general = { Offer("item.flashlight", 60000), Offer("item.battery_18650", 25000),
                Offer("item.power_bank", 150000), Offer("item.agenda_book", 30000), Offer("item.nasi_uduk", 15000) };
            ChapterOneShopOffer[] electronics = { Offer("item.battery_18650", 25000), Offer("item.power_bank", 150000), Offer("item.flashlight", 60000) };
            ChapterOneShopOffer[] material = { Offer("item.fiber_rope_10m", 40000), Offer("item.nylon_rope_10m", 35000) };
            foreach (Transform store in all.Where(t => t.parent != null && t.parent.name == "01_Toko_Dua_Sisi_Jalan"))
            {
                ChapterOneShopOffer[] stock = store.name.Contains("Elektronik") ? electronics
                    : store.name.Contains("Material") ? material : store.name.Contains("Apotek") || store.name.Contains("Sehat")
                    ? new[] { Offer("item.first_aid_kit", 75000) } : store.name.Contains("Toserba") || store.name.Contains("Serba") ? general
                    : store.name.Contains("Buku") ? new[] { Offer("item.agenda_book", 30000) } : null;
                if (stock == null) continue;
                Vector3 position = new Vector3(store.position.x + 6f, .95f, store.position.z > -62f ? -53.5f : -70.5f);
                Activity("Shop " + store.name, ChapterOneActivityType.Shop, position, new Vector3(1.4f, 1.9f, 1.4f))
                    .ConfigureShop(chapter, store.name, stock, dialogue);
                report.shops++;
            }
            Transform canteen = Find("Rumah_Makan_Selatan");
            if (canteen != null)
            {
                Activity("Shop Rumah Makan Selatan", ChapterOneActivityType.Shop, canteen.TransformPoint(new Vector3(12f, .95f, -3f)),
                    new Vector3(1.4f, 1.9f, 1.4f)).ConfigureShop(chapter, "Rumah Makan Selatan", new[] { Offer("item.nasi_uduk", 15000) }, dialogue);
                report.shops++;
            }
            Transform spbu = all.FirstOrDefault(t => t.name == "08_SPBU_dan_Supermarket");
            if (spbu != null)
            {
                Activity("Shop Supermarket SPBU", ChapterOneActivityType.Shop, new Vector3(56f, .95f, 291f), new Vector3(1.4f, 1.9f, 1.4f))
                    .ConfigureShop(chapter, "Supermarket SPBU", general, dialogue);
                report.shops++;
            }
            GameObject bulletin = Marker(activities, "Holiday Bulletin", new Vector3(35.7f, 1.4f, 6f), new Vector3(.25f, 1.3f, 1.6f));
            Component<ChapterOneBulletinInteractable>(bulletin).Configure(chapter);
            // Reuse the available board model instead of generating a replacement texture.
            if (bulletin.transform.Find("Board Visual") == null)
            {
                GameObject board = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ModularAsset/Perlengkapan/Prefabs/Kantor/OAA_Papan_Rapat_Whiteboard.prefab");
                if (board != null)
                {
                    GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(board, bulletin.transform);
                    visual.name = "Board Visual";
                    visual.transform.SetPositionAndRotation(new Vector3(35.7f, 0f, 6f), Quaternion.Euler(0f, 270f, 0f));
                }
            }
            Transform locations = Child(root.transform, "Locations");
            void Location(string name, ChapterOneLocation kind, Vector3 position, Vector3 size)
            {
                GameObject target = Marker(locations, name, position, size);
                target.layer = 2;
                Component<ChapterOneLocationTrigger>(target).Configure(chapter, kind);
                report.locationTriggers++;
            }
            Location("Office Entrance", ChapterOneLocation.OfficeEntrance, new Vector3(32f, 1.4f, 1f), new Vector3(4f, 2.8f, 1.4f));
            Location("Roof West Exit", ChapterOneLocation.RooftopExit, new Vector3(8f, 17f, 14f), new Vector3(2f, 2.8f, 4f));
            Location("Roof East Exit", ChapterOneLocation.RooftopExit, new Vector3(52f, 17f, 14f), new Vector3(2f, 2.8f, 4f));
            foreach (VehicleArrivalZone arrival in all.Select(t => t.GetComponent<VehicleArrivalZone>()).Where(c => c != null))
                arrival.enabled = false;
            Vector3[] reserved = homePositions.Concat(new[] { mealRaka.position, mealSinta.position, mealPlayer.position,
                roofOne.position, roofTwo.position, playerSeat.position, rakaSeat.position, sintaSeat.position }).ToArray();
            foreach (NpcSpawnPoint point in population.Points.Where(point => point != null))
                if (reserved.Any(position => Mathf.Abs(point.Position.y - position.y) < .75f
                    && Vector3.ProjectOnPlane(point.Position - position, Vector3.up).sqrMagnitude < 6.25f))
                { point.enabled = false; EditorUtility.SetDirty(point); report.reservedPopulationPosts++; }
            foreach (Component changed in root.GetComponentsInChildren<Component>(true))
            {
                EditorUtility.SetDirty(changed);
                PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
            }
            foreach (Component changed in player.GetComponents<Component>())
            {
                EditorUtility.SetDirty(changed);
                PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
            }
            EditorUtility.SetDirty(npcDirector);
            PrefabUtility.RecordPrefabInstancePropertyModifications(npcDirector);
            EditorUtility.SetDirty(inventory);
            foreach (MonoBehaviour changed in bindings.GetComponents<MonoBehaviour>()) EditorUtility.SetDirty(changed);
            npcDirector.RefreshDoors();
            report.unsampledAnchors = unsampled.Distinct().ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            return JsonUtility.ToJson(report, true);
        }

        private static bool HasAncestor(Transform value, string name)
        {
            for (Transform current = value; current != null; current = current.parent)
                if (current.name == name) return true;
            return false;
        }
        private static T Asset<T>(string path) where T : UnityEngine.Object
            => Require(AssetDatabase.LoadAssetAtPath<T>(path), path);
        private static T Require<T>(T value, string label) where T : UnityEngine.Object
            => value != null ? value : throw new InvalidOperationException("Missing Bab 1 binding: " + label);
        private static TMP_Text Text(Transform parent, string path)
            => Require(Require(parent.Find(path), path).GetComponent<TMP_Text>(), path + " text");
        private static T Component<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null) component = go.AddComponent<T>();
            return component;
        }
        private static Transform Child(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }
        private static GameObject Marker(Transform parent, string name, Vector3 position, Vector3 size)
        {
            GameObject go = Child(parent, name).gameObject;
            go.layer = 6; go.transform.SetPositionAndRotation(position, Quaternion.identity);
            BoxCollider collider = Component<BoxCollider>(go);
            collider.center = Vector3.zero; collider.size = size; collider.isTrigger = true;
            return go;
        }
        private static void Set(UnityEngine.Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field)
                ?? throw new InvalidOperationException(target.GetType().Name + " has no serialized field " + field);
            if (value is bool boolean) property.boolValue = boolean;
            else if (value is int integer) property.intValue = integer;
            else if (value is float number) property.floatValue = number;
            else property.objectReferenceValue = value as UnityEngine.Object;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
        private static int CountObjectReferences(UnityEngine.Object target)
        {
            int count = 0;
            SerializedProperty property = new SerializedObject(target).GetIterator();
            while (property.NextVisible(true))
                if (property.name != "m_Script" && property.propertyType == SerializedPropertyType.ObjectReference
                    && property.objectReferenceValue != null) count++;
            return count;
        }
    }
}
