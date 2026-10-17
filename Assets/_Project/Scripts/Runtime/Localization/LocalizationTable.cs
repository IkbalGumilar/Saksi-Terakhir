using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.Localization
{
    public enum GameLanguage
    {
        English,
        Indonesian
    }

    [Serializable]
    public sealed class LocalizedTextEntry
    {
        public string key;
        [TextArea(1, 4)] public string english;
        [TextArea(1, 4)] public string indonesian;
    }

    [CreateAssetMenu(fileName = "LocalizationTable", menuName = "Localization/Localization Table")]
    public sealed class LocalizationTable : ScriptableObject
    {
        public GameLanguage defaultLanguage = GameLanguage.Indonesian;
        public LocalizedTextEntry[] entries = Array.Empty<LocalizedTextEntry>();

        private Dictionary<string, LocalizedTextEntry> lookup;
        private Dictionary<string, string> indonesianToEnglishLookup;
        private Dictionary<string, string> englishToIndonesianLookup;

        public string GetText(string key, GameLanguage language)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            EnsureLookup();
            if (!lookup.TryGetValue(key, out LocalizedTextEntry entry))
            {
                return key;
            }

            string text = language == GameLanguage.Indonesian ? entry.indonesian : entry.english;
            if (string.IsNullOrEmpty(text))
            {
                text = language == GameLanguage.Indonesian ? entry.english : entry.indonesian;
            }

            return string.IsNullOrEmpty(text) ? key : text;
        }

        public string TranslateText(string text, GameLanguage language)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            EnsureLookup();
            if (lookup.TryGetValue(text, out LocalizedTextEntry byKey))
            {
                string resolved = language == GameLanguage.Indonesian ? byKey.indonesian : byKey.english;
                if (string.IsNullOrEmpty(resolved))
                {
                    resolved = language == GameLanguage.Indonesian ? byKey.english : byKey.indonesian;
                }
                return string.IsNullOrEmpty(resolved) ? text : resolved;
            }

            if (language == GameLanguage.English)
            {
                if (indonesianToEnglishLookup.TryGetValue(text, out string english) && !string.IsNullOrEmpty(english))
                {
                    return english;
                }
            }
            else if (language == GameLanguage.Indonesian)
            {
                if (englishToIndonesianLookup.TryGetValue(text, out string indonesian) && !string.IsNullOrEmpty(indonesian))
                {
                    return indonesian;
                }
            }

            return text;
        }

        public void ClearLookupCache()
        {
            lookup = null;
            indonesianToEnglishLookup = null;
            englishToIndonesianLookup = null;
        }

        private void EnsureLookup()
        {
            if (lookup != null && indonesianToEnglishLookup != null && englishToIndonesianLookup != null)
            {
                return;
            }

            lookup = new Dictionary<string, LocalizedTextEntry>(entries.Length);
            indonesianToEnglishLookup = new Dictionary<string, string>(entries.Length);
            englishToIndonesianLookup = new Dictionary<string, string>(entries.Length);
            foreach (LocalizedTextEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                if (lookup.ContainsKey(entry.key))
                {
                    Debug.LogWarning($"Duplicate localization key '{entry.key}' in {name}. The last entry wins.", this);
                }

                lookup[entry.key] = entry;
                if (!string.IsNullOrEmpty(entry.indonesian) && !string.IsNullOrEmpty(entry.english))
                {
                    indonesianToEnglishLookup[entry.indonesian] = entry.english;
                    englishToIndonesianLookup[entry.english] = entry.indonesian;
                }
            }
        }

        private void OnValidate()
        {
            ClearLookupCache();
            WarnAboutInvalidEntries();
        }

        private void WarnAboutInvalidEntries()
        {
            Dictionary<string, int> firstIndexByKey = new Dictionary<string, int>();
            for (int i = 0; i < entries.Length; i++)
            {
                LocalizedTextEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.key))
                {
                    Debug.LogWarning($"Entry {i} in {name} has no key and will be ignored.", this);
                    continue;
                }

                if (firstIndexByKey.TryGetValue(entry.key, out int firstIndex))
                {
                    Debug.LogWarning(
                        $"Duplicate localization key '{entry.key}' in {name} at entries {firstIndex} and {i}.",
                        this);
                }
                else
                {
                    firstIndexByKey.Add(entry.key, i);
                }

                if (string.IsNullOrWhiteSpace(entry.english) && string.IsNullOrWhiteSpace(entry.indonesian))
                {
                    Debug.LogWarning($"Entry '{entry.key}' in {name} has no English or Indonesian text.", this);
                }
            }
        }
    }
}
