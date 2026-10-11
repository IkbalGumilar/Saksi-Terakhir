using System.Text;
using SaksiTerakhir.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    public static class TimeOfDaySetup
    {
        private const string EnvironmentFolder = "Assets/_Project/Environment";
        private const string SkyTextureFolder = "Assets/_Project/Art/Textures/Sky";

        private static readonly string[] PresetNames =
            { "DiniHari", "Pagi", "Siang_11", "Siang_13", "Sore", "Malam", "TengahMalam" };

        [MenuItem("Saksi Terakhir/Create Time Of Day Presets")]
        public static void CreatePresets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Shader shader = Shader.Find("Skybox/Cubemap");
            if (shader == null) throw new System.InvalidOperationException("Skybox/Cubemap shader is unavailable.");

            if (!AssetDatabase.IsValidFolder(EnvironmentFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Environment");
            }

            var report = new StringBuilder();
            report.AppendLine("Time of day presets (existing lighting values preserved):");

            foreach (string name in PresetNames)
            {
                string source = name == "DiniHari" || name == "Malam" || name == "TengahMalam"
                    ? "Malam" : "Pagi";
                string texturePath = $"{SkyTextureFolder}/Sky_{source}.exr";
                var cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(texturePath);
                if (cubemap == null)
                {
                    texturePath = $"{SkyTextureFolder}/Sky_{source}.png";
                    cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(texturePath);
                }

                if (cubemap == null)
                {
                    throw new System.InvalidOperationException($"Sky texture missing: {source}");
                }

                string presetPath = $"{EnvironmentFolder}/TimeOfDay_{name}.asset";
                var preset = AssetDatabase.LoadAssetAtPath<TimeOfDayPreset>(presetPath);
                bool created = preset == null;
                if (created)
                {
                    preset = ScriptableObject.CreateInstance<TimeOfDayPreset>();
                    ConfigureDefaults(preset, name);
                    AssetDatabase.CreateAsset(preset, presetPath);
                }
                // These two assets predate the hour field; keep their calibrated lighting.
                else if (name == "Pagi") preset.hourOfDay = 7f;
                else if (name == "Malam") preset.hourOfDay = 20f;

                string materialPath = $"{EnvironmentFolder}/Sky_{name}.mat";
                Material material = preset.skybox != null ? preset.skybox
                    : AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, materialPath);
                }

                // A replacement sky supplied later by the artist remains attached on reruns.
                if (material.GetTexture("_Tex") == null) material.SetTexture("_Tex", cubemap);
                material.SetFloat("_Exposure", preset.skyExposure);
                material.SetFloat("_Rotation", preset.skyRotation);
                material.SetColor("_Tint", preset.skyTint);
                preset.skybox = material;
                EditorUtility.SetDirty(material);
                EditorUtility.SetDirty(preset);
                AssetDatabase.SaveAssetIfDirty(material);
                AssetDatabase.SaveAssetIfDirty(preset);
                report.AppendLine($"{name}: {preset.hourOfDay:00}:00 ({(created ? "created" : "preserved")})");
            }

            Debug.Log(report.ToString());
        }

        [MenuItem("Saksi Terakhir/Time Of Day/Configure Active Scene")]
        public static void ConfigureActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var morning = AssetDatabase.LoadAssetAtPath<TimeOfDayPreset>($"{EnvironmentFolder}/TimeOfDay_Pagi.asset");
            var dawn = AssetDatabase.LoadAssetAtPath<TimeOfDayPreset>($"{EnvironmentFolder}/TimeOfDay_DiniHari.asset");
            if (morning == null || dawn == null)
                throw new System.InvalidOperationException("Create the time presets first.");

            Scene scene = SceneManager.GetActiveScene();
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TimeOfDayApplier applier in root.GetComponentsInChildren<TimeOfDayApplier>(true))
                {
                    Undo.RecordObject(applier, "Configure initial menu time");
                    var serialized = new SerializedObject(applier);
                    serialized.FindProperty("initialMenuPreset").objectReferenceValue = dawn;
                    serialized.FindProperty("newGamePreset").objectReferenceValue = morning;
                    SerializedProperty gameplay = serialized.FindProperty("preset");
                    if (gameplay.objectReferenceValue == null) gameplay.objectReferenceValue = morning;
                    serialized.ApplyModifiedProperties();
                    count++;
                }
            }
            if (count > 0) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Initial menu time configured on {count} applier(s) in {scene.name}. Save the scene to persist the reference.");
        }

        private static void ConfigureDefaults(TimeOfDayPreset preset, string name)
        {
            preset.hourOfDay = 7f;
            preset.skyExposure = 0.1f;
            preset.sunElevation = 21.71f;
            preset.sunAzimuth = 104.68f;
            preset.sunColor = new Color(1f, 0.957f, 0.882f);
            preset.sunIntensity = 1.15f;
            preset.shadowStrength = 0.85f;
            preset.ambientMode = AmbientMode.Trilight;
            preset.ambientIntensity = 1f;
            SetAmbient(preset, new Color(0.522f, 0.643f, 0.784f),
                new Color(0.631f, 0.671f, 0.706f), new Color(0.216f, 0.216f, 0.235f));
            preset.fogEnabled = true;
            preset.fogMode = FogMode.ExponentialSquared;
            preset.fogColor = new Color(0.729f, 0.784f, 0.839f);
            preset.fogDensity = 0.0055f;

            switch (name)
            {
                case "DiniHari":
                    preset.hourOfDay = 5f;
                    preset.skyExposure = 0.7f;
                    preset.skyTint = new Color(0.47f, 0.53f, 0.68f);
                    preset.sunElevation = -6f;
                    preset.sunAzimuth = 90f;
                    preset.sunIntensity = 0f;
                    SetAmbient(preset, new Color(0.12f, 0.16f, 0.25f),
                        new Color(0.1f, 0.12f, 0.17f), new Color(0.025f, 0.03f, 0.045f));
                    preset.fogColor = new Color(0.19f, 0.23f, 0.32f);
                    preset.fogDensity = 0.007f;
                    break;
                case "Siang_11":
                    preset.hourOfDay = 11f;
                    preset.skyExposure = 0.13f;
                    preset.skyRotation = 40.32f;
                    preset.sunElevation = 68f;
                    preset.sunAzimuth = 145f;
                    preset.sunColor = new Color(1f, 0.985f, 0.95f);
                    preset.sunIntensity = 1.45f;
                    SetAmbient(preset, new Color(0.56f, 0.7f, 0.88f),
                        new Color(0.68f, 0.72f, 0.77f), new Color(0.25f, 0.25f, 0.26f));
                    preset.fogColor = new Color(0.75f, 0.82f, 0.9f);
                    preset.fogDensity = 0.0035f;
                    break;
                case "Siang_13":
                    preset.hourOfDay = 13f;
                    preset.skyExposure = 0.14f;
                    preset.skyRotation = 110.32f;
                    preset.sunElevation = 70f;
                    preset.sunAzimuth = 215f;
                    preset.sunColor = new Color(1f, 0.97f, 0.9f);
                    preset.sunIntensity = 1.55f;
                    SetAmbient(preset, new Color(0.59f, 0.7f, 0.84f),
                        new Color(0.71f, 0.72f, 0.73f), new Color(0.26f, 0.25f, 0.24f));
                    preset.fogColor = new Color(0.78f, 0.82f, 0.87f);
                    preset.fogDensity = 0.004f;
                    break;
                case "Sore":
                    preset.hourOfDay = 17f;
                    preset.skyExposure = 0.075f;
                    preset.skyRotation = 160.32f;
                    preset.skyTint = new Color(0.68f, 0.48f, 0.35f);
                    preset.sunAzimuth = 265f;
                    preset.sunColor = new Color(1f, 0.7f, 0.43f);
                    preset.sunIntensity = 0.85f;
                    SetAmbient(preset, new Color(0.36f, 0.38f, 0.54f),
                        new Color(0.53f, 0.36f, 0.27f), new Color(0.14f, 0.12f, 0.12f));
                    preset.fogColor = new Color(0.67f, 0.48f, 0.39f);
                    preset.fogDensity = 0.006f;
                    break;
                case "Malam":
                case "TengahMalam":
                    bool midnight = name == "TengahMalam";
                    preset.hourOfDay = midnight ? 0f : 20f;
                    preset.skyExposure = midnight ? 0.8f : 2f;
                    preset.skyRotation = midnight ? 70f : 0f;
                    preset.skyTint = midnight ? new Color(0.42f, 0.47f, 0.58f) : preset.skyTint;
                    preset.sunElevation = 38.06f;
                    preset.sunAzimuth = midnight ? 320.05f : 250.05f;
                    preset.sunColor = new Color(0.647f, 0.749f, 1f);
                    preset.sunIntensity = midnight ? 0.06f : 0.25f;
                    SetAmbient(preset,
                        midnight ? new Color(0.035f, 0.05f, 0.09f) : new Color(0.086f, 0.114f, 0.176f),
                        midnight ? new Color(0.025f, 0.035f, 0.055f) : new Color(0.055f, 0.067f, 0.098f),
                        midnight ? new Color(0.008f, 0.01f, 0.018f) : new Color(0.016f, 0.016f, 0.024f));
                    preset.fogColor = midnight ? new Color(0.025f, 0.035f, 0.065f) : new Color(0.047f, 0.067f, 0.11f);
                    preset.fogDensity = midnight ? 0.009f : 0.008f;
                    break;
            }
        }

        private static void SetAmbient(TimeOfDayPreset preset, Color sky, Color equator, Color ground)
        {
            preset.ambientSky = sky;
            preset.ambientEquator = equator;
            preset.ambientGround = ground;
        }
    }
}
