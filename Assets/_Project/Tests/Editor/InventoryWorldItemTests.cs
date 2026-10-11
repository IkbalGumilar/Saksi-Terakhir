using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Inventory;
using UnityEngine;
using UnityEngine.Events;

namespace SaksiTerakhir.Tests
{
    public sealed class InventoryWorldItemTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void Pickup_WhenFull_RemainsAvailableAndKeepsItsIdentity()
        {
            InventoryPanelController controller = CreateController(1, 1);
            Assert.That(controller.State.TryAdd(CreateDefinition("occupied").CreateEntry()), Is.True);
            ItemPickupInteractable pickup = CreatePickup(CreateDefinition("key"), "world-key");
            Assert.That(pickup.CanInteract(controller.transform), Is.True,
                "The actor must resolve its inventory before exercising the capacity rejection.");

            pickup.Interact(controller.transform);

            Assert.That(pickup.Collected, Is.False);
            Assert.That(pickup.gameObject.activeSelf, Is.True);
            Assert.That(pickup.InstanceId, Is.EqualTo("world-key"));
            Assert.That(controller.State.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void Pickup_WhenAccepted_PreservesInstanceAndCannotBeCollectedTwice()
        {
            InventoryPanelController controller = CreateController(1, 1);
            ItemPickupInteractable pickup = CreatePickup(CreateDefinition("key"), "dropped-key");
            Assert.That(pickup.CanInteract(controller.transform), Is.True,
                "The pickup fixture needs an active actor with an initialized inventory.");

            pickup.Interact(controller.transform);
            pickup.Interact(controller.transform);

            Assert.That(pickup.Collected, Is.True);
            Assert.That(pickup.gameObject.activeSelf, Is.False);
            Assert.That(controller.State.Items.Count, Is.EqualTo(1));
            Assert.That(controller.State.Items[0].InstanceId, Is.EqualTo("dropped-key"));
        }

        [Test]
        public void NativePickups_WithSameName_HaveDistinctStableHierarchyIds()
        {
            InventoryPanelController controller = CreateController(1, 1);
            ItemPickupInteractable first = CreatePickup(CreateDefinition("key"), null);
            ItemPickupInteractable second = CreatePickup(first.Definition, null);
            first.name = second.name = "Same pickup";

            string firstId = first.InstanceId;
            string secondId = second.InstanceId;
            first.RestoreCollected(true);
            first.RestoreCollected(false);

            Assert.That(firstId, Is.Not.Empty);
            Assert.That(firstId, Is.Not.EqualTo(secondId));
            Assert.That(first.InstanceId, Is.EqualTo(firstId));
            Assert.That(first.CanInteract(controller.transform), Is.True);
        }

        [Test]
        public void Drop_CreatesOneAggregateColliderAndKeepsFullModelAndInstance()
        {
            ItemDefinition definition = CreateDefinition("book", true);
            GameObject model = definition.ModelPrefab;
            var secondPage = GameObject.CreatePrimitive(PrimitiveType.Cube);
            secondPage.transform.SetParent(model.transform, false);
            secondPage.transform.localPosition = new Vector3(0.15f, 0f, 0f);
            secondPage.transform.localScale = new Vector3(0.1f, 0.03f, 0.2f);
            InventoryEntry entry = definition.CreateEntry("book-instance");
            Transform actor = CreateObject("Actor").transform;
            actor.position = new Vector3(50f, 10f, 50f);

            bool spawned = InventoryWorldItemFactory.TrySpawn(definition, entry, actor,
                out ItemPickupInteractable pickup);
            if (pickup != null) created.Add(pickup.gameObject);

            Assert.That(spawned, Is.True);
            Assert.That(pickup.InstanceId, Is.EqualTo("book-instance"));
            Assert.That(pickup.RuntimeSpawned, Is.True);
            Assert.That(pickup.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(2));
            Assert.That(pickup.GetComponentsInChildren<Collider>().Count(c => c.enabled), Is.EqualTo(1));
            Assert.That(pickup.GetComponent<BoxCollider>(), Is.Not.Null);
            Rigidbody body = pickup.GetComponent<Rigidbody>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body.useGravity, Is.True);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
            Assert.That(pickup.GetComponentsInChildren<Transform>().All(t => t.gameObject.layer == 6), Is.True);
        }

