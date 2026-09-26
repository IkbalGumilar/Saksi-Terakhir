using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            Assert.That(data.FindProperty("quests").arraySize, Is.EqualTo(8));
            Assert.That(data.FindProperty("sequences").arraySize, Is.EqualTo(12));
            Assert.That(Components<QuestTrackerView>().Length, Is.EqualTo(1));
            Assert.That(Components<StoryDialogueView>().Length, Is.EqualTo(1));
            Assert.That(Components<StoryNotificationView>().Length, Is.EqualTo(1));
        }

        [Test]
        public void BossGateAndVehicleMarkerHaveUsableGeometry()
        {
            BossOfficeGate gate = Components<BossOfficeGate>().Single();
            Assert.That(objects.Single(go => go.name == "Boss Player Barrier").layer,
                Is.EqualTo(2));
            Assert.That(gate.GetComponent<DoorInteractable>(), Is.Not.Null);
            SerializedObject data = new SerializedObject(gate);
            foreach (string field in new[] { "roomZone", "playerBarrier", "corridorExit", "player", "npcDirector",
                "boss", "colleagueA", "colleagueB" })
                Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            VehicleArrivalZone arrival = Components<VehicleArrivalZone>().Single();
            SphereCollider zone = arrival.GetComponent<SphereCollider>();
            Assert.That(zone, Is.Not.Null);
            Assert.That(zone.radius, Is.InRange(1.5f, 6f));
            Assert.That(Vector3.Distance(arrival.transform.position,
                objects.Single(go => go.name == "SUV_Fleet_Black").transform.position),
                Is.LessThan(5f));
        }

        private T[] Components<T>() where T : Component => objects.SelectMany(go => go.GetComponents<T>()).ToArray();
    }
}
