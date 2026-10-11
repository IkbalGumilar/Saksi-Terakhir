using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Story;
using TMPro;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class QuestJournalTests
    {
        private readonly List<Object> created = new List<Object>();
        private ChapterOneDirector chapter;
        private QuestJournalController journal;
        private QuestDefinition main, side, optional;

        [SetUp]
        public void SetUp()
        {
            GameObject owner = NewUi("Journal director");
            owner.SetActive(false);
            chapter = owner.AddComponent<ChapterOneDirector>();
            chapter.StartupManagedExternally = true;
            main = Definition("main", QuestCategory.Main);
            side = Definition("side", QuestCategory.Side);
            optional = Definition("optional", QuestCategory.Optional);
            Set(chapter, "quests", new[] { main });
            Set(chapter, "progress", new ChapterOneProgress());
            journal = owner.AddComponent<QuestJournalController>();
            var catalog = ScriptableObject.CreateInstance<QuestJournalCatalog>();
            created.Add(catalog);
            catalog.Configure(new[] { side, optional });
            journal.Configure(chapter, catalog);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void UnacceptedQuestsCannotBeSelectedAndMainStaysPinned()
        {
            Assert.That(journal.Entries.Count, Is.EqualTo(1));
            Assert.That(journal.Entries[0].Definition, Is.SameAs(main));
            Assert.That(journal.TrySelect(side.Id), Is.False);
            Assert.That(journal.TrySelect(main.Id), Is.False);
            Assert.That(journal.TryAccept(optional), Is.True);
            Assert.That(journal.TryAccept(side), Is.True);
            Assert.That(journal.TrySelect(side.Id), Is.True);
            Assert.That(journal.TrySelect(optional.Id), Is.True);
            Assert.That(journal.SelectedQuest, Is.SameAs(optional));
            Assert.That(journal.Entries[0].Definition, Is.SameAs(main));
        }

        [Test]
        public void SelectionAndQuestStatusSurviveJsonWithoutMutatingDefinitions()
        {
            journal.TryAccept(side);
            journal.TrySelect(side.Id);
            string json = JsonUtility.ToJson(chapter.Progress);
            ChapterOneProgress restored = JsonUtility.FromJson<ChapterOneProgress>(json);
            Assert.That(restored.ValidateStoryState(out _), Is.True);
            Set(chapter, "progress", restored);
            journal.Refresh();
            Assert.That(journal.SelectedQuestId, Is.EqualTo(side.Id));
            Assert.That(journal.SelectedQuest, Is.SameAs(side));
            Assert.That(journal.TryFinish(side.Id), Is.True);
            Assert.That(journal.SelectedQuest, Is.Null);
            Assert.That(journal.SelectedQuestId, Is.Empty);
            Assert.That(journal.TryAccept(side), Is.False);
            Assert.That(side.Id, Is.EqualTo("side"));
        }

        [Test]
        public void BoundStoryQuestUsesExistingStateAndClearsCancelledSelection()
        {
            side.BindProgress(QuestProgressSource.WrongRoute);
            Assert.That(journal.TryAccept(side), Is.False);
            Assert.That(chapter.Progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteStarted), Is.True);
            journal.Refresh();
            Assert.That(journal.TrySelect(side.Id), Is.True);
            Assert.That(chapter.Progress.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteSkipped), Is.True);
            journal.Refresh();
            Assert.That(journal.SelectedQuestId, Is.Empty);
            Assert.That(journal.Status(side), Is.EqualTo(StoryQuestStatus.Skipped));
            Assert.That(journal.TrySelect(side.Id), Is.False);
        }

        [Test]
        public void DuplicateJournalRecordsInvalidateTheSaveAndOldSavesGetAnEmptyJournal()
        {
            var invalid = JsonUtility.FromJson<QuestJournalState>("{\"records\":[{\"id\":\"x\",\"status\":1},{\"id\":\"x\",\"status\":1}]}");
            Assert.That(invalid.Validate(out _), Is.False);
            ChapterOneProgress old = JsonUtility.FromJson<ChapterOneProgress>("{\"version\":1,\"stage\":0}");
            Assert.That(old.ValidateStoryState(out _), Is.True);
            Assert.That(old.Journal.SelectedQuestId, Is.Empty);
        }

        [Test]
        public void MissionClickSelectsOnlyActiveSupportingRowsWithCorrectColors()
        {
            journal.TryAccept(side);
            journal.TryAccept(optional);
            GameObject page = NewUi("Quest List");
            GameObject scroll = Child(page, "Scroll View");
            var scrollRect = scroll.AddComponent<UnityEngine.UI.ScrollRect>();
            GameObject viewport = Child(scroll, "Viewport");
            GameObject content = Child(viewport, "Content");
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = content.GetComponent<RectTransform>();
            for (int i = 0; i < 3; i++)
            {
                Child(content, new[] { "Main Quest", "Side Quest", "Optional Quest" }[i]);
                GameObject row = Child(content, "Slot " + (i + 1));
                row.AddComponent<UnityEngine.UI.Image>();
                Child(row, "Icon").AddComponent<UnityEngine.UI.Image>();
                Child(row, "'Title '").AddComponent<TextMeshProUGUI>();
                Child(row, "Description").AddComponent<TextMeshProUGUI>();
            }
            using (var view = new InventoryMissionView(page, _ => { }))
            {
                view.Bind(journal);
                Transform mainRow = content.transform.Find("Mission main");
                Assert.That(mainRow.GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
                Transform row = content.transform.Find("Mission optional");
                row.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(journal.SelectedQuestId, Is.EqualTo(optional.Id));
                row = content.transform.Find("Mission optional");
                Assert.That(row.Find("Icon").GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(QuestTrackerView.OptionalDotColor));
                Assert.That(content.transform.Find("Mission side/Icon").GetComponent<UnityEngine.UI.Image>().color,
                    Is.EqualTo(QuestTrackerView.SideDotColor));
                journal.TryFinish(optional.Id);
                row = content.transform.Find("Mission optional");
                Assert.That(row.GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
                Assert.That(row.Find("'Title '").GetComponent<TMP_Text>().text, Does.Contain("<s>"));
            }
        }

        [Test]
        public void TrackerPlacesSelectionBelowMainAndHidesItWhenFinished()
        {
            GameObject parent = NewUi("HUD");
            GameObject root = Child(parent, "Main tracker");
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(268f, 108f);
            TMP_Text title = Child(root, "Title").AddComponent<TextMeshProUGUI>();
            TMP_Text objectives = Child(root, "Objectives").AddComponent<TextMeshProUGUI>();
            var dot = Child(root, "Dot").AddComponent<UnityEngine.UI.Image>();
            QuestTrackerView tracker = Child(parent, "Bindings").AddComponent<QuestTrackerView>();
            tracker.Configure(chapter, root, title, objectives, dot);
            journal.TryAccept(optional);
            journal.TrySelect(optional.Id);
            tracker.Render(main, chapter.Progress);
            Transform supporting = parent.transform.Find("Selected Supporting Quest");
            Assert.That(supporting, Is.Not.Null);
            Assert.That(root.activeSelf, Is.True);
            Assert.That(supporting.GetComponent<RectTransform>().anchoredPosition.y,
                Is.LessThan(root.GetComponent<RectTransform>().anchoredPosition.y - 108f));
            Assert.That(supporting.Find("Dot").GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(QuestTrackerView.OptionalDotColor));
            journal.TryFinish(optional.Id);
            Assert.That(supporting.gameObject.activeSelf, Is.False);
        }

        private QuestDefinition Definition(string id, QuestCategory category)
        {
            var result = ScriptableObject.CreateInstance<QuestDefinition>();
            created.Add(result);
            result.Configure(id, category, "quest." + id, new[] { "objective." + id });
            return result;
        }
        private GameObject NewUi(string name)
        {
            var result = new GameObject(name, typeof(RectTransform));
            created.Add(result);
            return result;
        }
        private GameObject Child(GameObject parent, string name)
        {
            GameObject result = NewUi(name);
            result.transform.SetParent(parent.transform, false);
            return result;
        }
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
