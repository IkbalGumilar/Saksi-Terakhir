using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace SaksiTerakhir.Inventory
{
    // Owns only the runtime copies of the user's slots. The original templates
    // remain in the scene, so their design can still be edited normally.
    public sealed class InventoryView : IDisposable
    {
        public const float SlotStep = 100f;
        private readonly GameObject gridPage;
        private readonly GameObject documentPage;
        private readonly RectTransform gridTemplate;
        private readonly RectTransform documentTemplate;
        private readonly RectTransform documentContent;
        private readonly RectTransform gridContent;
        private readonly UnityEngine.UI.ScrollRect gridScroll;
        private readonly UnityEngine.UI.ScrollRect documentScroll;
        private readonly List<Slot> gridSlots = new List<Slot>();
        private readonly List<Slot> documentSlots = new List<Slot>();
        private readonly List<Action> removeListeners = new List<Action>();
        private readonly UnityEngine.UI.Image preview;
        private readonly TMP_Text title;
        private readonly TMP_Text status;
        private readonly TMP_Text description;
        private InventoryEntry selected;
        private bool showingDocuments;

        private sealed class Slot
        {
            public RectTransform Rect;
            public UnityEngine.UI.Button Button;
            public UnityEngine.UI.Image Icon;
            public TMP_Text Title;
            public TMP_Text Description;
            public InventoryEntry Entry;
        }

        public InventoryView(GameObject panel, Action close)
        {
            Transform root = panel.transform;
            gridPage = root.Find("Inventory").gameObject;
            documentPage = root.Find("Inventory Dock").gameObject;
            gridTemplate = (RectTransform)gridPage.transform.Find("Slot 1");
            documentContent = (RectTransform)documentPage.transform.Find("Scroll View/Viewport/Content");
            documentTemplate = (RectTransform)documentContent.Find("Slot 1");
            documentScroll = documentPage.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
            documentScroll.horizontal = false;
            documentScroll.vertical = true;
            documentScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            documentContent.anchorMin = new Vector2(0f, 1f);
            documentContent.anchorMax = Vector2.one;
            documentContent.pivot = new Vector2(0f, 1f);
            documentContent.anchoredPosition = Vector2.zero;

            var viewport = CreateRect("Inventory Grid Viewport", gridPage.transform);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            // Arrows sit beside the page; the full interior is scrollable.
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            var hitArea = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            hitArea.color = Color.clear;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            gridContent = CreateRect("Inventory Grid Content", viewport);
            gridContent.anchorMin = new Vector2(0f, 1f);
            gridContent.anchorMax = new Vector2(0f, 1f);
            gridContent.pivot = new Vector2(0f, 1f);
            gridScroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            gridScroll.viewport = viewport;
            gridScroll.content = gridContent;
            gridScroll.horizontal = false;
            gridScroll.vertical = true;
            gridScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            gridScroll.scrollSensitivity = 35f;

            Transform info = root.Find("Informaton Panel");
            preview = info.Find("Preview Item").GetComponent<UnityEngine.UI.Image>();
            title = info.Find("Name Item").GetComponent<TMP_Text>();
            status = info.Find("Item Status").GetComponent<TMP_Text>();
            description = info.Find("Informations Text").GetComponent<TMP_Text>();
            gridTemplate.gameObject.SetActive(false);
            documentTemplate.gameObject.SetActive(false);

            Wire(gridPage.transform.Find("Arrow Right"), () => ShowPage(true));
            Wire(documentPage.transform.Find("Arrow Left"), () => ShowPage(false));
            SetArrowUnavailable(gridPage.transform.Find("Arrow Left"));
            SetArrowUnavailable(documentPage.transform.Find("Arrow Right"));
            Wire(root.Find("button panel/button 1"), () => close());
            ShowPage(false);
            Select(null);
        }

        public static bool CanBind(GameObject panel)
        {
            if (panel == null) return false;
            Transform root = panel.transform;
            return Has<RectTransform>(root, "Inventory/Slot 1")
                && Has<RectTransform>(root, "Inventory Dock/Scroll View/Viewport/Content/Slot 1")
                && Has<UnityEngine.UI.ScrollRect>(root, "Inventory Dock/Scroll View")
                && Has<UnityEngine.UI.Image>(root, "Informaton Panel/Preview Item")
                && Has<TMP_Text>(root, "Informaton Panel/Name Item")
                && Has<TMP_Text>(root, "Informaton Panel/Item Status")
                && Has<TMP_Text>(root, "Informaton Panel/Informations Text");
        }

        public void Refresh(InventoryState state)
        {
            EnsureCount(gridSlots, state.Capacity, gridTemplate, gridContent, false);
            gridContent.sizeDelta = new Vector2(state.Columns * SlotStep, state.Rows * SlotStep);
            for (int i = 0; i < gridSlots.Count; i++)
            {
                Slot slot = gridSlots[i];
                slot.Rect.anchoredPosition = new Vector2((i % state.Columns + 0.5f) * SlotStep,
                    -(i / state.Columns + 0.5f) * SlotStep);
                Bind(slot, i < state.Items.Count ? state.Items[i] : null);
            }

            EnsureCount(documentSlots, state.Documents.Count, documentTemplate, documentContent, true);
            float viewportHeight = documentScroll.viewport != null ? documentScroll.viewport.rect.height : 0f;
            documentContent.sizeDelta = new Vector2(0f, Mathf.Max(viewportHeight, state.Documents.Count * SlotStep));
            for (int i = 0; i < documentSlots.Count; i++)
            {
                Slot slot = documentSlots[i];
                slot.Rect.anchoredPosition = new Vector2(0f, -i * SlotStep);
                Bind(slot, state.Documents[i]);
            }
            if (selected != null && !Contains(state, selected)) Select(null);
            else Select(selected);
        }

        public void ShowPage(bool documents)
        {
            showingDocuments = documents;
            gridPage.SetActive(!documents);
            documentPage.SetActive(documents);
            Select(null);
            FocusFirstSlot();
        }

        public void OnOpened()
        {
            Canvas.ForceUpdateCanvases();
            gridScroll.StopMovement();
            documentScroll.StopMovement();
            FocusFirstSlot();
        }

        private void FocusFirstSlot()
        {
            if (EventSystem.current == null) return;
            List<Slot> slots = showingDocuments ? documentSlots : gridSlots;
            GameObject focus = null;
            foreach (Slot slot in slots)
            {
                if (!slot.Button.interactable) continue;
                focus = slot.Button.gameObject;
                break;
            }
            if (focus == null)
            {
                Transform arrow = (showingDocuments ? documentPage : gridPage).transform
                    .Find(showingDocuments ? "Arrow Left" : "Arrow Right");
                if (arrow != null) focus = arrow.gameObject;
            }
            EventSystem.current.SetSelectedGameObject(focus);
        }

        private void EnsureCount(List<Slot> slots, int count, RectTransform template, Transform parent, bool document)
        {
            while (slots.Count > count)
            {
                Slot last = slots[slots.Count - 1];
                last.Rect.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(last.Rect.gameObject);
                slots.RemoveAt(slots.Count - 1);
            }
            while (slots.Count < count)
            {
                RectTransform rect = UnityEngine.Object.Instantiate(template, parent, false);
                rect.name = document ? $"Document Slot {slots.Count + 1}" : $"Inventory Slot {slots.Count + 1}";
                rect.localScale = Vector3.one;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = document ? Vector2.one : new Vector2(0f, 1f);
                rect.pivot = document ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(document ? 0f : SlotStep, SlotStep);
                var slot = new Slot
                {
                    Rect = rect,
                    Button = GetButton(rect),
                    Icon = FindChild(rect, document ? "Icon" : "Image")?.GetComponent<UnityEngine.UI.Image>(),
                    Title = document ? FindChild(rect, "Title")?.GetComponent<TMP_Text>() : null,
                    Description = document ? FindChild(rect, "Description")?.GetComponent<TMP_Text>() : null
                };
                slot.Button.onClick.AddListener(() => Select(slot.Entry));
                // Ignore decorative descendants when resolving slot clicks.
                foreach (UnityEngine.UI.Graphic graphic in rect.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    if (graphic.gameObject != rect.gameObject) graphic.raycastTarget = false;
                rect.gameObject.SetActive(true);
                slots.Add(slot);
            }
        }

        private void Bind(Slot slot, InventoryEntry entry)
        {
            slot.Entry = entry;
            slot.Button.interactable = entry != null;
            if (slot.Icon != null)
            {
                slot.Icon.sprite = entry?.Icon;
                slot.Icon.enabled = entry?.Icon != null;
                slot.Icon.preserveAspect = true;
            }
            if (slot.Title != null) slot.Title.text = entry?.DisplayName ?? string.Empty;
            if (slot.Description != null) slot.Description.text = entry?.Description ?? string.Empty;
        }

        private void Select(InventoryEntry entry)
        {
            selected = entry;
            preview.sprite = entry?.Icon;
            preview.enabled = entry?.Icon != null;
            preview.preserveAspect = true;
            title.text = entry?.DisplayName ?? string.Empty;
            status.text = entry == null ? string.Empty : entry.Type == InventoryEntryType.Document ? "Dokumen" : "Item";
            description.text = entry?.Description ?? string.Empty;
        }

        private void Wire(Transform target, UnityAction action)
        {
            if (target == null) return;
            if (target.name == "Arrow Left")
            {
                // The authored 180-degree Y rotation mirrors the image, but
                // GraphicRaycaster ignores its reversed face. Mirror X instead.
                target.localRotation = Quaternion.identity;
                Vector3 scale = target.localScale;
                scale.x = -Mathf.Abs(scale.x);
                target.localScale = scale;
            }
            var button = GetButton(target);
            button.onClick.AddListener(action);
            removeListeners.Add(() => { if (button != null) button.onClick.RemoveListener(action); });
        }

        private static UnityEngine.UI.Button GetButton(Transform target)
        {
            var button = target.GetComponent<UnityEngine.UI.Button>();
            if (button == null) button = target.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = target.GetComponent<UnityEngine.UI.Graphic>();
            if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
            return button;
        }

        private static void SetArrowUnavailable(Transform target)
        {
            if (target == null) return;
            GetButton(target).interactable = false;
        }

        private static Transform FindChild(Transform parent, string trimmedName)
        {
            foreach (Transform child in parent)
                if (child.name.Trim() == trimmedName) return child;
            return null;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }

        private static bool Has<T>(Transform root, string path) where T : Component
        {
            Transform child = root.Find(path);
            return child != null && child.GetComponent<T>() != null;
        }

        private static bool Contains(InventoryState state, InventoryEntry entry)
        {
            foreach (InventoryEntry item in state.Items) if (ReferenceEquals(item, entry)) return true;
            foreach (InventoryEntry document in state.Documents) if (ReferenceEquals(document, entry)) return true;
            return false;
        }

        public void Dispose()
        {
            foreach (Action remove in removeListeners) remove();
            foreach (Slot slot in gridSlots) if (slot.Rect != null) UnityEngine.Object.Destroy(slot.Rect.gameObject);
            foreach (Slot slot in documentSlots) if (slot.Rect != null) UnityEngine.Object.Destroy(slot.Rect.gameObject);
            if (gridContent != null) UnityEngine.Object.Destroy(gridContent.parent.gameObject);
        }
    }
}
