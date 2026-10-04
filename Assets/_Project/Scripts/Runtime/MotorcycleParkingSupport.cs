using UnityEngine;

namespace SaksiTerakhir.Vehicles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MotorcycleParkingSupport : MonoBehaviour
    {
        [SerializeField] private GameObject centerStandVisual;
        [SerializeField] private Collider[] parkingSupportColliders = new Collider[0];
        [SerializeField] private bool parkedOnStart = true;

        public bool IsParked { get; private set; }

        private void Awake()
        {
            SetParked(parkedOnStart);
        }

        public void Configure(GameObject standVisual, Collider[] supportColliders, bool startParked)
        {
            centerStandVisual = standVisual;
            parkingSupportColliders = supportColliders ?? new Collider[0];
            parkedOnStart = startParked;
            SetParked(startParked);
        }

        public void SetParked(bool parked)
        {
            IsParked = parked;

            if (centerStandVisual != null && centerStandVisual.activeSelf != parked)
            {
                centerStandVisual.SetActive(parked);
            }

            if (parkingSupportColliders == null)
            {
                return;
            }

            foreach (var supportCollider in parkingSupportColliders)
            {
                if (supportCollider != null)
                {
                    supportCollider.enabled = parked;
                }
            }
        }
    }
}
