using System;
using System.Collections.Generic;
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
        [SerializeField] private GameObject autosaveRoot;
        [SerializeField] private TMP_Text autosaveLabel;
        [SerializeField, Min(0.1f)] private float autosaveSeconds = 3f;
        [SerializeField, Range(1, 4)] private int autosaveBlinks = 2;
        [SerializeField, Min(0.01f)] private float animationSeconds = 0.24f;
        [SerializeField, Min(0f)] private float slideDistance = 24f;

        private const string AutosaveSuccessKey = "story.autosave.saved";
        private const string AutosaveFailureKey = "story.autosave.failed";
        private readonly Queue<Notice> pending = new Queue<Notice>();
        private ChapterOneDirector subscribedChapter;
        private Surface notificationSurface;
        private Surface autosaveSurface;
        private Surface currentSurface;
        private TMP_Text currentLabel;
        private Notice current;
        private Phase phase;
        private float phaseSeconds;
        private float holdSeconds;
        private bool showingSaveResult;
        private bool showingSaveIcon;
        private bool advancing;
        private int queueGeneration;
        private string currentKey = string.Empty;
        private readonly System.Collections.Generic.Dictionary<UnityEngine.UI.Graphic, bool> hiddenSaveGraphics
            = new System.Collections.Generic.Dictionary<UnityEngine.UI.Graphic, bool>();

        public bool IsBusy => current != null || pending.Count > 0;
        public bool CheckpointsManagedExternally { get; set; }

        public void Configure(ChapterOneDirector director, GameObject root, TMP_Text label)
        {
            RectTransform rect = root != null ? root.GetComponent<RectTransform>() : null;
            Vector2 position = currentSurface != null && currentSurface.Root == root
                ? currentSurface.Position : rect != null ? rect.anchoredPosition : Vector2.zero;
            ClearQueue();
            if (rect != null) rect.anchoredPosition = position;
            chapter = director;
            notificationRoot = root;
            notificationLabel = label;
            notificationSurface = null;
            autosaveSurface = null;
            ResolveSurfaces();
            HideSurfaces();
            SubscribeChapter();
        }

        public void ConfigureAutosave(GameObject root, TMP_Text label = null)
        {
            RectTransform rect = root != null ? root.GetComponent<RectTransform>() : null;
            Vector2 position = currentSurface != null && currentSurface.Root == root
                ? currentSurface.Position : rect != null ? rect.anchoredPosition : Vector2.zero;
            ClearQueue();
            if (rect != null) rect.anchoredPosition = position;
            autosaveRoot = root;
            autosaveLabel = label;
            autosaveSurface = null;
            ResolveSurfaces();
            HideSurfaces();
        }

        public void Show(string textKey, float seconds)
        {
            if (string.IsNullOrWhiteSpace(textKey)) return;
            pending.Enqueue(new Notice(textKey, Mathf.Max(0.1f, seconds), null, false));
            if (isActiveAndEnabled) AdvanceQueue(0f);
        }

        /// <summary>Finishes the quest toast before saving, then displays the save result.</summary>
        public void EnqueueCheckpoint(string questKey, Func<bool> saveAction)
        {
            if (saveAction == null) throw new ArgumentNullException(nameof(saveAction));
            pending.Enqueue(new Notice(questKey, defaultSeconds, saveAction, true));
            if (isActiveAndEnabled) AdvanceQueue(0f);
        }

        /// <summary>Queues a save and its icon without inventing a quest notification.</summary>
        public void EnqueueSave(Func<bool> saveAction)
        {
            if (saveAction == null) throw new ArgumentNullException(nameof(saveAction));
            pending.Enqueue(new Notice(null, 0f, saveAction, true));
            if (isActiveAndEnabled) AdvanceQueue(0f);
        }

        public void ClearQueue()
        {
            queueGeneration++;
            pending.Clear();
            current = null;
            currentSurface = null;
            currentLabel = null;
            currentKey = string.Empty;
            phase = Phase.None;
            phaseSeconds = 0f;
            showingSaveResult = false;
            showingSaveIcon = false;
            HideSurfaces();
        }

        private void OnEnable()
        {
            ResolveSurfaces();
            HideSurfaces();
            SubscribeChapter();
            LocalizationManager.LanguageChanged += OnLanguageChanged;
            if (pending.Count > 0) AdvanceQueue(0f);
        }

        private void OnDisable()
        {
            if (subscribedChapter != null) subscribedChapter.NoticeRequested -= OnNotice;
            subscribedChapter = null;
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
            ClearQueue();
        }

        private void Update() => AdvanceQueue(Time.unscaledDeltaTime);

        private void SubscribeChapter()
        {
            if (subscribedChapter != null) subscribedChapter.NoticeRequested -= OnNotice;
            subscribedChapter = isActiveAndEnabled ? chapter : null;
            if (subscribedChapter != null) subscribedChapter.NoticeRequested += OnNotice;
        }

        private void OnNotice(string key)
        {
            if (CheckpointsManagedExternally
                && (key == "story.quest.started" || key == "story.quest.updated")) return;
            Show(key, defaultSeconds);
        }

        private void AdvanceQueue(float seconds)
        {
            if (advancing) return;
            advancing = true;
            try
            {
                float remaining = Mathf.Max(0f, seconds);
                // Bound callback reentrancy and missing-UI requests without dropping queued work.
                for (int transition = 0; transition < 128; transition++)
                {
                    if (current == null)
                    {
                        if (pending.Count == 0) break;
                        current = pending.Dequeue();
                        showingSaveResult = false;
                        if (current.IsCheckpoint && string.IsNullOrEmpty(current.Key))
                        {
                            CompleteToast();
                            continue;
                        }
                        BeginToast(current.Key, current.Seconds, false);
                    }

                    if (currentSurface == null)
                    {
                        CompleteToast();
                        continue;
                    }

                    float duration = phase == Phase.Hold ? holdSeconds
                        : Mathf.Max(0.01f, animationSeconds);
                    float step = Mathf.Min(remaining, Mathf.Max(0f, duration - phaseSeconds));
                    phaseSeconds += step;
                    remaining -= step;
                    float progress = Mathf.Clamp01(phaseSeconds / duration);
                    float visible = phase == Phase.Enter ? progress
                        : phase == Phase.Exit ? 1f - progress : 1f;
                    if (showingSaveIcon && phase == Phase.Hold)
                        visible = 0.3f + 0.7f * (0.5f + 0.5f
                            * Mathf.Cos(progress * Mathf.PI * 2f * Mathf.Max(1, autosaveBlinks)));
                    SetAppearance(visible);
                    if (phaseSeconds < duration) break;

                    phaseSeconds = 0f;
                    if (phase == Phase.Enter) phase = Phase.Hold;
                    else if (phase == Phase.Hold) phase = Phase.Exit;
                    else CompleteToast();
                }
            }
            finally { advancing = false; }
        }

        private void BeginToast(string key, float seconds, bool saved)
        {
            ResolveSurfaces();
            HideSurfaces();
            showingSaveIcon = saved;
            bool useAutosave = showingSaveIcon && autosaveRoot != null;
            // A successful save has icon feedback only. Missing UI must still
            // allow the save callback and subsequent notifications to finish.
            currentSurface = showingSaveIcon ? autosaveSurface : notificationSurface;
            currentLabel = showingSaveIcon ? null : notificationLabel;
            currentKey = key ?? string.Empty;
            holdSeconds = Mathf.Max(0.1f, seconds);
            phase = Phase.Enter;
            phaseSeconds = 0f;

            if (autosaveRoot != null) SetContentActive(autosaveRoot, useAutosave);
            if (notificationLabel != null && notificationLabel != autosaveLabel)
                SetContentActive(notificationLabel.gameObject, !showingSaveIcon);
            if (useAutosave && autosaveLabel != null)
            {
                autosaveLabel.enabled = false;
                if (autosaveLabel.gameObject != autosaveRoot)
                    SetContentActive(autosaveLabel.gameObject, false);
            }
            if (currentSurface == null) return;
            if (useAutosave && notificationRoot != null
                && autosaveRoot.transform.IsChildOf(notificationRoot.transform))
            {
                SetContentActive(notificationRoot, true);
                if (notificationSurface != null) notificationSurface.Group.alpha = 1f;
                // A logo may live inside the ordinary toast panel. Keep that panel's
                // transform, while hiding its background for icon-only feedback.
                for (Transform ancestor = autosaveRoot.transform.parent; ancestor != null;
                    ancestor = ancestor.parent)
                {
                    foreach (UnityEngine.UI.Graphic graphic in ancestor.GetComponents<UnityEngine.UI.Graphic>())
                    {
                        hiddenSaveGraphics[graphic] = graphic.enabled;
                        graphic.enabled = false;
                    }
                    if (ancestor == notificationRoot.transform) break;
                }
            }
            SetContentActive(currentSurface.Root, true);
            if (useAutosave) SetContentActive(autosaveRoot, true);
            if (currentLabel != null)
            {
                SetContentActive(currentLabel.gameObject, true);
                currentLabel.text = LocalizedNotice(currentKey);
            }
            SetAppearance(0f);
        }

        private void CompleteToast()
        {
            HideSurfaces();
            currentSurface = null;
            currentLabel = null;
            phase = Phase.None;
            if (current == null) return;
            if (!current.IsCheckpoint || showingSaveResult)
            {
                current = null;
                return;
            }

            showingSaveResult = true;
            int generation = queueGeneration;
            bool saved;
            try { saved = current.SaveAction(); }
            catch (Exception exception)
            {
                Debug.LogWarning("Checkpoint save failed (" + exception.GetType().Name + ").", this);
                saved = false;
            }
            // A new/load/menu action may cancel this pair from inside the save callback.
            if (generation != queueGeneration || current == null) return;
            BeginToast(saved ? AutosaveSuccessKey : AutosaveFailureKey,
                saved ? autosaveSeconds : defaultSeconds, saved);
        }

        private void ResolveSurfaces()
        {
            if (notificationRoot != null && (notificationSurface == null
                || notificationSurface.Root != notificationRoot))
                notificationSurface = new Surface(notificationRoot);
            if (autosaveRoot == null)
            {
                Transform scope = notificationRoot != null ? notificationRoot.transform : transform.parent;
                while (scope != null && autosaveRoot == null)
                {
                    foreach (Transform candidate in scope.GetComponentsInChildren<Transform>(true))
                    {
                        string name = candidate.name.Replace(" ", string.Empty)
                            .Replace("_", string.Empty).Replace("-", string.Empty);
                        if (!string.Equals(name, "AutoSave", StringComparison.OrdinalIgnoreCase)) continue;
                        autosaveRoot = candidate.gameObject;
                        break;
                    }
                    if (scope.GetComponent<Canvas>() != null) break;
                    scope = scope.parent;
                }
            }
            if (autosaveRoot == null && notificationRoot != null)
            {
                // The authored logo currently uses the generic object name "Icon".
                // Its sprite identifies it without renaming or saving the scene.
                foreach (UnityEngine.UI.Image image in notificationRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    if (image.sprite == null || image.gameObject == notificationRoot) continue;
                    string name = image.sprite.name.Replace(" ", string.Empty).Replace("_", string.Empty)
                        .Replace("-", string.Empty);
                    if (name.StartsWith("autosave", StringComparison.OrdinalIgnoreCase))
                    { autosaveRoot = image.gameObject; break; }
                }
            }
            if (autosaveRoot == null) return;
            if (autosaveLabel == null) autosaveLabel = autosaveRoot.GetComponentInChildren<TMP_Text>(true);
            if (autosaveSurface == null || autosaveSurface.Root != autosaveRoot)
                autosaveSurface = new Surface(autosaveRoot);
        }

        private void HideSurfaces()
        {
            HideSurface(notificationSurface);
            HideSurface(autosaveSurface);
            foreach (var pair in hiddenSaveGraphics)
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            hiddenSaveGraphics.Clear();
        }

        private void HideSurface(Surface surface)
        {
            if (surface == null || surface.Root == null) return;
            surface.Group.alpha = 0f;
            if (surface.Rect != null) surface.Rect.anchoredPosition = surface.Position;
            SetContentActive(surface.Root, false);
        }

        private void SetContentActive(GameObject target, bool active)
        {
            if (target == null) return;
            // Keep the component's owner running when callers use the toast as its parent.
            if (!active && transform.IsChildOf(target.transform)) return;
            target.SetActive(active);
        }

        private void SetAppearance(float visible)
        {
            if (currentSurface == null || currentSurface.Root == null) return;
            float eased = visible * visible * (3f - 2f * visible);
            currentSurface.Group.alpha = eased;
            if (currentSurface.Rect != null)
                currentSurface.Rect.anchoredPosition = currentSurface.Position
                    + (showingSaveIcon ? Vector2.zero : Vector2.up * (slideDistance * (1f - eased)));
        }

        private void OnLanguageChanged()
        {
            if (currentLabel != null) currentLabel.text = LocalizedNotice(currentKey);
        }

        private static string LocalizedNotice(string key)
        {
            string value = LocalizationManager.Get(key);
            if (!string.IsNullOrEmpty(value) && value != key) return value;
            bool indonesian = LocalizationManager.CurrentLanguage == GameLanguage.Indonesian;
            if (key == AutosaveSuccessKey) return indonesian ? "Tersimpan otomatis" : "Auto Saved";
            if (key == AutosaveFailureKey) return indonesian
                ? "Penyimpanan otomatis gagal. Silakan coba lagi." : "Auto save failed. Please try again.";
            return value;
        }

        private enum Phase { None, Enter, Hold, Exit }

        private sealed class Notice
        {
            public readonly string Key;
            public readonly float Seconds;
            public readonly Func<bool> SaveAction;
            public readonly bool IsCheckpoint;

            public Notice(string key, float seconds, Func<bool> saveAction, bool isCheckpoint)
            {
                Key = key;
                Seconds = seconds;
                SaveAction = saveAction;
                IsCheckpoint = isCheckpoint;
            }
        }

        private sealed class Surface
        {
            public readonly GameObject Root;
            public readonly CanvasGroup Group;
            public readonly RectTransform Rect;
            public readonly Vector2 Position;

            public Surface(GameObject root)
            {
                Root = root;
                Rect = root.GetComponent<RectTransform>();
                Position = Rect != null ? Rect.anchoredPosition : Vector2.zero;
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                if (group == null) group = root.AddComponent<CanvasGroup>();
                Group = group;
                Group.interactable = false;
                Group.blocksRaycasts = false;
            }
        }
    }
}
