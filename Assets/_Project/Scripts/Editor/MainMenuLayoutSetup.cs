using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SaksiTerakhir.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    /// <summary>One requested edit to the existing menu, with a scene backup and Unity-rendered previews.</summary>
    [InitializeOnLoad]
    public static class MainMenuLayoutSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string RequestPath = "Library/SaksiMainMenuLayout/request.json";
        private const string ResultPath = "Library/SaksiMainMenuLayout/result.json";
        private const string FontRoot = "Assets/_Project/UI/Fonts/TMP/";
        private const string ArtRoot = "Assets/_Project/UI/MainMenu/";
        private static double nextAttempt;
        private static readonly string[] Labels = { "Lanjutkan", "Permainan Baru", "Muat Permainan", "Pengaturan", "Ekstra", "Keluar" };
        private static readonly string[] RowNames = { "Continue", "New Game", "Load Game", "Settings", "Extra", "Quit" };
        private static readonly string[] HelpTexts = {
            "Melanjutkan dari simpanan terakhir.", "Memulai perjalanan baru.", "Memilih simpanan untuk dilanjutkan.",
            "Mengatur tampilan, suara, dan kontrol.", "Melihat konten tambahan.", "Keluar dari permainan." };

        [Serializable] private sealed class Request { public string id; public string outputDirectory; }
        [Serializable] private sealed class Result
        {
            public string id, status, error, scene, backup, preview1920, preview1280, preview1024;
            public int buttonCount;
            public bool sceneSaved;
            public TextReport[] texts;
        }
        [Serializable] private sealed class TextReport
        {
            public string name, text, font;
            public float size, characterSpacing;
            public bool autoSize, glyphsPresent;
        }

        static MainMenuLayoutSetup()
        {
            nextAttempt = EditorApplication.timeSinceStartup + 3;
            EditorApplication.update += ApplyRequestedEdit;
        }

        private static void ApplyRequestedEdit()
        {
            if (EditorApplication.timeSinceStartup < nextAttempt || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextAttempt = EditorApplication.timeSinceStartup + 3;
            if (!File.Exists(RequestPath)) { EditorApplication.update -= ApplyRequestedEdit; return; }
            Request request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
            if (File.Exists(ResultPath) && JsonUtility.FromJson<Result>(File.ReadAllText(ResultPath)).id == request.id)
            { EditorApplication.update -= ApplyRequestedEdit; return; }
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) return;
            EditorApplication.update -= ApplyRequestedEdit;
            var result = new Result { id = request.id, scene = ScenePath, status = "failed" };
            try
            {
                Apply(scene, request, result);
                result.status = "complete";
                Debug.Log("[Saksi Main Menu] Layout saved. Six menu options, reference typography, footer, and local responsive scaling applied.");
            }
            catch (Exception exception)
            {
                if (result.sceneSaved) result.status = "layout_saved_preview_failed";
                result.error = exception.ToString();
                Debug.LogException(exception);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
        }

        [MenuItem("Saksi Terakhir/UI/Match Main Menu Reference")]
        public static void ApplyFromMenu()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open Regional Archive Office before matching its menu.");
            var request = new Request { id = DateTime.UtcNow.ToString("O"), outputDirectory = "Library/SaksiMainMenuLayout/Previews" };
            var result = new Result { id = request.id, scene = ScenePath };
            Apply(scene, request, result);
            result.status = "complete";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
        }

        private static void Apply(Scene scene, Request request, Result result)
        {
            Transform canvas = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Canvas")?.transform;
            Transform menuTransform = canvas?.Find("Save Area/Menu") ?? canvas?.Find("Menu");
            if (menuTransform == null) throw new InvalidOperationException("Could not find Canvas/Save Area/Menu.");
            RectTransform menu = menuTransform.GetComponent<RectTransform>();
            RectTransform panel = menu.Find("Main Menu Panel")?.GetComponent<RectTransform>();
            RectTransform footer = menu.Find("Information")?.GetComponent<RectTransform>();
            if (panel == null || footer == null) throw new InvalidOperationException("Existing main-menu panel or Information footer is missing.");
            TMP_Text[] allTexts = menu.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text title = allTexts.Single(text => text.text.Trim() == "SAKSI TERAKHIR");
            TMP_Text[] labels = Labels.Select(value => allTexts.Single(text => text.text.Trim() == value)).ToArray();
            TMP_Text help = footer.Find("Information Text")?.GetComponent<TMP_Text>();
            TMP_Text choose = footer.Find("Inturction")?.GetComponent<TMP_Text>();
            TMP_Text confirm = footer.Find("Inturction (1)")?.GetComponent<TMP_Text>();
            TMP_Text enter = confirm != null ? confirm.transform.Find("Enter")?.GetComponent<TMP_Text>() : null;
            if (help == null || choose == null || confirm == null || enter == null)
                throw new InvalidOperationException("Existing footer text hierarchy differs from the inspected menu.");
            TMP_FontAsset displayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRoot + "Saksi Terakhir Display SDF.asset");
            TMP_FontAsset menuFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRoot + "Cormorant Garamond Medium SDF.asset");
            if (displayFont == null || menuFont == null) throw new InvalidOperationException("The installed menu font assets are missing.");
            Sprite shade = ImportSprite(ArtRoot + "Menu Shade.png");
            Sprite underlineSprite = ImportSprite(ArtRoot + "Underline Fade.png");

            Directory.CreateDirectory("Library/SaksiMainMenuLayout/Backups");
            result.backup = "Library/SaksiMainMenuLayout/Backups/Regional Archive Office-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, result.backup, true)) throw new InvalidOperationException("Could not save the pre-edit scene backup.");
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Match Saksi Terakhir main menu reference");
            var existingComponents = menu.GetComponentsInChildren<Component>(true).Where(c => c != null).Cast<UnityEngine.Object>();
            var existingObjects = menu.GetComponentsInChildren<Transform>(true).Select(t => (UnityEngine.Object)t.gameObject);
            Undo.RegisterCompleteObjectUndo(existingComponents.Concat(existingObjects).ToArray(), "Main menu reference layout");
            try
            {
                Stretch(panel);
                var panelImage = panel.GetComponent<UnityEngine.UI.Image>();
                if (panelImage != null)
                {
                    panelImage.sprite = shade;
                    panelImage.type = UnityEngine.UI.Image.Type.Simple;
                    panelImage.color = Color.white;
                    panelImage.raycastTarget = false;
                }
                TopLeft(title.rectTransform, 161, 206, 900, 145);
                StyleText(title, displayFont, 96, 18.3f, new Color32(214, 210, 200, 255));
                // This display title uses deliberate tracking rather than tight body-text kerning.
                title.fontFeatures = title.fontFeatures.Where(feature => feature != UnityEngine.TextCore.OTL_FeatureTag.kern).ToList();

                RectTransform list = panel.Find("Options")?.GetComponent<RectTransform>();
                if (list == null) list = NewRect("Options", panel);
                TopLeft(list, 164, 390, 420, 376);
                var layout = GetOrAdd<UnityEngine.UI.VerticalLayoutGroup>(list.gameObject);
                layout.padding = new RectOffset();
                layout.spacing = 8;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                var buttons = new UnityEngine.UI.Button[labels.Length];
                for (int i = 0; i < labels.Length; i++)
                {
                    RectTransform row = labels[i].transform.parent.GetComponent<RectTransform>();
                    Undo.SetTransformParent(row, list, "Group main-menu options");
                    row.name = RowNames[i] + " Background";
                    labels[i].name = RowNames[i];
                    row.localScale = Vector3.one;
                    row.localRotation = Quaternion.identity;
                    var element = GetOrAdd<UnityEngine.UI.LayoutElement>(row.gameObject);
                    element.minHeight = element.preferredHeight = 56;
                    element.flexibleHeight = 0;
                    element.minWidth = 280;
                    element.preferredWidth = 420;
                    element.flexibleWidth = 1;
                    var image = GetOrAdd<UnityEngine.UI.Image>(row.gameObject);
                    image.sprite = null;
                    image.type = UnityEngine.UI.Image.Type.Simple;
                    image.color = Color.clear;
                    image.raycastTarget = true;
                    Stretch(labels[i].rectTransform);
                    labels[i].text = Labels[i];
                    StyleText(labels[i], menuFont, 36, 1, Color.white);
                    buttons[i] = GetOrAdd<UnityEngine.UI.Button>(row.gameObject);
                    buttons[i].targetGraphic = labels[i];
                    buttons[i].transition = UnityEngine.UI.Selectable.Transition.ColorTint;
                    GetOrAdd<MainMenuOptionFocus>(row.gameObject);
                }
                var availableButtons = buttons.Where(button => button.isActiveAndEnabled && button.IsInteractable()).ToArray();
                for (int i = 0; i < availableButtons.Length; i++)
                {
                    var navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                        selectOnUp = availableButtons[(i + availableButtons.Length - 1) % availableButtons.Length],
                        selectOnDown = availableButtons[(i + 1) % availableButtons.Length] };
                    availableButtons[i].navigation = navigation;
                }
                RectTransform underline = menu.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(rect => rect.name == "Selection Underline");
                if (underline == null) underline = NewRect("Selection Underline", buttons[0].transform);
                var lineImage = GetOrAdd<UnityEngine.UI.Image>(underline.gameObject);
                lineImage.sprite = underlineSprite;
                lineImage.color = new Color32(214, 210, 200, 220);
                lineImage.type = UnityEngine.UI.Image.Type.Simple;
                lineImage.raycastTarget = false;

                TopLeft(footer, 109, 925, 680, 104);
                var footerImage = footer.GetComponent<UnityEngine.UI.Image>();
                if (footerImage != null) { footerImage.color = Color.clear; footerImage.raycastTarget = false; }
                TopLeft(help.rectTransform, 0, 0, 680, 40);
                StyleText(help, menuFont, 25, 1.5f, new Color32(200, 195, 184, 255));
                TopLeft(choose.rectTransform, 0, 44, 122, 40);
                choose.text = "↑ ↓ Pilih";
                StyleText(choose, menuFont, 25, 1, new Color32(224, 218, 204, 255));
                Transform oldIcons = choose.transform.Find("icon");
                if (oldIcons != null) oldIcons.gameObject.SetActive(false);
                TopLeft(confirm.rectTransform, 185, 44, 220, 40);
                confirm.text = "Konfirmasi";
                StyleText(confirm, menuFont, 25, 1, new Color32(224, 218, 204, 255));
                TopLeft(enter.rectTransform, -70, 0, 70, 40);
                StyleText(enter, menuFont, 25, 1, new Color32(224, 218, 204, 255));

                foreach (var text in menu.GetComponentsInChildren<TMP_Text>(true)) text.raycastTarget = false;
                RectTransform settingsStub = menu.Find("Senting Menu Panel")?.GetComponent<RectTransform>();
                if (settingsStub != null)
                {
                    settingsStub.name = "Settings Menu Panel";
                    var image = settingsStub.GetComponent<UnityEngine.UI.Image>();
                    if (image != null) image.raycastTarget = false;
                }
                MainMenuPresentation presentation = GetOrAdd<MainMenuPresentation>(menu.gameObject);
                presentation.Configure(buttons, underline, help, HelpTexts);
                EditorUtility.SetDirty(presentation);
                Canvas.ForceUpdateCanvases();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(list);
                foreach (TMP_Text text in menu.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
                result.buttonCount = buttons.Length;
                result.texts = menu.GetComponentsInChildren<TMP_Text>(true).Select(text => new TextReport {
                    name = text.name, text = text.text, font = AssetDatabase.GetAssetPath(text.font), size = text.fontSize,
                    characterSpacing = text.characterSpacing, autoSize = text.enableAutoSizing,
                    glyphsPresent = text.font.HasCharacters(text.text, out uint[] missing, true)
                }).ToArray();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the adjusted scene.");
                result.sceneSaved = true;
                Directory.CreateDirectory(request.outputDirectory);
                result.preview1920 = Path.Combine(request.outputDirectory, "menu-unity-1920x1080.png");
                result.preview1280 = Path.Combine(request.outputDirectory, "menu-unity-1280x720.png");
                result.preview1024 = Path.Combine(request.outputDirectory, "menu-unity-1024x768.png");
                RenderMenu(menu.gameObject, 1920, 1080, result.preview1920);
                RenderMenu(menu.gameObject, 1280, 720, result.preview1280);
                RenderMenu(menu.gameObject, 1024, 768, result.preview1024);
            }
            finally { Undo.CollapseUndoOperations(undoGroup); }
        }

        private static Sprite ImportSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Menu sprite is missing: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? Undo.AddComponent<T>(go);
        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Add main-menu layout element");
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        }
        private static void TopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        }
        private static void StyleText(TMP_Text text, TMP_FontAsset font, float size, float tracking, Color color)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.enableAutoSizing = false;
            text.fontSize = size;
            text.fontSizeMin = text.fontSizeMax = size;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.margin = Vector4.zero;
            text.characterSpacing = tracking;
            text.wordSpacing = text.lineSpacing = text.paragraphSpacing = 0;
            text.fontStyle = FontStyles.Normal;
            text.fontWeight = FontWeight.Regular;
            text.color = color;
            text.raycastTarget = false;
        }

        private static void RenderMenu(GameObject source, int width, int height, string output)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null, cameraObject = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                cameraObject = new GameObject("Menu Preview Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.scene = preview;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = height * 0.5f;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(35, 41, 43, 255);
                camera.cullingMask = 1 << 5;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                root = new GameObject("Menu Preview Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                root.layer = 5;
                SceneManager.MoveGameObjectToScene(root, preview);
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                GameObject clone = UnityEngine.Object.Instantiate(source, root.transform, false);
                clone.SetActive(true);
                Canvas.ForceUpdateCanvases();
                clone.GetComponent<MainMenuPresentation>().RefreshLayout();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(clone.GetComponent<RectTransform>());
                foreach (var text in clone.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(output, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
