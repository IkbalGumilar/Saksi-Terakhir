using System;
using System.Collections.Generic;
using SaksiTerakhir.Inventory;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    public enum QuestCategory { Main, Side, Optional }
    public enum QuestProgressSource { Journal, WrongRoute, SharedMeal, DeskPreparation, Supplies, Breakfast }

    [CreateAssetMenu(fileName = "QuestDefinition", menuName = "Saksi Terakhir/Quest Definition")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private QuestCategory category = QuestCategory.Main;
        [SerializeField] private string titleKey = string.Empty;
        [SerializeField] private string[] objectiveKeys = Array.Empty<string>();
        [SerializeField] private ChapterOneStoryCondition[] conditions = Array.Empty<ChapterOneStoryCondition>();
        [SerializeField] private QuestProgressSource progressSource;

        public string Id => id;
        public QuestCategory Category => category;
        public string TitleKey => titleKey;
        public IReadOnlyList<string> ObjectiveKeys => objectiveKeys;
        public IReadOnlyList<ChapterOneStoryCondition> Conditions => conditions;
        public QuestProgressSource ProgressSource => progressSource;
        public void BindProgress(QuestProgressSource source) => progressSource = source;

        public bool MeetsConditions(ChapterOneProgress progress, InventoryState inventory = null)
            => ChapterOneConditionEvaluator.AllMet(progress, conditions, inventory);

        public void Configure(string questId, QuestCategory questCategory, string title,
            string[] objectives, ChapterOneStoryCondition[] requirements = null)
        {
            id = questId;
            category = questCategory;
            titleKey = title;
            objectiveKeys = objectives ?? Array.Empty<string>();
            conditions = requirements ?? Array.Empty<ChapterOneStoryCondition>();
        }

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(titleKey))
            {
                error = "Quest ID and title key are required.";
                return false;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in objectiveKeys)
            {
                if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
                {
                    error = $"Quest {id} has an empty or duplicate objective key.";
                    return false;
                }
            }
            if (conditions != null)
            {
                foreach (ChapterOneStoryCondition condition in conditions)
                {
                    if (condition == null || !condition.Validate(out _))
                    {
                        error = $"Quest {id} has an invalid story condition.";
                        return false;
                    }
                }
            }
            error = string.Empty;
            return true;
        }
    }
}
