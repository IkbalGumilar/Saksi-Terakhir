using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using SaksiTerakhir.Persistence;

namespace SaksiTerakhir.UI
{
    /// <summary>Presents the authored menu and connects its runtime session actions.</summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class MainMenuPresentation : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);
        [SerializeField] private UnityEngine.UI.Button[] options;
        [SerializeField] private RectTransform selectionUnderline;
        [SerializeField] private TMP_Text description;
        [SerializeField] private string[] descriptions;
        [SerializeField] private Color normalColor = new Color32(189, 185, 177, 255);
        [SerializeField] private Color selectedColor = new Color32(241, 238, 229, 255);

        private RectTransform frame;
        private int selectedIndex = -1;
        private bool initializeSelection;
        private bool cursorCaptured;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private CanvasGroup fadeGroup;
        private bool fading;
        private float previousAlpha;
        private bool previousInteractable;
        private bool previousBlocksRaycasts;

        public UnityEngine.UI.Button[] Options => options;
        public GameObject MenuRoot
        {
            get
            {
                for (Transform node = transform; node != null; node = node.parent)
                    if (node.name == "Main Menu Panel") return node.gameObject;
                return gameObject;
            }
        }

        private void Awake()
        {
            if (Application.IsPlaying(gameObject)) GameSessionController.Bootstrap(this);
        }

        public void SetSaveAvailable(bool available)
        {
            if (options == null) return;
            if (options.Length > 0 && options[0] != null) options[0].gameObject.SetActive(available);
            if (options.Length > 2 && options[2] != null)
            {
                options[2].gameObject.SetActive(available);
                options[2].interactable = available;
            }
            RefreshRuntimeSelection();
        }

        public void ShowStatus(string text)
        {
            if (description != null) description.text = text;
        }

        public void SetContinueDescription(string text)
        {
            if (descriptions != null && descriptions.Length > 0) descriptions[0] = text;
        }

