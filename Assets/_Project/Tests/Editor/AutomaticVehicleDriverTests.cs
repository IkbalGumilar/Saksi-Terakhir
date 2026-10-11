using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Vehicles;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class AutomaticVehicleDriverTests
    {
        private static readonly Vector3 Origin = new Vector3(10000f, 0f, 10000f);
        private readonly List<GameObject> objects = new List<GameObject>();
        private AutomaticVehicleDriver driver;
        private GameObject car;
        private Rigidbody body;
        private int arrivals;

        [SetUp]
        public void SetUp()
        {
            arrivals = 0;
            car = Create("Automatic vehicle isolated test");
            car.transform.position = Origin;
            car.layer = 30;
            body = car.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var cabin = car.AddComponent<BoxCollider>();
            cabin.center = Vector3.up * .85f;
            cabin.size = new Vector3(1.8f, 1.2f, 4.4f);
            driver = car.AddComponent<AutomaticVehicleDriver>();
            Set("roadPoints", new[] { Origin, Origin + Vector3.forward * 12f });
            Set("roadSpeeds", new[] { 4f, 0f });
            Set("parkingPoints", new[] { Origin, Origin + Vector3.right * 6f });
            Set("parkingSpeeds", new[] { 2f, 0f });
            Set("obstacleLayers", (LayerMask)(1 << 30));
            driver.Arrived += () => arrivals++;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject item in objects)
                if (item != null) Object.DestroyImmediate(item);
            objects.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void ObstacleStopsProgressAndRemovingItResumesTheSameRoute()
        {
            GameObject obstacle = Create("Temporary pedestrian obstacle");
            obstacle.layer = 30;
            obstacle.transform.position = Origin + Vector3.forward * 3.5f + Vector3.up * .85f;
            obstacle.AddComponent<BoxCollider>().size = new Vector3(.6f, 1.8f, .6f);
            driver.Begin(false);
            Physics.SyncTransforms();

            driver.Advance(1f);

            Assert.That(driver.IsBlocked, Is.True);
            Assert.That(driver.IsMoving, Is.True, "An obstacle must not cancel the journey.");
            Assert.That(driver.Distance, Is.Zero);
            Assert.That(driver.Speed, Is.Zero);
            Assert.That(Vector3.Distance(body.position, Origin), Is.LessThan(.001f));
            Assert.That(arrivals, Is.Zero);

            Object.DestroyImmediate(obstacle);
            Physics.SyncTransforms();
            driver.Advance(.5f);

            Assert.That(driver.IsBlocked, Is.False);
            Assert.That(driver.Distance, Is.GreaterThan(0f));
            Assert.That(driver.IsMoving, Is.True);
            Assert.That(arrivals, Is.Zero);
        }

        [Test]
        public void PausedOrInvalidDeltaCannotMoveTheVehicleOrAdvanceArrival()
        {
            driver.Begin(false);
            driver.Advance(.5f);
            float distance = driver.Distance;
            float speed = driver.Speed;
            Vector3 position = body.position;
            Quaternion rotation = body.rotation;

            foreach (float delta in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                driver.Advance(delta);

            Assert.That(driver.Distance, Is.EqualTo(distance));
            Assert.That(driver.Speed, Is.EqualTo(speed));
            Assert.That(body.position, Is.EqualTo(position));
            Assert.That(body.rotation, Is.EqualTo(rotation));
            Assert.That(arrivals, Is.Zero);
        }

        [Test]
        public void LargeFrameEndsExactlyAtDestinationAndSignalsArrivalOnce()
        {
            driver.Begin(false);
            driver.Advance(100f);

            Vector3 destination = Origin + Vector3.forward * 12f;
            Assert.That(driver.Distance, Is.EqualTo(12f).Within(.001f));
            Assert.That(Vector3.Distance(body.position, destination), Is.LessThan(.001f));
            Assert.That(driver.IsMoving, Is.False);
            Assert.That(driver.Speed, Is.Zero);
            Assert.That(arrivals, Is.EqualTo(1));
            driver.Advance(100f);
            Assert.That(arrivals, Is.EqualTo(1));
            Assert.That(Vector3.Distance(body.position, destination), Is.LessThan(.001f));
        }

        [Test]
        public void ResumeUsesSavedDistanceAndParkingHasItsOwnRoute()
        {
            driver.Begin(false, 7f);
            Assert.That(driver.Distance, Is.EqualTo(7f));
            Assert.That(Vector3.Distance(body.position, Origin + Vector3.forward * 7f), Is.LessThan(.001f));
            Assert.That(arrivals, Is.Zero, "Restoring a pose must not replay arrival callbacks.");
            driver.Begin(true, 3f);
            Assert.That(driver.IsParkingRoute, Is.True);
            Assert.That(driver.Distance, Is.EqualTo(3f));
            Assert.That(Vector3.Distance(body.position, Origin + Vector3.right * 3f), Is.LessThan(.001f));
            Assert.That(arrivals, Is.Zero);
        }

        [Test]
        public void FinalShortStepSnapsThePhysicalPoseToTheDestination()
        {
            driver.Begin(false, 11.96f);
            driver.Advance(.1f);
            Assert.That(driver.IsMoving, Is.False);
            Assert.That(driver.Distance, Is.EqualTo(12f).Within(.001f));
            Assert.That(Vector3.Distance(body.position, Origin + Vector3.forward * 12f), Is.LessThan(.001f),
                "Arrival cannot leave the body at the previous near-destination pose.");
            Assert.That(arrivals, Is.EqualTo(1));
        }

        [Test]
        public void ItsOwnCabinColliderDoesNotPreventMotion()
        {
            driver.Begin(false);
            Physics.SyncTransforms();
            driver.Advance(.5f);
            Assert.That(driver.Distance, Is.GreaterThan(0f));
            Assert.That(driver.IsBlocked, Is.False);
        }

        [Test]
        public void DisablingDriverStopsWithoutReportingArrival()
        {
            driver.Begin(false);
            driver.Advance(.5f);
            float distance = driver.Distance;
            driver.enabled = false;
            // Edit Mode does not run ordinary MonoBehaviour lifecycle callbacks.
            typeof(AutomaticVehicleDriver).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(driver, null);
            driver.Advance(1f);
            Assert.That(driver.IsMoving, Is.False);
            Assert.That(driver.Speed, Is.Zero);
            Assert.That(driver.Distance, Is.EqualTo(distance));
            Assert.That(arrivals, Is.Zero);
        }

        private GameObject Create(string name)
        {
            var item = new GameObject(name);
            objects.Add(item);
            return item;
        }

        private void Set(string field, object value) => typeof(AutomaticVehicleDriver)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(driver, value);
    }
}
