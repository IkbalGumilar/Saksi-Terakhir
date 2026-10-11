using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    [Serializable]
    public sealed class QuestJournalRecord
    {
        public string id;
        public StoryQuestStatus status;
    }

    [Serializable]
    public sealed class QuestJournalState
    {
        [SerializeField] private string selectedQuestId = string.Empty;
        [SerializeField] private List<QuestJournalRecord> records = new List<QuestJournalRecord>();
        public string SelectedQuestId => selectedQuestId ?? string.Empty;
        public IReadOnlyList<QuestJournalRecord> Records => Data;
        private List<QuestJournalRecord> Data => records ?? (records = new List<QuestJournalRecord>());

        public StoryQuestStatus Status(string id)
        {
            foreach (QuestJournalRecord record in Data)
                if (record != null && record.id == id) return record.status;
            return StoryQuestStatus.NotStarted;
        }

        public bool TrySetStatus(string id, StoryQuestStatus status)
        {
            if (string.IsNullOrWhiteSpace(id) || !Enum.IsDefined(typeof(StoryQuestStatus), status)
                || status == StoryQuestStatus.NotStarted) return false;
            StoryQuestStatus previous = Status(id);
            if (previous == status || previous == StoryQuestStatus.Completed || previous == StoryQuestStatus.Skipped
                || (previous == StoryQuestStatus.NotStarted && status != StoryQuestStatus.Active)) return false;
            QuestJournalRecord target = Data.Find(value => value != null && value.id == id);
            if (target == null) Data.Add(new QuestJournalRecord { id = id, status = status });
            else target.status = status;
            if (status != StoryQuestStatus.Active && SelectedQuestId == id) selectedQuestId = string.Empty;
            return true;
        }

        internal bool Select(string id)
        {
            id = id ?? string.Empty;
            if (SelectedQuestId == id) return false;
            selectedQuestId = id;
            return true;
        }

        public bool Validate(out string error)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (QuestJournalRecord record in Data)
                if (record == null || string.IsNullOrWhiteSpace(record.id) || !seen.Add(record.id)
                    || !Enum.IsDefined(typeof(StoryQuestStatus), record.status)
                    || record.status == StoryQuestStatus.NotStarted)
                { error = "Invalid or duplicate journal quest."; return false; }
            error = string.Empty;
            return true;
        }
    }
}
