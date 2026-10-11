using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Persistence;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using SaksiTerakhir.Vehicles;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SaksiTerakhir.Tests
{
    public sealed class VehicleJourneyPersistenceTests
    {
        private static readonly Vector3 Origin = new Vector3(14000f, 0f, 14000f);
        private readonly List<Object> assets = new List<Object>();
        private Scene scene;
        private GameObject car, player;
        private VehicleJourneyDirector journey;
        private AutomaticVehicleDriver driver;
        private VehicleKeyUse seats;
        private ChapterOneProgress progress;
        private StoryDialogueController dialogue;
        private InventoryPanelController inventory;
        private ItemDefinition nasi;
        private InventoryEntry filler;
        private Transform driverSeat;
        private int boardingEvents;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            car = Create("Journey persistence test vehicle");
            car.SetActive(false);
            car.transform.position = Origin;
            var chapter = car.AddComponent<ChapterOneDirector>();
            chapter.StartupManagedExternally = true;
            progress = new ChapterOneProgress();
            Set(progress, "stage", ChapterOneStage.ReachVehicle);
            Set(progress, "hasCarKey", true);
            Set(progress.Story, "sharedMealQuest", StoryQuestStatus.Skipped);
            Set(chapter, "progress", progress);
            Set(chapter, "initialized", true);

            player = Create("Journey persistence test MC");
            player.SetActive(false);
            player.transform.position = Origin;
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerController>();
            player.AddComponent<StoryPlayerSeating>();
            inventory = player.AddComponent<InventoryPanelController>();
            Set(inventory, "notifications", null);
            inventory.RestoreState(new InventoryState(1, 1));
            Set(chapter, "playerInventory", inventory);
            Set(chapter, "player", player.transform);

            dialogue = car.AddComponent<StoryDialogueController>();
            Set(chapter, "dialogue", dialogue);
            journey = car.AddComponent<VehicleJourneyDirector>();
            driver = car.GetComponent<AutomaticVehicleDriver>();
            seats = car.GetComponent<VehicleKeyUse>();
            Set(driver, "roadPoints", new[] { Origin, Origin + Vector3.forward * 30f });
            Set(driver, "roadSpeeds", new[] { 5f, 0f });
            Set(driver, "parkingPoints", new[] { Origin + Vector3.forward * 30f, Origin + new Vector3(6f, 0f, 30f) });
            Set(driver, "parkingSpeeds", new[] { 2f, 0f });
            Set(seats, "chapter", chapter);
            driverSeat = Anchor("Driver", new Vector3(-.4f, .2f, .2f));
            Set(seats, "driverSeat", driverSeat);
            Set(seats, "exitPoint", Anchor("Exit", new Vector3(-3f, .04f, .2f)));
            var keyLock = car.AddComponent<ItemKeyLock>();
            Set(keyLock, "requiredKey", Definition("item.fixture.car_key"));
            keyLock.RestoreSavedState(true);
            Set(seats, "keyLock", keyLock);
            Set(journey, "chapter", chapter);
            Set(journey, "dialogue", dialogue);
            Set(journey, "player", player.transform);
            Set(journey, "officePosition", Origin);
            Set(journey, "officeEuler", Vector3.zero);
            nasi = Definition("item.fixture.nasi_uduk");
            // A usable model ensures an accidental world-drop fallback would be observable.
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(model, scene);
            model.name = "Reward model fixture";
            model.transform.position = Origin + Vector3.right * 100f;
            Set(nasi, "modelPrefab", model);
            Set(journey, "nasiUduk", nasi);
            DialogueSequence departure = ScriptableObject.CreateInstance<DialogueSequence>();
            departure.Configure("fixture.departure", new[] { new DialogueLine("PLAYER", "Siap berangkat.") });
            assets.Add(departure);
            Set(journey, "departureMealSkipped", departure);
            Set(journey, "departureMealComplete", departure);
            Call(journey, "ResolveDependencies");
            seats.Boarded += () => boardingEvents++;
            Assert.That(seats.RestoreOccupants(player.transform), Is.True);
            Call(journey, "OnBoarded");
            filler = new InventoryEntry("item.fixture.filler", "Occupied slot", InventoryEntryType.Item);
            Assert.That(inventory.TryAdd(filler), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            dialogue?.Stop();
            if (journey != null) journey.PrepareRestore();
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.ClosePreviewScene(scene);
            foreach (Object asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [Test]
        public void FullBagKeepsDepartureParkedAndDoesNotDropOrRecordTheReward()
        {
            int before = GameWorldSnapshot.FindAll<ItemPickupInteractable>(scene).Count();
            int notices = 0;
            inventory.FeedbackRequested += message => { if (message == "inventory.full") notices++; };
            Call(journey, "PlayDeparture");
            Call(journey, "PlayDeparture");

            Assert.That(inventory.State.Items, Has.Count.EqualTo(1));
            Assert.That(inventory.State.Items[0], Is.SameAs(filler));
            Assert.That(progress.Story.NasiUdukReceived, Is.False);
            Assert.That(progress.Story.DepartureDriver, Is.EqualTo(ChapterOneDepartureDriver.Unassigned));
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(journey.Phase, Is.EqualTo(VehicleJourneyPhase.DepartureDialogue));
            Assert.That(journey.CanExit, Is.True, "The player must be able to free inventory space before leaving.");
            Assert.That(driver.IsMoving || dialogue.IsPlaying, Is.False);
            Assert.That(GameWorldSnapshot.FindAll<ItemPickupInteractable>(scene).Count(), Is.EqualTo(before));
            Assert.That(notices, Is.EqualTo(1), "Waiting for space must not repeat the same notice every update.");
        }

        [Test]
        public void FullBagCapacityUpgradeRetriesRewardWithoutDiscardingExistingItems()
        {
            Call(journey, "PlayDeparture");
            Assert.That(progress.Story.NasiUdukReceived, Is.False);
            Assert.That(inventory.TrySetGridSize(2, 1), Is.True);

            Call(journey, "PlayDeparture");
            Call(journey, "PlayDeparture");

            Assert.That(inventory.State.Items, Has.Count.EqualTo(2));
            Assert.That(inventory.State.Items, Does.Contain(filler));
            Assert.That(inventory.State.Items.Count(item => item.Id == nasi.Id), Is.EqualTo(1));
            Assert.That(progress.Story.NasiUdukReceived, Is.True);
            Assert.That(dialogue.IsPlaying, Is.True);
            Assert.That(journey.Phase, Is.EqualTo(VehicleJourneyPhase.DepartureDialogue));
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(driver.IsMoving, Is.False,
                "Upgrading a full bag retries the reward while departure dialogue still holds movement.");
            Assert.That(GameWorldSnapshot.FindAll<ItemPickupInteractable>(scene), Is.Empty);
        }

        [Test]
        public void SpaceFreedRetriesRewardOnceEvenAfterDepartureDialogueIsRestored()
        {
            Call(journey, "PlayDeparture");
            Assert.That(inventory.Remove(filler), Is.True);
            Call(journey, "PlayDeparture");
            Assert.That(progress.Story.NasiUdukReceived, Is.True);
            Assert.That(inventory.State.Items, Has.Count.EqualTo(1));
            InventoryEntry rewarded = inventory.State.Items[0];
            Assert.That(rewarded.Id, Is.EqualTo(nasi.Id));
            Assert.That(dialogue.IsPlaying, Is.True);
            Assert.That(driver.IsMoving, Is.False, "The departure conversation finishes before road movement begins.");

            SavedVehicleJourney checkpoint = journey.Capture();
            dialogue.Stop();
            journey.PrepareRestore();
            journey.Restore(checkpoint);
            Call(journey, "PlayDeparture");
            Assert.That(inventory.State.Items, Has.Count.EqualTo(1));
            Assert.That(inventory.State.Items[0].InstanceId, Is.EqualTo(rewarded.InstanceId));
            Call(dialogue, "Advance");
            Assert.That(journey.Phase, Is.EqualTo(VehicleJourneyPhase.OnRoad));
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.TravelToFuelStation));
            Assert.That(driver.IsMoving, Is.True);
            Assert.That(GameWorldSnapshot.FindAll<ItemPickupInteractable>(scene), Is.Empty);
        }

        [Test]
        public void WorldSaveJsonRoundTripPreservesVehiclePhaseDistanceSeatAndKeyState()
        {
            ResolveDeparture();
            var snapshot = ValidSnapshot();
            snapshot.vehicleJourney = new SavedVehicleJourney { version = 1,
                phase = VehicleJourneyPhase.OnRoad, routeDistance = 7.25f, playerSeated = true, keyUnlocked = true };
            Assert.That(snapshot.Validate(out string error), Is.True, error);

            GameSaveSnapshot loaded = JsonUtility.FromJson<GameSaveSnapshot>(JsonUtility.ToJson(snapshot));

            Assert.That(loaded.Validate(out error), Is.True, error);
            Assert.That(loaded.vehicleJourney.version, Is.EqualTo(1));
            Assert.That(loaded.vehicleJourney.phase, Is.EqualTo(VehicleJourneyPhase.OnRoad));
            Assert.That(loaded.vehicleJourney.routeDistance, Is.EqualTo(7.25f));
            Assert.That(loaded.vehicleJourney.playerSeated && loaded.vehicleJourney.keyUnlocked, Is.True);
            Assert.That(loaded.chapter.Stage, Is.EqualTo(ChapterOneStage.TravelToFuelStation));
        }

        [Test]
        public void SaveRejectsInvalidVehiclePhaseCursorVersionOrMissingTravelOccupant()
        {
            ResolveDeparture();
            foreach (float distance in new[] { float.NaN, float.PositiveInfinity, -1f, 10001f })
            {
                GameSaveSnapshot snapshot = ValidSnapshot();
                snapshot.vehicleJourney = RoadCheckpoint();
                snapshot.vehicleJourney.routeDistance = distance;
                Assert.That(snapshot.Validate(out _), Is.False);
            }
            foreach (int version in new[] { -1, 2, 999 })
            {
                SavedVehicleJourney saved = RoadCheckpoint();
                saved.version = version;
                Assert.That(saved.Validate(), Is.False);
            }
            SavedVehicleJourney unknownPhase = RoadCheckpoint();
            unknownPhase.phase = (VehicleJourneyPhase)999;
            Assert.That(unknownPhase.Validate(), Is.False);
            SavedVehicleJourney unoccupiedRoad = RoadCheckpoint();
            unoccupiedRoad.playerSeated = false;
            Assert.That(unoccupiedRoad.Validate(), Is.False);
            GameSaveSnapshot inconsistent = ValidSnapshot();
            inconsistent.vehicleJourney = RoadCheckpoint();
            inconsistent.vehicleJourney.phase = VehicleJourneyPhase.ParkedStation;
            inconsistent.vehicleJourney.playerSeated = false;
            Assert.That(inconsistent.vehicleJourney.Validate(), Is.True);
            Assert.That(inconsistent.vehicleJourney.MatchesChapter(progress), Is.False,
                "A parked-at-station checkpoint cannot belong to a chapter still traveling there.");
            Assert.That(inconsistent.Validate(out _), Is.False);
            Assert.That(new SavedVehicleJourney().Validate(), Is.True,
                "An absent vehicle record in old JSON remains a supported legacy default.");
        }

        [Test]
        public void RestoringRoadCheckpointPlacesVehicleAndOccupantWithoutReplayingBoarding()
        {
            ResolveDeparture();
            journey.PrepareRestore();
            journey.Restore(RoadCheckpoint());

            Assert.That(journey.Phase, Is.EqualTo(VehicleJourneyPhase.OnRoad));
            Assert.That(driver.Distance, Is.EqualTo(7.25f));
            Assert.That(Vector3.Distance(car.transform.position, Origin + Vector3.forward * 7.25f), Is.LessThan(.001f));
            Assert.That(seats.IsOccupied, Is.True);
            Assert.That(player.GetComponent<StoryPlayerSeating>().CurrentSeat, Is.SameAs(driverSeat));
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.False);
            Assert.That(seats.KeyLock.IsLocked, Is.False);
            Assert.That(boardingEvents, Is.Zero);
            seats.RefreshPassengers();
            Assert.That(Vector3.Distance(player.transform.position, driverSeat.position), Is.LessThan(.001f));
            SavedVehicleJourney captured = journey.Capture();
            Assert.That(captured.Validate(), Is.True);
            Assert.That(captured.phase, Is.EqualTo(VehicleJourneyPhase.OnRoad));
            Assert.That(captured.routeDistance, Is.EqualTo(7.25f));
            Assert.That(captured.playerSeated && captured.keyUnlocked, Is.True);
        }

        private void ResolveDeparture()
        {
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.JourneyStarted), Is.True);
        }

        private GameSaveSnapshot ValidSnapshot() => new GameSaveSnapshot
        {
            runId = Guid.NewGuid().ToString("D"), scenePath = "Assets/_Project/Scenes/Regional Archive Office.unity",
            savedUtc = "2026-10-11T00:00:00Z", chapter = progress,
            player = new SavedPose(player.transform.position, player.transform.rotation),
            inventory = SavedInventory.Capture(inventory.State)
        };

        private static SavedVehicleJourney RoadCheckpoint() => new SavedVehicleJourney
            { version = 1, phase = VehicleJourneyPhase.OnRoad, routeDistance = 7.25f, playerSeated = true, keyUnlocked = true };

        private GameObject Create(string name)
        {
            var item = new GameObject(name);
            SceneManager.MoveGameObjectToScene(item, scene);
            return item;
        }

        private Transform Anchor(string name, Vector3 position)
        {
            Transform anchor = Create(name).transform;
            anchor.SetParent(car.transform, false);
            anchor.localPosition = position;
            return anchor;
        }

        private ItemDefinition Definition(string id)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            Set(definition, "id", id);
            assets.Add(definition);
            return definition;
        }

        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static object Call(object target, string method, params object[] arguments) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, arguments);
    }
}
