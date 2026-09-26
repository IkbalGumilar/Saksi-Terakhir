using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Interaction;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public class PlayerInteractorProbeRadiusTests
    {
        private static readonly Vector3 TestOrigin = new Vector3(10000f, 1000f, 10000f);
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject createdObject in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [TestCase(2f)]
        [TestCase(4f)]
        [TestCase(20f)]
        public void ProbeRadius_MaintainsTheSameViewportSize(float distance)
        {
            const float fieldOfView = 60f;
            const float viewportHeightFraction = 0.0125f;
            const float maximumDistance = 30f;
            float actualRadius = CalculateProbeRadius(distance, fieldOfView,
                viewportHeightFraction, maximumDistance);
            float apparentViewportFraction = actualRadius
                / (distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad));

            Assert.That(apparentViewportFraction,
                Is.EqualTo(viewportHeightFraction).Within(0.0001f));
        }

        [Test]
        public void ProbeRadius_IsCappedAtTheMaximumAimDistance()
        {
            const float fieldOfView = 60f;
            const float viewportHeightFraction = 0.0125f;
            const float maximumDistance = 30f;
            float actualRadius = CalculateProbeRadius(50f, fieldOfView,
                viewportHeightFraction, maximumDistance);
            float expectedRadius = maximumDistance
                * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad)
                * viewportHeightFraction;

            Assert.That(actualRadius, Is.EqualTo(expectedRadius).Within(0.0001f));
        }

        [Test]
        public void ProbeRadius_ReturnsZeroForInvalidInputs()
        {
            Assert.That(CalculateProbeRadius(-1f, 60f, 0.0125f, 30f), Is.Zero);
            Assert.That(CalculateProbeRadius(2f, 180f, 0.0125f, 30f), Is.Zero);
            Assert.That(CalculateProbeRadius(2f, 60f, 0f, 30f), Is.Zero);
            Assert.That(CalculateProbeRadius(2f, 60f, 0.0125f, 0f), Is.Zero);
        }

        [Test]
        public void Probe_DoesNotDetectTargetsPastMaximumAimDistance()
        {
            PlayerInteractor interactor = CreateInteractor();
            GameObject target = CreateBox("Distant Interactable", Vector3.forward * 31f,
                Vector3.one);
            target.layer = LayerMask.NameToLayer("Interactable");
            target.AddComponent<ProbeTestInteractable>();
            Physics.SyncTransforms();

            Interactable detected = InvokeProbe(interactor);

            Assert.That(detected, Is.Null);
            Assert.That(interactor.ProbeRadius, Is.Zero);
        }

        [Test]
        public void AimSphere_TurnsYellowOnDoorContactWithinTwoMeters_WithoutTintingDoor()
        {
            PlayerInteractor interactor = CreateInteractor();
            GameObject sphere = CreateVisibleSphere(interactor);
            GameObject door = CreateDoor(1.5f);
            Physics.SyncTransforms();

            InvokePrivate(interactor, "Update");

            Assert.That(interactor.Current, Is.SameAs(door.GetComponent<DoorInteractable>()));
            Assert.That(GetBaseColor(sphere.GetComponent<Renderer>()), Is.EqualTo(Color.yellow));
            Assert.That(GetBaseColor(door.GetComponent<Renderer>()), Is.EqualTo(Color.cyan));
        }

        [Test]
        public void AimSphere_RestoresItsOwnColorWhenDoorLeavesRange_WithoutTintingDoor()
        {
            PlayerInteractor interactor = CreateInteractor();
            GameObject sphere = CreateVisibleSphere(interactor);
            GameObject door = CreateDoor(1.5f);
            Physics.SyncTransforms();
            InvokePrivate(interactor, "Update");

            door.transform.position = TestOrigin + Vector3.forward * 5f;
            Physics.SyncTransforms();
            InvokePrivate(interactor, "Update");

            Assert.That(interactor.Current, Is.Null);
            Assert.That(GetBaseColor(sphere.GetComponent<Renderer>()), Is.EqualTo(Color.cyan));
            Assert.That(GetBaseColor(door.GetComponent<Renderer>()), Is.EqualTo(Color.cyan));
        }

        [Test]
        public void AimSphere_DoesNotTintAimedDoorOutsideInteractionRange()
        {
            PlayerInteractor interactor = CreateInteractor();
            GameObject sphere = CreateVisibleSphere(interactor);
            GameObject door = CreateDoor(5f);
            Physics.SyncTransforms();

            InvokePrivate(interactor, "Update");

            Assert.That(interactor.Current, Is.Null);
            Assert.That(GetBaseColor(sphere.GetComponent<Renderer>()), Is.EqualTo(Color.cyan));
            Assert.That(GetBaseColor(door.GetComponent<Renderer>()), Is.EqualTo(Color.cyan));
        }

        [Test]
        public void AimSphere_StaysUnchangedWhenDoorDoesNotTouchTheProbe()
        {
            PlayerInteractor interactor = CreateInteractor();
            GameObject sphere = CreateVisibleSphere(interactor);
            GameObject door = CreateDoor(1.5f);
            door.transform.position = TestOrigin + new Vector3(3f, 0f, 1.5f);
            CreateBox("Ray Hit Surface", Vector3.forward * 1.5f, Vector3.one * 0.02f);
            Physics.SyncTransforms();

            InvokePrivate(interactor, "Update");

            Assert.That(interactor.Current, Is.Null);
            Assert.That(GetBaseColor(sphere.GetComponent<Renderer>()), Is.EqualTo(Color.cyan));
            Assert.That(GetBaseColor(door.GetComponent<Renderer>()), Is.EqualTo(Color.cyan));
        }

        [TestCase(1f, false)]
        [TestCase(2f, true)]
        public void Probe_SelectsObjectsUsingSphereRadiusAtTheHitDistance(float distance,
            bool shouldSelect)
        {
            PlayerInteractor interactor = CreateInteractor();
            CreateBox("Ray Hit Surface", Vector3.forward * distance, new Vector3(0.02f, 0.02f, 0.02f));
            GameObject target = CreateBox("Offset Interactable",
                new Vector3(0.03f, 0f, distance - 0.005f), new Vector3(0.04f, 0.04f, 0.04f));
            target.layer = LayerMask.NameToLayer("Interactable");
            ProbeTestInteractable interactable = target.AddComponent<ProbeTestInteractable>();
            Physics.SyncTransforms();

            Interactable detected = InvokeProbe(interactor);

            Assert.That(detected == interactable, Is.EqualTo(shouldSelect));
        }

        [Test]
        public void Probe_DoesNotSelectAnOverlappingTargetOutsideInteractionRange()
        {
            PlayerInteractor interactor = CreateInteractor();
            CreateBox("Ray Hit Surface", new Vector3(0f, 0f, 2.019f),
                new Vector3(0.02f, 0.02f, 0.04f));
            GameObject target = CreateBox("Out Of Range Interactable",
                new Vector3(0.01f, 0f, 2.015f), new Vector3(0.01f, 0.02f, 0.02f));
            target.layer = LayerMask.NameToLayer("Interactable");
            target.AddComponent<ProbeTestInteractable>();
            Physics.SyncTransforms();

            Interactable detected = InvokeProbe(interactor);

            Assert.That(detected, Is.Null);
        }

        private static float CalculateProbeRadius(float distance, float fieldOfView,
            float viewportHeightFraction, float maximumDistance)
        {
            Assembly runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(assembly => assembly.GetName().Name == "SaksiTerakhir.Runtime");
            Assert.That(runtimeAssembly, Is.Not.Null,
                "The SaksiTerakhir runtime assembly must be loaded for the probe test.");

            Type interactorType = runtimeAssembly.GetType(
                "SaksiTerakhir.Interaction.PlayerInteractor");
            Assert.That(interactorType, Is.Not.Null,
                "The player interaction system must exist in the runtime assembly.");

            MethodInfo calculator = interactorType.GetMethod("CalculateProbeRadius",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(calculator, Is.Not.Null,
                "PlayerInteractor must expose the probe-radius calculation used by detection.");

            return (float)calculator.Invoke(null,
                new object[] { distance, fieldOfView, viewportHeightFraction, maximumDistance });
        }

        private PlayerInteractor CreateInteractor()
        {
            var player = new GameObject("Probe Test Player");
            player.SetActive(false);
            player.transform.position = TestOrigin;
            createdObjects.Add(player);
            PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();

            var viewpoint = new GameObject("Probe Test Viewpoint");
            viewpoint.transform.position = TestOrigin;
            createdObjects.Add(viewpoint);
            SetPrivateField(interactor, "viewpoint", viewpoint.transform);

            return interactor;
        }

        private GameObject CreateVisibleSphere(PlayerInteractor interactor)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            createdObjects.Add(sphere);
            sphere.GetComponent<Collider>().enabled = false;
            SetBaseColor(sphere.GetComponent<Renderer>(), Color.cyan);
            SetPrivateField(interactor, "debugTransform", sphere.transform);
            return sphere;
        }

        private GameObject CreateDoor(float distance)
        {
            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(door);
            door.transform.position = TestOrigin + Vector3.forward * distance;
            door.layer = LayerMask.NameToLayer("Interactable");
            SetBaseColor(door.GetComponent<Renderer>(), Color.cyan);
            door.AddComponent<DoorInteractable>();
            return door;
        }

        private static void SetBaseColor(Renderer renderer, Color color)
        {
            var propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetColor(Shader.PropertyToID("_BaseColor"), color);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private static Color GetBaseColor(Renderer renderer)
        {
            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            return propertyBlock.GetColor(Shader.PropertyToID("_BaseColor"));
        }

        private GameObject CreateBox(string name, Vector3 position, Vector3 size)
        {
            var box = new GameObject(name);
            box.transform.position = TestOrigin + position;
            BoxCollider collider = box.AddComponent<BoxCollider>();
            collider.size = size;
            createdObjects.Add(box);
            return box;
        }

        private static Interactable InvokeProbe(PlayerInteractor interactor)
        {
            MethodInfo probe = typeof(PlayerInteractor).GetMethod("Probe",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(probe, Is.Not.Null);
            return (Interactable)probe.Invoke(interactor, null);
        }

        private static void SetPrivateField<T>(PlayerInteractor interactor, string fieldName,
            T value)
        {
            FieldInfo field = typeof(PlayerInteractor).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(interactor, value);
        }

        private static void InvokePrivate(PlayerInteractor interactor, string methodName,
            params object[] arguments)
        {
            MethodInfo method = typeof(PlayerInteractor).GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Expected method {methodName}.");
            method.Invoke(interactor, arguments);
        }
    }

    public sealed class ProbeTestInteractable : Interactable
    {
        public override void Interact(Transform actor)
        {
        }
    }
}
