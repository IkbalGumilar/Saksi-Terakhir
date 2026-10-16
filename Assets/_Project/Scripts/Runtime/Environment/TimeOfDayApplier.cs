using UnityEngine;
using UnityEngine.Rendering;

namespace SaksiTerakhir.Environment
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class TimeOfDayApplier : MonoBehaviour
    {
        private static readonly int RotationId = Shader.PropertyToID("_Rotation");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        [SerializeField] private TimeOfDayPreset preset;
        [SerializeField] private TimeOfDayPreset initialMenuPreset;
        [SerializeField] private TimeOfDayPreset newGamePreset;
        [SerializeField] private Light sun;
        private Material runtimeSkybox;
        private Material sourceSkybox;

        public TimeOfDayPreset ActivePreset { get; private set; }

        public TimeOfDayPreset Preset
        {
            get => preset;
            set
            {
                preset = value;
                Apply();
            }
        }

        public void ApplyInitialMenu()
        {
            if (initialMenuPreset != null) ApplyPreset(initialMenuPreset);
        }

        public void ApplyNewGame()
        {
            if (newGamePreset != null) Preset = newGamePreset;
            else Apply();
        }

        public void Apply() => ApplyPreset(preset);

        private void ApplyPreset(TimeOfDayPreset selected)
        {
            if (selected == null)
            {
                return;
            }

            ActivePreset = selected;
            float brightnessFactor = 1f;
            if (Application.isPlaying && SaksiTerakhir.Settings.GameSettingsManager.Current != null)
            {
                brightnessFactor = Mathf.Lerp(0.5f, 1.5f,
                    Mathf.Clamp(SaksiTerakhir.Settings.GameSettingsManager.Current.brightness, 0f, 100f) / 100f);
            }

            if (selected.skybox != null)
            {
                Material material = GetSkyboxMaterial(selected.skybox);
                RenderSettings.skybox = material;
                if (material.HasProperty(RotationId))
                {
                    material.SetFloat(RotationId, selected.skyRotation);
                }

                if (material.HasProperty(ExposureId))
                {
                    material.SetFloat(ExposureId, selected.skyExposure * brightnessFactor);
                }
                if (material.HasProperty(TintId)) material.SetColor(TintId, selected.skyTint);
            }

            if (sun != null)
            {
                sun.transform.rotation = selected.SunRotation;
                sun.color = selected.sunColor;
                sun.useColorTemperature = selected.useColorTemperature;
                sun.colorTemperature = selected.colorTemperature;
                sun.intensity = selected.sunIntensity * brightnessFactor;
                sun.shadowStrength = selected.shadowStrength;
                if (Application.isPlaying && SaksiTerakhir.Settings.GameSettingsManager.Current != null)
                {
                    switch (SaksiTerakhir.Settings.GameSettingsManager.Current.shadowQuality)
                    {
                        case SaksiTerakhir.Settings.ShadowQualitySetting.Off:
                            sun.shadows = LightShadows.None;
                            break;
                        case SaksiTerakhir.Settings.ShadowQualitySetting.Low:
                            sun.shadows = LightShadows.Hard;
                            break;
                        case SaksiTerakhir.Settings.ShadowQualitySetting.High:
                            sun.shadows = LightShadows.Soft;
                            break;
                    }
                }
                RenderSettings.sun = sun;
            }

            RenderSettings.ambientMode = selected.ambientMode;
            RenderSettings.ambientIntensity = selected.ambientIntensity * brightnessFactor;
            RenderSettings.ambientSkyColor = selected.ambientSky * brightnessFactor;
            RenderSettings.ambientEquatorColor = selected.ambientEquator * brightnessFactor;
            RenderSettings.ambientGroundColor = selected.ambientGround * brightnessFactor;

            RenderSettings.fog = selected.fogEnabled;
            RenderSettings.fogMode = selected.fogMode;
            RenderSettings.fogColor = selected.fogColor;
            RenderSettings.fogDensity = selected.fogDensity;

            DynamicGI.UpdateEnvironment();
        }

        private Material GetSkyboxMaterial(Material source)
        {
            if (!Application.isPlaying) return source;
            if (runtimeSkybox != null && sourceSkybox == source) return runtimeSkybox;

            ReleaseRuntimeSkybox();
            sourceSkybox = source;
            // Menu previews must not change the material shared by other presets/scenes.
            runtimeSkybox = new Material(source) { hideFlags = HideFlags.DontSave };
            return runtimeSkybox;
        }

        private void ReleaseRuntimeSkybox()
        {
            if (runtimeSkybox == null) return;
            if (RenderSettings.skybox == runtimeSkybox) RenderSettings.skybox = sourceSkybox;
            if (Application.isPlaying) Destroy(runtimeSkybox);
            else DestroyImmediate(runtimeSkybox);
            runtimeSkybox = null;
            sourceSkybox = null;
        }

        private void OnDisable()
        {
            SaksiTerakhir.Settings.GameSettingsManager.SettingsChanged -= OnSettingsChanged;
            ReleaseRuntimeSkybox();
        }

        private void OnEnable()
        {
            SaksiTerakhir.Settings.GameSettingsManager.SettingsChanged += OnSettingsChanged;
            Apply();
        }

        private void OnSettingsChanged(SaksiTerakhir.Settings.GameSettingsData _)
        {
            if (ActivePreset != null) ApplyPreset(ActivePreset);
            else Apply();
        }

        private void OnValidate()
        {
            Apply();
        }
    }
}
