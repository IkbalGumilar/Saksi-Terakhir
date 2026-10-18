using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SaksiTerakhir.Environment;
using UnityEngine;
using UnityEngine.TestTools;

namespace SaksiTerakhir.Tests
{
    public sealed class ElevatorButtonPlayModeTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [UnityTest]
        public IEnumerator Awake_AssignsEveryButtonColliderToInteractableLayer()
        {
            var buttonObject = new GameObject("Elevator Button Play Test");
            createdObjects.Add(buttonObject);
            BoxCollider rootCollider = buttonObject.AddComponent<BoxCollider>();

            var visualObject = new GameObject("Button Mesh Collider");
            visualObject.transform.SetParent(buttonObject.transform, false);
            BoxCollider childCollider = visualObject.AddComponent<BoxCollider>();

            buttonObject.AddComponent<ElevatorButton>();
            yield return null;

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            Assert.That(interactableLayer, Is.GreaterThanOrEqualTo(0));
            Assert.That(rootCollider.gameObject.layer, Is.EqualTo(interactableLayer));
            Assert.That(childCollider.gameObject.layer, Is.EqualTo(interactableLayer));
        }

        [UnityTest]
        public IEnumerator Interact_CallButtonWithoutSerializedReferenceFindsSceneController()
        {
            var elevatorObject = new GameObject("Office Elevator Play Test");
            createdObjects.Add(elevatorObject);
            OfficeElevator elevator = elevatorObject.AddComponent<OfficeElevator>();

            var cabinObject = new GameObject("Kabin_Lift_PROTOTIPE");
            createdObjects.Add(cabinObject);
            elevator.Configure(cabinObject.transform, null, null, new List<ElevatorFloor>
            {
                new ElevatorFloor { name = "Lantai 1", height = 0f },
                new ElevatorFloor { name = "Lantai 2", height = 4f }
            });

            var buttonObject = new GameObject("Unlinked Floor 2 Call Button");
            createdObjects.Add(buttonObject);
            buttonObject.AddComponent<BoxCollider>();
            ElevatorButton button = buttonObject.AddComponent<ElevatorButton>();
            button.Configure(null, ElevatorButtonType.Call, 1);
            yield return null;

            Assert.That(Object.FindFirstObjectByType<OfficeElevator>(), Is.SameAs(elevator));
            Assert.That(button.CanInteract(buttonObject.transform), Is.True,
                "An unassigned call button should resolve the active scene elevator.");

            button.Interact(buttonObject.transform);

            Assert.That(elevator.TargetFloor, Is.EqualTo(1));
            Assert.That(elevator.State, Is.EqualTo(ElevatorState.Moving));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.Destroy(createdObject);
                }
            }

            createdObjects.Clear();
            yield return null;
        }
    }
}
