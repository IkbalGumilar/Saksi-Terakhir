using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Persistence;
using SaksiTerakhir.Story;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SaksiTerakhir.Tests
{
    public sealed class WorldItemSnapshotTests
    {
        private Scene scene;
        private readonly List<Object> assets = new List<Object>();

        [SetUp]
        public void SetUp() => scene = EditorSceneManager.NewPreviewScene();

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.ClosePreviewScene(scene);
            foreach (Object asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [Test]
        public void CaptureIncludesInactiveNativePickupsButOmitsCollectedRuntimeRootsAndDisabledClones()
        {
            ItemDefinition definition = Definition("battery", true);
            ItemPickupInteractable native = Pickup(definition, "native-battery");
            native.RestoreCollected(true);
            ItemPickupInteractable drop = Drop(definition, "active-drop", new Vector3(2, 3, 4));
            Drop(definition, "already-carried", Vector3.zero).RestoreCollected(true);
            ItemPickupInteractable disabled = Pickup(definition, "held-clone");
            disabled.enabled = false;

            SavedWorldItem[] records = WorldItemSnapshot.Capture(scene);

            Assert.That(records.Length, Is.EqualTo(2));
            Assert.That(records.Single(record => record.instanceId == native.InstanceId).collected, Is.True);
            SavedWorldItem savedDrop = records.Single(record => record.instanceId == drop.InstanceId);
            Assert.That(savedDrop.runtimeSpawned, Is.True);
            Assert.That(savedDrop.definitionId, Is.EqualTo("battery"));
            Assert.That(savedDrop.pose.position, Is.EqualTo(new Vector3(2, 3, 4)));
        }

        [Test]
        public void EmptyRestoreCleansDropsAndResetsNativePickupsWithoutEnablingAuthoredInactiveObjects()
        {
            ItemDefinition definition = Definition("battery", true);
            ItemPickupInteractable native = Pickup(definition, "native-battery");
            ItemPickupInteractable inactive = Pickup(definition, "quest-gated-battery");
            inactive.gameObject.SetActive(false);
            WorldItemSnapshot.Capture(scene);
            native.RestoreCollected(true);
            inactive.RestoreCollected(true);
            ItemPickupInteractable staleDrop = Drop(definition, "stale-drop", Vector3.zero);

            WorldItemSnapshot.Restore(scene, null);

            Assert.That(staleDrop == null, Is.True);
            Assert.That(native.Collected, Is.False);
            Assert.That(native.gameObject.activeSelf, Is.True);
            Assert.That(inactive.Collected, Is.False);
            Assert.That(inactive.gameObject.activeSelf, Is.False);
            Assert.That(WorldItemSnapshot.Capture(scene).Any(record => record.runtimeSpawned), Is.False);
        }

        [Test]
        public void RepeatedRestoreKeepsOneDroppedOwnerAndPreservesTheNativeCollectedRecord()
        {
            ItemDefinition definition = Definition("battery", true);
            ItemPickupInteractable native = Pickup(definition, "native-battery");
            WorldItemSnapshot.Capture(scene);
            native.RestoreCollected(true);
            ItemPickupInteractable original = Drop(definition, native.InstanceId, new Vector3(7, 8, 9));
            original.transform.rotation = Quaternion.Euler(0, 35, 0);
            SavedWorldItem[] records = WorldItemSnapshot.Capture(scene);
            Assert.That(WorldItemSnapshot.Validate(records, null, out string error), Is.True, error);

            WorldItemSnapshot.Restore(scene, records, null, id => id == "battery" ? definition : null);
            WorldItemSnapshot.Restore(scene, records, null, id => id == "battery" ? definition : null);

            ItemPickupInteractable[] drops = GameWorldSnapshot.FindAll<ItemPickupInteractable>(scene)
                .Where(pickup => pickup.RuntimeSpawned && pickup.enabled).ToArray();
            Assert.That(drops.Length, Is.EqualTo(1));
            Assert.That(drops[0].InstanceId, Is.EqualTo(native.InstanceId));
            Assert.That(drops[0].transform.position, Is.EqualTo(new Vector3(7, 8, 9)));
            Assert.That(Quaternion.Angle(drops[0].transform.rotation, Quaternion.Euler(0, 35, 0)), Is.LessThan(0.001f));
            Assert.That(native.Collected, Is.True);
            Assert.That(native.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void CollectedWorldRecordsMayShareTheCarriedIdentityButAnAvailableWorldRecordMayNot()
        {
            ItemDefinition definition = Definition("battery");
            var state = new InventoryState();
            state.TryAdd(definition.CreateEntry("same-instance"));
            SavedInventory inventory = SavedInventory.Capture(state);
            SavedWorldItem record = Record("battery", "same-instance", true, true);

            Assert.That(WorldItemSnapshot.Validate(new[] { record }, inventory, out string error), Is.True, error);
            record.collected = false;
            Assert.That(WorldItemSnapshot.Validate(new[] { record }, inventory, out _), Is.False);
            record.collected = true;
            record.definitionId = "another-item";
            Assert.That(WorldItemSnapshot.Validate(new[] { record }, inventory, out _), Is.False);
        }

        [Test]
        public void SaveValidationRejectsRepeatedWorldOwnersAndInvalidPosesButAllowsLegacyMissingWorldRecords()
        {
            GameSaveSnapshot saved = ValidSnapshot();
            saved.worldItems = null;
            Assert.That(saved.Validate(out string error), Is.True, error);
            saved.worldItems = new[] { Record("battery", "drop", true, false), Record("battery", "drop", true, false) };
            Assert.That(saved.Validate(out _), Is.False);
            saved.worldItems = new[] { Record("battery", "drop", true, false) };
            saved.worldItems[0].pose.position.x = float.NaN;
            Assert.That(saved.Validate(out _), Is.False);
            saved.worldItems[0].pose = new SavedPose(Vector3.zero, Quaternion.identity);
            saved.worldItems[0].instanceId = "";
            Assert.That(saved.Validate(out _), Is.False);
        }

        [Test]
        public void CollectedRuntimeRecordsAreCleanedInsteadOfBeingSpawnedAgain()
        {
            ItemDefinition definition = Definition("battery", true);
            ItemPickupInteractable obsolete = Drop(definition, "carried-battery", Vector3.zero);
            var state = new InventoryState();
            state.TryAdd(definition.CreateEntry("carried-battery"));

            WorldItemSnapshot.Restore(scene, new[] { Record("battery", "carried-battery", true, true) },
                SavedInventory.Capture(state), id => definition);

            Assert.That(obsolete == null, Is.True);
            Assert.That(GameWorldSnapshot.FindAll<ItemPickupInteractable>(scene), Is.Empty);
        }

        [Test]
        public void FailedDropReconstructionLeavesTheExistingWorldAndNativePickupUntouched()
        {
            ItemDefinition definition = Definition("battery", true);
            ItemDefinition missingModel = Definition("missing-model");
            ItemPickupInteractable native = Pickup(definition, "native-battery");
            ItemPickupInteractable previousDrop = Drop(definition, "previous-drop", Vector3.zero);
            SavedWorldItem[] records = { Record("missing-model", "unrestorable", true, false) };

            Assert.Throws<InvalidOperationException>(() => WorldItemSnapshot.Restore(scene, records,
                null, id => id == "missing-model" ? missingModel : definition));

            Assert.That(previousDrop != null && previousDrop.gameObject.activeSelf, Is.True);
            Assert.That(native.Collected, Is.False);
            Assert.That(native.gameObject.activeSelf, Is.True);
            Assert.That(WorldItemSnapshot.Capture(scene).Any(record => record.instanceId == "unrestorable"), Is.False);
        }

        [Test]
        public void LegacyRestoreWithACarriedNativeIdentityKeepsThatNativePickupCollected()
        {
            ItemDefinition definition = Definition("battery");
            ItemPickupInteractable native = Pickup(definition, "native-battery");
            var state = new InventoryState();
            state.TryAdd(definition.CreateEntry(native.InstanceId));

            WorldItemSnapshot.Restore(scene, Array.Empty<SavedWorldItem>(), SavedInventory.Capture(state));

            Assert.That(native.Collected, Is.True);
            Assert.That(native.gameObject.activeSelf, Is.False);
        }

        private ItemDefinition Definition(string id, bool withModel = false)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            assets.Add(definition);
            Set(definition, "id", id);
            if (withModel)
            {
                GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(model, scene);
                model.name = "Model source";
                model.SetActive(false);
                model.transform.localScale = Vector3.one * 0.2f;
                Set(definition, "modelPrefab", model);
            }
            return definition;
        }

        private ItemPickupInteractable Pickup(ItemDefinition definition, string instanceId)
        {
            var value = new GameObject("Native pickup");
            SceneManager.MoveGameObjectToScene(value, scene);
            ItemPickupInteractable pickup = value.AddComponent<ItemPickupInteractable>();
            pickup.Configure(definition, instanceId, false);
            return pickup;
        }

        private ItemPickupInteractable Drop(ItemDefinition definition, string instanceId, Vector3 position)
        {
            ItemPickupInteractable drop = InventoryWorldItemFactory.CreateAtPose(definition, instanceId, position, Quaternion.identity);
            Assert.That(drop, Is.Not.Null);
            SceneManager.MoveGameObjectToScene(drop.gameObject, scene);
            return drop;
        }

        private static SavedWorldItem Record(string definitionId, string instanceId, bool runtime, bool collected) =>
            new SavedWorldItem { definitionId = definitionId, instanceId = instanceId, runtimeSpawned = runtime,
                collected = collected, activeSelf = !collected, pose = new SavedPose(Vector3.zero, Quaternion.identity) };

        private static GameSaveSnapshot ValidSnapshot() => new GameSaveSnapshot {
            runId = Guid.NewGuid().ToString("D"), scenePath = "Assets/_Project/Scenes/Regional Archive Office.unity",
            savedUtc = "2026-10-05T12:00:00Z", chapter = new ChapterOneProgress(),
            player = new SavedPose(Vector3.zero, Quaternion.identity) };

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