        [Test]
        public void Drop_WhenAllSpaceAheadIsBlocked_FailsWithoutAnAvailablePickup()
        {
            ItemDefinition definition = CreateDefinition("key", true);
            Transform actor = CreateObject("Actor").transform;
            actor.position = new Vector3(75f, 10f, 75f);
            GameObject obstruction = CreateObject("Solid obstruction");
            obstruction.transform.position = actor.position + Vector3.up;
            obstruction.AddComponent<BoxCollider>().size = Vector3.one * 10f;
            Physics.SyncTransforms();

            bool spawned = InventoryWorldItemFactory.TrySpawn(definition,
                definition.CreateEntry("blocked-instance"), actor, out ItemPickupInteractable pickup);

            Assert.That(spawned, Is.False);
            Assert.That(pickup, Is.Null);
        }

        [Test]
        public void Drop_WhenTheViewpointIsInsideGeometry_DoesNotSpawnThroughThatGeometry()
        {
            ItemDefinition definition = CreateDefinition("key", true);
            Transform actor = CreateObject("Actor").transform;
            actor.position = new Vector3(80f, 10f, 80f);
            GameObject obstruction = CreateObject("Viewpoint obstruction");
            obstruction.transform.position = actor.position + Vector3.up * 1.1f;
            obstruction.AddComponent<BoxCollider>().size = Vector3.one * 0.15f;
            Physics.SyncTransforms();

            bool spawned = InventoryWorldItemFactory.TrySpawn(definition,
                definition.CreateEntry("inside-obstruction"), actor, out ItemPickupInteractable pickup);
            if (pickup != null) created.Add(pickup.gameObject);

            Assert.That(spawned, Is.False);
            Assert.That(pickup, Is.Null);
        }

        [Test]
        public void Drop_WithMissingModel_FailsWithoutChangingInventoryOwnership()
        {
            ItemDefinition definition = CreateDefinition("missing-model");
            InventoryEntry entry = definition.CreateEntry("still-owned");
            var state = new InventoryState(1, 1);
            state.TryAdd(entry);

            bool spawned = InventoryWorldItemFactory.TrySpawn(definition, entry,
                CreateObject("Actor").transform, out ItemPickupInteractable pickup);

            Assert.That(spawned, Is.False);
            Assert.That(pickup, Is.Null);
            Assert.That(state.Items, Does.Contain(entry));
        }

        [Test]
        public void Drop_DoesNotOverlapThePlayerCollider()
        {
            ItemDefinition definition = CreateDefinition("key", true);
            GameObject actor = CreateObject("Player collider");
            actor.transform.position = new Vector3(90f, 10f, 90f);
            BoxCollider playerCollider = actor.AddComponent<BoxCollider>();
            playerCollider.center = Vector3.up;
            playerCollider.size = new Vector3(1f, 2f, 1f);
            Physics.SyncTransforms();

            bool spawned = InventoryWorldItemFactory.TrySpawn(definition,
                definition.CreateEntry("player-clearance"), actor.transform, out ItemPickupInteractable pickup);
            if (pickup != null) created.Add(pickup.gameObject);

            Assert.That(spawned, Is.True);
            BoxCollider itemCollider = pickup.GetComponent<BoxCollider>();
            Assert.That(Physics.ComputePenetration(playerCollider, actor.transform.position,
                actor.transform.rotation, itemCollider, pickup.transform.position,
                pickup.transform.rotation, out _, out _), Is.False);
        }

