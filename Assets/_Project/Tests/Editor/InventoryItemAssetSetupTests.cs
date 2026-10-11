using System.Reflection;
using System.Linq;
using NUnit.Framework;
using SaksiTerakhir.Editor;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class InventoryItemAssetSetupTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [TestCase(100f, 100f, 100f, 0.065f)]
        [TestCase(-100f, 50f, 100f, 0.0325f)]
        public void ImportedScale_ModelWithTinyLocalBounds_ProducesColliderInRealWorldUnits(
            float scaleX, float scaleY, float scaleZ, float expectedHeight)
        {
            root = new GameObject("Imported battery root");
            root.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localScale = new Vector3(0.00018f, 0.00065f, 0.00018f);
            mesh.GetComponent<Collider>().enabled = false;
            MethodInfo boundsMethod = typeof(InventoryItemAssetSetup).GetMethod("TryGetLocalBounds",
                BindingFlags.NonPublic | BindingFlags.Static);
            object[] arguments = { root.transform, default(Bounds) };

            bool hasBounds = (bool)boundsMethod.Invoke(null, arguments);

            Assert.That(hasBounds, Is.True, "FBX local units must not reject a physically sized battery.");
            MethodInfo colliderMethod = typeof(InventoryItemAssetSetup).GetMethod("ConfigureCollider",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(colliderMethod, Is.Not.Null);
            BoxCollider shape = (BoxCollider)colliderMethod.Invoke(null, new[] { (object)root, arguments[1] });
            Physics.SyncTransforms();
            Assert.That(shape.bounds.size.x, Is.EqualTo(0.025f).Within(0.00001f));
            Assert.That(shape.bounds.size.y, Is.EqualTo(expectedHeight).Within(0.00001f));
            Assert.That(shape.bounds.size.z, Is.EqualTo(0.025f).Within(0.00001f));
            Assert.That(root.GetComponentsInChildren<Collider>().Count(c => c.enabled), Is.EqualTo(1));
            Assert.That(shape.enabled, Is.True);
            Assert.That(shape.isTrigger, Is.False);
        }
    }
}
