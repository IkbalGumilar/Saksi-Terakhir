using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace SaksiTerakhir.Settings
{
    /// <summary>
    /// Central manager for loading, saving, and applying game settings.
    /// Fully optimized, zero-alloc in hot paths.
    /// </summary>
    public static class GameSettingsManager
    {
        private const string PrefsKey = "SaksiTerakhir.GameSettings.v1";

        public static readonly (int width, int height)[] StandardResolutions =
        {
            (1920, 1080),
            (1600, 900),
            (1366, 768),
            (1280, 720)
        };

        private static GameSettingsData current;
        private static bool isInitialized;

        // Cached reflection for URP properties/fields to avoid assembly coupling
        private static PropertyInfo urpRenderScaleProp;
        private static PropertyInfo urpShadowDistanceProp;
        private static FieldInfo urpMainLightShadowsField;
        private static FieldInfo urpSoftShadowsField;
        private static FieldInfo urpShadowResolutionField;
        private static Type urpCameraDataType;
        private static PropertyInfo urpCameraAntialiasingProp;
        private static bool reflectionLookedUp;

        public static event Action<GameSettingsData> SettingsChanged;

        public static GameSettingsData Current
        {
            get
            {
                if (!isInitialized || current == null)
                {
                    Load();
                }
                return current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (isInitialized && current != null) return;
            Load();
            ApplyAll();
        }

        public static void Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    current = JsonUtility.FromJson<GameSettingsData>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GameSettingsManager] Failed to parse saved settings: {ex.Message}. Using defaults.");
                    current = null;
                }
            }

            if (current == null)
            {
                current = new GameSettingsData();
            }

            // Auto-detect input device on startup if gamepad is not connected
            if (Gamepad.current == null && current.inputDevice == "Gamepad")
            {
                current.inputDevice = "Keyboard & Mouse";
            }

            if (PlayerPrefs.HasKey("Localization.Language") &&
                Enum.TryParse(PlayerPrefs.GetString("Localization.Language"), out SaksiTerakhir.Localization.GameLanguage savedLang))
            {
                current.language = savedLang;
            }

            isInitialized = true;
            SaksiTerakhir.Localization.LocalizationManager.SetLanguage(current.language);
        }

        public static void Save()
        {
            if (current == null) return;
            string json = JsonUtility.ToJson(current);
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
            ApplyAll();
        }

        public static void ResetToDefaults()
        {
            Current.ResetToDefaults();
            if (Gamepad.current == null)
            {
                Current.inputDevice = "Keyboard & Mouse";
            }
            Save();
        }

        public static void ApplyPreset(GraphicsPreset preset)
        {
            GameSettingsData data = Current;
            data.qualityPreset = preset;
            switch (preset)
            {
                case GraphicsPreset.Low:
                    data.shadowQuality = ShadowQualitySetting.Off;
                    data.shadowDistance = ShadowDistanceSetting.Near15m;
                    data.textureQuality = TextureQualitySetting.Low;
                    data.antiAliasing = AntiAliasingSetting.Off;
                    data.renderScalePercent = 70;
                    break;
                case GraphicsPreset.Medium:
                    data.shadowQuality = ShadowQualitySetting.Low;
                    data.shadowDistance = ShadowDistanceSetting.Medium25m;
                    data.textureQuality = TextureQualitySetting.Medium;
                    data.antiAliasing = AntiAliasingSetting.FXAA;
                    data.renderScalePercent = 85;
                    break;
                case GraphicsPreset.High:
                    data.shadowQuality = ShadowQualitySetting.High;
                    data.shadowDistance = ShadowDistanceSetting.Far40m;
                    data.textureQuality = TextureQualitySetting.High;
                    data.antiAliasing = AntiAliasingSetting.SMAA;
                    data.renderScalePercent = 100;
                    break;
            }
            Save();
        }

        public static void ApplyAll()
        {
            if (current == null) return;

            SaksiTerakhir.Localization.LocalizationManager.SetLanguage(current.language);
            ApplyControlSettings();
            ApplyDisplaySettings();
            ApplyGraphicsSettings();
            ApplyTargetFrameRate();
            ApplyAudioSettings();
            SettingsChanged?.Invoke(current);
        }

        private static void ApplyAudioSettings()
        {
            if (current != null)
            {
                AudioListener.volume = Mathf.Clamp01(current.masterVolume / 100f);
            }
        }

        private static void ApplyControlSettings()
        {
            if (!current.gamepadVibration && Gamepad.current != null)
            {
                Gamepad.current.ResetHaptics();
            }
        }

        public static void TriggerGamepadVibration(float lowFrequency = 0.25f, float highFrequency = 0.25f)
        {
            if (Current == null || !Current.gamepadVibration) return;
            if (Gamepad.current == null) return;
            Gamepad.current.SetMotorSpeeds(lowFrequency, highFrequency);
        }

        public static void StopGamepadVibration()
        {
            if (Gamepad.current != null)
            {
                Gamepad.current.ResetHaptics();
            }
        }

        private static void ApplyDisplaySettings()
        {
            // VSync
            QualitySettings.vSyncCount = current.vSync ? 1 : 0;

            // Screen Mode & Resolution
            int resIndex = Mathf.Clamp(current.resolutionIndex, 0, StandardResolutions.Length - 1);
            var res = StandardResolutions[resIndex];

            FullScreenMode mode = current.screenMode == ScreenModeSetting.Fullscreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;

            if (Screen.width != res.width || Screen.height != res.height || Screen.fullScreenMode != mode)
            {
                Screen.SetResolution(res.width, res.height, mode);
            }
        }

        private static void ApplyTargetFrameRate()
        {
            Application.targetFrameRate = current.targetFrameRate;
        }

        private static void ApplyGraphicsSettings()
        {
            // Texture Quality (Mipmap limit: 0 = full res, 1 = half, 2 = quarter)
            int mipmapLimit = 0;
            switch (current.textureQuality)
            {
                case TextureQualitySetting.Low: mipmapLimit = 2; break;
                case TextureQualitySetting.Medium: mipmapLimit = 1; break;
                case TextureQualitySetting.High: mipmapLimit = 0; break;
            }
            QualitySettings.globalTextureMipmapLimit = mipmapLimit;

            // Apply shadow mode on main directional light
            Light sun = RenderSettings.sun;
            if (sun != null)
            {
                switch (current.shadowQuality)
                {
                    case ShadowQualitySetting.Off:
                        sun.shadows = LightShadows.None;
                        break;
                    case ShadowQualitySetting.Low:
                        sun.shadows = LightShadows.Hard;
                        break;
                    case ShadowQualitySetting.High:
                        sun.shadows = LightShadows.Soft;
                        break;
                }
            }

            // URP specific properties via cached reflection
            EnsureUrpReflection();
            RenderPipelineAsset currentPipeline = GraphicsSettings.currentRenderPipeline;
            if (currentPipeline != null)
            {
                // Render scale
                if (urpRenderScaleProp != null)
                {
                    float scale = Mathf.Clamp(current.renderScalePercent, 50, 100) / 100f;
                    urpRenderScaleProp.SetValue(currentPipeline, scale);
                }

                // Shadow Distance
                if (urpShadowDistanceProp != null)
                {
                    float shadowDist = 25f;
                    if (current.shadowQuality == ShadowQualitySetting.Off)
                    {
                        shadowDist = 0f;
                    }
                    else
                    {
                        switch (current.shadowDistance)
                        {
                            case ShadowDistanceSetting.Near15m: shadowDist = 15f; break;
                            case ShadowDistanceSetting.Medium25m: shadowDist = 25f; break;
                            case ShadowDistanceSetting.Far40m: shadowDist = 40f; break;
                        }
                    }
                    urpShadowDistanceProp.SetValue(currentPipeline, shadowDist);
                }

                // Main light shadows & soft shadows on URP asset
                bool enableShadows = current.shadowQuality != ShadowQualitySetting.Off;
                if (urpMainLightShadowsField != null)
                {
                    urpMainLightShadowsField.SetValue(currentPipeline, enableShadows);
                }
                if (urpSoftShadowsField != null)
                {
                    urpSoftShadowsField.SetValue(currentPipeline, current.shadowQuality == ShadowQualitySetting.High);
                }
                if (urpShadowResolutionField != null && enableShadows)
                {
                    int res = current.shadowQuality == ShadowQualitySetting.High ? 2048 : 1024;
                    urpShadowResolutionField.SetValue(currentPipeline, res);
                }
            }

            // Apply Post-Process Anti-Aliasing (Off = 0, FXAA = 1, SMAA = 2) to all active cameras
            if (urpCameraDataType != null && urpCameraAntialiasingProp != null)
            {
                int aaMode = Mathf.Clamp((int)current.antiAliasing, 0, 2);
                Camera[] cameras = Camera.allCameras;
                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera cam = cameras[i];
                    if (cam == null) continue;
                    Component camData = cam.GetComponent(urpCameraDataType);
                    if (camData != null)
                    {
                        object enumVal = Enum.ToObject(urpCameraAntialiasingProp.PropertyType, aaMode);
                        urpCameraAntialiasingProp.SetValue(camData, enumVal);
                    }
                }
            }
        }

        private static void EnsureUrpReflection()
        {
            if (reflectionLookedUp) return;
            reflectionLookedUp = true;

            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null)
            {
                Type pipelineType = pipeline.GetType();
                urpRenderScaleProp = pipelineType.GetProperty("renderScale", BindingFlags.Instance | BindingFlags.Public);
                urpShadowDistanceProp = pipelineType.GetProperty("shadowDistance", BindingFlags.Instance | BindingFlags.Public);
                urpMainLightShadowsField = pipelineType.GetField("m_MainLightShadowsSupported", BindingFlags.Instance | BindingFlags.NonPublic);
                urpSoftShadowsField = pipelineType.GetField("m_SoftShadowsSupported", BindingFlags.Instance | BindingFlags.NonPublic);
                urpShadowResolutionField = pipelineType.GetField("m_MainLightShadowmapResolution", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            urpCameraDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpCameraDataType != null)
            {
                urpCameraAntialiasingProp = urpCameraDataType.GetProperty("antialiasing", BindingFlags.Instance | BindingFlags.Public);
            }
        }
    }
}
