using System.Text;
using SaksiTerakhir.Environment;
using UnityEditor;
using UnityEngine;

namespace SaksiTerakhir.EditorTools
{
    public static class SkySunProbe
    {
        private const string PresetRoot = "Assets/_Project/Environment";

        private static readonly CubemapFace[] Faces =
        {
            CubemapFace.PositiveX, CubemapFace.NegativeX,
            CubemapFace.PositiveY, CubemapFace.NegativeY,
            CubemapFace.PositiveZ, CubemapFace.NegativeZ,
        };

        [MenuItem("Saksi Terakhir/Probe Sky Sun")]
        public static void ProbeSkySun()
        {
            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("=== SKY SUN PROBE ===");

            foreach (var guid in AssetDatabase.FindAssets("t:TimeOfDayPreset", new[] { PresetRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<TimeOfDayPreset>(path);
                if (preset == null || preset.skybox == null)
                {
                    report.AppendLine($"  {path} has no skybox material");
                    continue;
                }

                Measure(preset, report);
            }

            report.AppendLine("=== END SKY SUN PROBE ===");
            Debug.Log(report.ToString());
        }

        private static void Measure(TimeOfDayPreset preset, StringBuilder report)
        {
            report.AppendLine($"  {preset.name}  skybox {preset.skybox.name}  "
                              + $"skyRotation {preset.skyRotation:F2}");

            if (!(preset.skybox.GetTexture("_Tex") is Cubemap cubemap))
            {
                report.AppendLine("    _Tex is not a Cubemap - cannot probe");
                return;
            }

            var size = cubemap.width;
            var bestFace = CubemapFace.PositiveX;
            var bestValue = float.MinValue;
            var bestColumn = 0;
            var bestRow = 0;

            foreach (var face in Faces)
            {
                var pixels = cubemap.GetPixels(face);
                if (pixels == null || pixels.Length == 0)
                {
                    report.AppendLine($"    face {face} unreadable");
                    continue;
                }

                var faceBest = float.MinValue;
                var faceIndex = 0;
                for (var index = 0; index < pixels.Length; index++)
                {
                    var pixel = pixels[index];
                    var luminance = pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f;
                    if (luminance > faceBest)
                    {
                        faceBest = luminance;
                        faceIndex = index;
                    }
                }

                report.AppendLine($"    face {face,-9} peak {faceBest,12:F1}"
                                  + $"  at column {faceIndex % size}, row {faceIndex / size}");

                if (faceBest <= bestValue)
                {
                    continue;
                }

                bestValue = faceBest;
                bestFace = face;
                bestColumn = faceIndex % size;
                bestRow = faceIndex / size;
            }

            if (bestValue <= float.MinValue)
            {
                report.AppendLine("    no readable face");
                return;
            }

            var u = (bestColumn + 0.5f) / size;
            var v = (bestRow + 0.5f) / size;
            var direction = FaceDirection(bestFace, u, v);
            var flipped = FaceDirection(bestFace, u, 1f - v);

            Describe("sky sun (v as read)", direction, report);
            Describe("sky sun (v flipped)", flipped, report);

            var presetDirection = -(preset.SunRotation * Vector3.forward);
            Describe("preset light", presetDirection, report);
            report.AppendLine($"    preset fields  sunAzimuth {preset.sunAzimuth:F2}  "
                              + $"sunElevation {preset.sunElevation:F2}");
            report.AppendLine($"    mismatch vs v-as-read {Vector3.Angle(direction, presetDirection):F2} deg"
                              + $", vs v-flipped {Vector3.Angle(flipped, presetDirection):F2} deg");
        }

        private static void Describe(string label, Vector3 direction, StringBuilder report)
        {
            var azimuth = Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 360f);
            var elevation = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            report.AppendLine($"    {label,-20} azimuth {azimuth,7:F2}  elevation {elevation,7:F2}"
                              + $"  dir ({direction.x:F3}, {direction.y:F3}, {direction.z:F3})");
        }

        private static Vector3 FaceDirection(CubemapFace face, float u, float v)
        {
            var s = u * 2f - 1f;
            var t = v * 2f - 1f;
            switch (face)
            {
                case CubemapFace.PositiveX:
                    return new Vector3(1f, -t, -s).normalized;
                case CubemapFace.NegativeX:
                    return new Vector3(-1f, -t, s).normalized;
                case CubemapFace.PositiveY:
                    return new Vector3(s, 1f, t).normalized;
                case CubemapFace.NegativeY:
                    return new Vector3(s, -1f, -t).normalized;
                case CubemapFace.PositiveZ:
                    return new Vector3(s, -t, 1f).normalized;
                default:
                    return new Vector3(-s, -t, -1f).normalized;
            }
        }
    }
}
