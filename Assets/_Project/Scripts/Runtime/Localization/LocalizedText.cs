using TMPro;
using UnityEngine;

namespace SaksiTerakhir.Localization
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private bool useCurrentTextAsKeyIfEmpty;

        [Header("Responsive Text")]
        [SerializeField] private bool applyAutoSize = true;
        [SerializeField] private float minFontSize = 10f;
        [SerializeField] private bool capMaxFontSizeToCurrent = true;

        private TMP_Text targetText;
        private float initialFontSize;

        private void Awake()
        {
            targetText = GetComponent<TMP_Text>();
            initialFontSize = targetText.fontSize;

            if (string.IsNullOrWhiteSpace(key))
            {
                if (useCurrentTextAsKeyIfEmpty)
                {
                    key = targetText.text;
                    Debug.LogWarning(
                        $"LocalizedText on '{gameObject.name}' has no explicit key; using its placeholder text " +
                        $"'{key}' as the key. Identical placeholder text elsewhere will collide with this entry " +
                        "— assign an explicit key to avoid that.",
                        this);
                }
                else
                {
                    Debug.LogError($"LocalizedText on '{gameObject.name}' has no key assigned.", this);
                }
            }
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= Refresh;
        }

        public void Refresh()
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            targetText.text = LocalizationManager.Get(key);
            ApplyResponsiveTextSettings();
        }

        private void ApplyResponsiveTextSettings()
        {
            if (!applyAutoSize)
            {
                return;
            }

            float maxFontSize = initialFontSize > 0f ? initialFontSize : targetText.fontSize;
            targetText.enableAutoSizing = true;
            targetText.fontSizeMin = Mathf.Max(1f, minFontSize);
            if (capMaxFontSizeToCurrent)
            {
                targetText.fontSizeMax = Mathf.Max(targetText.fontSizeMin, maxFontSize);
            }
        }
    }
}
