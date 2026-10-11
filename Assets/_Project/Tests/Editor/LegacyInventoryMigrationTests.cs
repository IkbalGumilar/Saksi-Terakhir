using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
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
    public sealed class LegacyInventoryMigrationTests
    {
        private Scene scene;
        private readonly List<Object> assets = new List<Object>();
        private InventoryPanelController inventory;
        private ItemDefinition key;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            key = Definition(CarKeyInventory.CarKeyId);
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(model, scene);
            model.name = "Migration key model";
            model.transform.localScale = Vector3.one * 0.1f;
            model.SetActive(false);
            Set(key, "modelPrefab", model);
            ItemDefinitionCatalog catalog = ScriptableObject.CreateInstance<ItemDefinitionCatalog>();
            assets.Add(catalog);
            Set(catalog, "definitions", new[] { key });
            var actor = new GameObject("Legacy inventory actor");
            SceneManager.MoveGameObjectToScene(actor, scene);
            actor.transform.position = new Vector3(20000, 20, 20000);
            actor.SetActive(false);
            inventory = actor.AddComponent<InventoryPanelController>();
            Set(inventory, "itemCatalog", catalog);
            inventory.RestoreState(new InventoryState());
        }

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.ClosePreviewScene(scene);
            foreach (Object asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void LegacyStoryFlagWithoutAnItemAddsTheCatalogKeyExactlyOnce()
        {
            GameSaveSnapshot snapshot = Snapshot(0);

            Migrate(snapshot);
            Migrate(snapshot);

            Assert.That(inventory.State.Items.Count, Is.EqualTo(1));
            InventoryEntry restored = inventory.State.Items[0];
            Assert.That(restored.Id, Is.EqualTo(CarKeyInventory.CarKeyId));
            Assert.That(restored.Definition, Is.SameAs(key));
            Assert.That(restored.InstanceId, Is.EqualTo("legacy:car-key:" + snapshot.runId));
            Assert.That(inventory.State.GetItemAtSlot(0), Is.SameAs(restored));
            Assert.That(KeyDrops(), Is.Empty);
        }

        [Test]
        public void LegacyMissingInventoryAlsoMigratesTheStoryOwnedKey()
        {
            GameSaveSnapshot snapshot = Snapshot(0);
            snapshot.inventory = null;

            Migrate(snapshot);

            Assert.That(inventory.State.Items.Count, Is.EqualTo(1));
            Assert.That(inventory.State.Items[0].Definition, Is.SameAs(key));
        }

        [Test]
        public void LegacyInventoryAlreadyContainingTheKeyKeepsItsOriginalInstance()
        {
            InventoryEntry existing = key.CreateEntry("already-owned-key");
            inventory.State.TryAdd(existing);

            Migrate(Snapshot(0));

            Assert.That(inventory.State.Items, Is.EqualTo(new[] { existing }));
            Assert.That(inventory.State.Items[0].InstanceId, Is.EqualTo("already-owned-key"));
            Assert.That(KeyDrops(), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ModernSaveWithoutACarriedKeyDoesNotGrantItAgain(bool hasDroppedKey)
        {
            ItemPickupInteractable existingDrop = null;
            if (hasDroppedKey)
            {
                existingDrop = InventoryWorldItemFactory.CreateAtPose(key, "deliberately-dropped-key",
                    inventory.transform.position + Vector3.forward * 2, Quaternion.identity);
                Assert.That(existingDrop, Is.Not.Null);
                SceneManager.MoveGameObjectToScene(existingDrop.gameObject, scene);
            }
            GameSaveSnapshot snapshot = Snapshot(1);
            snapshot.worldItems = WorldItemSnapshot.Capture(scene);

            Migrate(snapshot);

            Assert.That(inventory.State.Items, Is.Empty);
            Assert.That(KeyDrops().Length, Is.EqualTo(hasDroppedKey ? 1 : 0));
            if (hasDroppedKey)
                Assert.That(KeyDrops()[0], Is.SameAs(existingDrop));
        }

        [Test]
        public void FullLegacyInventoryKeepsItsContentsAndPlacesTheMissingKeySafelyOnTheGround()
        {
            InventoryEntry occupied = FillBag();
            GameSaveSnapshot snapshot = Snapshot(0);

            Migrate(snapshot);

            AssertBagUnchanged(occupied);
            ItemPickupInteractable[] drops = KeyDrops();
            Assert.That(drops.Length, Is.EqualTo(1));
            Assert.That(drops[0].InstanceId, Is.EqualTo("legacy:car-key:" + snapshot.runId));
            Assert.That(drops[0].Definition, Is.SameAs(key));
            Assert.That(drops[0].Collected, Is.False);
            Assert.That(drops[0].gameObject.activeSelf, Is.True);
            Assert.That(drops[0].GetComponent<Rigidbody>().useGravity, Is.True);
            Assert.That(drops[0].GetComponent<BoxCollider>().enabled, Is.True);
        }

        [Test]
        public void RepeatedFullBagMigrationKeepsOneGroundKeyForTheSameLegacyRun()
        {
            InventoryEntry occupied = FillBag();
            GameSaveSnapshot snapshot = Snapshot(0);

            Migrate(snapshot);
            Migrate(snapshot);

            AssertBagUnchanged(occupied);
            Assert.That(KeyDrops().Length, Is.EqualTo(1));
            Assert.That(KeyDrops()[0].InstanceId, Is.EqualTo("legacy:car-key:" + snapshot.runId));
        }

        [Test]
        public void UnsafeGroundPlacementFailsWithoutDiscardingOrExpandingTheFullLegacyInventory()
        {
            InventoryEntry occupied = FillBag();
            var obstruction = new GameObject("Unsafe legacy migration volume");
            // Preview scenes are omitted from the global Physics queries used by drops.
            // This fixture-owned collider stays in the normal scene and is destroyed below.
            assets.Add(obstruction);
            obstruction.transform.position = inventory.transform.position + Vector3.up;
            obstruction.AddComponent<BoxCollider>().size = Vector3.one * 10;
            Physics.SyncTransforms();
            Assert.That(Physics.CheckBox(inventory.transform.position + Vector3.up * 1.1f,
                Vector3.one * 0.075f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore), Is.True,
                "The fixture must block the physics world used by item drop placement.");

            Assert.Throws<InvalidOperationException>(() => Migrate(Snapshot(0)));

            AssertBagUnchanged(occupied);
            Assert.That(KeyDrops(), Is.Empty);
        }

        [Test]
        public void AStoryWithoutTheReceptionKeyFlagDoesNotCreateAKey()
        {
            GameSaveSnapshot snapshot = Snapshot(0);
            Set(snapshot.chapter, "hasCarKey", false);

            Migrate(snapshot);

            Assert.That(inventory.State.Items, Is.Empty);
            Assert.That(KeyDrops(), Is.Empty);
        }

        private InventoryEntry FillBag()
        {
            inventory.RestoreState(new InventoryState(1, 1));
            var occupied = new InventoryEntry("existing-battery", "Battery", InventoryEntryType.Item);
            inventory.State.TryAdd(occupied);
            inventory.State.TryHold(occupied);
            inventory.State.TryAdd(new InventoryEntry("permanent-note", "Note", InventoryEntryType.Document));
            return occupied;
        }

        private void AssertBagUnchanged(InventoryEntry occupied)
        {
            Assert.That(inventory.State.Capacity, Is.EqualTo(1));
            Assert.That(inventory.State.Items, Is.EqualTo(new[] { occupied }));
            Assert.That(inventory.State.GetItemAtSlot(0), Is.SameAs(occupied));
            Assert.That(inventory.State.HeldItems, Is.EqualTo(new[] { occupied }));
            Assert.That(inventory.State.Documents.Count, Is.EqualTo(1));
            Assert.That(inventory.State.Documents[0].Id, Is.EqualTo("permanent-note"));
        }

        private GameSaveSnapshot Snapshot(int layoutVersion)
        {
            var progress = new ChapterOneProgress();
            Set(progress, "hasCarKey", true);
            return new GameSaveSnapshot { runId = Guid.NewGuid().ToString("D"), chapter = progress,
                inventory = new SavedInventory { slotLayoutVersion = layoutVersion } };
        }

        private ItemPickupInteractable[] KeyDrops() => GameWorldSnapshot.FindAll<ItemPickupInteractable>(scene)
            .Where(pickup => pickup.RuntimeSpawned && pickup.enabled && pickup.Definition == key && !pickup.Collected).ToArray();

        private void Migrate(GameSaveSnapshot snapshot)
        {
            MethodInfo policy = typeof(GameWorldSnapshot).GetMethod("RestoreLegacyReceptionKey", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(policy, Is.Not.Null);
            try { policy.Invoke(null, new object[] { snapshot, inventory, inventory.transform }); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
        }

        private ItemDefinition Definition(string id)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            assets.Add(definition);
            Set(definition, "id", id);
            return definition;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
