using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SaksiTerakhir.EditorTools
{
    public static class VehiclePrefabSetup
    {
        private const string ModelRoot = "Assets/_Project/Art/Models";
        private const string PrefabRoot = "Assets/_Project/Prefabs";
        private const string CollisionChild = "Collision";
        private const int CollisionBands = 10;
        private const int CollisionSlices = 8;
        private const float CollisionMargin = 0.002f;
        private const int ProbeRays = 24;
        private const float ProbeRadius = 6f;

        private static readonly float[] ProbeHeights =
            { 0.35f, 0.60f, 0.80f, 0.95f, 1.25f, 1.65f };

        private static readonly string[] Models =
        {
            "SUV_Fleet_Black",
            "SUV_Fleet_Silver",
            "SUV_Fleet_Grey",
        };

        private static readonly HashSet<string> HullParts = new HashSet<string>
        {
            "SUV_Fleet_Body",
            "SUV_Fleet_Wheels",
            "SUV_Fleet_Bonnet",
            "SUV_Fleet_Tailgate",
            "SUV_Fleet_Door_FR",
            "SUV_Fleet_Door_FL",
            "SUV_Fleet_Door_RR",
            "SUV_Fleet_Door_RL",
        };

        private static readonly HashSet<string> MovableParts = new HashSet<string>
        {
            "SUV_Fleet_Bonnet",
            "SUV_Fleet_Tailgate",
            "SUV_Fleet_Door_FR",
            "SUV_Fleet_Door_FL",
            "SUV_Fleet_Door_RR",
            "SUV_Fleet_Door_RL",
            "SUV_Fleet_Glass_FR",
            "SUV_Fleet_Glass_FL",
            "SUV_Fleet_Glass_RR",
            "SUV_Fleet_Glass_RL",
        };

        private const string MaterialRoot = "Assets/_Project/Art/Models/Materials";

        private static readonly (string name, float metallic)[] MetallicFinish =
        {
            ("Vehicle_Rim", 0.85f),
            ("Vehicle_Mirror", 1.00f),
            ("Vehicle_Paint_Black", 0.10f),
            ("Vehicle_Paint_Silver", 0.20f),
            ("Vehicle_Paint_Grey", 0.15f),
        };

        private static readonly (string name, float alpha)[] Glazing =
        {
            ("Vehicle_Glass", 0.30f),
        };

        private const StaticEditorFlags StaticFlags =
            StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic
            | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic
            | StaticEditorFlags.NavigationStatic | StaticEditorFlags.ReflectionProbeStatic;

        [MenuItem("Saksi Terakhir/Create Vehicle Prefabs")]
        public static void CreateVehiclePrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabRoot))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            }

            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("=== VEHICLE PREFABS ===");

            ConfigureMaterials(report);

            foreach (var modelName in Models)
            {
                BuildPrefab(modelName, report);
            }

            AssetDatabase.SaveAssets();
            report.AppendLine("=== END VEHICLE PREFABS ===");
            Debug.Log(report.ToString());
        }

        private static void ConfigureMaterials(StringBuilder report)
        {
            report.AppendLine("  materials (FBX carries no metallic and no alpha)");

            foreach (var (name, metallic) in MetallicFinish)
            {
                var material = LoadMaterial(name, report);
                if (material == null)
                {
                    continue;
                }

                material.SetFloat("_Metallic", metallic);
                EditorUtility.SetDirty(material);
                report.AppendLine($"    {name,-22} metallic {metallic:F2}  "
                                  + $"smoothness {material.GetFloat("_Smoothness"):F2}");
            }

            foreach (var (name, alpha) in Glazing)
            {
                var material = LoadMaterial(name, report);
                if (material == null)
                {
                    continue;
                }

                MakeTransparent(material, alpha);
                EditorUtility.SetDirty(material);
                report.AppendLine($"    {name,-22} transparent alpha {alpha:F2}  "
                                  + $"queue {material.renderQueue}  "
                                  + $"smoothness {material.GetFloat("_Smoothness"):F2}");
            }
        }

        private static Material LoadMaterial(string name, StringBuilder report)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{name}.mat");
            if (material == null)
            {
                report.AppendLine($"    {name,-22} MATERIAL MISSING - import the FBX first");
            }

            return material;
        }

        private static void MakeTransparent(Material material, float alpha)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_Cull", (float)CullMode.Back);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);

            var colour = material.GetColor("_BaseColor");
            colour.a = alpha;
            material.SetColor("_BaseColor", colour);
            material.SetColor("_Color", colour);
        }

        private static void BuildPrefab(string modelName, StringBuilder report)
        {
            var modelPath = $"{ModelRoot}/{modelName}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                report.AppendLine($"  {modelName,-20} MODEL MISSING at {modelPath}");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = modelName;
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

            var hull = CollectHullPoints(instance, out var readParts, out var missing);
            if (hull.Count == 0)
            {
                Object.DestroyImmediate(instance);
                report.AppendLine($"  {modelName,-20} NO READABLE MESH DATA - "
                                  + "collider cannot be derived");
                return;
            }

            var boxes = BandBoxes(hull);
            var collision = new GameObject(CollisionChild);
            collision.transform.SetParent(instance.transform, false);
            foreach (var box in boxes)
            {
                var collider = collision.AddComponent<BoxCollider>();
                collider.center = box.center;
                collider.size = box.size;
            }

            var statics = ApplyStaticFlags(instance);

            var prefabPath = $"{PrefabRoot}/{modelName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out var success);
            Object.DestroyImmediate(instance);

            var hullBounds = Encapsulate(boxes);
            var boxVolume = boxes.Sum(box => box.size.x * box.size.y * box.size.z);
            var crateVolume = hullBounds.size.x * hullBounds.size.y * hullBounds.size.z;

            report.AppendLine($"  {modelName,-20} {(success ? "ok" : "FAILED")}  "
                              + $"renderers={saved.GetComponentsInChildren<MeshRenderer>(true).Length}  "
                              + $"boxes={boxes.Count}  static={statics}");
            report.AppendLine($"    hull from {readParts} parts, {hull.Count} verts"
                              + (missing.Count > 0 ? $", MISSING {string.Join(",", missing)}" : string.Empty));
            report.AppendLine($"    bounds  centre({hullBounds.center.x:F3},{hullBounds.center.y:F3},"
                              + $"{hullBounds.center.z:F3})  size({hullBounds.size.x:F3},"
                              + $"{hullBounds.size.y:F3},{hullBounds.size.z:F3})");
            report.AppendLine($"    solid   {boxVolume:F3} m3 of {crateVolume:F3} m3 crate "
                              + $"= {100f * boxVolume / crateVolume:F1}% filled");
            foreach (var band in boxes.GroupBy(box => box.center.y).OrderBy(group => group.Key))
            {
                var widest = band.Max(box => box.size.x);
                var run = band.Max(box => box.center.z + box.size.z * 0.5f)
                          - band.Min(box => box.center.z - box.size.z * 0.5f);
                report.AppendLine($"      y {band.Key - band.First().size.y * 0.5f:F3}.."
                                  + $"{band.Key + band.First().size.y * 0.5f:F3}  "
                                  + $"slices {band.Count()}  widest {widest:F3}  "
                                  + $"length {run:F3}");
            }

            report.AppendLine(DescribePaint(saved));

            report.AppendLine(ProbeColliders(saved, model));
        }

        private static string DescribePaint(GameObject prefab)
        {
            var lines = new List<string>();
            var painted = 0;
            var slotIndices = new SortedSet<int>();
            var paintNames = new SortedSet<string>();

            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (var slot = 0; slot < slots.Length; slot++)
                {
                    if (slots[slot] == null || !slots[slot].name.StartsWith("Vehicle_Paint"))
                    {
                        continue;
                    }

                    painted++;
                    slotIndices.Add(slot);
                    paintNames.Add(slots[slot].name);
                }
            }

            lines.Add($"    paint on {painted} submeshes, slot index {string.Join("/", slotIndices)}"
                      + $", material {string.Join(",", paintNames)}");

            var glass = prefab.GetComponentsInChildren<MeshRenderer>(true)
                .FirstOrDefault(renderer => renderer.name == "SUV_Fleet_Glass");
            var glassMaterial = glass?.sharedMaterials
                .FirstOrDefault(material => material != null && material.name == "Vehicle_Glass");
            lines.Add($"    glass {(glassMaterial != null ? "found" : "MISSING")}"
                      + $"  queue {(glassMaterial != null ? glassMaterial.renderQueue : -1)}"
                      + $"  alpha {(glassMaterial != null ? glassMaterial.GetColor("_BaseColor").a : -1f):F2}");

            return string.Join("\n", lines);
        }

        private static string ProbeColliders(GameObject prefab, GameObject model)
        {
            var boxReach = Sweep(prefab, false);
            var meshReach = Sweep(model, true);

            var lines = new List<string>();
            for (var level = 0; level < ProbeHeights.Length; level++)
            {
                var misses = 0;
                var worstGap = 0f;
                var worstAngle = 0f;
                var totalGap = 0f;
                var counted = 0;
                for (var ray = 0; ray < ProbeRays; ray++)
                {
                    var index = level * ProbeRays + ray;
                    if (boxReach[index] < 0f)
                    {
                        misses++;
                        continue;
                    }

                    if (meshReach[index] < 0f)
                    {
                        continue;
                    }

                    var gap = boxReach[index] - meshReach[index];
                    totalGap += Mathf.Max(0f, gap);
                    counted++;
                    if (gap > worstGap)
                    {
                        worstGap = gap;
                        worstAngle = 360f * ray / ProbeRays;
                    }
                }

                var average = counted > 0 ? totalGap / counted : 0f;
                lines.Add($"    probe y={ProbeHeights[level]:F2}  "
                          + (misses == 0 ? "blocked from all 24 sides" : $"MISSED {misses}/24")
                          + $"  invisible wall avg {average * 1000f:F0} mm, "
                          + $"worst {worstGap * 1000f:F0} mm at {worstAngle:F0} deg");
            }

            return string.Join("\n", lines);
        }

        private static float[] Sweep(GameObject source, bool useMeshColliders)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

            if (useMeshColliders)
            {
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!HullParts.Contains(filter.name) || filter.sharedMesh == null)
                    {
                        continue;
                    }

                    filter.gameObject.AddComponent<MeshCollider>();
                }
            }

            Physics.SyncTransforms();

            var reach = new float[ProbeHeights.Length * ProbeRays];
            for (var level = 0; level < ProbeHeights.Length; level++)
            {
                for (var ray = 0; ray < ProbeRays; ray++)
                {
                    var angle = 2f * Mathf.PI * ray / ProbeRays;
                    var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    var origin = direction * ProbeRadius + Vector3.up * ProbeHeights[level];
                    reach[level * ProbeRays + ray] =
                        Physics.Raycast(origin, -direction, out var hit, ProbeRadius * 2f)
                            ? ProbeRadius - hit.distance
                            : -1f;
                }
            }

            Object.DestroyImmediate(instance);
            return reach;
        }

        private static List<Vector3> CollectHullPoints(GameObject root, out int readParts,
            out List<string> missing)
        {
            var points = new List<Vector3>();
            var found = new HashSet<string>();
            readParts = 0;

            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!HullParts.Contains(filter.name) || filter.sharedMesh == null)
                {
                    continue;
                }

                found.Add(filter.name);
                var mesh = filter.sharedMesh;
                if (mesh.vertexCount == 0)
                {
                    continue;
                }

                var toRoot = root.transform.worldToLocalMatrix
                             * filter.transform.localToWorldMatrix;
                foreach (var vertex in mesh.vertices)
                {
                    points.Add(toRoot.MultiplyPoint3x4(vertex));
                }

                readParts++;
            }

            missing = HullParts.Where(name => !found.Contains(name)).ToList();
            return points;
        }

        private static List<Bounds> BandBoxes(List<Vector3> points)
        {
            var low = points[0];
            var high = points[0];
            foreach (var point in points)
            {
                low = Vector3.Min(low, point);
                high = Vector3.Max(high, point);
            }

            var spanY = high.y - low.y;
            var spanZ = high.z - low.z;
            var cells = new Dictionary<int, Vector4>();
            var occupied = new Dictionary<int, bool>();

            foreach (var point in points)
            {
                var band = Mathf.Clamp((int)((point.y - low.y) / spanY * CollisionBands),
                    0, CollisionBands - 1);
                var slice = Mathf.Clamp((int)((point.z - low.z) / spanZ * CollisionSlices),
                    0, CollisionSlices - 1);
                var key = band * CollisionSlices + slice;

                if (!occupied.ContainsKey(key))
                {
                    occupied[key] = true;
                    cells[key] = new Vector4(point.x, point.x, point.z, point.z);
                    continue;
                }

                var cell = cells[key];
                cells[key] = new Vector4(Mathf.Min(cell.x, point.x), Mathf.Max(cell.y, point.x),
                    Mathf.Min(cell.z, point.z), Mathf.Max(cell.w, point.z));
            }

            var boxes = new List<Bounds>();
            foreach (var entry in cells.OrderBy(pair => pair.Key))
            {
                var band = entry.Key / CollisionSlices;
                var cell = entry.Value;
                var bandLow = low.y + spanY * band / CollisionBands;
                var bandHigh = low.y + spanY * (band + 1) / CollisionBands;

                var centre = new Vector3((cell.x + cell.y) * 0.5f,
                    (bandLow + bandHigh) * 0.5f, (cell.z + cell.w) * 0.5f);
                var size = new Vector3(cell.y - cell.x, bandHigh - bandLow,
                    cell.w - cell.z + CollisionMargin * 2f);
                boxes.Add(new Bounds(centre, size));
            }

            return boxes;
        }

        private static Bounds Encapsulate(List<Bounds> boxes)
        {
            var bounds = boxes[0];
            foreach (var box in boxes.Skip(1))
            {
                bounds.Encapsulate(box);
            }

            return bounds;
        }

        private static int ApplyStaticFlags(GameObject root)
        {
            var marked = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (MovableParts.Contains(transform.name)
                    || transform.name == CollisionChild)
                {
                    continue;
                }

                GameObjectUtility.SetStaticEditorFlags(transform.gameObject, StaticFlags);
                marked++;
            }

            return marked;
        }
    }
}
