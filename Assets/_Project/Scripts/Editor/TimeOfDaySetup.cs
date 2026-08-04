using System.Text;
using SaksiTerakhir.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SaksiTerakhir.EditorTools
{
    public static class TimeOfDaySetup
    {
        private const string EnvironmentFolder = "Assets/_Project/Environment";
        private const string SkyTextureFolder = "Assets/_Project/Art/Textures/Sky";

        private static readonly (string zone, float elevation, float azimuth,
            Color sun, float intensity, Color sky, Color equator, Color ground,
            Color fog, float density, float skyExposure)[] Zones =
        {
            ("Pagi", 21.71f, 104.68f,
                new Color(1f, 0.957f, 0.882f), 1.15f,
                new Color(0.522f, 0.643f, 0.784f),
                new Color(0.631f, 0.671f, 0.706f),
                new Color(0.216f, 0.216f, 0.235f),
                new Color(0.729f, 0.784f, 0.839f), 0.0055f, 0.1f),
            ("Malam", 38.06f, 250.05f,
                new Color(0.647f, 0.749f, 1f), 0.25f,
                new Color(0.086f, 0.114f, 0.176f),
                new Color(0.055f, 0.067f, 0.098f),
                new Color(0.016f, 0.016f, 0.024f),
                new Color(0.047f, 0.067f, 0.11f), 0.008f, 2f),
        };

        [MenuItem("Saksi Terakhir/Create Time Of Day Presets")]
        public static void CreatePresets()
        {
            if (!AssetDatabase.IsValidFolder(EnvironmentFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Environment");
            }

            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("=== TIME OF DAY ===");

            foreach (var zone in Zones)
            {
                string texturePath = $"{SkyTextureFolder}/Sky_{zone.zone}.exr";
                var cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(texturePath);
                if (cubemap == null)
                {
                    texturePath = $"{SkyTextureFolder}/Sky_{zone.zone}.png";
                    cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(texturePath);
                }

                if (cubemap == null)
                {
                    report.AppendLine($"  {zone.zone,-8} sky texture missing in {SkyTextureFolder}");
                    continue;
                }

                string materialPath = $"{EnvironmentFolder}/Sky_{zone.zone}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Skybox/Cubemap"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }

                material.SetTexture("_Tex", cubemap);
                material.SetFloat("_Exposure", zone.skyExposure);
                EditorUtility.SetDirty(material);

                string presetPath = $"{EnvironmentFolder}/TimeOfDay_{zone.zone}.asset";
                var preset = AssetDatabase.LoadAssetAtPath<TimeOfDayPreset>(presetPath);
                var created = preset == null;
                if (created)
                {
                    preset = ScriptableObject.CreateInstance<TimeOfDayPreset>();
                    AssetDatabase.CreateAsset(preset, presetPath);
                }

                preset.skybox = material;
                preset.skyRotation = 0f;
                preset.skyExposure = zone.skyExposure;
                preset.sunElevation = zone.elevation;
                preset.sunAzimuth = zone.azimuth;
                preset.sunColor = zone.sun;
                preset.useColorTemperature = false;
                preset.colorTemperature = 6500f;
                preset.sunIntensity = zone.intensity;
                preset.shadowStrength = 0.85f;
                preset.ambientMode = AmbientMode.Trilight;
                preset.ambientIntensity = 1f;
                preset.ambientSky = zone.sky;
                preset.ambientEquator = zone.equator;
                preset.ambientGround = zone.ground;
                preset.fogEnabled = true;
                preset.fogMode = FogMode.ExponentialSquared;
                preset.fogColor = zone.fog;
                preset.fogDensity = zone.density;
                EditorUtility.SetDirty(preset);

                report.AppendLine($"  {zone.zone,-8} {(created ? "created" : "updated")}  "
                                  + $"sun {zone.elevation:F1} deg elev / {zone.azimuth:F1} deg azim  "
                                  + $"fog {zone.density:F4}");
                report.AppendLine($"           sky      {texturePath}");
                report.AppendLine($"           cubemap  {cubemap.width} px faces, {cubemap.format}, "
                                  + $"{cubemap.mipmapCount} mips, exposure {zone.skyExposure:F2}");
                report.AppendLine($"           material {materialPath}");
                report.AppendLine($"           preset   {presetPath}");
            }

            AssetDatabase.SaveAssets();
            report.AppendLine("=== END TIME OF DAY ===");
            Debug.Log(report.ToString());
        }
    }
}
