using System;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SaksiTerakhir.Story
{
    public sealed class ChapterOneChoiceView : MonoBehaviour
    {
        public readonly struct Option
        {
            public readonly string Label;
            public readonly Action Selected;
            public Option(string label, Action selected) { Label = label; Selected = selected; }
        }

        private static ChapterOneChoiceView active;
        private UnityEngine.Object owner;
        private PlayerController movement;
        private PlayerCameraController cameraController;
        private PlayerInteractor interactor;
        private PlayerInputReader input;
        private GameObject previousSelection;
        private CursorLockMode cursorLock;
        private bool cursorVisible;
        private bool movementLocked;
        private bool lookLocked;
        private bool interactionBlocked;
        private bool restored;

        public static bool IsOpen => active != null;

        public static bool Show(UnityEngine.Object choiceOwner, Transform actor, string title, params Option[] options)
        {
            if (active != null || actor == null || options == null || options.Length == 0) return false;
            Canvas canvas = null;
            foreach (GameObject root in actor.gameObject.scene.GetRootGameObjects())
            {
                if (root.name != "Canvas") continue;
                canvas = root.GetComponent<Canvas>();
                if (canvas != null) break;
            }
            if (canvas == null) return false;
            Transform parent = canvas.transform.Find("Save Area") ?? canvas.transform;
            var rootObject = new GameObject("Chapter One Choices", typeof(RectTransform));
            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            ChapterOneChoiceView view = rootObject.AddComponent<ChapterOneChoiceView>();
            active = view;
            view.owner = choiceOwner;
            view.LockPlayer(actor);
            view.Build(title, options, canvas);
            return true;
        }

        public static void CancelForOwner(UnityEngine.Object choiceOwner)
        {
            if (active != null && active.owner == choiceOwner) active.Close();
        }

        private void LockPlayer(Transform actor)
        {
            movement = actor.GetComponentInParent<PlayerController>();
            cameraController = actor.GetComponentInParent<PlayerCameraController>();
            interactor = actor.GetComponentInParent<PlayerInteractor>();
            input = actor.GetComponentInParent<PlayerInputReader>();
            cursorLock = Cursor.lockState;
            cursorVisible = Cursor.visible;
            movementLocked = movement != null && movement.UiMovementLocked;
            lookLocked = cameraController != null && cameraController.UiLookLocked;
            interactionBlocked = interactor != null && interactor.UiInteractionBlocked;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (movement != null) movement.SetUiMovementLocked(true);
            if (cameraController != null) cameraController.SetUiLookLocked(true);
            if (interactor != null) interactor.SetUiInteractionBlocked(true);
            if (input != null) input.PausePressed += Close;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Build(string title, Option[] options, Canvas canvas)
        {
            TMP_Text source = canvas.GetComponentInChildren<TMP_Text>(true);
            TMP_FontAsset font = source != null ? source.font : TMP_Settings.defaultFontAsset;
            RectTransform backdrop = Rect("Backdrop", transform, Vector2.zero, Vector2.one);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            RectTransform panel = Rect("Choice Panel", transform, Vector2.one * 0.5f, Vector2.one * 0.5f);
            panel.sizeDelta = new Vector2(620f, 120f + options.Length * 64f);
            panel.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.055f, 0.085f, 0.98f);
            TMP_Text heading = Label("Title", panel, title, font, 24f);
            heading.rectTransform.anchorMin = new Vector2(0f, 1f);
            heading.rectTransform.anchorMax = Vector2.one;
            heading.rectTransform.pivot = new Vector2(0.5f, 1f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            heading.rectTransform.sizeDelta = new Vector2(-40f, 72f);
            GameObject first = null;
            for (int index = 0; index < options.Length; index++)
            {
                Option option = options[index];
                RectTransform row = Rect("Choice " + (index + 1), panel, new Vector2(0f, 1f), Vector2.one);
                row.pivot = new Vector2(0.5f, 1f);
                row.anchoredPosition = new Vector2(0f, -96f - index * 64f);
                row.sizeDelta = new Vector2(-40f, 52f);
                Image background = row.gameObject.AddComponent<Image>();
                background.color = new Color(0.14f, 0.18f, 0.23f, 1f);
                Button button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(1f, 0.9f, 0.65f, 1f);
                colors.selectedColor = colors.highlightedColor;
                button.colors = colors;
                Label("Label", row, option.Label, font, 22f);
                button.onClick.AddListener(() => { Close(); option.Selected?.Invoke(); });
                if (first == null) first = row.gameObject;
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static TMP_Text Label(string name, RectTransform parent, string text, TMP_FontAsset font, float size)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.one);
            TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private void Close()
        {
            RestorePlayer();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void RestorePlayer()
        {
            if (restored) return;
            restored = true;
            if (active == this) active = null;
            if (input != null) input.PausePressed -= Close;
            if (movement != null) movement.SetUiMovementLocked(movementLocked);
            if (cameraController != null) cameraController.SetUiLookLocked(lookLocked);
            if (interactor != null) interactor.SetUiInteractionBlocked(interactionBlocked);
            bool gameplayActive = movement != null && movement.isActiveAndEnabled;
            Cursor.lockState = gameplayActive ? cursorLock : CursorLockMode.None;
            Cursor.visible = !gameplayActive || cursorVisible;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        }

        private void OnDisable() => RestorePlayer();
        private void OnDestroy() => RestorePlayer();
    }
}
