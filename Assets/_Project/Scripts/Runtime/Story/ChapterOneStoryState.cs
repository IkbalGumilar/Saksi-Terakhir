using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    public enum BreakfastChoice { Unresolved, Paid, Owed, Skipped }
    public enum AgendaChoice { Unresolved, Written, Skipped }
    public enum StoryQuestStatus { NotStarted, Active, Completed, Skipped }
    public enum ChapterOneDepartureDriver { Unassigned, ColleagueA, Player }

    public enum ChapterOneStoryEvent
    {
        BreakfastPaid, BreakfastOwed, BreakfastSkipped,
        WrongRouteStarted, WrongRouteCompleted, WrongRouteSkipped,
        AgendaWritten, SharedMealStarted, SharedMealCompleted, SharedMealSkipped,
        AtmVisited, TravelCashDistributed, NasiUdukReceived, TravelReceiptSubmitted,
        DepartureStarted, AgendaSkipped,
        DeskPreparationStarted, DeskPreparationCompleted, DeskPreparationSkipped,
        SuppliesStarted, SuppliesCompleted, SuppliesSkipped
    }

    // Mutable session data belongs to ChapterOneProgress, not to shared quest
    // or dialogue ScriptableObjects. Fields are included in the same save file.
    [Serializable]
    public sealed class ChapterOneStoryState
    {
        public const int TravelCashAllocationPerPerson = 2250000;

        [SerializeField] private BreakfastChoice breakfast;
        [SerializeField] private StoryQuestStatus wrongRouteQuest;
        [SerializeField] private AgendaChoice agenda;
        [SerializeField] private StoryQuestStatus sharedMealQuest;
        [SerializeField] private StoryQuestStatus deskPreparationQuest;
        [SerializeField] private StoryQuestStatus suppliesQuest;
        [SerializeField] private bool atmVisited;
        [SerializeField] private int travelCashPerPerson;
        [SerializeField] private bool nasiUdukReceived;
        [SerializeField] private bool travelReceiptSubmitted;
        [SerializeField] private ChapterOneDepartureDriver departureDriver;
        [SerializeField] private List<string> purchasedSupplyIds = new List<string>();
        [SerializeField] private FuelStationProgress fuelStation = new FuelStationProgress();

        public BreakfastChoice Breakfast => breakfast;
        public StoryQuestStatus WrongRouteQuest => wrongRouteQuest;
        public AgendaChoice Agenda => agenda;
        public bool Agenda24AugustWritten => agenda == AgendaChoice.Written;
        public StoryQuestStatus SharedMealQuest => sharedMealQuest;
        public StoryQuestStatus DeskPreparationQuest => deskPreparationQuest;
        public bool DeskPrepared => deskPreparationQuest == StoryQuestStatus.Completed;
        public StoryQuestStatus SuppliesQuest => suppliesQuest;
        public bool AtmVisited => atmVisited;
        // Records the original allocation, not a spendable wallet balance.
        public int TravelCashPerPerson => travelCashPerPerson;
        // Historical receipt; current ownership is checked against inventory.
        public bool NasiUdukReceived => nasiUdukReceived;
        public bool TravelReceiptSubmitted => travelReceiptSubmitted;
        public ChapterOneDepartureDriver DepartureDriver => departureDriver;
        public IReadOnlyList<string> PurchasedSupplyIds => Supplies.AsReadOnly();
        public FuelStationProgress FuelStation => fuelStation ?? (fuelStation = new FuelStationProgress());
        private List<string> Supplies => purchasedSupplyIds ?? (purchasedSupplyIds = new List<string>());

        internal bool TryApply(ChapterOneStoryEvent kind)
        {
            switch (kind)
            {
                case ChapterOneStoryEvent.BreakfastPaid:
                    return ChooseBreakfast(BreakfastChoice.Paid);
                case ChapterOneStoryEvent.BreakfastOwed:
                    return ChooseBreakfast(BreakfastChoice.Owed);
                case ChapterOneStoryEvent.BreakfastSkipped:
                    return ChooseBreakfast(BreakfastChoice.Skipped);
                case ChapterOneStoryEvent.WrongRouteStarted:
                    return StartQuest(ref wrongRouteQuest);
                case ChapterOneStoryEvent.WrongRouteCompleted:
                    return CompleteQuest(ref wrongRouteQuest);
                case ChapterOneStoryEvent.WrongRouteSkipped:
                    return SkipQuest(ref wrongRouteQuest);
                case ChapterOneStoryEvent.AgendaWritten:
                    return ChooseAgenda(AgendaChoice.Written);
                case ChapterOneStoryEvent.AgendaSkipped:
                    return ChooseAgenda(AgendaChoice.Skipped);
                case ChapterOneStoryEvent.SharedMealStarted:
                    return StartQuest(ref sharedMealQuest);
                case ChapterOneStoryEvent.SharedMealCompleted:
                    return CompleteQuest(ref sharedMealQuest);
                case ChapterOneStoryEvent.SharedMealSkipped:
                    return SkipQuest(ref sharedMealQuest);
                case ChapterOneStoryEvent.DeskPreparationStarted:
                    return StartQuest(ref deskPreparationQuest);
                case ChapterOneStoryEvent.DeskPreparationCompleted:
                    return CompleteQuest(ref deskPreparationQuest);
                case ChapterOneStoryEvent.DeskPreparationSkipped:
                    return SkipQuest(ref deskPreparationQuest);
                case ChapterOneStoryEvent.SuppliesStarted:
                    return StartQuest(ref suppliesQuest);
                case ChapterOneStoryEvent.SuppliesCompleted:
                    return Supplies.Count > 0 && CompleteQuest(ref suppliesQuest);
                case ChapterOneStoryEvent.SuppliesSkipped:
                    return SkipQuest(ref suppliesQuest);
                case ChapterOneStoryEvent.AtmVisited:
                    if (sharedMealQuest != StoryQuestStatus.Completed || atmVisited) return false;
                    atmVisited = true;
                    return true;
                case ChapterOneStoryEvent.TravelCashDistributed:
                    if (!atmVisited || travelCashPerPerson != 0) return false;
                    travelCashPerPerson = TravelCashAllocationPerPerson;
                    return true;
                case ChapterOneStoryEvent.NasiUdukReceived:
                    if (sharedMealQuest != StoryQuestStatus.Skipped || nasiUdukReceived) return false;
                    nasiUdukReceived = true;
                    return true;
                case ChapterOneStoryEvent.TravelReceiptSubmitted:
                    if (Supplies.Count == 0 || travelReceiptSubmitted) return false;
                    travelReceiptSubmitted = true;
                    return true;
                case ChapterOneStoryEvent.DepartureStarted:
                    if (departureDriver != ChapterOneDepartureDriver.Unassigned) return false;
                    if (sharedMealQuest == StoryQuestStatus.Completed
                        && travelCashPerPerson == TravelCashAllocationPerPerson)
                        departureDriver = ChapterOneDepartureDriver.ColleagueA;
                    else if (sharedMealQuest == StoryQuestStatus.Skipped && nasiUdukReceived)
                        departureDriver = ChapterOneDepartureDriver.Player;
                    else return false;
                    if (breakfast == BreakfastChoice.Unresolved) breakfast = BreakfastChoice.Skipped;
                    if (agenda == AgendaChoice.Unresolved) agenda = AgendaChoice.Skipped;
                    SkipQuest(ref wrongRouteQuest);
                    SkipQuest(ref deskPreparationQuest);
                    SkipQuest(ref suppliesQuest);
                    return true;
                default:
                    return false;
            }
        }

        internal bool TryRecordPurchase(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            string canonicalId = itemId.Trim();
            if (Supplies.Contains(canonicalId)) return false;
            Supplies.Add(canonicalId);
            return true;
        }

        public bool Validate(out string error)
        {
            if (!FuelStation.Validate(out error)) return false;
            if (!Enum.IsDefined(typeof(BreakfastChoice), breakfast)
                || !Enum.IsDefined(typeof(AgendaChoice), agenda)
                || !Enum.IsDefined(typeof(StoryQuestStatus), wrongRouteQuest)
                || !Enum.IsDefined(typeof(StoryQuestStatus), sharedMealQuest)
                || !Enum.IsDefined(typeof(StoryQuestStatus), deskPreparationQuest)
                || !Enum.IsDefined(typeof(StoryQuestStatus), suppliesQuest)
                || !Enum.IsDefined(typeof(ChapterOneDepartureDriver), departureDriver))
            {
                error = "Unknown story choice or quest status.";
                return false;
            }
            if ((atmVisited && sharedMealQuest != StoryQuestStatus.Completed)
                || (travelCashPerPerson != 0 && (!atmVisited
                    || travelCashPerPerson != TravelCashAllocationPerPerson))
                || (nasiUdukReceived && sharedMealQuest != StoryQuestStatus.Skipped)
                || (departureDriver == ChapterOneDepartureDriver.ColleagueA && travelCashPerPerson == 0)
                || (departureDriver == ChapterOneDepartureDriver.Player && !nasiUdukReceived)
                || (travelReceiptSubmitted && Supplies.Count == 0)
                || (suppliesQuest == StoryQuestStatus.Completed && Supplies.Count == 0)
                || (departureDriver != ChapterOneDepartureDriver.Unassigned
                    && (breakfast == BreakfastChoice.Unresolved || agenda == AgendaChoice.Unresolved
                        || wrongRouteQuest == StoryQuestStatus.Active
                        || deskPreparationQuest == StoryQuestStatus.Active
                        || suppliesQuest == StoryQuestStatus.Active)))
            {
                error = "Story branch contains conflicting outcomes or premature rewards.";
                return false;
            }
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string itemId in Supplies)
            {
                if (string.IsNullOrWhiteSpace(itemId) || itemId != itemId.Trim() || !unique.Add(itemId))
                {
                    error = "Purchase history contains an empty or duplicate item ID.";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private bool ChooseBreakfast(BreakfastChoice choice)
        {
            if (breakfast != BreakfastChoice.Unresolved) return false;
            breakfast = choice;
            return true;
        }

        private bool ChooseAgenda(AgendaChoice choice)
        {
            if (agenda != AgendaChoice.Unresolved) return false;
            agenda = choice;
            return true;
        }

        private static bool StartQuest(ref StoryQuestStatus status)
        {
            if (status != StoryQuestStatus.NotStarted) return false;
            status = StoryQuestStatus.Active;
            return true;
        }

        private static bool CompleteQuest(ref StoryQuestStatus status)
        {
            if (status != StoryQuestStatus.Active) return false;
            status = StoryQuestStatus.Completed;
            return true;
        }

        private static bool SkipQuest(ref StoryQuestStatus status)
        {
            if (status != StoryQuestStatus.NotStarted && status != StoryQuestStatus.Active) return false;
            status = StoryQuestStatus.Skipped;
            return true;
        }
    }
}
