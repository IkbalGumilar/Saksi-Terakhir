using System;
using UnityEngine;

namespace SaksiTerakhir.Inventory
{
    public enum InventoryEntryType
    {
        Item,
        Document
    }

    [Serializable]
    public sealed class InventoryEntry
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private InventoryEntryType type;
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;

        public string Id => id;
        public string DisplayName => displayName;
        public InventoryEntryType Type => type;
        public string Description => description;
        public Sprite Icon => icon;

        public InventoryEntry(string id, string displayName, InventoryEntryType type,
            Sprite icon = null, string description = "")
        {
            this.id = id ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.type = type;
            this.icon = icon;
            this.description = description ?? string.Empty;
        }
    }
}
