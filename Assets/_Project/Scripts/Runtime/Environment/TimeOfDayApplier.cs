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
                    material.SetFloat(ExposureId, selected.skyExposure);
                }
                if (material.HasProperty(TintId)) material.SetColor(TintId, selected.skyTint);
            }

            if (sun != null)
            {
                sun.transform.rotation = selected.SunRotation;
                sun.color = selected.sunColor;
                sun.useColorTemperature = selected.useColorTemperature;
                sun.colorTemperature = selected.colorTemperature;
                sun.intensity = selected.sunIntensity;
                sun.shadowStrength = selected.shadowStrength;
                RenderSettings.sun = sun;
            }

            RenderSettings.ambientMode = selected.ambientMode;
            RenderSettings.ambientIntensity = selected.ambientIntensity;
            RenderSettings.ambientSkyColor = selected.ambientSky;
            RenderSettings.ambientEquatorColor = selected.ambientEquator;
            RenderSettings.ambientGroundColor = selected.ambientGround;

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

        private void OnDisable() => ReleaseRuntimeSkybox();

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }
    }
}
