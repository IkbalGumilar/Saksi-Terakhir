using System;
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
        Complete
    }

    public enum ChapterOneEvent
    {
        RooftopDialogueFinished,
        BossCallFinished,
        FirstBriefingFinished,
        ColleagueDialogueStarted,
        ColleagueDialogueFinished,
        ColleagueArrived,
        WalkDialogueFinished,
        FinalBriefingFinished,
        CarKeyReceived,
        VehicleReached
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
        [SerializeField] private bool hasCarKey;

        public int Version => version;
        public ChapterOneStage Stage => stage;
        public bool RakaMet => rakaMet;
        public bool SintaMet => sintaMet;
        public string FirstColleagueId => firstColleagueId;
        public string LastColleagueId => lastColleagueId;
        public bool RakaArrived => rakaArrived;
        public bool SintaArrived => sintaArrived;
        public bool WalkDialogueComplete => walkDialogueComplete;
        public bool HasCarKey => hasCarKey;

        public bool TryApply(ChapterOneEvent kind, string actorId = "")
        {
            switch (kind)
            {
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
                    stage = ChapterOneStage.EscortLastColleague;
                    return true;
                case ChapterOneEvent.WalkDialogueFinished when stage == ChapterOneStage.EscortLastColleague:
                    if (actorId != lastColleagueId || walkDialogueComplete) return false;
                    walkDialogueComplete = true;
                    SetMet(actorId);
                    AdvanceIfReady();
                    return true;
                case ChapterOneEvent.ColleagueArrived when stage == ChapterOneStage.FindColleagues
                    || stage == ChapterOneStage.EscortLastColleague:
                    if (!IsColleague(actorId) || !IsMet(actorId) || HasArrived(actorId)) return false;
                    if (actorId == "NPC-003") rakaArrived = true;
                    else sintaArrived = true;
                    AdvanceIfReady();
                    return true;
                case ChapterOneEvent.FinalBriefingFinished when stage == ChapterOneStage.FinalBossBriefing:
                    stage = ChapterOneStage.CollectCarKey;
                    return true;
                case ChapterOneEvent.CarKeyReceived when stage == ChapterOneStage.CollectCarKey:
                    hasCarKey = true;
                    stage = ChapterOneStage.ReachVehicle;
                    return true;
                case ChapterOneEvent.VehicleReached when stage == ChapterOneStage.ReachVehicle && hasCarKey:
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
