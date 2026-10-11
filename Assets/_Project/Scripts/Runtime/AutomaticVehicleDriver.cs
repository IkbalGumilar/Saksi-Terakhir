using System;
using UnityEngine;

namespace SaksiTerakhir.Vehicles
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class AutomaticVehicleDriver : MonoBehaviour
    {
        [SerializeField] private Vector3[] roadPoints = Array.Empty<Vector3>();
        [SerializeField] private float[] roadSpeeds = Array.Empty<float>();
        [SerializeField] private Vector3[] parkingPoints = Array.Empty<Vector3>();
        [SerializeField] private float[] parkingSpeeds = Array.Empty<float>();
        [SerializeField] private Vector3[] stationDeparturePoints = Array.Empty<Vector3>();
        [SerializeField] private float[] stationDepartureSpeeds = Array.Empty<float>();
        [SerializeField, Min(.1f)] private float acceleration = 1.8f;
        [SerializeField, Min(.1f)] private float braking = 3f;
        [SerializeField] private LayerMask obstacleLayers = ~0;
        [SerializeField] private Vector3 probeCenter = new Vector3(0, .85f, 0);
        [SerializeField] private Vector3 probeHalfSize = new Vector3(.92f, .6f, 2.2f);
        [SerializeField] private Transform[] wheels = Array.Empty<Transform>();
        [SerializeField, Min(.01f)] private float wheelRadius = .375f;

        private VehicleRoutePath path;
        private Rigidbody body;
        private VehicleKeyUse occupants;
        private VehicleJourneyDirector journey;
        private readonly RaycastHit[] obstacles = new RaycastHit[32];
        private float distance;
        private float speed;
        public bool IsMoving { get; private set; }
        public bool IsParkingRoute { get; private set; }
        public bool IsStationDepartureRoute { get; private set; }
        public bool IsBlocked { get; private set; }
        public float Distance => distance;
        public float Speed => speed;
        public event Action Arrived;

        public VehicleRoutePath BuildPath(bool parking)
            => new VehicleRoutePath(parking ? parkingPoints : roadPoints, parking ? parkingSpeeds : roadSpeeds);

        public VehicleRoutePath BuildStationDeparturePath()
            => new VehicleRoutePath(stationDeparturePoints, stationDepartureSpeeds);

        public void Begin(bool parking, float savedDistance = 0)
            => BeginRoute(BuildPath(parking), parking, false, savedDistance);

        public void BeginStationDeparture(float savedDistance = 0)
            => BeginRoute(BuildStationDeparturePath(), false, true, savedDistance);

        private void BeginRoute(VehicleRoutePath route, bool parking, bool stationDeparture, float savedDistance)
        {
            path = route;
            body = GetComponent<Rigidbody>();
            occupants = GetComponent<VehicleKeyUse>();
            journey = GetComponent<VehicleJourneyDirector>();
            body.isKinematic = true;
            body.useGravity = false;
            // The authored lane path owns suspension and wheels during this scripted journey.
            foreach (WheelCollider wheel in GetComponentsInChildren<WheelCollider>()) wheel.enabled = false;
            VehicleWheelVisualSync wheelSync = GetComponent<VehicleWheelVisualSync>();
            if (wheelSync != null) wheelSync.enabled = false;
            IsParkingRoute = parking;
            IsStationDepartureRoute = stationDeparture;
            distance = Mathf.Clamp(savedDistance, 0, path.Length);
            speed = 0;
            Vector3 position = path.PositionAt(distance);
            Quaternion rotation = path.RotationAt(distance);
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            IsMoving = distance < path.Length;
            IsBlocked = false;
        }

        public void Stop()
        {
            IsMoving = false;
            IsBlocked = false;
            speed = 0;
        }

        private void FixedUpdate()
        {
            if (!IsMoving) return;
            if (journey != null && journey.GameplaySuspended) return;
            Advance(Time.fixedDeltaTime);
        }

        public void Advance(float deltaTime)
        {
            if (!IsMoving || path == null || deltaTime <= 0 || !float.IsFinite(deltaTime)) return;
            float targetSpeed = path.SpeedLimitAt(distance, braking);
            speed = Mathf.MoveTowards(speed, targetSpeed, (targetSpeed < speed ? braking : acceleration) * deltaTime);
            float nextDistance = Mathf.Min(path.Length, distance + speed * deltaTime);
            Vector3 next = path.PositionAt(nextDistance);
            Quaternion rotation = path.RotationAt(nextDistance);
            if (!CanMoveTo(next, rotation)) { speed = 0; IsBlocked = true; return; }
            IsBlocked = false;
            float moved = nextDistance - distance;
            distance = nextDistance;
            body.MovePosition(next);
            body.MoveRotation(rotation);
            if (!Application.isPlaying) { body.position = next; body.rotation = rotation; }
            foreach (Transform wheel in wheels)
                if (wheel != null) wheel.Rotate(Vector3.right, moved / wheelRadius * Mathf.Rad2Deg, Space.Self);
            if (path.Length - distance > .04f) return;
            distance = path.Length;
            body.MovePosition(path.PositionAt(distance));
            body.MoveRotation(path.RotationAt(distance));
            if (!Application.isPlaying) { body.position = path.PositionAt(distance); body.rotation = path.RotationAt(distance); }
            Stop();
            Arrived?.Invoke();
        }

        private bool CanMoveTo(Vector3 next, Quaternion rotation)
        {
            Vector3 current = body.position;
            Vector3 delta = next - current;
            if (delta.sqrMagnitude < .000001f) return true;
            float castLength = delta.magnitude + .55f;
            int count = Physics.BoxCastNonAlloc(current + rotation * probeCenter, probeHalfSize,
                delta.normalized, obstacles, rotation, castLength, obstacleLayers, QueryTriggerInteraction.Ignore);
            if (count == obstacles.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = obstacles[i].collider;
                if (obstacle == null || obstacle.transform.IsChildOf(transform)
                    || occupants != null && occupants.ContainsPassenger(obstacle.transform)) continue;
                return false;
            }
            return true;
        }

        private void OnDisable() => Stop();

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            DrawPath(roadPoints, new Color(.15f, .7f, 1));
            DrawPath(parkingPoints, Color.yellow);
            DrawPath(stationDeparturePoints, new Color(.35f, 1f, .4f));
        }

        private static void DrawPath(Vector3[] points, Color color)
        {
            if (points == null) return;
            Gizmos.color = color;
            for (int i = 0; i < points.Length; i++)
            {
                Gizmos.DrawSphere(points[i], .15f);
                if (i > 0) Gizmos.DrawLine(points[i - 1], points[i]);
            }
        }
#endif
    }
}
