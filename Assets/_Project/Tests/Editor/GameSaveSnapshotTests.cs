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
    public sealed class GameSaveSnapshotTests
    {
        private const string OriginalArchiveId = "document.wanaseta_original";
        private const string LegacyTranslationId = "document.wanaseta_translation";
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [Test]
        public void InventoryRestoreRetainsCapacityDocumentSeparationAndOrder()
        {
            var saved = new SavedInventory { columns = 4, rows = 4,
                items = new[] { Entry("car-key"), Entry("battery") },
                documents = new[] { Entry("village-archive"), Entry("agenda") } };
            InventoryState result = saved.CreateState();
            Assert.That(result.Capacity, Is.EqualTo(16));
            Assert.That(result.Items[0].Id, Is.EqualTo("car-key"));
            Assert.That(result.Items[1].Id, Is.EqualTo("battery"));
            Assert.That(result.Documents[0].Id, Is.EqualTo("village-archive"));
            Assert.That(result.Documents[1].Type, Is.EqualTo(InventoryEntryType.Document));
        }

        [Test]
        public void OverfullOrUnidentifiedInventoryCannotBeRestored()
        {
            var saved = new SavedInventory { columns = 1, rows = 1,
                items = new[] { Entry("key"), Entry("battery") } };
            Assert.That(saved.Validate(out _), Is.False);
            saved.items = new[] { Entry("key") };
            saved.documents = new[] { Entry("") };
            Assert.That(saved.Validate(out _), Is.False);
        }

        [Test]
        public void MultipleItemsOfTheSameTypeKeepSeparateSlots()
        {
            var saved = new SavedInventory { items = new[] { Entry("battery"), Entry("battery") } };
            InventoryState result = saved.CreateState();
            Assert.That(result.Items.Count, Is.EqualTo(2));
            Assert.That(result.Items[0], Is.Not.SameAs(result.Items[1]));
        }

        [Test]
        public void LegacyTranslationLookupResolvesTheCombinedArchiveDefinition()
        {
            ItemDefinition original = Document(OriginalArchiveId);
            ItemDefinition translation = Document(LegacyTranslationId);
            ItemDefinitionCatalog catalog = Catalog(translation, original);

            Assert.That(catalog.Find(LegacyTranslationId), Is.SameAs(original));
            Assert.That(catalog.Find(OriginalArchiveId), Is.SameAs(original));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OldArchiveAndTranslationRestoreAsOneDocumentWithTheOriginalIdentity(bool translationFirst)
        {
            ItemDefinition original = Document(OriginalArchiveId);
            ItemDefinitionCatalog catalog = Catalog(original);
            SavedInventoryEntry archive = Entry(OriginalArchiveId);
            archive.instanceId = "archive-original-instance";
            SavedInventoryEntry translation = Entry(LegacyTranslationId);
            translation.instanceId = "archive-translation-instance";
            var saved = new SavedInventory { slotLayoutVersion = 1,
                documents = translationFirst ? new[] { translation, archive } : new[] { archive, translation } };

            InventoryState restored = saved.CreateState(resolveDefinition: catalog.Find);
            SavedInventory roundTrip = SavedInventory.Capture(restored);

            Assert.That(restored.Documents.Count, Is.EqualTo(1));
            Assert.That(restored.Documents[0].Definition, Is.SameAs(original));
            Assert.That(restored.Documents[0].InstanceId, Is.EqualTo(archive.instanceId));
            Assert.That(roundTrip.documents[0].id, Is.EqualTo(OriginalArchiveId));
            Assert.That(roundTrip.documents[0].instanceId, Is.EqualTo(archive.instanceId));
            Assert.That(roundTrip.slotLayoutVersion, Is.EqualTo(1));
            Assert.That(saved.documents.Length, Is.EqualTo(2), "Restoring must not rewrite the loaded save.");
        }

        [Test]
        public void TranslationOnlySaveRestoresTheCombinedDefinitionAndKeepsItsSavedIdentity()
        {
            ItemDefinition original = Document(OriginalArchiveId);
            ItemDefinitionCatalog catalog = Catalog(original);
            SavedInventoryEntry translation = Entry(LegacyTranslationId);
            translation.instanceId = "translation-only-instance";
            var saved = new SavedInventory { slotLayoutVersion = 1, documents = new[] { translation } };

            InventoryState restored = saved.CreateState(resolveDefinition: catalog.Find);
            SavedInventory nextSave = SavedInventory.Capture(restored);
            InventoryState restoredAgain = nextSave.CreateState(resolveDefinition: catalog.Find);

            Assert.That(restored.Documents[0].Id, Is.EqualTo(OriginalArchiveId));
            Assert.That(restored.Documents[0].Definition, Is.SameAs(original));
            Assert.That(restoredAgain.Documents[0].InstanceId, Is.EqualTo(translation.instanceId));
            Assert.That(restoredAgain.Documents.Count, Is.EqualTo(1));
        }

        [Test]
        public void ArchiveMergeKeepsOtherDocumentsAndItemSlotsAndHeldOwnership()
        {
            SavedInventoryEntry firstNote = Entry("document.repeated-note");
            firstNote.instanceId = "first-note";
            SavedInventoryEntry secondNote = Entry("document.repeated-note");
            secondNote.instanceId = "second-note";
            SavedInventoryEntry translation = Entry(LegacyTranslationId);
            translation.instanceId = "translation-instance";
            SavedInventoryEntry archive = Entry(OriginalArchiveId);
            archive.instanceId = "archive-instance";
            SavedInventoryEntry battery = Entry("battery");
            battery.instanceId = "battery-instance";
            battery.slotIndex = 7;
            var saved = new SavedInventory { slotLayoutVersion = 1, items = new[] { battery },
                documents = new[] { firstNote, translation, archive, secondNote },
                heldInstanceIds = new[] { battery.instanceId } };

            InventoryState restored = saved.CreateState(resolveDefinition: _ => null);

            Assert.That(restored.Documents.Count, Is.EqualTo(3));
            Assert.That(restored.Documents[0].InstanceId, Is.EqualTo(firstNote.instanceId));
            Assert.That(restored.Documents[1].Id, Is.EqualTo(OriginalArchiveId));
            Assert.That(restored.Documents[1].InstanceId, Is.EqualTo(archive.instanceId));
            Assert.That(restored.Documents[2].InstanceId, Is.EqualTo(secondNote.instanceId));
            Assert.That(restored.GetItemAtSlot(7).InstanceId, Is.EqualTo(battery.instanceId));
            Assert.That(restored.HeldItems[0], Is.SameAs(restored.GetItemAtSlot(7)));
        }

        [Test]
        public void TranslationOnlyWithoutACatalogStillRestoresTheCanonicalDocumentId()
        {
            SavedInventoryEntry translation = Entry(LegacyTranslationId);
            translation.instanceId = "translation-instance";
            var saved = new SavedInventory { documents = new[] { translation } };

            InventoryState restored = saved.CreateState(resolveDefinition: _ => null);

            Assert.That(restored.Documents[0].Id, Is.EqualTo(OriginalArchiveId));
            Assert.That(restored.Documents[0].InstanceId, Is.EqualTo(translation.instanceId));
        }

        [Test]
        public void SnapshotRejectsInvalidPlayerCoordinatesAndVersions()
        {
            GameSaveSnapshot saved = Valid();
            Assert.That(saved.Validate(out _), Is.True);
            saved.player.position.x = float.NaN;
            Assert.That(saved.Validate(out _), Is.False);
            saved = Valid(); saved.version = 999;
            Assert.That(saved.Validate(out _), Is.False);
        }

        [Test]
        public void SnapshotRejectsDuplicatedNpcIdsInsteadOfRestoringOneTwice()
        {
            GameSaveSnapshot saved = Valid();
            saved.npcs = new[] { new SavedNpcPose { id = "NPC-003", pose = new SavedPose(Vector3.zero, Quaternion.identity) },
                new SavedNpcPose { id = "NPC-003", pose = new SavedPose(Vector3.one, Quaternion.identity) } };
            Assert.That(saved.Validate(out _), Is.False);
        }

        private static SavedInventoryEntry Entry(string id) => new SavedInventoryEntry
            { id = id, displayName = id, description = "test" };

        private ItemDefinition Document(string id)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            assets.Add(definition);
            typeof(ItemDefinition).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(definition, id);
            typeof(ItemDefinition).GetField("type", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(definition, InventoryEntryType.Document);
            return definition;
        }

        private ItemDefinitionCatalog Catalog(params ItemDefinition[] definitions)
        {
            ItemDefinitionCatalog catalog = ScriptableObject.CreateInstance<ItemDefinitionCatalog>();
            assets.Add(catalog);
            typeof(ItemDefinitionCatalog).GetField("definitions", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(catalog, definitions);
            return catalog;
        }

        private static GameSaveSnapshot Valid() => new GameSaveSnapshot {
            runId = Guid.NewGuid().ToString("D"), scenePath = "Assets/_Project/Scenes/Regional Archive Office.unity",
            savedUtc = "2026-10-04T12:00:00Z", chapter = new ChapterOneProgress(),
            player = new SavedPose(Vector3.zero, Quaternion.identity) };
    }
}
