using System;
using System.Collections;
using System.IO;
using SaksiTerakhir.Environment;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using SaksiTerakhir.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SaksiTerakhir.Persistence
{
    /// <summary>Owns the menu preview, scene startup and ordered quest checkpoints.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-9000)]
    public sealed class GameSessionController : MonoBehaviour
    {
        private enum Startup { Menu, NewGame, LoadGame }
        private static Startup pendingStartup;
        private MainMenuPresentation menu;
        private ChapterOneDirector chapter;
        private PlayerController player;
        private PlayerCameraController cameraController;
        private PlayerInputReader input;
        private PlayerInteractor interactor;
        private StoryNotificationView notifications;
        private QuestTrackerView tracker;
        private TimeOfDayApplier timeOfDay;
        private GameSaveSnapshot loaded;
        private Button continueButton;
        private Button newGameButton;
        private Button loadGameButton;
        private Button settingsButton;
        private SettingsMenuPresentation settingsMenu;
        private bool inputWasEnabled;
        private bool previousMovementLock;
        private bool previousLookLock;
        private bool previousInteractionBlock;
        private bool subscribed;
        private bool loading;
        private bool locksCaptured;
        private float previousTimeScale = 1;
        private string runId;
        private string lastQuestKey;
        private string autosavePath;
        private bool retryCheckpoint;
        private float retryAt;
        private Coroutine continueTransition;
        private Coroutine settingsSlide;

        public bool MenuOpen { get; private set; }
        public bool HasAutosave => loaded != null;
        public string AutosavePath => autosavePath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => pendingStartup = Startup.Menu;

        public static void Bootstrap(MainMenuPresentation presentation)
        {
            Scene scene = presentation.gameObject.scene;
            ChapterOneDirector director = FindActiveChapter(scene);
            PlayerController scenePlayer = GameWorldSnapshot.Find<PlayerController>(scene);
            if (scenePlayer == null) return;
            GameObject owner = director != null ? director.gameObject : scenePlayer.gameObject;
            var controller = owner.GetComponent<GameSessionController>();
            if (controller == null) controller = owner.AddComponent<GameSessionController>();
            controller.Configure(presentation);
        }

        private static ChapterOneDirector FindActiveChapter(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (ChapterOneDirector candidate in root.GetComponentsInChildren<ChapterOneDirector>(true))
                    // Menu Awake precedes chapter OnEnable. isActiveAndEnabled is still
                    // false during that registration window, even for the live chapter.
                    if (candidate.enabled && candidate.gameObject.activeInHierarchy) return candidate;
            return null;
        }

        public void Configure(MainMenuPresentation presentation, string path = null)
        {
            if (menu != null) return;
            menu = presentation;
            Scene scene = gameObject.scene;
            chapter = FindActiveChapter(scene);
            player = GameWorldSnapshot.Find<PlayerController>(scene);
            notifications = GameWorldSnapshot.Find<StoryNotificationView>(scene);
            tracker = GameWorldSnapshot.Find<QuestTrackerView>(scene);
            timeOfDay = GameWorldSnapshot.Find<TimeOfDayApplier>(scene);
            autosavePath = path ?? Path.Combine(Application.persistentDataPath, "autosave.json");
            if (player == null || menu == null)
            { Debug.LogError("Main menu needs a player.", this); return; }
            if (chapter != null)
            {
                chapter.StartupManagedExternally = true;
                chapter.SuspendGameplay();
            }
            if (notifications != null) notifications.CheckpointsManagedExternally = true;
            MenuOpen = true;
            previousTimeScale = Time.timeScale > 0 ? Time.timeScale : 1;
            Time.timeScale = 0;
        }

        private void Start()
        {
            if (player == null || menu == null) return;
            cameraController = player.GetComponent<PlayerCameraController>();
            input = player.GetComponent<PlayerInputReader>();
            interactor = player.GetComponent<PlayerInteractor>();
            CaptureAndLockControls();
            notifications?.ClearQueue();
            if (notifications == null && chapter != null)
            {
                // Keep persistence ordering even when a scene has no authored toast component.
                notifications = gameObject.AddComponent<StoryNotificationView>();
                notifications.CheckpointsManagedExternally = true;
            }
            Button[] options = menu.Options;
            if (options != null && options.Length > 1)
            {
                continueButton = options[0];
                newGameButton = options[1];
                continueButton?.onClick.AddListener(ContinueGame);
                newGameButton?.onClick.AddListener(NewGame);
            }
            if (options != null && options.Length > 2)
            {
                loadGameButton = options[2];
                loadGameButton?.onClick.AddListener(LoadGame);
            }
            if (options != null && options.Length > 3)
            {
                settingsButton = options[3];
                settingsButton?.onClick.AddListener(OpenSettings);
            }
            if (chapter != null)
            {
                chapter.ProgressChanged += OnProgressChanged;
                subscribed = true;
            }
            Startup startup = pendingStartup;
            pendingStartup = Startup.Menu;
            if (startup == Startup.NewGame) NpcGreetingMemory.Clear();
            runId = Guid.NewGuid().ToString("D");
            if (chapter == null)
            {
                // Environment assembly can run before its new story cast is connected.
                // Legacy chapter saves must not move the player back into the old layout.
                tracker?.Render(null, null);
                menu.SetSaveAvailable(false);
                if (startup == Startup.NewGame) EnterGameplay(true);
                else
                {
                    timeOfDay?.ApplyInitialMenu();
                    cameraController?.BeginMenuPreview();
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                return;
            }
            if (startup == Startup.NewGame)
            {
                chapter.InitializeNewGame();
                menu.SetSaveAvailable(false);
                lastQuestKey = null;
                EnterGameplay(true);
                return;
            }

            chapter.InitializeNewGame();
            GameSaveSnapshot initial = GameWorldSnapshot.Capture(gameObject.scene, chapter, player, runId);
            if (GameSaveRepository.TryLoadLatest(autosavePath, out GameSaveSnapshot snapshot, out string error)
                && snapshot.scenePath == gameObject.scene.path)
            {
                try
                {
                    GameWorldSnapshot.Restore(gameObject.scene, snapshot, chapter, player);
                    loaded = snapshot;
                    runId = snapshot.runId;
                    if (!string.IsNullOrEmpty(error)) Debug.LogWarning(error, this);
                }
                catch (Exception exception)
                {
                    Debug.LogError("Autosave preview failed: " + exception.Message, this);
                    // A failed restoration may already have replaced chapter or inventory data.
                    GameWorldSnapshot.Restore(gameObject.scene, initial, chapter, player);
                }
            }
            else if (!string.IsNullOrEmpty(error)) Debug.LogWarning("Autosave unavailable: " + error, this);
            if (loaded == null) chapter.InitializeNewGame();
            lastQuestKey = CurrentQuestKey();
            tracker?.Render(chapter.CurrentQuest, chapter.Progress);
            menu.SetSaveAvailable(HasAutosave);
            if (startup == Startup.LoadGame)
            {
                if (HasAutosave)
                {
                    EnterGameplay();
                    return;
                }
                menu.ShowStatus(Text("Simpanan gagal dimuat. Silakan mulai permainan baru.",
                    "Could not load the save. You can start a new game."));
            }
            if (HasAutosave) SetContinueSummary();
            else timeOfDay?.ApplyInitialMenu();
            cameraController?.BeginMenuPreview();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ContinueGame()
        {
            if (!MenuOpen || loading || loaded == null) return;
            loading = true;
            continueTransition = StartCoroutine(ContinueFromMenu());
        }

        private IEnumerator ContinueFromMenu()
        {
            loading = true;
            // Stop the menu scan at its currently visible heading without rolling back
            // the supported position or bringing back the save's steep camera pitch.
            if (cameraController != null) cameraController.EndMenuPreview();
            yield return menu.FadeOut(0.45f);
            continueTransition = null;
            loading = false;
            if (menu == null || !menu.isActiveAndEnabled || !menu.MenuRoot.activeInHierarchy) yield break;
            EnterGameplay();
        }

        public void NewGame()
        {
            if (!MenuOpen || loading) return;
            StartCoroutine(ReloadScene(Startup.NewGame));
        }

        public void LoadGame()
        {
            if (!MenuOpen || loading || !HasAutosave) return;
            // Check the current file before replacing the preview. Startup reads it again
            // and retains its rollback if the file changes or restoration fails.
            if (!GameSaveRepository.TryLoadLatest(autosavePath, out GameSaveSnapshot snapshot, out _)
                || snapshot.scenePath != gameObject.scene.path)
            {
                menu.ShowStatus(Text("Simpanan gagal dimuat.", "Could not load the save."));
                return;
            }
            StartCoroutine(ReloadScene(Startup.LoadGame));
        }

        public void OpenSettings()
        {
            if (!MenuOpen || loading || settingsSlide != null) return;
            if (settingsMenu == null)
            {
                settingsMenu = GameWorldSnapshot.Find<SettingsMenuPresentation>(gameObject.scene);
            }
            if (settingsMenu == null)
            {
                Debug.LogWarning("[GameSessionController] SettingsMenuPresentation not found in scene.");
                return;
            }

            settingsSlide = StartCoroutine(SlideBetweenMenuAndSettings(true));
        }

        private void CloseSettingsAnimated()
        {
            if (settingsSlide != null) return;
            settingsSlide = StartCoroutine(SlideBetweenMenuAndSettings(false));
        }

        /// <summary>Main menu exits left while settings enters from the right; closing reverses it.</summary>
        private IEnumerator SlideBetweenMenuAndSettings(bool opening)
        {
            GameObject menuRoot = menu.MenuRoot;
            var menuRect = menuRoot.GetComponent<RectTransform>();
            var settingsRect = settingsMenu.GetComponent<RectTransform>();
            float width = menuRect.parent is RectTransform parentRect ? parentRect.rect.width : 1920f;
            if (opening)
            {
                settingsMenu.Open(CloseSettingsAnimated);
                settingsMenu.SetInteractable(false);
                settingsRect.anchoredPosition = Vector2.right * width;
            }
            else
            {
                menuRoot.SetActive(true);
                menu.RefreshRuntimeSelection();
                menuRect.anchoredPosition = Vector2.left * width;
            }

            CanvasGroup menuGroup = menuRoot.GetComponent<CanvasGroup>();
            if (menuGroup == null) menuGroup = menuRoot.AddComponent<CanvasGroup>();
            menuGroup.interactable = false;
            float menuFrom = menuRect.anchoredPosition.x;
            float menuTo = opening ? -width : 0f;
            float settingsFrom = settingsRect.anchoredPosition.x;
            float settingsTo = opening ? 0f : width;
            const float seconds = 0.35f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                menuRect.anchoredPosition = new Vector2(Mathf.Lerp(menuFrom, menuTo, k), 0f);
                settingsRect.anchoredPosition = new Vector2(Mathf.Lerp(settingsFrom, settingsTo, k), 0f);
                yield return null;
            }
            menuRect.anchoredPosition = new Vector2(menuTo, 0f);
            settingsRect.anchoredPosition = new Vector2(settingsTo, 0f);

            if (menuGroup != null) menuGroup.interactable = true;
            if (opening)
            {
                menuRoot.SetActive(false);
                menuRect.anchoredPosition = Vector2.zero;
                settingsMenu.SetInteractable(true);
            }
            else
            {
                settingsMenu.gameObject.SetActive(false);
                settingsRect.anchoredPosition = Vector2.zero;
                if (settingsButton != null && settingsButton.isActiveAndEnabled && settingsButton.interactable)
                    settingsButton.Select();
            }
            settingsSlide = null;
        }

        private IEnumerator ReloadScene(Startup startup)
        {
            loading = true;
            notifications?.ClearQueue();
            bool newGameEnabled = newGameButton != null && newGameButton.interactable;
            bool continueEnabled = continueButton != null && continueButton.interactable;
            bool loadEnabled = loadGameButton != null && loadGameButton.interactable;
            if (newGameButton != null) newGameButton.interactable = false;
            if (continueButton != null) continueButton.interactable = false;
            if (loadGameButton != null) loadGameButton.interactable = false;
            pendingStartup = startup;
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(gameObject.scene.path); }
            catch (Exception exception)
            { Debug.LogError("Game scene could not load: " + exception.Message, this); }
            if (operation == null)
            {
                pendingStartup = Startup.Menu;
                loading = false;
                if (newGameButton != null) newGameButton.interactable = newGameEnabled;
                if (continueButton != null) continueButton.interactable = continueEnabled;
                if (loadGameButton != null) loadGameButton.interactable = loadEnabled;
                menu.ShowStatus(startup == Startup.NewGame
                    ? Text("Permainan baru gagal dimuat.", "Could not start a new game.")
                    : Text("Simpanan gagal dimuat.", "Could not load the save."));
                yield break;
            }
            while (!operation.isDone) yield return null;
        }

        private void EnterGameplay(bool newGame = false)
        {
            notifications?.ClearQueue();
            if (newGame) timeOfDay?.ApplyNewGame();
            else timeOfDay?.Apply();
            cameraController?.EndMenuPreview();
            MenuOpen = false;
            menu.MenuRoot.SetActive(false);
            ReleaseControls();
            cameraController?.SetCursorLocked(true);
            chapter?.ResumeGameplay();
            if (chapter != null) OnProgressChanged(chapter.Progress);
        }

        private void OnProgressChanged(ChapterOneProgress progress)
        {
            if (chapter == null) return;
            // The persistent quest must be current before its transient notice begins.
            tracker?.Render(chapter.CurrentQuest, progress);
            if (MenuOpen || loading || progress == null) return;
            string key = CurrentQuestKey();
            if (string.IsNullOrEmpty(key) || key == lastQuestKey) return;
            bool first = string.IsNullOrEmpty(lastQuestKey);
            lastQuestKey = key;
            QueueCheckpoint(first);
        }

        private void Update()
        {
            if (!retryCheckpoint || MenuOpen || loading || notifications == null
                || notifications.IsBusy || Time.unscaledTime < retryAt) return;
            retryCheckpoint = false;
            QueueCheckpoint(false);
        }

        private void QueueCheckpoint(bool first)
        {
            if (chapter == null || player == null || notifications == null) return;
            // Capture only after the quest animation, once the quest event's world changes
            // have finished. A queued old notice must never overwrite newer progress.
            notifications.EnqueueCheckpoint(first ? "story.quest.started" : "story.quest.updated", TrySaveGame);
        }

        /// <summary>Writes the current run; a future pause menu can use the same save operation.</summary>
        public void RequestAutosave()
        {
            if (MenuOpen || loading || chapter == null || chapter.GameplaySuspended) return;
            if (notifications != null) notifications.EnqueueSave(TrySaveGame);
            else TrySaveGame();
        }

        /// <summary>Writes immediately, including the final checkpoint before leaving Play Mode.</summary>
        public bool TrySaveGame()
        {
            if (MenuOpen || loading || chapter == null || player == null || chapter.GameplaySuspended) return false;
            try
            {
                GameSaveSnapshot checkpoint = GameWorldSnapshot.Capture(gameObject.scene, chapter, player, runId);
                if (!checkpoint.Validate(out string error)) throw new InvalidOperationException(error);
                string payload = JsonUtility.ToJson(checkpoint);
                ProtectedJsonSaveStore.Save(autosavePath, payload);
                loaded = JsonUtility.FromJson<GameSaveSnapshot>(payload);
                retryCheckpoint = false;
                return true;
            }
            catch (Exception exception)
            {
                retryCheckpoint = true;
                retryAt = Time.unscaledTime + 15f;
                Debug.LogError("Autosave failed: " + exception.Message, this);
                return false;
            }
        }

        private string CurrentQuestKey() => chapter != null
            ? MainQuestCheckpointKey.Create(chapter.Progress,
                chapter.CurrentQuest?.Id, chapter.CurrentQuest?.Category == QuestCategory.Main)
            : string.Empty;

        private void SetContinueSummary()
        {
            string quest = chapter != null && chapter.CurrentQuest != null
                ? LocalizationManager.Get(chapter.CurrentQuest.TitleKey) : string.Empty;
            menu.SetContinueDescription(Text("Melanjutkan simpanan terakhir", "Resume the latest autosave")
                + (string.IsNullOrEmpty(quest) ? "." : ": " + quest));
        }

        private void CaptureAndLockControls()
        {
            locksCaptured = true;
            inputWasEnabled = input != null && input.enabled;
            previousMovementLock = player.UiMovementLocked;
            previousLookLock = cameraController != null && cameraController.UiLookLocked;
            previousInteractionBlock = interactor != null && interactor.UiInteractionBlocked;
            player.GetComponent<InventoryPanelController>()?.Close();
            player.SetUiMovementLocked(true);
            cameraController?.SetUiLookLocked(true);
            interactor?.SetUiInteractionBlocked(true);
            if (inputWasEnabled) input.enabled = false;
        }

        private void ReleaseControls()
        {
            if (!locksCaptured) return;
            locksCaptured = false;
            player?.SetUiMovementLocked(previousMovementLock);
            cameraController?.SetUiLookLocked(previousLookLock);
            interactor?.SetUiInteractionBlocked(previousInteractionBlock);
            if (input != null) input.enabled = inputWasEnabled;
            Time.timeScale = previousTimeScale;
        }

        private void OnDisable()
        {
            if (continueTransition != null)
            {
                StopCoroutine(continueTransition);
                continueTransition = null;
                loading = false;
            }
            if (menu != null) menu.CancelFade();
            if (cameraController != null) cameraController.EndMenuPreview(false);
        }

        private void OnDestroy()
        {
            if (cameraController != null) cameraController.EndMenuPreview(false);
            if (subscribed && chapter != null) chapter.ProgressChanged -= OnProgressChanged;
            continueButton?.onClick.RemoveListener(ContinueGame);
            newGameButton?.onClick.RemoveListener(NewGame);
            loadGameButton?.onClick.RemoveListener(LoadGame);
            notifications?.ClearQueue();
            ReleaseControls();
        }

        private static string Text(string indonesian, string english)
            => LocalizationManager.CurrentLanguage == GameLanguage.Indonesian ? indonesian : english;
    }
}
