using System.IO;
using NUnit.Framework;
using SaksiTerakhir.Story;
using UnityEngine;
using UnityEngine.TestTools;

namespace SaksiTerakhir.Tests
{
    public sealed class ChapterOneSaveTests
    {
        private string path;

        [SetUp]
        public void SetUp() => path = Path.Combine(Path.GetTempPath(), "saksi-chapter-test-" + System.Guid.NewGuid() + ".json");

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }

        [TestCase("NPC-003", "NPC-004")]
        [TestCase("NPC-004", "NPC-003")]
        public void RoundTripPreservesColleagueOrderArrivalsAndKey(string first, string last)
        {
            ChapterOneProgress progress = ReachColleagues();
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, first), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, last), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, first), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.BossMovedToMeeting, "left"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, last), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, last), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, last), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.MeetingAssembled), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.PlayerSeated), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.FinalBriefingFinished), Is.True);
            Assert.That(new CarKeyInventory(progress).TryAdd("car-key"), Is.True);

            ChapterOneSaveStore.Save(progress, path);
            ChapterOneProgress loaded = ChapterOneSaveStore.Load(path);
            Assert.That(loaded.Version, Is.EqualTo(1));
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(loaded.FirstColleagueId, Is.EqualTo(first));
            Assert.That(loaded.LastColleagueId, Is.EqualTo(last));
            Assert.That(loaded.RakaMet && loaded.SintaMet && loaded.RakaArrived && loaded.SintaArrived,
                Is.True);
            Assert.That(loaded.WalkDialogueComplete && loaded.HasCarKey, Is.True);
            Assert.That(loaded.LastColleagueIntroComplete && loaded.MeetingAssembled
                && loaded.BossMeetingSide == 1 && loaded.PlayerSeated, Is.True);
            Assert.That(new CarKeyInventory(loaded).TryAdd("car-key"), Is.False);
        }

        [TestCase("left", 1)]
        [TestCase("right", 2)]
        public void RoundTripPreservesPendingMeetingSeatState(string bossSide, int expectedSide)
        {
            ChapterOneProgress progress = ReadyForBossInvitation(bossSide);
            ChapterOneSaveStore.Save(progress, path);

            ChapterOneProgress loaded = ChapterOneSaveStore.Load(path);
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.FinalBossBriefing));
            Assert.That(loaded.LastColleagueIntroComplete, Is.True);
            Assert.That(loaded.MeetingAssembled, Is.True);
            Assert.That(loaded.BossMeetingSide, Is.EqualTo(expectedSide));
            Assert.That(loaded.PlayerSeated, Is.False,
                "Reloading before the invitation must not silently put the player into the sofa.");
        }

        [TestCase("NPC-003", "NPC-004", "intro.b.after-a")]
        [TestCase("NPC-004", "NPC-003", "intro.a.after-b")]
        public void RoundTripPreservesSelectedDialogueDuringLastColleagueIntroduction(string first,
            string last, string selectionKey)
        {
            ChapterOneProgress progress = ReachColleagues();
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, first), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, last, selectionKey), Is.True);
            ChapterOneSaveStore.Save(progress, path);

            ChapterOneProgress loaded = ChapterOneSaveStore.Load(path);
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            Assert.That(loaded.LastColleagueId, Is.EqualTo(last));
            Assert.That(loaded.LastColleagueIntroComplete, Is.False);
            Assert.That(loaded.LastColleagueIntroSelectionKey, Is.EqualTo(selectionKey),
                "Reloading must keep the pre-start variant even though LastColleagueId has already changed.");
            Assert.That(loaded.TryApply(ChapterOneEvent.LastColleagueIntroFinished, last), Is.True);
            Assert.That(loaded.TryApply(ChapterOneEvent.WalkDialogueFinished, last), Is.True);
            Assert.That(loaded.LastColleagueIntroSelectionKey, Is.EqualTo(selectionKey));
        }

        [Test]
        public void InventoryAcceptsOnlyOneStoryKeyAtTheCorrectStage()
        {
            ChapterOneProgress progress = ReachColleagues();
            CarKeyInventory inventory = new CarKeyInventory(progress);
            Assert.That(inventory.TryAdd("car-key"), Is.False);
            Assert.That(inventory.TryAdd("archive-key"), Is.False);
            Assert.That(inventory.Has("car-key"), Is.False);
        }

        [Test]
        public void MissingSaveBeginsFreshWithoutAnError()
        {
            ChapterOneProgress loaded = ChapterOneSaveStore.Load(path);
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
        }

        [Test]
        public void SavingAgainReplacesPreviousSnapshotWithoutTemporaryFile()
        {
            ChapterOneProgress progress = new ChapterOneProgress();
            ChapterOneSaveStore.Save(progress, path);
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            ChapterOneSaveStore.Save(progress, path);

            Assert.That(ChapterOneSaveStore.Load(path).Stage,
                Is.EqualTo(ChapterOneStage.AnswerBossCall));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void MalformedOrFutureSaveBeginsFreshWithError()
        {
            File.WriteAllText(path, "{oops");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Chapter one save"));
            Assert.That(ChapterOneSaveStore.Load(path).Stage,
                Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));

            File.WriteAllText(path, "{\"version\":2,\"progress\":{}}");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Chapter one save"));
            Assert.That(ChapterOneSaveStore.Load(path).Stage,
                Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
        }

        [Test]
        public void CompletedCallAndRooftopDialogueAreNotReopenedByLoading()
        {
            ChapterOneProgress progress = new ChapterOneProgress();
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            progress.TryApply(ChapterOneEvent.BossCallFinished);
            ChapterOneSaveStore.Save(progress, path);
            ChapterOneProgress loaded = ChapterOneSaveStore.Load(path);
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.MeetBoss));
            Assert.That(loaded.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.False);
            Assert.That(loaded.TryApply(ChapterOneEvent.BossCallFinished), Is.False);
        }

        [TestCase("NPC-003", "NPC-004", true)]
        [TestCase("NPC-004", "NPC-003", false)]
        public void RoundTripPreservesAllOptionalFactsAndResolvedDeparture(string first, string last,
            bool completedMeal)
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApplyStoryEvent(completedMeal
                ? ChapterOneStoryEvent.BreakfastPaid : ChapterOneStoryEvent.BreakfastOwed), Is.True);
            if (completedMeal)
            {
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteStarted), Is.True);
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteCompleted), Is.True);
            }
            else Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteSkipped), Is.True);
            AdvanceToCarKey(progress, first, last);
            Assert.That(progress.TryApplyStoryEvent(completedMeal
                ? ChapterOneStoryEvent.AgendaWritten : ChapterOneStoryEvent.AgendaSkipped), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationStarted), Is.True);
            if (completedMeal)
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationCompleted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesStarted), Is.True);
            if (completedMeal)
            {
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealStarted), Is.True);
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealCompleted), Is.True);
            }
            else Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealSkipped), Is.True);
            Assert.That(progress.TryRecordTravelPurchase("water"), Is.True);
            Assert.That(progress.TryRecordTravelPurchase("medicine"), Is.True);
            if (completedMeal)
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesCompleted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.TravelReceiptSubmitted), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            if (completedMeal)
            {
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.AtmVisited), Is.True);
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.TravelCashDistributed), Is.True);
            }
            else Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.VehicleReached), Is.True);

            ChapterOneSaveStore.Save(progress, path);
            ChapterOneProgress loaded = ChapterOneSaveStore.Load(path);
            Assert.That(loaded.Version, Is.EqualTo(1));
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.Complete));
            Assert.That(loaded.FirstColleagueId, Is.EqualTo(first));
            Assert.That(loaded.LastColleagueId, Is.EqualTo(last));
            Assert.That(loaded.Story.Breakfast, Is.EqualTo(completedMeal ? BreakfastChoice.Paid : BreakfastChoice.Owed));
            Assert.That(loaded.Story.WrongRouteQuest, Is.EqualTo(completedMeal
                ? StoryQuestStatus.Completed : StoryQuestStatus.Skipped));
            Assert.That(loaded.Story.Agenda, Is.EqualTo(completedMeal ? AgendaChoice.Written : AgendaChoice.Skipped));
            Assert.That(loaded.Story.Agenda24AugustWritten, Is.EqualTo(completedMeal));
            Assert.That(loaded.Story.DeskPreparationQuest, Is.EqualTo(completedMeal
                ? StoryQuestStatus.Completed : StoryQuestStatus.Skipped));
            Assert.That(loaded.Story.DeskPrepared, Is.EqualTo(completedMeal));
            Assert.That(loaded.Story.SuppliesQuest, Is.EqualTo(completedMeal
                ? StoryQuestStatus.Completed : StoryQuestStatus.Skipped));
            Assert.That(loaded.Story.SharedMealQuest, Is.EqualTo(completedMeal
                ? StoryQuestStatus.Completed : StoryQuestStatus.Skipped));
            Assert.That(loaded.Story.AtmVisited, Is.EqualTo(completedMeal));
            Assert.That(loaded.Story.TravelCashPerPerson, Is.EqualTo(completedMeal ? 2250000 : 0));
            Assert.That(loaded.Story.NasiUdukReceived, Is.EqualTo(!completedMeal));
            Assert.That(loaded.Story.PurchasedSupplyIds, Is.EquivalentTo(new[] { "water", "medicine" }));
            Assert.That(loaded.Story.TravelReceiptSubmitted, Is.True);
            Assert.That(loaded.Story.DepartureDriver, Is.EqualTo(completedMeal
                ? ChapterOneDepartureDriver.ColleagueA : ChapterOneDepartureDriver.Player));
            Assert.That(loaded.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.KnowsColleagueABusinessPlan)),
                Is.EqualTo(last == "NPC-003"));
            Assert.That(loaded.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.KnowsColleagueBHolidayPlans)),
                Is.EqualTo(last == "NPC-004"));
            Assert.That(loaded.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.False);
            Assert.That(loaded.TryApplyStoryEvent(ChapterOneStoryEvent.TravelCashDistributed), Is.False);
            Assert.That(loaded.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.False);
        }

        [Test]
        public void RoundTripKeepsActiveOptionalQuestsResumableBeforeKeyCollection()
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteStarted), Is.True);
            AdvanceToCarKey(progress, "NPC-003", "NPC-004");
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationStarted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesStarted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealStarted), Is.True);
            ChapterOneSaveStore.Save(progress, path);

            ChapterOneProgress loaded = ChapterOneSaveStore.Load(path);
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.CollectCarKey));
            Assert.That(loaded.Story.Breakfast, Is.EqualTo(BreakfastChoice.Unresolved));
            Assert.That(loaded.Story.Agenda, Is.EqualTo(AgendaChoice.Unresolved));
            Assert.That(loaded.Story.WrongRouteQuest, Is.EqualTo(StoryQuestStatus.Active));
            Assert.That(loaded.Story.DeskPreparationQuest, Is.EqualTo(StoryQuestStatus.Active));
            Assert.That(loaded.Story.SuppliesQuest, Is.EqualTo(StoryQuestStatus.Active));
            Assert.That(loaded.Story.SharedMealQuest, Is.EqualTo(StoryQuestStatus.Active));
            Assert.That(loaded.Story.DepartureDriver, Is.EqualTo(ChapterOneDepartureDriver.Unassigned));
            Assert.That(loaded.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteCompleted), Is.True);
            Assert.That(loaded.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationCompleted), Is.True);
            Assert.That(loaded.TryRecordTravelPurchase("water"), Is.True);
            Assert.That(loaded.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesCompleted), Is.True);
            Assert.That(loaded.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealCompleted), Is.True);
            Assert.That(loaded.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(loaded.Story.SharedMealQuest, Is.EqualTo(StoryQuestStatus.Completed));
        }

        [Test]
        public void LegacyVersionOneSaveDefaultsNewFactsWithoutInventingBranchHistory()
        {
            File.WriteAllText(path, "{\"version\":1,\"progress\":{\"version\":1,\"stage\":7,"
                + "\"rakaMet\":true,\"sintaMet\":true,\"firstColleagueId\":\"NPC-003\","
                + "\"lastColleagueId\":\"NPC-004\",\"rakaArrived\":true,\"sintaArrived\":true,"
                + "\"walkDialogueComplete\":true,\"lastColleagueIntroComplete\":true,"
                + "\"bossMeetingSide\":1,\"meetingAssembled\":true,\"playerSeated\":true,\"hasCarKey\":true}}");

            ChapterOneProgress loaded = ChapterOneSaveStore.Load(path);
            Assert.That(loaded.Version, Is.EqualTo(1));
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(loaded.HasCarKey, Is.True);
            Assert.That(loaded.FirstColleagueId, Is.EqualTo("NPC-003"));
            Assert.That(loaded.LastColleagueId, Is.EqualTo("NPC-004"));
            Assert.That(loaded.LastColleagueIntroSelectionKey, Is.Empty);
            Assert.That(loaded.Story.Breakfast, Is.EqualTo(BreakfastChoice.Unresolved));
            Assert.That(loaded.Story.Agenda, Is.EqualTo(AgendaChoice.Unresolved));
            Assert.That(loaded.Story.WrongRouteQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
            Assert.That(loaded.Story.DeskPreparationQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
            Assert.That(loaded.Story.DeskPrepared, Is.False);
            Assert.That(loaded.Story.SuppliesQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
            Assert.That(loaded.Story.SharedMealQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
            Assert.That(loaded.Story.AtmVisited, Is.False);
            Assert.That(loaded.Story.TravelCashPerPerson, Is.Zero);
            Assert.That(loaded.Story.NasiUdukReceived, Is.False);
            Assert.That(loaded.Story.TravelReceiptSubmitted, Is.False);
            Assert.That(loaded.Story.PurchasedSupplyIds, Is.Empty);
            Assert.That(loaded.Story.DepartureDriver, Is.EqualTo(ChapterOneDepartureDriver.Unassigned));
            Assert.That(loaded.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.KnowsColleagueBHolidayPlans)), Is.True);
            Assert.That(loaded.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.SharedMealSkipped)), Is.False);
        }

        [TestCase("\"sharedMealQuest\":2")]
        [TestCase("\"agenda\":1")]
        [TestCase("\"purchasedSupplyIds\":[\"water\"]")]
        public void LoadingPreBriefingPreparationFactsRejectsTheCorruptedSave(string preparationFact)
        {
            File.WriteAllText(path, "{\"version\":1,\"progress\":{\"version\":1,\"stage\":3,\"story\":{"
                + preparationFact + "}}}");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Chapter one save"));
            ChapterOneProgress loaded = ChapterOneSaveStore.Load(path);
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
            Assert.That(loaded.Story.SharedMealQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
            Assert.That(loaded.Story.Agenda, Is.EqualTo(AgendaChoice.Unresolved));
            Assert.That(loaded.Story.PurchasedSupplyIds, Is.Empty);
        }

        private static void AdvanceToCarKey(ChapterOneProgress progress, string first, string last)
        {
            Assert.That(progress.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.BossCallFinished), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.FirstBriefingFinished), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, first), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, first), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.BossMovedToMeeting, "left"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, last), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, last), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, last), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, last), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.MeetingAssembled), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.PlayerSeated), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.FinalBriefingFinished), Is.True);
        }

        private static ChapterOneProgress ReachColleagues()
        {
            ChapterOneProgress progress = new ChapterOneProgress();
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            progress.TryApply(ChapterOneEvent.BossCallFinished);
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);
            return progress;
        }

        private static ChapterOneProgress ReadyForBossInvitation(string bossSide)
        {
            ChapterOneProgress progress = ReachColleagues();
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-003"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.BossMovedToMeeting, bossSide), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.MeetingAssembled), Is.True);
            return progress;
        }
    }
}
