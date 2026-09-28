using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SaksiTerakhir.EditorTools
{
    public static class SettingsHeaderLayoutSetup
    {
        private const string OfficeScenePath = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string HeaderPath = "Save Area/Settings Panel/Header";
        private const float TabPreferredWidth = 200f;
        private const float TabHeight = 100f;
        private const float TabSpacing = 0f;
        private const float IconSize = 40f;
        private const float LabelWidth = 140f;
        private const float LabelHeight = 50f;
        private const float IconLabelSpacing = 12f;
        private const float LabelFontSize = 28f;

        private static readonly (string currentName, string correctedName)[] Tabs =
        {
            ("General", "General"),
            ("Graficts", "Graphics"),
            ("Audio", "Audio"),
            ("Control", "Controls"),
            ("Gameplay", "Gameplay"),
        };

        [MenuItem("Saksi Terakhir/Settings/Arrange Header Tabs")]
        private static void ArrangeFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != OfficeScenePath)
            {
                EditorUtility.DisplayDialog("Settings header", "Buka adegan Regional Archive Office terlebih dahulu.", "OK");
                return;
            }

            Transform canvas = FindRoot(scene, "Canvas");
            Transform header = canvas != null ? canvas.Find(HeaderPath) : null;
            if (header == null || header.gameObject.scene != scene)
            {
                EditorUtility.DisplayDialog("Settings header", "Tidak menemukan Canvas > Save Area > Settings Panel > Header.", "OK");
                return;
            }

            var tabs = new List<RectTransform>(Tabs.Length);
            foreach ((string currentName, string correctedName) in Tabs)
            {
                Transform tab = header.Find(currentName) ?? header.Find(correctedName);
                if (tab == null || !tab.TryGetComponent(out RectTransform rect))
                {
                    EditorUtility.DisplayDialog("Settings header",
                        $"Tab '{currentName}' tidak ditemukan atau tidak memiliki RectTransform. Tidak ada perubahan yang diterapkan.", "OK");
                    return;
                }
                tabs.Add(rect);
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Arrange settings header tabs");

            int removedAccessibilityTabs = RemoveAccessibilityTabs(header);
            for (int i = 0; i < tabs.Count; i++)
                ArrangeTab(tabs[i], Tabs[i].correctedName);

            HorizontalLayoutGroup headerLayout = GetOrAddLayoutGroup(header.gameObject);
            headerLayout.spacing = TabSpacing;
            headerLayout.padding = new RectOffset(0, 0, 0, 0);
            headerLayout.childAlignment = TextAnchor.MiddleCenter;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            RectTransform headerRect = header.GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(headerRect);
            foreach (RectTransform tab in tabs)
                LayoutRebuilder.ForceRebuildLayoutImmediate(tab);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            string result = $"Header dirapikan menjadi lima tab setara; {removedAccessibilityTabs} tab Aksesibilitas dihapus.";
            Debug.Log(result + " Tekan Ctrl+S untuk menyimpan scene.");
            EditorUtility.DisplayDialog("Settings header", result + " Tekan Ctrl+S untuk menyimpan scene.", "OK");
        }

        private static Transform FindRoot(Scene scene, string rootName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == rootName)
                    return root.transform;
            }
            return null;
        }

        private static int RemoveAccessibilityTabs(Transform header)
        {
            var toRemove = new List<GameObject>();
            for (int i = 0; i < header.childCount; i++)
            {
                Transform child = header.GetChild(i);
                if (IsAccessibilityTab(child))
                    toRemove.Add(child.gameObject);
            }

            foreach (GameObject tab in toRemove)
                Undo.DestroyObjectImmediate(tab);

            return toRemove.Count;
        }

        private static bool IsAccessibilityTab(Transform tab)
        {
            if (IsAccessibilityName(tab.name))
                return true;

            foreach (TMP_Text label in tab.GetComponentsInChildren<TMP_Text>(true))
            {
                if (IsAccessibilityName(label.text.Trim()))
                    return true;
            }
            return false;
        }

        private static bool IsAccessibilityName(string value)
        {
            return string.Equals(value, "Accessibility", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Aksesibilitas", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void ArrangeTab(RectTransform tab, string correctedName)
        {
            Undo.RecordObject(tab.gameObject, "Correct settings tab name");
            tab.name = correctedName;
            Undo.RecordObject(tab, "Set settings tab layout size");
            tab.sizeDelta = new Vector2(TabPreferredWidth, TabHeight);

            LayoutElement size = tab.GetComponent<LayoutElement>();
            if (size == null)
                size = Undo.AddComponent<LayoutElement>(tab.gameObject);
            else
                Undo.RecordObject(size, "Set settings tab layout size");
            size.minWidth = 0f;
            size.preferredWidth = TabPreferredWidth;
            size.flexibleWidth = 1f;
            size.minHeight = TabHeight;
            size.preferredHeight = TabHeight;
            size.flexibleHeight = 0f;

            HorizontalLayoutGroup tabLayout = GetOrAddLayoutGroup(tab.gameObject);
            tabLayout.spacing = IconLabelSpacing;
            tabLayout.padding = new RectOffset(16, 16, 0, 0);
            tabLayout.childAlignment = TextAnchor.MiddleCenter;
            tabLayout.childControlWidth = false;
            tabLayout.childControlHeight = false;
            tabLayout.childForceExpandWidth = false;
            tabLayout.childForceExpandHeight = false;

            Transform icon = tab.Find("Icon");
            if (icon != null && icon.TryGetComponent(out RectTransform iconRect))
            {
                Undo.RecordObject(iconRect, "Resize settings tab icon");
                iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            }

            TMP_Text[] labels = tab.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text label in labels)
            {
                Undo.RecordObject(label, "Correct settings tab label");
                label.text = correctedName;
                label.fontSize = LabelFontSize;
                label.enableAutoSizing = false;
                label.alignment = TextAlignmentOptions.MidlineLeft;

                Undo.RecordObject(label.rectTransform, "Set settings tab label layout");
                label.rectTransform.sizeDelta = new Vector2(LabelWidth, LabelHeight);
            }
        }

        private static HorizontalLayoutGroup GetOrAddLayoutGroup(GameObject target)
        {
            HorizontalLayoutGroup layout = target.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                return Undo.AddComponent<HorizontalLayoutGroup>(target);

            Undo.RecordObject(layout, "Arrange settings header tabs");
            return layout;
        }

        [MenuItem("Saksi Terakhir/Settings/Arrange Header Tabs", true)]
        private static bool CanArrangeFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            return scene.IsValid() && scene.path == OfficeScenePath;
        }
    }
}
