using NUnit.Framework;
using SaksiTerakhir.Vehicles;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public class MotorcycleParkingSupportTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetParked_TogglesStandAndParkingColliders()
        {
            root = new GameObject("Motor Test");
            var stand = new GameObject("Standar Tengah");
            stand.transform.SetParent(root.transform, false);
            var firstSupport = new GameObject("Collider Parkir Roda");
            firstSupport.transform.SetParent(root.transform, false);
            var secondSupport = new GameObject("Collider Parkir Standar");
            secondSupport.transform.SetParent(root.transform, false);
            var colliders = new Collider[]
            {
                firstSupport.AddComponent<BoxCollider>(),
                secondSupport.AddComponent<BoxCollider>()
            };
            var parking = root.AddComponent<MotorcycleParkingSupport>();

            parking.Configure(stand, colliders, true);

            Assert.That(parking.IsParked, Is.True);
            Assert.That(stand.activeSelf, Is.True);
            Assert.That(colliders[0].enabled, Is.True);
            Assert.That(colliders[1].enabled, Is.True);

            parking.SetParked(false);

            Assert.That(parking.IsParked, Is.False);
            Assert.That(stand.activeSelf, Is.False);
            Assert.That(colliders[0].enabled, Is.False);
            Assert.That(colliders[1].enabled, Is.False);

            parking.SetParked(true);

            Assert.That(parking.IsParked, Is.True);
            Assert.That(stand.activeSelf, Is.True);
            Assert.That(colliders[0].enabled, Is.True);
            Assert.That(colliders[1].enabled, Is.True);
        }
    }
}
