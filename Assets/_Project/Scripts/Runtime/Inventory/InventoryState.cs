using System;
using System.Collections.Generic;

namespace SaksiTerakhir.Inventory
{
    public sealed class InventoryState
    {
        private readonly List<InventoryEntry> items = new List<InventoryEntry>();
        private readonly List<InventoryEntry> documents = new List<InventoryEntry>();
        private readonly List<InventoryEntry> heldItems = new List<InventoryEntry>();
        private InventoryEntry[] slots;

        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public int Capacity => Columns * Rows;
        public IReadOnlyList<InventoryEntry> Items { get; }
        public IReadOnlyList<InventoryEntry> Documents { get; }
        public IReadOnlyList<InventoryEntry> HeldItems { get; }
        public event Action Changed;

        public InventoryState(int columns = 3, int rows = 3)
        {
            if (columns < 1 || columns > 8)
                throw new ArgumentOutOfRangeException(nameof(columns), "Columns must be between 1 and 8.");
            if (rows < 1 || rows > 8)
                throw new ArgumentOutOfRangeException(nameof(rows), "Rows must be between 1 and 8.");

            Columns = columns;
            Rows = rows;
            Items = items.AsReadOnly();
            Documents = documents.AsReadOnly();
            HeldItems = heldItems.AsReadOnly();
            slots = new InventoryEntry[Capacity];
        }

        public bool TryAdd(InventoryEntry entry)
        {
            if (entry == null || ContainsInstance(entry.InstanceId)) return false;
            List<InventoryEntry> destination;
            switch (entry.Type)
            {
                case InventoryEntryType.Item:
                    if (items.Count >= Capacity) return false;
                    destination = items;
                    break;
                case InventoryEntryType.Document:
                    destination = documents;
                    break;
                default:
                    return false;
            }

            destination.Add(entry);
            if (entry.Type == InventoryEntryType.Item)
                slots[Array.IndexOf(slots, null)] = entry;
            Changed?.Invoke();
            return true;
        }

        public InventoryEntry GetItemAtSlot(int slotIndex) =>
            slotIndex >= 0 && slotIndex < slots.Length ? slots[slotIndex] : null;

        public int GetSlotIndex(InventoryEntry entry) => entry != null ? Array.IndexOf(slots, entry) : -1;

        public bool TryMoveToSlot(InventoryEntry entry, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return false;
            int previousIndex = GetSlotIndex(entry);
            if (previousIndex < 0) return false;
            if (previousIndex == slotIndex) return true;
            slots[previousIndex] = slots[slotIndex];
            slots[slotIndex] = entry;
            Changed?.Invoke();
            return true;
        }

        public bool IsHeld(InventoryEntry entry) => entry != null && heldItems.Contains(entry);

        public bool TryHold(InventoryEntry entry)
        {
            if (entry == null || !items.Contains(entry)) return false;
            if (heldItems.Contains(entry)) return true;
            if (heldItems.Count == 2) heldItems.RemoveAt(0);
            heldItems.Add(entry);
            Changed?.Invoke();
            return true;
        }

        public bool Release(InventoryEntry entry)
        {
            if (entry == null || !heldItems.Remove(entry)) return false;
            Changed?.Invoke();
            return true;
        }

        public bool Remove(InventoryEntry entry)
        {
            if (entry == null) return false;
            if (!items.Remove(entry) && !documents.Remove(entry)) return false;
            int slotIndex = GetSlotIndex(entry);
            if (slotIndex >= 0) slots[slotIndex] = null;
            heldItems.Remove(entry);
            Changed?.Invoke();
            return true;
        }

        public bool TryResize(int columns, int rows)
        {
            if (columns < 1 || columns > 8 || rows < 1 || rows > 8
                || columns * rows < items.Count) return false;
            if (Columns == columns && Rows == rows) return true;

            var resizedSlots = new InventoryEntry[columns * rows];
            Array.Copy(slots, resizedSlots, Math.Min(slots.Length, resizedSlots.Length));
            for (int index = resizedSlots.Length; index < slots.Length; index++)
                if (slots[index] != null)
                    resizedSlots[Array.IndexOf(resizedSlots, null)] = slots[index];
            Columns = columns;
            Rows = rows;
            slots = resizedSlots;
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (items.Count == 0 && documents.Count == 0) return;
            items.Clear();
            documents.Clear();
            heldItems.Clear();
            Array.Clear(slots, 0, slots.Length);
            Changed?.Invoke();
        }

        private bool ContainsInstance(string instanceId)
        {
            foreach (InventoryEntry entry in items)
                if (string.Equals(entry.InstanceId, instanceId, StringComparison.Ordinal)) return true;
            foreach (InventoryEntry entry in documents)
                if (string.Equals(entry.InstanceId, instanceId, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
