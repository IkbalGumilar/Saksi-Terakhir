using System.Collections.Generic;
using System.Linq;
using System.Text;
using SaksiTerakhir.Localization;
using UnityEditor;
using UnityEngine;

namespace SaksiTerakhir.EditorTools
{
    public static class LocalizationTableSetup
    {
        private const string TableFolder = "Assets/_Project/Localization";
        private const string TablePath = TableFolder + "/LocalizationTable.asset";

        private static readonly (string key, string english, string indonesian)[] Seeds =
        {
            ("interact.generic", "Interact", "Berinteraksi"),
            ("interact.door.open", "Open Door", "Buka Pintu"),
            ("interact.door.close", "Close Door", "Tutup Pintu"),
            ("interact.door.locked", "Locked", "Terkunci"),
        };

        [MenuItem("Saksi Terakhir/Create Localization Table")]
        public static void CreateTable()
        {
            if (!AssetDatabase.IsValidFolder(TableFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Localization");
            }

            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(TablePath);
            var created = table == null;
            if (created)
            {
                table = ScriptableObject.CreateInstance<LocalizationTable>();
                table.defaultLanguage = GameLanguage.Indonesian;
                AssetDatabase.CreateAsset(table, TablePath);
            }

            var existing = table.entries.Where(entry => entry != null).ToList();
            var known = new HashSet<string>(existing.Select(entry => entry.key));
            var added = new List<string>();
            foreach (var (key, english, indonesian) in Seeds)
            {
                if (known.Contains(key))
                {
                    continue;
                }

                existing.Add(new LocalizedTextEntry
                {
                    key = key,
                    english = english,
                    indonesian = indonesian,
                });
                added.Add(key);
            }

            table.entries = existing.ToArray();
            table.ClearLookupCache();
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("=== LOCALIZATION TABLE ===");
            report.AppendLine($"  asset      {TablePath} ({(created ? "created" : "updated")})");
            report.AppendLine($"  default    {table.defaultLanguage}");
            report.AppendLine($"  entries    {table.entries.Length} total, {added.Count} added");
            foreach (var key in added)
            {
                report.AppendLine($"    + {key}");
            }

            report.AppendLine("=== END LOCALIZATION TABLE ===");
            Debug.Log(report.ToString());
        }
    }
}
