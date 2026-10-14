using System;
using System.Collections.Generic;
using SaksiTerakhir.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace SaksiTerakhir.Inventory
{
    // Runtime reader inside the authored information panel. Its original writing
    // and translated page belong to one inventory entry.
    internal sealed class InventoryDocumentView : IDisposable
    {
        private readonly RectTransform root;
        private readonly TMP_Text title;
        private readonly TMP_Text text;
        private readonly TMP_Text translateLabel;
        private readonly TMP_FontAsset translatedFont;
        private readonly UnityEngine.UI.Button translate;
        private readonly UnityEngine.UI.ScrollRect scroll;
        private readonly RectTransform content;
        private InventoryEntry entry;
        private bool showingTranslation;

        public InventoryDocumentView(RectTransform parent, TMP_Text style)
        {
            translatedFont = style.font != null ? style.font : TMP_Settings.defaultFontAsset;
            root = Rect("Document Reader", parent);
            Fill(root, new Vector2(12f, 12f), new Vector2(-12f, -12f));
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.055f, .065f, .085f, .98f);
            title = Label("Title", root, translatedFont, 28f);
            Fill(title.rectTransform, new Vector2(20f, 0f), new Vector2(-20f, -16f));
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.offsetMin = new Vector2(20f, -72f);
            title.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform scrollRoot = Rect("Scroll View", root);
            Fill(scrollRoot, new Vector2(20f, 62f), new Vector2(-20f, -82f));
            scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            RectTransform viewport = Rect("Viewport", scrollRoot);
            Fill(viewport, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.clear;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1f);
            text = Label("Text", content, translatedFont, 26f);
            Fill(text.rectTransform, Vector2.zero, Vector2.zero);
            text.fontFeatures = new List<OTL_FeatureTag> { OTL_FeatureTag.kern,
                OTL_FeatureTag.mark, OTL_FeatureTag.mkmk };
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45f;

            RectTransform button = Rect("Translate", root);
            button.anchorMin = button.anchorMax = new Vector2(1f, 0f);
            button.pivot = new Vector2(1f, 0f);
            button.sizeDelta = new Vector2(160f, 38f);
            button.anchoredPosition = new Vector2(-16f, 12f);
            var background = button.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(.13f, .18f, .24f, 1f);
            translate = button.gameObject.AddComponent<UnityEngine.UI.Button>();
            translate.targetGraphic = background;
            translate.onClick.AddListener(ToggleTranslation);
            translateLabel = Label("Text", button, translatedFont, 21f);
            Fill(translateLabel.rectTransform, new Vector2(6f, 2f), new Vector2(-6f, -2f));
            translateLabel.alignment = TextAlignmentOptions.Center;
            Hide();
        }

        public void Show(InventoryEntry selected)
        {
            if (selected?.Definition == null || !selected.Definition.HasDocumentTranslation)
            {
                Hide();
                return;
            }
            bool changed = !ReferenceEquals(entry, selected);
            entry = selected;
            if (changed) showingTranslation = false;
            root.gameObject.SetActive(true);
            title.text = selected.DisplayName;
            Render(changed);
        }

        public void Hide()
        {
            entry = null;
            showingTranslation = false;
            if (scroll != null) scroll.StopMovement();
            if (root != null) root.gameObject.SetActive(false);
        }

        private void ToggleTranslation()
        {
            if (entry == null) return;
            showingTranslation = !showingTranslation;
            Render(true);
        }

        private void Render(bool resetScroll)
        {
            ItemDefinition document = entry.Definition;
            text.font = !showingTranslation && document.DocumentFont != null
                ? document.DocumentFont : translatedFont;
            text.text = showingTranslation ? LocalizationManager.Get(document.DocumentTranslationKey) : document.DocumentText;
            translateLabel.text = TranslateLabel(showingTranslation);
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Max(1f, scroll.viewport.rect.width);
            float height = text.GetPreferredValues(text.text, width, Mathf.Infinity).y + 12f;
            content.sizeDelta = new Vector2(0f, Mathf.Max(scroll.viewport.rect.height, height));
            if (!resetScroll) return;
            scroll.StopMovement();
            content.anchoredPosition = Vector2.zero;
            scroll.verticalNormalizedPosition = 1f;
        }

        private static string TranslateLabel(bool translated)
        {
            string key = translated ? "inventory.document.original" : "inventory.document.translate";
            string label = LocalizationManager.Get(key);
            if (label != key) return label;
            bool indonesian = LocalizationManager.CurrentLanguage == GameLanguage.Indonesian;
            return translated ? (indonesian ? "Teks asli" : "Original text") : (indonesian ? "Terjemahkan" : "Translate");
        }

        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
            label.color = new Color(.94f, .93f, .86f, 1f);
            return label;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }

        private static void Fill(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = minimum;
            rect.offsetMax = maximum;
        }

        public void Dispose()
        {
            if (translate != null) translate.onClick.RemoveListener(ToggleTranslation);
            if (root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }
    }
}
