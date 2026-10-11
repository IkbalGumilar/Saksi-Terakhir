using System;
using SaksiTerakhir.Inventory;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    public enum ChapterOneConditionId
    {
        BreakfastPaid, BreakfastOwed, BreakfastSkipped,
        WrongRouteActive, WrongRouteCompleted, WrongRouteSkipped, KnowsDigitizationTeam,
        ColleagueAFoundFirst, ColleagueBFoundFirst, ColleagueAFoundLast, ColleagueBFoundLast,
        KnowsColleagueABusinessPlan, KnowsColleagueBHolidayPlans, Agenda24AugustWritten,
        SharedMealAvailable, SharedMealActive, SharedMealCompleted, SharedMealSkipped,
        ColleagueAGoingToAtm, ColleagueBWaitingAtVehicle, ColleaguesWaitingAtVehicle,
        AtmVisited, TravelCashReceived, DepartureDriverA, DepartureDriverPlayer,
        NasiUdukReceived, HasPurchasedSupplies, TravelReceiptSubmitted,
        CarKeyReceived, InventoryContainsEntry,
        Agenda24AugustSkipped, RooftopDialogueComplete, BossCallComplete,
        FirstBriefingComplete, ColleagueSearchComplete, FinalBriefingComplete, DepartureStarted,
        DeskPreparationActive, DeskPreparationCompleted, DeskPreparationSkipped,
        SuppliesActive, SuppliesCompleted, SuppliesSkipped,
        GrandfatherReportHeard, GrandfatherNightAdviceHeard, GrandfatherMorningAdviceHeard,
        GrandfatherDirectionsComplete, StationReady, StationPhotoTaken, ChapterOneFinished
    }

    // A requirement may expect either true or false. Unknown predicates and
    // missing inventory context fail closed even when the expected value is false.
    [Serializable]
    public sealed class ChapterOneStoryCondition
    {
        [SerializeField] private ChapterOneConditionId condition;
        [SerializeField] private bool expectedValue = true;
        [SerializeField] private string inventoryEntryId = string.Empty;
        [SerializeField] private InventoryEntryType inventoryEntryType;

        public ChapterOneConditionId Condition => condition;
        public bool ExpectedValue => expectedValue;
        public string InventoryEntryId => inventoryEntryId ?? string.Empty;
        public InventoryEntryType InventoryEntryType => inventoryEntryType;

        public ChapterOneStoryCondition(ChapterOneConditionId condition, bool expectedValue = true,
            string inventoryEntryId = "", InventoryEntryType inventoryEntryType = InventoryEntryType.Item)
        {
            this.condition = condition;
            this.expectedValue = expectedValue;
            this.inventoryEntryId = inventoryEntryId ?? string.Empty;
            this.inventoryEntryType = inventoryEntryType;
        }

        public bool Validate(out string error)
        {
            if (!Enum.IsDefined(typeof(ChapterOneConditionId), condition)
                || (condition == ChapterOneConditionId.InventoryContainsEntry
                    && (string.IsNullOrWhiteSpace(inventoryEntryId)
                        || !Enum.IsDefined(typeof(InventoryEntryType), inventoryEntryType))))
            {
                error = "Unknown story condition or missing inventory entry ID/type.";
                return false;
            }
            error = string.Empty;
            return true;
        }
    }
}
