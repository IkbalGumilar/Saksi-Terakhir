using System;
using System.Collections.Generic;

namespace SaksiTerakhir.Inventory
{
    public sealed class InventoryState
    {
        private readonly List<InventoryEntry> items = new List<InventoryEntry>();
        private readonly List<InventoryEntry> documents = new List<InventoryEntry>();

        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public int Capacity => Columns * Rows;
        public IReadOnlyList<InventoryEntry> Items { get; }
        public IReadOnlyList<InventoryEntry> Documents { get; }
        public event Action Changed;

        public InventoryState(int columns = 3, int rows = 3)
        {
            if (columns < 1 || columns > 8)
                throw new ArgumentOutOfRangeException(nameof(columns), "Columns must be between 1 and 8.");
            if (rows < 1 || rows > 7)
                throw new ArgumentOutOfRangeException(nameof(rows), "Rows must be between 1 and 7.");

            Columns = columns;
            Rows = rows;
            Items = items.AsReadOnly();
            Documents = documents.AsReadOnly();
        }

        public bool TryAdd(InventoryEntry entry)
        {
            if (entry == null) return false;
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

            if (destination.Contains(entry)) return false;
            destination.Add(entry);
            Changed?.Invoke();
            return true;
        }

        public bool Remove(InventoryEntry entry)
        {
            if (entry == null) return false;
            if (!items.Remove(entry) && !documents.Remove(entry)) return false;
            Changed?.Invoke();
            return true;
        }

        public bool TryResize(int columns, int rows)
        {
            if (columns < 1 || columns > 8 || rows < 1 || rows > 7
                || columns * rows < items.Count) return false;
            if (Columns == columns && Rows == rows) return true;

            Columns = columns;
            Rows = rows;
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (items.Count == 0 && documents.Count == 0) return;
            items.Clear();
            documents.Clear();
            Changed?.Invoke();
        }
    }
}
