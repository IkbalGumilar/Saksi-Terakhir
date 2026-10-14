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
        public static readonly Color OptionalDotColor = new Color(0.3f, 0.8f, 0.4f, 1f);
        public static readonly Color SideDotColor = new Color(0.27f, 0.63f, 1f, 1f);

        [SerializeField] private ChapterOneDirector chapter;
        [SerializeField] private GameObject trackerRoot;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text objectivesLabel;
        [SerializeField] private Image categoryDot;
        private QuestJournalController journal;
        private GameObject supportingRoot;
        private TMP_Text supportingTitle;
        private TMP_Text supportingObjectives;
        private Image supportingDot;

        public void Configure(ChapterOneDirector director, GameObject root, TMP_Text title,
            TMP_Text objectives, Image dot)
        {
            chapter = director;
            trackerRoot = root;
            titleLabel = title;
            objectivesLabel = objectives;
            categoryDot = dot;
            BindJournal();
        }

        public void Render(QuestDefinition quest, ChapterOneProgress progress)
        {
            if (trackerRoot == null) return;
            var settings = SaksiTerakhir.Settings.GameSettingsManager.Current;
            bool showMain = settings == null || settings.showMainQuest;
            bool visible = showMain && quest != null && progress != null
                && progress.Stage != ChapterOneStage.Complete;
            trackerRoot.SetActive(visible);
            RenderSupporting();
            if (!visible) return;

            if (categoryDot != null) categoryDot.color = DotColor(quest.Category);
            if (titleLabel != null) titleLabel.text = LocalizationManager.Get(quest.TitleKey);
            if (objectivesLabel == null) return;

            if (progress.Stage == ChapterOneStage.MorningRoutine)
            {
                objectivesLabel.text = Row("quest.main.morning.enter", progress.EnteredOffice)
                    + "\n" + Row("quest.main.morning.desk", false);
                return;
            }

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
            BindJournal();
            LocalizationManager.LanguageChanged += Refresh;
            SaksiTerakhir.Settings.GameSettingsManager.SettingsChanged += OnSettingsChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (chapter != null) chapter.ProgressChanged -= OnProgressChanged;
            if (journal != null) journal.Changed -= Refresh;
            LocalizationManager.LanguageChanged -= Refresh;
            SaksiTerakhir.Settings.GameSettingsManager.SettingsChanged -= OnSettingsChanged;
        }

        private void OnSettingsChanged(SaksiTerakhir.Settings.GameSettingsData _) => Refresh();

        private void OnProgressChanged(ChapterOneProgress _) => Refresh();
        private void Refresh()
        {
            if (chapter != null) Render(chapter.CurrentQuest, chapter.Progress);
        }

        public static Color DotColor(QuestCategory category) => category == QuestCategory.Main
            ? MainDotColor : category == QuestCategory.Optional ? OptionalDotColor : SideDotColor;

        private void BindJournal()
        {
            if (journal != null) journal.Changed -= Refresh;
            journal = QuestJournalController.GetOrCreate(chapter);
            if (journal != null && isActiveAndEnabled) journal.Changed += Refresh;
        }

        private void RenderSupporting()
        {
            var settings = SaksiTerakhir.Settings.GameSettingsManager.Current;
            bool showMain = settings == null || settings.showMainQuest;
            bool showTracked = settings == null || settings.showTrackedQuest;
            if (!showMain || !showTracked)
            {
                if (supportingRoot != null) supportingRoot.SetActive(false);
                return;
            }

            QuestDefinition quest = journal != null ? journal.SelectedQuest : null;
            if (quest == null)
            {
                if (supportingRoot != null) supportingRoot.SetActive(false);
                return;
            }
            if (supportingRoot == null)
            {
                supportingRoot = Instantiate(trackerRoot, trackerRoot.transform.parent, false);
                supportingRoot.name = "Selected Supporting Quest";
                supportingTitle = FindMatching<TMP_Text>(supportingRoot, titleLabel);
                supportingObjectives = FindMatching<TMP_Text>(supportingRoot, objectivesLabel);
                supportingDot = FindMatching<Image>(supportingRoot, categoryDot);
            }
            RectTransform mainRect = trackerRoot.transform as RectTransform;
            RectTransform supportRect = supportingRoot.transform as RectTransform;
            if (mainRect != null && supportRect != null)
                supportRect.anchoredPosition = mainRect.anchoredPosition
                    + Vector2.down * (mainRect.rect.height + 8f);
            if (supportingTitle != null) supportingTitle.text = LocalizationManager.Get(quest.TitleKey);
            if (supportingDot != null) supportingDot.color = DotColor(quest.Category);
            if (supportingObjectives != null)
            {
                string text = string.Empty;
                foreach (string key in quest.ObjectiveKeys)
                    text += (text.Length > 0 ? "\n" : string.Empty) + "• " + LocalizationManager.Get(key);
                supportingObjectives.text = text;
            }
            supportingRoot.SetActive(true);
        }

        private static T FindMatching<T>(GameObject root, T original) where T : Component
        {
            if (original == null) return null;
            foreach (T component in root.GetComponentsInChildren<T>(true))
                if (component.name == original.name) return component;
            return null;
        }

        private void OnDestroy()
        {
            if (supportingRoot == null) return;
            supportingRoot.SetActive(false);
            if (Application.isPlaying) Destroy(supportingRoot);
            else DestroyImmediate(supportingRoot);
        }
    }
}
