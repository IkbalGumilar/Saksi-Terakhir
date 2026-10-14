using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Story;
using TMPro;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class InventoryMissionViewTests
    {
        private GameObject page;
        private GameObject story;
        private QuestDefinition main;
        private QuestDefinition side;
        private QuestDefinition optional;
        private QuestDefinition unavailable;
        private QuestJournalCatalog catalog;
        private QuestJournalController journal;
        private InventoryMissionView view;
        private RectTransform content;
        private QuestJournalEntry displayedEntry;

        [SetUp]
        public void SetUp()
        {
            page = Rect("Quest List", null, new Vector2(800f, 600f)).gameObject;
            RectTransform scroll = Rect("Scroll View", page.transform, new Vector2(800f, 600f));
            RectTransform viewport = Rect("Viewport", scroll, new Vector2(780f, 600f));
            content = Rect("Content", viewport, new Vector2(780f, 300f));
            var scrollRect = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            string[] categories = { "Main Quest", "Side Quest", "Optional Quest" };
            for (int i = 0; i < categories.Length; i++)
            {
                Rect(categories[i], content, new Vector2(300f, 50f)).gameObject.AddComponent<TextMeshProUGUI>();
                RectTransform template = Rect("Slot " + (i + 1), content, new Vector2(780f, 100f));
                template.gameObject.AddComponent<UnityEngine.UI.Image>();
                Rect("Icon", template, new Vector2(50f, 50f)).gameObject.AddComponent<UnityEngine.UI.Image>();
                Rect("'Title '", template, new Vector2(570f, 50f)).gameObject.AddComponent<TextMeshProUGUI>();
                Rect("Description", template, new Vector2(570f, 50f)).gameObject.AddComponent<TextMeshProUGUI>();
            }
            main = Definition("main", QuestCategory.Main);
            side = Definition("side", QuestCategory.Side);
            optional = Definition("optional", QuestCategory.Optional);
            unavailable = Definition("unavailable", QuestCategory.Side);
            catalog = ScriptableObject.CreateInstance<QuestJournalCatalog>();
            catalog.Configure(new[] { main, side, optional, unavailable });
            story = new GameObject("Mission Test Story");
            var director = story.AddComponent<ChapterOneDirector>();
            director.StartupManagedExternally = true;
            SetField(director, "progress", new ChapterOneProgress());
            SetField(director, "quests", new[] { main });
            journal = story.AddComponent<QuestJournalController>();
            journal.Configure(director, catalog);
            Assert.That(journal.TryAccept(side), Is.True);
            Assert.That(journal.TryAccept(optional), Is.True);
            view = new InventoryMissionView(page, entry => displayedEntry = entry);
            view.Bind(journal);
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            foreach (UnityEngine.Object owned in new UnityEngine.Object[] { page, story, main, side, optional, unavailable, catalog })
                if (owned != null) UnityEngine.Object.DestroyImmediate(owned);
        }

        [Test]
        public void AcquiredRowsReuseTemplatesAndSelectionOnlyTracksAnActiveSupportingQuest()
        {
            Assert.That(content.Find("Mission unavailable"), Is.Null);
            Assert.That(Row("main").GetComponent<UnityEngine.UI.Button>().interactable, Is.True);
            Assert.That(displayedEntry.Definition, Is.SameAs(main));
            Assert.That(Row("optional").Find("'Title '").GetComponent<TMP_Text>().text, Is.EqualTo("optional.title"));
            Assert.That(Row("optional").Find("Icon").GetComponent<UnityEngine.UI.Image>().color,
                Is.EqualTo(QuestTrackerView.OptionalDotColor));
            Assert.That(Row("side").Find("Icon").GetComponent<UnityEngine.UI.Image>().color,
                Is.EqualTo(QuestTrackerView.SideDotColor));
            Row("optional").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(journal.SelectedQuestId, Is.EqualTo("optional"));
            Row("side").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(journal.SelectedQuestId, Is.EqualTo("side"));
            Assert.That(content.Find("Slot 1").gameObject.activeSelf, Is.False);
            Assert.That(content.Find("Slot 2").gameObject.activeSelf, Is.False);
            Assert.That(content.Find("Slot 3").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void RefreshDoesNotDuplicateRowsAndCompletedDetailsRemainReadable()
        {
            view.Refresh();
            view.Refresh();
            Assert.That(content.childCount, Is.EqualTo(9), "Three headings and templates plus three owned rows.");
            Row("optional").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(journal.TryFinish("optional"), Is.True);
            Assert.That(journal.SelectedQuestId, Is.Empty);
            Assert.That(Row("optional").GetComponent<UnityEngine.UI.Button>().interactable, Is.True);
            Assert.That(displayedEntry.Status, Is.EqualTo(StoryQuestStatus.Completed));
            Assert.That(Row("optional").Find("'Title '").GetComponent<TMP_Text>().text, Does.Contain("<s>"));
            Assert.That(content.childCount, Is.EqualTo(9));
            Assert.That(content.sizeDelta.y, Is.GreaterThanOrEqualTo(600f));
        }

        [Test]
        public void ReadingMainOrFinishedMissionPreservesTheTrackedSupportingQuest()
        {
            Row("side").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Row("main").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(displayedEntry.Definition, Is.SameAs(main));
            Assert.That(journal.SelectedQuestId, Is.EqualTo("side"));
            Assert.That(journal.TryFinish("optional"), Is.True);
            Row("optional").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(displayedEntry.Definition, Is.SameAs(optional));
            Assert.That(displayedEntry.Status, Is.EqualTo(StoryQuestStatus.Completed));
            Assert.That(journal.SelectedQuestId, Is.EqualTo("side"));
        }

        [Test]
        public void MissionRowsShowLocationAndKeepObjectivesAsFallbackForOlderDefinitions()
        {
            side.ConfigureDetails("side.description", "side.location");
            view.Refresh();
            string description = Row("side").Find("Description").GetComponent<TMP_Text>().text;
            Assert.That(description, Does.Contain("side.location"));
            Assert.That(description, Does.Not.Contain("side.objective"));
            Assert.That(Row("main").Find("Description").GetComponent<TMP_Text>().text, Does.Contain("main.objective"));
        }

        private Transform Row(string id) => content.Find("Mission " + id);
        private static QuestDefinition Definition(string id, QuestCategory category)
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.Configure(id, category, id + ".title", new[] { id + ".objective" });
            return quest;
        }
        private static void SetField(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            if (parent != null) rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }
    }
}
