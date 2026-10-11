using System;
using SaksiTerakhir.Inventory;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    public enum ChapterOneStage
    {
        MeetRooftopWorkers,
        AnswerBossCall,
        MeetBoss,
        FindColleagues,
        EscortLastColleague,
        FinalBossBriefing,
        CollectCarKey,
        ReachVehicle,
        Complete,
        // Append journey checkpoints without changing existing saved stage values.
        TravelToFuelStation = 9,
        AtFuelStation = 10,
        ReturnToVehicle = 11,
        LeavingFuelStation = 12,
        // Existing stage values are part of the save format. Morning precedes them.
        MorningRoutine = -1
    }

    public enum ChapterOneEvent
    {
        RooftopDialogueFinished,
        BossCallFinished,
        FirstBriefingFinished,
        ColleagueDialogueStarted,
        LastColleagueIntroFinished,
        ColleagueDialogueFinished,
        ColleagueArrived,
        BossMovedToMeeting,
        WalkDialogueFinished,
        MeetingAssembled,
        PlayerSeated,
        FinalBriefingFinished,
        CarKeyReceived,
        VehicleReached,
        MorningRoutineFinished,
        JourneyStarted = 15,
        StationParked = 16,
        StationReady = 17,
        StationDepartureStarted = 18,
        ChapterFinished = 19
    }

    [Serializable]
    public sealed class ChapterOneProgress
    {
        [SerializeField] private int version = 1;
        [SerializeField] private ChapterOneStage stage;
        [SerializeField] private bool rakaMet;
        [SerializeField] private bool sintaMet;
        [SerializeField] private string firstColleagueId = string.Empty;
        [SerializeField] private string lastColleagueId = string.Empty;
        [SerializeField] private bool rakaArrived;
        [SerializeField] private bool sintaArrived;
        [SerializeField] private bool walkDialogueComplete;
        [SerializeField] private bool lastColleagueIntroComplete;
        [SerializeField] private string lastColleagueIntroSelectionKey = string.Empty;
        [SerializeField] private int bossMeetingSide;
        [SerializeField] private bool meetingAssembled;
        [SerializeField] private bool playerSeated;
        [SerializeField] private bool hasCarKey;
        [SerializeField] private bool bossCallAvailable;
        [SerializeField] private bool enteredOffice;
        [SerializeField] private ChapterOneStoryState story = new ChapterOneStoryState();
        [SerializeField] private QuestJournalState journal = new QuestJournalState();

        public int Version => version;
        public ChapterOneStage Stage => stage;
        public bool RakaMet => rakaMet;
        public bool SintaMet => sintaMet;
        public string FirstColleagueId => firstColleagueId;
        public string LastColleagueId => lastColleagueId;
        public bool RakaArrived => rakaArrived;
        public bool SintaArrived => sintaArrived;
        public bool WalkDialogueComplete => walkDialogueComplete;
        public bool LastColleagueIntroComplete => lastColleagueIntroComplete;
        public string LastColleagueIntroSelectionKey => lastColleagueIntroSelectionKey ?? string.Empty;
        public int BossMeetingSide => bossMeetingSide;
        public bool MeetingAssembled => meetingAssembled;
        public bool PlayerSeated => playerSeated;
        public bool HasCarKey => hasCarKey;
        public bool MorningRoutineComplete => stage != ChapterOneStage.MorningRoutine;
        public bool BossCallAvailable => bossCallAvailable;
        public bool EnteredOffice => enteredOffice;
        public ChapterOneStoryState Story => story ?? (story = new ChapterOneStoryState());
        public QuestJournalState Journal => journal ?? (journal = new QuestJournalState());

        public static ChapterOneProgress CreateFullChapter()
            => new ChapterOneProgress { stage = ChapterOneStage.MorningRoutine };

        public bool TryMigrateLegacyVehicleReachedCheckpoint()
        {
            // Earlier chapter saves ended at VehicleReached, before the car/SPBU sequence existed.
            // Keep all choices, inventory ownership and rewards; only resume the unfinished journey.
            if (stage != ChapterOneStage.Complete || Story.FuelStation.Complete || Story.FuelStation.Departed)
                return false;
            stage = ChapterOneStage.ReachVehicle;
            return true;
        }

        public bool TryEnterOffice()
        {
            if (stage != ChapterOneStage.MorningRoutine || enteredOffice) return false;
            enteredOffice = true;
            return true;
        }

        public bool TryTriggerBossCall()
        {
            if (stage != ChapterOneStage.AnswerBossCall || bossCallAvailable) return false;
            bossCallAvailable = true;
            return true;
        }

        public bool Meets(ChapterOneStoryCondition condition, InventoryState playerInventory = null)
            => ChapterOneConditionEvaluator.Matches(this, condition, playerInventory);

        public bool TryApplyStoryEvent(ChapterOneStoryEvent kind)
        {
            if (Story.DepartureDriver != ChapterOneDepartureDriver.Unassigned) return false;
            switch (kind)
            {
                case ChapterOneStoryEvent.SharedMealStarted:
                case ChapterOneStoryEvent.SharedMealCompleted:
                case ChapterOneStoryEvent.SharedMealSkipped:
                    if (stage != ChapterOneStage.CollectCarKey || hasCarKey) return false;
                    break;
                case ChapterOneStoryEvent.AgendaWritten:
                case ChapterOneStoryEvent.AgendaSkipped:
                case ChapterOneStoryEvent.TravelReceiptSubmitted:
                case ChapterOneStoryEvent.DeskPreparationStarted:
                case ChapterOneStoryEvent.DeskPreparationCompleted:
                case ChapterOneStoryEvent.DeskPreparationSkipped:
                case ChapterOneStoryEvent.SuppliesStarted:
                case ChapterOneStoryEvent.SuppliesCompleted:
                case ChapterOneStoryEvent.SuppliesSkipped:
                    if (stage != ChapterOneStage.CollectCarKey && stage != ChapterOneStage.ReachVehicle) return false;
                    break;
                case ChapterOneStoryEvent.TravelCashDistributed:
                case ChapterOneStoryEvent.NasiUdukReceived:
                case ChapterOneStoryEvent.DepartureStarted:
                    if (!hasCarKey || (stage != ChapterOneStage.ReachVehicle && stage != ChapterOneStage.Complete)) return false;
                    break;
            }
            return Story.TryApply(kind);
        }

        public bool TryRecordTravelPurchase(string itemId)
        {
            return Story.DepartureDriver == ChapterOneDepartureDriver.Unassigned
                && (stage == ChapterOneStage.CollectCarKey || stage == ChapterOneStage.ReachVehicle)
                && Story.TryRecordPurchase(itemId);
        }

        public bool ValidateStoryState(out string error)
        {
            if (version != 1 || !Enum.IsDefined(typeof(ChapterOneStage), stage))
            {
                error = "Unsupported version or invalid chapter stage.";
                return false;
            }
            if (!Story.Validate(out error)) return false;
            if (!Journal.Validate(out error)) return false;
            if (stage < ChapterOneStage.CollectCarKey
                && (Story.SharedMealQuest != StoryQuestStatus.NotStarted
                    || Story.DeskPreparationQuest != StoryQuestStatus.NotStarted
                    || Story.SuppliesQuest != StoryQuestStatus.NotStarted
                    || Story.Agenda != AgendaChoice.Unresolved || Story.PurchasedSupplyIds.Count > 0
                    || Story.TravelReceiptSubmitted))
            {
                error = "Preparation choices cannot precede the final boss briefing.";
                return false;
            }
            if ((!hasCarKey && (Story.NasiUdukReceived || Story.TravelCashPerPerson != 0
                    || Story.DepartureDriver != ChapterOneDepartureDriver.Unassigned))
                || (hasCarKey && Story.SharedMealQuest == StoryQuestStatus.Active)
                || (Story.SharedMealQuest == StoryQuestStatus.Active && stage != ChapterOneStage.CollectCarKey)
                || (hasCarKey && stage < ChapterOneStage.ReachVehicle)
                || (!hasCarKey && stage >= ChapterOneStage.ReachVehicle))
            {
                error = "Story reward or meal status conflicts with car-key progress.";
                return false;
            }
            if ((stage == ChapterOneStage.TravelToFuelStation || stage == ChapterOneStage.AtFuelStation
                || stage == ChapterOneStage.ReturnToVehicle || stage == ChapterOneStage.LeavingFuelStation)
                && Story.DepartureDriver == ChapterOneDepartureDriver.Unassigned)
            {
                error = "Vehicle journey requires a resolved departure driver.";
                return false;
            }
            if ((stage == ChapterOneStage.ReturnToVehicle && !Story.FuelStation.Ready)
                || (stage == ChapterOneStage.LeavingFuelStation && !Story.FuelStation.Departed)
                || (Story.FuelStation.Complete && stage != ChapterOneStage.Complete))
            {
                error = "Station activity conflicts with chapter progress.";
                return false;
            }
            return true;
        }

        public bool TryApply(ChapterOneEvent kind, string actorId = "", string dialogueSelectionKey = "")
        {
            switch (kind)
            {
                case ChapterOneEvent.MorningRoutineFinished when stage == ChapterOneStage.MorningRoutine:
                    if (Story.Breakfast == BreakfastChoice.Unresolved)
                        Story.TryApply(ChapterOneStoryEvent.BreakfastSkipped);
                    stage = ChapterOneStage.MeetRooftopWorkers;
                    return true;
                case ChapterOneEvent.RooftopDialogueFinished when stage == ChapterOneStage.MeetRooftopWorkers:
                    stage = ChapterOneStage.AnswerBossCall;
                    return true;
                case ChapterOneEvent.BossCallFinished when stage == ChapterOneStage.AnswerBossCall:
                    stage = ChapterOneStage.MeetBoss;
                    return true;
                case ChapterOneEvent.FirstBriefingFinished when stage == ChapterOneStage.MeetBoss:
                    stage = ChapterOneStage.FindColleagues;
                    return true;
                case ChapterOneEvent.ColleagueDialogueFinished when stage == ChapterOneStage.FindColleagues:
                    if (!IsColleague(actorId) || firstColleagueId.Length != 0) return false;
                    firstColleagueId = actorId;
                    SetMet(actorId);
                    return true;
                case ChapterOneEvent.ColleagueDialogueStarted when stage == ChapterOneStage.FindColleagues:
                    if (firstColleagueId.Length == 0 || !IsColleague(actorId)
                        || actorId == firstColleagueId) return false;
                    lastColleagueId = actorId;
                    lastColleagueIntroSelectionKey = dialogueSelectionKey ?? string.Empty;
                    stage = ChapterOneStage.EscortLastColleague;
                    return true;
                case ChapterOneEvent.LastColleagueIntroFinished when stage == ChapterOneStage.EscortLastColleague:
                    if (actorId != lastColleagueId || lastColleagueIntroComplete) return false;
                    lastColleagueIntroComplete = true;
                    return true;
                case ChapterOneEvent.WalkDialogueFinished when stage == ChapterOneStage.EscortLastColleague:
                    if (actorId != lastColleagueId || !lastColleagueIntroComplete
                        || walkDialogueComplete) return false;
                    walkDialogueComplete = true;
                    SetMet(actorId);
                    AdvanceIfReady();
                    return true;
                case ChapterOneEvent.ColleagueArrived when stage == ChapterOneStage.FindColleagues
                    || stage == ChapterOneStage.EscortLastColleague:
                    if (!IsColleague(actorId) || !IsMet(actorId) || HasArrived(actorId)) return false;
                    if (actorId == lastColleagueId && (!lastColleagueIntroComplete
                        || !walkDialogueComplete)) return false;
                    if (actorId == "NPC-003") rakaArrived = true;
                    else sintaArrived = true;
                    AdvanceIfReady();
                    return true;
                case ChapterOneEvent.BossMovedToMeeting when stage == ChapterOneStage.FindColleagues
                    || stage == ChapterOneStage.EscortLastColleague
                    || stage == ChapterOneStage.FinalBossBriefing:
                    if (bossMeetingSide != 0 || !rakaArrived && !sintaArrived) return false;
                    bossMeetingSide = actorId == "left" ? 1 : actorId == "right" ? 2 : 0;
                    return bossMeetingSide != 0;
                case ChapterOneEvent.MeetingAssembled when stage == ChapterOneStage.FinalBossBriefing:
                    if (meetingAssembled || bossMeetingSide == 0 || !rakaArrived || !sintaArrived)
                        return false;
                    meetingAssembled = true;
                    return true;
                case ChapterOneEvent.PlayerSeated when stage == ChapterOneStage.FinalBossBriefing:
                    if (!meetingAssembled || playerSeated) return false;
                    playerSeated = true;
                    return true;
                case ChapterOneEvent.FinalBriefingFinished when stage == ChapterOneStage.FinalBossBriefing:
                    if (!meetingAssembled || !playerSeated) return false;
                    stage = ChapterOneStage.CollectCarKey;
                    return true;
                case ChapterOneEvent.CarKeyReceived when stage == ChapterOneStage.CollectCarKey:
                    // Completing the reception handoff closes the meal opportunity.
                    // A completed meal is immutable and remains the ATM branch.
                    Story.TryApply(ChapterOneStoryEvent.SharedMealSkipped);
                    hasCarKey = true;
                    stage = ChapterOneStage.ReachVehicle;
                    return true;
                case ChapterOneEvent.VehicleReached when stage == ChapterOneStage.ReachVehicle && hasCarKey:
                    stage = ChapterOneStage.Complete;
                    return true;
                case ChapterOneEvent.JourneyStarted when stage == ChapterOneStage.ReachVehicle && hasCarKey
                    && Story.DepartureDriver != ChapterOneDepartureDriver.Unassigned:
                    stage = ChapterOneStage.TravelToFuelStation;
                    return true;
                case ChapterOneEvent.StationParked when stage == ChapterOneStage.TravelToFuelStation && hasCarKey
                    && Story.DepartureDriver != ChapterOneDepartureDriver.Unassigned:
                    stage = ChapterOneStage.AtFuelStation;
                    return true;
                case ChapterOneEvent.StationReady when stage == ChapterOneStage.AtFuelStation && Story.FuelStation.Ready:
                    stage = ChapterOneStage.ReturnToVehicle;
                    return true;
                case ChapterOneEvent.StationDepartureStarted when stage == ChapterOneStage.ReturnToVehicle:
                    if (!Story.FuelStation.Depart()) return false;
                    stage = ChapterOneStage.LeavingFuelStation;
                    return true;
                case ChapterOneEvent.ChapterFinished when stage == ChapterOneStage.LeavingFuelStation:
                    if (!Story.FuelStation.Finish()) return false;
                    stage = ChapterOneStage.Complete;
                    return true;
                default:
                    return false;
            }
        }

        private void AdvanceIfReady()
        {
            if (walkDialogueComplete && rakaMet && sintaMet && rakaArrived && sintaArrived)
                stage = ChapterOneStage.FinalBossBriefing;
        }

        private static bool IsColleague(string actorId) => actorId == "NPC-003" || actorId == "NPC-004";
        private bool IsMet(string actorId) => actorId == "NPC-003" ? rakaMet : sintaMet;
        private bool HasArrived(string actorId) => actorId == "NPC-003" ? rakaArrived : sintaArrived;
        private void SetMet(string actorId)
        {
            if (actorId == "NPC-003") rakaMet = true;
            else sintaMet = true;
        }
    }
}
