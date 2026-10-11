using System.Collections.Generic;
using SaksiTerakhir.Inventory;

namespace SaksiTerakhir.Story
{
    public static class ChapterOneConditionEvaluator
    {
        private const string ColleagueAId = "NPC-003";
        private const string ColleagueBId = "NPC-004";

        public static bool Matches(ChapterOneProgress progress, ChapterOneStoryCondition condition,
            InventoryState inventory = null)
        {
            return TryEvaluate(progress, condition, inventory, out bool actual)
                && actual == condition.ExpectedValue;
        }

        public static bool AllMet(ChapterOneProgress progress,
            IReadOnlyList<ChapterOneStoryCondition> conditions, InventoryState inventory = null)
        {
            if (conditions == null || conditions.Count == 0) return true;
            foreach (ChapterOneStoryCondition condition in conditions)
                if (!Matches(progress, condition, inventory)) return false;
            return true;
        }

        public static bool TryEvaluate(ChapterOneProgress progress, ChapterOneStoryCondition requirement,
            InventoryState inventory, out bool actual)
        {
            actual = false;
            if (progress == null || requirement == null || !requirement.Validate(out _)) return false;
            ChapterOneStoryState story = progress.Story;
            switch (requirement.Condition)
            {
                case ChapterOneConditionId.BreakfastPaid: actual = story.Breakfast == BreakfastChoice.Paid; break;
                case ChapterOneConditionId.BreakfastOwed: actual = story.Breakfast == BreakfastChoice.Owed; break;
                case ChapterOneConditionId.BreakfastSkipped: actual = story.Breakfast == BreakfastChoice.Skipped; break;
                case ChapterOneConditionId.WrongRouteActive: actual = story.WrongRouteQuest == StoryQuestStatus.Active; break;
                case ChapterOneConditionId.WrongRouteCompleted:
                case ChapterOneConditionId.KnowsDigitizationTeam:
                    actual = story.WrongRouteQuest == StoryQuestStatus.Completed; break;
                case ChapterOneConditionId.WrongRouteSkipped: actual = story.WrongRouteQuest == StoryQuestStatus.Skipped; break;
                case ChapterOneConditionId.ColleagueAFoundFirst: actual = progress.FirstColleagueId == ColleagueAId; break;
                case ChapterOneConditionId.ColleagueBFoundFirst: actual = progress.FirstColleagueId == ColleagueBId; break;
                case ChapterOneConditionId.ColleagueAFoundLast: actual = progress.LastColleagueId == ColleagueAId; break;
                case ChapterOneConditionId.ColleagueBFoundLast: actual = progress.LastColleagueId == ColleagueBId; break;
                case ChapterOneConditionId.KnowsColleagueABusinessPlan:
                    actual = progress.WalkDialogueComplete && progress.LastColleagueId == ColleagueAId; break;
                case ChapterOneConditionId.KnowsColleagueBHolidayPlans:
                    actual = progress.WalkDialogueComplete && progress.LastColleagueId == ColleagueBId; break;
                case ChapterOneConditionId.Agenda24AugustWritten: actual = story.Agenda24AugustWritten; break;
                case ChapterOneConditionId.Agenda24AugustSkipped: actual = story.Agenda == AgendaChoice.Skipped; break;
                case ChapterOneConditionId.SharedMealAvailable:
                    actual = progress.Stage == ChapterOneStage.CollectCarKey && !progress.HasCarKey
                        && (story.SharedMealQuest == StoryQuestStatus.NotStarted
                            || story.SharedMealQuest == StoryQuestStatus.Active); break;
                case ChapterOneConditionId.SharedMealActive: actual = story.SharedMealQuest == StoryQuestStatus.Active; break;
                case ChapterOneConditionId.SharedMealCompleted: actual = story.SharedMealQuest == StoryQuestStatus.Completed; break;
                case ChapterOneConditionId.SharedMealSkipped: actual = story.SharedMealQuest == StoryQuestStatus.Skipped; break;
                case ChapterOneConditionId.DeskPreparationActive: actual = story.DeskPreparationQuest == StoryQuestStatus.Active; break;
                case ChapterOneConditionId.DeskPreparationCompleted: actual = story.DeskPrepared; break;
                case ChapterOneConditionId.DeskPreparationSkipped: actual = story.DeskPreparationQuest == StoryQuestStatus.Skipped; break;
                case ChapterOneConditionId.SuppliesActive: actual = story.SuppliesQuest == StoryQuestStatus.Active; break;
                case ChapterOneConditionId.SuppliesCompleted: actual = story.SuppliesQuest == StoryQuestStatus.Completed; break;
                case ChapterOneConditionId.SuppliesSkipped: actual = story.SuppliesQuest == StoryQuestStatus.Skipped; break;
                case ChapterOneConditionId.ColleagueAGoingToAtm:
                    actual = story.SharedMealQuest == StoryQuestStatus.Completed && !story.AtmVisited; break;
                case ChapterOneConditionId.ColleagueBWaitingAtVehicle:
                    actual = story.SharedMealQuest == StoryQuestStatus.Completed
                        || (story.SharedMealQuest == StoryQuestStatus.Skipped && progress.HasCarKey); break;
                case ChapterOneConditionId.ColleaguesWaitingAtVehicle:
                    actual = story.SharedMealQuest == StoryQuestStatus.Skipped && progress.HasCarKey; break;
                case ChapterOneConditionId.AtmVisited: actual = story.AtmVisited; break;
                case ChapterOneConditionId.TravelCashReceived: actual = story.TravelCashPerPerson > 0; break;
                case ChapterOneConditionId.DepartureDriverA: actual = story.DepartureDriver == ChapterOneDepartureDriver.ColleagueA; break;
                case ChapterOneConditionId.DepartureDriverPlayer: actual = story.DepartureDriver == ChapterOneDepartureDriver.Player; break;
                case ChapterOneConditionId.NasiUdukReceived: actual = story.NasiUdukReceived; break;
                case ChapterOneConditionId.HasPurchasedSupplies: actual = story.PurchasedSupplyIds.Count > 0; break;
                case ChapterOneConditionId.TravelReceiptSubmitted: actual = story.TravelReceiptSubmitted; break;
                case ChapterOneConditionId.CarKeyReceived: actual = progress.HasCarKey; break;
                case ChapterOneConditionId.InventoryContainsEntry:
                    if (inventory == null) return false;
                    IReadOnlyList<InventoryEntry> entries = requirement.InventoryEntryType == InventoryEntryType.Document
                        ? inventory.Documents : inventory.Items;
                    foreach (InventoryEntry entry in entries)
                        if (entry.Id == requirement.InventoryEntryId) { actual = true; break; }
                    break;
                case ChapterOneConditionId.RooftopDialogueComplete: actual = progress.Stage > ChapterOneStage.MeetRooftopWorkers; break;
                case ChapterOneConditionId.BossCallComplete: actual = progress.Stage > ChapterOneStage.AnswerBossCall; break;
                case ChapterOneConditionId.FirstBriefingComplete: actual = progress.Stage >= ChapterOneStage.FindColleagues; break;
                case ChapterOneConditionId.ColleagueSearchComplete: actual = progress.RakaMet && progress.SintaMet; break;
                case ChapterOneConditionId.FinalBriefingComplete: actual = progress.Stage >= ChapterOneStage.CollectCarKey; break;
                case ChapterOneConditionId.DepartureStarted: actual = story.DepartureDriver != ChapterOneDepartureDriver.Unassigned; break;
                case ChapterOneConditionId.GrandfatherReportHeard: actual = story.FuelStation.ReportHeard; break;
                case ChapterOneConditionId.GrandfatherNightAdviceHeard: actual = story.FuelStation.NightAdviceHeard; break;
                case ChapterOneConditionId.GrandfatherMorningAdviceHeard: actual = story.FuelStation.MorningAdviceHeard; break;
                case ChapterOneConditionId.GrandfatherDirectionsComplete: actual = story.FuelStation.DirectionsComplete; break;
                case ChapterOneConditionId.StationReady: actual = story.FuelStation.Ready; break;
                case ChapterOneConditionId.StationPhotoTaken: actual = story.FuelStation.PhotoTaken; break;
                case ChapterOneConditionId.ChapterOneFinished: actual = story.FuelStation.Complete; break;
                default: return false;
            }
            return true;
        }
    }
}
