using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace SaksiTerakhir.EditorTools
{
    /// <summary>Creates the menu font assets once, without changing scenes or TMP defaults.</summary>
    [InitializeOnLoad]
    public static class TypographyFontSetup
    {
        private const string Root = "Assets/_Project/UI/Fonts";
        private const string DisplayPath = Root + "/TMP/Saksi Terakhir Display SDF.asset";
        private const string MenuPath = Root + "/TMP/Cormorant Garamond Medium SDF.asset";
        private const string ItalicPath = Root + "/TMP/Cormorant Garamond Medium Italic SDF.asset";
        private const string FallbackPath = Root + "/TMP/Cormorant Garamond Dynamic Fallback.asset";
        private const string PreviewPath = Root + "/Typography Preview.prefab";
        private static double nextAttempt;

        static TypographyFontSetup()
        {
            nextAttempt = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update += InstallWhenReady;
        }

        private static void InstallWhenReady()
        {
            if (EditorApplication.timeSinceStartup < nextAttempt || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            nextAttempt = EditorApplication.timeSinceStartup + 5;
            if (AssetDatabase.LoadAssetAtPath<Font>(Root + "/Source/SaksiTerakhirDisplay-Regular.ttf") == null ||
                AssetDatabase.LoadAssetAtPath<Font>(Root + "/Source/CormorantGaramond-Medium.ttf") == null ||
                AssetDatabase.LoadAssetAtPath<Font>(Root + "/Source/CormorantGaramond-MediumItalic.ttf") == null)
                return;
            EditorApplication.update -= InstallWhenReady;
            // Completed font installations need no work on later unrelated script reloads.
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayPath) != null &&
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MenuPath) != null &&
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ItalicPath) != null &&
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath) != null)
                return;
            try { InstallFonts(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Saksi Terakhir/UI/Install Typography Fonts")]
        public static void InstallFonts()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before installing typography fonts.");
            EnsureFolder(Root + "/TMP");
            FontEngine.InitializeFontEngine();
            TMP_FontAsset display = CreateFont(DisplayPath, "SaksiTerakhirDisplay-Regular.ttf", "DisplayCharacters.txt", false);
            TMP_FontAsset menu = CreateFont(MenuPath, "CormorantGaramond-Medium.ttf", "MenuCharacters.txt", false);
            TMP_FontAsset italic = CreateFont(ItalicPath, "CormorantGaramond-MediumItalic.ttf", "MenuItalicCharacters.txt", false);
            TMP_FontAsset fallback = CreateFont(FallbackPath, "CormorantGaramond-Medium.ttf", null, true);

            // Local fallbacks keep the new menu fonts self-contained.
            SetFallback(display, menu);
            SetFallback(menu, fallback);
            SetFallback(italic, fallback);
            TrimUnusedKerning(display);
            TrimUnusedKerning(menu);
            TrimUnusedKerning(italic);
            if (menu.fontWeightTable[4].italicTypeface != italic || menu.fontWeightTable[5].italicTypeface != italic)
            {
                menu.fontWeightTable[4].italicTypeface = italic;
                menu.fontWeightTable[5].italicTypeface = italic;
                EditorUtility.SetDirty(menu);
            }
            SaveOwnedAsset(display);
            SaveOwnedAsset(menu);
            SaveOwnedAsset(italic);
            SaveOwnedAsset(fallback);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath) == null)
                CreatePreview(display, menu, italic);
            WriteReport(display, menu, italic, fallback);
            Debug.Log("[Saksi Typography] Installed: display, menu, italic, dynamic fallback, and Typography Preview prefab. Assets/_Project/UI/Fonts");
        }

        private static TMP_FontAsset CreateFont(string path, string sourceName, string characterFile, bool dynamic)
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            Font source = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Source/" + sourceName);
            if (source == null) throw new InvalidOperationException("Font has not been imported: " + sourceName);
            string characters = dynamic ? string.Empty : File.ReadAllText(Root + "/Source/" + characterFile);
            TMP_FontAsset asset = null;
            for (int size = 1024; size <= 2048; size *= 2)
            {
                asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, size, size,
                    AtlasPopulationMode.Dynamic, false);
                if (asset == null) throw new InvalidOperationException("Unity could not load: " + sourceName);
                if (dynamic || asset.TryAddCharacters(characters, out string missing, true)) break;
                DestroyUnsavedFont(asset);
                asset = null;
            }
            if (asset == null) throw new InvalidOperationException("Could not bake all characters for " + sourceName);
            asset.name = Path.GetFileNameWithoutExtension(path);
            asset.atlasPopulationMode = dynamic ? AtlasPopulationMode.Dynamic : AtlasPopulationMode.Static;
            asset.isMultiAtlasTexturesEnabled = dynamic;
            var serializedFont = new SerializedObject(asset);
            serializedFont.FindProperty("m_ClearDynamicDataOnBuild").boolValue = dynamic;
            serializedFont.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
            foreach (Texture2D texture in asset.atlasTextures)
            {
                texture.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, asset);
            }
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            SaveOwnedAsset(asset);
            return asset;
        }

        private static void DestroyUnsavedFont(TMP_FontAsset font)
        {
            foreach (Texture2D texture in font.atlasTextures) UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(font.material);
            UnityEngine.Object.DestroyImmediate(font);
        }

        private static void SetFallback(TMP_FontAsset asset, TMP_FontAsset fallback)
        {
            if (asset.fallbackFontAssetTable != null && asset.fallbackFontAssetTable.Contains(fallback)) return;
            if (asset.fallbackFontAssetTable == null) asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            asset.fallbackFontAssetTable.Add(fallback);
            EditorUtility.SetDirty(asset);
        }

        private static void TrimUnusedKerning(TMP_FontAsset asset)
        {
            var bakedGlyphs = new HashSet<uint>();
            foreach (var glyph in asset.glyphTable) bakedGlyphs.Add(glyph.index);
            int removed = asset.fontFeatureTable.glyphPairAdjustmentRecords.RemoveAll(pair =>
                !bakedGlyphs.Contains(pair.firstAdjustmentRecord.glyphIndex) ||
                !bakedGlyphs.Contains(pair.secondAdjustmentRecord.glyphIndex));
            if (removed == 0) return;
            asset.ReadFontAssetDefinition();
            EditorUtility.SetDirty(asset);
        }

        private static void SaveOwnedAsset(TMP_FontAsset asset)
        {
            foreach (Texture2D texture in asset.atlasTextures) EditorUtility.SetDirty(texture);
            EditorUtility.SetDirty(asset.material);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        private static void EnsureFolder(string path)
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void CreatePreview(TMP_FontAsset display, TMP_FontAsset menu, TMP_FontAsset italic)
        {
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("Typography Preview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                SceneManager.MoveGameObjectToScene(root, previewScene);
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                GameObject background = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                background.transform.SetParent(root.transform, false);
                RectTransform backgroundRect = background.GetComponent<RectTransform>();
                backgroundRect.anchorMin = Vector2.zero;
                backgroundRect.anchorMax = Vector2.one;
                backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
                background.GetComponent<UnityEngine.UI.Image>().color = new Color32(20, 25, 27, 255);
                background.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
                AddText(root.transform, "Title", "SAKSI TERAKHIR", display, 140, new Vector2(140, -150), new Vector2(1650, 180), 7);
                AddText(root.transform, "Menu", "Lanjutkan\nPermainan Baru\nMuat Permainan\nPengaturan\nKredit\nKeluar", menu, 44,
                    new Vector2(150, -380), new Vector2(600, 430), 0);
                AddText(root.transform, "Settings Sample", "PENGATURAN\n\nVolume Utama                  ‹ 80 ›\nVolume Musik                    ‹ 60 ›\nVolume Efek                       ‹ 90 ›", menu, 40,
                    new Vector2(930, -400), new Vector2(860, 400), 0);
                AddText(root.transform, "Italic Sample", "Siapa yang masih mengingat?", italic, 34,
                    new Vector2(930, -840), new Vector2(860, 70), 0);
                AddText(root.transform, "Navigation", "↑ ↓ Pilih     Enter Konfirmasi     Esc Kembali", menu, 30,
                    new Vector2(150, -955), new Vector2(1300, 60), 0);
                Canvas.ForceUpdateCanvases();
                PrefabUtility.SaveAsPrefabAsset(root, PreviewPath, out bool success);
                if (!success) throw new InvalidOperationException("Could not save typography preview prefab.");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void AddText(Transform parent, string name, string content, TMP_FontAsset font, float size,
            Vector2 position, Vector2 dimensions, float spacing)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.characterSpacing = spacing;
            text.color = new Color32(224, 218, 204, 255);
            text.raycastTarget = false;
            text.text = content;
        }

        [Serializable] private class FontReport
        {
            public string path, family, style, populationMode;
            public int atlasWidth, atlasHeight, characters;
            public bool sourceFontPresent, materialSaved, atlasSaved, clearDynamicDataOnBuild;
        }
        [Serializable] private class InstallReport
        {
            public string unityVersion, installedUtc, previewPrefab;
            public FontReport[] fonts;
        }
        private static void WriteReport(params TMP_FontAsset[] fonts)
        {
            var reports = new List<FontReport>();
            foreach (TMP_FontAsset font in fonts)
            {
                reports.Add(new FontReport {
                    path = AssetDatabase.GetAssetPath(font), family = font.faceInfo.familyName,
                    style = font.faceInfo.styleName, populationMode = font.atlasPopulationMode.ToString(),
                    atlasWidth = font.atlasWidth, atlasHeight = font.atlasHeight, characters = font.characterTable.Count,
                    sourceFontPresent = font.sourceFontFile != null,
                    materialSaved = AssetDatabase.Contains(font.material), atlasSaved = AssetDatabase.Contains(font.atlasTexture),
                    clearDynamicDataOnBuild = new SerializedObject(font).FindProperty("m_ClearDynamicDataOnBuild").boolValue
                });
            }
            var report = new InstallReport { unityVersion = Application.unityVersion,
                installedUtc = DateTime.UtcNow.ToString("O"), previewPrefab = PreviewPath, fonts = reports.ToArray() };
            File.WriteAllText("SourceArt/Typography/unity-font-install-report.json", JsonUtility.ToJson(report, true));
        }
    }
}
