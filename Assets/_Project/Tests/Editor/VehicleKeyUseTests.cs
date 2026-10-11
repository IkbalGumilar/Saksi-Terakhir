using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Vehicles;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class VehicleKeyUseTests
    {
        private GameObject actor, car;
        private ItemDefinition keyDefinition;
        private InventoryPanelController inventory;
        private InventoryEntry key;
        private ItemKeyLock keyLock;
        private VehicleKeyUse vehicle;
        private Light indicator;

        [SetUp]
        public void SetUp()
        {
            actor = new GameObject("Vehicle key test actor");
            car = new GameObject("Vehicle key test car");
            keyDefinition = ScriptableObject.CreateInstance<ItemDefinition>();
            Set(keyDefinition, "id", CarKeyInventory.CarKeyId);
            inventory = actor.AddComponent<InventoryPanelController>();
            inventory.RestoreState(new InventoryState(2, 1));
            key = keyDefinition.CreateEntry();
            Assert.That(inventory.TryAdd(key), Is.True);
            keyLock = car.AddComponent<ItemKeyLock>();
            Set(keyLock, "requiredKey", keyDefinition);
            vehicle = car.AddComponent<VehicleKeyUse>();
            Set(vehicle, "keyLock", keyLock);
            indicator = car.AddComponent<Light>();
            indicator.enabled = false;
            var feedback = car.AddComponent<VehicleSignalFeedback>();
            Set(feedback, "indicatorLights", new[] { indicator });
            Set(vehicle, "signalFeedback", feedback);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(car);
            Object.DestroyImmediate(keyDefinition);
        }

        [Test]
        public void CatalogResolvesTheSameCarKeyRewardedByReceptionist()
        {
            ItemDefinitionCatalog catalog = ItemDefinitionCatalog.LoadDefault();
            Assert.That(catalog, Is.Not.Null);
            ItemDefinition definition = catalog.Find(CarKeyInventory.CarKeyId);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Type, Is.EqualTo(InventoryEntryType.Item));
            Assert.That(definition.ModelPrefab, Is.Not.Null);
        }

        [Test]
        public void RemoteWorksAtExactlyFiveMetresWithoutUnlockingOrConsumingKey()
        {
            actor.transform.position = new Vector3(5f, 0f, 0f);
            Assert.That(vehicle.TryUseRemote(key, actor.transform), Is.True);
            Assert.That(indicator.enabled, Is.True);
            Assert.That(keyLock.IsLocked, Is.True);
            Assert.That(inventory.HasItem(key.Id), Is.True);
        }

        [Test]
        public void RemoteOutsideFiveMetresDoesNothing()
        {
            actor.transform.position = new Vector3(5.01f, 0f, 0f);
            Assert.That(vehicle.TryUseRemote(key, actor.transform), Is.False);
            Assert.That(indicator.enabled, Is.False);
            Assert.That(keyLock.IsLocked, Is.True);
        }

        [Test]
        public void RemoteRejectsAnItemNotOwnedByActor()
        {
            inventory.Remove(key);
            Assert.That(vehicle.TryUseRemote(key, actor.transform), Is.False);
            Assert.That(keyLock.IsLocked, Is.True);
        }

        [Test]
        public void CorrectKeyUnlocksAndCanStillSignalVehicle()
        {
            Assert.That(keyLock.TryUnlock(key), Is.True);
            Assert.That(keyLock.IsLocked, Is.False);
            Assert.That(vehicle.TryUseRemote(key, actor.transform), Is.True);
            Assert.That(inventory.HasItem(key.Id), Is.True);
        }

        [Test]
        public void WrongKeyDoesNotUnlock()
        {
            Assert.That(keyLock.TryUnlock(new InventoryEntry("item.other_key", "Other", InventoryEntryType.Item)), Is.False);
            Assert.That(keyLock.IsLocked, Is.True);
        }

        [Test]
        public void LockedDoorCannotBeForcedOpenByAutomaticMovement()
        {
            var door = car.AddComponent<DoorInteractable>();
            door.ForceOpen(actor.transform);
            Assert.That(door.IsOpen, Is.False);
        }

        [Test]
        public void UsingOwnedKeyOpensDoorWithoutRemovingIt()
        {
            var door = car.AddComponent<DoorInteractable>();
            Assert.That(door.TryUseItem(key, actor.transform), Is.True);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(keyLock.IsLocked, Is.False);
            Assert.That(inventory.HasItem(key.Id), Is.True);
        }

        [Test]
        public void UnlockingFromOutsideDoorReachFailsEvenInsideRemoteRange()
        {
            actor.transform.position = new Vector3(4f, 0f, 0f);
            Assert.That(vehicle.TryUnlockAndBoard(key, actor.transform), Is.False);
            Assert.That(keyLock.IsLocked, Is.True);
            Assert.That(indicator.enabled, Is.False);
        }

        [Test]
        public void CompletedSharedMealSeatsPlayerInBackWithoutCompletingChapter()
        {
            CheckSeat(StoryQuestStatus.Completed, true);
        }

        [Test]
        public void SkippedSharedMealSeatsPlayerAsDriverWithoutCompletingChapter()
        {
            CheckSeat(StoryQuestStatus.Skipped, false);
        }

        [Test]
        public void DisablingSignalRestoresIndicatorToOff()
        {
            var signal = car.GetComponent<VehicleSignalFeedback>();
            signal.Flash();
            Assert.That(indicator.enabled, Is.True);
            typeof(VehicleSignalFeedback).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(signal, null);
            Assert.That(indicator.enabled, Is.False);
            Assert.That(signal.IsFlashing, Is.False);
        }

        private void CheckSeat(StoryQuestStatus meal, bool expectsRear)
        {
            actor.AddComponent<CharacterController>();
            var movement = actor.AddComponent<PlayerController>();
            var seating = actor.AddComponent<StoryPlayerSeating>();
            var chapter = car.AddComponent<ChapterOneDirector>();
            var progress = new ChapterOneProgress();
            Set(progress, "stage", ChapterOneStage.ReachVehicle);
            Set(progress.Story, "sharedMealQuest", meal);
            Set(chapter, "progress", progress);
            Set(vehicle, "chapter", chapter);
            var driver = new GameObject("Driver Seat").transform;
            driver.SetParent(car.transform);
            driver.position = new Vector3(0.4f, 0.3f, 0.5f);
            var rear = new GameObject("Rear Seat").transform;
            rear.SetParent(car.transform);
            rear.position = new Vector3(0.4f, 0.3f, -0.5f);
            Set(vehicle, "driverSeat", driver);
            Set(vehicle, "rearPlayerSeat", rear);
            Assert.That(keyLock.TryUnlock(key), Is.True);

            Assert.That(vehicle.TryBoard(actor.transform), Is.True);

            Assert.That(seating.CurrentSeat, Is.SameAs(expectsRear ? rear : driver));
            Assert.That(movement.StoryCinematicMovementLocked, Is.True);
            Assert.That(movement.enabled, Is.False);
            Assert.That(actor.GetComponent<CharacterController>().enabled, Is.False);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(progress.Stage, Is.Not.EqualTo(ChapterOneStage.Complete));
        }

        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
