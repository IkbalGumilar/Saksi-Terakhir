using System;
using UnityEngine;

namespace SaksiTerakhir.Vehicles
{
    // Distance along authored lane points is the source of truth, including on restore.
    public sealed class VehicleRoutePath
    {
        private readonly Vector3[] points;
        private readonly float[] speeds;
        private readonly float[] distances;
        public float Length => distances[distances.Length - 1];

        public VehicleRoutePath(Vector3[] positions, float[] speedLimits)
        {
            if (positions == null || positions.Length < 2 || speedLimits == null || speedLimits.Length != positions.Length)
                throw new ArgumentException("A vehicle route needs matching positions and speed limits.");
            points = (Vector3[])positions.Clone();
            speeds = (float[])speedLimits.Clone();
            distances = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                if (!Finite(points[i].x) || !Finite(points[i].y) || !Finite(points[i].z)
                    || !Finite(speeds[i]) || speeds[i] < 0)
                    throw new ArgumentException("Vehicle route contains an invalid point or speed.");
                if (i == 0) continue;
                float length = Vector3.Distance(points[i - 1], points[i]);
                if (length < .01f) throw new ArgumentException("Vehicle route has repeated adjacent points.");
                distances[i] = distances[i - 1] + length;
            }
        }

        public Vector3 PositionAt(float distance)
        {
            distance = Mathf.Clamp(distance, 0, Length);
            int segment = SegmentAt(distance);
            float t = (distance - distances[segment]) / (distances[segment + 1] - distances[segment]);
            return Vector3.Lerp(points[segment], points[segment + 1], t);
        }

        public Quaternion RotationAt(float distance)
        {
            Vector3 direction = PositionAt(Mathf.Min(Length, distance + .6f))
                - PositionAt(Mathf.Max(0, distance - .6f));
            direction.y = 0;
            return direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
        }

        public float SpeedLimitAt(float distance, float braking)
        {
            int segment = SegmentAt(Mathf.Clamp(distance, 0, Length));
            float limit = speeds[segment];
            for (int i = segment + 1; i < points.Length; i++)
            {
                float remaining = Mathf.Max(0, distances[i] - distance);
                limit = Mathf.Min(limit, Mathf.Sqrt(speeds[i] * speeds[i] + 2 * braking * remaining));
            }
            return limit;
        }

        private int SegmentAt(float distance)
        {
            int index = Array.BinarySearch(distances, distance);
            if (index < 0) index = ~index - 1;
            return Mathf.Clamp(index, 0, points.Length - 2);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
