using UnityEngine;

namespace SaksiTerakhir.Vehicles
{
    [DisallowMultipleComponent]
    public sealed class VehicleWheelVisualSync : MonoBehaviour
    {
        [SerializeField] private WheelCollider[] wheelColliders = new WheelCollider[0];
        [SerializeField] private Transform[] wheelVisuals = new Transform[0];

        public void Configure(WheelCollider[] colliders, Transform[] visuals)
        {
            wheelColliders = colliders;
            wheelVisuals = visuals;
        }

        private void LateUpdate()
        {
            var count = Mathf.Min(wheelColliders.Length, wheelVisuals.Length);
            for (var i = 0; i < count; i++)
            {
                var wheel = wheelColliders[i];
                var visual = wheelVisuals[i];
                if (wheel == null || visual == null || !wheel.enabled)
                {
                    continue;
                }

                wheel.GetWorldPose(out var position, out var rotation);
                visual.SetPositionAndRotation(position, rotation);
            }
        }
    }
}
