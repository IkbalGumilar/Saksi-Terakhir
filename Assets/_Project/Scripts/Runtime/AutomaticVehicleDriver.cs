using System;
using System.Collections.Generic;
using SaksiTerakhir.Npc;
using UnityEngine;

namespace SaksiTerakhir.Vehicles
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class AutomaticVehicleDriver : MonoBehaviour
    {
        private static readonly List<AutomaticVehicleDriver> activeDrivers = new List<AutomaticVehicleDriver>();
        public static IReadOnlyList<AutomaticVehicleDriver> ActiveDrivers => activeDrivers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveDrivers() => activeDrivers.Clear();

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
        private readonly Collider[] nearbyPedestrians = new Collider[24];
        private float distance;
        private float speed;
        public bool IsMoving { get; private set; }
        public bool IsParkingRoute { get; private set; }
        public bool IsStationDepartureRoute { get; private set; }
        public bool IsBlocked { get; private set; }
        public float Distance => distance;
        public float Speed => speed;
        public float RemainingDistance => path != null ? Mathf.Max(0f, path.Length - distance) : 0f;
        public float StoppingDistance => speed * speed / (2f * Mathf.Max(.1f, braking));
        public event Action Arrived;

        private void OnEnable()
        {
            if (!activeDrivers.Contains(this)) activeDrivers.Add(this);
        }

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
            bool yieldingToPedestrian = DetectAndAlertPedestriansAhead(out float pedestrianDistance);
            if (yieldingToPedestrian && pedestrianDistance <= Mathf.Max(2.6f, StoppingDistance + 1.8f))
                targetSpeed = 0f;
            float decel = yieldingToPedestrian ? braking * 1.5f : braking;
            speed = Mathf.MoveTowards(speed, targetSpeed, (targetSpeed < speed ? decel : acceleration) * deltaTime);
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

        private bool DetectAndAlertPedestriansAhead(out float nearestPedestrianDistance)
        {
            nearestPedestrianDistance = float.PositiveInfinity;
            if (obstacleLayers.value == 0 || path == null || body == null) return false;
            Vector3 current = body.position;
            Quaternion rotation = body.rotation;
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
            else forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float lookAhead = Mathf.Clamp(speed * 1.35f + 4.2f, 4.2f, 8.5f);
            Vector3 alertHalfSize = new Vector3(probeHalfSize.x + 0.9f, probeHalfSize.y, probeHalfSize.z);
            int count = Physics.BoxCastNonAlloc(current + rotation * probeCenter, alertHalfSize,
                forward, obstacles, rotation, lookAhead, obstacleLayers, QueryTriggerInteraction.Ignore);
            bool foundInLane = false;
            for (int i = 0; i < count; i++)
            {
                Collider col = obstacles[i].collider;
                if (col == null || col.transform.IsChildOf(transform)
                    || occupants != null && occupants.ContainsPassenger(col.transform)) continue;
                OfficeNpcAgent npc = col.GetComponentInParent<OfficeNpcAgent>();
                if (npc == null) continue;
                npc.NotifyApproachingVehicle(current, forward, probeHalfSize.x + 1.25f);
                Vector3 rel = npc.transform.position - current;
                rel.y = 0f;
                float along = Vector3.Dot(rel, forward);
                float lateral = Mathf.Abs(Vector3.Dot(rel, right));
                bool onZebraCross = OfficeNpcAgent.IsOnZebraCrossOrRoadway(npc.transform.position);
                if (along >= -probeHalfSize.z && (lateral <= probeHalfSize.x + 0.55f
                    || onZebraCross && lateral <= probeHalfSize.x + 1.15f))
                {
                    foundInLane = true;
                    nearestPedestrianDistance = Mathf.Min(nearestPedestrianDistance, Mathf.Max(0f, along - probeHalfSize.z));
                }
            }

            int overlapCount = Physics.OverlapBoxNonAlloc(current + rotation * probeCenter + forward * 1.6f,
                new Vector3(probeHalfSize.x + 1.1f, probeHalfSize.y + 0.4f, probeHalfSize.z + 1.8f),
                nearbyPedestrians, rotation, obstacleLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = nearbyPedestrians[i];
                if (col == null || col.transform.IsChildOf(transform)
                    || occupants != null && occupants.ContainsPassenger(col.transform)) continue;
                OfficeNpcAgent npc = col.GetComponentInParent<OfficeNpcAgent>();
                if (npc == null) continue;
                npc.NotifyApproachingVehicle(current, forward, probeHalfSize.x + 1.25f);
                Vector3 rel = npc.transform.position - current;
                rel.y = 0f;
                float along = Vector3.Dot(rel, forward);
                float lateral = Mathf.Abs(Vector3.Dot(rel, right));
                if (along >= -0.5f && lateral <= probeHalfSize.x + 0.65f)
                {
                    foundInLane = true;
                    nearestPedestrianDistance = Mathf.Min(nearestPedestrianDistance, Mathf.Max(0f, along - probeHalfSize.z));
                }
            }
            return foundInLane;
        }

        private bool CanMoveTo(Vector3 next, Quaternion rotation)
        {
            Vector3 current = body.position;
            Vector3 delta = next - current;
            if (delta.sqrMagnitude < .000001f) return true;
            float castLength = delta.magnitude + .55f;
            Vector3 forward = delta.normalized;
            int count = Physics.BoxCastNonAlloc(current + rotation * probeCenter, probeHalfSize,
                forward, obstacles, rotation, castLength, obstacleLayers, QueryTriggerInteraction.Ignore);
            if (count == obstacles.Length) return false;
            bool blocked = false;
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = obstacles[i].collider;
                if (obstacle == null || obstacle.transform.IsChildOf(transform)
                    || occupants != null && occupants.ContainsPassenger(obstacle.transform)) continue;
                OfficeNpcAgent npc = obstacle.GetComponentInParent<OfficeNpcAgent>();
                if (npc != null) npc.NotifyApproachingVehicle(current, forward, probeHalfSize.x + 1.35f);
                blocked = true;
            }
            return !blocked;
        }

        private void OnDisable()
        {
            activeDrivers.Remove(this);
            Stop();
        }

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
