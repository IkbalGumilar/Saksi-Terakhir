using System;
using SaksiTerakhir.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    public static class NpcDialogueUiSetup
    {
        private const string OfficeScenePath = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string PanelName = "NPC Dialogue Panel";

        [MenuItem("Saksi Terakhir/Ensure NPC Dialogue UI")]
        private static void EnsureFromMenu() => Debug.Log(Ensure());

        public static string Ensure()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != OfficeScenePath)
                throw new InvalidOperationException($"Open {OfficeScenePath} before setting up NPC dialogue UI.");

            Canvas canvas = FindOfficeCanvas(scene);
            if (canvas == null) throw new InvalidOperationException("The office scene has no screen-space Canvas.");

            Transform saveArea = canvas.transform.Find("Save Area");
            Transform actionTextObject = saveArea != null ? saveArea.Find("Action/Action Text") : null;
            InteractionPromptView prompt = saveArea != null ? saveArea.GetComponent<InteractionPromptView>() : null;
            if (prompt == null || actionTextObject == null
                || !actionTextObject.TryGetComponent(out TextMeshProUGUI actionText))
                throw new InvalidOperationException("The existing door interaction prompt is incomplete.");

            RectTransform actionRect = actionText.rectTransform;
            actionRect.sizeDelta = new Vector2(Mathf.Max(420f, actionRect.sizeDelta.x),
                Mathf.Max(50f, actionRect.sizeDelta.y));

            Transform existingPanel = canvas.transform.Find(PanelName) ?? saveArea.Find(PanelName);
            GameObject panel = existingPanel != null ? existingPanel.gameObject : CreatePanel(canvas.transform);
            if (!panel.TryGetComponent(out RectTransform _))
                throw new InvalidOperationException($"{PanelName} must have a RectTransform.");

            UnityEngine.UI.Image background = panel.GetComponent<UnityEngine.UI.Image>();
            if (background == null) background = panel.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.05f, 0.08f, 0.13f, 0.88f);
            background.raycastTarget = false;

            TextMeshProUGUI nameLabel = EnsureText(panel.transform, "NPC Name", actionText,
                new Vector2(0f, -12f), new Vector2(-36f, 32f), 24f,
                new Color(1f, 0.89f, 0.52f), FontStyles.Bold);
            TextMeshProUGUI lineLabel = EnsureText(panel.transform, "NPC Line", actionText,
                new Vector2(0f, -52f), new Vector2(-36f, 66f), 20f,
                Color.white, FontStyles.Normal);

            prompt.ConfigureNpcDialogue(panel, nameLabel, lineLabel);
            EditorUtility.SetDirty(prompt);
            EditorUtility.SetDirty(actionRect);
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the office scene with NPC dialogue UI.");

            return $"NPC dialogue UI is wired to the existing prompt in {OfficeScenePath}; door UI remains in place.";
        }

        private static Canvas FindOfficeCanvas(Scene scene)
        {
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas.gameObject.scene == scene && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    return canvas;
            }
            return null;
        }

        private static GameObject CreatePanel(Transform canvas)
        {
            var panel = new GameObject(PanelName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.layer = 5;
            panel.transform.SetParent(canvas, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 68f);
            rect.sizeDelta = new Vector2(660f, 138f);
            return panel;
        }

        private static TextMeshProUGUI EnsureText(Transform panel, string objectName,
            TextMeshProUGUI actionText, Vector2 position, Vector2 size, float fontSize,
            Color color, FontStyles style)
        {
            Transform existing = panel.Find(objectName);
            GameObject holder = existing != null
                ? existing.gameObject
                : new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            if (existing == null) holder.transform.SetParent(panel, false);
            if (!holder.TryGetComponent(out TextMeshProUGUI text))
                throw new InvalidOperationException($"{objectName} must be a TextMeshProUGUI object.");

            holder.layer = 5;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.font = actionText.font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            EditorUtility.SetDirty(text);
            EditorUtility.SetDirty(rect);
            return text;
        }
    }
}
