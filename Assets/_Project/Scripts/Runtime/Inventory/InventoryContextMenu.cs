using System;
using SaksiTerakhir.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SaksiTerakhir.Inventory
{
    // The overlay and popup belong to InventoryView, never to the authored UI.
    public sealed class InventoryContextMenu : MonoBehaviour, IPointerDownHandler, IDisposable
    {
        private const float Width = 190f;
        private const float RowHeight = 42f;
        private const float Padding = 6f;
        private RectTransform popup;
        private GameObject owner;
        private TMP_Text holdLabel;
        private InventoryEntry entry;
        private bool disposed;

        public InventoryEntry Entry => entry;

        public static InventoryContextMenu Create(Transform parent, GameObject panel,
            TMP_Text textStyle, UnityEngine.UI.Image backgroundStyle,
            Action<InventoryEntry> use, Action<InventoryEntry> hold, Action<InventoryEntry> drop)
        {
            RectTransform overlay = CreateRect("Inventory Context Menu", parent);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            var blocker = overlay.gameObject.AddComponent<UnityEngine.UI.Image>();
            blocker.color = Color.clear;
            blocker.raycastTarget = true;
            InventoryContextMenu menu = overlay.gameObject.AddComponent<InventoryContextMenu>();
            menu.owner = panel;
            menu.popup = CreateRect("Actions", overlay);
            menu.popup.anchorMin = menu.popup.anchorMax = new Vector2(0.5f, 0.5f);
            menu.popup.pivot = new Vector2(0f, 1f);
            menu.popup.sizeDelta = new Vector2(Width, RowHeight * 3f + Padding * 2f);
            var background = menu.popup.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = backgroundStyle != null ? backgroundStyle.color : new Color(0.16f, 0.18f, 0.16f, 1f);
            background.color = new Color(background.color.r, background.color.g, background.color.b, 1f);
            if (backgroundStyle != null)
            {
                background.sprite = backgroundStyle.sprite;
                background.type = backgroundStyle.type;
            }
            menu.AddAction(0, "Use", "inventory.action.use", textStyle, background.color, use);
            menu.holdLabel = menu.AddAction(1, "Hold", "inventory.action.hold", textStyle, background.color, hold);
            menu.AddAction(2, "Drop", "inventory.action.drop", textStyle, background.color, drop);
            overlay.gameObject.SetActive(false);
            return menu;
        }

        public void Show(InventoryEntry value, bool held, PointerEventData pointer)
        {
            if (disposed || value == null || value.Type != InventoryEntryType.Item) return;
            entry = value;
            RefreshLabels(held);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RectTransform overlay = (RectTransform)transform;
            Canvas.ForceUpdateCanvases();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, pointer.position,
                pointer.pressEventCamera, out Vector2 local)) local = Vector2.zero;
            Rect bounds = overlay.rect;
            local.x = Mathf.Clamp(local.x, bounds.xMin, Mathf.Max(bounds.xMin, bounds.xMax - popup.rect.width));
            local.y = Mathf.Clamp(local.y, Mathf.Min(bounds.yMax, bounds.yMin + popup.rect.height), bounds.yMax);
            popup.anchoredPosition = local;
        }

        public void RefreshLabels(bool held)
        {
            if (disposed) return;
            popup.Find("Use/Label").GetComponent<TMP_Text>().text = LocalizationManager.Get("inventory.action.use");
            holdLabel.text = LocalizationManager.Get(held ? "inventory.action.release" : "inventory.action.hold");
            popup.Find("Drop/Label").GetComponent<TMP_Text>().text = LocalizationManager.Get("inventory.action.drop");
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(popup, eventData.position, eventData.pressEventCamera))
                Close();
        }

        public void Close()
        {
            entry = null;
            if (this != null) gameObject.SetActive(false);
        }

        private void Update()
        {
            if (owner == null || !owner.activeInHierarchy) Close();
        }

        private TMP_Text AddAction(int row, string name, string key, TMP_Text style, Color background,
            Action<InventoryEntry> callback)
        {
            RectTransform rect = CreateRect(name, popup);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(Padding, -Padding - row * RowHeight);
            rect.sizeDelta = new Vector2(Width - Padding * 2f, RowHeight);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = background;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.interactable = callback != null;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.82f, 0.85f, 0.78f, 1f);
            colors.pressedColor = new Color(0.65f, 0.7f, 0.6f, 1f);
            button.colors = colors;
            RectTransform labelRect = CreateRect("Label", rect);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            if (style != null)
            {
                label.font = style.font;
                label.fontSharedMaterial = style.fontSharedMaterial;
                label.fontStyle = style.fontStyle;
            }
            label.fontSize = style != null ? Mathf.Clamp(style.fontSize, 18f, 24f) : 22f;
            float luminance = background.r * 0.2126f + background.g * 0.7152f + background.b * 0.0722f;
            Color authored = style != null ? style.color : Color.white;
            float authoredLuminance = authored.r * 0.2126f + authored.g * 0.7152f + authored.b * 0.0722f;
            label.color = Mathf.Abs(luminance - authoredLuminance) >= 0.4f
                ? authored : luminance > 0.5f ? new Color(0.08f, 0.1f, 0.08f, 1f) : new Color(0.95f, 0.95f, 0.9f, 1f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.text = LocalizationManager.Get(key);
            button.onClick.AddListener(() =>
            {
                InventoryEntry target = entry;
                Close();
                if (target != null && target.Type == InventoryEntryType.Item) callback?.Invoke(target);
            });
            return label;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Close();
            if (this == null) return;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
