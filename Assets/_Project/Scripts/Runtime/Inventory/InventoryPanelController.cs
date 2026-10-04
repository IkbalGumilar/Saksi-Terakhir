using SaksiTerakhir.Interaction;
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
        [Header("Kapasitas awal (kolom × baris)")]
        [SerializeField, Range(1, 8)] private int columns = 3;
        [SerializeField, Range(1, 7)] private int rows = 3;
        private InventoryState state;
        private InventoryView view;
        private PlayerController movement;
        private PlayerCameraController cameraController;
        private PlayerInteractor interactor;
        private StoryDialogueController dialogue;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private bool previousMovementLock;
        private bool previousLookLock;
        private bool previousInteractionBlock;
        private GameObject previousSelection;
        private bool resizePending;

        public InventoryState State => state;
        public bool IsOpen { get; private set; }

        public void BindPanel(GameObject panel) => panelRoot = panel;

        private void Awake()
        {
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
                dialogue = root.GetComponentInChildren<StoryDialogueController>(true);
                if (dialogue != null) break;
            }
            state = new InventoryState(Mathf.Clamp(columns, 1, 8), Mathf.Clamp(rows, 1, 8));
            view = new InventoryView(panelRoot, Close);
            state.Changed += Refresh;
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
            view?.Dispose();
        }

        private void Update()
        {
            if (resizePending)
            {
                resizePending = false;
                if (!state.TryResize(columns, rows))
                {
                    columns = state.Columns;
                    rows = state.Rows;
                }
            }
            if (IsOpen && IsStoryBusy()) Close();
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

        public bool TryAdd(InventoryEntry entry) => state != null && state.TryAdd(entry);
        public bool Remove(InventoryEntry entry) => state != null && state.Remove(entry);

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen || view == null || !isActiveAndEnabled || IsStoryBusy()) return;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            previousMovementLock = movement != null && movement.UiMovementLocked;
            previousLookLock = cameraController != null && cameraController.UiLookLocked;
            previousInteractionBlock = interactor != null && interactor.UiInteractionBlocked;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            movement?.SetUiMovementLocked(true);
            cameraController?.SetUiLookLocked(true);
            interactor?.SetUiInteractionBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            IsOpen = true;
            panelRoot.SetActive(true);
            view.OnOpened();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (panelRoot != null) panelRoot.SetActive(false);
            movement?.SetUiMovementLocked(previousMovementLock);
            cameraController?.SetUiLookLocked(previousLookLock);
            interactor?.SetUiInteractionBlocked(previousInteractionBlock);
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
            return (movement != null && movement.StoryMovementLocked)
                || (cameraController != null && cameraController.StoryLookLocked)
                || (dialogue != null && dialogue.IsPlaying);
        }

        private void Refresh() => view.Refresh(state);
    }
}
