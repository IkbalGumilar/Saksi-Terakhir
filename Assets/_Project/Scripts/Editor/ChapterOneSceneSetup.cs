using System;
using System.Linq;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            OfficeNpcDirector npcDirector = Find("Office NPC System").GetComponent<OfficeNpcDirector>();
            OfficeNpcAgent[] cast = npcDirector.Actors;
            OfficeNpcAgent Actor(string id) => cast.Single(npc => npc.Profile != null && npc.Profile.Id == id);
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Chapter One Story")
                ?? new GameObject("Chapter One Story");
            StoryDialogueController dialogue = EnsureComponent<StoryDialogueController>(root);
            ChapterOneDirector chapter = EnsureComponent<ChapterOneDirector>(root);
            SetObjectReference(dialogue, "playerMovement", movement);
            SetObjectReference(interactor, "storyDialogue", dialogue);
            SetObjectReference(interactor, "storyDirector", chapter);
            var serializedInteractor = new SerializedObject(interactor);
            serializedInteractor.FindProperty("aimColliderLayers").intValue = Physics.DefaultRaycastLayers;
            serializedInteractor.ApplyModifiedPropertiesWithoutUndo();

            Transform rakaSeat = Anchor(root.transform, "Raka Seat", new Vector3(4.27f, 15.84f, -32.06f));
            Transform sintaSeat = Anchor(root.transform, "Sinta Seat", new Vector3(4.55f, 15.84f, -31f));
            Transform doorWait = Anchor(root.transform, "Boss Door Wait", new Vector3(2.1f, 15.84f, -27.6f));
            Transform corridorExit = Anchor(root.transform, "Boss Corridor Exit", new Vector3(1.2f, 15.84f, -26.4f));
            GameObject roomObject = Child(root.transform, "Boss Room Zone");
            roomObject.transform.position = new Vector3(4f, 16.9f, -31.8f);
            BoxCollider roomZone = EnsureComponent<BoxCollider>(roomObject);
            roomZone.isTrigger = true;
            roomZone.size = new Vector3(7.8f, 2.8f, 5.2f);
            DoorInteractable bossDoor = Find("Office_ArchiveHQ_DoorLeaf_10").GetComponent<DoorInteractable>();
            GameObject barrierObject = Child(root.transform, "Boss Player Barrier");
            barrierObject.layer = 2; // Ignore Raycast, so the interaction probe still sees the door.
            barrierObject.transform.position = bossDoor.GetComponent<Collider>().bounds.center;
            BoxCollider barrier = EnsureComponent<BoxCollider>(barrierObject);
            barrier.isTrigger = false;
            barrier.size = new Vector3(1.2f, 2.7f, 0.22f);
            barrier.enabled = false;
            BossOfficeGate gate = EnsureComponent<BossOfficeGate>(bossDoor.gameObject);
            gate.ConfigureScene(roomZone, barrier, corridorExit, player, npcDirector, Actor("NPC-006"),
                Actor("NPC-003"), Actor("NPC-004"));
            Actor("NPC-006").transform.position = new Vector3(5.3f, 15.84f, -30f);

            QuestDefinition[] quests = new[] { "rooftop", "call", "boss-first", "find-colleagues",
                "escort", "boss-final", "car-key", "vehicle" }
                .Select(name => AssetDatabase.LoadAssetAtPath<QuestDefinition>(
                    "Assets/_Project/Story/Quests/" + name + ".asset")).ToArray();
            DialogueSequence[] sequences = new[] { "rooftop", "boss-call", "boss-first",
                "raka-first", "sinta-first", "raka-last", "sinta-last", "boss-final",
                "nadia-key", "boss-scold-raka", "boss-scold-sinta", "boss-scold-wait" }
                .Select(name => AssetDatabase.LoadAssetAtPath<DialogueSequence>(
                    "Assets/_Project/Story/Dialogues/" + name + ".asset")).ToArray();
            if (quests.Any(asset => asset == null) || sequences.Any(asset => asset == null))
                throw new InvalidOperationException("Chapter one content assets must exist before scene setup.");
            chapter.ConfigureScene(dialogue, player, gate,
                new[] { Actor("NPC-021"), Actor("NPC-022"), Actor("NPC-003"), Actor("NPC-004"),
                    Actor("NPC-006"), Actor("NPC-002") }, rakaSeat, sintaSeat, doorWait,
                quests, sequences);

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
            return "Chapter one scene wired: six story actors, boss door, quest UI, dialogue, notice and SUV.";
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

        private static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            Transform result = Child(parent, name).transform;
            result.position = position;
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
