using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using UnityEngine;
using UnityEngine.TestTools;

namespace SaksiTerakhir.Tests
{
    public sealed class InventoryPhysicsPlayModeTests
    {
        private readonly List<Object> created = new List<Object>();
        private ItemDefinition definition;
        private InventoryPanelController inventory;
        private float previousTimeScale;
        private float previousFixedDeltaTime;

        [SetUp]
        public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            previousFixedDeltaTime = Time.fixedDeltaTime;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            GameObject source = CreateObject("Physics test model source");
            source.SetActive(false);
            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.transform.SetParent(source.transform, false);
            mesh.transform.localScale = Vector3.one * 0.2f;
            definition = ScriptableObject.CreateInstance<ItemDefinition>();
            created.Add(definition);
            typeof(ItemDefinition).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(definition, "physics-test-item");
            typeof(ItemDefinition).GetField("modelPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(definition, source);

            GameObject actor = CreateObject("Physics test inventory actor");
            actor.SetActive(false);
            actor.transform.position = new Vector3(175f, 0f, 175f);
            inventory = actor.AddComponent<InventoryPanelController>();
            inventory.RestoreState(new InventoryState(1, 1));
            inventory.enabled = false;
            LogAssert.Expect(LogType.Warning,
                "Inventory belum terhubung: periksa panel dan Slot 1 pada kedua halaman.");
            actor.SetActive(true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object value in created)
                if (value != null) Object.Destroy(value);
            created.Clear();
            yield return null;
            Time.fixedDeltaTime = previousFixedDeltaTime;
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator Drop_SettlesOnFloor_AndRepeatedPickupKeepsOneInventoryInstance()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(floor);
            floor.name = "Physics test floor";
            floor.transform.position = inventory.transform.position + Vector3.down * 0.1f;
            floor.transform.localScale = new Vector3(8f, 0.2f, 8f);
            Physics.SyncTransforms();
            InventoryEntry entry = definition.CreateEntry("physics-instance");
            Assert.That(inventory.TryAdd(entry), Is.True);

            Assert.That(inventory.TryDrop(entry), Is.True);
            Assert.That(inventory.State.Items.Count, Is.Zero);
            ItemPickupInteractable pickup = FindSingleAvailablePickup();
            created.Add(pickup.gameObject);
            Rigidbody body = pickup.GetComponent<Rigidbody>();
            Assert.That(body.useGravity, Is.True);
            Assert.That(body.isKinematic, Is.False);
            float initialHeight = body.position.y;
            for (int frame = 0; frame < 100; frame++) yield return new WaitForFixedUpdate();

            Assert.That(body.position.y, Is.LessThan(initialHeight - 0.5f),
                "The Rigidbody must actually fall under gravity.");
            Assert.That(pickup.GetComponent<BoxCollider>().bounds.min.y, Is.InRange(-0.01f, 0.035f),
                "The aggregate collider must stop the drop at the floor.");
            Assert.That(body.linearVelocity.sqrMagnitude, Is.LessThan(0.01f),
                "The dropped item must settle after its floor contact.");
            Assert.That(pickup.InstanceId, Is.EqualTo("physics-instance"));

            for (int cycle = 0; cycle < 3; cycle++)
            {
                Assert.That(pickup.CanInteract(inventory.transform), Is.True);
                pickup.Interact(inventory.transform);
                pickup.Interact(inventory.transform);
                Assert.That(pickup.Collected, Is.True);
                Assert.That(pickup.gameObject.activeSelf, Is.False);
                Assert.That(inventory.State.Items.Count, Is.EqualTo(1));
                Assert.That(inventory.State.Items[0].InstanceId, Is.EqualTo("physics-instance"));
                Assert.That(AvailablePickups().Length, Is.Zero);
                yield return null;
                if (cycle == 2) break;
                Assert.That(inventory.TryDrop(inventory.State.Items[0]), Is.True);
                Assert.That(inventory.State.Items.Count, Is.Zero);
                pickup = FindSingleAvailablePickup();
                created.Add(pickup.gameObject);
                Assert.That(pickup.InstanceId, Is.EqualTo("physics-instance"));
            }
            Assert.That(inventory.State.Items.Count, Is.EqualTo(1));
            Assert.That(AvailablePickups().Length, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FailedDrop_KeepsInventoryAndHeldOwnership_AndDestroysStagedClone()
        {
            InventoryEntry entry = definition.CreateEntry("physics-instance");
            Assert.That(inventory.TryAdd(entry), Is.True);
            Assert.That(inventory.State.TryHold(entry), Is.True);
            GameObject obstruction = CreateObject("Physics test obstruction");
            obstruction.transform.position = inventory.transform.position + Vector3.up * 1.1f;
            obstruction.AddComponent<BoxCollider>().size = Vector3.one * 5f;
            Physics.SyncTransforms();

            Assert.That(inventory.TryDrop(entry), Is.False);
            Assert.That(inventory.State.Items.Count, Is.EqualTo(1));
            Assert.That(inventory.State.Items[0], Is.SameAs(entry));
            Assert.That(inventory.State.IsHeld(entry), Is.True);
            Assert.That(AvailablePickups().Length, Is.Zero,
                "A failed drop must never enable its staged pickup.");
            yield return null;

            Assert.That(Object.FindObjectsByType<ItemPickupInteractable>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Count(p => p.Definition == definition), Is.Zero,
                "The failed clone must be gone after deferred runtime destruction.");
            Assert.That(inventory.State.Items[0].InstanceId, Is.EqualTo("physics-instance"));
        }

        private ItemPickupInteractable FindSingleAvailablePickup()
        {
            ItemPickupInteractable[] pickups = AvailablePickups();
            Assert.That(pickups.Length, Is.EqualTo(1),
                "There must be exactly one available world owner after a successful drop.");
            return pickups[0];
        }

        private ItemPickupInteractable[] AvailablePickups() =>
            Object.FindObjectsByType<ItemPickupInteractable>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Where(p => p.Definition == definition && !p.Collected
                    && p.gameObject.activeInHierarchy).ToArray();

        private GameObject CreateObject(string name)
        {
            var value = new GameObject(name);
            created.Add(value);
            return value;
        }
    }
}
