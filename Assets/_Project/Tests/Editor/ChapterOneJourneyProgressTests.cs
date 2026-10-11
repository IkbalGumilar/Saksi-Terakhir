using System;
using System.IO;
using NUnit.Framework;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class ChapterOneJourneyProgressTests
    {
        // Numeric values let these tests compile against the legacy state machine
        // before the new transitions are implemented, and preserve save identities.
        private const ChapterOneEvent JourneyStarted = (ChapterOneEvent)15;
        private const ChapterOneEvent StationParked = (ChapterOneEvent)16;
        private const ChapterOneStage Traveling = (ChapterOneStage)9;
        private const ChapterOneStage AtStation = (ChapterOneStage)10;

        [Test]
        public void JourneyStartsOnlyAfterTheKeyAndDepartureBranchAreResolved()
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApply(JourneyStarted), Is.False);
            ReachKey(progress, false);
            Assert.That(progress.TryApply(JourneyStarted), Is.False,
                "Receiving the car key alone must not skip the departure conversation.");
            ResolveDeparture(progress, false);
            Assert.That(progress.TryApply(JourneyStarted), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(Traveling));
            Assert.That(progress.Story.DepartureDriver, Is.EqualTo(ChapterOneDepartureDriver.Player));
            Assert.That(progress.TryApply(JourneyStarted), Is.False);
        }

        [Test]
        public void FuelStopCannotPrecedeTheJourneyAndDoesNotCompleteTheChapter()
        {
            var progress = new ChapterOneProgress();
            ReachKey(progress, true);
            ResolveDeparture(progress, true);
            Assert.That(progress.TryApply(StationParked), Is.False);
            Assert.That(progress.TryApply(JourneyStarted), Is.True);
            Assert.That(progress.TryApply(StationParked), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(AtStation));
            Assert.That(progress.Stage, Is.Not.EqualTo(ChapterOneStage.Complete));
            Assert.That(progress.TryApply(StationParked), Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.VehicleReached), Is.False,
                "A legacy vehicle trigger must not finish the chapter during the fuel stop.");
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealSkipped), Is.False);
            Assert.That(progress.ValidateStoryState(out string error), Is.True, error);
        }

        [Test]
        public void BothJourneyStagesRoundTripWithoutLosingDriverOrRewards()
        {
            foreach (bool completedMeal in new[] { false, true })
            {
                var progress = new ChapterOneProgress();
                ReachKey(progress, completedMeal);
                ResolveDeparture(progress, completedMeal);
                Assert.That(progress.TryApply(JourneyStarted), Is.True);
                VerifyRoundTrip(progress, Traveling, completedMeal);
                Assert.That(progress.TryApply(StationParked), Is.True);
                VerifyRoundTrip(progress, AtStation, completedMeal);
            }
        }

        [Test]
        public void NewJourneySaveStatesRequireAResolvedDepartureDriver()
        {
            foreach (int stage in new[] { 9, 10 })
            {
                ChapterOneProgress invalid = JsonUtility.FromJson<ChapterOneProgress>(
                    "{\"version\":1,\"stage\":" + stage + ",\"hasCarKey\":true}");
                Assert.That(invalid.ValidateStoryState(out _), Is.False,
                    "A journey cannot resume without a resolved departure branch.");
            }
        }

        [Test]
        public void NewTransitionsKeepExistingSavedEnumValuesAndLegacyCompletion()
        {
            Assert.That((int)ChapterOneStage.MorningRoutine, Is.EqualTo(-1));
            Assert.That((int)ChapterOneStage.MeetRooftopWorkers, Is.EqualTo(0));
            Assert.That((int)ChapterOneStage.ReachVehicle, Is.EqualTo(7));
            Assert.That((int)ChapterOneStage.Complete, Is.EqualTo(8));
            Assert.That((int)ChapterOneEvent.VehicleReached, Is.EqualTo(13));
            Assert.That((int)ChapterOneEvent.MorningRoutineFinished, Is.EqualTo(14));
            Assert.That(Enum.GetName(typeof(ChapterOneStage), Traveling), Is.EqualTo("TravelToFuelStation"));
            Assert.That(Enum.GetName(typeof(ChapterOneStage), AtStation), Is.EqualTo("AtFuelStation"));
            Assert.That(Enum.GetName(typeof(ChapterOneEvent), JourneyStarted), Is.EqualTo("JourneyStarted"));
            Assert.That(Enum.GetName(typeof(ChapterOneEvent), StationParked), Is.EqualTo("StationParked"));
            var progress = new ChapterOneProgress();
            ReachKey(progress, false);
            Assert.That(progress.TryApply(ChapterOneEvent.VehicleReached), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.Complete));
            Assert.That(progress.TryApply(JourneyStarted), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyVehicleReachedCompletionReturnsToTheCarWithoutChangingChoicesOrRewards(bool completedMeal)
        {
            var progress = new ChapterOneProgress();
            ReachKey(progress, completedMeal);
            ResolveDeparture(progress, completedMeal);
            Assert.That(progress.TryApply(ChapterOneEvent.VehicleReached), Is.True);
            string originalChoices = JsonUtility.ToJson(progress.Story);
            Assert.That(progress.TryMigrateLegacyVehicleReachedCheckpoint(), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(progress.HasCarKey, Is.True);
            Assert.That(JsonUtility.ToJson(progress.Story), Is.EqualTo(originalChoices));
            Assert.That(progress.ValidateStoryState(out string error), Is.True, error);
            Assert.That(progress.TryMigrateLegacyVehicleReachedCheckpoint(), Is.False);
            Assert.That(progress.TryApply(JourneyStarted), Is.True);
        }

        [Test]
        public void ActualFuelStationCompletionIsNeverMigratedBackToTheOffice()
        {
            var progress = new ChapterOneProgress();
            ReachKey(progress, false);
            ResolveDeparture(progress, false);
            Assert.That(progress.TryApply(JourneyStarted), Is.True);
            Assert.That(progress.TryApply(StationParked), Is.True);
            Assert.That(progress.Story.FuelStation.BeginExploration(), Is.True);
            Assert.That(progress.Story.FuelStation.AdvanceTime(FuelStationProgress.FreeTimeSeconds, 0f), Is.True);
            Assert.That(progress.Story.FuelStation.MarkCallDelivered(), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.StationReady), Is.True);
            Assert.That(progress.Story.FuelStation.ChooseDestination(), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.StationDepartureStarted), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ChapterFinished), Is.True);
            string before = JsonUtility.ToJson(progress);
            Assert.That(progress.TryMigrateLegacyVehicleReachedCheckpoint(), Is.False);
            Assert.That(JsonUtility.ToJson(progress), Is.EqualTo(before));
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.Complete));
        }

        private static void VerifyRoundTrip(ChapterOneProgress progress, ChapterOneStage expectedStage,
            bool completedMeal)
        {
            string path = Path.Combine(Path.GetTempPath(), "saksi-journey-progress-" + Guid.NewGuid() + ".json");
            try
            {
                ChapterOneSaveStore.Save(progress, path);
                ChapterOneProgress loaded = ChapterOneSaveStore.Load(path);
                Assert.That(loaded.Stage, Is.EqualTo(expectedStage));
                Assert.That(loaded.Version, Is.EqualTo(1));
                Assert.That(loaded.HasCarKey, Is.True);
                Assert.That(loaded.Story.DepartureDriver, Is.EqualTo(completedMeal
                    ? ChapterOneDepartureDriver.ColleagueA : ChapterOneDepartureDriver.Player));
                Assert.That(loaded.Story.TravelCashPerPerson, Is.EqualTo(completedMeal ? 2250000 : 0));
                Assert.That(loaded.Story.NasiUdukReceived, Is.EqualTo(!completedMeal));
                Assert.That(loaded.ValidateStoryState(out string error), Is.True, error);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            }
        }

        private static void ResolveDeparture(ChapterOneProgress progress, bool completedMeal)
        {
            if (completedMeal)
            {
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.AtmVisited), Is.True);
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.TravelCashDistributed), Is.True);
            }
            else Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.True);
        }

        private static void ReachKey(ChapterOneProgress progress, bool completedMeal)
        {
            Assert.That(progress.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.BossCallFinished), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.FirstBriefingFinished), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-003"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.BossMovedToMeeting, "left"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.MeetingAssembled), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.PlayerSeated), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.FinalBriefingFinished), Is.True);
            if (completedMeal)
            {
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealStarted), Is.True);
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealCompleted), Is.True);
            }
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
        }
    }
}
