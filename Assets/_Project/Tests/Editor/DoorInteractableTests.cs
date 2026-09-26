using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Interaction;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public class DoorInteractableTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void CanInteract_UsesTheDoorLocalInteractionFlag(bool interact,
            bool expectedCanInteract)
        {
            DoorInteractable door = CreateDoor(interact);

            Assert.That(door.CanInteract(door.transform), Is.EqualTo(expectedCanInteract));
        }

        [Test]
        public void Interact_DoesNotOpenDoorWhenItsLocalFlagIsFalse()
        {
            DoorInteractable door = CreateDoor(false);

            door.Interact(door.transform);

            Assert.That(door.IsOpen, Is.False);
        }

        private DoorInteractable CreateDoor(bool interact)
        {
            var doorObject = new GameObject("Door Test Object");
            doorObject.SetActive(false);
            createdObjects.Add(doorObject);
            doorObject.AddComponent<BoxCollider>();
            DoorInteractable door = doorObject.AddComponent<DoorInteractable>();

            FieldInfo interactionFlag = typeof(DoorInteractable).GetField("interact",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(interactionFlag, Is.Not.Null,
                "Each door must have an object-local 'interact' flag.");
            interactionFlag.SetValue(door, interact);
            doorObject.SetActive(true);

            return door;
        }
    }
}