        public IEnumerator FadeOut(float seconds)
        {
            CancelFade();
            fadeGroup = MenuRoot.GetComponent<CanvasGroup>();
            if (fadeGroup == null) fadeGroup = MenuRoot.AddComponent<CanvasGroup>();
            previousAlpha = fadeGroup.alpha;
            previousInteractable = fadeGroup.interactable;
            previousBlocksRaycasts = fadeGroup.blocksRaycasts;
            fading = true;
            fadeGroup.interactable = false;
            // Keep the fading menu in front of other UI until the handoff is complete.
            fadeGroup.blocksRaycasts = true;
            float elapsed = 0f;
            while (fading && elapsed < seconds && fadeGroup != null)
            {
                fadeGroup.alpha = Mathf.Lerp(previousAlpha, 0f,
                    Mathf.SmoothStep(0f, 1f, elapsed / seconds));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            if (fading && fadeGroup != null) fadeGroup.alpha = 0f;
        }

        public void CancelFade()
        {
            if (!fading) return;
            fading = false;
            if (fadeGroup == null) return;
            fadeGroup.alpha = previousAlpha;
            fadeGroup.interactable = previousInteractable;
            fadeGroup.blocksRaycasts = previousBlocksRaycasts;
        }

        public void RefreshRuntimeSelection()
        {
            RebuildNavigation();
            initializeSelection = true;
            selectedIndex = -1;
        }

        public void Configure(UnityEngine.UI.Button[] buttons, RectTransform underline, TMP_Text help, string[] helpTexts)
        {
            options = buttons;
            selectionUnderline = underline;
            description = help;
            descriptions = helpTexts;
            selectedIndex = -1;
            if (Application.IsPlaying(gameObject)) RebuildNavigation();
            RefreshLayout();
            PresentSelection(0);
        }

        private void OnEnable()
        {
            frame = GetComponent<RectTransform>();
            initializeSelection = true;
            selectedIndex = -1;
            if (Application.IsPlaying(gameObject)) RebuildNavigation();
            Canvas.willRenderCanvases += RefreshLayout;
            RefreshLayout();
            PresentSelection(0);
        }

        private void OnDisable()
        {
            CancelFade();
            Canvas.willRenderCanvases -= RefreshLayout;
            if (!cursorCaptured) return;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            cursorCaptured = false;
        }

        private void OnValidate()
        {
            referenceResolution.x = Mathf.Max(1, referenceResolution.x);
            referenceResolution.y = Mathf.Max(1, referenceResolution.y);
        }

        private void LateUpdate()
        {
            RefreshLayout();
            if (fading || !Application.IsPlaying(gameObject) || options == null || options.Length == 0)
                return;
            bool hasActiveOption = false;
            foreach (var option in options)
                if (option != null && option.isActiveAndEnabled) hasActiveOption = true;
            if (!hasActiveOption) return;
            EventSystem events = EventSystem.current;
            if (events == null) return;
            if (!cursorCaptured)
            {
                previousCursorLock = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                cursorCaptured = true;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (initializeSelection)
            {
                initializeSelection = false;
                bool belongsToMenu = false;
                foreach (var option in options)
                    if (option != null && events.currentSelectedGameObject == option.gameObject && option.isActiveAndEnabled && option.IsInteractable())
                        belongsToMenu = true;
                if (!belongsToMenu)
                    foreach (var option in options)
                        if (option != null && option.isActiveAndEnabled && option.IsInteractable())
                        { option.Select(); break; }
            }
            GameObject current = events.currentSelectedGameObject;
            if (current == null)
            {
                if (selectedIndex >= 0 && selectedIndex < options.Length && options[selectedIndex] != null &&
                    options[selectedIndex].isActiveAndEnabled && options[selectedIndex].IsInteractable())
                    options[selectedIndex].Select();
                else
                    foreach (var option in options)
                        if (option != null && option.isActiveAndEnabled && option.IsInteractable())
                        { option.Select(); break; }
                current = events.currentSelectedGameObject;
            }
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] != null && current == options[i].gameObject)
                {
                    PresentSelection(i);
                    break;
                }
            }
        }

        private void RebuildNavigation()
        {
            if (options == null) return;
            var available = new List<UnityEngine.UI.Button>(options.Length);
            foreach (var option in options)
            {
                if (option == null) continue;
                var navigation = option.navigation;
                navigation.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                navigation.selectOnUp = null;
                navigation.selectOnDown = null;
                option.navigation = navigation;
                if (option.isActiveAndEnabled && option.IsInteractable()) available.Add(option);
            }
            // Explicit navigation does not skip hidden or disabled targets. Build the
            // authored circular order again whenever runtime availability changes.
            for (int index = 0; index < available.Count; index++)
            {
                var navigation = available[index].navigation;
                navigation.selectOnUp = available[(index + available.Count - 1) % available.Count];
                navigation.selectOnDown = available[(index + 1) % available.Count];
                available[index].navigation = navigation;
            }
        }

        public void RefreshLayout()
        {
            if (frame == null) frame = GetComponent<RectTransform>();
            RectTransform viewport = frame.parent as RectTransform;
            if (viewport == null || viewport.rect.width <= 0 || viewport.rect.height <= 0) return;
            float scale = Mathf.Min(viewport.rect.width / referenceResolution.x, viewport.rect.height / referenceResolution.y);
            Vector2 anchor = new Vector2(0.5f, 0.5f);
            if (frame.anchorMin != anchor) frame.anchorMin = anchor;
            if (frame.anchorMax != anchor) frame.anchorMax = anchor;
            if (frame.pivot != anchor) frame.pivot = anchor;
            if (frame.sizeDelta != referenceResolution) frame.sizeDelta = referenceResolution;
            if (frame.anchoredPosition != Vector2.zero) frame.anchoredPosition = Vector2.zero;
            Vector3 fittedScale = new Vector3(scale, scale, 1);
            if (frame.localScale != fittedScale) frame.localScale = fittedScale;
        }

        private void PresentSelection(int index)
        {
            if (options == null || index < 0 || index >= options.Length || options[index] == null || selectedIndex == index)
                return;
            selectedIndex = index;
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == null) continue;
                var colors = options[i].colors;
                colors.normalColor = !Application.IsPlaying(gameObject) && i == index ? selectedColor : normalColor;
                colors.highlightedColor = selectedColor;
                colors.selectedColor = selectedColor;
                colors.pressedColor = selectedColor * 0.85f;
                colors.colorMultiplier = 1;
                colors.fadeDuration = 0.12f;
                options[i].colors = colors;
            }
            if (selectionUnderline != null)
            {
                selectionUnderline.SetParent(options[index].transform, false);
                selectionUnderline.anchorMin = selectionUnderline.anchorMax = new Vector2(0, 1);
                selectionUnderline.pivot = new Vector2(0, 1);
                selectionUnderline.anchoredPosition = new Vector2(0, -47);
                selectionUnderline.sizeDelta = new Vector2(280, 2);
            }
            if (description != null && descriptions != null && index < descriptions.Length)
                description.text = descriptions[index];
        }
    }
}
