using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Story;
using TMPro;
using UnityEditor;
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
                Assert.That(mainRow.GetComponent<UnityEngine.UI.Button>().interactable, Is.True);
                Transform row = content.transform.Find("Mission optional");
                row.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(journal.SelectedQuestId, Is.EqualTo(optional.Id));
                row = content.transform.Find("Mission optional");
                Assert.That(row.Find("Icon").GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(QuestTrackerView.OptionalDotColor));
                Assert.That(content.transform.Find("Mission side/Icon").GetComponent<UnityEngine.UI.Image>().color,
                    Is.EqualTo(QuestTrackerView.SideDotColor));
                journal.TryFinish(optional.Id);
                row = content.transform.Find("Mission optional");
                Assert.That(row.GetComponent<UnityEngine.UI.Button>().interactable, Is.True);
                Assert.That(row.Find("'Title '").GetComponent<TMP_Text>().text, Does.Contain("<s>"));
            }
        }

        [Test]
        public void QuestDetailsUseLocalizedDescriptionAndLocationWithoutChangingHudObjectives()
        {
            const string preferenceKey = "Localization.Language";
            bool hadPreference = PlayerPrefs.HasKey(preferenceKey);
            string originalPreference = PlayerPrefs.GetString(preferenceKey);
            const BindingFlags staticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo tableField = typeof(LocalizationManager).GetField("table", staticPrivate);
            FieldInfo languageField = typeof(LocalizationManager).GetField("currentLanguage", staticPrivate);
            FieldInfo initializedField = typeof(LocalizationManager).GetField("isInitialized", staticPrivate);
            object originalTable = tableField.GetValue(null);
            object originalLanguage = languageField.GetValue(null);
            object originalInitialized = initializedField.GetValue(null);
            var table = ScriptableObject.CreateInstance<LocalizationTable>();
            created.Add(table);
            table.entries = new[] {
                new LocalizedTextEntry { key = "journal.description", english = "Find both colleagues.", indonesian = "Cari kedua rekan kerja." },
                new LocalizedTextEntry { key = "journal.location", english = "Floor 2, Archive Staff.", indonesian = "Lantai 2, Petugas Arsip." },
                new LocalizedTextEntry { key = "objective.main", english = "Find Raka", indonesian = "Temukan Raka" },
                new LocalizedTextEntry { key = "quest.journal.location_label", english = "Location", indonesian = "Lokasi" },
                new LocalizedTextEntry { key = "quest.journal.objectives_label", english = "Objectives", indonesian = "Tujuan" }
            };
            main.ConfigureDetails("journal.description", "journal.location", new[] { "car-key" });
            try
            {
                LocalizationManager.Initialize(table);
                LocalizationManager.SetLanguage(GameLanguage.Indonesian);
                Assert.That(InventoryMissionView.DetailsText(main), Does.Contain("Cari kedua rekan kerja."));
                Assert.That(InventoryMissionView.DetailsText(main), Does.Contain("Lokasi: Lantai 2, Petugas Arsip."));
                Assert.That(InventoryMissionView.ObjectivesText(main), Is.EqualTo("Temukan Raka"));
                LocalizationManager.SetLanguage(GameLanguage.English);
                Assert.That(InventoryMissionView.DetailsText(main), Does.Contain("Location: Floor 2, Archive Staff."));
                Assert.That(InventoryMissionView.ObjectivesText(main), Is.EqualTo("Find Raka"));
                main.Configure("main", QuestCategory.Main, "quest.main", new[] { "objective.main" });
                Assert.That(main.DescriptionKey, Is.EqualTo("journal.description"));
                Assert.That(main.RequiredItemIds, Is.EquivalentTo(new[] { "car-key" }));
            }
            finally
            {
                if (hadPreference) PlayerPrefs.SetString(preferenceKey, originalPreference);
                else PlayerPrefs.DeleteKey(preferenceKey);
                PlayerPrefs.Save();
                tableField.SetValue(null, originalTable);
                languageField.SetValue(null, originalLanguage);
                initializedField.SetValue(null, originalInitialized);
                (typeof(LocalizationManager).GetField("LanguageChanged", staticPrivate).GetValue(null) as System.Action)?.Invoke();
            }
        }

        [Test]
        public void QuestRequiredItemsRejectEmptyAndDuplicateIdentities()
        {
            main.ConfigureDetails("description", "location", new[] { "car-key", "car-key" });
            Assert.That(main.Validate(out _), Is.False);
            main.ConfigureDetails("description", "location", new[] { "" });
            Assert.That(main.Validate(out _), Is.False);
            main.ConfigureDetails("description", "location", new[] { "car-key" });
            Assert.That(main.Validate(out _), Is.True);
        }

        [Test]
        public void ChapterOneQuestAssetsHaveIndonesianAndEnglishJournalDetails()
        {
            LocalizationTable table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                "Assets/_Project/Localization/LocalizationTable.asset");
            ItemDefinitionCatalog items = AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(
                "Assets/_Project/Resources/ItemDefinitionCatalog.asset");
            Assert.That(table, Is.Not.Null);
            Assert.That(items, Is.Not.Null);
            string[] guids = AssetDatabase.FindAssets("t:QuestDefinition", new[] { "Assets/_Project/Story/Quests" });
            Assert.That(guids.Length, Is.GreaterThanOrEqualTo(17));
            foreach (string guid in guids)
            {
                QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(quest.Validate(out string error), Is.True, error);
                foreach (string itemId in quest.RequiredItemIds)
                    Assert.That(items.Find(itemId), Is.Not.Null, quest.Id + ": " + itemId);
                Assert.That(quest.DescriptionKey, Is.Not.Empty, quest.Id);
                Assert.That(quest.LocationKey, Is.Not.Empty, quest.Id);
                foreach (string key in new[] { quest.DescriptionKey, quest.LocationKey })
                {
                    LocalizedTextEntry entry = System.Array.Find(table.entries, value => value != null && value.key == key);
                    Assert.That(entry, Is.Not.Null, quest.Id);
                    Assert.That(entry.english, Is.Not.Empty, key);
                    Assert.That(entry.indonesian, Is.Not.Empty, key);
                }
            }
            QuestDefinition find = AssetDatabase.LoadAssetAtPath<QuestDefinition>(
                "Assets/_Project/Story/Quests/find-colleagues.asset");
            string location = table.GetText(find.LocationKey, GameLanguage.Indonesian);
            Assert.That(location, Does.Contain("lantai 2"));
            Assert.That(location, Does.Contain("Ruang Kerja Raka"));
            Assert.That(location, Does.Contain("Ruang Kerja MC dan Sinta"));
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
