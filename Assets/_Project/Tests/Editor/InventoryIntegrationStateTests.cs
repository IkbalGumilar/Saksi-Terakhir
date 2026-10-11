using System;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Persistence;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class InventoryIntegrationStateTests
    {
        [Test]
        public void MoveToEmptySlotKeepsAHoleAndAddFillsTheFirstEmptySlot()
        {
            var state = new InventoryState();
            InventoryEntry first = Item("first"), second = Item("second"), third = Item("third");
            state.TryAdd(first);
            state.TryAdd(second);

            Assert.That(state.TryMoveToSlot(first, 7), Is.True);
            Assert.That(state.GetItemAtSlot(0), Is.Null);
            Assert.That(state.GetItemAtSlot(1), Is.SameAs(second));
            Assert.That(state.GetItemAtSlot(7), Is.SameAs(first));
            Assert.That(state.Items.Count, Is.EqualTo(2));
            Assert.That(state.TryAdd(third), Is.True);
            Assert.That(state.GetSlotIndex(third), Is.EqualTo(0));
        }

        [Test]
        public void MovingOntoAnotherItemSwapsBothSlotsWithoutChangingCollectionOrder()
        {
            var state = new InventoryState();
            InventoryEntry first = Item("first"), second = Item("second");
            state.TryAdd(first);
            state.TryAdd(second);

            Assert.That(state.TryMoveToSlot(first, 1), Is.True);
            Assert.That(state.GetItemAtSlot(0), Is.SameAs(second));
            Assert.That(state.GetItemAtSlot(1), Is.SameAs(first));
            Assert.That(state.Items, Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void ShrinkKeepsInRangeSlotsAndCompactsOnlyEntriesOutsideTheNewGrid()
        {
            var state = new InventoryState();
            InventoryEntry first = Item("first"), second = Item("second"), third = Item("third");
            state.TryAdd(first);
            state.TryAdd(second);
            state.TryAdd(third);
            state.TryMoveToSlot(first, 8);
            state.TryMoveToSlot(second, 3);

            Assert.That(state.TryResize(2, 2), Is.True);
            Assert.That(state.GetSlotIndex(first), Is.EqualTo(0));
            Assert.That(state.GetSlotIndex(second), Is.EqualTo(3));
            Assert.That(state.GetSlotIndex(third), Is.EqualTo(2));
            Assert.That(state.TryResize(1, 2), Is.False);
            Assert.That(state.GetSlotIndex(second), Is.EqualTo(3));
        }

        [Test]
        public void HoldingAThirdItemReleasesTheOldestWithoutRemovingItFromTheGrid()
        {
            var state = new InventoryState();
            InventoryEntry first = Item("first"), second = Item("second"), third = Item("third");
            state.TryAdd(first);
            state.TryAdd(second);
            state.TryAdd(third);
            state.TryHold(first);
            state.TryHold(second);

            Assert.That(state.TryHold(first), Is.True);
            Assert.That(state.HeldItems, Is.EqualTo(new[] { first, second }));
            Assert.That(state.TryHold(third), Is.True);
            Assert.That(state.HeldItems, Is.EqualTo(new[] { second, third }));
            Assert.That(state.IsHeld(first), Is.False);
            Assert.That(state.GetItemAtSlot(0), Is.SameAs(first));
            Assert.That(state.Items.Count, Is.EqualTo(3));
        }

        [Test]
        public void RemovingAHeldItemClearsItsHandAndSlotWhileReleaseKeepsItsSlot()
        {
            var state = new InventoryState();
            InventoryEntry first = Item("first"), second = Item("second");
            state.TryAdd(first);
            state.TryAdd(second);
            state.TryHold(first);
            state.TryHold(second);

            Assert.That(state.Remove(first), Is.True);
            Assert.That(state.GetItemAtSlot(0), Is.Null);
            Assert.That(state.HeldItems, Is.EqualTo(new[] { second }));
            Assert.That(state.Release(second), Is.True);
            Assert.That(state.HeldItems, Is.Empty);
            Assert.That(state.GetItemAtSlot(1), Is.SameAs(second));
            Assert.That(state.Release(second), Is.False);
        }

        [Test]
        public void InvalidMovesAndHoldsDoNotMutateOrAnnounceChanges()
        {
            var state = new InventoryState(1, 1);
            InventoryEntry item = Item("item");
            var document = new InventoryEntry("note", "Note", InventoryEntryType.Document);
            state.TryAdd(item);
            state.TryAdd(document);
            int changes = 0;
            state.Changed += () => changes++;

            Assert.That(state.TryMoveToSlot(item, -1), Is.False);
            Assert.That(state.TryMoveToSlot(item, 1), Is.False);
            Assert.That(state.TryMoveToSlot(Item("outside"), 0), Is.False);
            Assert.That(state.TryMoveToSlot(document, 0), Is.False);
            Assert.That(state.TryMoveToSlot(item, 0), Is.True);
            Assert.That(state.TryHold(document), Is.False);
            Assert.That(state.TryHold(Item("outside")), Is.False);
            Assert.That(state.TryHold(null), Is.False);
            Assert.That(state.GetItemAtSlot(-1), Is.Null);
            Assert.That(state.GetItemAtSlot(1), Is.Null);
            Assert.That(changes, Is.Zero);
            Assert.That(state.TryHold(item), Is.True);
            Assert.That(state.TryHold(item), Is.True);
            Assert.That(changes, Is.EqualTo(1));
            state.Clear();
            Assert.That(state.GetItemAtSlot(0), Is.Null);
            Assert.That(state.HeldItems, Is.Empty);
        }

        [Test]
        public void DuplicateItemDefinitionsRemainSeparateInstancesButDuplicateInstanceIdsAreRefused()
        {
            ItemDefinition definition = Definition("battery");
            try
            {
                InventoryEntry first = definition.CreateEntry(), second = definition.CreateEntry();
                var state = new InventoryState();
                Assert.That(first.InstanceId, Is.Not.Empty);
                Assert.That(second.InstanceId, Is.Not.EqualTo(first.InstanceId));
                Assert.That(state.TryAdd(first), Is.True);
                Assert.That(state.TryAdd(second), Is.True);
                Assert.That(state.TryAdd(new InventoryEntry(definition, first.InstanceId)), Is.False);
                Assert.That(state.Items.Count, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void SaveRoundTripKeepsEmptySlotsSeparateInstancesAndHeldOrderUsingDefinitionIds()
        {
            ItemDefinition definition = Definition("battery");
            var catalog = ScriptableObject.CreateInstance<ItemDefinitionCatalog>();
            Set(catalog, "definitions", new[] { definition });
            try
            {
                var state = new InventoryState(8, 8);
                InventoryEntry first = definition.CreateEntry(), second = definition.CreateEntry();
                state.TryAdd(first);
                state.TryAdd(second);
                state.TryMoveToSlot(first, 63);
                state.TryMoveToSlot(second, 17);
                state.TryHold(second);
                state.TryHold(first);
                SavedInventory saved = JsonUtility.FromJson<SavedInventory>(JsonUtility.ToJson(SavedInventory.Capture(state)));
                int legacyIconCalls = 0;
                InventoryState restored = saved.CreateState(_ => { legacyIconCalls++; return null; }, catalog.Find);

                Assert.That(restored.Capacity, Is.EqualTo(64));
                Assert.That(restored.GetItemAtSlot(0), Is.Null);
                Assert.That(restored.GetItemAtSlot(63).InstanceId, Is.EqualTo(first.InstanceId));
                Assert.That(restored.GetItemAtSlot(17).InstanceId, Is.EqualTo(second.InstanceId));
                Assert.That(restored.GetItemAtSlot(63).Definition, Is.SameAs(definition));
                Assert.That(restored.HeldItems[0].InstanceId, Is.EqualTo(second.InstanceId));
                Assert.That(restored.HeldItems[1].InstanceId, Is.EqualTo(first.InstanceId));
                Assert.That(legacyIconCalls, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void LegacyJsonWithoutSlotsOrInstancesRestoresInOrderAndGainsSeparateInstances()
        {
            const string json = "{\"columns\":3,\"rows\":3,\"items\":[{\"id\":\"battery\",\"displayName\":\"Old battery\",\"iconName\":\"OldSprite\"},{\"id\":\"battery\",\"displayName\":\"Old battery\"}],\"documents\":[{\"id\":\"note\",\"displayName\":\"Old note\"}]}";
            SavedInventory saved = JsonUtility.FromJson<SavedInventory>(json);
            string resolvedName = null;
            InventoryState state = saved.CreateState(name => { if (!string.IsNullOrEmpty(name)) resolvedName = name; return null; });

            Assert.That(state.GetItemAtSlot(0).DisplayName, Is.EqualTo("Old battery"));
            Assert.That(state.GetItemAtSlot(1).Id, Is.EqualTo("battery"));
            Assert.That(state.Items[0].InstanceId, Is.Not.EqualTo(state.Items[1].InstanceId));
            Assert.That(state.Documents[0].Type, Is.EqualTo(InventoryEntryType.Document));
            Assert.That(resolvedName, Is.EqualTo("OldSprite"));
        }

        [Test]
        public void InvalidSavedSlotsInstancesOrHeldOwnershipAreRejected()
        {
            var state = new InventoryState();
            InventoryEntry first = Item("first"), second = Item("second");
            state.TryAdd(first);
            state.TryAdd(second);
            SavedInventory saved = SavedInventory.Capture(state);
            saved.items[1].slotIndex = saved.items[0].slotIndex;
            Assert.That(saved.Validate(out _), Is.False);
            saved = SavedInventory.Capture(state);
            saved.items[0].slotIndex = 9;
            Assert.That(saved.Validate(out _), Is.False);
            saved = SavedInventory.Capture(state);
            saved.items[1].instanceId = saved.items[0].instanceId;
            Assert.That(saved.Validate(out _), Is.False);
            saved = SavedInventory.Capture(state);
            saved.heldInstanceIds = new[] { "missing-instance" };
            Assert.That(saved.Validate(out _), Is.False);
            saved.heldInstanceIds = new[] { first.InstanceId, first.InstanceId };
            Assert.That(saved.Validate(out _), Is.False);
            saved.heldInstanceIds = new[] { first.InstanceId, second.InstanceId, "third" };
            Assert.That(saved.Validate(out _), Is.False);
        }

        [Test]
        public void DefinitionBackedEntriesRefreshLocalizedTextWithoutReplacingTheInstance()
        {
            const string preferenceKey = "Localization.Language";
            bool hadPreference = PlayerPrefs.HasKey(preferenceKey);
            string originalPreference = PlayerPrefs.GetString(preferenceKey);
            const BindingFlags staticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo tableField = typeof(LocalizationManager).GetField("table", staticPrivate);
            FieldInfo languageField = typeof(LocalizationManager).GetField("currentLanguage", staticPrivate);
            FieldInfo initializedField = typeof(LocalizationManager).GetField("isInitialized", staticPrivate);
            object originalTable = tableField.GetValue(null);
            object originalLanguage = languageField.GetValue(null);
            object originalInitialized = initializedField.GetValue(null);
            ItemDefinition definition = Definition("car-key");
            var table = ScriptableObject.CreateInstance<LocalizationTable>();
            table.entries = new[]
            {
                new LocalizedTextEntry { key = "item.car-key.name", english = "Car key", indonesian = "Kunci mobil" },
                new LocalizedTextEntry { key = "item.car-key.description", english = "Open the car", indonesian = "Buka mobil" }
            };
            try
            {
                InventoryEntry entry = definition.CreateEntry();
                string instanceId = entry.InstanceId;
                LocalizationManager.Initialize(table);
                LocalizationManager.SetLanguage(GameLanguage.English);
                Assert.That(entry.DisplayName, Is.EqualTo("Car key"));
                LocalizationManager.SetLanguage(GameLanguage.Indonesian);
                Assert.That(entry.DisplayName, Is.EqualTo("Kunci mobil"));
                Assert.That(entry.Description, Is.EqualTo("Buka mobil"));
                Assert.That(entry.InstanceId, Is.EqualTo(instanceId));
                Assert.That(entry.Definition, Is.SameAs(definition));
            }
            finally
            {
                if (hadPreference) PlayerPrefs.SetString(preferenceKey, originalPreference);
                else PlayerPrefs.DeleteKey(preferenceKey);
                PlayerPrefs.Save();
                tableField.SetValue(null, originalTable);
                languageField.SetValue(null, originalLanguage);
                initializedField.SetValue(null, originalInitialized);
                (typeof(LocalizationManager).GetField("LanguageChanged", staticPrivate).GetValue(null) as Action)?.Invoke();
                UnityEngine.Object.DestroyImmediate(table);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static InventoryEntry Item(string id) => new InventoryEntry(id, id, InventoryEntryType.Item);

        private static ItemDefinition Definition(string id)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            Set(definition, "id", id);
            Set(definition, "nameKey", "item." + id + ".name");
            Set(definition, "descriptionKey", "item." + id + ".description");
            return definition;
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
