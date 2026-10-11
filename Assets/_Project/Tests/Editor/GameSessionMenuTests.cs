using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Persistence;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using SaksiTerakhir.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SaksiTerakhir.Tests
{
    public sealed class GameSessionMenuTests
    {
        private Scene previousScene;
        private Scene scene;
        private bool reusedTemporaryScene;
        private string sceneAssetPath;
        private string saveDirectory;
        private string savePath;
        private float previousTimeScale;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private GameSessionController session;
        private ChapterOneDirector chapter;
        private PlayerController player;
        private PlayerCameraController cameraController;
        private PlayerInputReader input;
        private PlayerInteractor interactor;
        private MainMenuPresentation menu;
        private Button[] options;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            scene = default;
            reusedTemporaryScene = false;
            sceneAssetPath = null;
            saveDirectory = null;
            previousScene = SceneManager.GetActiveScene();
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Time.timeScale = 1f;
            ResetSessionStartup();
            string id = Guid.NewGuid().ToString("N");
            sceneAssetPath = "Assets/__GameSessionMenuTests_" + id + ".unity";
            bool hasUntitledScene = false;
            for (int index = 0; index < SceneManager.sceneCount; index++)
                hasUntitledScene |= string.IsNullOrEmpty(SceneManager.GetSceneAt(index).path);
            if (hasUntitledScene)
            {
                bool emptyDefault = previousScene.rootCount == 0 && !previousScene.isDirty;
                if (SceneManager.sceneCount == 1 && string.IsNullOrEmpty(previousScene.path)
                    && (emptyDefault || IsRunnerBootstrapScene(previousScene)))
                {
                    // Native EditMode runs use a temporary bootstrap scene with default
                    // camera/light objects. Identify its exact job-owned Scene, never by
                    // object names, before reusing it. The runner restores authored scenes.
                    scene = previousScene;
                    reusedTemporaryScene = true;
                }
                else Assert.Ignore("Menu tests require a separate scene, but an untitled scene contains "
                    + "existing objects or unsaved changes. All open scenes have been left untouched.");
            }
            else scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Assert.That(EditorSceneManager.SaveScene(scene, sceneAssetPath), Is.True);
            // Let the Editor finish registering the saved additive scene. Every fixture
            // root is assigned explicitly, so correctness does not depend on activation.
            yield return null;
            if (EditorSceneManager.GetActiveScene() != scene) EditorSceneManager.SetActiveScene(scene);
            saveDirectory = Path.Combine(Path.GetTempPath(), "saksi-menu-tests-" + id);
            Directory.CreateDirectory(saveDirectory);
            savePath = Path.Combine(saveDirectory, "autosave.json");

            // The player components stay inactive throughout EditMode testing.
            // This avoids running input Awake with an unassigned InputActionAsset.
            var playerObject = NewRoot("Player", false);
            input = playerObject.AddComponent<PlayerInputReader>();
            player = playerObject.AddComponent<PlayerController>();
            cameraController = playerObject.AddComponent<PlayerCameraController>();
            interactor = playerObject.AddComponent<PlayerInteractor>();
            player.transform.position = new Vector3(-3f, 0f, 1f);
            // The session intentionally ignores disabled/inactive legacy chapters.
            // Edit Mode does not run these ordinary MonoBehaviour lifecycle methods.
            var storyObject = NewRoot("Chapter One Story", true);
            chapter = storyObject.AddComponent<ChapterOneDirector>();
            session = storyObject.AddComponent<GameSessionController>();

            GameObject menuObject = NewRoot("Main Menu Panel", true, typeof(RectTransform));
            menu = menuObject.AddComponent<MainMenuPresentation>();
            string[] names = { "Continue", "New Game", "Load Game", "Extra", "Settings", "Exit" };
            options = new Button[names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                var option = new GameObject(names[index], typeof(RectTransform));
                option.transform.SetParent(menuObject.transform, false);
                options[index] = option.AddComponent<Button>();
            }
            menu.Configure(options, null, null, new string[names.Length]);
        }

        [TearDown]
        public void TearDown()
        {
            if (reusedTemporaryScene)
            {
                // This scene was either proven empty and clean or owned by the running
                // test job. Never replace a user's untitled scene to clean up a fixture.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                if (previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            if (!string.IsNullOrEmpty(sceneAssetPath)) AssetDatabase.DeleteAsset(sceneAssetPath);
            if (!string.IsNullOrEmpty(saveDirectory) && Directory.Exists(saveDirectory))
                Directory.Delete(saveDirectory, true);
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            ResetSessionStartup();
        }

        [Test]
        public void MissingAutosaveHidesContinueAndLoadWhileNewGameAndExtraRemainAvailable()
        {
            StartMenu();

            Assert.That(session.MenuOpen, Is.True);
            Assert.That(session.HasAutosave, Is.False);
            Assert.That(options[0].gameObject.activeSelf, Is.False);
            Assert.That(options[2].gameObject.activeSelf, Is.False);
            Assert.That(options[1].gameObject.activeSelf, Is.True);
            Assert.That(options[1].interactable, Is.True);
            Assert.That(options[3].gameObject.activeSelf, Is.True);
            Assert.That(options[3].interactable, Is.True);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
            AssertMenuControlsLocked();

            // Even invoking the hidden Continue event directly must not start an empty run.
            options[0].onClick.Invoke();
            InvokePublicSessionMethod("LoadGame");
            Assert.That(session.MenuOpen, Is.True);
            Assert.That(menu.MenuRoot.activeSelf, Is.True);
            AssertMenuControlsLocked();
        }

        [Test]
        public void InactiveLegacyChapterDoesNotRestoreOrModifyExistingAutosave()
        {
            SaveCheckpoint();
            byte[] originalSave = File.ReadAllBytes(savePath);
            Vector3 originalPosition = player.transform.position;
            chapter.gameObject.SetActive(false);

            StartMenu();

            Assert.That(session.MenuOpen, Is.True);
            Assert.That(session.HasAutosave, Is.False);
            Assert.That(options[0].gameObject.activeSelf, Is.False);
            Assert.That(options[2].gameObject.activeSelf, Is.False);
            Assert.That(options[1].gameObject.activeSelf, Is.True);
            Assert.That(options[1].interactable, Is.True);
            Assert.That(menu.MenuRoot.activeSelf, Is.True);
            Assert.That(player.transform.position, Is.EqualTo(originalPosition));
            Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(originalSave));
            Assert.That(chapter.Progress, Is.Null);
            Assert.That(chapter.GameplaySuspended, Is.False);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(player.UiMovementLocked, Is.True);
            Assert.That(cameraController.UiLookLocked, Is.True);
            Assert.That(interactor.UiInteractionBlocked, Is.True);
            Assert.That(input.enabled, Is.False);

            // A stale listener cannot bypass the hidden Continue option.
            options[0].onClick.Invoke();
            Assert.That(session.MenuOpen, Is.True);
            Assert.That(player.transform.position, Is.EqualTo(originalPosition));
            Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(originalSave));
        }

        [UnityTest]
        public IEnumerator ValidAutosaveRestoresPlayerAndChapterBeforeContinueResumesGameplay()
        {
            GameSaveSnapshot saved = SaveCheckpoint();
            StartMenu();

            Assert.That(session.HasAutosave, Is.True);
            Assert.That(options[0].gameObject.activeSelf, Is.True);
            Assert.That(options[0].interactable, Is.True);
            Assert.That(options[2].gameObject.activeSelf, Is.True);
            Assert.That(options[2].interactable, Is.True);
            Assert.That(options[3].gameObject.activeSelf, Is.True);
            Assert.That(player.transform.position, Is.EqualTo(new Vector3(8.25f, 1.5f, -9.75f)));
            Assert.That(Quaternion.Angle(player.transform.rotation, saved.player.rotation), Is.LessThan(0.001f));
            Assert.That(cameraController.SavedPitch, Is.EqualTo(-18f));
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.AnswerBossCall));
            AssertMenuControlsLocked();

            yield return ContinueMenu();

            Assert.That(session.MenuOpen, Is.False);
            Assert.That(menu.MenuRoot.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(player.UiMovementLocked, Is.False);
            Assert.That(cameraController.UiLookLocked, Is.False);
            Assert.That(interactor.UiInteractionBlocked, Is.False);
            Assert.That(input.enabled, Is.True);
            Assert.That(chapter.GameplaySuspended, Is.False);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.AnswerBossCall));
            Assert.That(player.transform.position, Is.EqualTo(new Vector3(8.25f, 1.5f, -9.75f)));
        }

        [UnityTest]
        public IEnumerator ContinueRestoresPreviouslyOwnedLocksAndTimeScale()
        {
            SaveCheckpoint();
            Time.timeScale = 0.5f;
            player.SetUiMovementLocked(true);
            cameraController.SetUiLookLocked(true);
            interactor.SetUiInteractionBlocked(true);
            input.enabled = false;
            StartMenu();

            yield return ContinueMenu();

            Assert.That(session.MenuOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            Assert.That(player.UiMovementLocked, Is.True);
            Assert.That(cameraController.UiLookLocked, Is.True);
            Assert.That(interactor.UiInteractionBlocked, Is.True);
            Assert.That(input.enabled, Is.False);
        }

        [Test]
        public void LoadStartupReadsLatestProtectedCheckpointAndEntersGameplayDirectly()
        {
            GameSaveSnapshot latest = SaveCheckpoint();
            latest.player = new SavedPose(new Vector3(14f, 0.75f, -32f), Quaternion.Euler(0f, 204f, 0f));
            latest.cameraPitch = 23f;
            latest.savedUtc = "2026-10-11T10:30:00Z";
            ProtectedJsonSaveStore.Save(savePath, JsonUtility.ToJson(latest));
            SetPendingLoadStartup();

            // Simulate the reloaded scene's startup. This deliberately does not invoke
            // an asynchronous scene reload against the runner's temporary fixture.
            StartMenu();

            Assert.That(session.HasAutosave, Is.True);
            Assert.That(session.MenuOpen, Is.False);
            Assert.That(menu.MenuRoot.activeSelf, Is.False);
            Assert.That(player.transform.position, Is.EqualTo(latest.player.position));
            Assert.That(Quaternion.Angle(player.transform.rotation, latest.player.rotation), Is.LessThan(0.001f));
            Assert.That(cameraController.SavedPitch, Is.EqualTo(23f));
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.AnswerBossCall));
            Assert.That(chapter.GameplaySuspended, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(player.UiMovementLocked, Is.False);
            Assert.That(cameraController.UiLookLocked, Is.False);
            Assert.That(interactor.UiInteractionBlocked, Is.False);
            Assert.That(input.enabled, Is.True);
        }

        [Test]
        public void InvalidLoadStartupKeepsMainMenuLockedAndDoesNotStartAnEmptyRun()
        {
            ProtectedJsonSaveStore.Save(savePath, "{\"version\":999}");
            SetPendingLoadStartup();
            LogAssert.Expect(LogType.Warning, new Regex("^Autosave unavailable:"));

            StartMenu();

            Assert.That(session.HasAutosave, Is.False);
            Assert.That(session.MenuOpen, Is.True);
            Assert.That(menu.MenuRoot.activeSelf, Is.True);
            Assert.That(options[0].gameObject.activeSelf, Is.False);
            Assert.That(options[2].gameObject.activeSelf, Is.False);
            Assert.That(options[1].interactable, Is.True);
            Assert.That(player.transform.position, Is.EqualTo(new Vector3(-3f, 0f, 1f)));
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
            AssertMenuControlsLocked();
        }

        [Test]
        public void ManualSaveCannotOverwriteCheckpointWhileMainMenuIsOpen()
        {
            SaveCheckpoint();
            byte[] originalSave = File.ReadAllBytes(savePath);
            StartMenu();
            player.transform.position = new Vector3(91f, 2f, -41f);

            Assert.That(InvokePublicSessionMethod("TrySaveGame"), Is.False);

            Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(originalSave));
            Assert.That(session.MenuOpen, Is.True);
            AssertMenuControlsLocked();
        }

        [UnityTest]
        public IEnumerator ManualSaveCapturesLatestPoseInventoryAndHeldItemsThroughProtectedRepository()
        {
            GameSaveSnapshot initial = SaveCheckpoint();
            StartMenu();
            yield return ContinueMenu();
            var inventory = player.gameObject.AddComponent<InventoryPanelController>();
            var state = new InventoryState(3, 3);
            var key = new InventoryEntry("test.checkpoint.key", "Checkpoint key", InventoryEntryType.Item,
                instanceId: "checkpoint-key-instance");
            Assert.That(state.TryAdd(key), Is.True);
            Assert.That(state.TryMoveToSlot(key, 7), Is.True);
            Assert.That(state.TryHold(key), Is.True);
            Assert.That(state.TryAdd(new InventoryEntry("test.checkpoint.note", "Checkpoint note",
                InventoryEntryType.Document, instanceId: "checkpoint-note-instance")), Is.True);
            inventory.RestoreState(state);
            player.transform.SetPositionAndRotation(new Vector3(21f, 0.25f, -18f), Quaternion.Euler(0f, 245f, 0f));
            cameraController.RestoreSavedPitch(12f);

            Assert.That(InvokePublicSessionMethod("TrySaveGame"), Is.True);
            Assert.That(GameSaveRepository.TryLoadLatest(savePath, out GameSaveSnapshot saved, out string error),
                Is.True, error);

            Assert.That(saved.runId, Is.EqualTo(initial.runId));
            Assert.That(saved.player.position, Is.EqualTo(player.transform.position));
            Assert.That(Quaternion.Angle(saved.player.rotation, player.transform.rotation), Is.LessThan(0.001f));
            Assert.That(saved.cameraPitch, Is.EqualTo(12f));
            Assert.That(saved.chapter.Stage, Is.EqualTo(ChapterOneStage.AnswerBossCall));
            Assert.That(saved.inventory.items, Has.Length.EqualTo(1));
            Assert.That(saved.inventory.items[0].instanceId, Is.EqualTo(key.InstanceId));
            Assert.That(saved.inventory.items[0].slotIndex, Is.EqualTo(7));
            Assert.That(saved.inventory.documents, Has.Length.EqualTo(1));
            Assert.That(saved.inventory.heldInstanceIds, Is.EqualTo(new[] { key.InstanceId }));
            Assert.That(File.ReadAllText(savePath), Does.Not.Contain("test.checkpoint.key"));

            // Reload the actual protected record, rather than the menu's older cached snapshot.
            player.transform.position = Vector3.zero;
            inventory.RestoreState(new InventoryState());
            GameWorldSnapshot.Restore(scene, saved, chapter, player);
            Assert.That(player.transform.position, Is.EqualTo(new Vector3(21f, 0.25f, -18f)));
            Assert.That(inventory.State.Items[0].InstanceId, Is.EqualTo(key.InstanceId));
            Assert.That(inventory.State.GetSlotIndex(inventory.State.Items[0]), Is.EqualTo(7));
            Assert.That(inventory.State.IsHeld(inventory.State.Items[0]), Is.True);
            Assert.That(inventory.State.Documents[0].Id, Is.EqualTo("test.checkpoint.note"));
        }

        [UnityTest]
        public IEnumerator ManualSaveRemainsAvailableForFuturePauseMenu()
        {
            SaveCheckpoint();
            StartMenu();
            yield return ContinueMenu();
            Time.timeScale = 0f;
            player.SetUiMovementLocked(true);
            cameraController.SetUiLookLocked(true);
            player.transform.position = new Vector3(4f, 0.5f, -11f);

            Assert.That(InvokePublicSessionMethod("TrySaveGame"), Is.True);
            Assert.That(GameSaveRepository.TryLoadLatest(savePath, out GameSaveSnapshot saved, out string error),
                Is.True, error);
            Assert.That(saved.player.position, Is.EqualTo(player.transform.position));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(player.UiMovementLocked, Is.True);
            Assert.That(cameraController.UiLookLocked, Is.True);
        }

        [UnityTest]
        public IEnumerator ManualSaveCannotOverwriteCheckpointDuringSceneTransition()
        {
            SaveCheckpoint();
            StartMenu();
            yield return ContinueMenu();
            byte[] originalSave = File.ReadAllBytes(savePath);
            typeof(GameSessionController).GetField("loading", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, true);
            player.transform.position = Vector3.one * 42f;

            Assert.That(InvokePublicSessionMethod("TrySaveGame"), Is.False);
            Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(originalSave));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MalformedAutosaveCannotEnableContinueOrReleaseMenuLocks(bool protectedPayload)
        {
            if (protectedPayload) ProtectedJsonSaveStore.Save(savePath, "{\"version\":999}");
            else File.WriteAllText(savePath, "{\"version\":1}");
            LogAssert.Expect(LogType.Warning, new Regex("^Autosave unavailable:"));
            StartMenu();

            Assert.That(session.HasAutosave, Is.False);
            Assert.That(options[0].gameObject.activeSelf, Is.False);
            Assert.That(options[2].gameObject.activeSelf, Is.False);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
            session.ContinueGame();
            Assert.That(session.MenuOpen, Is.True);
            Assert.That(menu.MenuRoot.activeSelf, Is.True);
            Assert.That(player.transform.position, Is.EqualTo(new Vector3(-3f, 0f, 1f)));
            AssertMenuControlsLocked();
        }

        private void StartMenu()
        {
            session.Configure(menu, savePath);
            typeof(GameSessionController).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, null);
        }

        private object InvokePublicSessionMethod(string name)
        {
            MethodInfo method = typeof(GameSessionController).GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "The session must expose " + name + " for the menu or future pause UI.");
            return method.Invoke(session, null);
        }

        private static void SetPendingLoadStartup()
        {
            Type startup = typeof(GameSessionController).GetNestedType("Startup", BindingFlags.NonPublic);
            Assert.That(startup, Is.Not.Null);
            object load = Enum.Parse(startup, "LoadGame");
            typeof(GameSessionController).GetField("pendingStartup", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, load);
        }

        private IEnumerator ContinueMenu()
        {
            // The fixture deliberately keeps gameplay components inactive in Edit Mode.
            // Drive the real transition without starting a MonoBehaviour coroutine there.
            var transition = (IEnumerator)typeof(GameSessionController)
                .GetMethod("ContinueFromMenu", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, null);
            Assert.That(transition.MoveNext(), Is.True);
            AssertMenuControlsLocked();
            Assert.That(menu.MenuRoot.activeSelf, Is.True);
            // EditMode has no gameplay clock: the positive-duration fade cannot
            // advance when unscaledDeltaTime is zero. Complete the same menu fade
            // immediately to verify the real control handoff, not its duration.
            yield return menu.FadeOut(0f);
            while (transition.MoveNext()) yield return transition.Current;
        }

        private void AssertMenuControlsLocked()
        {
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(player.UiMovementLocked, Is.True);
            Assert.That(cameraController.UiLookLocked, Is.True);
            Assert.That(interactor.UiInteractionBlocked, Is.True);
            Assert.That(input.enabled, Is.False);
            Assert.That(chapter.GameplaySuspended, Is.True);
        }

        private GameSaveSnapshot SaveCheckpoint()
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.True);
            var saved = new GameSaveSnapshot
            {
                runId = Guid.NewGuid().ToString("D"),
                scenePath = scene.path,
                savedUtc = "2026-10-04T12:00:00Z",
                chapter = progress,
                player = new SavedPose(new Vector3(8.25f, 1.5f, -9.75f), Quaternion.Euler(0f, 112f, 0f)),
                cameraPitch = -18f
            };
            Assert.That(saved.Validate(out string error), Is.True, error);
            ProtectedJsonSaveStore.Save(savePath, JsonUtility.ToJson(saved));
            return saved;
        }

        private GameObject NewRoot(string name, bool active, params Type[] components)
        {
            var result = new GameObject(name, components);
            result.SetActive(active);
            if (result.scene != scene) SceneManager.MoveGameObjectToScene(result, scene);
            Assert.That(result.scene, Is.EqualTo(scene), "Fixture objects must belong to the isolated test scene.");
            return result;
        }

        private static void ResetSessionStartup() => typeof(GameSessionController)
            .GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        private static bool IsRunnerBootstrapScene(Scene candidate)
        {
            // TestJobDataHolder is internal to the installed Test Framework. If that
            // implementation changes, fail closed and retain the untitled-scene guard.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type holderType = assembly.GetType("UnityEditor.TestTools.TestRunner.TestRun.TestJobDataHolder");
                if (holderType == null) continue;
                PropertyInfo instance = holderType.GetProperty("instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                object holder = instance?.GetValue(null);
                if (holder == null) return false;
                var runs = holderType.GetField("TestRuns", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(holder) as IEnumerable;
                if (runs == null) return false;
                foreach (object run in runs)
                {
                    Type runType = run.GetType();
                    object running = runType.GetField("isRunning")?.GetValue(run);
                    object bootstrap = runType.GetField("InitTestScene")?.GetValue(run);
                    if (running is bool active && active && bootstrap is Scene owned && owned == candidate)
                        return true;
                }
                return false;
            }
            return false;
        }
    }
}
