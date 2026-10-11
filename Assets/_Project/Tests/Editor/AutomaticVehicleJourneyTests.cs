using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class AutomaticVehicleJourneyTests
    {
        private static object Path(Vector3[] points)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("SaksiTerakhir.Vehicles.VehicleRoutePath")).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "Automatic vehicle route must exist.");
            return Activator.CreateInstance(type, new object[] { points, new float[] { 4f, 8f, 0f } });
        }

        private static Vector3 Position(object route, float distance)
            => (Vector3)route.GetType().GetMethod("PositionAt").Invoke(route, new object[] { distance });

        [Test] public void RouteUsesLaneWaypointsAndNeverOvershootsFinalParkingPoint()
        {
            object route = Path(new[] { new Vector3(0, .36f, -60), new Vector3(20, .36f, -60), new Vector3(20, .36f, -30) });
            Assert.That(Position(route, 10), Is.EqualTo(new Vector3(10, .36f, -60)));
            Assert.That(Position(route, 100), Is.EqualTo(new Vector3(20, .36f, -30)));
            Assert.That(Position(route, -10), Is.EqualTo(new Vector3(0, .36f, -60)));
        }

        [Test] public void RouteDistanceAccountsForBothRoadSegments()
        {
            object route = Path(new[] { Vector3.zero, Vector3.right * 20, new Vector3(20, 0, 30) });
            Assert.That((float)route.GetType().GetProperty("Length").GetValue(route), Is.EqualTo(50).Within(.001f));
            Assert.That(Position(route, 35), Is.EqualTo(new Vector3(20, 0, 15)));
        }

        [Test] public void InvalidRouteCannotStartMoving()
        {
            Assert.Throws<TargetInvocationException>(() => Path(new[] { Vector3.zero, Vector3.zero, Vector3.forward }));
            Assert.Throws<TargetInvocationException>(() => Path(new[] { Vector3.zero, new Vector3(float.NaN, 0, 0), Vector3.forward }));
        }
    }
}
