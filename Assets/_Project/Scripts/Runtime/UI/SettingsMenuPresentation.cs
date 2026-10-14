using System;
using System.Collections.Generic;
using SaksiTerakhir.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SaksiTerakhir.UI
{
    /// <summary>
    /// Handles visual presentation, row binding, persistence, and input navigation for Settings Panel.
    /// Completely zero-allocation in hot paths.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SettingsMenuPresentation : MonoBehaviour
    {
        [Header("Categories")]
        [SerializeField] private Button[] categoryButtons;
        [SerializeField] private GameObject[] contentPanels;
        [SerializeField] private RectTransform activeCategoryOutline;

        [Header("Secondary Buttons")]
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button backButton;

        [Header("Footer")]
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private string[] categoryDescriptions = {
            "Mengatur cara bermain dan respons kontrol.",
            "Mengatur gerakan dan kenyamanan kamera.",
            "Mengatur bantuan dan informasi selama bermain.",
            "Mengatur mode layar dan resolusi gambar.",
            "Mengatur kualitas visual dan performa grafis.",
            "Mengatur volume suara dan audio permainan.",
            "Mengatur bahasa tampilan dan subtitle.",
            "Mengatur fitur aksesibilitas dan kenyamanan."
        };

        [Header("Visual Colors")]
        [SerializeField] private Color activeTextColor = new Color32(245, 242, 235, 255);
        [SerializeField] private Color inactiveTextColor = new Color32(180, 175, 165, 255);
        [SerializeField] private Color disabledTextColor = new Color32(110, 105, 100, 255);

        private int activeTabIndex = 0;
        private Action onClosedCallback;
        private bool isBound;

        // References for dynamic dependencies
        private Button trackedQuestLeftBtn;
        private Button trackedQuestRightBtn;
        private TMP_Text trackedQuestLabel;
        private TMP_Text trackedQuestValueText;

        public int ActiveTabIndex => activeTabIndex;
        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            FindSecondaryButtons();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                BindAllRows();
                RefreshAllControls();
            }
        }

        private void OnEnable()
        {
            SelectTab(activeTabIndex, true);
            if (Application.isPlaying)
            {
                if (!isBound) BindAllRows();
                RefreshAllControls();
            }
        }

        public void Open(Action onClosed = null)
        {
            onClosedCallback = onClosed;
            gameObject.SetActive(true);
            SelectTab(0, true);
            if (Application.isPlaying)
            {
                if (!isBound) BindAllRows();
                RefreshAllControls();
            }
        }

        public void Close()
        {
            GameSettingsManager.Save();
            gameObject.SetActive(false);
            Action callback = onClosedCallback;
            onClosedCallback = null;
            callback?.Invoke();
        }

        public void Configure(Button[] buttons, GameObject[] panels, RectTransform outline, TMP_Text desc, string[] descriptions)
        {
            categoryButtons = buttons;
            contentPanels = panels;
            activeCategoryOutline = outline;
            descriptionText = desc;
            if (descriptions != null && descriptions.Length > 0)
                categoryDescriptions = descriptions;

            FindSecondaryButtons();
            WireCategoryButtons();
            SelectTab(0, true);
        }

        private void FindSecondaryButtons()
        {
            Transform panel = transform.Find("Categories Panel");
            if (panel == null) return;

            Transform cred = panel.Find("Credits Option");
            if (cred != null) creditsButton = cred.GetComponent<Button>();

            Transform res = panel.Find("Reset Option");
            if (res != null) resetButton = res.GetComponent<Button>();

            Transform bak = panel.Find("Back Option");
            if (bak != null) backButton = bak.GetComponent<Button>();

            if (creditsButton != null)
            {
                creditsButton.onClick.RemoveAllListeners();
                creditsButton.onClick.AddListener(OnCreditsClicked);
            }
            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(OnResetClicked);
            }
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(Close);
            }
        }

        private void WireCategoryButtons()
        {
            if (categoryButtons == null) return;
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                int index = i;
                if (categoryButtons[i] != null)
                {
                    categoryButtons[i].onClick.RemoveAllListeners();
                    categoryButtons[i].onClick.AddListener(() => SelectTab(index));
                }
            }
        }

        public void SelectTab(int index, bool force = false)
        {
            if (categoryButtons == null || categoryButtons.Length == 0) return;
            if (!force && activeTabIndex == index) return;

            activeTabIndex = Mathf.Clamp(index, 0, categoryButtons.Length - 1);

            for (int i = 0; i < categoryButtons.Length; i++)
            {
                if (categoryButtons[i] == null) continue;
                bool isActive = (i == activeTabIndex);

                TMP_Text label = categoryButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.color = isActive ? activeTextColor : inactiveTextColor;
                }

                if (isActive && activeCategoryOutline != null)
                {
                    activeCategoryOutline.SetParent(categoryButtons[i].transform, false);
                    activeCategoryOutline.anchorMin = Vector2.zero;
                    activeCategoryOutline.anchorMax = Vector2.one;
                    activeCategoryOutline.offsetMin = Vector2.zero;
                    activeCategoryOutline.offsetMax = Vector2.zero;
                    activeCategoryOutline.localScale = Vector3.one;
                    activeCategoryOutline.SetAsFirstSibling();
                    activeCategoryOutline.gameObject.SetActive(true);
                }
            }

            if (contentPanels != null)
            {
                for (int i = 0; i < contentPanels.Length; i++)
                {
                    if (contentPanels[i] != null)
                    {
                        contentPanels[i].SetActive(i == activeTabIndex);
                    }
                }
            }

            if (descriptionText != null && categoryDescriptions != null && activeTabIndex < categoryDescriptions.Length)
            {
                descriptionText.text = categoryDescriptions[activeTabIndex];
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || !IsOpen) return;

            // Esc closes the settings menu
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            // Gamepad B button or cancel
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
            {
                Close();
                return;
            }
        }

        private void OnCreditsClicked()
        {
            if (descriptionText != null)
            {
                descriptionText.text = "Saksi Terakhir — Dikembangkan oleh Tim Saksi Terakhir (Unity 6 / URP).";
            }
        }

        private void OnResetClicked()
        {
            GameSettingsManager.ResetToDefaults();
            RefreshAllControls();
            if (descriptionText != null)
            {
                descriptionText.text = "Semua pengaturan telah diatur ulang ke nilai bawaan.";
            }
        }

        #region Row Binding & Settings Sync

        private void BindAllRows()
        {
            isBound = true;
            Transform contentPanel = transform.Find("Content Panel");
            if (contentPanel == null) return;

            // 1. KONTROL
            Transform controlList = contentPanel.Find("Control Content/Control Options List");
            if (controlList != null)
            {
                // Input Device
                BindStepper(controlList.Find("Input Device Row"),
                    new[] { "Keyboard & Mouse", "Gamepad" },
                    () => GameSettingsManager.Current.inputDevice == "Gamepad" ? 1 : 0,
                    idx =>
                    {
                        GameSettingsManager.Current.inputDevice = idx == 1 ? "Gamepad" : "Keyboard & Mouse";
                        GameSettingsManager.Save();
                    });

                // Mouse Sensitivity
                BindSlider(controlList.Find("Mouse Sensitivity Row"), 1f, 100f,
                    () => GameSettingsManager.Current.mouseSensitivity,
                    val =>
                    {
                        GameSettingsManager.Current.mouseSensitivity = val;
                        GameSettingsManager.Save();
                    });

                // Sprint Mode
                BindStepper(controlList.Find("Sprint Mode Row"),
                    new[] { "Tahan", "1 Klik (Beralih)" },
                    () => GameSettingsManager.Current.sprintMode == SprintMode.Toggle ? 1 : 0,
                    idx =>
                    {
                        GameSettingsManager.Current.sprintMode = idx == 1 ? SprintMode.Toggle : SprintMode.Hold;
                        GameSettingsManager.Save();
                    });

                // Gamepad Vibration
                BindStepper(controlList.Find("Gamepad Vibration Row"),
                    new[] { "Aktif", "Nonaktif" },
                    () => GameSettingsManager.Current.gamepadVibration ? 0 : 1,
                    idx =>
                    {
                        GameSettingsManager.Current.gamepadVibration = (idx == 0);
                        GameSettingsManager.Save();
                    });
            }

            // 2. KAMERA
            Transform cameraList = contentPanel.Find("Camera Content/Camera Options List");
            if (cameraList != null)
            {
                BindSlider(cameraList.Find("Horizontal Sensitivity Row"), 1f, 100f,
                    () => GameSettingsManager.Current.horizontalSensitivity,
                    val =>
                    {
                        GameSettingsManager.Current.horizontalSensitivity = val;
                        GameSettingsManager.Save();
                    });

                BindSlider(cameraList.Find("Vertical Sensitivity Row"), 1f, 100f,
                    () => GameSettingsManager.Current.verticalSensitivity,
                    val =>
                    {
                        GameSettingsManager.Current.verticalSensitivity = val;
                        GameSettingsManager.Save();
                    });

                BindStepper(cameraList.Find("Invert Vertical Axis Row"),
                    new[] { "Nonaktif", "Aktif" },
                    () => GameSettingsManager.Current.invertVerticalAxis ? 1 : 0,
                    idx =>
                    {
                        GameSettingsManager.Current.invertVerticalAxis = (idx == 1);
                        GameSettingsManager.Save();
                    });

                BindSlider(cameraList.Find("Field Of View Row"), 60f, 100f,
                    () => GameSettingsManager.Current.fieldOfView,
                    val =>
                    {
                        GameSettingsManager.Current.fieldOfView = val;
                        GameSettingsManager.Save();
                    }, "°");

                BindStepper(cameraList.Find("Camera Speed Row"),
                    new[] { "Lambat", "Normal", "Cepat" },
                    () => (int)GameSettingsManager.Current.cameraSpeed,
                    idx =>
                    {
                        GameSettingsManager.Current.cameraSpeed = (CameraSpeedSetting)idx;
                        GameSettingsManager.Save();
                    });
            }

            // 3. PERMAINAN (GAMEPLAY)
            Transform gameplayList = contentPanel.Find("Gameplay Content/Gameplay Options List");
            if (gameplayList != null)
            {
                // Main Quest
                BindStepper(gameplayList.Find("Main Quest Display Row"),
                    new[] { "Aktif", "Nonaktif" },
                    () => GameSettingsManager.Current.showMainQuest ? 0 : 1,
                    idx =>
                    {
                        GameSettingsManager.Current.showMainQuest = (idx == 0);
                        GameSettingsManager.Save();
                        UpdateTrackedQuestInteractability();
                    });

                // Tracked Quest (Cache references to disable when Main Quest is off)
                Transform trackedRow = gameplayList.Find("Tracked Quest Display Row");
                if (trackedRow != null)
                {
                    trackedQuestLabel = trackedRow.Find("Label")?.GetComponent<TMP_Text>();
                    Transform stepper = trackedRow.Find("Stepper");
                    if (stepper != null)
                    {
                        trackedQuestLeftBtn = stepper.Find("Left Arrow")?.GetComponent<Button>();
                        trackedQuestRightBtn = stepper.Find("Right Arrow")?.GetComponent<Button>();
                        trackedQuestValueText = stepper.Find("Value Text")?.GetComponent<TMP_Text>();
                    }
                }

                BindStepper(trackedRow,
                    new[] { "Aktif", "Nonaktif" },
                    () => GameSettingsManager.Current.showTrackedQuest ? 0 : 1,
                    idx =>
                    {
                        if (!GameSettingsManager.Current.showMainQuest) return;
                        GameSettingsManager.Current.showTrackedQuest = (idx == 0);
                        GameSettingsManager.Save();
                    });

                // Interaction Prompt
                BindStepper(gameplayList.Find("Interaction Prompt Row"),
                    new[] { "Aktif", "Nonaktif" },
                    () => GameSettingsManager.Current.interactionPrompt ? 0 : 1,
                    idx =>
                    {
                        GameSettingsManager.Current.interactionPrompt = (idx == 0);
                        GameSettingsManager.Save();
                    });
            }

            // 4. TAMPILAN (DISPLAY)
            Transform displayList = contentPanel.Find("Display Content/Display Options List");
            if (displayList != null)
            {
                BindStepper(displayList.Find("Screen Mode Row"),
                    new[] { "Layar Penuh", "Jendela" },
                    () => GameSettingsManager.Current.screenMode == ScreenModeSetting.Fullscreen ? 0 : 1,
                    idx =>
                    {
                        GameSettingsManager.Current.screenMode = idx == 0 ? ScreenModeSetting.Fullscreen : ScreenModeSetting.Windowed;
                        GameSettingsManager.Save();
                    });

                BindStepper(displayList.Find("Resolution Row"),
                    new[] { "1920 × 1080", "1600 × 900", "1366 × 768", "1280 × 720" },
                    () => Mathf.Clamp(GameSettingsManager.Current.resolutionIndex, 0, 3),
                    idx =>
                    {
                        GameSettingsManager.Current.resolutionIndex = idx;
                        GameSettingsManager.Save();
                    });

                BindSlider(displayList.Find("Brightness Row"), 0f, 100f,
                    () => GameSettingsManager.Current.brightness,
                    val =>
                    {
                        GameSettingsManager.Current.brightness = val;
                        GameSettingsManager.Save();
                    });

                BindStepper(displayList.Find("VSync Row"),
                    new[] { "Aktif", "Nonaktif" },
                    () => GameSettingsManager.Current.vSync ? 0 : 1,
                    idx =>
                    {
                        GameSettingsManager.Current.vSync = (idx == 0);
                        GameSettingsManager.Save();
                    });
            }

            // 5. GRAFIS (GRAPHICS)
            Transform graphicsList = contentPanel.Find("Graphics Content/Graphics Options List");
            if (graphicsList != null)
            {
                BindStepper(graphicsList.Find("Graphics Preset Row"),
                    new[] { "Rendah", "Sedang", "Tinggi" },
                    () => (int)GameSettingsManager.Current.qualityPreset,
                    idx =>
                    {
                        GameSettingsManager.Current.qualityPreset = (GraphicsPreset)idx;
                        GameSettingsManager.Save();
                    });

                BindStepper(graphicsList.Find("Shadow Quality Row"),
                    new[] { "Nonaktif", "Rendah", "Tinggi" },
                    () => (int)GameSettingsManager.Current.shadowQuality,
                    idx =>
                    {
                        GameSettingsManager.Current.shadowQuality = (ShadowQualitySetting)idx;
                        GameSettingsManager.Save();
                    });

                BindStepper(graphicsList.Find("Shadow Distance Row"),
                    new[] { "Dekat (15m)", "Sedang (25m)", "Jauh (40m)" },
                    () => (int)GameSettingsManager.Current.shadowDistance,
                    idx =>
                    {
                        GameSettingsManager.Current.shadowDistance = (ShadowDistanceSetting)idx;
                        GameSettingsManager.Save();
                    });

                BindStepper(graphicsList.Find("Texture Quality Row"),
                    new[] { "Rendah", "Sedang", "Tinggi" },
                    () => (int)GameSettingsManager.Current.textureQuality,
                    idx =>
                    {
                        GameSettingsManager.Current.textureQuality = (TextureQualitySetting)idx;
                        GameSettingsManager.Save();
                    });

                BindStepper(graphicsList.Find("Anti Aliasing Row"),
                    new[] { "Nonaktif", "FXAA", "SMAA" },
                    () => (int)GameSettingsManager.Current.antiAliasing,
                    idx =>
                    {
                        GameSettingsManager.Current.antiAliasing = (AntiAliasingSetting)idx;
                        GameSettingsManager.Save();
                    });

                BindStepper(graphicsList.Find("Render Scale Row"),
                    new[] { "70%", "85%", "100%" },
                    () => GameSettingsManager.Current.renderScalePercent == 70 ? 0 : (GameSettingsManager.Current.renderScalePercent == 85 ? 1 : 2),
                    idx =>
                    {
                        GameSettingsManager.Current.renderScalePercent = idx == 0 ? 70 : (idx == 1 ? 85 : 100);
                        GameSettingsManager.Save();
                    });

                BindStepper(graphicsList.Find("Frame Rate Limit Row"),
                    new[] { "30 FPS", "60 FPS", "Tanpa Batas" },
                    () => GameSettingsManager.Current.targetFrameRate == 30 ? 0 : (GameSettingsManager.Current.targetFrameRate == 60 ? 1 : 2),
                    idx =>
                    {
                        GameSettingsManager.Current.targetFrameRate = idx == 0 ? 30 : (idx == 1 ? 60 : -1);
                        GameSettingsManager.Save();
                    });
            }

            // 6. BAHASA / SUBTITLE
            Transform languageList = contentPanel.Find("Language Content/Language Options List");
            if (languageList != null)
            {
                BindStepper(languageList.Find("Speaker Name Row"),
                    new[] { "Aktif", "Nonaktif" },
                    () => GameSettingsManager.Current.showSpeakerName ? 0 : 1,
                    idx =>
                    {
                        GameSettingsManager.Current.showSpeakerName = (idx == 0);
                        GameSettingsManager.Save();
                    });

                BindSlider(languageList.Find("Text Speed Row"), 10f, 100f,
                    () => GameSettingsManager.Current.textSpeed,
                    val =>
                    {
                        GameSettingsManager.Current.textSpeed = val;
                        GameSettingsManager.Save();
                    });

                BindStepper(languageList.Find("Auto Advance Row"),
                    new[] { "Aktif", "Nonaktif" },
                    () => GameSettingsManager.Current.autoAdvance ? 0 : 1,
                    idx =>
                    {
                        GameSettingsManager.Current.autoAdvance = (idx == 0);
                        GameSettingsManager.Save();
                    });

                BindSlider(languageList.Find("Auto Advance Delay Row"), 0.1f, 5.0f,
                    () => GameSettingsManager.Current.autoAdvanceDelay,
                    val =>
                    {
                        GameSettingsManager.Current.autoAdvanceDelay = val;
                        GameSettingsManager.Save();
                    }, "s");

                BindStepper(languageList.Find("Text Animation Row"),
                    new[] { "Aktif", "Nonaktif" },
                    () => GameSettingsManager.Current.textAnimation ? 0 : 1,
                    idx =>
                    {
                        GameSettingsManager.Current.textAnimation = (idx == 0);
                        GameSettingsManager.Save();
                    });
            }
        }

        private void BindStepper(Transform rowTransform, string[] options, Func<int> getter, Action<int> setter)
        {
            if (rowTransform == null || options == null || options.Length == 0) return;
            Transform stepper = rowTransform.Find("Stepper");
            if (stepper == null) return;

            TMP_Text valText = stepper.Find("Value Text")?.GetComponent<TMP_Text>();
            Button leftBtn = stepper.Find("Left Arrow")?.GetComponent<Button>();
            Button rightBtn = stepper.Find("Right Arrow")?.GetComponent<Button>();

            void RefreshText()
            {
                if (valText != null && getter != null)
                {
                    int cur = Mathf.Clamp(getter(), 0, options.Length - 1);
                    valText.text = options[cur];
                }
            }

            if (leftBtn != null)
            {
                leftBtn.onClick.RemoveAllListeners();
                leftBtn.onClick.AddListener(() =>
                {
                    int cur = getter != null ? getter() : 0;
                    cur = (cur - 1 + options.Length) % options.Length;
                    setter?.Invoke(cur);
                    RefreshText();
                });
            }

            if (rightBtn != null)
            {
                rightBtn.onClick.RemoveAllListeners();
                rightBtn.onClick.AddListener(() =>
                {
                    int cur = getter != null ? getter() : 0;
                    cur = (cur + 1) % options.Length;
                    setter?.Invoke(cur);
                    RefreshText();
                });
            }

            RefreshText();
        }

        private void BindSlider(Transform rowTransform, float min, float max, Func<float> getter, Action<float> setter, string unit = "")
        {
            if (rowTransform == null) return;
            Transform sliderObj = rowTransform.Find("Slider Control");
            if (sliderObj == null) return;

            Slider slider = sliderObj.GetComponent<Slider>();
            TMP_Text valText = sliderObj.Find("Value Text")?.GetComponent<TMP_Text>();

            if (slider != null)
            {
                slider.minValue = min;
                slider.maxValue = max;
                if (getter != null) slider.value = getter();

                slider.onValueChanged.RemoveAllListeners();
                slider.onValueChanged.AddListener(v =>
                {
                    if (valText != null)
                    {
                        valText.text = string.IsNullOrEmpty(unit)
                            ? Mathf.RoundToInt(v).ToString()
                            : $"{v:0.0}{unit}";
                    }
                    setter?.Invoke(v);
                });
            }

            if (valText != null && getter != null)
            {
                float v = getter();
                valText.text = string.IsNullOrEmpty(unit)
                    ? Mathf.RoundToInt(v).ToString()
                    : $"{v:0.0}{unit}";
            }
        }

        public void RefreshAllControls()
        {
            BindAllRows();
            UpdateTrackedQuestInteractability();
        }

        private void UpdateTrackedQuestInteractability()
        {
            bool mainQuestActive = GameSettingsManager.Current.showMainQuest;

            if (trackedQuestLeftBtn != null) trackedQuestLeftBtn.interactable = mainQuestActive;
            if (trackedQuestRightBtn != null) trackedQuestRightBtn.interactable = mainQuestActive;

            Color targetColor = mainQuestActive ? activeTextColor : disabledTextColor;
            if (trackedQuestLabel != null) trackedQuestLabel.color = targetColor;
            if (trackedQuestValueText != null) trackedQuestValueText.color = targetColor;
        }

        #endregion
    }
}
