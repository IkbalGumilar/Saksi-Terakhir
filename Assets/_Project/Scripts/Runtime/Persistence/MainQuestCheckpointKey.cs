using SaksiTerakhir.Story;

namespace SaksiTerakhir.Persistence
{
    public static class MainQuestCheckpointKey
    {
        public static string Create(ChapterOneProgress progress, string questId, bool mainQuest)
        {
            if (progress == null) return string.Empty;
            if (progress.Stage == ChapterOneStage.Complete) return "main:complete";
            if (progress.Stage == ChapterOneStage.AtFuelStation) return "main:at-fuel-station";
            if (!mainQuest || string.IsNullOrEmpty(questId)) return string.Empty;
            string key = questId + ":" + (int)progress.Stage;
            if (progress.Stage == ChapterOneStage.MorningRoutine) key += ":" + progress.EnteredOffice;
            if (progress.Stage == ChapterOneStage.FindColleagues
                || progress.Stage == ChapterOneStage.EscortLastColleague)
                key += ":" + progress.RakaMet + ":" + progress.SintaMet;
            return key;
        }
    }
}
