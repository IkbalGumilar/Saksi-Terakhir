using System;
using UnityEngine;

namespace SaksiTerakhir.Localization
{
    public static class LocalizationManager
    {
        private const string LanguagePrefsKey = "Localization.Language";

        public static event Action LanguageChanged;

        private static LocalizationTable table;
        private static GameLanguage currentLanguage = GameLanguage.Indonesian;
        private static bool isInitialized;

        public static GameLanguage CurrentLanguage => currentLanguage;

        public static void Initialize(LocalizationTable localizationTable)
        {
            if (isInitialized && table == localizationTable)
            {
                return;
            }

            table = localizationTable;
            currentLanguage = LoadLanguage();
            isInitialized = true;
            LanguageChanged?.Invoke();
        }

        public static string Get(string key)
        {
            if (!isInitialized || table == null)
            {
                return key;
            }

            return table.GetText(key, currentLanguage);
        }

        public static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text) || !isInitialized || table == null)
            {
                return text ?? string.Empty;
            }

            return table.TranslateText(text, currentLanguage);
        }

        public static void SetLanguage(GameLanguage language)
        {
            if (currentLanguage == language)
            {
                return;
            }

            currentLanguage = language;
            PlayerPrefs.SetString(LanguagePrefsKey, currentLanguage.ToString());
            PlayerPrefs.Save();
            LanguageChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            table = null;
            currentLanguage = GameLanguage.Indonesian;
            isInitialized = false;
            LanguageChanged = null;
        }

        private static GameLanguage LoadLanguage()
        {
            GameLanguage defaultLanguage = table != null ? table.defaultLanguage : GameLanguage.Indonesian;
            string savedValue = PlayerPrefs.GetString(LanguagePrefsKey, defaultLanguage.ToString());
            return Enum.TryParse(savedValue, out GameLanguage parsedLanguage) ? parsedLanguage : defaultLanguage;
        }
    }
}