        [Test]
        public void RestoreDrop_PreservesTheSavedRootPoseAndInstance()
        {
            ItemDefinition definition = CreateDefinition("saved-key", true);
            Vector3 position = new Vector3(125f, 10f, 125f);
            Quaternion rotation = Quaternion.Euler(15f, 40f, 25f);

            ItemPickupInteractable pickup = InventoryWorldItemFactory.CreateAtPose(definition,
                "saved-instance", position, rotation);
            if (pickup != null) created.Add(pickup.gameObject);

            Assert.That(pickup, Is.Not.Null);
            Assert.That(pickup.InstanceId, Is.EqualTo("saved-instance"));
            Assert.That(pickup.RuntimeSpawned, Is.True);
            Assert.That(Vector3.Distance(pickup.transform.position, position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(pickup.transform.rotation, rotation), Is.LessThan(0.001f));
        }

        [Test]
        public void HeldVisuals_FollowHeldOrderAndCannotParticipateInPhysicsOrInteraction()
        {
            ItemDefinition definition = CreateDefinition("held-model", true);
            definition.ModelPrefab.AddComponent<Rigidbody>();
            definition.ModelPrefab.AddComponent<ItemUseInteractable>();
            var state = new InventoryState();
            InventoryEntry first = definition.CreateEntry("held-first");
            InventoryEntry second = definition.CreateEntry("held-second");
            state.TryAdd(first);
            state.TryAdd(second);
            state.TryHold(first);
            state.TryHold(second);
            GameObject actor = CreateObject("Actor");
            GameObject cameraObject = CreateObject("Camera");
            cameraObject.transform.SetParent(actor.transform);
            cameraObject.AddComponent<Camera>();

            using (var view = new InventoryHeldItemView(actor.transform))
            {
                view.Refresh(state);

                Assert.That(actor.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(2));
                Assert.That(actor.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True);
                Assert.That(actor.GetComponentsInChildren<Rigidbody>(true).All(b => b.isKinematic && !b.useGravity), Is.True);
                Assert.That(actor.GetComponentsInChildren<Interactable>(true).All(i => !i.enabled), Is.True);
                Assert.That(actor.GetComponentsInChildren<Renderer>().All(r => r.gameObject.layer == 2), Is.True);

                state.Release(first);
                view.Refresh(state);
                Assert.That(actor.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(1));
                Assert.That(actor.transform.Find("Camera/Inventory Right Hand/Held held-second"), Is.Not.Null);
            }
            Assert.That(actor.GetComponentsInChildren<Renderer>().Length, Is.Zero);
        }

        [TestCase(100f, 100f, 100f)]
        [TestCase(0.01f, 0.01f, 0.01f)]
        [TestCase(100f, 50f, 25f)]
        public void HeldVisuals_PreserveModelSizeAndHandOffsetUnderScaledAttachmentParents(
            float scaleX, float scaleY, float scaleZ)
        {
            ItemDefinition definition = CreateDefinition("scaled-hand", true);
            definition.ModelPrefab.transform.GetChild(0).localPosition = new Vector3(0.03f, 0.02f, 0.01f);
            var state = new InventoryState();
            InventoryEntry entry = definition.CreateEntry("scaled-hand-instance");
            state.TryAdd(entry);
            state.TryHold(entry);
            GameObject actor = CreateObject("Imported actor");
            actor.transform.position = new Vector3(10f, 2f, 20f);
            actor.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
            GameObject camera = CreateObject("Camera");
            camera.transform.SetParent(actor.transform, false);
            camera.AddComponent<Camera>();

            using (var view = new InventoryHeldItemView(actor.transform))
            {
                view.Refresh(state);
                Renderer held = actor.GetComponentInChildren<Renderer>();

                Assert.That(held.bounds.size.x, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(held.bounds.size.y, Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(held.bounds.size.z, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(Vector3.Distance(held.bounds.center,
                    camera.transform.position + new Vector3(0.22f, -0.2f, 0.45f)), Is.LessThan(0.0001f));
                Assert.That(definition.ModelPrefab.transform.localScale, Is.EqualTo(Vector3.one));
            }
        }

        [Test]
        public void HeldVisuals_KeepImportedBatteryAtItsAuthoredPhysicalSize()
        {
            ItemDefinition definition = CreateDefinition("imported-battery", true);
            definition.ModelPrefab.transform.localScale = Vector3.one * 100f;
            definition.ModelPrefab.transform.GetChild(0).localScale = new Vector3(0.00018f, 0.00065f, 0.00018f);
            var state = new InventoryState();
            InventoryEntry entry = definition.CreateEntry("battery-instance");
            state.TryAdd(entry);
            state.TryHold(entry);
            GameObject actor = CreateObject("Imported actor");
            actor.transform.localScale = Vector3.one * 100f;

            using (var view = new InventoryHeldItemView(actor.transform))
            {
                view.Refresh(state);
                Renderer held = actor.GetComponentInChildren<Renderer>();

                Assert.That(held.bounds.size.y, Is.EqualTo(0.065f).Within(0.0001f));
                Assert.That(held.bounds.size.x, Is.EqualTo(0.018f).Within(0.0001f));
                Assert.That(definition.ModelPrefab.transform.localScale, Is.EqualTo(Vector3.one * 100f));
            }
        }

        [Test]
        public void HeldVisuals_CapOversizedSourceInWorldMetresRatherThanImportedHandUnits()
        {
            ItemDefinition definition = CreateDefinition("oversized-source", true);
            definition.ModelPrefab.transform.localScale = Vector3.one * 100f;
            var state = new InventoryState();
            InventoryEntry entry = definition.CreateEntry("oversized-instance");
            state.TryAdd(entry);
            state.TryHold(entry);
            GameObject actor = CreateObject("Imported actor");
            actor.transform.localScale = Vector3.one * 100f;

            using (var view = new InventoryHeldItemView(actor.transform))
            {
                view.Refresh(state);
                Renderer held = actor.GetComponentInChildren<Renderer>();

                Assert.That(held.bounds.size.z, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(definition.ModelPrefab.transform.localScale, Is.EqualTo(Vector3.one * 100f));
            }
        }

        [Test]
        public void UseTarget_OnlyInvokesCallbackForMatchingEntryWithinReach()
        {
            ItemDefinition key = CreateDefinition("key");
            GameObject targetObject = CreateObject("Use target");
            targetObject.AddComponent<BoxCollider>();
            ItemUseInteractable target = targetObject.AddComponent<ItemUseInteractable>();
            SetField(target, "requiredItem", key);
            int calls = 0;
            var callback = new UnityEvent();
            callback.AddListener(() => calls++);
            SetField(target, "onUsed", callback);
            Transform actor = CreateObject("Actor").transform;

            Assert.That(target.TryUseItem(CreateDefinition("wrong").CreateEntry(), actor), Is.False);
            actor.position = Vector3.one * 10f;
            Assert.That(target.TryUseItem(key.CreateEntry(), actor), Is.False);
            actor.position = Vector3.zero;
            Assert.That(target.TryUseItem(key.CreateEntry(), actor), Is.True);
            Assert.That(calls, Is.EqualTo(1));
            targetObject.SetActive(false);
            Assert.That(target.TryUseItem(key.CreateEntry(), actor), Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void UseTarget_WhenAnObstacleBlocksReach_DoesNotInvokeCallback()
        {
            ItemDefinition key = CreateDefinition("key");
            GameObject targetObject = CreateObject("Blocked use target");
            targetObject.transform.position = new Vector3(150f, 10f, 151.5f);
            targetObject.AddComponent<BoxCollider>().size = Vector3.one * 0.2f;
            ItemUseInteractable target = targetObject.AddComponent<ItemUseInteractable>();
            SetField(target, "requiredItem", key);
            int calls = 0;
            target.OnUsed.AddListener(() => calls++);
            Transform actor = CreateObject("Actor").transform;
            actor.position = new Vector3(150f, 10f, 150f);
            GameObject wall = CreateObject("Wall");
            wall.transform.position = actor.position + Vector3.forward * 0.75f;
            wall.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 0.1f);
            Physics.SyncTransforms();

            Assert.That(target.TryUseItem(key.CreateEntry(), actor), Is.False);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void UseTarget_ReentrantCallback_CannotInvokeTheSameAttemptTwice()
        {
            ItemDefinition key = CreateDefinition("key");
            ItemUseInteractable target = CreateObject("Use target").AddComponent<ItemUseInteractable>();
            SetField(target, "requiredItem", key);
            Transform actor = CreateObject("Actor").transform;
            InventoryEntry entry = key.CreateEntry();
            int calls = 0;
            target.OnUsed.AddListener(() =>
            {
                calls++;
                Assert.That(target.TryUseItem(entry, actor), Is.False);
            });

            Assert.That(target.TryUseItem(entry, actor), Is.True);
            Assert.That(calls, Is.EqualTo(1));
        }

        private ItemDefinition CreateDefinition(string id, bool withModel = false)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            created.Add(definition);
            SetField(definition, "id", id);
            if (withModel)
            {
                GameObject model = CreateObject("Model source");
                model.SetActive(false);
                GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mesh.transform.SetParent(model.transform, false);
                mesh.transform.localScale = new Vector3(0.1f, 0.04f, 0.2f);
                SetField(definition, "modelPrefab", model);
            }
            return definition;
        }

        private ItemPickupInteractable CreatePickup(ItemDefinition definition, string instanceId)
        {
            ItemPickupInteractable pickup = CreateObject("Pickup").AddComponent<ItemPickupInteractable>();
            pickup.Configure(definition, instanceId, false);
            return pickup;
        }

        private InventoryPanelController CreateController(int columns, int rows)
        {
            GameObject actor = CreateObject("Inventory actor");
            actor.SetActive(false);
            InventoryPanelController controller = actor.AddComponent<InventoryPanelController>();
            controller.RestoreState(new InventoryState(columns, rows));
            controller.enabled = false;
            actor.SetActive(true);
            return controller;
        }

        private GameObject CreateObject(string name)
        {
            var value = new GameObject(name);
            created.Add(value);
            return value;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
