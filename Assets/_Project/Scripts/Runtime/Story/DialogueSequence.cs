using System;
using System.Collections.Generic;
using System.Globalization;
using SaksiTerakhir.Inventory;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    [Serializable]
    public sealed class DialogueLine
    {
        [SerializeField] private string speakerId = string.Empty;
        [SerializeField, TextArea(2, 4)] private string text = string.Empty;

        public string SpeakerId => speakerId;
        public string Text => text;
        public string LocalizedText => SaksiTerakhir.Localization.LocalizationManager.Translate(text);

        public DialogueLine(string id, string content)
        {
            speakerId = id;
            text = content;
        }
    }

    [CreateAssetMenu(fileName = "DialogueSequence", menuName = "Saksi Terakhir/Dialogue Sequence")]
    public sealed class DialogueSequence : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private DialogueLine[] lines = Array.Empty<DialogueLine>();
        [SerializeField] private ChapterOneStoryCondition[] conditions = Array.Empty<ChapterOneStoryCondition>();
        [SerializeField] private bool phoneConversation;

        public string Id => id;
        public IReadOnlyList<DialogueLine> Lines => lines;
        public IReadOnlyList<ChapterOneStoryCondition> Conditions => conditions;
        public bool IsPhoneConversation => phoneConversation
            || string.Equals(id, "chapter.boss_call", StringComparison.Ordinal);

        // Identifies the chosen logical branch without using transient Unity
        // instance IDs. Text edits and AND-requirement reordering keep this key.
        public string SelectionKey
        {
            get
            {
                var parts = new List<string>();
                if (conditions != null)
                {
                    foreach (ChapterOneStoryCondition requirement in conditions)
                    {
                        if (requirement == null) return string.Empty;
                        bool inventoryCondition = requirement.Condition == ChapterOneConditionId.InventoryContainsEntry;
                        string entryId = inventoryCondition ? requirement.InventoryEntryId : string.Empty;
                        parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}:{3}:{4}",
                            (int)requirement.Condition, requirement.ExpectedValue ? 1 : 0,
                            inventoryCondition ? (int)requirement.InventoryEntryType : 0, entryId.Length, entryId));
                    }
                }
                parts.Sort(StringComparer.Ordinal);
                return id.Length.ToString(CultureInfo.InvariantCulture) + ":" + id + "|"
                    + string.Join("|", parts);
            }
        }

        public bool MeetsConditions(ChapterOneProgress progress, InventoryState inventory = null)
            => ChapterOneConditionEvaluator.AllMet(progress, conditions, inventory);

        public void Configure(string sequenceId, DialogueLine[] dialogueLines,
            ChapterOneStoryCondition[] requirements = null, bool isPhoneConversation = false)
        {
            id = sequenceId;
            lines = dialogueLines ?? Array.Empty<DialogueLine>();
            conditions = requirements ?? Array.Empty<ChapterOneStoryCondition>();
            phoneConversation = isPhoneConversation;
        }

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(id) || lines.Length == 0)
            {
                error = "Dialogue ID and at least one line are required.";
                return false;
            }
            foreach (DialogueLine line in lines)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.SpeakerId)
                    || string.IsNullOrWhiteSpace(line.Text))
                {
                    error = $"Dialogue {id} has an incomplete line.";
                    return false;
                }
            }
            if (conditions != null)
            {
                foreach (ChapterOneStoryCondition condition in conditions)
                {
                    if (condition == null || !condition.Validate(out _))
                    {
                        error = $"Dialogue {id} has an invalid story condition.";
                        return false;
                    }
                }
            }
            error = string.Empty;
            return true;
        }
    }
}
