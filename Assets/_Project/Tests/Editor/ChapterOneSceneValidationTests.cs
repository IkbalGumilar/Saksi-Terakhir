using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.Tests
{
    public sealed class ChapterOneSceneValidationTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private Scene scene;
        private GameObject[] objects;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject).ToArray();
        }

        [TearDown]
        public void TearDown() => EditorSceneManager.CloseScene(scene, true);

        [Test]
        public void ExistingCastAndNavigationStayIntact()
        {
            OfficeNpcAgent[] cast = Components<OfficeNpcAgent>();
            Assert.That(cast.Length, Is.EqualTo(25));
            Assert.That(cast.Count(actor => actor.Profile != null), Is.EqualTo(25));
            string[] expected = { "NPC-021", "NPC-022", "NPC-003", "NPC-004", "NPC-006", "NPC-002" };
            foreach (string id in expected)
                Assert.That(cast.Count(actor => actor.Profile.Id == id), Is.EqualTo(1), id);
            Assert.That(objects.Any(go => go.GetComponents<Component>().Any(component =>
                component != null && component.GetType().Name == "NavMeshSurface")), Is.True);
            Assert.That(objects.Count(go => go.GetComponents<Component>().Any(component =>
                component != null && component.GetType().Name == "NavMeshLink")), Is.GreaterThan(0));
        }

        [Test]
        public void StoryReferencesAndSingleInteractionInputAreSavedInOfficeScene()
        {
            Assert.That(Components<PlayerInteractor>().Length, Is.EqualTo(1));
            SerializedObject interactorData = new SerializedObject(Components<PlayerInteractor>().Single());
            Assert.That((interactorData.FindProperty("aimColliderLayers").intValue & (1 << 2)),
                Is.Zero, "The visible aim sphere must see through the invisible player barrier.");
            ChapterOneDirector chapter = Components<ChapterOneDirector>().Single();
            SerializedObject data = new SerializedObject(chapter);
            Assert.That(data.FindProperty("dialogue").objectReferenceValue, Is.Not.Null);
            Assert.That(data.FindProperty("player").objectReferenceValue, Is.Not.Null);
            Assert.That(data.FindProperty("bossGate").objectReferenceValue, Is.Not.Null);
            Assert.That(data.FindProperty("rakaSeat").objectReferenceValue, Is.Not.Null);
            Assert.That(data.FindProperty("sintaSeat").objectReferenceValue, Is.Not.Null);
            Assert.That(data.FindProperty("bossDoorWait").objectReferenceValue, Is.Not.Null);
            foreach (string field in new[] { "bossChair", "bossMeetingLeft", "bossMeetingRight",
                "playerSeat", "playerMover", "playerSeating", "playerCamera",
                "meetingApproach", "bossWalkStart" })
                Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(data.FindProperty("quests").arraySize, Is.EqualTo(8));
            Assert.That(data.FindProperty("sequences").arraySize, Is.EqualTo(15));
            SerializedProperty castData = data.FindProperty("cast");
            Assert.That(castData.arraySize, Is.EqualTo(25),
                "Every office NPC must be registered so ordinary dialogue has speaker focus.");
            Assert.That(Enumerable.Range(0, castData.arraySize)
                .Select(index => castData.GetArrayElementAtIndex(index).objectReferenceValue)
                .Distinct().Count(), Is.EqualTo(25));
            SerializedProperty sequenceData = data.FindProperty("sequences");
            var sequenceIds = new HashSet<string>();
            for (int index = 0; index < sequenceData.arraySize; index++)
            {
                DialogueSequence sequence = sequenceData.GetArrayElementAtIndex(index)
                    .objectReferenceValue as DialogueSequence;
                Assert.That(sequence, Is.Not.Null, $"Sequence {index}");
                sequenceIds.Add(sequence.Id);
            }
            foreach (string id in new[] { "chapter.raka_last_intro", "chapter.sinta_last_intro",
                "chapter.boss_seating" })
                Assert.That(sequenceIds, Does.Contain(id), id);
            StoryDialogueController dialogue = Components<StoryDialogueController>().Single();
            SerializedObject dialogueData = new SerializedObject(dialogue);
            Assert.That(dialogueData.FindProperty("playerCamera").objectReferenceValue,
                Is.EqualTo(Components<PlayerCameraController>().Single()));
            StoryPlayerCinematicMover mover = Components<StoryPlayerCinematicMover>().Single();
            StoryPlayerSeating seating = Components<StoryPlayerSeating>().Single();
            Assert.That(mover.gameObject, Is.EqualTo(Components<PlayerInteractor>().Single().gameObject));
            Assert.That(seating.gameObject, Is.EqualTo(mover.gameObject));
            Assert.That(new SerializedObject(mover).FindProperty("characterController").objectReferenceValue,
                Is.EqualTo(mover.GetComponent<CharacterController>()));
            OfficeNpcAgent raka = Components<OfficeNpcAgent>().Single(actor =>
                actor.Profile != null && actor.Profile.Id == "NPC-003");
            Assert.That(mover.NavMeshAgentTypeId,
                Is.EqualTo(raka.GetComponent<NavMeshAgent>().agentTypeID));
            Assert.That(Components<QuestTrackerView>().Length, Is.EqualTo(1));
            Assert.That(Components<StoryDialogueView>().Length, Is.EqualTo(1));
            Assert.That(Components<StoryNotificationView>().Length, Is.EqualTo(1));
        }

        [Test]
        public void BossGateAndVehicleMarkerHaveUsableGeometry()
        {
            BossOfficeGate gate = Components<BossOfficeGate>().Single();
            Assert.That(gate.gameObject.name, Is.EqualTo("Office_ArchiveHQ_DoorLeaf_12"));
            Assert.That(objects.Single(go => go.name == "Boss Player Barrier").layer,
                Is.EqualTo(2));
            Assert.That(gate.GetComponent<DoorInteractable>(), Is.Not.Null);
            SerializedObject data = new SerializedObject(gate);
            foreach (string field in new[] { "roomZone", "playerBarrier", "corridorExit", "player", "npcDirector",
                "boss", "colleagueA", "colleagueB" })
                Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            BoxCollider room = objects.Single(go => go.name == "Boss Room Zone").GetComponent<BoxCollider>();
            Assert.That(room.isTrigger, Is.True);
            Assert.That(Vector3.Distance(room.transform.position,
                new Vector3(-5.65f, 16.9f, -34.31f)), Is.LessThan(0.05f));
            Assert.That(Vector3.Distance(room.size, new Vector3(4.9f, 2.8f, 10.2f)),
                Is.LessThan(0.05f));
            BoxCollider barrier = objects.Single(go => go.name == "Boss Player Barrier")
                .GetComponent<BoxCollider>();
            Assert.That(Vector3.Distance(barrier.size, new Vector3(0.22f, 2.7f, 1.2f)),
                Is.LessThan(0.05f));
            foreach (string name in new[] { "Raka Seat", "Player Seat", "Sinta Seat",
                "Boss Meeting Left", "Boss Meeting Right", "Boss Chair" })
                Assert.That(room.bounds.Contains(objects.Single(go => go.name == name).transform.position),
                    Is.True, name);
            VehicleArrivalZone arrival = Components<VehicleArrivalZone>().Single();
            SphereCollider zone = arrival.GetComponent<SphereCollider>();
            Assert.That(zone, Is.Not.Null);
            Assert.That(zone.radius, Is.InRange(1.5f, 6f));
            Assert.That(Vector3.Distance(arrival.transform.position,
                objects.Single(go => go.name == "SUV_Fleet_Black").transform.position),
                Is.LessThan(5f));
        }

        [Test]
        public void BossAndThreeSeatSofaUseFlagRoomLayout()
        {
            AssertAnchor("Boss Chair", new Vector3(-5.643f, 15.84f, -38.926f), 0f);
            AssertAnchor("Boss Meeting Left", new Vector3(-7.099f, 15.84f, -33.161f), 0f);
            AssertAnchor("Boss Meeting Right", new Vector3(-4.629f, 15.84f, -33.161f), 0f);
            AssertAnchor("Raka Seat", new Vector3(-5.140f, 15.84f, -30.262f), 180f);
            AssertAnchor("Player Seat", new Vector3(-5.864f, 15.84f, -30.262f), 180f);
            AssertAnchor("Sinta Seat", new Vector3(-6.589f, 15.84f, -30.262f), 180f);
            AssertAnchor("Meeting Approach", new Vector3(-4.68f, 15.86f, -31.07f), 0f);
            AssertAnchor("Boss Walk Start", new Vector3(-5.5f, 15.84f, -36.5f), 0f);
            OfficeNpcAgent boss = Components<OfficeNpcAgent>().Single(actor =>
                actor.Profile != null && actor.Profile.Id == "NPC-006");
            Assert.That(Vector3.Distance(boss.transform.position,
                objects.Single(go => go.name == "Boss Chair").transform.position),
                Is.LessThan(0.05f));
        }

        [Test]
        public void FlagRoomInteriorLinkBridgesTheTwoWalkableIslands()
        {
            NavMeshLink link = objects.Single(go => go.name == "Flag Room Interior Link")
                .GetComponent<NavMeshLink>();
            Assert.That(link, Is.Not.Null);
            NavMeshSurface surface = Components<NavMeshSurface>().Single();
            Assert.That(link.agentTypeID, Is.EqualTo(surface.agentTypeID));
            Assert.That(link.activated, Is.True);
            Assert.That(link.bidirectional, Is.True);
            Assert.That(link.width, Is.GreaterThanOrEqualTo(0.5f));
            Vector3 start = link.transform.position + link.transform.rotation * link.startPoint;
            Vector3 end = link.transform.position + link.transform.rotation * link.endPoint;
            Assert.That(Vector3.Distance(start, new Vector3(-5.53f, 15.86f, -32.5f)),
                Is.LessThan(0.05f));
            Assert.That(Vector3.Distance(end, new Vector3(-4.5f, 15.86f, -32.37f)),
                Is.LessThan(0.05f));
        }

        private void AssertAnchor(string name, Vector3 expectedPosition, float yaw)
        {
            Transform anchor = objects.Single(go => go.name == name).transform;
            Assert.That(Vector3.Distance(anchor.position, expectedPosition), Is.LessThan(0.05f), name);
            Assert.That(Vector3.Dot(anchor.forward, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward),
                Is.GreaterThan(0.99f), name);
        }

        private T[] Components<T>() where T : Component => objects.SelectMany(go => go.GetComponents<T>()).ToArray();
    }
}
