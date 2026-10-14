using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.Inventory
{
    [CreateAssetMenu(fileName = "ItemDefinitionCatalog", menuName = "Saksi Terakhir/Inventory/Item Definition Catalog")]
    public sealed class ItemDefinitionCatalog : ScriptableObject
    {
        public const string WanasetaArchiveId = "document.wanaseta_original";
        public const string LegacyWanasetaTranslationId = "document.wanaseta_translation";

        [SerializeField] private ItemDefinition[] definitions = Array.Empty<ItemDefinition>();

        public IReadOnlyList<ItemDefinition> Definitions => Array.AsReadOnly(definitions ?? Array.Empty<ItemDefinition>());

        public static ItemDefinitionCatalog LoadDefault() => Resources.Load<ItemDefinitionCatalog>("ItemDefinitionCatalog");

        public static string CanonicalId(string id) =>
            id == LegacyWanasetaTranslationId ? WanasetaArchiveId : id;

        public ItemDefinition Find(string id)
        {
            id = CanonicalId(id);
            if (string.IsNullOrWhiteSpace(id) || definitions == null) return null;
            foreach (ItemDefinition definition in definitions)
                if (definition != null && string.Equals(definition.Id, id, StringComparison.Ordinal))
                    return definition;
            return null;
        }
    }
}
