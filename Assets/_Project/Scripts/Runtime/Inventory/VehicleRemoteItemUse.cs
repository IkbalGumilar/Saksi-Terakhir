using SaksiTerakhir.Player;
using SaksiTerakhir.Vehicles;
using UnityEngine;

namespace SaksiTerakhir.Inventory
{
    [DisallowMultipleComponent]
    public sealed class VehicleRemoteItemUse : MonoBehaviour, IInventoryItemUseHandler
    {
        [SerializeField] private VehicleKeyUse vehicle;
        [SerializeField] private PlayerInputReader input;

        public bool HandlesItem(string id) => vehicle != null && vehicle.HandlesKey(id);

        public bool TryUseItem(InventoryEntry entry, Transform actor) => vehicle != null
            && vehicle.TryUseRemote(entry, actor);

        private void OnEnable()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (input != null) input.InteractPressed += TryExit;
        }

        private void OnDisable()
        {
            if (input != null) input.InteractPressed -= TryExit;
        }

        private void TryExit()
        {
            if (vehicle != null && vehicle.IsOccupied) vehicle.TryExit(transform);
        }
    }
}
