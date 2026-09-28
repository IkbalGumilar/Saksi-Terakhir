using System;
using System.Linq;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SaksiTerakhir.EditorTools
{
    public static class ChapterOneSceneSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Regional Archive Office.unity";

        [MenuItem("Saksi Terakhir/Ensure Chapter One Scene")]
        public static void EnsureFromMenu() => Debug.Log(Ensure());

        public static void EnsureFromBatch() => Debug.Log(Ensure());

        public static string Ensure()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject[] objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject).ToArray();
            GameObject Find(string name) => objects.Single(go => go.name == name);
            Transform player = objects.SelectMany(go => go.GetComponents<PlayerInteractor>()).Single().transform;
            PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
            PlayerController movement = player.GetComponent<PlayerController>();
            PlayerCameraController playerCamera = player.GetComponent<PlayerCameraController>();
            CharacterController playerBody = player.GetComponent<CharacterController>();
            StoryPlayerCinematicMover playerMover = EnsureComponent<StoryPlayerCinematicMover>(player.gameObject);
            StoryPlayerSeating playerSeating = EnsureComponent<StoryPlayerSeating>(player.gameObject);
            OfficeNpcDirector npcDirector = Find("Office NPC System").GetComponent<OfficeNpcDirector>();
            OfficeNpcAgent[] cast = npcDirector.Actors;
            OfficeNpcAgent Actor(string id) => cast.Single(npc => npc.Profile != null && npc.Profile.Id == id);
            int agentTypeId = Actor("NPC-003").GetComponent<NavMeshAgent>().agentTypeID;
            if (!playerMover.Configure(movement, playerBody, agentTypeId)
                || !playerSeating.Configure(movement, playerBody,
                    player.GetComponentInChildren<Animator>(true)))
                throw new InvalidOperationException("The chapter one player requires movement and a CharacterController.");
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Chapter One Story")
                ?? new GameObject("Chapter One Story");
            StoryDialogueController dialogue = EnsureComponent<StoryDialogueController>(root);
            ChapterOneDirector chapter = EnsureComponent<ChapterOneDirector>(root);
            SetObjectReference(dialogue, "playerMovement", movement);
            SetObjectReference(dialogue, "playerCamera", playerCamera);
            SetObjectReference(interactor, "storyDialogue", dialogue);
            SetObjectReference(interactor, "storyDirector", chapter);
            var serializedInteractor = new SerializedObject(interactor);
            serializedInteractor.FindProperty("aimColliderLayers").intValue = Physics.DefaultRaycastLayers;
            serializedInteractor.ApplyModifiedPropertiesWithoutUndo();

            Transform bossChair = Anchor(root.transform, "Boss Chair",
                new Vector3(-5.643f, 15.84f, -38.926f), 0f);
            Transform bossMeetingLeft = Anchor(root.transform, "Boss Meeting Left",
                new Vector3(-7.099f, 15.84f, -33.161f), 0f);
            Transform bossMeetingRight = Anchor(root.transform, "Boss Meeting Right",
                new Vector3(-4.629f, 15.84f, -33.161f), 0f);
            Transform rakaSeat = Anchor(root.transform, "Raka Seat",
                new Vector3(-5.140f, 15.84f, -30.262f), 180f);
            Transform playerSeat = Anchor(root.transform, "Player Seat",
                new Vector3(-5.864f, 15.84f, -30.262f), 180f);
            Transform sintaSeat = Anchor(root.transform, "Sinta Seat",
                new Vector3(-6.589f, 15.84f, -30.262f), 180f);
            Transform doorWait = Anchor(root.transform, "Boss Door Wait",
                new Vector3(-2.094f, 15.84f, -30.964f), 270f);
            Transform corridorExit = Anchor(root.transform, "Boss Corridor Exit",
                new Vector3(-1.704f, 15.84f, -30.964f), 270f);
            Transform meetingApproach = Anchor(root.transform, "Meeting Approach",
                new Vector3(-4.68f, 15.86f, -31.07f), 0f);
            Transform bossWalkStart = Anchor(root.transform, "Boss Walk Start",
                new Vector3(-5.5f, 15.84f, -36.5f), 0f);
            GameObject flagRoomLink = Child(root.transform, "Flag Room Interior Link");
            flagRoomLink.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            NavMeshLink interiorLink = EnsureComponent<NavMeshLink>(flagRoomLink);
            interiorLink.agentTypeID = agentTypeId;
            interiorLink.startPoint = new Vector3(-5.53f, 15.86f, -32.5f);
            interiorLink.endPoint = new Vector3(-4.5f, 15.86f, -32.37f);
            interiorLink.width = 0.6f;
            interiorLink.bidirectional = true;
            interiorLink.activated = true;
            GameObject roomObject = Child(root.transform, "Boss Room Zone");
            roomObject.transform.SetPositionAndRotation(new Vector3(-5.65f, 16.9f, -34.31f),
                Quaternion.identity);
            BoxCollider roomZone = EnsureComponent<BoxCollider>(roomObject);
            roomZone.isTrigger = true;
            roomZone.size = new Vector3(4.9f, 2.8f, 10.2f);
            DoorInteractable bossDoor = Find("Office_ArchiveHQ_DoorLeaf_12").GetComponent<DoorInteractable>();
            if (bossDoor == null)
                throw new InvalidOperationException("The Indonesian-flag office door needs a DoorInteractable.");
            foreach (BossOfficeGate oldGate in objects.SelectMany(go => go.GetComponents<BossOfficeGate>())
                .Where(candidate => candidate.gameObject != bossDoor.gameObject))
                UnityEngine.Object.DestroyImmediate(oldGate);
            GameObject barrierObject = Child(root.transform, "Boss Player Barrier");
            barrierObject.layer = 2; // Ignore Raycast, so the interaction probe still sees the door.
            barrierObject.transform.SetPositionAndRotation(bossDoor.GetComponent<Collider>().bounds.center,
                Quaternion.identity);
            BoxCollider barrier = EnsureComponent<BoxCollider>(barrierObject);
            barrier.isTrigger = false;
            barrier.size = new Vector3(0.22f, 2.7f, 1.2f);
            barrier.enabled = false;
            BossOfficeGate gate = EnsureComponent<BossOfficeGate>(bossDoor.gameObject);
            gate.ConfigureScene(roomZone, barrier, corridorExit, player, npcDirector, Actor("NPC-006"),
                Actor("NPC-003"), Actor("NPC-004"));
            Actor("NPC-006").transform.SetPositionAndRotation(bossChair.position, bossChair.rotation);

            QuestDefinition[] quests = new[] { "rooftop", "call", "boss-first", "find-colleagues",
                "escort", "boss-final", "car-key", "vehicle" }
                .Select(name => AssetDatabase.LoadAssetAtPath<QuestDefinition>(
                    "Assets/_Project/Story/Quests/" + name + ".asset")).ToArray();
            DialogueSequence[] sequences = new[] { "rooftop", "boss-call", "boss-first",
                "raka-first", "sinta-first", "raka-last-intro", "sinta-last-intro",
                "raka-last", "sinta-last", "boss-seating", "boss-final",
                "nadia-key", "boss-scold-raka", "boss-scold-sinta", "boss-scold-wait" }
                .Select(name => AssetDatabase.LoadAssetAtPath<DialogueSequence>(
                    "Assets/_Project/Story/Dialogues/" + name + ".asset")).ToArray();
            if (quests.Any(asset => asset == null) || sequences.Any(asset => asset == null))
                throw new InvalidOperationException("Chapter one content assets must exist before scene setup.");
            chapter.ConfigureScene(dialogue, player, gate,
                cast, rakaSeat, sintaSeat, doorWait,
                quests, sequences, bossChair, bossMeetingLeft, bossMeetingRight, playerSeat,
                playerMover, playerSeating, playerCamera, meetingApproach, bossWalkStart);

            GameObject vehicle = Find("SUV_Fleet_Black");
            GameObject arrivalObject = Child(root.transform, "SUV Arrival Zone");
            arrivalObject.transform.position = vehicle.transform.position;
            SphereCollider arrivalCollider = EnsureComponent<SphereCollider>(arrivalObject);
            arrivalCollider.isTrigger = true;
            arrivalCollider.radius = 3f;
            EnsureComponent<VehicleArrivalZone>(arrivalObject).Configure(chapter, vehicle.transform, player, 3f);

            RectTransform saveArea = Find("Save Area").GetComponent<RectTransform>();
            GameObject bindings = Child(saveArea, "Chapter One UI Bindings");
            GameObject tracker = Panel(saveArea, "Chapter One Quest Tracker",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -20f),
                new Vector2(268f, 108f), new Color(0.04f, 0.05f, 0.07f, 0.78f));
            TMP_Text trackerTitle = Label(tracker.transform, "Quest Title", new Vector2(42f, -10f),
                new Vector2(210f, 32f), 17f);
            TMP_Text trackerObjectives = Label(tracker.transform, "Quest Objectives", new Vector2(18f, -47f),
                new Vector2(235f, 55f), 13f);
            Image questDot = Panel(tracker.transform, "Main Quest Dot", new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(18f, -17f), new Vector2(12f, 12f),
                QuestTrackerView.MainDotColor).GetComponent<Image>();
            GameObject storyPanel = Panel(saveArea, "Chapter One Dialogue Panel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 118f),
                new Vector2(548f, 112f), new Color(0.025f, 0.03f, 0.04f, 0.88f));
            TMP_Text speaker = Label(storyPanel.transform, "Speaker", new Vector2(18f, -10f),
                new Vector2(510f, 26f), 18f);
            speaker.color = new Color(1f, 0.82f, 0.36f, 1f);
            TMP_Text line = Label(storyPanel.transform, "Dialogue Line", new Vector2(18f, -37f),
                new Vector2(510f, 70f), 14f);

            GameObject notice = Find("Notificatuion");
            TMP_Text noticeText = notice.GetComponentInChildren<TMP_Text>(true);
            GameObject npcPanel = Find("NPC Dialogue Panel");
            InteractionPromptView genericPrompt = saveArea.GetComponent<InteractionPromptView>();
            genericPrompt.ConfigureStoryVisibility(Find("Action"), npcPanel);
            NpcProfile[] profiles = cast.Where(npc => npc.Profile != null)
                .Select(npc => npc.Profile).ToArray();
            EnsureComponent<QuestTrackerView>(bindings).Configure(chapter, tracker,
                trackerTitle, trackerObjectives, questDot);
            EnsureComponent<StoryDialogueView>(bindings).Configure(dialogue, storyPanel,
                speaker, line, genericPrompt, profiles);
            EnsureComponent<StoryNotificationView>(bindings).Configure(chapter, notice, noticeText);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "Chapter one scene wired: 25 NPCs, flag office, cinematic seating, quest UI, dialogue, notice and SUV.";
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static GameObject Child(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Transform Anchor(Transform parent, string name, Vector3 position, float yaw)
        {
            Transform result = Child(parent, name).transform;
            result.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return result;
        }

        private static GameObject Panel(Transform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject :
                new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (existing == null) go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            EnsureComponent<Image>(go).color = color;
            return go;
        }

        private static TMP_Text Label(Transform parent, string name, Vector2 position,
            Vector2 size, float fontSize)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject :
                new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            if (existing == null) go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.text = string.Empty;
            return text;
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

    }
}
