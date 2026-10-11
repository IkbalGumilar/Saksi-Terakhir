using System;
using System.Collections.Generic;
using SaksiTerakhir.Npc;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    public sealed class QuestJournalEntry
    {
        public QuestDefinition Definition { get; }
        public StoryQuestStatus Status { get; }
        public bool IsSelected { get; }
        public QuestJournalEntry(QuestDefinition definition, StoryQuestStatus status, bool selected)
        { Definition = definition; Status = status; IsSelected = selected; }
    }

    [DisallowMultipleComponent]
    public sealed class QuestJournalController : MonoBehaviour
    {
        [SerializeField] private ChapterOneDirector chapter;
        [SerializeField] private QuestJournalCatalog catalog;
        private readonly List<QuestJournalEntry> entries = new List<QuestJournalEntry>();
        private readonly Dictionary<string, QuestDefinition> definitions = new Dictionary<string, QuestDefinition>(StringComparer.Ordinal);
        public event Action Changed;
        public IReadOnlyList<QuestJournalEntry> Entries => entries;
        public string SelectedQuestId => chapter != null && chapter.Progress != null ? chapter.Progress.Journal.SelectedQuestId : string.Empty;
        public QuestDefinition SelectedQuest => definitions.TryGetValue(SelectedQuestId, out QuestDefinition quest)
            && Status(quest) == StoryQuestStatus.Active ? quest : null;

        public static QuestJournalController GetOrCreate(ChapterOneDirector director)
        {
            if (director == null) return null;
            QuestJournalController journal = director.GetComponent<QuestJournalController>();
            if (journal == null) journal = director.gameObject.AddComponent<QuestJournalController>();
            if (journal.chapter != director) journal.Configure(director, QuestJournalCatalog.LoadDefault());
            return journal;
        }

        public void Configure(ChapterOneDirector director, QuestJournalCatalog questCatalog)
        {
            if (chapter != null) chapter.ProgressChanged -= OnProgressChanged;
            chapter = director;
            catalog = questCatalog;
            if (isActiveAndEnabled && chapter != null) chapter.ProgressChanged += OnProgressChanged;
            RebuildDefinitions();
            Refresh();
        }

        public bool TrySelect(string id)
        {
            if (chapter == null || chapter.Progress == null) return false;
            if (!string.IsNullOrEmpty(id) && (!definitions.TryGetValue(id, out QuestDefinition quest)
                || quest.Category == QuestCategory.Main || Status(quest) != StoryQuestStatus.Active)) return false;
            if (!chapter.Progress.Journal.Select(id)) return false;
            Refresh();
            return true;
        }

        public bool TryAccept(QuestDefinition quest)
        {
            if (quest == null || chapter == null || chapter.Progress == null || quest.Category == QuestCategory.Main
                || (quest.ProgressSource != QuestProgressSource.Journal
                    && quest.ProgressSource != QuestProgressSource.Breakfast)
                || !quest.MeetsConditions(chapter.Progress)) return false;
            if (quest.ProgressSource == QuestProgressSource.Breakfast
                && chapter.Progress.Story.Breakfast != BreakfastChoice.Unresolved) return false;
            definitions[quest.Id] = quest;
            bool changed = chapter.Progress.Journal.TrySetStatus(quest.Id, StoryQuestStatus.Active);
            if (changed) Refresh();
            return changed;
        }

        public bool TryFinish(string id, bool skipped = false)
        {
            if (chapter == null || chapter.Progress == null || !definitions.TryGetValue(id, out QuestDefinition quest)
                || (quest.ProgressSource != QuestProgressSource.Journal
                    && quest.ProgressSource != QuestProgressSource.Breakfast)
                || quest.Category == QuestCategory.Main) return false;
            if (quest.ProgressSource == QuestProgressSource.Breakfast)
            {
                BreakfastChoice choice = chapter.Progress.Story.Breakfast;
                if (skipped ? choice != BreakfastChoice.Skipped
                    : choice != BreakfastChoice.Paid && choice != BreakfastChoice.Owed) return false;
            }
            bool changed = chapter.Progress.Journal.TrySetStatus(id, skipped ? StoryQuestStatus.Skipped : StoryQuestStatus.Completed);
            if (changed) Refresh();
            return changed;
        }

        public StoryQuestStatus Status(QuestDefinition quest)
        {
            if (quest == null || chapter == null || chapter.Progress == null) return StoryQuestStatus.NotStarted;
            ChapterOneProgress progress = chapter.Progress;
            if (quest.Category == QuestCategory.Main) return chapter.CurrentQuest == quest
                && progress.Stage != ChapterOneStage.Complete ? StoryQuestStatus.Active : StoryQuestStatus.NotStarted;
            switch (quest.ProgressSource)
            {
                case QuestProgressSource.Breakfast:
                    StoryQuestStatus breakfastStatus = progress.Journal.Status(quest.Id);
                    // Breakfast is discovered by visiting the canteen, never by
                    // merely passing the morning chapter checkpoint.
                    if (breakfastStatus == StoryQuestStatus.NotStarted) return breakfastStatus;
                    if (progress.Story.Breakfast == BreakfastChoice.Paid
                        || progress.Story.Breakfast == BreakfastChoice.Owed) return StoryQuestStatus.Completed;
                    if (progress.Story.Breakfast == BreakfastChoice.Skipped) return StoryQuestStatus.Skipped;
                    return breakfastStatus;
                case QuestProgressSource.WrongRoute: return progress.Story.WrongRouteQuest;
                case QuestProgressSource.SharedMeal: return progress.Story.SharedMealQuest;
                case QuestProgressSource.DeskPreparation: return progress.Story.DeskPreparationQuest;
                case QuestProgressSource.Supplies: return progress.Story.SuppliesQuest;
                default: return progress.Journal.Status(quest.Id);
            }
        }

        public void Refresh()
        {
            entries.Clear();
            if (chapter != null && chapter.Progress != null)
            {
                QuestDefinition main = chapter.CurrentQuest;
                if (main != null && chapter.Progress.Stage != ChapterOneStage.Complete)
                    entries.Add(new QuestJournalEntry(main, StoryQuestStatus.Active, false));
                if (!string.IsNullOrEmpty(SelectedQuestId) && SelectedQuest == null)
                    chapter.Progress.Journal.Select(string.Empty);
                foreach (QuestDefinition definition in definitions.Values)
                {
                    StoryQuestStatus status = Status(definition);
                    if (definition != null && definition.Category != QuestCategory.Main && status != StoryQuestStatus.NotStarted)
                        entries.Add(new QuestJournalEntry(definition, status, definition.Id == SelectedQuestId));
                }
                entries.Sort((a, b) => a.Definition.Category == b.Definition.Category
                    ? string.CompareOrdinal(a.Definition.Id, b.Definition.Id)
                    : a.Definition.Category.CompareTo(b.Definition.Category));
            }
            Changed?.Invoke();
        }

        private void RebuildDefinitions()
        {
            definitions.Clear();
            if (catalog != null)
                foreach (QuestDefinition quest in catalog.Definitions) AddDefinition(quest);
            if (chapter != null && chapter.gameObject.scene.IsValid())
                foreach (GameObject root in chapter.gameObject.scene.GetRootGameObjects())
                    foreach (OfficeNpcAgent npc in root.GetComponentsInChildren<OfficeNpcAgent>(true))
                        if (npc.Profile != null)
                            foreach (QuestDefinition quest in npc.Profile.StoryQuests) AddDefinition(quest);
        }
        private void AddDefinition(QuestDefinition quest)
        { if (quest != null && !string.IsNullOrWhiteSpace(quest.Id)) definitions[quest.Id] = quest; }
        private void OnProgressChanged(ChapterOneProgress _) => Refresh();
        private void Awake()
        {
            chapter = GetComponent<ChapterOneDirector>();
            if (catalog == null) catalog = QuestJournalCatalog.LoadDefault();
            RebuildDefinitions();
        }
        private void OnEnable()
        {
            if (chapter != null) chapter.ProgressChanged += OnProgressChanged;
            Refresh();
        }
        private void OnDisable()
        { if (chapter != null) chapter.ProgressChanged -= OnProgressChanged; }
    }
}
