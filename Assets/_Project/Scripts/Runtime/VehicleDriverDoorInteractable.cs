using SaksiTerakhir.Interaction;
using SaksiTerakhir.Inventory;
using UnityEngine;

namespace SaksiTerakhir.Vehicles
{
    [DisallowMultipleComponent]
    public sealed class VehicleDriverDoorInteractable : Interactable, IInventoryItemUseTarget
    {
        [SerializeField] private VehicleKeyUse vehicle;

        public override bool CanInteract(Transform actor) => base.CanInteract(actor) && vehicle != null
            && vehicle.CanBoard && !vehicle.IsOccupied && actor != null && actor.GetComponentInParent<InventoryPanelController>() != null
            && vehicle.IsWithinDriverDoorReach(actor);

        public override void Interact(Transform actor)
        {
            if (!CanInteract(actor)) return;
            ItemKeyLock keyLock = vehicle.KeyLock;
            if (keyLock != null && keyLock.IsLocked) keyLock.RequestUnlock(this, actor);
            else vehicle.TryBoardFromDoor(actor);
        }

        public bool AcceptsItem(string id) => isActiveAndEnabled && vehicle != null
            && vehicle.KeyLock != null && vehicle.KeyLock.AcceptsItem(id);

        public bool TryUseItem(InventoryEntry entry, Transform actor) => CanInteract(actor)
            && entry != null && AcceptsItem(entry.Id) && vehicle.TryUnlockAndBoard(entry, actor);

        protected override string ResolvePromptKey() => vehicle != null && vehicle.KeyLock != null
            && vehicle.KeyLock.IsLocked ? "interact.door.open" : "interact.item.use";
    }
}
