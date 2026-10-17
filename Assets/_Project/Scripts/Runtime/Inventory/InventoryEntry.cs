using System;
using SaksiTerakhir.Localization;
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
        [SerializeField] private ItemDefinition definition;
        [SerializeField] private string instanceId;

        public ItemDefinition Definition => definition;
        public string InstanceId
        {
            get
            {
                // Entries serialized before instance tracking receive one stable identity on first use.
                if (string.IsNullOrWhiteSpace(instanceId)) instanceId = Guid.NewGuid().ToString("N");
                return instanceId;
            }
        }
        public string Id => definition != null ? definition.Id : id;
        public string DisplayName => definition != null ? LocalizationManager.Get(definition.NameKey) : LocalizationManager.Translate(displayName);
        public InventoryEntryType Type => definition != null ? definition.Type : type;
        public string Description => definition != null ? LocalizationManager.Get(definition.DescriptionKey) : LocalizationManager.Translate(description);
        public Sprite Icon => definition != null ? definition.Icon : icon;

        public InventoryEntry(string id, string displayName, InventoryEntryType type,
            Sprite icon = null, string description = "", string instanceId = null)
        {
            this.id = id ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.type = type;
            this.icon = icon;
            this.description = description ?? string.Empty;
            this.instanceId = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId;
        }

        public InventoryEntry(ItemDefinition definition, string instanceId = null)
            : this(definition != null ? definition.Id : string.Empty,
                definition != null ? definition.NameKey : string.Empty,
                definition != null ? definition.Type : InventoryEntryType.Item,
                definition != null ? definition.Icon : null,
                definition != null ? definition.DescriptionKey : string.Empty, instanceId)
        {
            this.definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
        }
    }
}
