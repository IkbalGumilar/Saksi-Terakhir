using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class InventoryControllerIntegrationTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (Object value in created) if (value != null) Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void PickupRejectionKeepsExistingItemAndReportsCapacity()
        {
            InventoryPanelController controller = Controller(new InventoryState(1, 1));
            ItemDefinition definition = Definition("test.item");
            string feedback = null;
            controller.FeedbackRequested += key => feedback = key;
            Assert.That(controller.TryAdd(definition, "first"), Is.True);
            Assert.That(controller.TryAdd(definition, "second"), Is.False);
            Assert.That(controller.State.Items.Count, Is.EqualTo(1));
            Assert.That(controller.State.Items[0].InstanceId, Is.EqualTo("first"));
            Assert.That(feedback, Is.EqualTo("inventory.full"));
        }

        [Test]
        public void MatchingHeldItemUsesTargetImmediatelyWithoutOpeningInventory()
        {
            InventoryPanelController controller = Controller(new InventoryState());
            controller.TryAdd(Definition("test.key"), "key");
            InventoryEntry key = controller.State.Items[0];
            controller.ToggleHeld(key);
            var target = new UseTarget("test.key");
            Assert.That(controller.RequestUseForTarget(target), Is.True);
            Assert.That(target.Uses, Is.EqualTo(1));
            Assert.That(target.UsedEntry, Is.SameAs(key));
            Assert.That(controller.IsOpen, Is.False);
            Assert.That(controller.State.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void UseWithoutConfiguredEffectPreservesItemAndReportsUnavailable()
        {
            InventoryPanelController controller = Controller(new InventoryState());
            controller.TryAdd(Definition("test.item"));
            InventoryEntry entry = controller.State.Items[0];
            string feedback = null;
            controller.FeedbackRequested += key => feedback = key;
            Assert.That(controller.TryUse(entry), Is.False);
            Assert.That(controller.State.Items[0], Is.SameAs(entry));
            Assert.That(feedback, Is.EqualTo("inventory.cannot_use_yet"));
        }

        [Test]
        public void DropWithMissingModelDoesNotRemoveHeldItem()
        {
            InventoryPanelController controller = Controller(new InventoryState());
            controller.TryAdd(Definition("test.no_model"));
            InventoryEntry entry = controller.State.Items[0];
            controller.ToggleHeld(entry);
            Assert.That(controller.TryDrop(entry), Is.False);
            Assert.That(controller.State.Items[0], Is.SameAs(entry));
            Assert.That(controller.State.IsHeld(entry), Is.True);
        }

        [Test]
        public void PermanentDocumentHasNoUseHoldOrDropAction()
        {
            InventoryPanelController controller = Controller(new InventoryState(1, 1));
            var document = new InventoryEntry("note", "Catatan", InventoryEntryType.Document);
            controller.TryAdd(document);
            controller.ToggleHeld(document);
            Assert.That(controller.TryUse(document), Is.False);
            Assert.That(controller.TryDrop(document), Is.False);
            Assert.That(controller.State.HeldItems, Is.Empty);
            Assert.That(controller.State.Documents, Has.Member(document));
        }

        private InventoryPanelController Controller(InventoryState state)
        {
            var owner = new GameObject("Inventory controller fixture");
            owner.SetActive(false);
            created.Add(owner);
            InventoryPanelController result = owner.AddComponent<InventoryPanelController>();
            result.RestoreState(state);
            return result;
        }

        private ItemDefinition Definition(string id)
        {
            ItemDefinition value = ScriptableObject.CreateInstance<ItemDefinition>();
            typeof(ItemDefinition).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, id);
            created.Add(value);
            return value;
        }

        private sealed class UseTarget : IInventoryItemUseTarget
        {
            private readonly string required;
            public int Uses { get; private set; }
            public InventoryEntry UsedEntry { get; private set; }
            public UseTarget(string required) => this.required = required;
            public bool AcceptsItem(string id) => id == required;
            public bool TryUseItem(InventoryEntry entry, Transform actor)
            {
                if (!AcceptsItem(entry.Id)) return false;
                Uses++;
                UsedEntry = entry;
                return true;
            }
        }
    }
}
