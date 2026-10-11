using UnityEngine;

namespace SaksiTerakhir.Inventory
{
    public interface IInventoryItemUseTarget
    {
        bool AcceptsItem(string itemId);
        bool TryUseItem(InventoryEntry entry, Transform actor);
    }
}
