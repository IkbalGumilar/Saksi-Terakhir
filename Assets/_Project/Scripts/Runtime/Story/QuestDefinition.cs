using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    public enum QuestCategory { Main, Side, Optional }

    [CreateAssetMenu(fileName = "QuestDefinition", menuName = "Saksi Terakhir/Quest Definition")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private QuestCategory category = QuestCategory.Main;
        [SerializeField] private string titleKey = string.Empty;
        [SerializeField] private string[] objectiveKeys = Array.Empty<string>();

        public string Id => id;
        public QuestCategory Category => category;
        public string TitleKey => titleKey;
        public IReadOnlyList<string> ObjectiveKeys => objectiveKeys;

        public void Configure(string questId, QuestCategory questCategory, string title,
            string[] objectives)
        {
            id = questId;
            category = questCategory;
            titleKey = title;
            objectiveKeys = objectives ?? Array.Empty<string>();
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
            error = string.Empty;
            return true;
        }
    }
}
