using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SaksiTerakhir.Tests
{
    public sealed class InventoryTargetFlowTests
    {
        private GameObject actor;
        private GameObject canvasObject;
        private GameObject eventObject;
        private GameObject panel;
        private InputActionAsset actions;
        private InventoryPanelController inventory;
        private PlayerController movement;
        private PlayerInteractor interactor;
        private TMP_Text selectedName;
        private Button closeButton;
        private CursorLockMode originalCursorLock;
        private bool originalCursorVisible;

        [SetUp]
        public void SetUp()
        {
            originalCursorLock = Cursor.lockState;
            originalCursorVisible = Cursor.visible;
        }

        [TearDown]
        public void TearDown()
        {
            if (inventory != null)
            {
                inventory.Close();
                // EditMode does not automatically run ordinary MonoBehaviour lifecycle methods.
                InvokeLifecycle(inventory, "OnDestroy");
            }
            if (actor != null) Object.DestroyImmediate(actor);
            if (canvasObject != null) Object.DestroyImmediate(canvasObject);
            if (eventObject != null) Object.DestroyImmediate(eventObject);
            if (actions != null) Object.DestroyImmediate(actions);
            Cursor.lockState = originalCursorLock;
            Cursor.visible = originalCursorVisible;
        }

        [Test]
        public void UnheldTargetSelectsMatchingItemAndWrongSelectionKeepsUsePending()
        {
            var correct = Item("key", "Key");
            var wrong = Item("rope", "Rope");
            CreateController(correct, wrong);
            var target = new UseTarget(correct.Id);
            string feedback = null;
            inventory.FeedbackRequested += key => feedback = key;

            Assert.That(inventory.RequestUseForTarget(target), Is.True);
            Assert.That(inventory.IsOpen, Is.True);
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(selectedName.text, Is.EqualTo(correct.DisplayName));
            AssertControlsLocked(true);

            Slot(1).onClick.Invoke();
            Assert.That(selectedName.text, Is.EqualTo(wrong.DisplayName));
            Assert.That(inventory.TryUse(wrong), Is.False);
            Assert.That(target.Uses, Is.Zero);
            Assert.That(feedback, Is.EqualTo("inventory.wrong_item"));
            Assert.That(inventory.IsOpen, Is.True);
            AssertControlsLocked(true);

            Assert.That(inventory.TryUse(correct), Is.True);
            Assert.That(target.Uses, Is.EqualTo(1));
            Assert.That(target.UsedEntry, Is.SameAs(correct));
            Assert.That(target.Actor, Is.SameAs(actor.transform));
            Assert.That(inventory.IsOpen, Is.False);
            Assert.That(panel.activeSelf, Is.False);
            AssertControlsLocked(false);
            Assert.That(inventory.State.GetItemAtSlot(0), Is.SameAs(correct));
            Assert.That(inventory.State.GetItemAtSlot(1), Is.SameAs(wrong));
        }

        [Test]
        public void ClosingSelectionCancelsTargetWithoutUsingOrRemovingItem()
        {
            var item = Item("key", "Key");
            CreateController(item);
            var target = new UseTarget(item.Id);
            Assert.That(inventory.RequestUseForTarget(target), Is.True);
            AssertControlsLocked(true);

            closeButton.onClick.Invoke();

            Assert.That(inventory.IsOpen, Is.False);
            Assert.That(panel.activeSelf, Is.False);
            AssertControlsLocked(false);
            Assert.That(target.Uses, Is.Zero);
            Assert.That(inventory.State.GetItemAtSlot(0), Is.SameAs(item));
            Assert.That(inventory.TryUse(item), Is.False,
                "Closing the panel must clear the pending target, including a later use request.");
            Assert.That(target.Uses, Is.Zero);
            Assert.That(inventory.State.GetItemAtSlot(0), Is.SameAs(item));
        }

        [Test]
        public void HeldItemUsesImmediatelyAndRejectedTargetsPreserveIt()
        {
            var item = Item("key", "Key");
            CreateController(item);
            Assert.That(inventory.State.TryHold(item), Is.True);
            var target = new UseTarget(item.Id);

            Assert.That(inventory.RequestUseForTarget(target), Is.True);
            Assert.That(target.Uses, Is.EqualTo(1));
            Assert.That(target.UsedEntry, Is.SameAs(item));
            Assert.That(target.Actor, Is.SameAs(actor.transform));
            Assert.That(inventory.IsOpen, Is.False);
            Assert.That(panel.activeSelf, Is.False);
            AssertControlsLocked(false);

            var otherTarget = new UseTarget("unrelated");
            Assert.That(inventory.RequestUseForTarget(otherTarget), Is.True);
            Assert.That(otherTarget.Uses, Is.Zero);
            Assert.That(inventory.IsOpen, Is.True,
                "The first missing-key attempt lets the player inspect the inventory.");
            inventory.Close();
            var rejectedEffect = new UseTarget(item.Id, false);
            Assert.That(inventory.RequestUseForTarget(rejectedEffect), Is.False);
            Assert.That(rejectedEffect.Uses, Is.EqualTo(1));
            Assert.That(inventory.IsOpen, Is.False);
            Assert.That(panel.activeSelf, Is.False);
            AssertControlsLocked(false);
            Assert.That(inventory.State.GetItemAtSlot(0), Is.SameAs(item));
            Assert.That(inventory.State.IsHeld(item), Is.True);
        }

        [Test]
        public void FirstMissingItemAttemptOpensEmptyInventoryAndRetryDoesNotReopenIt()
        {
            CreateController();
            var target = new UseTarget("key");

            Assert.That(inventory.RequestUseForTarget(target), Is.True);
            Assert.That(inventory.IsOpen, Is.True);
            AssertControlsLocked(true);
            Assert.That(target.Uses, Is.Zero);
            inventory.Close();

            Assert.That(inventory.RequestUseForTarget(target), Is.False);
            Assert.That(inventory.IsOpen, Is.False);
            AssertControlsLocked(false);
            Assert.That(target.Uses, Is.Zero);

            Assert.That(inventory.RequestUseForTarget(new UseTarget("key")), Is.True,
                "The first-attempt rule belongs to each lock, not to the item ID.");
            Assert.That(inventory.IsOpen, Is.True);
        }

        [Test]
        public void AcquiredUnheldKeyReopensInventoryOnEveryAttempt()
        {
            CreateController();
            var target = new UseTarget("key");
            Assert.That(inventory.RequestUseForTarget(target), Is.True);
            inventory.Close();
            Assert.That(inventory.RequestUseForTarget(target), Is.False);
            var key = Item("key", "Key");
            Assert.That(inventory.TryAdd(key), Is.True);

            Assert.That(inventory.RequestUseForTarget(target), Is.True);
            Assert.That(selectedName.text, Is.EqualTo(key.DisplayName));
            inventory.Close();
            Assert.That(inventory.RequestUseForTarget(target), Is.True);
            Assert.That(inventory.TryUse(key), Is.True);
            Assert.That(target.Uses, Is.EqualTo(1));
            Assert.That(inventory.HasItem(key.Id), Is.True);
        }

        [Test]
        public void MatchingSecondHeldItemUsesImmediatelyWithoutOpeningInventory()
        {
            var rope = Item("rope", "Rope");
            var key = Item("key", "Key");
            CreateController(rope, key);
            Assert.That(inventory.State.TryHold(rope), Is.True);
            Assert.That(inventory.State.TryHold(key), Is.True);
            var target = new UseTarget(key.Id);

            Assert.That(inventory.RequestUseForTarget(target), Is.True);

            Assert.That(target.Uses, Is.EqualTo(1));
            Assert.That(target.UsedEntry, Is.SameAs(key));
            Assert.That(inventory.IsOpen, Is.False);
            AssertControlsLocked(false);
            Assert.That(inventory.State.HeldItems[0], Is.SameAs(rope));
            Assert.That(inventory.State.HeldItems[1], Is.SameAs(key));
        }

        [Test]
        public void MissingKeySelectionRetainsPendingTargetUntilCancelled()
        {
            var wrong = Item("rope", "Rope");
            CreateController(wrong);
            var target = new UseTarget("key");
            string feedback = null;
            inventory.FeedbackRequested += key => feedback = key;

            Assert.That(inventory.RequestUseForTarget(target), Is.True);
            Assert.That(inventory.TryUse(wrong), Is.False);
            Assert.That(feedback, Is.EqualTo("inventory.wrong_item"));
            Assert.That(inventory.IsOpen, Is.True);
            Assert.That(target.Uses, Is.Zero);

            inventory.Close();
            Assert.That(inventory.TryUse(wrong), Is.False);
            Assert.That(feedback, Is.EqualTo("inventory.cannot_use_yet"));
            Assert.That(target.Uses, Is.Zero);
        }

        [Test]
        public void DestroyedPendingTargetClosesInventoryAndRestoresControls()
        {
            var key = Item("key", "Key");
            CreateController(key);
            var target = ScriptableObject.CreateInstance<InventoryTargetTestAsset>();
            target.requiredId = key.Id;
            try
            {
                Assert.That(inventory.RequestUseForTarget(target), Is.True);
                Object.DestroyImmediate(target);

                Assert.DoesNotThrow(() => InvokeLifecycle(inventory, "Update"));

                Assert.That(inventory.IsOpen, Is.False);
                AssertControlsLocked(false);
                Assert.That(inventory.State.GetItemAtSlot(0), Is.SameAs(key));
                Assert.That(inventory.TryUse(key), Is.False);
            }
            finally
            {
                if (target != null) Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void StandaloneUseDispatchesToPlayerHandlerAndPreservesItem()
        {
            var key = Item("key", "Key");
            CreateController(key);
            var handler = actor.AddComponent<InventoryUseHandlerTestComponent>();
            handler.requiredId = key.Id;
            int legacyUses = 0;
            inventory.UseRequested += _ => legacyUses++;

            Assert.That(inventory.TryUse(key), Is.True);

            Assert.That(handler.Uses, Is.EqualTo(1));
            Assert.That(handler.UsedEntry, Is.SameAs(key));
            Assert.That(handler.Actor, Is.SameAs(actor.transform));
            Assert.That(legacyUses, Is.Zero);
            Assert.That(inventory.HasItem(key.Id), Is.True);
        }

        [Test]
        public void MatchedStandaloneHandlerFailureIsSilentAndDoesNotFallBack()
        {
            var key = Item("key", "Key");
            CreateController(key);
            var handler = actor.AddComponent<InventoryUseHandlerTestComponent>();
            handler.requiredId = key.Id;
            handler.result = false;
            int legacyUses = 0;
            string feedback = null;
            inventory.UseRequested += _ => legacyUses++;
            inventory.FeedbackRequested += value => feedback = value;

            Assert.That(inventory.TryUse(key), Is.False);

            Assert.That(handler.Uses, Is.EqualTo(1));
            Assert.That(legacyUses, Is.Zero);
            Assert.That(feedback, Is.Null,
                "A remote outside its range is handled without an unavailable-item notice.");
            Assert.That(inventory.HasItem(key.Id), Is.True);
        }

        [Test]
        public void PendingLockRejectsWrongItemBeforeStandaloneHandler()
        {
            var key = Item("key", "Key");
            CreateController(key);
            var handler = actor.AddComponent<InventoryUseHandlerTestComponent>();
            handler.requiredId = key.Id;
            var target = new UseTarget("different.key");
            string feedback = null;
            inventory.FeedbackRequested += value => feedback = value;
            Assert.That(inventory.RequestUseForTarget(target), Is.True);

            Assert.That(inventory.TryUse(key), Is.False);

            Assert.That(handler.Uses, Is.Zero);
            Assert.That(target.Uses, Is.Zero);
            Assert.That(feedback, Is.EqualTo("inventory.wrong_item"));
            Assert.That(inventory.IsOpen, Is.True);
        }

        [Test]
        public void UnmatchedStandaloneItemKeepsLegacyUseEvent()
        {
            var item = Item("rope", "Rope");
            CreateController(item);
            var handler = actor.AddComponent<InventoryUseHandlerTestComponent>();
            handler.requiredId = "key";
            InventoryEntry legacyEntry = null;
            inventory.UseRequested += entry => legacyEntry = entry;

            Assert.That(inventory.TryUse(item), Is.True);

            Assert.That(handler.Uses, Is.Zero);
            Assert.That(legacyEntry, Is.SameAs(item));
            Assert.That(inventory.State.GetItemAtSlot(0), Is.SameAs(item));
        }

        private void CreateController(params InventoryEntry[] entries)
        {
            CreatePanel();
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player");
            map.AddAction("Move", InputActionType.Value).expectedControlType = "Vector2";
            map.AddAction("Look", InputActionType.Value).expectedControlType = "Vector2";
            foreach (string name in new[] { "Sprint", "Crouch", "Jump", "Interact", "Journal", "Pause" })
                map.AddAction(name, InputActionType.Button);
            actions.AddActionMap(map);

            actor = new GameObject("Inventory target actor");
            actor.SetActive(false);
            var input = actor.AddComponent<PlayerInputReader>();
            SetField(input, "inputActions", actions);
            movement = actor.AddComponent<PlayerController>();
            SetField(movement, "input", input);
            interactor = actor.AddComponent<PlayerInteractor>();
            SetField(interactor, "input", input);
            SetField(interactor, "viewpoint", actor.transform);
            inventory = actor.AddComponent<InventoryPanelController>();
            inventory.BindPanel(panel);
            var state = new InventoryState(2, 1);
            foreach (InventoryEntry entry in entries) Assert.That(state.TryAdd(entry), Is.True);
            inventory.RestoreState(state);

            // Resolve real components without entering Play Mode or replacing the controller's view.
            InvokeLifecycle(input, "Awake");
            InvokeLifecycle(movement, "Awake");
            InvokeLifecycle(interactor, "Awake");
            InvokeLifecycle(inventory, "Awake");
            // Observe feedback via the fixture event without animating the open scene's UI.
            SetField(inventory, "notifications", null);
            actor.SetActive(true);
            Assert.That(inventory.isActiveAndEnabled, Is.True);
            Assert.That(panel.activeSelf, Is.False);
        }

        private void CreatePanel()
        {
            canvasObject = new GameObject("Inventory target canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            eventObject = new GameObject("Inventory target events", typeof(EventSystem));
            panel = Rect("Panel", canvasObject.transform, new Vector2(900f, 600f)).gameObject;
            panel.AddComponent<Image>();
            RectTransform grid = Rect("Inventory", panel.transform, new Vector2(300f, 300f));
            RectTransform template = Rect("Slot 1", grid, new Vector2(100f, 100f));
            template.gameObject.AddComponent<Image>();
            RectTransform icon = Rect("image Item", template, new Vector2(70f, 70f));
            icon.gameObject.AddComponent<Image>();
            Rect("mark", icon, new Vector2(20f, 20f)).gameObject.AddComponent<Image>();
            RectTransform dock = Rect("Inventory Dock", panel.transform, new Vector2(300f, 300f));
            RectTransform scroll = Rect("Scroll View", dock, new Vector2(300f, 300f));
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Rect("Viewport", scroll, new Vector2(300f, 300f));
            RectTransform content = Rect("Content", viewport, new Vector2(300f, 300f));
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            RectTransform document = Rect("Slot 1", content, new Vector2(300f, 100f));
            document.gameObject.AddComponent<Image>();
            Rect("Icon", document, new Vector2(70f, 70f)).gameObject.AddComponent<Image>();
            Text("Title ", document);
            Text("Description", document);
            RectTransform information = Rect("Informaton Panel", panel.transform, new Vector2(200f, 300f));
            Rect("Preview Item", information, new Vector2(100f, 100f)).gameObject.AddComponent<Image>();
            selectedName = Text("Name Item", information);
            Text("Item Status", information);
            Text("Informations Text", information);
            RectTransform buttons = Rect("button panel", panel.transform, new Vector2(100f, 40f));
            GameObject close = Rect("button 1", buttons, new Vector2(100f, 40f)).gameObject;
            close.AddComponent<Image>();
            closeButton = close.AddComponent<Button>();
            Assert.That(InventoryView.CanBind(panel), Is.True);
        }

        private void AssertControlsLocked(bool locked)
        {
            Assert.That(movement.UiMovementLocked, Is.EqualTo(locked));
            Assert.That(interactor.UiInteractionBlocked, Is.EqualTo(locked));
        }

        private Button Slot(int index) => panel.transform.Find(
            "Inventory/Inventory Grid Viewport/Inventory Grid Content/Inventory Slot " + (index + 1)).GetComponent<Button>();
        private static InventoryEntry Item(string id, string name) => new InventoryEntry(id, name, InventoryEntryType.Item);
        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }
        private static TMP_Text Text(string name, Transform parent) =>
            Rect(name, parent, new Vector2(150f, 40f)).gameObject.AddComponent<TextMeshProUGUI>();
        private static void SetField(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void InvokeLifecycle(object target, string name) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private sealed class UseTarget : IInventoryItemUseTarget
        {
            private readonly string requiredId;
            private readonly bool result;
            public int Uses { get; private set; }
            public InventoryEntry UsedEntry { get; private set; }
            public Transform Actor { get; private set; }
            public UseTarget(string requiredId, bool result = true)
            {
                this.requiredId = requiredId;
                this.result = result;
            }
            public bool AcceptsItem(string id) => id == requiredId;
            public bool TryUseItem(InventoryEntry entry, Transform actor)
            {
                Uses++;
                UsedEntry = entry;
                Actor = actor;
                return result;
            }
        }
    }

    public sealed class InventoryTargetTestAsset : ScriptableObject, IInventoryItemUseTarget
    {
        public string requiredId;
        public bool AcceptsItem(string itemId) => itemId == requiredId;
        public bool TryUseItem(InventoryEntry entry, Transform actor) => AcceptsItem(entry.Id);
    }

    public sealed class InventoryUseHandlerTestComponent : MonoBehaviour, IInventoryItemUseHandler
    {
        public string requiredId;
        public bool result = true;
        public int Uses { get; private set; }
        public InventoryEntry UsedEntry { get; private set; }
        public Transform Actor { get; private set; }
        public bool HandlesItem(string itemId) => itemId == requiredId;
        public bool TryUseItem(InventoryEntry entry, Transform actor)
        {
            Uses++;
            UsedEntry = entry;
            Actor = actor;
            return result;
        }
    }
}
