using UnityEngine;
using UnityEngine.Rendering;

namespace SaksiTerakhir.Environment
{
    [CreateAssetMenu(fileName = "TimeOfDay", menuName = "Saksi Terakhir/Time Of Day Preset")]
    public sealed class TimeOfDayPreset : ScriptableObject
    {
        [Header("Sky")]
        public Material skybox;
        [Range(0f, 360f)] public float skyRotation;
        [Range(0f, 8f)] public float skyExposure = 1f;

        [Header("Sun")]
        [Range(-15f, 90f)] public float sunElevation = 22f;
        [Range(0f, 360f)] public float sunAzimuth = 105f;
        [ColorUsage(false, true)] public Color sunColor = Color.white;
        public bool useColorTemperature;
        [Range(1500f, 20000f)] public float colorTemperature = 6500f;
        [Range(0f, 4f)] public float sunIntensity = 1.1f;
        [Range(0f, 1f)] public float shadowStrength = 0.85f;

        [Header("Ambient")]
        public AmbientMode ambientMode = AmbientMode.Trilight;
        [Range(0f, 3f)] public float ambientIntensity = 1f;
        [ColorUsage(false, true)] public Color ambientSky = Color.white;
        [ColorUsage(false, true)] public Color ambientEquator = Color.grey;
        [ColorUsage(false, true)] public Color ambientGround = Color.black;

        [Header("Fog")]
        public bool fogEnabled = true;
        public FogMode fogMode = FogMode.ExponentialSquared;
        public Color fogColor = Color.grey;
        [Range(0f, 0.2f)] public float fogDensity = 0.006f;

        public Quaternion SunRotation => Quaternion.Euler(sunElevation, sunAzimuth + 180f, 0f);
    }
}
