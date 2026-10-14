using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Player;
using SaksiTerakhir.Persistence;
using SaksiTerakhir.Story;
using SaksiTerakhir.Vehicles;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SaksiTerakhir.Tests
{
    public sealed class VehicleStationDepartureTests
    {
        private static readonly Vector3 Origin = new Vector3(18000f, 0f, 18000f);
        private readonly List<Object> objects = new List<Object>();
        private GameObject car;
        private AutomaticVehicleDriver driver;
        private VehicleJourneyDirector journey;
        private VehicleKeyUse seats;
        private ChapterOneProgress progress;
        private Transform actor;

        [SetUp]
        public void SetUp()
        {
            car = Create("Station departure test vehicle");
            car.SetActive(false);
            car.transform.position = Origin;
            var chapter = car.AddComponent<ChapterOneDirector>();
            chapter.StartupManagedExternally = true;
            progress = new ChapterOneProgress();
            Set(progress, "stage", (ChapterOneStage)11);
            Set(chapter, "progress", progress);
            Set(chapter, "initialized", true);
            journey = car.AddComponent<VehicleJourneyDirector>();
            driver = car.GetComponent<AutomaticVehicleDriver>();
            seats = car.GetComponent<VehicleKeyUse>();
            Set(journey, "chapter", chapter);
            Set(seats, "chapter", chapter);
            Set(driver, "roadPoints", new[] { Origin, Origin + Vector3.forward * 20f });
            Set(driver, "roadSpeeds", new[] { 5f, 0f });
            Set(driver, "parkingPoints", new[] { Origin + Vector3.forward * 20f, Origin + Vector3.forward * 30f });
            Set(driver, "parkingSpeeds", new[] { 2f, 0f });
            Set(driver, "stationDeparturePoints", new[] { Origin + Vector3.forward * 30f,
                Origin + new Vector3(0f, 0f, 20f), Origin + new Vector3(-40f, 0f, 20f) });
            Set(driver, "stationDepartureSpeeds", new[] { 2f, 5f, 0f });
            Set(driver, "obstacleLayers", (LayerMask)0);
            actor = Create("Station departure test MC").transform;
            actor.gameObject.SetActive(false);
            actor.position = Origin;
            actor.gameObject.AddComponent<CharacterController>();
            actor.gameObject.AddComponent<PlayerController>();
            actor.gameObject.AddComponent<StoryPlayerSeating>();
            Set(journey, "player", actor);
            Transform seat = Create("Station driver seat").transform;
            seat.SetParent(car.transform, false);
            seat.localPosition = new Vector3(.36f, .24f, .18f);
            Set(seats, "driverSeat", seat);
            var key = ScriptableObject.CreateInstance<SaksiTerakhir.Inventory.ItemDefinition>();
            objects.Add(key);
            Set(key, "id", "item.fixture.car_key");
            var keyLock = car.AddComponent<SaksiTerakhir.Inventory.ItemKeyLock>();
            Set(keyLock, "requiredKey", key);
            keyLock.RestoreSavedState(true);
            Set(seats, "keyLock", keyLock);
            typeof(VehicleJourneyDirector).GetMethod("ResolveDependencies", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(journey, null);
        }

        [TearDown]
        public void TearDown()
        {
            journey?.PrepareRestore();
            foreach (Object value in objects) if (value != null) Object.DestroyImmediate(value);
            objects.Clear();
        }

        [Test]
        public void StationDepartureHasItsOwnCursorAndTurnsRightFromSouthToWest()
        {
            driver.BeginStationDeparture(15f);
            Assert.That(driver.IsStationDepartureRoute, Is.True);
            Assert.That(driver.IsParkingRoute, Is.False);
            Assert.That(driver.Distance, Is.EqualTo(15f));
            Assert.That(car.transform.position, Is.EqualTo(Origin + new Vector3(-5f, 0f, 20f)));
            Assert.That(Vector3.Dot(car.transform.forward, Vector3.left), Is.GreaterThan(.99f));
            Assert.That(driver.BuildPath(false).Length, Is.EqualTo(20f));
            Assert.That(driver.BuildPath(true).Length, Is.EqualTo(10f));
        }

        [Test]
        public void RestoringAnOutboundCheckpointResumesTheExitRouteWithoutBoardingAgain()
        {
            Set(progress, "stage", (ChapterOneStage)12);
            int boarded = 0;
            seats.Boarded += () => boarded++;
            var checkpoint = new SavedVehicleJourney { version = 1, phase = (VehicleJourneyPhase)7,
                routeDistance = 15f, playerSeated = true, keyUnlocked = true };
            journey.Restore(checkpoint);
            Assert.That(driver.IsStationDepartureRoute && driver.IsMoving, Is.True);
            Assert.That(driver.Distance, Is.EqualTo(15f));
            Assert.That(car.transform.position, Is.EqualTo(Origin + new Vector3(-5f, 0f, 20f)));
            Assert.That(seats.IsOccupied, Is.True);
            Assert.That(boarded, Is.Zero);
            Assert.That(journey.Capture().MatchesChapter(progress), Is.True);
        }

        [Test]
        public void ChapterEndingBeginsWhileDrivingAndKeepsMovingThroughTheFade()
        {
            Set(progress, "stage", ChapterOneStage.LeavingFuelStation);
            Set(journey, "<Phase>k__BackingField", VehicleJourneyPhase.LeavingStation);
            var dialogue = car.AddComponent<StoryDialogueController>();
            Set(journey, "dialogue", dialogue);
            var station = car.AddComponent<FuelStationChapterDirector>();
            Set(station, "chapter", car.GetComponent<ChapterOneDirector>());
            Set(station, "fadeSeconds", 2.5f);
            Set(journey, "fuelStation", station);
            driver.BeginStationDeparture();
            MethodInfo checkEnding = typeof(VehicleJourneyDirector).GetMethod("TryBeginEndingWhileDriving",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(checkEnding, Is.Not.Null, "Departure needs an ending transition before the driver stops at arrival.");

            for (int frame = 0; frame < 1200 && journey.Phase == VehicleJourneyPhase.LeavingStation; frame++)
            {
                driver.Advance(.02f);
                checkEnding.Invoke(journey, null);
            }

            Assert.That(journey.Phase, Is.EqualTo(VehicleJourneyPhase.ChapterFinished));
            Assert.That(driver.IsMoving, Is.True, "Fade must start before the route's arrival stops the car.");
            Assert.That(driver.Speed, Is.GreaterThan(0f));
            // This fixture keeps the Rigidbody inactive; no physics step updates its Transform.
            // The first 10 m leave the forecourt and the next segment is the right-turn lane.
            Assert.That(driver.Distance, Is.GreaterThan(10.6f),
                "The exit's right turn must finish before the ending fade.");
            float fadeStartDistance = driver.Distance;
            for (int frame = 0; frame < 126; frame++) driver.Advance(.02f);
            Assert.That(driver.Distance, Is.GreaterThan(fadeStartDistance));
            Assert.That(driver.IsMoving, Is.True, "The car must still be travelling when the fade becomes fully black.");
            Assert.That(driver.Speed, Is.GreaterThan(0f));
            Assert.That(journey.Capture().MatchesChapter(progress), Is.True);
        }

        [Test]
        public void ReturningToCarBoardsWhileStationActivitiesStillPreventBoarding()
        {
            Set(progress, "stage", ChapterOneStage.AtFuelStation);
            Assert.That(seats.TryBoard(actor), Is.False);
            Set(progress, "stage", (ChapterOneStage)11);
            Assert.That(progress.Story.FuelStation.BeginExploration(), Is.True);
            Assert.That(progress.Story.FuelStation.AdvanceTime(FuelStationProgress.FreeTimeSeconds, 5f), Is.True);
            Assert.That(seats.TryBoard(actor), Is.False, "The ready call must finish before MC can board.");
            Assert.That(progress.Story.FuelStation.MarkCallDelivered(), Is.True);
            Assert.That(seats.TryBoard(actor), Is.True);
            Assert.That(progress.Stage, Is.EqualTo((ChapterOneStage)11));
        }

        [Test]
        public void FinishedCheckpointKeepsTheCarAtTheExitEndWithoutRestartingTheJourney()
        {
            Set(progress, "stage", ChapterOneStage.Complete);
            journey.Restore(new SavedVehicleJourney { version = 1, phase = (VehicleJourneyPhase)8,
                routeDistance = 50f, playerSeated = true, keyUnlocked = true });
            Assert.That(driver.IsMoving, Is.False);
            Assert.That(car.transform.position, Is.EqualTo(Origin + new Vector3(-40f, 0f, 20f)));
            Assert.That(journey.Phase, Is.EqualTo((VehicleJourneyPhase)8));
            Assert.That(journey.Capture().MatchesChapter(progress), Is.True);
        }

        [Test]
        public void FinishedCheckpointPreservesTheMovingFadeSavePositionInsteadOfJumpingToTheRouteEnd()
        {
            Set(progress, "stage", ChapterOneStage.Complete);
            journey.Restore(new SavedVehicleJourney { version = 1, phase = VehicleJourneyPhase.ChapterFinished,
                routeDistance = 37f, playerSeated = true, keyUnlocked = true });

            Assert.That(driver.IsMoving, Is.False, "A completed save must not restart driving.");
            Assert.That(driver.Distance, Is.EqualTo(37f));
            Assert.That(car.transform.position, Is.EqualTo(Origin + new Vector3(-27f, 0f, 20f)),
                "The final checkpoint is captured while the car moves under black; restore must retain that position.");
            Assert.That(journey.Capture().routeDistance, Is.EqualTo(37f));
            Assert.That(journey.Capture().MatchesChapter(progress), Is.True);
        }

        [Test]
        public void RestoringAnUnfinishedFadeResumesDrivingFromTheSavedExitCursor()
        {
            Set(progress, "stage", ChapterOneStage.LeavingFuelStation);
            var checkpoint = new SavedVehicleJourney { version = 1, phase = VehicleJourneyPhase.ChapterFinished,
                routeDistance = 37f, playerSeated = true, keyUnlocked = true };
            Assert.That(checkpoint.Validate() && checkpoint.MatchesChapter(progress), Is.True);

            journey.Restore(checkpoint);

            Assert.That(journey.Phase, Is.EqualTo(VehicleJourneyPhase.ChapterFinished));
            Assert.That(driver.Distance, Is.EqualTo(37f));
            Assert.That(driver.IsStationDepartureRoute && driver.IsMoving, Is.True,
                "A checkpoint taken before full black must resume its fade while the car keeps travelling.");
            driver.Advance(.5f);
            Assert.That(driver.Distance, Is.GreaterThan(37f));
            Assert.That(journey.Capture().MatchesChapter(progress), Is.True);
        }

        [Test]
        public void LegacyFinishedCheckpointWithoutARouteCursorKeepsTheExitEndpoint()
        {
            FuelStationProgress station = progress.Story.FuelStation;
            Assert.That(station.BeginExploration(), Is.True);
            Assert.That(station.AdvanceTime(FuelStationProgress.FreeTimeSeconds, 5f), Is.True);
            Assert.That(station.MarkCallDelivered(), Is.True);
            Assert.That(station.ChooseDestination(), Is.True);
            Assert.That(station.Depart(), Is.True);
            Assert.That(station.Finish(), Is.True);
            Set(progress, "stage", ChapterOneStage.Complete);

            journey.Restore(new SavedVehicleJourney());

            Assert.That(journey.Phase, Is.EqualTo(VehicleJourneyPhase.ChapterFinished));
            Assert.That(driver.IsMoving, Is.False);
            Assert.That(driver.Distance, Is.EqualTo(50f));
            Assert.That(car.transform.position, Is.EqualTo(Origin + new Vector3(-40f, 0f, 20f)));
        }

        [Test]
        public void SynchronousDepartureCheckpointDoesNotReuseTheParkingRouteCursor()
        {
            Set(progress, "stage", (ChapterOneStage)12);
            Assert.That(seats.RestoreOccupants(actor), Is.True);
            driver.Begin(true, 7f);
            Set(journey, "<Phase>k__BackingField", (VehicleJourneyPhase)7);

            SavedVehicleJourney checkpoint = journey.Capture();

            Assert.That(checkpoint.Validate() && checkpoint.MatchesChapter(progress), Is.True);
            Assert.That(checkpoint.routeDistance, Is.Zero,
                "The outbound checkpoint is published before its driver begins, so parking distance cannot become outbound progress.");
        }

        [TestCase(5, 10, false, true)]
        [TestCase(5, 11, false, true)]
        [TestCase(6, 11, true, true)]
        [TestCase(6, 10, true, false)]
        [TestCase(7, 12, true, true)]
        [TestCase(7, 11, true, false)]
        [TestCase(8, 8, true, true)]
        public void SavedVehiclePhaseMustAgreeWithTheChapterCheckpoint(int phase, int stage, bool occupied, bool expected)
        {
            Set(progress, "stage", (ChapterOneStage)stage);
            var checkpoint = new SavedVehicleJourney { version = 1, phase = (VehicleJourneyPhase)phase,
                playerSeated = occupied, keyUnlocked = true };
            Assert.That(checkpoint.Validate(), Is.True);
            Assert.That(checkpoint.MatchesChapter(progress), Is.EqualTo(expected));
        }

        [Test]
        public void OldVehiclePhaseNumbersAndSaveVersionStayCompatible()
        {
            Assert.That((int)VehicleJourneyPhase.ParkedOffice, Is.Zero);
            Assert.That((int)VehicleJourneyPhase.DepartureDialogue, Is.EqualTo(1));
            Assert.That((int)VehicleJourneyPhase.OnRoad, Is.EqualTo(2));
            Assert.That((int)VehicleJourneyPhase.ParkedStation, Is.EqualTo(5));
            Assert.That(new SavedVehicleJourney().Validate(), Is.True);
        }

        [Test]
        public void MissingLegacyVehicleRecordDoesNotTurnOfficeCompletionIntoTheStationFinale()
        {
            Set(progress, "stage", ChapterOneStage.Complete);
            journey.Restore(new SavedVehicleJourney());
            Assert.That(journey.Phase, Is.EqualTo(VehicleJourneyPhase.ParkedOffice));
            Assert.That(driver.IsMoving || driver.IsStationDepartureRoute, Is.False);
            Assert.That(seats.IsOccupied, Is.False);
        }

        [TestCase(VehicleJourneyPhase.StationDepartureDialogue)]
        [TestCase(VehicleJourneyPhase.LeavingStation)]
        [TestCase(VehicleJourneyPhase.ChapterFinished)]
        public void SavePayloadRoundTripsValidStationStagesAndRestoresTheirOwnRoute(VehicleJourneyPhase phase)
        {
            Set(progress, "stage", ChapterOneStage.ReachVehicle);
            Set(progress, "hasCarKey", true);
            Set(progress.Story, "sharedMealQuest", StoryQuestStatus.Skipped);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.JourneyStarted), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.StationParked), Is.True);
            Assert.That(progress.Story.FuelStation.BeginExploration(), Is.True);
            Assert.That(progress.Story.FuelStation.AdvanceTime(FuelStationProgress.FreeTimeSeconds, 5f), Is.True);
            Assert.That(progress.Story.FuelStation.MarkCallDelivered(), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.StationReady), Is.True);
            if (phase != VehicleJourneyPhase.StationDepartureDialogue)
            {
                Assert.That(progress.Story.FuelStation.ChooseDestination(), Is.True);
                Assert.That(progress.TryApply(ChapterOneEvent.StationDepartureStarted), Is.True);
                if (phase == VehicleJourneyPhase.ChapterFinished)
                    Assert.That(progress.TryApply(ChapterOneEvent.ChapterFinished), Is.True);
            }
            var checkpoint = new SavedVehicleJourney { version = 1, phase = phase,
                routeDistance = phase == VehicleJourneyPhase.ChapterFinished ? 50f : 15f,
                playerSeated = true, keyUnlocked = true };
            var snapshot = new GameSaveSnapshot { runId = Guid.NewGuid().ToString("D"),
                savedUtc = "2026-10-11T00:00:00Z", scenePath = "Assets/_Project/Scenes/Regional Archive Office.unity",
                chapter = progress, player = new SavedPose(actor.position, actor.rotation), vehicleJourney = checkpoint };
            Assert.That(snapshot.Validate(out string error), Is.True, error);
            GameSaveSnapshot restored = JsonUtility.FromJson<GameSaveSnapshot>(JsonUtility.ToJson(snapshot));
            Assert.That(restored.Validate(out error), Is.True, error);
            Assert.That(restored.chapter.Stage, Is.EqualTo(progress.Stage));
            Assert.That(restored.chapter.Story.FuelStation.CallDelivered, Is.True);
            Assert.That(restored.chapter.Story.NasiUdukReceived, Is.True);
            journey.Restore(restored.vehicleJourney);
            Assert.That(seats.IsOccupied, Is.True);
            Assert.That(driver.IsMoving, Is.EqualTo(phase == VehicleJourneyPhase.LeavingStation));
            Assert.That(driver.IsStationDepartureRoute, Is.EqualTo(phase != VehicleJourneyPhase.StationDepartureDialogue));
            Assert.That(journey.Capture().MatchesChapter(restored.chapter), Is.True);
        }

        private GameObject Create(string name)
        {
            var created = new GameObject(name);
            objects.Add(created);
            return created;
        }

        private static void Set(object owner, string field, object value) => owner.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    }
}
