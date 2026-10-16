using System;
using System.IO;
using System.Linq;
using SaksiTerakhir.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SaksiTerakhir.EditorTools
{
    public static class SettingsMenuLayoutSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string FontRoot = "Assets/_Project/UI/Fonts/TMP/";
        private const string ShadePath = "Assets/_Project/UI/MainMenu/Menu Shade.png";
        private const string SettingsArtRoot = "Assets/_Project/UI/Settings/";

        private static readonly Color ColorTitle = new Color32(235, 230, 222, 255);
        private static readonly Color ColorActiveText = new Color32(245, 242, 235, 255);
        private static readonly Color ColorInactiveText = new Color32(180, 175, 165, 255);
        private static readonly Color ColorDivider = new Color32(255, 255, 255, 45);
        private static readonly Color ColorOutline = new Color32(245, 242, 235, 240);
        private static readonly Color ColorDescription = new Color32(195, 190, 180, 255);
        private static readonly Color ColorHint = new Color32(220, 215, 205, 255);

        private static readonly string[] Categories = {
            "Kontrol", "Kamera", "Permainan", "Tampilan", "Grafis", "Audio", "Bahasa", "Aksesibilitas"
        };

        private static readonly string[] CategoryDescriptions = {
            "Mengatur cara bermain dan respons kontrol.",
            "Mengatur gerakan dan kenyamanan kamera.",
            "Mengatur bantuan dan informasi selama bermain.",
            "Mengatur mode layar dan resolusi gambar.",
            "Mengatur kualitas visual dan performa grafis.",
            "Mengatur volume suara dan audio permainan.",
            "Mengatur bahasa tampilan dan subtitle.",
            "Mengatur fitur aksesibilitas dan kenyamanan."
        };

        [MenuItem("Saksi Terakhir/Settings/Build All Settings Tabs")]
        public static void BuildAllSettingsTabs()
        {
            BuildSettingsMenu("Kontrol");
        }

        [MenuItem("Saksi Terakhir/Settings/Build Graphics Menu")]
        public static void BuildGraphicsMenu()
        {
            BuildSettingsMenu("Grafis");
        }

        [MenuItem("Saksi Terakhir/Settings/Build Camera Menu")]
        public static void BuildCameraMenu()
        {
            BuildSettingsMenu("Kamera");
        }

        [MenuItem("Saksi Terakhir/Settings/Build Kontrol Menu")]
        public static void BuildControlsMenu()
        {
            BuildSettingsMenu("Kontrol");
        }

        [MenuItem("Saksi Terakhir/Settings/Build Gameplay Menu")]
        public static void BuildGameplayMenu()
        {
            BuildSettingsMenu("Permainan");
        }

        [MenuItem("Saksi Terakhir/Settings/Build Display Menu")]
        public static void BuildDisplayMenu()
        {
            BuildSettingsMenu("Tampilan");
        }

        [MenuItem("Saksi Terakhir/Settings/Build Audio Menu")]
        public static void BuildAudioMenu()
        {
            BuildSettingsMenu("Audio");
        }

        [MenuItem("Saksi Terakhir/Settings/Build Language Menu")]
        public static void BuildLanguageMenu()
        {
            BuildSettingsMenu("Bahasa");
        }

        [MenuItem("Saksi Terakhir/Settings/Build Accessibility Menu")]
        public static void BuildAccessibilityMenu()
        {
            BuildSettingsMenu("Aksesibilitas");
        }

        public static void BuildSettingsMenu(string initialActiveTab)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            EnsureSpriteAssets();

            TMP_FontAsset displayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRoot + "Saksi Terakhir Display SDF.asset");
            TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRoot + "Cormorant Garamond Medium SDF.asset");
            Sprite shadeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShadePath);
            Sprite outlineSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingsArtRoot + "OutlineBox.png");
            Sprite circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingsArtRoot + "CircleHandle.png");
            Sprite badgeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingsArtRoot + "KeyBadge.png");

            Transform canvas = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Canvas")?.transform;
            if (canvas == null) throw new InvalidOperationException("Canvas root not found.");

            Transform saveArea = canvas.Find("Save Area") ?? canvas;

            Transform settingsPanelTransform = saveArea.Find("Settings Panel");
            if (settingsPanelTransform == null)
            {
                settingsPanelTransform = canvas.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "Settings Panel");
            }

            if (settingsPanelTransform == null)
            {
                var newPanel = new GameObject("Settings Panel", typeof(RectTransform));
                newPanel.layer = 5;
                settingsPanelTransform = newPanel.transform;
            }

            // Ensure parent is directly Canvas > Save Area
            settingsPanelTransform.SetParent(saveArea, false);
            settingsPanelTransform.SetAsLastSibling();

            GameObject settingsPanel = settingsPanelTransform.gameObject;
            settingsPanel.layer = 5;
            settingsPanel.SetActive(true);

            // Backup scene before editing
            Directory.CreateDirectory("Library/SaksiSettingsLayout/Backups");
            string backup = "Library/SaksiSettingsLayout/Backups/Regional Archive Office-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".unity";
            EditorSceneManager.SaveScene(scene, backup, true);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Settings Menu - " + initialActiveTab);

            try
            {
                // Clean existing children
                while (settingsPanelTransform.childCount > 0)
                {
                    Transform child = settingsPanelTransform.GetChild(0);
                    Undo.DestroyObjectImmediate(child.gameObject);
                }

                Image panelImage = settingsPanel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = Color.clear;
                    panelImage.raycastTarget = false;
                }

                RectTransform panelRect = settingsPanel.GetComponent<RectTransform>();
                Stretch(panelRect);

                // 1. Background Shade (covers left 75%, smoothly fades to 0 at right)
                RectTransform shadeRect = CreateRect("Background Shade", panelRect);
                shadeRect.anchorMin = new Vector2(0f, 0f);
                shadeRect.anchorMax = new Vector2(0.75f, 1f);
                shadeRect.offsetMin = Vector2.zero;
                shadeRect.offsetMax = Vector2.zero;
                Image shadeImg = shadeRect.gameObject.AddComponent<Image>();
                shadeImg.sprite = shadeSprite;
                shadeImg.type = Image.Type.Simple;
                shadeImg.color = new Color32(20, 20, 22, 240);
                shadeImg.raycastTarget = false;

                // 2. Header Title "PENGATURAN"
                RectTransform titleRect = CreateRect("Header Title", panelRect);
                TopLeft(titleRect, 100f, 130f, 450f, 60f);
                TMP_Text titleText = titleRect.gameObject.AddComponent<TextMeshProUGUI>();
                StyleText(titleText, displayFont, 48f, 18f, ColorTitle);
                titleText.text = "PENGATURAN";

                // 3. Categories Panel (Left Column)
                RectTransform categoriesPanel = CreateRect("Categories Panel", panelRect);
                TopLeft(categoriesPanel, 100f, 240f, 380f, 640f);

                Button[] categoryButtons = new Button[Categories.Length];
                float currentY = 0f;
                const float categoryHeight = 46f;
                const float categorySpacing = 5f;

                for (int i = 0; i < Categories.Length; i++)
                {
                    string cat = Categories[i];
                    string objName = GetCategoryEnglishName(cat) + " Category";

                    RectTransform itemRect = CreateRect(objName, categoriesPanel);
                    TopLeft(itemRect, 0f, currentY, 380f, categoryHeight);

                    Image btnBg = itemRect.gameObject.AddComponent<Image>();
                    btnBg.color = Color.clear;
                    Button btn = itemRect.gameObject.AddComponent<Button>();
                    btn.targetGraphic = btnBg;
                    btn.transition = Selectable.Transition.None;
                    categoryButtons[i] = btn;

                    RectTransform textRect = CreateRect("Label", itemRect);
                    Stretch(textRect);
                    textRect.offsetMin = new Vector2(24f, 0f);
                    textRect.offsetMax = new Vector2(-16f, 0f);
                    TMP_Text catText = textRect.gameObject.AddComponent<TextMeshProUGUI>();
                    StyleText(catText, bodyFont, 30f, 1f, ColorInactiveText);
                    catText.alignment = TextAlignmentOptions.MidlineLeft;
                    catText.text = cat;

                    currentY += categoryHeight + categorySpacing;
                }

                // Category Divider Line
                currentY += 12f;
                RectTransform dividerRect = CreateRect("Category Divider", categoriesPanel);
                TopLeft(dividerRect, 0f, currentY, 380f, 1f);
                Image divImg = dividerRect.gameObject.AddComponent<Image>();
                divImg.color = ColorDivider;
                divImg.raycastTarget = false;
                currentY += 14f;

                // Secondary actions
                string[] secondary = { "Kredit", "Atur Ulang", "Kembali" };
                for (int i = 0; i < secondary.Length; i++)
                {
                    string sec = secondary[i];
                    string objName = GetSecondaryEnglishName(sec) + " Option";
                    RectTransform secRect = CreateRect(objName, categoriesPanel);
                    TopLeft(secRect, 0f, currentY, 380f, categoryHeight);

                    Image secBg = secRect.gameObject.AddComponent<Image>();
                    secBg.color = Color.clear;
                    Button secBtn = secRect.gameObject.AddComponent<Button>();
                    secBtn.targetGraphic = secBg;
                    secBtn.transition = Selectable.Transition.None;

                    RectTransform textRect = CreateRect("Label", secRect);
                    Stretch(textRect);
                    textRect.offsetMin = new Vector2(24f, 0f);
                    textRect.offsetMax = new Vector2(-16f, 0f);
                    TMP_Text secText = textRect.gameObject.AddComponent<TextMeshProUGUI>();
                    StyleText(secText, bodyFont, 30f, 1f, ColorInactiveText);
                    secText.alignment = TextAlignmentOptions.MidlineLeft;
                    secText.text = sec;

                    currentY += categoryHeight + categorySpacing;
                }

                // Shared Active Category Outline
                RectTransform outlineRect = CreateRect("Active Category Outline", categoriesPanel);
                Image outImg = outlineRect.gameObject.AddComponent<Image>();
                outImg.sprite = outlineSprite;
                outImg.type = Image.Type.Sliced;
                outImg.color = ColorOutline;
                outImg.raycastTarget = false;

                // 4. Content Panel (Center Column)
                RectTransform contentPanel = CreateRect("Content Panel", panelRect);
                TopLeft(contentPanel, 530f, 230f, 855f, 650f);

                // Build each category tab's content
                GameObject[] contentPanels = new GameObject[Categories.Length];
                contentPanels[0] = BuildControlContent(contentPanel, bodyFont, outlineSprite, circleSprite);
                contentPanels[1] = BuildCameraContent(contentPanel, bodyFont, outlineSprite, circleSprite);
                contentPanels[2] = BuildGameplayContent(contentPanel, bodyFont, outlineSprite);
                contentPanels[3] = BuildDisplayContent(contentPanel, bodyFont, outlineSprite, circleSprite);
                contentPanels[4] = BuildGraphicsContent(contentPanel, bodyFont, outlineSprite);
                contentPanels[5] = BuildAudioContent(contentPanel, bodyFont, outlineSprite, circleSprite);
                contentPanels[6] = BuildLanguageContent(contentPanel, bodyFont, outlineSprite, circleSprite);
                contentPanels[7] = BuildAccessibilityContent(contentPanel, bodyFont, outlineSprite, circleSprite);

                // 5. Footer Panel (Bottom Left)
                RectTransform footerPanel = CreateRect("Footer Panel", panelRect);
                TopLeft(footerPanel, 100f, 930f, 800f, 110f);

                // Description Text
                RectTransform descRect = CreateRect("Description Text", footerPanel);
                TopLeft(descRect, 0f, 0f, 750f, 32f);
                TMP_Text descText = descRect.gameObject.AddComponent<TextMeshProUGUI>();
                StyleText(descText, bodyFont, 24f, 1f, ColorDescription);

                // Input Hints
                RectTransform hintsRect = CreateRect("Input Hints", footerPanel);
                TopLeft(hintsRect, 0f, 42f, 800f, 40f);

                float hintX = 0f;

                // 1) Navigate: ↑↓ Pilih
                RectTransform navHint = CreateRect("Navigate Hint", hintsRect);
                TopLeft(navHint, hintX, 0f, 110f, 36f);
                TMP_Text navText = navHint.gameObject.AddComponent<TextMeshProUGUI>();
                StyleText(navText, bodyFont, 24f, 1f, ColorHint);
                navText.text = "↑↓  Pilih";
                hintX += 130f;

                // 2) Change: ←→ Ubah
                RectTransform chgHint = CreateRect("Change Hint", hintsRect);
                TopLeft(chgHint, hintX, 0f, 110f, 36f);
                TMP_Text chgText = chgHint.gameObject.AddComponent<TextMeshProUGUI>();
                StyleText(chgText, bodyFont, 24f, 1f, ColorHint);
                chgText.text = "←→  Ubah";
                hintX += 130f;

                // 3) Confirm: [Enter] Konfirmasi
                RectTransform cnfHint = CreateRect("Confirm Hint", hintsRect);
                TopLeft(cnfHint, hintX, 0f, 180f, 36f);
                RectTransform enterBadge = CreateRect("Key Badge", cnfHint);
                TopLeft(enterBadge, 0f, 3f, 62f, 28f);
                Image enterImg = enterBadge.gameObject.AddComponent<Image>();
                enterImg.sprite = badgeSprite;
                enterImg.type = Image.Type.Sliced;
                enterImg.color = ColorOutline;
                RectTransform enterTextRect = CreateRect("Badge Text", enterBadge);
                Stretch(enterTextRect);
                TMP_Text enterText = enterTextRect.gameObject.AddComponent<TextMeshProUGUI>();
                StyleText(enterText, bodyFont, 18f, 1f, ColorActiveText);
                enterText.alignment = TextAlignmentOptions.Center;
                enterText.text = "Enter";
                RectTransform enterLabelRect = CreateRect("Action Label", cnfHint);
                TopLeft(enterLabelRect, 72f, 0f, 110f, 36f);
                TMP_Text enterLabel = enterLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
                StyleText(enterLabel, bodyFont, 24f, 1f, ColorHint);
                enterLabel.text = "Konfirmasi";
                hintX += 200f;

                // 4) Back: [Esc] Kembali
                RectTransform escHint = CreateRect("Back Hint", hintsRect);
                TopLeft(escHint, hintX, 0f, 150f, 36f);
                RectTransform escBadge = CreateRect("Key Badge", escHint);
                TopLeft(escBadge, 0f, 3f, 48f, 28f);
                Image escImg = escBadge.gameObject.AddComponent<Image>();
                escImg.sprite = badgeSprite;
                escImg.type = Image.Type.Sliced;
                escImg.color = ColorOutline;
                RectTransform escTextRect = CreateRect("Badge Text", escBadge);
                Stretch(escTextRect);
                TMP_Text escText = escTextRect.gameObject.AddComponent<TextMeshProUGUI>();
                StyleText(escText, bodyFont, 18f, 1f, ColorActiveText);
                escText.alignment = TextAlignmentOptions.Center;
                escText.text = "Esc";
                RectTransform escLabelRect = CreateRect("Action Label", escHint);
                TopLeft(escLabelRect, 58f, 0f, 90f, 36f);
                TMP_Text escLabel = escLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
                StyleText(escLabel, bodyFont, 24f, 1f, ColorHint);
                escLabel.text = "Kembali";

                // Wire up SettingsMenuPresentation
                SettingsMenuPresentation presentation = settingsPanel.GetComponent<SettingsMenuPresentation>()
                    ?? settingsPanel.AddComponent<SettingsMenuPresentation>();

                presentation.Configure(categoryButtons, contentPanels, outlineRect, descText, CategoryDescriptions);

                // Select default tab
                int targetTab = Array.FindIndex(Categories, c => string.Equals(c, initialActiveTab, StringComparison.OrdinalIgnoreCase));
                if (targetTab < 0) targetTab = 0;
                presentation.SelectTab(targetTab, true);

                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text t in settingsPanel.GetComponentsInChildren<TMP_Text>(true))
                    t.ForceMeshUpdate(true);

                // Start inactive so Main Menu is displayed cleanly by default
                settingsPanel.SetActive(false);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Saksi Settings] All Settings tabs built successfully and scene saved in Canvas > Save Area.");
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static GameObject BuildControlContent(RectTransform parent, TMP_FontAsset bodyFont, Sprite outlineSprite, Sprite circleSprite)
        {
            RectTransform container = CreateRect("Control Content", parent);
            Stretch(container);

            CreateHeader(container, "Kontrol", bodyFont);

            RectTransform optionsList = CreateRect("Control Options List", container);
            TopLeft(optionsList, 0f, 75f, 855f, 500f);

            float rowY = 0f;
            const float rowHeight = 58f;

            CreateStepperRow(optionsList, "Input Device Row", "Perangkat Input", "Keyboard & Mouse",
                rowY, rowHeight, true, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateSliderRow(optionsList, "Mouse Sensitivity Row", "Sensitivitas Mouse", 60,
                rowY, rowHeight, false, bodyFont, circleSprite, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Sprint Mode Row", "Berlari", "Tahan",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Key Binding Row", "Atur Tombol", "Segera Hadir",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Gamepad Vibration Row", "Getaran Gamepad", "Aktif",
                rowY, rowHeight, false, bodyFont, outlineSprite);

            return container.gameObject;
        }

        private static GameObject BuildCameraContent(RectTransform parent, TMP_FontAsset bodyFont, Sprite outlineSprite, Sprite circleSprite)
        {
            RectTransform container = CreateRect("Camera Content", parent);
            Stretch(container);

            CreateHeader(container, "Kamera", bodyFont);

            RectTransform optionsList = CreateRect("Camera Options List", container);
            TopLeft(optionsList, 0f, 75f, 855f, 500f);

            float rowY = 0f;
            const float rowHeight = 58f;

            CreateSliderRow(optionsList, "Horizontal Sensitivity Row", "Sensitivitas Horizontal", 50,
                rowY, rowHeight, true, bodyFont, circleSprite, outlineSprite);
            rowY += rowHeight;

            CreateSliderRow(optionsList, "Vertical Sensitivity Row", "Sensitivitas Vertikal", 50,
                rowY, rowHeight, false, bodyFont, circleSprite, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Invert Vertical Axis Row", "Balik Sumbu Vertikal", "Nonaktif",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateSliderRow(optionsList, "Field Of View Row", "Bidang Pandang", 75,
                rowY, rowHeight, false, bodyFont, circleSprite, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Camera Speed Row", "Kecepatan Kamera", "Normal",
                rowY, rowHeight, false, bodyFont, outlineSprite);

            return container.gameObject;
        }

        private static GameObject BuildGameplayContent(RectTransform parent, TMP_FontAsset bodyFont, Sprite outlineSprite)
        {
            RectTransform container = CreateRect("Gameplay Content", parent);
            Stretch(container);

            CreateHeader(container, "Permainan", bodyFont);

            RectTransform optionsList = CreateRect("Gameplay Options List", container);
            TopLeft(optionsList, 0f, 75f, 855f, 500f);

            float rowY = 0f;
            const float rowHeight = 58f;

            CreateStepperRow(optionsList, "Main Quest Display Row", "Tampilkan Quest Utama", "Aktif",
                rowY, rowHeight, true, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Tracked Quest Display Row", "Tampilkan Quest Pantauan", "Aktif",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Interaction Prompt Row", "Petunjuk Interaksi", "Aktif",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Exploration Hint Row", "Petunjuk Eksplorasi", "Segera Hadir",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Tutorial Reminder Row", "Pengingat Tutorial", "Aktif",
                rowY, rowHeight, false, bodyFont, outlineSprite);

            return container.gameObject;
        }

        private static GameObject BuildDisplayContent(RectTransform parent, TMP_FontAsset bodyFont, Sprite outlineSprite, Sprite circleSprite)
        {
            RectTransform container = CreateRect("Display Content", parent);
            Stretch(container);

            CreateHeader(container, "Tampilan", bodyFont);

            RectTransform optionsList = CreateRect("Display Options List", container);
            TopLeft(optionsList, 0f, 75f, 855f, 500f);

            float rowY = 0f;
            const float rowHeight = 58f;

            CreateStepperRow(optionsList, "Screen Mode Row", "Mode Layar", "Layar Penuh",
                rowY, rowHeight, true, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Resolution Row", "Resolusi", "1920 × 1080",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateSliderRow(optionsList, "Brightness Row", "Kecerahan", 50,
                rowY, rowHeight, false, bodyFont, circleSprite, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "VSync Row", "Sinkronisasi Vertikal", "Aktif",
                rowY, rowHeight, false, bodyFont, outlineSprite);

            return container.gameObject;
        }

        private static GameObject BuildGraphicsContent(RectTransform parent, TMP_FontAsset bodyFont, Sprite outlineSprite)
        {
            RectTransform container = CreateRect("Graphics Content", parent);
            Stretch(container);

            CreateHeader(container, "Grafis", bodyFont);

            RectTransform optionsList = CreateRect("Graphics Options List", container);
            TopLeft(optionsList, 0f, 75f, 855f, 500f);

            float rowY = 0f;
            const float rowHeight = 58f;

            CreateStepperRow(optionsList, "Graphics Preset Row", "Preset Grafis", "Sedang",
                rowY, rowHeight, true, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Shadow Quality Row", "Kualitas Bayangan", "Rendah",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Shadow Distance Row", "Jarak Bayangan", "Sedang (25m)",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Texture Quality Row", "Kualitas Tekstur", "Sedang",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Anti Aliasing Row", "Anti-Aliasing", "FXAA",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Render Scale Row", "Skala Render", "100%",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Frame Rate Limit Row", "Batas Bingkai", "60 FPS",
                rowY, rowHeight, false, bodyFont, outlineSprite);

            return container.gameObject;
        }

        private static GameObject BuildAudioContent(RectTransform parent, TMP_FontAsset bodyFont, Sprite outlineSprite, Sprite circleSprite)
        {
            RectTransform container = CreateRect("Audio Content", parent);
            Stretch(container);

            CreateHeader(container, "Audio", bodyFont);

            RectTransform optionsList = CreateRect("Audio Options List", container);
            TopLeft(optionsList, 0f, 75f, 855f, 500f);

            float rowY = 0f;
            const float rowHeight = 58f;

            CreateSliderRow(optionsList, "Master Volume Row", "Volume Utama", 80,
                rowY, rowHeight, true, bodyFont, circleSprite, outlineSprite);
            rowY += rowHeight;

            CreateSliderRow(optionsList, "Music Volume Row", "Volume Musik", 60,
                rowY, rowHeight, false, bodyFont, circleSprite, outlineSprite);
            rowY += rowHeight;

            CreateSliderRow(optionsList, "Sfx Volume Row", "Volume Efek", 90,
                rowY, rowHeight, false, bodyFont, circleSprite, outlineSprite);
            rowY += rowHeight;

            CreateSliderRow(optionsList, "Dialogue Volume Row", "Volume Dialog", 100,
                rowY, rowHeight, false, bodyFont, circleSprite, outlineSprite);

            return container.gameObject;
        }

        private static GameObject BuildLanguageContent(RectTransform parent, TMP_FontAsset bodyFont, Sprite outlineSprite, Sprite circleSprite)
        {
            RectTransform container = CreateRect("Language Content", parent);
            Stretch(container);

            CreateHeader(container, "Bahasa", bodyFont);

            RectTransform optionsList = CreateRect("Language Options List", container);
            TopLeft(optionsList, 0f, 75f, 855f, 500f);

            float rowY = 0f;
            const float rowHeight = 58f;

            CreateStepperRow(optionsList, "Speaker Name Row", "Nama Pembicara", "Aktif",
                rowY, rowHeight, true, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateSliderRow(optionsList, "Text Speed Row", "Kecepatan Teks", 35,
                rowY, rowHeight, false, bodyFont, circleSprite, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Auto Advance Row", "Lanjut Otomatis", "Aktif",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateFloatSliderRow(optionsList, "Auto Advance Delay Row", "Jeda Lanjut Otomatis", 1.5f, 0.1f, 5.0f, "s",
                rowY, rowHeight, false, bodyFont, circleSprite, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Text Animation Row", "Animasi Teks", "Aktif",
                rowY, rowHeight, false, bodyFont, outlineSprite);

            return container.gameObject;
        }

        private static GameObject BuildAccessibilityContent(RectTransform parent, TMP_FontAsset bodyFont, Sprite outlineSprite, Sprite circleSprite)
        {
            RectTransform container = CreateRect("Accessibility Content", parent);
            Stretch(container);

            CreateHeader(container, "Aksesibilitas", bodyFont);

            RectTransform optionsList = CreateRect("Accessibility Options List", container);
            TopLeft(optionsList, 0f, 75f, 855f, 500f);

            float rowY = 0f;
            const float rowHeight = 58f;

            CreateStepperRow(optionsList, "Subtitle Size Row", "Ukuran Subtitle", "Sedang",
                rowY, rowHeight, true, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateSliderRow(optionsList, "Background Opacity Row", "Latar Belakang Teks", 50,
                rowY, rowHeight, false, bodyFont, circleSprite, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Speaker Color Row", "Warna Nama Pembicara", "Berwarna",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Camera Shake Row", "Goyangan Kamera", "Aktif",
                rowY, rowHeight, false, bodyFont, outlineSprite);
            rowY += rowHeight;

            CreateStepperRow(optionsList, "Head Bob Row", "Goyangan Berjalan", "Aktif",
                rowY, rowHeight, false, bodyFont, outlineSprite);

            return container.gameObject;
        }

        private static void CreateHeader(RectTransform parent, string title, TMP_FontAsset font)
        {
            RectTransform headerRect = CreateRect("Content Header", parent);
            TopLeft(headerRect, 0f, 0f, 400f, 45f);
            TMP_Text headerText = headerRect.gameObject.AddComponent<TextMeshProUGUI>();
            StyleText(headerText, font, 38f, 1f, ColorActiveText);
            headerText.alignment = TextAlignmentOptions.MidlineLeft;
            headerText.text = title;

            RectTransform contentDivider = CreateRect("Content Divider", parent);
            TopLeft(contentDivider, 0f, 55f, 855f, 1f);
            Image cDivImg = contentDivider.gameObject.AddComponent<Image>();
            cDivImg.color = ColorDivider;
            cDivImg.raycastTarget = false;
        }

        private static void CreateStepperRow(RectTransform parent, string rowName, string labelText, string valueText,
            float y, float height, bool isActive, TMP_FontAsset font, Sprite outlineSprite)
        {
            RectTransform row = CreateRect(rowName, parent);
            TopLeft(row, 0f, y, 855f, height);

            if (isActive)
            {
                RectTransform outline = CreateRect("Active Option Outline", row);
                Stretch(outline);
                Image outImg = outline.gameObject.AddComponent<Image>();
                outImg.sprite = outlineSprite;
                outImg.type = Image.Type.Sliced;
                outImg.color = ColorOutline;
                outImg.raycastTarget = false;
            }

            // Divider line below row
            RectTransform div = CreateRect("Row Divider", row);
            TopLeft(div, 0f, height - 1f, 855f, 1f);
            Image divImg = div.gameObject.AddComponent<Image>();
            divImg.color = new Color32(255, 255, 255, 30);
            divImg.raycastTarget = false;

            // Label
            RectTransform labelRect = CreateRect("Label", row);
            TopLeft(labelRect, 24f, 0f, 400f, height);
            TMP_Text label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            StyleText(label, font, 28f, 1f, isActive ? ColorActiveText : new Color32(230, 226, 218, 255));
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.text = labelText;

            // Stepper Container
            RectTransform stepper = CreateRect("Stepper", row);
            stepper.anchorMin = stepper.anchorMax = stepper.pivot = new Vector2(1f, 0.5f);
            stepper.anchoredPosition = new Vector2(-24f, 0f);
            stepper.sizeDelta = new Vector2(360f, height);

            // Left Arrow
            RectTransform leftArrowRect = CreateRect("Left Arrow", stepper);
            leftArrowRect.anchorMin = leftArrowRect.anchorMax = leftArrowRect.pivot = new Vector2(0f, 0.5f);
            leftArrowRect.anchoredPosition = new Vector2(0f, 0f);
            leftArrowRect.sizeDelta = new Vector2(30f, height);
            TMP_Text leftArrow = leftArrowRect.gameObject.AddComponent<TextMeshProUGUI>();
            StyleText(leftArrow, font, 26f, 1f, new Color32(200, 195, 185, 255));
            leftArrow.alignment = TextAlignmentOptions.Center;
            leftArrow.text = "<";
            leftArrow.raycastTarget = true;
            Button leftBtn = leftArrowRect.gameObject.AddComponent<Button>();
            leftBtn.targetGraphic = leftArrow;

            // Value Text
            RectTransform valRect = CreateRect("Value Text", stepper);
            valRect.anchorMin = valRect.anchorMax = valRect.pivot = new Vector2(0.5f, 0.5f);
            valRect.anchoredPosition = new Vector2(0f, 0f);
            valRect.sizeDelta = new Vector2(280f, height);
            TMP_Text val = valRect.gameObject.AddComponent<TextMeshProUGUI>();
            StyleText(val, font, 26f, 1f, ColorActiveText);
            val.alignment = TextAlignmentOptions.Center;
            val.text = valueText;

            // Right Arrow
            RectTransform rightArrowRect = CreateRect("Right Arrow", stepper);
            rightArrowRect.anchorMin = rightArrowRect.anchorMax = rightArrowRect.pivot = new Vector2(1f, 0.5f);
            rightArrowRect.anchoredPosition = new Vector2(0f, 0f);
            rightArrowRect.sizeDelta = new Vector2(30f, height);
            TMP_Text rightArrow = rightArrowRect.gameObject.AddComponent<TextMeshProUGUI>();
            StyleText(rightArrow, font, 26f, 1f, new Color32(200, 195, 185, 255));
            rightArrow.alignment = TextAlignmentOptions.Center;
            rightArrow.text = ">";
            rightArrow.raycastTarget = true;
            Button rightBtn = rightArrowRect.gameObject.AddComponent<Button>();
            rightBtn.targetGraphic = rightArrow;
        }

        private static void CreateSliderRow(RectTransform parent, string rowName, string labelText, int value,
            float y, float height, bool isActive, TMP_FontAsset font, Sprite circleSprite, Sprite outlineSprite)
        {
            RectTransform row = CreateRect(rowName, parent);
            TopLeft(row, 0f, y, 855f, height);

            if (isActive)
            {
                RectTransform outline = CreateRect("Active Option Outline", row);
                Stretch(outline);
                Image outImg = outline.gameObject.AddComponent<Image>();
                outImg.sprite = outlineSprite;
                outImg.type = Image.Type.Sliced;
                outImg.color = ColorOutline;
                outImg.raycastTarget = false;
            }

            // Divider line below row
            RectTransform div = CreateRect("Row Divider", row);
            TopLeft(div, 0f, height - 1f, 855f, 1f);
            Image divImg = div.gameObject.AddComponent<Image>();
            divImg.color = new Color32(255, 255, 255, 30);
            divImg.raycastTarget = false;

            // Label
            RectTransform labelRect = CreateRect("Label", row);
            TopLeft(labelRect, 24f, 0f, 400f, height);
            TMP_Text label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            StyleText(label, font, 28f, 1f, isActive ? ColorActiveText : new Color32(230, 226, 218, 255));
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.text = labelText;

            // Slider Container
            RectTransform slider = CreateRect("Slider Control", row);
            slider.anchorMin = slider.anchorMax = slider.pivot = new Vector2(1f, 0.5f);
            slider.anchoredPosition = new Vector2(-24f, 0f);
            slider.sizeDelta = new Vector2(360f, height);

            const float trackWidth = 260f;
            const float trackHeight = 3f;
            const float handleSize = 14f;

            // Track Background
            RectTransform trackBg = CreateRect("Track Background", slider);
            trackBg.anchorMin = trackBg.anchorMax = trackBg.pivot = new Vector2(0f, 0.5f);
            trackBg.anchoredPosition = new Vector2(10f, 0f);
            trackBg.sizeDelta = new Vector2(trackWidth, trackHeight);
            Image trackImg = trackBg.gameObject.AddComponent<Image>();
            trackImg.color = new Color32(100, 95, 90, 180);
            trackImg.raycastTarget = false;

            // Fill Area
            float normalized = Mathf.Clamp01(value / 100f);
            RectTransform fillArea = CreateRect("Fill Area", slider);
            fillArea.anchorMin = fillArea.anchorMax = fillArea.pivot = new Vector2(0f, 0.5f);
            fillArea.anchoredPosition = new Vector2(10f, 0f);
            fillArea.sizeDelta = new Vector2(trackWidth, trackHeight);

            RectTransform fill = CreateRect("Fill", fillArea);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(normalized, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.sizeDelta = Vector2.zero;
            Image fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = ColorActiveText;
            fillImg.raycastTarget = false;

            // Handle Slide Area (height = handleSize prevents vertical stretching by Unity Slider)
            RectTransform slideArea = CreateRect("Handle Slide Area", slider);
            slideArea.anchorMin = slideArea.anchorMax = slideArea.pivot = new Vector2(0f, 0.5f);
            slideArea.anchoredPosition = new Vector2(10f, 0f);
            slideArea.sizeDelta = new Vector2(trackWidth, handleSize);

            // Handle
            RectTransform handle = CreateRect("Handle", slideArea);
            handle.anchorMin = new Vector2(normalized, 0f);
            handle.anchorMax = new Vector2(normalized, 1f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(handleSize, 0f);
            Image handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = circleSprite;
            handleImg.color = ColorActiveText;
            handleImg.preserveAspect = true;
            handleImg.raycastTarget = true;

            // Slider Component on Slider Control
            Slider sliderComp = slider.gameObject.AddComponent<Slider>();
            sliderComp.direction = Slider.Direction.LeftToRight;
            sliderComp.minValue = 0f;
            sliderComp.maxValue = 100f;
            sliderComp.wholeNumbers = true;
            sliderComp.fillRect = fill;
            sliderComp.handleRect = handle;
            sliderComp.targetGraphic = handleImg;
            sliderComp.value = value;

            // Value Text
            RectTransform valRect = CreateRect("Value Text", slider);
            valRect.anchorMin = valRect.anchorMax = valRect.pivot = new Vector2(1f, 0.5f);
            valRect.anchoredPosition = new Vector2(0f, 0f);
            valRect.sizeDelta = new Vector2(60f, height);
            TMP_Text val = valRect.gameObject.AddComponent<TextMeshProUGUI>();
            StyleText(val, font, 26f, 1f, ColorActiveText);
            val.alignment = TextAlignmentOptions.MidlineRight;
            val.text = value.ToString();
        }

        private static void CreateFloatSliderRow(RectTransform parent, string rowName, string labelText, float value, float minValue, float maxValue, string formatSuffix,
            float y, float height, bool isActive, TMP_FontAsset font, Sprite circleSprite, Sprite outlineSprite)
        {
            RectTransform row = CreateRect(rowName, parent);
            TopLeft(row, 0f, y, 855f, height);

            if (isActive)
            {
                RectTransform outline = CreateRect("Active Option Outline", row);
                Stretch(outline);
                Image outImg = outline.gameObject.AddComponent<Image>();
                outImg.sprite = outlineSprite;
                outImg.type = Image.Type.Sliced;
                outImg.color = ColorOutline;
                outImg.raycastTarget = false;
            }

            // Divider line below row
            RectTransform div = CreateRect("Row Divider", row);
            TopLeft(div, 0f, height - 1f, 855f, 1f);
            Image divImg = div.gameObject.AddComponent<Image>();
            divImg.color = new Color32(255, 255, 255, 30);
            divImg.raycastTarget = false;

            // Label
            RectTransform labelRect = CreateRect("Label", row);
            TopLeft(labelRect, 24f, 0f, 400f, height);
            TMP_Text label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            StyleText(label, font, 28f, 1f, isActive ? ColorActiveText : new Color32(230, 226, 218, 255));
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.text = labelText;

            // Slider Container
            RectTransform slider = CreateRect("Slider Control", row);
            slider.anchorMin = slider.anchorMax = slider.pivot = new Vector2(1f, 0.5f);
            slider.anchoredPosition = new Vector2(-24f, 0f);
            slider.sizeDelta = new Vector2(360f, height);

            const float trackWidth = 260f;
            const float trackHeight = 3f;
            const float handleSize = 14f;

            // Track Background
            RectTransform trackBg = CreateRect("Track Background", slider);
            trackBg.anchorMin = trackBg.anchorMax = trackBg.pivot = new Vector2(0f, 0.5f);
            trackBg.anchoredPosition = new Vector2(10f, 0f);
            trackBg.sizeDelta = new Vector2(trackWidth, trackHeight);
            Image trackImg = trackBg.gameObject.AddComponent<Image>();
            trackImg.color = new Color32(100, 95, 90, 180);
            trackImg.raycastTarget = false;

            // Fill Area
            float range = Mathf.Max(0.001f, maxValue - minValue);
            float normalized = Mathf.Clamp01((value - minValue) / range);
            RectTransform fillArea = CreateRect("Fill Area", slider);
            fillArea.anchorMin = fillArea.anchorMax = fillArea.pivot = new Vector2(0f, 0.5f);
            fillArea.anchoredPosition = new Vector2(10f, 0f);
            fillArea.sizeDelta = new Vector2(trackWidth, trackHeight);

            RectTransform fill = CreateRect("Fill", fillArea);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(normalized, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.sizeDelta = Vector2.zero;
            Image fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = ColorActiveText;
            fillImg.raycastTarget = false;

            // Handle Slide Area (height = handleSize prevents vertical stretching by Unity Slider)
            RectTransform slideArea = CreateRect("Handle Slide Area", slider);
            slideArea.anchorMin = slideArea.anchorMax = slideArea.pivot = new Vector2(0f, 0.5f);
            slideArea.anchoredPosition = new Vector2(10f, 0f);
            slideArea.sizeDelta = new Vector2(trackWidth, handleSize);

            // Handle
            RectTransform handle = CreateRect("Handle", slideArea);
            handle.anchorMin = new Vector2(normalized, 0f);
            handle.anchorMax = new Vector2(normalized, 1f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(handleSize, 0f);
            Image handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = circleSprite;
            handleImg.color = ColorActiveText;
            handleImg.preserveAspect = true;
            handleImg.raycastTarget = true;

            // Slider Component on Slider Control
            Slider sliderComp = slider.gameObject.AddComponent<Slider>();
            sliderComp.direction = Slider.Direction.LeftToRight;
            sliderComp.minValue = minValue;
            sliderComp.maxValue = maxValue;
            sliderComp.wholeNumbers = false;
            sliderComp.fillRect = fill;
            sliderComp.handleRect = handle;
            sliderComp.targetGraphic = handleImg;
            sliderComp.value = value;

            // Value Text
            RectTransform valRect = CreateRect("Value Text", slider);
            valRect.anchorMin = valRect.anchorMax = valRect.pivot = new Vector2(1f, 0.5f);
            valRect.anchoredPosition = new Vector2(0f, 0f);
            valRect.sizeDelta = new Vector2(70f, height);
            TMP_Text val = valRect.gameObject.AddComponent<TextMeshProUGUI>();
            StyleText(val, font, 26f, 1f, ColorActiveText);
            val.alignment = TextAlignmentOptions.MidlineRight;
            val.text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0}{1}", value, formatSuffix);
        }

        private static string GetCategoryEnglishName(string indonesian)
        {
            switch (indonesian)
            {
                case "Kontrol": return "Control";
                case "Kamera": return "Camera";
                case "Permainan": return "Gameplay";
                case "Tampilan": return "Display";
                case "Grafis": return "Graphics";
                case "Audio": return "Audio";
                case "Bahasa": return "Language";
                case "Aksesibilitas": return "Accessibility";
                default: return indonesian;
            }
        }

        private static string GetSecondaryEnglishName(string indonesian)
        {
            switch (indonesian)
            {
                case "Kredit": return "Credits";
                case "Atur Ulang": return "Reset";
                case "Kembali": return "Back";
                default: return indonesian;
            }
        }

        private static void EnsureSpriteAssets()
        {
            EnsureSprite(SettingsArtRoot + "OutlineBox.png", new Vector4(4, 4, 4, 4));
            EnsureSprite(SettingsArtRoot + "CircleHandle.png", Vector4.zero);
            EnsureSprite(SettingsArtRoot + "KeyBadge.png", new Vector4(4, 4, 4, 4));
        }

        private static void EnsureSprite(string path, Vector4 border)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool modified = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                modified = true;
            }
            if (importer.spriteBorder != border)
            {
                importer.spriteBorder = border;
                modified = true;
            }
            if (importer.alphaIsTransparency != true)
            {
                importer.alphaIsTransparency = true;
                modified = true;
            }
            if (modified)
            {
                importer.SaveAndReimport();
            }
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void TopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
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
    }
}
