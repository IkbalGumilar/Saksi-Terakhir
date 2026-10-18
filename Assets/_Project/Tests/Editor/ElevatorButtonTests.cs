using System.Collections.Generic;
using NUnit.Framework;
using SaksiTerakhir.Environment;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class ElevatorButtonTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void Interact_LinkedCallButtonRequestsItsConfiguredFloor()
        {
            var elevatorObject = new GameObject("Office Elevator Test");
            createdObjects.Add(elevatorObject);
            OfficeElevator elevator = elevatorObject.AddComponent<OfficeElevator>();

            var cabinObject = new GameObject("Kabin_Lift_PROTOTIPE");
            createdObjects.Add(cabinObject);
            elevator.Configure(cabinObject.transform, null, null, new List<ElevatorFloor>
            {
                new ElevatorFloor { name = "Lantai 1", height = 0f },
                new ElevatorFloor { name = "Lantai 2", height = 4f }
            });

            var buttonObject = new GameObject("Floor 2 Call Button");
            createdObjects.Add(buttonObject);
            buttonObject.AddComponent<BoxCollider>();
            ElevatorButton button = buttonObject.AddComponent<ElevatorButton>();
            button.Configure(elevator, ElevatorButtonType.Call, 1);

            button.Interact(buttonObject.transform);

            Assert.That(elevator.TargetFloor, Is.EqualTo(1));
            Assert.That(elevator.State, Is.EqualTo(ElevatorState.Moving));
        }
    }
}
