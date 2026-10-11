using UnityEngine;

namespace SaksiTerakhir.Inventory
{
    // Player-local effects handle inventory use without a selected world target.
    public interface IInventoryItemUseHandler
    {
        bool HandlesItem(string itemId);
        bool TryUseItem(InventoryEntry entry, Transform actor);
    }
}
