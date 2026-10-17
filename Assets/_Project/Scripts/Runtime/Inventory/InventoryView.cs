using System;
using System.Collections.Generic;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Story;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace SaksiTerakhir.Inventory
{
    public enum InventoryPage { Items, Documents, Missions }

    // Owns only the runtime copies of the user's slots. The original templates
    // remain in the scene, so their design can still be edited normally.
    public sealed class InventoryView : IDisposable
    {
        public const float SlotStep = 100f;
        private readonly GameObject gridPage;
        private readonly GameObject documentPage;
        private readonly GameObject missionPage;
        private readonly InventoryMissionView missions;
        private readonly List<GameObject> underlines = new List<GameObject>();
        private readonly RectTransform gridTemplate;
        private readonly RectTransform documentTemplate;
        private readonly RectTransform documentContent;
        private readonly RectTransform gridContent;
        private readonly RectTransform gridBoundary;
        private readonly RectTransform overlayRoot;
        private readonly UnityEngine.UI.ScrollRect gridScroll;
        private readonly UnityEngine.UI.ScrollRect documentScroll;
        private readonly List<Slot> gridSlots = new List<Slot>();
        private readonly List<Slot> documentSlots = new List<Slot>();
        private readonly List<Action> removeListeners = new List<Action>();
        private readonly UnityEngine.UI.Image preview;
        private readonly TMP_Text title;
        private readonly TMP_Text status;
        private readonly TMP_Text description;
        private readonly InventoryDocumentView documentReader;
        private readonly Action<InventoryEntry> drop;
        private readonly Action<InventoryEntry, int> move;
        private readonly InventoryContextMenu contextMenu;
        private readonly Func<InventoryEntry, QuestCategory?> questCategory;
        private InventoryState state;
        private InventoryEntry draggedEntry;
        private InventorySlotPointer dragSource;
        private RectTransform dragGhost;
        private InventoryEntry selected;
        private bool disposed;

        public InventoryPage ActivePage { get; private set; }
        private bool ShowingDocuments => ActivePage == InventoryPage.Documents;

        private sealed class Slot
        {
            public InventoryQuestFrame QuestFrame;
            public RectTransform Rect;
            public UnityEngine.UI.Button Button;
            public UnityEngine.UI.Image Icon;
            public TMP_Text Title;
            public TMP_Text Description;
            public InventoryEntry Entry;
            public GameObject Mark;
            public InventorySlotPointer Pointer;
            public int Index;
        }

        public InventoryView(GameObject panel, Action close)
            : this(panel, close, null, null, null, null) { }

        public InventoryView(GameObject panel, Action close,
            Action<InventoryEntry> use, Action<InventoryEntry> hold, Action<InventoryEntry> drop,
            Action<InventoryEntry, int> move, Func<InventoryEntry, QuestCategory?> questCategory = null)
        {
            if (!CanBind(panel)) throw new ArgumentException("Inventory panel hierarchy is incomplete.", nameof(panel));
            this.drop = drop;
            this.move = move;
            this.questCategory = questCategory;
            Transform root = panel.transform;
            gridPage = root.Find("Inventory").gameObject;
            gridBoundary = (RectTransform)gridPage.transform;
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            overlayRoot = canvas != null ? (RectTransform)canvas.rootCanvas.transform : (RectTransform)root;
            documentPage = root.Find("Inventory Dock").gameObject;
            missionPage = root.Find("Quest List")?.gameObject;
            if (missionPage != null) missions = new InventoryMissionView(missionPage, ShowQuestDetails);
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
            documentReader = new InventoryDocumentView((RectTransform)info, title);
            var bgImage = root.GetComponent<UnityEngine.UI.Image>();
            if (bgImage == null) bgImage = gridTemplate.GetComponent<UnityEngine.UI.Image>();
            contextMenu = InventoryContextMenu.Create(overlayRoot, panel, title,
                bgImage, use, hold, drop);
            gridTemplate.gameObject.SetActive(false);
            documentTemplate.gameObject.SetActive(false);

            WirePageArrows(gridPage);
            WirePageArrows(documentPage);
            if (missionPage != null) WirePageArrows(missionPage);
            WireHeader(root.Find("Header"));
            Wire(root.Find("button panel/button 1"), () => { OnClosed(); close?.Invoke(); });
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
            if (disposed) return;
            if (state == null) throw new ArgumentNullException(nameof(state));
            this.state = state;
            if (draggedEntry != null && !Contains(state, draggedEntry)) CancelDrag();
            if (contextMenu.Entry != null && !Contains(state, contextMenu.Entry)) contextMenu.Close();
            else if (contextMenu.Entry != null) contextMenu.RefreshLabels(state.IsHeld(contextMenu.Entry),
                !QuestCategoryFor(contextMenu.Entry).HasValue);
            EnsureCount(gridSlots, state.Capacity, gridTemplate, gridContent, false);
            gridContent.sizeDelta = new Vector2(state.Columns * SlotStep, state.Rows * SlotStep);
            for (int i = 0; i < gridSlots.Count; i++)
            {
                Slot slot = gridSlots[i];
                slot.Rect.anchoredPosition = new Vector2((i % state.Columns + 0.5f) * SlotStep,
                    -(i / state.Columns + 0.5f) * SlotStep);
                slot.Index = i;
                Bind(slot, state.GetItemAtSlot(i));
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
            if (ActivePage == InventoryPage.Missions) missions?.Refresh();
            else if (selected != null && !Contains(state, selected)) Select(null);
            else Select(selected);
        }

        public void ShowPage(bool documents)
            => ShowPage(documents ? InventoryPage.Documents : InventoryPage.Items);

        public void ShowPage(InventoryPage page)
        {
            if (disposed) return;
            if (page == InventoryPage.Missions && missionPage == null) return;
            CancelDrag();
            contextMenu.Close();
            ActivePage = page;
            gridPage.SetActive(page == InventoryPage.Items);
            documentPage.SetActive(ShowingDocuments);
            if (missionPage != null) missionPage.SetActive(page == InventoryPage.Missions);
            for (int i = 0; i < underlines.Count; i++)
                if (underlines[i] != null) underlines[i].SetActive(i == (int)page);
            Select(null);
            if (page == InventoryPage.Missions) missions?.Refresh();
            FocusFirstSlot();
        }

        public void MovePage(int direction)
        {
            if (direction == 0 || disposed) return;
            int count = missionPage != null ? 3 : 2;
            ShowPage((InventoryPage)(((int)ActivePage + (direction > 0 ? 1 : count - 1)) % count));
        }

        public void BindJournal(QuestJournalController journal) => missions?.Bind(journal);

        private void WirePageArrows(GameObject page)
        {
            Wire(page.transform.Find("Arrow Left"), () => MovePage(-1));
            Wire(page.transform.Find("Arrow Right"), () => MovePage(1));
        }

        private void WireHeader(Transform header)
        {
            string[] names = { "Inventory Tab", "Dokcument Tab", "Quest list Tab" };
            for (int i = 0; i < names.Length; i++)
            {
                Transform tab = header != null ? header.Find(names[i]) : null;
                underlines.Add(tab != null ? tab.Find("Selection Underline")?.gameObject : null);
                InventoryPage page = (InventoryPage)i;
                Wire(tab, () => ShowPage(page));
            }
        }

        public void OnOpened()
        {
            if (disposed) return;
            OnClosed();
            Canvas.ForceUpdateCanvases();
            gridScroll.StopMovement();
            documentScroll.StopMovement();
            if (ActivePage == InventoryPage.Missions) missions?.Refresh();
            else Select(selected);
            FocusFirstSlot();
        }

        public void OnClosed()
        {
            CancelDrag();
            if (contextMenu != null) contextMenu.Close();
            if (gridScroll != null) gridScroll.StopMovement();
            if (documentScroll != null) documentScroll.StopMovement();
            missions?.StopMovement();
            documentReader?.Hide();
        }

        public void SelectEntry(InventoryEntry entry)
        {
            if (disposed) return;
            if (entry != null && state != null && !Contains(state, entry)) return;
            bool document = entry != null && entry.Type == InventoryEntryType.Document;
            if (entry != null && ActivePage != (document ? InventoryPage.Documents : InventoryPage.Items)) ShowPage(document);
            contextMenu.Close();
            Select(entry);
            if (entry == null || EventSystem.current == null) return;
            foreach (Slot slot in document ? documentSlots : gridSlots)
                if (ReferenceEquals(slot.Entry, entry))
                {
                    EventSystem.current.SetSelectedGameObject(slot.Button.gameObject);
                    break;
                }
        }

        private void FocusFirstSlot()
        {
            if (EventSystem.current == null) return;
            if (ActivePage == InventoryPage.Missions)
            {
                EventSystem.current.SetSelectedGameObject(missions?.FirstSelectable);
                return;
            }
            List<Slot> slots = ShowingDocuments ? documentSlots : gridSlots;
            GameObject focus = null;
            foreach (Slot slot in slots)
            {
                if (!slot.Button.interactable) continue;
                focus = slot.Button.gameObject;
                break;
            }
            if (focus == null)
            {
                Transform arrow = (ShowingDocuments ? documentPage : gridPage).transform
                    .Find(ShowingDocuments ? "Arrow Left" : "Arrow Right");
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
                DestroyRuntime(last.Rect.gameObject);
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
                    Icon = FindItemIcon(rect, document),
                    Title = document ? FindChild(rect, "Title")?.GetComponent<TMP_Text>() : null,
                    Description = document ? FindChild(rect, "Description")?.GetComponent<TMP_Text>() : null
                };
                slot.Index = slots.Count;
                slot.Mark = FindHeldMark(slot.Icon != null ? slot.Icon.transform : rect)?.gameObject;
                slot.Button.onClick.AddListener(() => { contextMenu.Close(); Select(slot.Entry); });
                if (!document)
                {
                    slot.QuestFrame = InventoryQuestFrame.Create(rect);
                    slot.Pointer = rect.GetComponent<InventorySlotPointer>();
                    if (slot.Pointer == null) slot.Pointer = rect.gameObject.AddComponent<InventorySlotPointer>();
                }
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
            if (slot.Mark != null) slot.Mark.SetActive(entry != null && state.IsHeld(entry));
            slot.QuestFrame?.SetCategory(QuestCategoryFor(entry));
            if (slot.Pointer != null)
                slot.Pointer.Configure(entry, OpenContextMenu,
                    (value, pointer) => BeginDrag(slot, value, pointer), UpdateDrag, EndDrag, CancelDrag, gridScroll);
        }

        private void Select(InventoryEntry entry)
        {
            selected = entry;
            preview.sprite = entry?.Icon;
            preview.enabled = entry?.Icon != null;
            preview.preserveAspect = true;
            title.text = entry?.DisplayName ?? string.Empty;
            status.text = entry == null ? string.Empty : LocalizationManager.Get(
                entry.Type == InventoryEntryType.Document ? "inventory.type.document" : "inventory.type.item");
            description.text = entry != null && entry.Type == InventoryEntryType.Document
                && entry.Definition != null && !entry.Definition.HasDocumentTranslation
                && !string.IsNullOrEmpty(entry.Definition.DocumentText)
                ? LocalizationManager.Translate(entry.Definition.DocumentText) : entry?.Description ?? string.Empty;
            documentReader.Show(entry);
        }

        private void ShowQuestDetails(QuestJournalEntry entry)
        {
            if (ActivePage != InventoryPage.Missions) return;
            if (entry == null || entry.Definition == null) { Select(null); return; }
            selected = null;
            documentReader.Hide();
            preview.sprite = null;
            preview.enabled = false;
            title.text = LocalizationManager.Get(entry.Definition.TitleKey);
            status.text = InventoryMissionView.StatusText(entry.Status);
            description.text = InventoryMissionView.DetailsText(entry.Definition);
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

        private static Transform FindChild(Transform parent, string trimmedName)
        {
            foreach (Transform child in parent)
                if (string.Equals(child.name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase)) return child;
            return null;
        }

        private static UnityEngine.UI.Image FindItemIcon(Transform slot, bool document)
        {
            string[] names = document ? new[] { "Icon", "image Item", "Item Image", "Image" } : new[] { "image Item", "Image Item", "Item Image", "Image", "Icon" };
            foreach (string name in names)
            {
                Transform child = FindChild(slot, name);
                if (child != null && child.TryGetComponent(out UnityEngine.UI.Image image)) return image;
            }
            foreach (UnityEngine.UI.Image image in slot.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                if (image.transform != slot && !IsHeldMarkName(image.name)) return image;
            return null;
        }

        private static Transform FindHeldMark(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (IsHeldMarkName(child.name)) return child;
                Transform nested = FindHeldMark(child);
                if (nested != null) return nested;
            }
            return null;
        }

        private static bool IsHeldMarkName(string name) =>
            string.Equals(name.Trim(), "mark", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name.Trim(), "Mark Use", StringComparison.OrdinalIgnoreCase);

        private void OpenContextMenu(InventoryEntry entry, PointerEventData pointer)
        {
            if (disposed || ShowingDocuments || draggedEntry != null || entry == null
                || state == null || !Contains(state, entry)) return;
            Select(entry);
            contextMenu.Show(entry, state.IsHeld(entry), pointer, !QuestCategoryFor(entry).HasValue);
        }

        private QuestCategory? QuestCategoryFor(InventoryEntry entry) => questCategory != null
            ? questCategory(entry) : InventoryQuestProtection.CategoryFor(entry, state, null);

        private bool BeginDrag(Slot slot, InventoryEntry entry, PointerEventData pointer)
        {
            if (disposed || ShowingDocuments || entry == null || entry.Type != InventoryEntryType.Item
                || state == null || !Contains(state, entry) || (drop == null && move == null)) return false;
            CancelDrag();
            contextMenu.Close();
            gridScroll.StopMovement();
            Select(entry);
            draggedEntry = entry;
            dragSource = slot.Pointer;
            dragGhost = CreateRect("Inventory Drag Ghost", overlayRoot);
            dragGhost.sizeDelta = slot.Icon != null ? slot.Icon.rectTransform.rect.size : slot.Rect.rect.size;
            if (dragGhost.sizeDelta.x <= 0f || dragGhost.sizeDelta.y <= 0f) dragGhost.sizeDelta = new Vector2(80f, 80f);
            var image = dragGhost.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = entry.Icon;
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, 0.8f);
            image.raycastTarget = false;
            UpdateDrag(pointer);
            return true;
        }

        private void UpdateDrag(PointerEventData pointer)
        {
            if (dragGhost == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, pointer.position,
                pointer.pressEventCamera, out Vector2 local)) dragGhost.localPosition = new Vector3(local.x, local.y, 0f);
        }

        private void EndDrag(InventoryEntry entry, PointerEventData pointer)
        {
            bool active = ReferenceEquals(entry, draggedEntry);
            // Clear all transient state before invoking callbacks that refresh/close the panel.
            CancelDrag();
            if (!active || disposed || ShowingDocuments || !gridBoundary.gameObject.activeInHierarchy
                || state == null || !Contains(state, entry)) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(gridBoundary, pointer.position, pointer.pressEventCamera))
            {
                drop?.Invoke(entry);
                return;
            }
            foreach (Slot slot in gridSlots)
                if (slot.Rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(
                    slot.Rect, pointer.position, pointer.pressEventCamera))
                {
                    move?.Invoke(entry, slot.Index);
                    return;
                }
            // The unused interior belongs to Inventory; removing the ghost reverts the drag.
        }

        private void CancelDrag()
        {
            draggedEntry = null;
            if (dragSource != null) dragSource.CancelDrag();
            dragSource = null;
            if (dragGhost == null) return;
            dragGhost.gameObject.SetActive(false);
            DestroyRuntime(dragGhost.gameObject);
            dragGhost = null;
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
            if (disposed) return;
            OnClosed();
            disposed = true;
            if (contextMenu != null) contextMenu.Dispose();
            missions?.Dispose();
            documentReader?.Dispose();
            foreach (Action remove in removeListeners) remove();
            foreach (Slot slot in gridSlots) if (slot.Rect != null) DestroyRuntime(slot.Rect.gameObject);
            foreach (Slot slot in documentSlots) if (slot.Rect != null) DestroyRuntime(slot.Rect.gameObject);
            gridSlots.Clear();
            documentSlots.Clear();
            removeListeners.Clear();
            if (gridContent != null) DestroyRuntime(gridContent.parent.gameObject);
        }

        private static void DestroyRuntime(GameObject owned)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(owned);
            else UnityEngine.Object.DestroyImmediate(owned);
        }
    }
}
