using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    [CreateAssetMenu(fileName = "QuestJournalCatalog", menuName = "Saksi Terakhir/Quest Journal Catalog")]
    public sealed class QuestJournalCatalog : ScriptableObject
    {
        [SerializeField] private QuestDefinition[] definitions = Array.Empty<QuestDefinition>();
        public IReadOnlyList<QuestDefinition> Definitions => definitions;
        public static QuestJournalCatalog LoadDefault() => Resources.Load<QuestJournalCatalog>("QuestJournalCatalog");
        public void Configure(QuestDefinition[] quests) => definitions = quests ?? Array.Empty<QuestDefinition>();
    }
}
