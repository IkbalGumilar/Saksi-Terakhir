using SaksiTerakhir.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SaksiTerakhir.Story
{
    [DisallowMultipleComponent]
    public sealed class QuestTrackerView : MonoBehaviour
    {
        public static readonly Color MainDotColor = new Color(1f, 0.82f, 0.12f, 1f);

        [SerializeField] private ChapterOneDirector chapter;
        [SerializeField] private GameObject trackerRoot;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text objectivesLabel;
        [SerializeField] private Image categoryDot;

        public void Configure(ChapterOneDirector director, GameObject root, TMP_Text title,
            TMP_Text objectives, Image dot)
        {
            chapter = director;
            trackerRoot = root;
            titleLabel = title;
            objectivesLabel = objectives;
            categoryDot = dot;
        }

        public void Render(QuestDefinition quest, ChapterOneProgress progress)
        {
            if (trackerRoot == null) return;
            bool visible = quest != null && progress != null
                && progress.Stage != ChapterOneStage.Complete;
            trackerRoot.SetActive(visible);
            if (!visible) return;

            if (categoryDot != null) categoryDot.color = quest.Category == QuestCategory.Main
                ? MainDotColor : quest.Category == QuestCategory.Side
                    ? new Color(0.27f, 0.63f, 1f, 1f)
                    : new Color(0.3f, 0.8f, 0.4f, 1f);
            if (titleLabel != null) titleLabel.text = LocalizationManager.Get(quest.TitleKey);
            if (objectivesLabel == null) return;

            if (progress.Stage == ChapterOneStage.FindColleagues
                || progress.Stage == ChapterOneStage.EscortLastColleague)
            {
                objectivesLabel.text = Row("quest.main.find.raka", progress.RakaMet)
                    + "\n" + Row("quest.main.find.sinta", progress.SintaMet);
                return;
            }

            string text = string.Empty;
            for (int index = 0; index < quest.ObjectiveKeys.Count; index++)
            {
                if (index > 0) text += "\n";
                text += "• " + LocalizationManager.Get(quest.ObjectiveKeys[index]);
            }
            objectivesLabel.text = text;
        }

        private static string Row(string key, bool complete)
        {
            string text = LocalizationManager.Get(key);
            return complete ? "• <color=#9AA0A8><s>" + text + "</s></color>"
                : "• " + text;
        }

        private void OnEnable()
        {
            if (chapter != null) chapter.ProgressChanged += OnProgressChanged;
            LocalizationManager.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (chapter != null) chapter.ProgressChanged -= OnProgressChanged;
            LocalizationManager.LanguageChanged -= Refresh;
        }

        private void OnProgressChanged(ChapterOneProgress _) => Refresh();
        private void Refresh()
        {
            if (chapter != null) Render(chapter.CurrentQuest, chapter.Progress);
        }
    }
}
