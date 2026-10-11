using UnityEngine;
using UnityEngine.Serialization;

namespace SaksiTerakhir.Inventory
{
    [CreateAssetMenu(fileName = "Item_Baru", menuName = "Saksi Terakhir/Inventory/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField, InspectorName("ID")]
        private string id = string.Empty;

        [FormerlySerializedAs("displayName")]
        [SerializeField, InspectorName("Nama")]
        [Tooltip("Key nama pada LocalizationTable, misalnya item.car_key.name.")]
        private string nameKey = string.Empty;

        [FormerlySerializedAs("description")]
        [SerializeField, InspectorName("Deskripsi")]
        [Tooltip("Key deskripsi pada LocalizationTable, misalnya item.car_key.description.")]
        private string descriptionKey = string.Empty;

        [SerializeField, InspectorName("Jenis")]
        private InventoryEntryType type = InventoryEntryType.Item;

        [SerializeField, InspectorName("Icon")]
        private Sprite icon;

        [SerializeField, InspectorName("Prefab Model")]
        private GameObject modelPrefab;

        [SerializeField, InspectorName("Isi Dokumen"), TextArea(3, 12)]
        private string documentText = string.Empty;

        public string Id => id;
        public string NameKey => nameKey;
        public string DescriptionKey => descriptionKey;
        public InventoryEntryType Type => type;
        public bool IsDocument => type == InventoryEntryType.Document;
        public Sprite Icon => icon;
        public GameObject ModelPrefab => modelPrefab;
        public string DocumentText => documentText;

        public InventoryEntry CreateEntry(string instanceId = null) => new InventoryEntry(this, instanceId);
    }
}
