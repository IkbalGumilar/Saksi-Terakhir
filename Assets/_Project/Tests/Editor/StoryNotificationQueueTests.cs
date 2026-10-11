using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Story;
using TMPro;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class StoryNotificationQueueTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private StoryNotificationView view;
        private GameObject notice;
        private TMP_Text questLabel;
        private GameObject autosave;
        private TMP_Text autosaveLabel;

        [SetUp]
        public void SetUp()
        {
            GameObject saveArea = NewUi("Save Area");
            notice = NewUi("Notificatuion", saveArea.transform);
            questLabel = NewUi("text notificatuion", notice.transform)
                .AddComponent<TextMeshProUGUI>();
            questLabel.gameObject.SetActive(false);
            autosave = NewUi("Auto Save", notice.transform);
            autosaveLabel = autosave.AddComponent<TextMeshProUGUI>();
            view = NewUi("Chapter One UI Bindings", saveArea.transform)
                .AddComponent<StoryNotificationView>();
            SetField("defaultSeconds", 1f);
            SetField("autosaveSeconds", 1f);
            SetField("animationSeconds", 0.2f);
            view.Configure(null, notice, questLabel);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
                if (created[index] != null) UnityEngine.Object.DestroyImmediate(created[index]);
            created.Clear();
        }

        [Test]
        public void CheckpointSavesAfterQuestExitAndThenShowsAuthoredAutosave()
        {
            int saves = 0;
            view.EnqueueCheckpoint("quest.first", () =>
            {
                Assert.That(notice.activeSelf, Is.False, "Quest must finish before saving.");
                saves++;
                return true;
            });
            Assert.That(questLabel.gameObject.activeSelf, Is.True);
            Assert.That(autosave.activeSelf, Is.False);
            Tick(1.3f);
            Assert.That(saves, Is.Zero);
            Tick(0.11f);
            Assert.That(saves, Is.EqualTo(1));
            Assert.That(questLabel.gameObject.activeSelf, Is.False);
            Assert.That(autosave.activeInHierarchy, Is.True);
            Assert.That(autosaveLabel.enabled, Is.False, "Autosave feedback is logo only.");
            Tick(1.4f);
            Assert.That(view.IsBusy, Is.False);
            Assert.That(notice.activeSelf, Is.False);
        }

        [Test]
        public void CheckpointPairsAndOrdinaryNoticesNeverInterruptEachOther()
        {
            var saved = new List<string>();
            view.EnqueueCheckpoint("quest.first", () => { saved.Add("first"); return true; });
            view.Show("story.call.incoming", 1f);
            view.EnqueueCheckpoint("quest.second", () => { saved.Add("second"); return true; });
            Tick(1.41f);
            Assert.That(saved, Is.EqualTo(new[] { "first" }));
            Tick(1.4f);
            Assert.That(questLabel.text, Is.EqualTo("story.call.incoming"));
            Assert.That(autosave.activeSelf, Is.False);
            Tick(1.4f);
            Assert.That(questLabel.text, Is.EqualTo("quest.second"));
            Assert.That(saved, Is.EqualTo(new[] { "first" }));
            Tick(2.8f);
            Assert.That(saved, Is.EqualTo(new[] { "first", "second" }));
            Assert.That(view.IsBusy, Is.False);
        }

        [Test]
        public void FailedCheckpointShowsErrorWithoutAutosaveSuccess()
        {
            view.EnqueueCheckpoint("quest.first", () => false);
            Tick(1.41f);
            Assert.That(questLabel.gameObject.activeSelf, Is.True);
            Assert.That(autosave.activeSelf, Is.False);
            Assert.That(questLabel.text, Does.Not.Contain("story.autosave"));
            Assert.That(questLabel.text, Is.Not.EqualTo("quest.first"));
        }

        [Test]
        public void OptionalSaveWaitsForTheQuestAndBlinksWithoutAnEmptyQuestToast()
        {
            int optionalSaves = 0;
            view.EnqueueCheckpoint("quest.first", () => true);
            view.EnqueueSave(() => { optionalSaves++; return true; });
            Assert.That(optionalSaves, Is.Zero);
            Tick(2.81f);
            Assert.That(optionalSaves, Is.EqualTo(1));
            Assert.That(autosave.activeSelf, Is.True);
            Assert.That(questLabel.gameObject.activeSelf, Is.False);
            Tick(1.41f);
            Assert.That(view.IsBusy, Is.False);
        }

        [Test]
        public void ClearQueueCancelsPendingSavesAndRestoresAuthoredPosition()
        {
            RectTransform rect = notice.GetComponent<RectTransform>();
            Vector2 position = rect.anchoredPosition;
            int saves = 0;
            view.EnqueueCheckpoint("quest.first", () => { saves++; return true; });
            view.EnqueueCheckpoint("quest.second", () => { saves++; return true; });
            Tick(0.1f);
            Assert.That(rect.anchoredPosition, Is.Not.EqualTo(position));
            view.ClearQueue();
            Tick(20f);
            Assert.That(saves, Is.Zero);
            Assert.That(view.IsBusy, Is.False);
            Assert.That(rect.anchoredPosition, Is.EqualTo(position));
            Assert.That(notice.activeSelf, Is.False);
            Assert.That(view.isActiveAndEnabled, Is.True);
        }

        [Test]
        public void ClearingFromSaveCallbackCancelsAutosaveAndRemainingQueue()
        {
            int secondSave = 0;
            view.EnqueueCheckpoint("quest.first", () => { view.ClearQueue(); return true; });
            view.EnqueueCheckpoint("quest.second", () => { secondSave++; return true; });
            Tick(20f);
            Assert.That(secondSave, Is.Zero);
            Assert.That(view.IsBusy, Is.False);
            Assert.That(notice.activeSelf, Is.False);
        }

        [Test]
        public void ConfigurePreservesAuthoredRectAndHiddenViewOwnerStaysActive()
        {
            view.ClearQueue();
            RectTransform rect = notice.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.2f, 0.3f);
            rect.anchorMax = new Vector2(0.4f, 0.5f);
            rect.anchoredPosition = new Vector2(75f, -82f);
            rect.sizeDelta = new Vector2(510f, 95f);
            view.Configure(null, notice, questLabel);
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.2f, 0.3f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.4f, 0.5f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(75f, -82f)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(510f, 95f)));
            Assert.That(view.isActiveAndEnabled, Is.True);
        }

        [Test]
        public void CoordinatorManagedQuestNoticesDoNotDuplicateButCallsStillQueue()
        {
            view.CheckpointsManagedExternally = true;
            MethodInfo onNotice = typeof(StoryNotificationView).GetMethod("OnNotice",
                BindingFlags.Instance | BindingFlags.NonPublic);
            onNotice.Invoke(view, new object[] { "story.quest.started" });
            onNotice.Invoke(view, new object[] { "story.quest.updated" });
            Assert.That(view.IsBusy, Is.False);
            onNotice.Invoke(view, new object[] { "story.call.incoming" });
            Assert.That(view.IsBusy, Is.True);
            Assert.That(questLabel.text, Is.EqualTo("story.call.incoming"));
        }

        [Test]
        public void MissingAutosaveWidgetStillSavesAndContinuesQueueWithoutSuccessText()
        {
            UnityEngine.Object.DestroyImmediate(autosave);
            view.ConfigureAutosave(null);
            int saves = 0;
            view.EnqueueCheckpoint("quest.first", () => { saves++; return true; });
            Tick(1.41f);
            Assert.That(saves, Is.EqualTo(1));
            Assert.That(view.IsBusy, Is.False);
            Assert.That(notice.activeSelf, Is.False);
            view.Show("next.notice", 1f);
            Assert.That(questLabel.text, Is.EqualTo("next.notice"));
        }

        [Test]
        public void ImageOnlyAutosavePulsesWithoutMovingOrShowingQuestText()
        {
            UnityEngine.Object.DestroyImmediate(autosaveLabel);
            autosave.AddComponent<UnityEngine.UI.Image>();
            RectTransform rect = autosave.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(22f, -37f);
            Vector2 originalPosition = rect.anchoredPosition;
            view.ConfigureAutosave(autosave);
            int saves = 0;
            view.EnqueueCheckpoint("quest.first", () => { saves++; return true; });
            Tick(1.41f);
            Assert.That(saves, Is.EqualTo(1));
            Assert.That(autosave.activeInHierarchy, Is.True);
            Assert.That(questLabel.gameObject.activeSelf, Is.False);
            Tick(0.19f);
            float bright = autosave.GetComponent<CanvasGroup>().alpha;
            Tick(0.25f);
            float dim = autosave.GetComponent<CanvasGroup>().alpha;
            Assert.That(dim, Is.LessThan(bright));
            Assert.That(dim, Is.GreaterThan(0f), "Slow blinking should not abruptly disappear.");
            Assert.That(rect.anchoredPosition, Is.EqualTo(originalPosition));
            Assert.That(notice.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Tick(2f);
            Assert.That(view.IsBusy, Is.False);
            Assert.That(autosave.activeSelf, Is.False);
        }

        [Test]
        public void GenericIconIsIdentifiedByItsAutosaveSpriteAndDoesNotShowTheToastBackground()
        {
            UnityEngine.Object.DestroyImmediate(autosave);
            autosave = NewUi("Icon", notice.transform);
            UnityEngine.UI.Image background = notice.AddComponent<UnityEngine.UI.Image>();
            var image = autosave.AddComponent<UnityEngine.UI.Image>();
            var texture = new Texture2D(2, 2);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            sprite.name = "auto save_0";
            try
            {
                image.sprite = sprite;
                view.ConfigureAutosave(null);
                int saves = 0;
                view.EnqueueCheckpoint("quest.first", () => { saves++; return true; });
                Assert.That(background.enabled, Is.True);
                Assert.That(autosave.activeSelf, Is.False);
                Tick(1.6f);
                Assert.That(saves, Is.EqualTo(1));
                Assert.That(autosave.activeInHierarchy, Is.True);
                Assert.That(questLabel.gameObject.activeSelf, Is.False);
                Assert.That(background.enabled, Is.False);
                Tick(1.5f);
                Assert.That(background.enabled, Is.True, "Ordinary toast styling must recover for the next queued quest.");
                Assert.That(notice.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private GameObject NewUi(string name, Transform parent = null)
        {
            var result = new GameObject(name, typeof(RectTransform));
            created.Add(result);
            if (parent != null) result.transform.SetParent(parent, false);
            return result;
        }

        private void SetField(string name, object value) => typeof(StoryNotificationView)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, value);

        private void Tick(float seconds) => typeof(StoryNotificationView)
            .GetMethod("AdvanceQueue", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, new object[] { seconds });
    }
}
