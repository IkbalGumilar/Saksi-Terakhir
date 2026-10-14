using System;
using System.Collections.Generic;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SaksiTerakhir.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private ItemDefinitionCatalog itemCatalog;
        [Header("Kapasitas awal (kolom × baris)")]
        [SerializeField, Range(1, 8)] private int columns = 3;
        [SerializeField, Range(1, 8)] private int rows = 3;
        private InventoryState state;
        private InventoryView view;
        private PlayerController movement;
        private PlayerCameraController cameraController;
        private PlayerInteractor interactor;
        private StoryDialogueController dialogue;
        private StoryNotificationView notifications;
        private QuestJournalController questJournal;
        private InventoryHeldItemView heldView;
        private IInventoryItemUseTarget pendingUseTarget;
        private readonly HashSet<IInventoryItemUseTarget> inspectedUseTargets = new HashSet<IInventoryItemUseTarget>();
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private bool previousMovementLock;
        private bool previousLookLock;
        private bool previousInteractionBlock;
        private GameObject previousSelection;
        private bool resizePending;
        private float nextJournalBindAttempt;

        public InventoryState State => state;
        public bool IsOpen { get; private set; }
        public int LastClosedFrame { get; private set; } = -1;
        public event Action<string> FeedbackRequested;
        public event Action<InventoryEntry> UseRequested;

        public void BindPanel(GameObject panel) => panelRoot = panel;

        private void Awake()
        {
            if (itemCatalog == null) itemCatalog = ItemDefinitionCatalog.LoadDefault();
            if (state == null) state = new InventoryState(Mathf.Clamp(columns, 1, 8), Mathf.Clamp(rows, 1, 8));
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (panelRoot == null) panelRoot = InventorySceneBootstrap.FindPanel(gameObject.scene);
            if (input == null || !InventoryView.CanBind(panelRoot))
            {
                Debug.LogWarning("Inventory belum terhubung: periksa panel dan Slot 1 pada kedua halaman.", this);
                enabled = false;
                return;
            }
            movement = GetComponent<PlayerController>();
            cameraController = GetComponent<PlayerCameraController>();
            interactor = GetComponent<PlayerInteractor>();
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (dialogue == null)
                    foreach (StoryDialogueController candidate in root.GetComponentsInChildren<StoryDialogueController>(true))
                        if (candidate.isActiveAndEnabled) { dialogue = candidate; break; }
                if (notifications == null)
                    foreach (StoryNotificationView candidate in root.GetComponentsInChildren<StoryNotificationView>(true))
                        if (candidate.isActiveAndEnabled) { notifications = candidate; break; }
                if (dialogue != null && notifications != null) break;
            }
            heldView = new InventoryHeldItemView(transform);
            view = new InventoryView(panelRoot, Close, entry => TryUse(entry), ToggleHeld,
                entry => TryDrop(entry), (entry, slot) => state.TryMoveToSlot(entry, slot), GetQuestCategory);
            TryBindQuestJournal();
            state.Changed += Refresh;
            LocalizationManager.LanguageChanged += Refresh;
            Refresh();
            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (input == null || view == null) return;
            input.InventoryTogglePressed += Toggle;
            input.PausePressed += Close;
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.InventoryTogglePressed -= Toggle;
                input.PausePressed -= Close;
            }
            Close();
        }

        private void OnDestroy()
        {
            if (state != null) state.Changed -= Refresh;
            LocalizationManager.LanguageChanged -= Refresh;
            if (questJournal != null) questJournal.Changed -= Refresh;
            view?.Dispose();
            heldView?.Dispose();
        }

        private void Update()
        {
            if (questJournal == null && view != null && Time.time >= nextJournalBindAttempt)
            {
                nextJournalBindAttempt = Time.time + 0.5f;
                TryBindQuestJournal();
            }
            if (resizePending)
            {
                resizePending = false;
                if (!state.TryResize(columns, rows))
                {
                    columns = state.Columns;
                    rows = state.Rows;
                }
            }
            if (IsOpen && (IsStoryBusy()
                || (pendingUseTarget != null && !TargetExists(pendingUseTarget)))) Close();
        }

        private void OnValidate()
        {
            columns = Mathf.Clamp(columns, 1, 8);
            rows = Mathf.Clamp(rows, 1, 8);
            // Validation can run while Unity imports assets; create/destroy
            // UI copies on the next Update rather than inside OnValidate.
            if (Application.isPlaying && state != null) resizePending = true;
        }

        public bool TrySetGridSize(int newColumns, int newRows)
        {
            if (state == null || !state.TryResize(newColumns, newRows)) return false;
            columns = newColumns;
            rows = newRows;
            return true;
        }

        public ItemDefinition FindDefinition(string id)
        {
            if (itemCatalog == null) itemCatalog = ItemDefinitionCatalog.LoadDefault();
            return itemCatalog != null ? itemCatalog.Find(id) : null;
        }

        public bool HasItem(string id)
        {
            if (state == null) return false;
            foreach (InventoryEntry entry in state.Items)
                if (string.Equals(entry.Id, id, StringComparison.Ordinal)) return true;
            return false;
        }

        public bool TryAdd(ItemDefinition definition, string instanceId = null) =>
            definition != null && TryAdd(definition.CreateEntry(instanceId));

        public bool TryAdd(InventoryEntry entry)
        {
            if (state == null || entry == null) return false;
            if (state.TryAdd(entry)) return true;
            if (entry.Type == InventoryEntryType.Item && state.Items.Count >= state.Capacity)
                ShowNotice("inventory.full");
            return false;
        }

        public bool Remove(InventoryEntry entry) => state != null && state.Remove(entry);

        public void ToggleHeld(InventoryEntry entry)
        {
            if (!OwnsItem(entry)) return;
            if (state.IsHeld(entry)) state.Release(entry);
            else state.TryHold(entry);
        }

        public bool TryDrop(InventoryEntry entry)
        {
            if (!OwnsItem(entry)) return false;
            if (GetQuestCategory(entry).HasValue)
            {
                ShowNotice("inventory.quest_item_locked");
                return false;
            }
            ItemDefinition definition = entry.Definition != null ? entry.Definition : FindDefinition(entry.Id);
            if (!InventoryWorldItemFactory.TrySpawn(definition, entry, transform, out ItemPickupInteractable pickup))
            {
                ShowNotice("inventory.drop_failed");
                return false;
            }
            if (state.Remove(entry)) return true;
            Destroy(pickup.gameObject);
            return false;
        }

        public QuestCategory? GetQuestCategory(InventoryEntry entry) =>
            InventoryQuestProtection.CategoryFor(entry, state, questJournal != null ? questJournal.Entries : null);

        public bool ReleaseQuestItem(string itemId) => state != null && state.ReleaseQuestItem(itemId);

        public bool RetireStoryItem(string itemId)
        {
            if (state == null || !state.RetireStoryItem(itemId)) return false;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (ItemPickupInteractable pickup in root.GetComponentsInChildren<ItemPickupInteractable>(true))
                {
                    if (pickup.Definition == null || InventoryQuestProtection.CanonicalId(pickup.Definition.Id)
                        != InventoryQuestProtection.CanonicalId(itemId)) continue;
                    // Include previously discarded items. Native identity remains
                    // collected history so the next save cannot respawn it.
                    pickup.RestoreCollected(true);
                    if (pickup.RuntimeSpawned) InventoryWorldItemFactory.DestroyRuntimeObject(pickup.gameObject);
                }
            return true;
        }

        public bool RequestUseForTarget(IInventoryItemUseTarget target)
        {
            if (!TargetExists(target) || state == null || IsStoryBusy()) return false;
            foreach (InventoryEntry entry in state.HeldItems)
            {
                if (!target.AcceptsItem(entry.Id)) continue;
                inspectedUseTargets.Add(target);
                return target.TryUseItem(entry, transform);
            }

            InventoryEntry matchingItem = null;
            foreach (InventoryEntry entry in state.Items)
            {
                if (!target.AcceptsItem(entry.Id)) continue;
                matchingItem = entry;
                break;
            }
            if (matchingItem == null && inspectedUseTargets.Contains(target))
            {
                ShowNotice("inventory.required_item_missing");
                return false;
            }

            Open();
            if (!IsOpen) return false;
            inspectedUseTargets.Add(target);
            pendingUseTarget = target;
            view.ShowPage(InventoryPage.Items);
            view.SelectEntry(matchingItem);
            if (matchingItem == null) ShowNotice("inventory.required_item_missing");
            return true;
        }

        public bool TryUse(InventoryEntry entry)
        {
            if (!OwnsItem(entry)) return false;
            if (pendingUseTarget != null)
            {
                if (!TargetExists(pendingUseTarget))
                {
                    pendingUseTarget = null;
                    ShowNotice("inventory.unavailable");
                    return false;
                }
                if (!pendingUseTarget.AcceptsItem(entry.Id))
                {
                    ShowNotice("inventory.wrong_item");
                    return false;
                }
                if (!pendingUseTarget.TryUseItem(entry, transform)) return false;
                Close();
                return true;
            }
            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            {
                if (component == null || !component.isActiveAndEnabled
                    || !(component is IInventoryItemUseHandler handler) || !handler.HandlesItem(entry.Id)) continue;
                return handler.TryUseItem(entry, transform);
            }
            if (UseRequested != null)
            {
                UseRequested.Invoke(entry);
                return true;
            }
            ShowNotice("inventory.cannot_use_yet");
            return false;
        }

        private bool OwnsItem(InventoryEntry entry) => state != null && entry != null
            && entry.Type == InventoryEntryType.Item && state.GetSlotIndex(entry) >= 0;

        private static bool TargetExists(IInventoryItemUseTarget target) => target != null
            && (!(target is UnityEngine.Object unityTarget) || unityTarget != null);

        private void ShowNotice(string key)
        {
            FeedbackRequested?.Invoke(key);
            if (notifications != null) notifications.Show(key, 3f);
        }

        public void RestoreState(InventoryState restored)
        {
            if (restored == null) throw new System.ArgumentNullException(nameof(restored));
            Close();
            inspectedUseTargets.Clear();
            if (state != null) state.Changed -= Refresh;
            state = restored;
            columns = restored.Columns;
            rows = restored.Rows;
            state.Changed += Refresh;
            Refresh();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen || view == null || !isActiveAndEnabled || IsStoryBusy()
                || (movement != null && movement.UiMovementLocked)) return;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            previousMovementLock = movement != null && movement.UiMovementLocked;
            previousLookLock = cameraController != null && cameraController.UiLookLocked;
            previousInteractionBlock = interactor != null && interactor.UiInteractionBlocked;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (movement != null) movement.SetUiMovementLocked(true);
            if (cameraController != null) cameraController.SetUiLookLocked(true);
            if (interactor != null) interactor.SetUiInteractionBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            IsOpen = true;
            panelRoot.SetActive(true);
            view.OnOpened();
        }

        public void Close()
        {
            pendingUseTarget = null;
            view?.OnClosed();
            if (!IsOpen) return;
            IsOpen = false;
            LastClosedFrame = Time.frameCount;
            if (panelRoot != null) panelRoot.SetActive(false);
            if (movement != null) movement.SetUiMovementLocked(previousMovementLock);
            if (cameraController != null) cameraController.SetUiLookLocked(previousLookLock);
            if (interactor != null) interactor.SetUiInteractionBlocked(previousInteractionBlock);
            // A camera/player being disabled releases the cursor. Closing this
            // panel must not reacquire it afterward, regardless of callback order.
            bool gameplayActive = gameObject.activeInHierarchy
                && (cameraController == null || cameraController.isActiveAndEnabled);
            Cursor.lockState = gameplayActive ? previousCursorLock : CursorLockMode.None;
            Cursor.visible = !gameplayActive || previousCursorVisible;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        }

        private bool IsStoryBusy()
        {
            if (dialogue != null && dialogue.BlocksInventory) return true;
            if (dialogue != null && dialogue.VehicleTravelActive) return false;
            return (movement != null && movement.StoryMovementLocked)
                || (cameraController != null && cameraController.StoryLookLocked)
                || (dialogue != null && dialogue.IsPlaying);
        }

        private void TryBindQuestJournal()
        {
            ChapterOneDirector chapter = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (ChapterOneDirector candidate in root.GetComponentsInChildren<ChapterOneDirector>(true))
                    if (candidate.isActiveAndEnabled) { chapter = candidate; break; }
                if (chapter != null) break;
            }
            // A migration backup can still contain a journal and director. Bind only
            // the active story so inactive legacy state cannot replace the live list.
            if (chapter == null) return;
            if (questJournal != null) questJournal.Changed -= Refresh;
            questJournal = QuestJournalController.GetOrCreate(chapter);
            if (questJournal == null) return;
            questJournal.Changed += Refresh;
            view?.BindJournal(questJournal);
            Refresh();
        }

        private void Refresh()
        {
            if (state == null) return;
            view?.Refresh(state);
            heldView?.Refresh(state);
        }
    }
}
