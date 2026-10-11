using System;
using NUnit.Framework;
using SaksiTerakhir.Persistence;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using SaksiTerakhir.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.Tests
{
    public sealed class GameSessionBootstrapPlayModeTests
    {
        [Test]
        public void MainMenuAwakeFindsEnabledChapterBeforeItsOnEnable()
        {
            Assert.That(Application.isPlaying, Is.True, "This regression needs Unity's actual Awake ordering.");
            Scene previousScene = SceneManager.GetActiveScene();
            float previousTimeScale = Time.timeScale;
            CursorLockMode previousCursorLock = Cursor.lockState;
            bool previousCursorVisible = Cursor.visible;
            Scene fixtureScene = SceneManager.CreateScene("MenuBootstrapTest_" + Guid.NewGuid().ToString("N"));
            GameObject root = null;
            try
            {
                SceneManager.SetActiveScene(fixtureScene);
                root = new GameObject("Menu bootstrap fixture");
                root.SetActive(false);
                // Awake follows this hierarchy during activation: the menu discovers
                // the chapter before Unity enables the chapter's behaviour callbacks.
                var menuObject = new GameObject("Main Menu Panel", typeof(RectTransform));
                menuObject.transform.SetParent(root.transform, false);
                menuObject.AddComponent<MainMenuPresentation>();
                var playerObject = new GameObject("Inactive fixture player");
                playerObject.transform.SetParent(root.transform, false);
                playerObject.SetActive(false);
                playerObject.AddComponent<PlayerController>();
                var chapterObject = new GameObject("Chapter One Story");
                chapterObject.transform.SetParent(root.transform, false);
                ChapterOneDirector chapter = chapterObject.AddComponent<ChapterOneDirector>();

                root.SetActive(true);

                Assert.That(chapterObject.GetComponent<GameSessionController>(), Is.Not.Null,
                    "The menu must bind the enabled chapter before its OnEnable runs.");
                Assert.That(playerObject.GetComponent<GameSessionController>(), Is.Null,
                    "A chapter-backed scene must not fall back to an environment-only player session.");
                Assert.That(chapter.StartupManagedExternally, Is.True);
                Assert.That(chapter.GameplaySuspended, Is.True);
                Assert.That(chapter.Progress, Is.Null,
                    "The chapter's Awake must wait for the session's save-aware initialization.");
            }
            finally
            {
                // Complete synchronously before the next frame: the fixture session
                // never runs Start, reads persistent saves, or begins gameplay.
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
                SceneManager.UnloadSceneAsync(fixtureScene);
                Time.timeScale = previousTimeScale;
                Cursor.lockState = previousCursorLock;
                Cursor.visible = previousCursorVisible;
            }
        }
    }
}
