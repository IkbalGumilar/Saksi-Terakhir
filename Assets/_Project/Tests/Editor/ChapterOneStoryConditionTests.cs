using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Story;

namespace SaksiTerakhir.Tests
{
    public sealed class ChapterOneStoryConditionTests
    {
        [TestCase(ChapterOneStoryEvent.BreakfastPaid, BreakfastChoice.Paid, ChapterOneConditionId.BreakfastPaid)]
        [TestCase(ChapterOneStoryEvent.BreakfastOwed, BreakfastChoice.Owed, ChapterOneConditionId.BreakfastOwed)]
        [TestCase(ChapterOneStoryEvent.BreakfastSkipped, BreakfastChoice.Skipped, ChapterOneConditionId.BreakfastSkipped)]
        public void FirstBreakfastChoiceRejectsEveryLaterDecision(ChapterOneStoryEvent choice,
            BreakfastChoice expected, ChapterOneConditionId condition)
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApplyStoryEvent(choice), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.BreakfastPaid), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.BreakfastOwed), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.BreakfastSkipped), Is.False);
            Assert.That(progress.Story.Breakfast, Is.EqualTo(expected));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(condition)), Is.True);
        }

        [Test]
        public void PendingChoicesAreNotReportedAsSkipped()
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.Story.Breakfast, Is.EqualTo(BreakfastChoice.Unresolved));
            Assert.That(progress.Story.WrongRouteQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
            Assert.That(progress.Story.SharedMealQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.BreakfastSkipped)), Is.False);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.WrongRouteSkipped)), Is.False);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.SharedMealSkipped)), Is.False);
            Assert.That(progress.Story.DepartureDriver, Is.EqualTo(ChapterOneDepartureDriver.Unassigned));
        }

        [Test]
        public void WrongRouteKnowledgeRequiresCompletingAnActiveQuest()
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteCompleted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteStarted), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.WrongRouteActive)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.KnowsDigitizationTeam)), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteCompleted), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.KnowsDigitizationTeam)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.WrongRouteActive)), Is.False);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.WrongRouteCompleted)), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteStarted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteSkipped), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WrongRouteCanBeSkippedWithoutBeingCompleted(bool started)
        {
            var progress = new ChapterOneProgress();
            if (started) Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteStarted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteSkipped), Is.True);
            Assert.That(progress.Story.WrongRouteQuest, Is.EqualTo(StoryQuestStatus.Skipped));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.KnowsDigitizationTeam)), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteStarted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteCompleted), Is.False);
        }

        [TestCase(ChapterOneStoryEvent.SharedMealStarted)]
        [TestCase(ChapterOneStoryEvent.SharedMealCompleted)]
        [TestCase(ChapterOneStoryEvent.SharedMealSkipped)]
        public void SharedMealCannotResolveBeforeFinalBriefing(ChapterOneStoryEvent storyEvent)
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApplyStoryEvent(storyEvent), Is.False);
            Assert.That(progress.Story.SharedMealQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
        }

        [Test]
        public void SharedMealMustStartBeforeItCanComplete()
        {
            ChapterOneProgress progress = AtCarKey();
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.SharedMealAvailable)), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealCompleted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealStarted), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.SharedMealActive)), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealCompleted), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.SharedMealCompleted)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.SharedMealActive)), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealSkipped), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealStarted), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReceivingKeyExpiresAnUnfinishedMealWithoutGivingDepartureRewards(bool started)
        {
            ChapterOneProgress progress = AtCarKey();
            if (started) Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealStarted), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.Story.SharedMealQuest, Is.EqualTo(StoryQuestStatus.Skipped));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.SharedMealAvailable)), Is.False);
            Assert.That(progress.Story.DepartureDriver, Is.EqualTo(ChapterOneDepartureDriver.Unassigned));
            Assert.That(progress.Story.TravelCashPerPerson, Is.Zero);
            Assert.That(progress.Story.NasiUdukReceived, Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealStarted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealCompleted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.False);
        }

        [Test]
        public void ReceivingKeyPreservesACompletedMealWithoutAssigningDriver()
        {
            ChapterOneProgress progress = CompletedMeal();
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.Story.SharedMealQuest, Is.EqualTo(StoryQuestStatus.Completed));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.SharedMealCompleted)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.SharedMealSkipped)), Is.False);
            Assert.That(progress.Story.DepartureDriver, Is.EqualTo(ChapterOneDepartureDriver.Unassigned));
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.False);
        }

        [Test]
        public void CompletedMealRequiresAtmAndKeyBeforeOneCashGrantAndDeparture()
        {
            ChapterOneProgress progress = CompletedMeal();
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.ColleagueAGoingToAtm)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.ColleagueBWaitingAtVehicle)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.ColleaguesWaitingAtVehicle)), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.TravelCashDistributed), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.AtmVisited), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.TravelCashDistributed), Is.False,
                "Withdrawing cash does not hand it to MC before the vehicle departure sequence.");
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.TravelCashDistributed), Is.True);
            Assert.That(progress.Story.TravelCashPerPerson, Is.EqualTo(2250000));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.AtmVisited)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.TravelCashReceived)), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.TravelCashDistributed), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.AtmVisited), Is.False);
            Assert.That(progress.Story.TravelCashPerPerson, Is.EqualTo(2250000));
            Assert.That(progress.Story.DepartureDriver, Is.EqualTo(ChapterOneDepartureDriver.Unassigned));
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.True);
            Assert.That(progress.Story.DepartureDriver, Is.EqualTo(ChapterOneDepartureDriver.ColleagueA));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.DepartureDriverA)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.DepartureDriverPlayer)), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.False);
        }

        [Test]
        public void SkippedMealGivesNasiUdukAtVehicleAndMakesPlayerDriveAtDeparture()
        {
            ChapterOneProgress progress = AtCarKey();
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealSkipped), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.ColleaguesWaitingAtVehicle)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.ColleagueAGoingToAtm)), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.AtmVisited), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.TravelCashDistributed), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.False);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.NasiUdukReceived)), Is.True);
            Assert.That(progress.Story.TravelCashPerPerson, Is.Zero);
            Assert.That(progress.Story.DepartureDriver, Is.EqualTo(ChapterOneDepartureDriver.Unassigned));
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.True);
            Assert.That(progress.Story.DepartureDriver, Is.EqualTo(ChapterOneDepartureDriver.Player));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.DepartureDriverPlayer)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.DepartureDriverA)), Is.False);
        }

        [TestCase("NPC-003", "NPC-004", ChapterOneConditionId.ColleagueAFoundFirst,
            ChapterOneConditionId.ColleagueBFoundLast, ChapterOneConditionId.KnowsColleagueBHolidayPlans,
            ChapterOneConditionId.KnowsColleagueABusinessPlan)]
        [TestCase("NPC-004", "NPC-003", ChapterOneConditionId.ColleagueBFoundFirst,
            ChapterOneConditionId.ColleagueAFoundLast, ChapterOneConditionId.KnowsColleagueABusinessPlan,
            ChapterOneConditionId.KnowsColleagueBHolidayPlans)]
        public void ColleagueOrderDoesNotRevealWalkingConversationBeforeItEnds(string first, string last,
            ChapterOneConditionId firstCondition, ChapterOneConditionId lastCondition,
            ChapterOneConditionId learned, ChapterOneConditionId otherKnowledge)
        {
            ChapterOneProgress progress = AtColleagueSearch();
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, first), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, last), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(firstCondition)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(lastCondition)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(learned)), Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, last), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(learned)), Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, last), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(learned)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(otherKnowledge)), Is.False);
        }

        [Test]
        public void IntroSelectionKeyIsRecordedOnlyWhenLastColleagueStartIsValid()
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.LastColleagueIntroSelectionKey, Is.Empty);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004", "intro.b"), Is.False);
            Assert.That(progress.LastColleagueIntroSelectionKey, Is.Empty);
            AdvanceToColleagueSearch(progress);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-003", "intro.same"), Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-999", "intro.unknown"), Is.False);
            Assert.That(progress.LastColleagueIntroSelectionKey, Is.Empty);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004", "intro.b"), Is.True);
            Assert.That(progress.LastColleagueIntroSelectionKey, Is.EqualTo("intro.b"));
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-003", "intro.replacement"), Is.False);
            Assert.That(progress.LastColleagueIntroSelectionKey, Is.EqualTo("intro.b"));
        }

        [TestCase("NPC-003", "NPC-004", "intro.b.after-a")]
        [TestCase("NPC-004", "NPC-003", "intro.a.after-b")]
        public void IntroSelectionKeySurvivesIntroductionWalkAndArrival(string first, string last,
            string selectionKey)
        {
            ChapterOneProgress progress = AtColleagueSearch();
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, first), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, first), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.BossMovedToMeeting, "left"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, last, selectionKey), Is.True);
            Assert.That(progress.LastColleagueIntroSelectionKey, Is.EqualTo(selectionKey));
            Assert.That(progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, last), Is.True);
            Assert.That(progress.LastColleagueIntroSelectionKey, Is.EqualTo(selectionKey));
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, last), Is.True);
            Assert.That(progress.LastColleagueIntroSelectionKey, Is.EqualTo(selectionKey));
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, last), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.FinalBossBriefing));
            Assert.That(progress.LastColleagueIntroSelectionKey, Is.EqualTo(selectionKey));
        }

        [Test]
        public void ExistingColleagueStartCallsKeepAnEmptyIntroSelectionKey()
        {
            ChapterOneProgress progress = AtColleagueSearch();
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004"), Is.True);
            Assert.That(progress.LastColleagueIntroSelectionKey, Is.Empty);
        }

        [TestCase("NPC-003", "NPC-004", false)]
        [TestCase("NPC-003", "NPC-004", true)]
        [TestCase("NPC-004", "NPC-003", false)]
        [TestCase("NPC-004", "NPC-003", true)]
        public void ColleagueOrderAndMealOutcomeRemainIndependent(string first, string last, bool mealCompleted)
        {
            ChapterOneProgress progress = AtCarKey(first, last);
            if (mealCompleted)
            {
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealStarted), Is.True);
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealCompleted), Is.True);
            }
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            if (mealCompleted)
            {
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.AtmVisited), Is.True);
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.TravelCashDistributed), Is.True);
            }
            else Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.True);
            Assert.That(progress.FirstColleagueId, Is.EqualTo(first));
            Assert.That(progress.LastColleagueId, Is.EqualTo(last));
            Assert.That(progress.Story.DepartureDriver, Is.EqualTo(mealCompleted
                ? ChapterOneDepartureDriver.ColleagueA : ChapterOneDepartureDriver.Player));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(
                ChapterOneConditionId.KnowsColleagueABusinessPlan)), Is.EqualTo(last == "NPC-003"));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(
                ChapterOneConditionId.KnowsColleagueBHolidayPlans)), Is.EqualTo(last == "NPC-004"));
        }

        [Test]
        public void OptionalFactsSurviveIndependentMealBranchResolution()
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.BreakfastOwed), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteStarted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteCompleted), Is.True);
            AdvanceToCarKey(progress, "NPC-003", "NPC-004");
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.AgendaWritten), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.Story.SharedMealQuest, Is.EqualTo(StoryQuestStatus.Skipped));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.BreakfastOwed)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.KnowsDigitizationTeam)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.Agenda24AugustWritten)), Is.True);
        }

        [TestCase(ChapterOneStoryEvent.AgendaWritten, AgendaChoice.Written)]
        [TestCase(ChapterOneStoryEvent.AgendaSkipped, AgendaChoice.Skipped)]
        public void AgendaChoiceRequiresPreparationAndCannotBeReplaced(ChapterOneStoryEvent choice,
            AgendaChoice expected)
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApplyStoryEvent(choice), Is.False);
            Assert.That(progress.Story.Agenda, Is.EqualTo(AgendaChoice.Unresolved));
            AdvanceToCarKey(progress, "NPC-003", "NPC-004");
            Assert.That(progress.TryApplyStoryEvent(choice), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.AgendaWritten), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.AgendaSkipped), Is.False);
            Assert.That(progress.Story.Agenda, Is.EqualTo(expected));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.Agenda24AugustWritten)),
                Is.EqualTo(expected == AgendaChoice.Written));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.Agenda24AugustSkipped)),
                Is.EqualTo(expected == AgendaChoice.Skipped));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DepartureExpiresUnresolvedOfficeChoicesWithoutRewritingHistory(bool wrongRouteStarted)
        {
            var progress = new ChapterOneProgress();
            if (wrongRouteStarted)
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteStarted), Is.True);
            AdvanceToCarKey(progress, "NPC-003", "NPC-004");
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.Story.Breakfast, Is.EqualTo(BreakfastChoice.Unresolved));
            Assert.That(progress.Story.Agenda, Is.EqualTo(AgendaChoice.Unresolved));
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.True);
            Assert.That(progress.Story.Breakfast, Is.EqualTo(BreakfastChoice.Skipped));
            Assert.That(progress.Story.Agenda, Is.EqualTo(AgendaChoice.Skipped));
            Assert.That(progress.Story.WrongRouteQuest, Is.EqualTo(StoryQuestStatus.Skipped));
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.BreakfastPaid), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.AgendaWritten), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteCompleted), Is.False);
        }

        [Test]
        public void DeparturePreservesRecordedOfficeChoices()
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.BreakfastOwed), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteStarted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteCompleted), Is.True);
            AdvanceToCarKey(progress, "NPC-004", "NPC-003");
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.AgendaWritten), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.True);
            Assert.That(progress.Story.Breakfast, Is.EqualTo(BreakfastChoice.Owed));
            Assert.That(progress.Story.Agenda, Is.EqualTo(AgendaChoice.Written));
            Assert.That(progress.Story.WrongRouteQuest, Is.EqualTo(StoryQuestStatus.Completed));
        }

        [TestCase(ChapterOneStoryEvent.AgendaWritten)]
        [TestCase(ChapterOneStoryEvent.AgendaSkipped)]
        public void AgendaChoiceDoesNotSilentlyCompleteDeskPreparation(ChapterOneStoryEvent choice)
        {
            ChapterOneProgress progress = AtCarKey();
            Assert.That(progress.TryApplyStoryEvent(choice), Is.True);
            Assert.That(progress.Story.DeskPreparationQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
            Assert.That(progress.Story.DeskPrepared, Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationCompleted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationStarted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationCompleted), Is.True);
            Assert.That(progress.Story.DeskPrepared, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PreparationQuestsStartOnlyDuringPreparationBeforeDeparture(bool keyCollected)
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationStarted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesStarted), Is.False);
            AdvanceToCarKey(progress, "NPC-003", "NPC-004");
            if (keyCollected) Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationStarted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesStarted), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.DeskPreparationActive)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.SuppliesActive)), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationStarted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesStarted), Is.False);
        }

        [Test]
        public void DeskPreparationMustBeActiveBeforeCompletingAndCannotBeReopened()
        {
            ChapterOneProgress progress = AtCarKey();
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationCompleted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationStarted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationCompleted), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.DeskPreparationCompleted)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.DeskPreparationActive)), Is.False);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.DeskPreparationSkipped)), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationStarted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationSkipped), Is.False);
            Assert.That(progress.Story.Agenda, Is.EqualTo(AgendaChoice.Unresolved),
                "Completing preparation does not choose a separate agenda outcome.");
        }

        [TestCase("water")]
        [TestCase("medicine")]
        public void SuppliesCompletionRequiresAnActiveQuestAndAnyRecordedPurchase(string purchaseId)
        {
            ChapterOneProgress progress = AtCarKey();
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesCompleted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesStarted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesCompleted), Is.False);
            Assert.That(progress.Story.SuppliesQuest, Is.EqualTo(StoryQuestStatus.Active));
            Assert.That(progress.TryRecordTravelPurchase(purchaseId), Is.True);
            Assert.That(progress.Story.SuppliesQuest, Is.EqualTo(StoryQuestStatus.Active),
                "Buying supplies records a purchase without silently completing the quest.");
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesCompleted), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.SuppliesCompleted)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.SuppliesActive)), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesSkipped), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesStarted), Is.False);
            Assert.That(progress.Story.PurchasedSupplyIds, Is.EqualTo(new[] { purchaseId }));
        }

        [Test]
        public void RecordingPurchaseWithoutStartingSuppliesQuestCannotCompleteIt()
        {
            ChapterOneProgress progress = AtCarKey();
            Assert.That(progress.TryRecordTravelPurchase("water"), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesCompleted), Is.False);
            Assert.That(progress.Story.SuppliesQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PreparationQuestsCanBeSkippedFromPendingOrActiveWithoutCompleting(bool started)
        {
            ChapterOneProgress progress = AtCarKey();
            if (started)
            {
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationStarted), Is.True);
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesStarted), Is.True);
            }
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationSkipped), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesSkipped), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.DeskPreparationSkipped)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.SuppliesSkipped)), Is.True);
            Assert.That(progress.Story.DeskPrepared, Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationCompleted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesCompleted), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DepartureExpiresPendingAndActivePreparationQuests(bool started)
        {
            ChapterOneProgress progress = AtCarKey();
            if (started)
            {
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationStarted), Is.True);
                Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesStarted), Is.True);
            }
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.Story.DeskPreparationQuest, Is.EqualTo(started
                ? StoryQuestStatus.Active : StoryQuestStatus.NotStarted));
            Assert.That(progress.Story.SuppliesQuest, Is.EqualTo(started
                ? StoryQuestStatus.Active : StoryQuestStatus.NotStarted));
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.True);
            Assert.That(progress.Story.DeskPreparationQuest, Is.EqualTo(StoryQuestStatus.Skipped));
            Assert.That(progress.Story.SuppliesQuest, Is.EqualTo(StoryQuestStatus.Skipped));
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationStarted), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesStarted), Is.False);
        }

        [Test]
        public void DeparturePreservesCompletedPreparationQuests()
        {
            ChapterOneProgress progress = AtCarKey();
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationStarted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationCompleted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesStarted), Is.True);
            Assert.That(progress.TryRecordTravelPurchase("water"), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesCompleted), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.True);
            Assert.That(progress.Story.DeskPreparationQuest, Is.EqualTo(StoryQuestStatus.Completed));
            Assert.That(progress.Story.SuppliesQuest, Is.EqualTo(StoryQuestStatus.Completed));
            Assert.That(progress.Story.DeskPrepared, Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationSkipped), Is.False);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesSkipped), Is.False);
        }

        [TestCase(InventoryEntryType.Item)]
        [TestCase(InventoryEntryType.Document)]
        public void InventoryConditionTracksActualOwnershipAndType(InventoryEntryType type)
        {
            var progress = new ChapterOneProgress();
            var inventory = new InventoryState(1, 1);
            var entry = new InventoryEntry("field-notes", "Catatan lapangan", type);
            var condition = new ChapterOneStoryCondition(ChapterOneConditionId.InventoryContainsEntry,
                true, "field-notes", type);
            Assert.That(progress.Meets(condition, inventory), Is.False);
            Assert.That(inventory.TryAdd(entry), Is.True);
            Assert.That(progress.Meets(condition, inventory), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.InventoryContainsEntry,
                true, "field-notes", type == InventoryEntryType.Item
                    ? InventoryEntryType.Document : InventoryEntryType.Item), inventory), Is.False);
            Assert.That(inventory.Remove(entry), Is.True);
            Assert.That(progress.Meets(condition, inventory), Is.False);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.InventoryContainsEntry,
                false, "field-notes", type), inventory), Is.True);
        }

        [Test]
        public void StoryKeyFactDoesNotInventAnInventoryEntry()
        {
            ChapterOneProgress progress = AtCarKey();
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.CarKeyReceived)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.InventoryContainsEntry,
                true, "car-key"), new InventoryState(1, 1)), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void InvalidAndMissingConditionContextFailsClosed(bool expected)
        {
            var progress = new ChapterOneProgress();
            var inventory = new InventoryState(1, 1);
            Assert.That(progress.Meets(null, inventory), Is.False);
            Assert.That(progress.Meets(new ChapterOneStoryCondition((ChapterOneConditionId)999, expected), inventory), Is.False);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.InventoryContainsEntry,
                expected, "missing")), Is.False);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.InventoryContainsEntry,
                expected, "missing", (InventoryEntryType)999), inventory), Is.False);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.InventoryContainsEntry,
                expected, ""), inventory), Is.False);
        }

        [Test]
        public void FalseExpectationMatchesAnAbsentEntryInAnAvailableInventory()
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.InventoryContainsEntry,
                false, "water"), new InventoryState(1, 1)), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.BreakfastPaid, false)), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.BreakfastPaid), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.BreakfastPaid, false)), Is.False);
        }

        [Test]
        public void TravelPurchasesRequirePreparationStageAndUniqueNonemptyIds()
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryRecordTravelPurchase("water"), Is.False);
            AdvanceToCarKey(progress, "NPC-003", "NPC-004");
            Assert.That(progress.TryRecordTravelPurchase(null), Is.False);
            Assert.That(progress.TryRecordTravelPurchase(" "), Is.False);
            Assert.That(progress.TryRecordTravelPurchase("water"), Is.True);
            Assert.That(progress.TryRecordTravelPurchase("water"), Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.TryRecordTravelPurchase("snacks"), Is.True);
            Assert.That(progress.Story.PurchasedSupplyIds, Is.EquivalentTo(new[] { "water", "snacks" }));
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.HasPurchasedSupplies)), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.VehicleReached), Is.True);
            Assert.That(progress.TryRecordTravelPurchase("medicine"), Is.False);
        }

        [Test]
        public void TravelReceiptRequiresAtLeastOnePurchaseAndCanOnlyBeSubmittedOnce()
        {
            ChapterOneProgress progress = AtCarKey();
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.TravelReceiptSubmitted), Is.False);
            Assert.That(progress.TryRecordTravelPurchase("water"), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.TravelReceiptSubmitted), Is.True);
            Assert.That(progress.Meets(new ChapterOneStoryCondition(ChapterOneConditionId.TravelReceiptSubmitted)), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.TravelReceiptSubmitted), Is.False);
        }

        [Test]
        public void UnknownStoryEventsDoNotResolveAnyPendingChoice()
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApplyStoryEvent((ChapterOneStoryEvent)999), Is.False);
            Assert.That(progress.Story.Breakfast, Is.EqualTo(BreakfastChoice.Unresolved));
            Assert.That(progress.Story.WrongRouteQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
            Assert.That(progress.Story.SharedMealQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
        }

        [TestCase("version", 0)]
        [TestCase("version", 2)]
        [TestCase("stage", -2)]
        [TestCase("stage", 999)]
        public void SaveIntegrityRejectsUnknownChapterVersionOrStage(string fieldName, int value)
        {
            var progress = new ChapterOneProgress();
            SetSavedField(progress, fieldName, fieldName == "stage" ? (object)(ChapterOneStage)value : value);
            Assert.That(progress.ValidateStoryState(out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [TestCase("sharedMealQuest", StoryQuestStatus.Completed)]
        [TestCase("sharedMealQuest", StoryQuestStatus.Skipped)]
        [TestCase("deskPreparationQuest", StoryQuestStatus.Completed)]
        [TestCase("suppliesQuest", StoryQuestStatus.Active)]
        public void SaveIntegrityRejectsPreparationQuestFactsBeforeFinalBriefing(string fieldName,
            StoryQuestStatus status)
        {
            ChapterOneProgress progress = AtColleagueSearch();
            SetSavedField(progress.Story, fieldName, status);
            Assert.That(progress.ValidateStoryState(out _), Is.False);
        }

        [TestCase(AgendaChoice.Written)]
        [TestCase(AgendaChoice.Skipped)]
        public void SaveIntegrityRejectsAgendaChoicesBeforeFinalBriefing(AgendaChoice choice)
        {
            ChapterOneProgress progress = AtColleagueSearch();
            SetSavedField(progress.Story, "agenda", choice);
            Assert.That(progress.ValidateStoryState(out _), Is.False);
        }

        [Test]
        public void SaveIntegrityRejectsPurchaseHistoryBeforeFinalBriefing()
        {
            ChapterOneProgress progress = AtColleagueSearch();
            SetSavedField(progress.Story, "purchasedSupplyIds", new List<string> { "water" });
            Assert.That(progress.ValidateStoryState(out _), Is.False);
        }

        [TestCase(ChapterOneStage.FindColleagues, true)]
        [TestCase(ChapterOneStage.CollectCarKey, true)]
        [TestCase(ChapterOneStage.ReachVehicle, false)]
        [TestCase(ChapterOneStage.Complete, false)]
        public void SaveIntegrityRejectsKeyOwnershipContradictingChapterStage(ChapterOneStage stage,
            bool hasCarKey)
        {
            ChapterOneProgress progress = AtCarKey();
            SetSavedField(progress, "stage", stage);
            SetSavedField(progress, "hasCarKey", hasCarKey);
            Assert.That(progress.ValidateStoryState(out _), Is.False);
        }

        [Test]
        public void SaveIntegrityRejectsActiveMealAfterKeyCollection()
        {
            ChapterOneProgress progress = AtCarKey();
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            SetSavedField(progress.Story, "sharedMealQuest", StoryQuestStatus.Active);
            Assert.That(progress.ValidateStoryState(out _), Is.False);
        }

        [Test]
        public void SaveIntegrityAcceptsLegacyKeyProgressWithUnknownOptionalHistory()
        {
            ChapterOneProgress progress = AtCarKey();
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            SetSavedField(progress, "story", null);
            Assert.That(progress.ValidateStoryState(out string error), Is.True);
            Assert.That(error, Is.Empty);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(progress.HasCarKey, Is.True);
            Assert.That(progress.Story.SharedMealQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
            Assert.That(progress.Story.DeskPreparationQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
            Assert.That(progress.Story.SuppliesQuest, Is.EqualTo(StoryQuestStatus.NotStarted));
            Assert.That(progress.Story.Agenda, Is.EqualTo(AgendaChoice.Unresolved));
            Assert.That(progress.Story.PurchasedSupplyIds, Is.Empty);
            Assert.That(progress.Story.DepartureDriver, Is.EqualTo(ChapterOneDepartureDriver.Unassigned));
        }

        private static void SetSavedField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "The controlled invalid-save fixture must target a serialized field.");
            field.SetValue(target, value);
        }

        private static ChapterOneProgress CompletedMeal()
        {
            ChapterOneProgress progress = AtCarKey();
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealStarted), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealCompleted), Is.True);
            return progress;
        }

        private static ChapterOneProgress AtCarKey(string first = "NPC-003", string last = "NPC-004")
        {
            var progress = new ChapterOneProgress();
            AdvanceToCarKey(progress, first, last);
            return progress;
        }

        private static ChapterOneProgress AtColleagueSearch()
        {
            var progress = new ChapterOneProgress();
            AdvanceToColleagueSearch(progress);
            return progress;
        }

        private static void AdvanceToColleagueSearch(ChapterOneProgress progress)
        {
            Assert.That(progress.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.BossCallFinished), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.FirstBriefingFinished), Is.True);
        }

        private static void AdvanceToCarKey(ChapterOneProgress progress, string first, string last)
        {
            AdvanceToColleagueSearch(progress);
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
    }
}
