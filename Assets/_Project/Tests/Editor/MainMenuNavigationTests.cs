using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SaksiTerakhir.Tests
{
    public sealed class MainMenuNavigationTests
    {
        private GameObject root;
        private GameObject eventRoot;
        private EventSystem events;
        private EventSystem previousEvents;
        private MainMenuPresentation menu;
        private Button[] options;

        [SetUp]
        public void SetUp()
        {
            previousEvents = EventSystem.current;
            eventRoot = new GameObject("Menu Navigation Test Events", typeof(EventSystem));
            events = eventRoot.GetComponent<EventSystem>();
            InvokeEventLifecycle(events, "OnEnable");
            EventSystem.current = events;
            root = new GameObject("Menu Navigation Test", typeof(RectTransform));
            menu = root.AddComponent<MainMenuPresentation>();
            string[] names = { "Continue", "New Game", "Load Game", "Settings", "Extra", "Quit" };
            options = new Button[names.Length];
            for (int i = 0; i < options.Length; i++)
            {
                var option = new GameObject(names[i], typeof(RectTransform), typeof(Button));
                option.transform.SetParent(root.transform, false);
                options[i] = option.GetComponent<Button>();
            }
            // Reproduce the scene's authored explicit cycle, including Continue and Load.
            for (int i = 0; i < options.Length; i++)
                options[i].navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnUp = options[(i + options.Length - 1) % options.Length],
                    selectOnDown = options[(i + 1) % options.Length]
                };
            menu.Configure(options, null, null, new string[options.Length]);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (events != null) InvokeEventLifecycle(events, "OnDisable");
            if (eventRoot != null) Object.DestroyImmediate(eventRoot);
            if (previousEvents != null) EventSystem.current = previousEvents;
        }

        [Test]
        public void MissingSaveMovingUpFromSettingsSelectsTopNewGame()
        {
            menu.SetSaveAvailable(false);
            Move(3, MoveDirection.Up);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(options[1].gameObject));
        }

        [Test]
        public void AvailableSaveMovingDownFromNewGameSelectsLoad()
        {
            menu.SetSaveAvailable(true);
            Move(1, MoveDirection.Down);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(options[2].gameObject));
            Assert.That(options[2].interactable, Is.True);
        }

        [Test]
        public void MissingSaveMovingUpFromNewGameWrapsToQuit()
        {
            menu.SetSaveAvailable(false);
            Move(1, MoveDirection.Up);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(options[5].gameObject));
        }

        [Test]
        public void MissingSaveMovingDownFromQuitWrapsToNewGame()
        {
            menu.SetSaveAvailable(false);
            Move(5, MoveDirection.Down);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(options[1].gameObject));
        }

        [Test]
        public void SaveBecomingAvailableMakesContinueReachableAgain()
        {
            menu.SetSaveAvailable(false);
            menu.SetSaveAvailable(true);
            Move(1, MoveDirection.Up);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(options[0].gameObject));
            Move(5, MoveDirection.Down);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(options[0].gameObject));
        }

        [Test]
        public void RefreshSelectionSkipsAnOptionDisabledAfterSaveAvailabilityChanged()
        {
            menu.SetSaveAvailable(false);
            options[3].interactable = false;
            menu.RefreshRuntimeSelection();
            Move(1, MoveDirection.Down);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(options[4].gameObject));
        }

        [Test]
        public void RuntimeAvailabilityDoesNotChangeAuthoredOptionRectangles()
        {
            var rect = (RectTransform)options[1].transform;
            rect.anchoredPosition = new Vector2(27f, 123f);
            rect.sizeDelta = new Vector2(400f, 50f);
            menu.SetSaveAvailable(false);
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(27f, 123f)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(400f, 50f)));
        }

        private static void InvokeEventLifecycle(EventSystem instance, string method)
        {
            // EventSystem does not ExecuteAlways; this isolated EditMode fixture must
            // register its real component before testing Unity's selected-object routing.
            typeof(EventSystem).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(instance, null);
        }

        private void Move(int index, MoveDirection direction)
        {
            events.SetSelectedGameObject(options[index].gameObject);
            Vector2 vector = direction == MoveDirection.Up ? Vector2.up : Vector2.down;
            var data = new AxisEventData(events) { moveDir = direction, moveVector = vector };
            ExecuteEvents.Execute(options[index].gameObject, data, ExecuteEvents.moveHandler);
        }
    }
}
