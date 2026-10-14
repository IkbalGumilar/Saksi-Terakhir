using System;
using System.Collections.Generic;
using SaksiTerakhir.Story;

namespace SaksiTerakhir.Inventory
{
    public static class InventoryQuestProtection
    {
        public static QuestCategory? CategoryFor(InventoryEntry entry, InventoryState state,
            IEnumerable<QuestJournalEntry> quests)
        {
            if (entry == null || entry.Type != InventoryEntryType.Item || state == null
                || state.IsQuestItemReleased(entry.Id) || state.IsStoryItemRetired(entry.Id)) return null;
            // The office quest ends before arrival in the village. Keep the key
            // required across that chapter boundary until the arrival event releases it.
            QuestCategory? category = CanonicalId(entry.Id) == CarKeyInventory.CarKeyId
                ? QuestCategory.Main : (QuestCategory?)null;
            if (quests == null) return category;
            foreach (QuestJournalEntry quest in quests)
            {
                if (quest == null || quest.Status != StoryQuestStatus.Active || quest.Definition == null) continue;
                foreach (string requiredId in quest.Definition.RequiredItemIds)
                {
                    if (!string.Equals(CanonicalId(requiredId), CanonicalId(entry.Id), StringComparison.Ordinal)) continue;
                    if (!category.HasValue || quest.Definition.Category < category.Value)
                        category = quest.Definition.Category;
                    break;
                }
            }
            return category;
        }

        internal static string CanonicalId(string id) => id == "item.car_key" ? CarKeyInventory.CarKeyId : id;
    }
}
