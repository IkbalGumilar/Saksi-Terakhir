using System;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerPauseController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerController movement;
        [SerializeField] private PlayerCameraController cameraController;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private InventoryPanelController inventory;
        [SerializeField] private StoryDialogueController dialogue;
        [SerializeField] private GameObject panelRoot;

        private float previousTimeScale;
        private bool previousMovementLock;
        private bool previousLookLock;
        private bool previousInteractionBlock;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        public event Action PauseRequested;
        public event Action<bool> PauseChanged;
        public bool IsPaused { get; private set; }

        public void BindPanel(GameObject panel)
        {
            Resume();
            panelRoot = panel;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void Awake()
        {
            ResolveDependencies();
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            ResolveDependencies();
            if (input != null) input.PausePressed += OnPausePressed;
        }

        private void OnDisable()
        {
            if (input != null) input.PausePressed -= OnPausePressed;
            Resume();
        }

        private void OnPausePressed()
        {
            if (IsPaused) { Resume(); return; }
            if (inventory != null)
            {
                if (inventory.IsOpen) { inventory.Close(); return; }
                if (inventory.LastClosedFrame == Time.frameCount) return;
            }
            if (!CanPause()) return;
            PauseRequested?.Invoke();
            // A future menu can handle the request. An absent panel must not
            // freeze the game without giving the player a visible way to resume.
            if (!IsPaused && panelRoot != null) TryPause();
        }

        public bool TryPause()
        {
            if (!enabled || IsPaused || !CanPause()) return false;
            inventory?.Close();
            previousTimeScale = Time.timeScale;
            previousMovementLock = movement != null && movement.UiMovementLocked;
            previousLookLock = cameraController != null && cameraController.UiLookLocked;
            previousInteractionBlock = interactor != null && interactor.UiInteractionBlocked;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            IsPaused = true;
            movement?.SetUiMovementLocked(true);
            cameraController?.SetUiLookLocked(true);
            interactor?.SetUiInteractionBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
            if (panelRoot != null) panelRoot.SetActive(true);
            PauseChanged?.Invoke(true);
            return true;
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            if (panelRoot != null) panelRoot.SetActive(false);
            movement?.SetUiMovementLocked(previousMovementLock);
            cameraController?.SetUiLookLocked(previousLookLock);
            interactor?.SetUiInteractionBlocked(previousInteractionBlock);
            Time.timeScale = previousTimeScale;
            bool gameplayActive = gameObject.activeInHierarchy
                && (cameraController == null || cameraController.isActiveAndEnabled);
            Cursor.lockState = gameplayActive ? previousCursorLock : CursorLockMode.None;
            Cursor.visible = !gameplayActive || previousCursorVisible;
            PauseChanged?.Invoke(false);
        }

        private bool CanPause()
        {
            if (Time.timeScale <= 0f || (dialogue != null && dialogue.BlocksPause)) return false;
            bool inventoryOpen = inventory != null && inventory.IsOpen;
            if (!inventoryOpen && ((movement != null && movement.UiMovementLocked)
                || (cameraController != null && cameraController.UiLookLocked))) return false;
            if (dialogue != null && dialogue.VehicleTravelActive) return true;
            return (movement == null || !movement.StoryMovementLocked)
                && (cameraController == null || !cameraController.StoryLookLocked);
        }

        private void ResolveDependencies()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (movement == null) movement = GetComponent<PlayerController>();
            if (cameraController == null) cameraController = GetComponent<PlayerCameraController>();
            if (interactor == null) interactor = GetComponent<PlayerInteractor>();
            if (inventory == null) inventory = GetComponent<InventoryPanelController>();
            if (dialogue != null) return;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (StoryDialogueController candidate in root.GetComponentsInChildren<StoryDialogueController>(true))
                    if (candidate.isActiveAndEnabled) { dialogue = candidate; return; }
            }
        }
    }
}
