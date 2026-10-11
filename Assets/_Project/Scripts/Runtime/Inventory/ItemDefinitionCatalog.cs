using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.Inventory
{
    [CreateAssetMenu(fileName = "ItemDefinitionCatalog", menuName = "Saksi Terakhir/Inventory/Item Definition Catalog")]
    public sealed class ItemDefinitionCatalog : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] definitions = Array.Empty<ItemDefinition>();

        public IReadOnlyList<ItemDefinition> Definitions => Array.AsReadOnly(definitions ?? Array.Empty<ItemDefinition>());

        public static ItemDefinitionCatalog LoadDefault() => Resources.Load<ItemDefinitionCatalog>("ItemDefinitionCatalog");

        public ItemDefinition Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || definitions == null) return null;
            foreach (ItemDefinition definition in definitions)
                if (definition != null && string.Equals(definition.Id, id, StringComparison.Ordinal))
                    return definition;
            return null;
        }
    }
}
