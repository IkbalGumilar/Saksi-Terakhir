using UnityEngine;

namespace SaksiTerakhir.Story
{
    // Saved world coordinates belong to a particular layout. Progress and item
    // identities can survive a move to a new building even when those poses cannot.
    [DisallowMultipleComponent]
    public sealed class ChapterOneWorldLayout : MonoBehaviour
    {
        public const string CurrentId = "archive-campus-bab1-v1";
        [SerializeField] private string layoutId = CurrentId;
        [SerializeField] private Transform parking;
        [SerializeField] private Transform rooftop;
        [SerializeField] private Transform bossDoor;
        [SerializeField] private Transform colleagueSearch;
        [SerializeField] private Transform meetingApproach;
        [SerializeField] private Transform meetingSeat;

        public string Id => layoutId;
        public bool NeedsMigration(string savedLayoutId) => savedLayoutId != layoutId;

        public void Configure(Transform parkingAnchor, Transform rooftopAnchor, Transform doorAnchor,
            Transform searchAnchor, Transform approachAnchor, Transform seatAnchor)
        {
            parking = parkingAnchor;
            rooftop = rooftopAnchor;
            bossDoor = doorAnchor;
            colleagueSearch = searchAnchor;
            meetingApproach = approachAnchor;
            meetingSeat = seatAnchor;
        }

        public Transform ResumeAnchor(ChapterOneProgress progress)
        {
            switch (progress.Stage)
            {
                case ChapterOneStage.MeetRooftopWorkers:
                case ChapterOneStage.AnswerBossCall: return rooftop;
                case ChapterOneStage.MeetBoss: return bossDoor;
                case ChapterOneStage.FindColleagues:
                case ChapterOneStage.EscortLastColleague: return colleagueSearch;
                case ChapterOneStage.FinalBossBriefing:
                    return progress.PlayerSeated ? meetingSeat : meetingApproach;
                case ChapterOneStage.CollectCarKey: return meetingApproach;
                default: return parking;
            }
        }
    }
}
