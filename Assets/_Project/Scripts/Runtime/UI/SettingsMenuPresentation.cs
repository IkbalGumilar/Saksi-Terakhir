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
    [RequireComponent(typeof(CanvasGroup))]
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

        [Header("Outline Animation")]
        [SerializeField] private float outlinePulseSpeed = 5f;
        private const float MinOutlineAlpha = 10f / 255f;
        private const float MaxOutlineAlpha = 200f / 255f;
        private Image outlineImage;

        private int activeTabIndex = 0;
        private Action onClosedCallback;
        private bool isBound;
        private bool locked;
        private GameObject lastSelected;
        private readonly List<Button> navigationList = new List<Button>(12);

        // References for dynamic dependencies
        private Button trackedQuestLeftBtn;
        private Button trackedQuestRightBtn;
        private TMP_Text trackedQuestLabel;
        private TMP_Text trackedQuestValueText;

        public int ActiveTabIndex => activeTabIndex;
        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            CacheOutlineImage();
            FindSecondaryButtons();
            // onClick listeners added in edit mode are not serialized, so wire them again at runtime.
            WireCategoryButtons();
            RebuildNavigation();
        }

        private RectTransform activeOptionOutline;
        private Image activeOptionOutlineImage;
        private Transform currentHoveredOptionRow;
        private Coroutine vibrationTestRoutine;

        private void CacheOutlineImage()
        {
            if (activeCategoryOutline != null)
            {
                outlineImage = activeCategoryOutline.GetComponent<Image>();
            }
        }

        private void Start()
        {
            if (Application.isPlaying && !isBound)
            {
                RefreshAllControls();
            }
        }

        private void OnEnable()
        {
            SelectTab(activeTabIndex, true);
            if (Application.isPlaying)
            {
                RefreshAllControls();
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                GameSettingsManager.StopGamepadVibration();
            }
        }

        public void Open(Action onClosed = null)
        {
            onClosedCallback = onClosed;
            gameObject.SetActive(true);
            SelectTab(0, true);
            if (Application.isPlaying)
            {
                RefreshAllControls();
                RebuildNavigation();
                lastSelected = null;
                FocusActiveCategory();
            }
        }

        /// <summary>Blocks input while the panel slides in or out.</summary>
        public void SetInteractable(bool value)
        {
            locked = !value;
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.interactable = value;
        }

        public void Close()
        {
            if (locked) return;
            GameSettingsManager.Save();
            // The owner keeps the panel active until its own closing animation ends.
            if (onClosedCallback != null) onClosedCallback();
            else gameObject.SetActive(false);
        }

        private void FocusActiveCategory()
        {
            EventSystem events = EventSystem.current;
            if (events == null || categoryButtons == null || categoryButtons.Length == 0) return;
            Button target = categoryButtons[Mathf.Clamp(activeTabIndex, 0, categoryButtons.Length - 1)];
            if (target != null && target.isActiveAndEnabled) events.SetSelectedGameObject(target.gameObject);
        }

        /// <summary>Circular up/down order through categories, then Credits, Reset and Back.</summary>
        private void RebuildNavigation()
        {
            navigationList.Clear();
            if (categoryButtons != null)
                foreach (Button b in categoryButtons) if (b != null) navigationList.Add(b);
            if (creditsButton != null) navigationList.Add(creditsButton);
            if (resetButton != null) navigationList.Add(resetButton);
            if (backButton != null) navigationList.Add(backButton);
            for (int i = 0; i < navigationList.Count; i++)
            {
                UnityEngine.UI.Navigation nav = navigationList[i].navigation;
                nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                nav.selectOnUp = navigationList[(i + navigationList.Count - 1) % navigationList.Count];
                nav.selectOnDown = navigationList[(i + 1) % navigationList.Count];
                nav.selectOnLeft = null;
                nav.selectOnRight = null;
                navigationList[i].navigation = nav;
            }
        }

        /// <summary>Follows the EventSystem selection like the main menu does.</summary>
        private void TrackSelection()
        {
            EventSystem events = EventSystem.current;
            if (events == null) return;
            GameObject current = events.currentSelectedGameObject;
            if (current == null)
            {
                FocusActiveCategory();
                return;
            }
            if (current == lastSelected) return;
            lastSelected = current;
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                if (categoryButtons[i] != null && categoryButtons[i].gameObject == current)
                {
                    SelectTab(i);
                    break;
                }
            }
            PaintSecondary(creditsButton, current);
            PaintSecondary(resetButton, current);
            PaintSecondary(backButton, current);
        }

        private void PaintSecondary(Button button, GameObject current)
        {
            if (button == null) return;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = button.gameObject == current ? activeTextColor : inactiveTextColor;
        }

        public void Configure(Button[] buttons, GameObject[] panels, RectTransform outline, TMP_Text desc, string[] descriptions)
        {
            categoryButtons = buttons;
            contentPanels = panels;
            activeCategoryOutline = outline;
            CacheOutlineImage();
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
                Button button = categoryButtons[i];
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectTab(index));

                // Highlight (hover) selects the row; selecting a row shows its tab immediately.
                EventTrigger trigger = button.GetComponent<EventTrigger>();
                if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
                trigger.triggers.Clear();
                var hover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                hover.callback.AddListener(_ =>
                {
                    if (!locked && EventSystem.current != null)
                        EventSystem.current.SetSelectedGameObject(button.gameObject);
                });
                trigger.triggers.Add(hover);
                var select = new EventTrigger.Entry { eventID = EventTriggerType.Select };
                select.callback.AddListener(_ => SelectTab(index));
                trigger.triggers.Add(select);
            }
        }

        /// <summary>Esc/B: leave the Control category first, then leave Settings.</summary>
        private void HandleBack()
        {
            EventSystem events = EventSystem.current;
            GameObject current = events != null ? events.currentSelectedGameObject : null;
            bool onControlTab = activeTabIndex == 0 && categoryButtons != null && categoryButtons.Length > 0
                && categoryButtons[0] != null && current == categoryButtons[0].gameObject;
            if (onControlTab)
            {
                Close();
                return;
            }
            SelectTab(0);
            if (events != null && categoryButtons != null && categoryButtons.Length > 0 && categoryButtons[0] != null)
                events.SetSelectedGameObject(categoryButtons[0].gameObject);
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

            SelectDefaultOptionRowInActiveTab();

            if (descriptionText != null && categoryDescriptions != null && activeTabIndex < categoryDescriptions.Length)
            {
                descriptionText.text = categoryDescriptions[activeTabIndex];
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || !IsOpen) return;
            AnimateOutlinePulse();
            if (locked) return;
            TrackSelection();
            HandleOptionHorizontalInput();

            // Esc (or gamepad B): back to the Control tab first, then out of Settings
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back) HandleBack();
        }

        private void HandleOptionHorizontalInput()
        {
            if (currentHoveredOptionRow == null || !currentHoveredOptionRow.gameObject.activeInHierarchy) return;

            bool left = (Keyboard.current != null && (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame))
                || (Gamepad.current != null && Gamepad.current.dpad.left.wasPressedThisFrame);
            bool right = (Keyboard.current != null && (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame))
                || (Gamepad.current != null && Gamepad.current.dpad.right.wasPressedThisFrame);

            if (!left && !right) return;

            Transform stepper = currentHoveredOptionRow.Find("Stepper");
            if (stepper != null)
            {
                Button btn = GetComponentAt<Button>(stepper, left ? "Left Arrow" : "Right Arrow");
                if (btn != null && btn.interactable)
                {
                    btn.onClick.Invoke();
                }
                return;
            }

            Slider slider = GetComponentAt<Slider>(currentHoveredOptionRow, "Slider Control");
            if (slider != null && slider.interactable)
            {
                float step = slider.wholeNumbers ? 5f : 0.1f;
                float next = Mathf.Clamp(slider.value + (right ? step : -step), slider.minValue, slider.maxValue);
                slider.value = slider.wholeNumbers ? Mathf.Round(next) : Mathf.Round(next * 10f) / 10f;
            }
        }

        private void AnimateOutlinePulse()
        {
            float t = (Mathf.Sin(Time.unscaledTime * outlinePulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(MinOutlineAlpha, MaxOutlineAlpha, t);

            if (outlineImage == null && activeCategoryOutline != null)
            {
                outlineImage = activeCategoryOutline.GetComponent<Image>();
            }
            if (outlineImage != null && activeCategoryOutline != null && activeCategoryOutline.gameObject.activeInHierarchy)
            {
                Color c = outlineImage.color;
                c.a = alpha;
                outlineImage.color = c;
            }

            if (activeOptionOutlineImage != null && activeOptionOutline != null && activeOptionOutline.gameObject.activeInHierarchy)
            {
                Color oc = activeOptionOutlineImage.color;
                oc.a = alpha;
                activeOptionOutlineImage.color = oc;
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

        private System.Collections.IEnumerator TestGamepadVibrationRoutine()
        {
            GameSettingsManager.TriggerGamepadVibration(0.25f, 0.25f);
            float end = Time.unscaledTime + 0.15f;
            while (Time.unscaledTime < end)
            {
                yield return null;
            }
            GameSettingsManager.StopGamepadVibration();
            vibrationTestRoutine = null;
        }

        #region Row Binding & Settings Sync

        private static T GetComponentAt<T>(Transform root, string path) where T : Component
        {
            if (root == null) return null;
            Transform child = root.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

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
                        if (idx == 1 && Gamepad.current == null && descriptionText != null)
                        {
                            descriptionText.text = "Mode Gamepad dipilih (kontroler belum terdeteksi, Keyboard & Mouse tetap aktif).";
                        }
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

                // Key Binding (Disabled for now per specification)
                SetRowDisabled(controlList.Find("Key Binding Row"));

                // Gamepad Vibration
                BindStepper(controlList.Find("Gamepad Vibration Row"),
                    new[] { "Aktif", "Nonaktif" },
                    () => GameSettingsManager.Current.gamepadVibration ? 0 : 1,
                    idx =>
                    {
                        bool active = (idx == 0);
                        GameSettingsManager.Current.gamepadVibration = active;
                        GameSettingsManager.Save();
                        if (active && Gamepad.current != null && gameObject.activeInHierarchy)
                        {
                            if (vibrationTestRoutine != null) StopCoroutine(vibrationTestRoutine);
                            vibrationTestRoutine = StartCoroutine(TestGamepadVibrationRoutine());
                        }
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
                    trackedQuestLabel = GetComponentAt<TMP_Text>(trackedRow, "Label");
                    Transform stepper = trackedRow.Find("Stepper");
                    if (stepper != null)
                    {
                        trackedQuestLeftBtn = GetComponentAt<Button>(stepper, "Left Arrow");
                        trackedQuestRightBtn = GetComponentAt<Button>(stepper, "Right Arrow");
                        trackedQuestValueText = GetComponentAt<TMP_Text>(stepper, "Value Text");
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

                // Exploration Hint & Tutorial Reminder (Disabled per specification)
                SetRowDisabled(gameplayList.Find("Exploration Hint Row"));
                SetRowDisabled(gameplayList.Find("Tutorial Reminder Row"));
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
                        GameSettingsManager.ApplyPreset((GraphicsPreset)idx);
                        RefreshAllControls();
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

            // 6. AUDIO (Disabled until audio assets are added)
            Transform audioList = contentPanel.Find("Audio Content/Audio Options List");
            if (audioList != null)
            {
                for (int i = 0; i < audioList.childCount; i++)
                {
                    SetRowDisabled(audioList.GetChild(i));
                }
            }

            // 7. BAHASA / SUBTITLE
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

            // 8. AKSESIBILITAS (Disabled per specification)
            Transform accessibilityList = contentPanel.Find("Accessibility Content/Accessibility Options List");
            if (accessibilityList != null)
            {
                for (int i = 0; i < accessibilityList.childCount; i++)
                {
                    SetRowDisabled(accessibilityList.GetChild(i));
                }
            }
        }

        private void SetRowDisabled(Transform rowTransform)
        {
            if (rowTransform == null) return;

            Transform optOutline = rowTransform.Find("Active Option Outline");
            if (optOutline != null) optOutline.gameObject.SetActive(false);

            Button[] buttons = rowTransform.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null) buttons[i].interactable = false;
            }

            Slider[] sliders = rowTransform.GetComponentsInChildren<Slider>(true);
            for (int i = 0; i < sliders.Length; i++)
            {
                if (sliders[i] != null) sliders[i].interactable = false;
            }

            TMP_Text[] texts = rowTransform.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null) texts[i].color = disabledTextColor;
            }

            Transform sliderCtrl = rowTransform.Find("Slider Control");
            if (sliderCtrl != null)
            {
                Image fillImg = GetComponentAt<Image>(sliderCtrl, "Fill Area/Fill");
                if (fillImg != null) fillImg.color = disabledTextColor;
                Image handleImg = GetComponentAt<Image>(sliderCtrl, "Handle Slide Area/Handle");
                if (handleImg != null) handleImg.color = disabledTextColor;
            }
        }

        private void WireOptionRowHover(Transform rowTransform)
        {
            if (rowTransform == null) return;
            Image hitGraphic = rowTransform.GetComponent<Image>();
            if (hitGraphic == null)
            {
                hitGraphic = rowTransform.gameObject.AddComponent<Image>();
                hitGraphic.color = new Color(0f, 0f, 0f, 0f);
            }
            hitGraphic.raycastTarget = true;

            EventTrigger trigger = rowTransform.GetComponent<EventTrigger>();
            if (trigger == null) trigger = rowTransform.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();
            var hover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            hover.callback.AddListener(_ =>
            {
                if (!locked) HighlightOptionRow(rowTransform);
            });
            trigger.triggers.Add(hover);
        }

        private void SelectDefaultOptionRowInActiveTab()
        {
            if (contentPanels == null || activeTabIndex < 0 || activeTabIndex >= contentPanels.Length) return;
            GameObject activePanel = contentPanels[activeTabIndex];
            if (activePanel == null) return;

            Transform optionsList = null;
            for (int i = 0; i < activePanel.transform.childCount; i++)
            {
                Transform child = activePanel.transform.GetChild(i);
                if (child.name.EndsWith("Options List", StringComparison.Ordinal))
                {
                    optionsList = child;
                    break;
                }
            }
            if (optionsList == null || optionsList.childCount == 0) return;

            // Find first enabled row
            for (int i = 0; i < optionsList.childCount; i++)
            {
                Transform row = optionsList.GetChild(i);
                Slider s = GetComponentAt<Slider>(row, "Slider Control");
                Button b = GetComponentAt<Button>(row, "Stepper/Left Arrow");
                if ((s != null && s.interactable) || (b != null && b.interactable))
                {
                    HighlightOptionRow(row);
                    return;
                }
            }
        }

        private void HighlightOptionRow(Transform rowTransform)
        {
            if (rowTransform == null || rowTransform.parent == null) return;
            currentHoveredOptionRow = rowTransform;

            // Find the Active Option Outline inside this options list
            Transform listParent = rowTransform.parent;
            RectTransform foundOutline = null;
            for (int i = 0; i < listParent.childCount; i++)
            {
                Transform r = listParent.GetChild(i);
                Transform candidate = r.Find("Active Option Outline");
                if (candidate != null)
                {
                    foundOutline = candidate as RectTransform;
                    break;
                }
            }

            if (foundOutline != null)
            {
                activeOptionOutline = foundOutline;
                activeOptionOutlineImage = foundOutline.GetComponent<Image>();
                foundOutline.SetParent(rowTransform, false);
                foundOutline.anchorMin = Vector2.zero;
                foundOutline.anchorMax = Vector2.one;
                foundOutline.offsetMin = Vector2.zero;
                foundOutline.offsetMax = Vector2.zero;
                foundOutline.localScale = Vector3.one;
                foundOutline.SetAsFirstSibling();
                foundOutline.gameObject.SetActive(true);
            }
        }

        private void BindStepper(Transform rowTransform, string[] options, Func<int> getter, Action<int> setter)
        {
            if (rowTransform == null || options == null || options.Length == 0) return;
            WireOptionRowHover(rowTransform);
            Transform stepper = rowTransform.Find("Stepper");
            if (stepper == null) return;

            TMP_Text valText = GetComponentAt<TMP_Text>(stepper, "Value Text");
            Button leftBtn = GetComponentAt<Button>(stepper, "Left Arrow");
            Button rightBtn = GetComponentAt<Button>(stepper, "Right Arrow");

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
                    HighlightOptionRow(rowTransform);
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
                    HighlightOptionRow(rowTransform);
                    int cur = getter != null ? getter() : 0;
                    cur = (cur + 1) % options.Length;
                    setter?.Invoke(cur);
                    RefreshText();
                });
            }

            RefreshText();
        }

        private static string FormatSliderValue(float v, bool wholeNumbers, string unit)
        {
            if (wholeNumbers)
            {
                return string.IsNullOrEmpty(unit)
                    ? Mathf.RoundToInt(v).ToString()
                    : $"{Mathf.RoundToInt(v)}{unit}";
            }
            return string.IsNullOrEmpty(unit)
                ? $"{v:0.0}"
                : $"{v:0.0}{unit}";
        }

        private void BindSlider(Transform rowTransform, float min, float max, Func<float> getter, Action<float> setter, string unit = "")
        {
            if (rowTransform == null) return;
            WireOptionRowHover(rowTransform);
            Transform sliderObj = rowTransform.Find("Slider Control");
            if (sliderObj == null) return;

            Slider slider = sliderObj.GetComponent<Slider>();
            TMP_Text valText = GetComponentAt<TMP_Text>(sliderObj, "Value Text");

            // Make track & fill clickable so clicking anywhere along the bar moves the slider
            Image trackImg = GetComponentAt<Image>(sliderObj, "Track Background");
            if (trackImg != null) trackImg.raycastTarget = true;
            Image fillImg = GetComponentAt<Image>(sliderObj, "Fill Area/Fill");
            if (fillImg != null) fillImg.raycastTarget = true;

            if (slider != null)
            {
                slider.onValueChanged.RemoveAllListeners();
                slider.minValue = min;
                slider.maxValue = max;
                float initialVal = getter != null ? getter() : min;
                slider.SetValueWithoutNotify(initialVal);

                bool isWhole = slider.wholeNumbers;
                if (valText != null)
                {
                    valText.text = FormatSliderValue(initialVal, isWhole, unit);
                }

                slider.onValueChanged.AddListener(v =>
                {
                    HighlightOptionRow(rowTransform);
                    if (valText != null)
                    {
                        valText.text = FormatSliderValue(v, isWhole, unit);
                    }
                    setter?.Invoke(v);
                });
            }
        }

        public void RefreshAllControls()
        {
            BindAllRows();
            UpdateTrackedQuestInteractability();
            SelectDefaultOptionRowInActiveTab();
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
