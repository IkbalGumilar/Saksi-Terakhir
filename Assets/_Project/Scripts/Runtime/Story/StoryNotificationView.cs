using SaksiTerakhir.Localization;
using TMPro;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    [DisallowMultipleComponent]
    public sealed class StoryNotificationView : MonoBehaviour
    {
        [SerializeField] private ChapterOneDirector chapter;
        [SerializeField] private GameObject notificationRoot;
        [SerializeField] private TMP_Text notificationLabel;
        [SerializeField, Min(1f)] private float defaultSeconds = 4f;

        private float hideAt;
        private string currentKey = string.Empty;

        public void Configure(ChapterOneDirector director, GameObject root, TMP_Text label)
        {
            chapter = director;
            notificationRoot = root;
            notificationLabel = label;
            if (notificationRoot != null) notificationRoot.SetActive(false);
        }

        public void Show(string textKey, float seconds)
        {
            if (notificationRoot == null || notificationLabel == null) return;
            currentKey = textKey;
            notificationLabel.text = LocalizationManager.Get(textKey);
            notificationRoot.SetActive(true);
            hideAt = Time.time + Mathf.Max(1f, seconds);
        }

        private void OnEnable()
        {
            if (chapter != null) chapter.NoticeRequested += OnNotice;
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            if (chapter != null) chapter.NoticeRequested -= OnNotice;
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
        }

        private void Update()
        {
            if (notificationRoot != null && notificationRoot.activeSelf && Time.time >= hideAt)
                notificationRoot.SetActive(false);
        }

        private void OnNotice(string key) => Show(key, defaultSeconds);
        private void OnLanguageChanged()
        {
            if (notificationRoot != null && notificationRoot.activeSelf && notificationLabel != null)
                notificationLabel.text = LocalizationManager.Get(currentKey);
        }
    }
}
