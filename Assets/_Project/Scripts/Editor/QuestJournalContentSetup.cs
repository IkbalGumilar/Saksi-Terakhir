using System.Collections.Generic;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Story;
using UnityEditor;
using UnityEngine;

namespace SaksiTerakhir.EditorTools
{
    public static class QuestJournalContentSetup
    {
        private const string QuestFolder = "Assets/_Project/Story/Quests";
        [MenuItem("Saksi Terakhir/Ensure Quest Journal Content")]
        public static void EnsureFromMenu() => Debug.Log(Ensure());

        public static string Ensure()
        {
            var quests = new[] {
                Quest("wrong-route", "side.chapter1.wrong_route", QuestCategory.Side, QuestProgressSource.WrongRoute,
                    "quest.side.wrong_route", "quest.side.wrong_route.talk", "Wrong Route", "Salah Jalur",
                    "Speak with the digitization team and return to your route.", "Bicara dengan tim digitalisasi lalu kembali ke jalurmu."),
                Quest("shared-meal", "optional.chapter1.shared_meal", QuestCategory.Optional, QuestProgressSource.SharedMeal,
                    "quest.optional.shared_meal", "quest.optional.shared_meal.talk", "Have a Meal Together", "Makan Bersama Rekan",
                    "Have a meal with Raka and Sinta before collecting the car key.", "Makan bersama Raka dan Sinta sebelum mengambil kunci mobil."),
                Quest("desk-preparation", "optional.chapter1.desk_preparation", QuestCategory.Optional, QuestProgressSource.DeskPreparation,
                    "quest.optional.desk_preparation", "quest.optional.desk_preparation.finish", "Prepare Your Desk", "Rapikan Meja Kerja",
                    "Finish the preparations at your desk before departure.", "Selesaikan persiapan di meja kerja sebelum berangkat."),
                Quest("supplies", "optional.chapter1.supplies", QuestCategory.Optional, QuestProgressSource.Supplies,
                    "quest.optional.supplies", "quest.optional.supplies.buy", "Travel Supplies", "Perbekalan Perjalanan",
                    "Buy the supplies you need at the convenience store.", "Beli perbekalan yang dibutuhkan di toserba.")
            };
            const string path = "Assets/_Project/Resources/QuestJournalCatalog.asset";
            QuestJournalCatalog catalog = AssetDatabase.LoadAssetAtPath<QuestJournalCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<QuestJournalCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }
            catalog.Configure(quests);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return "Quest journal catalog: four existing chapter-one quest-state bindings; authored scene unchanged.";
        }

        private static QuestDefinition Quest(string file, string id, QuestCategory category, QuestProgressSource source,
            string titleKey, string objectiveKey, string englishTitle, string indonesianTitle, string englishObjective, string indonesianObjective)
        {
            string path = QuestFolder + "/" + file + ".asset";
            QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (quest == null)
            {
                quest = ScriptableObject.CreateInstance<QuestDefinition>();
                quest.Configure(id, category, titleKey, new[] { objectiveKey });
                quest.BindProgress(source);
                AssetDatabase.CreateAsset(quest, path);
            }
            AddText(titleKey, englishTitle, indonesianTitle);
            AddText(objectiveKey, englishObjective, indonesianObjective);
            return quest;
        }

        private static void AddText(string key, string english, string indonesian)
        {
            LocalizationTable table = AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/_Project/Localization/LocalizationTable.asset");
            if (table == null) return;
            var entries = new List<LocalizedTextEntry>(table.entries);
            if (entries.Exists(entry => entry != null && entry.key == key)) return;
            entries.Add(new LocalizedTextEntry { key = key, english = english, indonesian = indonesian });
            table.entries = entries.ToArray();
            table.ClearLookupCache();
            EditorUtility.SetDirty(table);
        }
    }
}
