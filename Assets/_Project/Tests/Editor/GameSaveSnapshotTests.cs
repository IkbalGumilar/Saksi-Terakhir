using System;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Persistence;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class GameSaveSnapshotTests
    {
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
        private static GameSaveSnapshot Valid() => new GameSaveSnapshot {
            runId = Guid.NewGuid().ToString("D"), scenePath = "Assets/_Project/Scenes/Regional Archive Office.unity",
            savedUtc = "2026-10-04T12:00:00Z", chapter = new ChapterOneProgress(),
            player = new SavedPose(Vector3.zero, Quaternion.identity) };
    }
}
