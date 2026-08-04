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

        [SerializeField] private TimeOfDayPreset preset;
        [SerializeField] private Light sun;

        public TimeOfDayPreset Preset
        {
            get => preset;
            set
            {
                preset = value;
                Apply();
            }
        }

        public void Apply()
        {
            if (preset == null)
            {
                return;
            }

            if (preset.skybox != null)
            {
                RenderSettings.skybox = preset.skybox;
                if (preset.skybox.HasProperty(RotationId))
                {
                    preset.skybox.SetFloat(RotationId, preset.skyRotation);
                }

                if (preset.skybox.HasProperty(ExposureId))
                {
                    preset.skybox.SetFloat(ExposureId, preset.skyExposure);
                }
            }

            if (sun != null)
            {
                sun.transform.rotation = preset.SunRotation;
                sun.color = preset.sunColor;
                sun.useColorTemperature = preset.useColorTemperature;
                sun.colorTemperature = preset.colorTemperature;
                sun.intensity = preset.sunIntensity;
                sun.shadowStrength = preset.shadowStrength;
                RenderSettings.sun = sun;
            }

            RenderSettings.ambientMode = preset.ambientMode;
            RenderSettings.ambientIntensity = preset.ambientIntensity;
            RenderSettings.ambientSkyColor = preset.ambientSky;
            RenderSettings.ambientEquatorColor = preset.ambientEquator;
            RenderSettings.ambientGroundColor = preset.ambientGround;

            RenderSettings.fog = preset.fogEnabled;
            RenderSettings.fogMode = preset.fogMode;
            RenderSettings.fogColor = preset.fogColor;
            RenderSettings.fogDensity = preset.fogDensity;

            DynamicGI.UpdateEnvironment();
        }

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
