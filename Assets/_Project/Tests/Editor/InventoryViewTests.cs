using System;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Story;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SaksiTerakhir.Tests
{
    public sealed class InventoryViewTests
    {
        private GameObject canvasObject;
        private GameObject eventObject;
        private GameObject panel;
        private RectTransform grid;
        private RectTransform information;
        private InventoryView view;
        private InventoryEntry dropped;
        private InventoryEntry moved;
        private int destination = -1;
        private int dropCount;
        private int moveCount;

        [SetUp]
        public void SetUp()
        {
            // NUnit reuses a fixture instance; callback observations belong to one test only.
            dropped = null;
            moved = null;
            destination = -1;
            dropCount = 0;
            moveCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
            if (eventObject != null) UnityEngine.Object.DestroyImmediate(eventObject);
        }

        [Test]
        public void OneArchiveDocumentOpensOriginalAndTranslatesWithoutAddingAnotherSlot()
        {
            CreateView();
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            var table = ScriptableObject.CreateInstance<LocalizationTable>();
            var tableField = typeof(LocalizationManager).GetField("table",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var previousTable = (LocalizationTable)tableField.GetValue(null);
            try
            {
                const string original = "Original inscription\n\nA long document with its own original writing.";
                table.entries = new[] { new LocalizedTextEntry { key = "test.archive.translation",
                    english = "Translated inscription", indonesian = "Terjemahan prasasti" } };
                LocalizationManager.Initialize(table);
                var fields = new SerializedObject(definition);
                fields.FindProperty("id").stringValue = "document.wanaseta_original";
                fields.FindProperty("type").enumValueIndex = (int)InventoryEntryType.Document;
                fields.FindProperty("documentText").stringValue = original;
                SerializedProperty translation = fields.FindProperty("documentTranslationKey");
                if (translation != null) translation.stringValue = "test.archive.translation";
                fields.ApplyModifiedPropertiesWithoutUndo();
                var state = new InventoryState();
                InventoryEntry archive = definition.CreateEntry();
                state.TryAdd(archive);
                view.Refresh(state);
                view.SelectEntry(archive);

                Transform reader = information.Find("Document Reader");
                Assert.That(reader, Is.Not.Null, "The single archive needs a reader with a translation action.");
                TMP_Text body = reader.Find("Scroll View/Viewport/Content/Text").GetComponent<TMP_Text>();
                var translate = reader.Find("Translate").GetComponent<UnityEngine.UI.Button>();
                Assert.That(body.text, Is.EqualTo(original));
                Assert.That(translate.gameObject.activeSelf, Is.True);
                Assert.That(((RectTransform)translate.transform).anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
                translate.onClick.Invoke();
                Assert.That(body.text, Is.EqualTo(LocalizationManager.Get("test.archive.translation")));
                view.Refresh(state);
                Assert.That(body.text, Is.EqualTo(LocalizationManager.Get("test.archive.translation")),
                    "An inventory refresh should preserve the page being read.");
                translate.onClick.Invoke();
                Assert.That(body.text, Is.EqualTo(original));
                Assert.That(state.Documents.Count, Is.EqualTo(1));
                Assert.That(state.Documents[0], Is.SameAs(archive));
                view.OnClosed();
                Assert.That(reader.gameObject.activeSelf, Is.False);
                view.OnOpened();
                Assert.That(body.text, Is.EqualTo(original));
                view.ShowPage(InventoryPage.Items);
                Assert.That(reader.gameObject.activeSelf, Is.False);
            }
            finally
            {
                view.Dispose();
                LocalizationManager.Initialize(previousTable);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void DestroyedDocumentReaderDoesNotBreakInventoryCleanup()
        {
            CreateView();
            UnityEngine.Object.DestroyImmediate(information.Find("Document Reader").gameObject);
            Assert.DoesNotThrow(() => view.OnClosed());
            Assert.DoesNotThrow(() => view.Dispose());
        }

        [TestCase("image Item", "mark", false)]
        [TestCase("Image", "Mark Use", false)]
        [TestCase("Item Image", "Mark Use", true)]
        public void FixedSlotBindingPreservesHolesAndHeldMark(string iconName, string markName, bool textMark)
        {
            CreateView(iconName, markName, textMark: textMark);
            var state = new InventoryState(2, 1);
            var item = Item();
            state.TryAdd(item);
            state.TryMoveToSlot(item, 1);
            state.TryHold(item);
            view.Refresh(state);

            RectTransform first = Slot(0);
            RectTransform second = Slot(1);
            Assert.That(first.GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
            Assert.That(second.GetComponent<UnityEngine.UI.Button>().interactable, Is.True);
            Assert.That(second.Find(iconName + "/" + markName).gameObject.activeSelf, Is.True);
            Assert.That(first.Find(iconName + "/" + markName).gameObject.activeSelf, Is.False);
            foreach (UnityEngine.UI.Graphic graphic in second.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                if (graphic.transform != second) Assert.That(graphic.raycastTarget, Is.False);
            state.Release(item);
            view.Refresh(state);
            Assert.That(second.Find(iconName + "/" + markName).gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ReleasingOverInformationPanelDropsOutsideInventoryBoundary()
        {
            CreateView();
            var state = new InventoryState(2, 1);
            var item = Item();
            state.TryAdd(item);
            view.Refresh(state);
            PointerEventData pointer = Pointer(Point(Slot(0)));
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.beginDragHandler);
            Transform ghost = canvasObject.transform.Find("Inventory Drag Ghost");
            Assert.That(ghost, Is.Not.Null);
            Assert.That(ghost.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.False);
            pointer.position = Point(information);
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.endDragHandler);

            Assert.That(dropped, Is.SameAs(item));
            Assert.That(moved, Is.Null);
            Assert.That(dropCount, Is.EqualTo(1));
            Assert.That(moveCount, Is.Zero);
            Assert.That(canvasObject.transform.Find("Inventory Drag Ghost"), Is.Null);
        }

        [Test]
        public void ReleasingInsideEmptySlotRequestsMoveButInsideUnusedGridSpaceReverts()
        {
            CreateView();
            var state = new InventoryState(2, 1);
            var item = Item();
            state.TryAdd(item);
            view.Refresh(state);
            PointerEventData pointer = Pointer(Point(Slot(0)));
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.beginDragHandler);
            pointer.position = Point(Slot(1));
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(moved, Is.SameAs(item));
            Assert.That(destination, Is.EqualTo(1));
            Assert.That(dropped, Is.Null);
            Assert.That(moveCount, Is.EqualTo(1));

            moved = null;
            destination = -1;
            pointer.position = Point(Slot(0));
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.beginDragHandler);
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, grid.TransformPoint(new Vector3(120f, -120f)));
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(moved, Is.Null);
            Assert.That(dropped, Is.Null);
            Assert.That(moveCount, Is.EqualTo(1), "Unused Inventory interior must not request a second move.");
            Assert.That(dropCount, Is.Zero);
        }

        [Test]
        public void PageAndPanelCloseDismissContextMenuAndCancelDrag()
        {
            CreateView();
            var state = new InventoryState(2, 1);
            state.TryAdd(Item());
            view.Refresh(state);
            PointerEventData pointer = Pointer(Point(Slot(0)));
            pointer.button = PointerEventData.InputButton.Right;
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Transform menu = canvasObject.transform.Find("Inventory Context Menu");
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.gameObject.activeSelf, Is.True);
            view.ShowPage(true);
            Assert.That(menu.gameObject.activeSelf, Is.False);
            view.ShowPage(false);
            pointer.button = PointerEventData.InputButton.Left;
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.beginDragHandler);
            view.OnClosed();
            Assert.That(canvasObject.transform.Find("Inventory Drag Ghost"), Is.Null);
            pointer.position = Point(information);
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(dropped, Is.Null);
            Assert.That(dropCount, Is.Zero);
            Assert.That(moveCount, Is.Zero);
        }

        [Test]
        public void DropCallbackCanRefreshAndCloseWithoutLeavingOrRepeatingDrag()
        {
            var state = new InventoryState(2, 1);
            var item = Item();
            state.TryAdd(item);
            CreateView(dropAction: entry =>
            {
                Assert.That(canvasObject.transform.Find("Inventory Drag Ghost"), Is.Null,
                    "Transient drag must be cleared before callbacks can refresh the view.");
                state.Remove(entry);
                view.Refresh(state);
                view.OnClosed();
            });
            view.Refresh(state);
            RectTransform source = Slot(0);
            PointerEventData pointer = Pointer(Point(source));
            ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.beginDragHandler);
            pointer.position = Point(information);
            ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(dropped, Is.SameAs(item));
            Assert.That(dropCount, Is.EqualTo(1));
            Assert.That(moveCount, Is.Zero);
            Assert.That(state.Items, Is.Empty);
            ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(dropCount, Is.EqualTo(1), "A repeated EndDrag cannot repeat the physical drop.");
        }

        [Test]
        public void OutsideContextClickClosesWithoutDroppingAndDropActionUsesItsEntry()
        {
            CreateView();
            var state = new InventoryState(2, 1);
            var item = Item();
            state.TryAdd(item);
            view.Refresh(state);
            PointerEventData pointer = Pointer(Point(Slot(0)));
            pointer.button = PointerEventData.InputButton.Right;
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Transform menu = canvasObject.transform.Find("Inventory Context Menu");
            Assert.That(menu.gameObject.activeSelf, Is.True);
            pointer.position = new Vector2(-10000f, -10000f);
            ExecuteEvents.Execute(menu.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            Assert.That(menu.gameObject.activeSelf, Is.False);
            Assert.That(dropped, Is.Null);
            Assert.That(dropCount, Is.Zero);
            pointer.position = Point(Slot(0));
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.pointerClickHandler);
            menu.Find("Actions/Drop").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(menu.gameObject.activeSelf, Is.False);
            Assert.That(dropped, Is.SameAs(item));
            Assert.That(dropCount, Is.EqualTo(1));
        }

        [Test]
        public void DocumentsRemainSelectableWithoutContextOrDragActions()
        {
            CreateView();
            var state = new InventoryState();
            var document = new InventoryEntry("note", "Note", InventoryEntryType.Document);
            state.TryAdd(document);
            view.Refresh(state);
            view.ShowPage(true);
            Transform slot = panel.transform.Find("Inventory Dock/Scroll View/Viewport/Content/Document Slot 1");
            slot.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(panel.transform.Find("Informaton Panel/Name Item").GetComponent<TMP_Text>().text,
                Is.EqualTo("Note"));
            PointerEventData pointer = Pointer(Point((RectTransform)slot));
            pointer.button = PointerEventData.InputButton.Right;
            ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            pointer.button = PointerEventData.InputButton.Left;
            ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.beginDragHandler);
            pointer.position = Point(information);
            ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(dropped, Is.Null);
            Assert.That(moved, Is.Null);
            Assert.That(canvasObject.transform.Find("Inventory Drag Ghost"), Is.Null);
            Transform menu = canvasObject.transform.Find("Inventory Context Menu");
            Assert.That(menu == null || !menu.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void HeaderClicksAndPageArrowsAlwaysUpdateTheSameUnderline()
        {
            CreateView(includeMissions: true);
            AssertPage(InventoryPage.Items);
            panel.transform.Find("Header/Dokcument Tab").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            AssertPage(InventoryPage.Documents);
            panel.transform.Find("Inventory Dock/Arrow Right").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            AssertPage(InventoryPage.Missions);
            panel.transform.Find("Quest List/Arrow Right").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            AssertPage(InventoryPage.Items);
            panel.transform.Find("Inventory/Arrow Left").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            AssertPage(InventoryPage.Missions);
            panel.transform.Find("Header/Inventory Tab").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            AssertPage(InventoryPage.Items);
        }

        [Test]
        public void SwitchingToMissionTabCancelsAnItemDragWithoutDroppingIt()
        {
            CreateView(includeMissions: true);
            var state = new InventoryState(1, 1);
            state.TryAdd(Item());
            view.Refresh(state);
            PointerEventData pointer = Pointer(Point(Slot(0)));
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.beginDragHandler);
            panel.transform.Find("Header/Quest list Tab").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(canvasObject.transform.Find("Inventory Drag Ghost"), Is.Null);
            pointer.position = Point(information);
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(dropCount, Is.Zero);
            AssertPage(InventoryPage.Missions);
        }

        private void AssertPage(InventoryPage page)
        {
            Assert.That(view.ActivePage, Is.EqualTo(page));
            string[] tabNames = { "Inventory Tab", "Dokcument Tab", "Quest list Tab" };
            string[] pageNames = { "Inventory", "Inventory Dock", "Quest List" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                Assert.That(panel.transform.Find("Header/" + tabNames[i] + "/Selection Underline").gameObject.activeSelf,
                    Is.EqualTo(i == (int)page));
                Assert.That(panel.transform.Find(pageNames[i]).gameObject.activeSelf, Is.EqualTo(i == (int)page));
            }
        }

        [TestCase(QuestCategory.Main)]
        [TestCase(QuestCategory.Side)]
        [TestCase(QuestCategory.Optional)]
        public void RequiredItemFrameUsesTheQuestColorWithoutInterceptingInput(QuestCategory category)
        {
            CreateView(questCategory: _ => category);
            var state = new InventoryState(1, 1);
            state.TryAdd(Item());
            view.Refresh(state);
            InventoryQuestFrame frame = Slot(0).GetComponentInChildren<InventoryQuestFrame>(true);
            Assert.That(frame.Category, Is.EqualTo(category));
            Assert.That(frame.gameObject.activeSelf, Is.True);
            foreach (UnityEngine.UI.Image border in frame.GetComponentsInChildren<UnityEngine.UI.Image>())
            {
                Assert.That(border.color, Is.EqualTo(QuestTrackerView.DotColor(category)));
                Assert.That(border.raycastTarget, Is.False);
            }
        }

        [Test]
        public void JourneyKeyFrameAndDropOptionUpdateAfterReleaseWhileTheHeldMarkRemains()
        {
            CreateView();
            var state = new InventoryState(1, 1);
            var key = new InventoryEntry(CarKeyInventory.CarKeyId, "Key", InventoryEntryType.Item);
            state.TryAdd(key);
            state.TryHold(key);
            view.Refresh(state);
            var frame = Slot(0).GetComponentInChildren<InventoryQuestFrame>(true);
            PointerEventData pointer = Pointer(Point(Slot(0)));
            pointer.button = PointerEventData.InputButton.Right;
            ExecuteEvents.Execute(Slot(0).gameObject, pointer, ExecuteEvents.pointerClickHandler);
            var dropButton = canvasObject.transform.Find("Inventory Context Menu/Actions/Drop")
                .GetComponent<UnityEngine.UI.Button>();
            Assert.That(dropButton.interactable, Is.False);
            Assert.That(frame.gameObject.activeSelf, Is.True);
            state.ReleaseQuestItem(key.Id);
            view.Refresh(state);
            Assert.That(dropButton.interactable, Is.True);
            Assert.That(frame.gameObject.activeSelf, Is.False);
            Assert.That(Slot(0).Find("image Item/mark").gameObject.activeSelf, Is.True);
        }

        private void CreateView(string iconName = "image Item", string markName = "mark",
            Action<InventoryEntry> dropAction = null, bool textMark = false, bool includeMissions = false,
            Func<InventoryEntry, QuestCategory?> questCategory = null)
        {
            canvasObject = new GameObject("Inventory Test Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            eventObject = new GameObject("Inventory Test Events", typeof(EventSystem));
            panel = Rect("Panel", canvasObject.transform, new Vector2(900f, 600f)).gameObject;
            panel.AddComponent<UnityEngine.UI.Image>();
            grid = Rect("Inventory", panel.transform, new Vector2(300f, 300f));
            grid.anchoredPosition = new Vector2(-200f, 0f);
            RectTransform template = Rect("Slot 1", grid, new Vector2(100f, 100f));
            template.gameObject.AddComponent<UnityEngine.UI.Image>();
            RectTransform icon = Rect(iconName, template, new Vector2(70f, 70f));
            icon.gameObject.AddComponent<UnityEngine.UI.Image>();
            GameObject heldMark = Rect(markName, icon, new Vector2(20f, 20f)).gameObject;
            if (textMark) heldMark.AddComponent<TextMeshProUGUI>();
            else heldMark.AddComponent<UnityEngine.UI.Image>();
            RectTransform dock = Rect("Inventory Dock", panel.transform, new Vector2(300f, 300f));
            RectTransform scroll = Rect("Scroll View", dock, new Vector2(300f, 300f));
            var scrollRect = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            RectTransform viewport = Rect("Viewport", scroll, new Vector2(300f, 300f));
            RectTransform content = Rect("Content", viewport, new Vector2(300f, 300f));
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            RectTransform document = Rect("Slot 1", content, new Vector2(300f, 100f));
            document.gameObject.AddComponent<UnityEngine.UI.Image>();
            Rect("Icon", document, new Vector2(70f, 70f)).gameObject.AddComponent<UnityEngine.UI.Image>();
            Text("Title ", document);
            Text("Description", document);
            information = Rect("Informaton Panel", panel.transform, new Vector2(200f, 300f));
            information.anchoredPosition = new Vector2(200f, 0f);
            Rect("Preview Item", information, new Vector2(100f, 100f)).gameObject.AddComponent<UnityEngine.UI.Image>();
            Text("Name Item", information);
            Text("Item Status", information);
            Text("Informations Text", information);
            if (includeMissions)
            {
                RectTransform header = Rect("Header", panel.transform, new Vector2(900f, 50f));
                foreach (string name in new[] { "Inventory Tab", "Dokcument Tab", "Quest list Tab" })
                {
                    RectTransform tab = Rect(name, header, new Vector2(200f, 50f));
                    tab.gameObject.AddComponent<UnityEngine.UI.Image>();
                    Rect("Selection Underline", tab, new Vector2(200f, 3f)).gameObject.AddComponent<UnityEngine.UI.Image>();
                }
                Rect("Quest List", panel.transform, new Vector2(300f, 300f));
                foreach (string pageName in new[] { "Inventory", "Inventory Dock", "Quest List" })
                    foreach (string arrow in new[] { "Arrow Left", "Arrow Right" })
                        Rect(arrow, panel.transform.Find(pageName), new Vector2(30f, 30f)).gameObject.AddComponent<UnityEngine.UI.Image>();
            }
            view = new InventoryView(panel, () => { }, _ => { }, _ => { },
                entry => { dropped = entry; dropCount++; dropAction?.Invoke(entry); },
                (entry, index) => { moved = entry; destination = index; moveCount++; }, questCategory);
            Canvas.ForceUpdateCanvases();
        }

        private RectTransform Slot(int index) => (RectTransform)grid.Find("Inventory Grid Viewport/Inventory Grid Content/Inventory Slot " + (index + 1));
        private PointerEventData Pointer(Vector2 point) => new PointerEventData(eventObject.GetComponent<EventSystem>())
        { position = point, button = PointerEventData.InputButton.Left };
        private static Vector2 Point(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        private static InventoryEntry Item() => new InventoryEntry("item", "Item", InventoryEntryType.Item);
        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }
        private static void Text(string name, Transform parent) =>
            Rect(name, parent, new Vector2(150f, 40f)).gameObject.AddComponent<TextMeshProUGUI>();
    }
}
