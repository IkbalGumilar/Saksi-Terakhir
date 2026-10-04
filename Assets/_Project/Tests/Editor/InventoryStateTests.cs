using System;
using NUnit.Framework;
using SaksiTerakhir.Inventory;

namespace SaksiTerakhir.Tests
{
    public sealed class InventoryStateTests
    {
        [Test]
        public void InitialGridAcceptsNineItemsAndLeavesTheTenthOutside()
        {
            var inventory = new InventoryState();
            for (int index = 0; index < 9; index++)
                Assert.That(inventory.TryAdd(Item(index)), Is.True);

            InventoryEntry extra = Item(9);
            Assert.That(inventory.TryAdd(extra), Is.False);
            Assert.That(inventory.Items.Count, Is.EqualTo(9));
            Assert.That(inventory.Items, Has.No.Member(extra));
        }

        [TestCase(1, 1, 1)]
        [TestCase(1, 2, 2)]
        [TestCase(2, 1, 2)]
        [TestCase(4, 3, 12)]
        [TestCase(8, 8, 64)]
        public void RectangularGridAcceptsExactlyItsCapacity(int columns, int rows, int capacity)
        {
            var inventory = new InventoryState(columns, rows);
            for (int index = 0; index < capacity; index++)
                Assert.That(inventory.TryAdd(Item(index)), Is.True);

            Assert.That(inventory.TryAdd(Item(capacity)), Is.False);
            Assert.That(inventory.Columns, Is.EqualTo(columns));
            Assert.That(inventory.Rows, Is.EqualTo(rows));
            Assert.That(inventory.Capacity, Is.EqualTo(capacity));
        }

        [TestCase(0, 3)]
        [TestCase(3, 0)]
        [TestCase(9, 3)]
        [TestCase(3, 9)]
        public void DimensionsOutsideTheSupportedRangeAreRefused(int columns, int rows)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryState(columns, rows));
            var inventory = new InventoryState();
            Assert.That(inventory.TryResize(columns, rows), Is.False);
            Assert.That(inventory.Columns, Is.EqualTo(3));
            Assert.That(inventory.Rows, Is.EqualTo(3));
        }

        [Test]
        public void DocumentsRemainUnlimitedWhenThePhysicalGridIsFull()
        {
            var inventory = new InventoryState(1, 1);
            InventoryEntry item = Item(0);
            Assert.That(inventory.TryAdd(item), Is.True);
            for (int index = 0; index < 100; index++)
            {
                var document = new InventoryEntry("document-" + index, "Catatan " + index,
                    InventoryEntryType.Document);
                Assert.That(inventory.TryAdd(document), Is.True);
            }

            Assert.That(inventory.Items, Is.EqualTo(new[] { item }));
            Assert.That(inventory.Documents.Count, Is.EqualTo(100));
            Assert.That(inventory.Documents[0].Id, Is.EqualTo("document-0"));
            Assert.That(inventory.Documents[99].Id, Is.EqualTo("document-99"));
        }

        [Test]
        public void BagUpgradePreservesItemsAndDocumentsInTheirExistingOrder()
        {
            var inventory = new InventoryState();
            InventoryEntry first = Item(0);
            InventoryEntry second = Item(1);
            var document = new InventoryEntry("note", "Catatan", InventoryEntryType.Document);
            inventory.TryAdd(first);
            inventory.TryAdd(second);
            inventory.TryAdd(document);

            Assert.That(inventory.TryResize(4, 4), Is.True);
            Assert.That(inventory.Capacity, Is.EqualTo(16));
            Assert.That(inventory.Items, Is.EqualTo(new[] { first, second }));
            Assert.That(inventory.Documents, Is.EqualTo(new[] { document }));
            for (int index = 2; index < 16; index++)
                Assert.That(inventory.TryAdd(Item(index)), Is.True);
            Assert.That(inventory.TryAdd(Item(16)), Is.False);
        }

        [Test]
        public void ShrinkCannotDiscardItemsButCanFitAllExistingEntries()
        {
            var inventory = new InventoryState();
            InventoryEntry first = Item(0);
            InventoryEntry second = Item(1);
            inventory.TryAdd(first);
            inventory.TryAdd(second);

            Assert.That(inventory.TryResize(1, 1), Is.False);
            Assert.That(inventory.Capacity, Is.EqualTo(9));
            Assert.That(inventory.Items, Is.EqualTo(new[] { first, second }));
            Assert.That(inventory.TryResize(1, 2), Is.True);
            Assert.That(inventory.Items, Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void RemoveFreesOneSlotAndClearRemovesBothCollections()
        {
            var inventory = new InventoryState(1, 1);
            InventoryEntry item = Item(0);
            var document = new InventoryEntry("note", "Catatan", InventoryEntryType.Document);
            inventory.TryAdd(item);
            inventory.TryAdd(document);

            Assert.That(inventory.Remove(item), Is.True);
            Assert.That(inventory.Remove(item), Is.False);
            Assert.That(inventory.TryAdd(Item(1)), Is.True);
            Assert.That(inventory.Remove(document), Is.True);
            inventory.TryAdd(document);
            inventory.Clear();
            Assert.That(inventory.Items, Is.Empty);
            Assert.That(inventory.Documents, Is.Empty);
            Assert.That(inventory.Capacity, Is.EqualTo(1));
        }

        [Test]
        public void InvalidAndRepeatedEntriesDoNotCreateExtraSlots()
        {
            var inventory = new InventoryState();
            InventoryEntry item = Item(0);
            Assert.That(inventory.TryAdd(null), Is.False);
            Assert.That(inventory.TryAdd(item), Is.True);
            Assert.That(inventory.TryAdd(item), Is.False);
            Assert.That(inventory.Remove(null), Is.False);
            Assert.That(inventory.TryAdd(new InventoryEntry("invalid", "Invalid",
                (InventoryEntryType)99)), Is.False);
            Assert.That(inventory.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void ChangeEventOnlyReportsSuccessfulMutations()
        {
            var inventory = new InventoryState(1, 1);
            int changes = 0;
            inventory.Changed += () => changes++;
            InventoryEntry item = Item(0);
            inventory.TryAdd(item);
            inventory.TryAdd(Item(1));
            inventory.TryResize(1, 1);
            inventory.TryResize(9, 9);
            inventory.Remove(Item(2));
            Assert.That(changes, Is.EqualTo(1));
            inventory.TryResize(2, 2);
            inventory.Remove(item);
            inventory.TryAdd(new InventoryEntry("note", "Catatan", InventoryEntryType.Document));
            inventory.Clear();
            inventory.Clear();
            Assert.That(changes, Is.EqualTo(5));
        }

        private static InventoryEntry Item(int index) =>
            new InventoryEntry("item-" + index, "Barang " + index, InventoryEntryType.Item);
    }
}
