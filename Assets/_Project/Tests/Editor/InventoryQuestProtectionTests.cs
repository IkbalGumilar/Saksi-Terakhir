using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Persistence;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class InventoryQuestProtectionTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (UnityEngine.Object value in created)
                if (value != null) UnityEngine.Object.DestroyImmediate(value);
            created.Clear();
        }

        [TestCase(QuestCategory.Main)]
        [TestCase(QuestCategory.Side)]
        [TestCase(QuestCategory.Optional)]
        public void ActiveQuestProtectsRequiredItemWithoutReservingAnExtraSlot(QuestCategory category)
        {
            var state = new InventoryState(1, 1);
            var entry = new InventoryEntry("test.required", "Required", InventoryEntryType.Item);
            state.TryAdd(entry);
            QuestJournalEntry quest = Quest(category, StoryQuestStatus.Active, entry.Id);
            Assert.That(InventoryQuestProtection.CategoryFor(entry, state, new[] { quest }), Is.EqualTo(category));
            Assert.That(state.Capacity, Is.EqualTo(1));
            Assert.That(state.TryAdd(new InventoryEntry("other", "Other", InventoryEntryType.Item)), Is.False);
            Assert.That(state.TryHold(entry), Is.True);
            Assert.That(state.TryMoveToSlot(entry, 0), Is.True);
        }

        [TestCase(StoryQuestStatus.Completed)]
        [TestCase(StoryQuestStatus.Skipped)]
        [TestCase(StoryQuestStatus.NotStarted)]
        public void InactiveQuestDoesNotProtectItsItem(StoryQuestStatus status)
        {
            var entry = new InventoryEntry("test.required", "Required", InventoryEntryType.Item);
            Assert.That(InventoryQuestProtection.CategoryFor(entry, new InventoryState(),
                new[] { Quest(QuestCategory.Side, status, entry.Id) }), Is.Null);
        }

        [Test]
        public void SharedItemUsesMainThenSideThenOptionalPriority()
        {
            var entry = new InventoryEntry("test.shared", "Shared", InventoryEntryType.Item);
            var state = new InventoryState();
            QuestJournalEntry main = Quest(QuestCategory.Main, StoryQuestStatus.Active, entry.Id);
            QuestJournalEntry side = Quest(QuestCategory.Side, StoryQuestStatus.Active, entry.Id);
            QuestJournalEntry optional = Quest(QuestCategory.Optional, StoryQuestStatus.Active, entry.Id);
            Assert.That(InventoryQuestProtection.CategoryFor(entry, state, new[] { optional, side, main }), Is.EqualTo(QuestCategory.Main));
            Assert.That(InventoryQuestProtection.CategoryFor(entry, state, new[] { optional, side }), Is.EqualTo(QuestCategory.Side));
        }

        [TestCase("item.car_key")]
        [TestCase("car-key")]
        public void JourneyKeyRemainsProtectedUntilExplicitlyReleasedAtTheVillage(string keyId)
        {
            var state = new InventoryState();
            var entry = new InventoryEntry(keyId, "Key", InventoryEntryType.Item);
            state.TryAdd(entry);
            Assert.That(InventoryQuestProtection.CategoryFor(entry, state, null), Is.EqualTo(QuestCategory.Main));
            Assert.That(state.ReleaseQuestItem(keyId), Is.True);
            Assert.That(InventoryQuestProtection.CategoryFor(entry, state, null), Is.Null);
            InventoryState restored = SavedInventory.Capture(state).CreateState();
            Assert.That(InventoryQuestProtection.CategoryFor(restored.Items[0], restored, null), Is.Null);
        }

        [Test]
        public void StoryRemovalSurvivesSaveAndPreventsAWorldPickupFromReintroducingTheItem()
        {
            var state = new InventoryState();
            var key = new InventoryEntry("item.car_key", "Key", InventoryEntryType.Item);
            state.TryAdd(key);
            state.TryHold(key);
            state.ReleaseQuestItem(key.Id);
            Assert.That(state.RetireStoryItem(key.Id), Is.True);
            Assert.That(state.Items, Is.Empty);
            Assert.That(state.HeldItems, Is.Empty);
            SavedInventory save = SavedInventory.Capture(state);
            Assert.That(save.Validate(out string error), Is.True, error);
            InventoryState restored = save.CreateState();
            Assert.That(restored.TryAdd(new InventoryEntry(key.Id, "Key", InventoryEntryType.Item)), Is.False);
            restored.Clear();
            Assert.That(restored.TryAdd(key), Is.True, "New Game must reset story removal even with an empty bag.");
        }

        [Test]
        public void DropRejectsTheJourneyKeyBeforeCreatingAnyWorldObject()
        {
            var owner = new GameObject("Quest item inventory fixture");
            owner.SetActive(false);
            created.Add(owner);
            var controller = owner.AddComponent<InventoryPanelController>();
            controller.RestoreState(new InventoryState());
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            created.Add(definition);
            typeof(ItemDefinition).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, "item.car_key");
            controller.TryAdd(definition, "mission-key");
            InventoryEntry key = controller.State.Items[0];
            controller.ToggleHeld(key);
            string feedback = null;
            controller.FeedbackRequested += message => feedback = message;
            Assert.That(controller.TryDrop(key), Is.False);
            Assert.That(feedback, Is.EqualTo("inventory.quest_item_locked"));
            Assert.That(controller.State.GetItemAtSlot(0), Is.SameAs(key));
            Assert.That(controller.State.IsHeld(key), Is.True);
        }

        [Test]
        public void TragedyRetiresThePreviouslyDroppedKeyAsWellAsTheCarriedCopies()
        {
            var owner = new GameObject("Story item lifecycle fixture");
            owner.SetActive(false);
            created.Add(owner);
            var controller = owner.AddComponent<InventoryPanelController>();
            controller.RestoreState(new InventoryState());
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            created.Add(definition);
            typeof(ItemDefinition).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(definition, CarKeyInventory.CarKeyId);
            var droppedRoot = new GameObject("Previously dropped key");
            created.Add(droppedRoot);
            var pickup = droppedRoot.AddComponent<ItemPickupInteractable>();
            pickup.Configure(definition, "dropped-key", false);
            Assert.That(controller.RetireStoryItem(CarKeyInventory.CarKeyId), Is.True);
            Assert.That(pickup.Collected, Is.True);
            Assert.That(droppedRoot.activeSelf, Is.False);
            Assert.That(controller.TryAdd(definition), Is.False);
            Assert.That(WorldItemSnapshot.Validate(WorldItemSnapshot.Capture(owner.scene),
                SavedInventory.Capture(controller.State), out string error), Is.True, error);
        }

        [Test]
        public void LegacySaveWithoutLifecycleFieldsRestoresTheJourneyKeyAsRequired()
        {
            var state = new InventoryState();
            state.TryAdd(new InventoryEntry(CarKeyInventory.CarKeyId, "Key", InventoryEntryType.Item));
            SavedInventory snapshot = SavedInventory.Capture(state);
            snapshot.releasedQuestItemIds = null;
            snapshot.retiredStoryItemIds = null;
            Assert.That(snapshot.Validate(out string error), Is.True, error);
            InventoryState restored = snapshot.CreateState();
            Assert.That(InventoryQuestProtection.CategoryFor(restored.Items[0], restored, null), Is.EqualTo(QuestCategory.Main));
        }

        private QuestJournalEntry Quest(QuestCategory category, StoryQuestStatus status, string itemId)
        {
            var definition = ScriptableObject.CreateInstance<QuestDefinition>();
            created.Add(definition);
            definition.Configure("test.quest." + category, category, "test.title", Array.Empty<string>());
            definition.ConfigureDetails("test.description", "test.location", new[] { itemId });
            return new QuestJournalEntry(definition, status, false);
        }
    }
}
