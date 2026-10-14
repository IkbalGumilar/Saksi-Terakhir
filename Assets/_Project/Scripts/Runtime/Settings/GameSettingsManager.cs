using System;
using System.Reflection;
using UnityEngine;
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

        // Cached reflection for URP properties to avoid assembly coupling
        private static PropertyInfo urpRenderScaleProp;
        private static PropertyInfo urpShadowDistanceProp;
        private static PropertyInfo urpMainLightShadowsProp;
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

            isInitialized = true;
        }

        public static void Save()
        {
            if (current == null) return;
            string json = JsonUtility.ToJson(current);
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
            ApplyAll();
            SettingsChanged?.Invoke(current);
        }

        public static void ResetToDefaults()
        {
            Current.ResetToDefaults();
            Save();
        }

        public static void ApplyAll()
        {
            if (current == null) return;

            ApplyDisplaySettings();
            ApplyGraphicsSettings();
            ApplyTargetFrameRate();
            SettingsChanged?.Invoke(current);
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

            // URP specific properties via cached reflection
            EnsureUrpReflection();
            RenderPipelineAsset currentPipeline = GraphicsSettings.currentRenderPipeline;
            if (currentPipeline != null)
            {
                // Render scale
                if (urpRenderScaleProp != null)
                {
                    float scale = current.renderScalePercent / 100f;
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

                // Main light shadows
                if (urpMainLightShadowsProp != null)
                {
                    bool enableShadows = current.shadowQuality != ShadowQualitySetting.Off;
                    urpMainLightShadowsProp.SetValue(currentPipeline, enableShadows);
                }
            }
        }

        private static void EnsureUrpReflection()
        {
            if (reflectionLookedUp) return;
            reflectionLookedUp = true;

            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return;

            Type pipelineType = pipeline.GetType();
            urpRenderScaleProp = pipelineType.GetProperty("renderScale", BindingFlags.Instance | BindingFlags.Public);
            urpShadowDistanceProp = pipelineType.GetProperty("shadowDistance", BindingFlags.Instance | BindingFlags.Public);
            urpMainLightShadowsProp = pipelineType.GetProperty("mainLightShadowsSupported", BindingFlags.Instance | BindingFlags.Public);
        }
    }
}
