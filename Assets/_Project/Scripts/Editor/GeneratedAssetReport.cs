using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SaksiTerakhir.EditorTools
{
    public static class GeneratedAssetReport
    {
        private const string ModelRoot = "Assets/_Project/Art/Models";
        private const string TextureRoot = "Assets/_Project/Art/Textures";

        private static readonly HumanBodyBones[] RequiredBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
            HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
        };

        [MenuItem("Saksi Terakhir/Report Generated Assets")]
        public static void Run()
        {
            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("=== GENERATED ASSET REPORT ===");

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    report.AppendLine($"  {System.IO.Path.GetFileName(path),-34} FAILED TO LOAD");
                    continue;
                }

                var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                var skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var triangles = filters.Sum(f => f.sharedMesh == null ? 0 : f.sharedMesh.triangles.Length / 3)
                                + skinned.Sum(s => s.sharedMesh == null ? 0 : s.sharedMesh.triangles.Length / 3);
                var materials = new HashSet<Material>();
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null)
                        {
                            materials.Add(material);
                        }
                    }
                }

                report.AppendLine($"  {System.IO.Path.GetFileName(path)}");
                var vertices = filters.Sum(f => f.sharedMesh == null ? 0 : f.sharedMesh.vertexCount)
                               + skinned.Sum(s => s.sharedMesh == null ? 0 : s.sharedMesh.vertexCount);
                report.AppendLine($"      renderers  mesh={filters.Length} skinned={skinned.Length} "
                                  + $"tris={triangles} verts={vertices} materials={materials.Count}");
                report.AppendLine($"      importer   rig={importer.animationType} "
                                  + $"normals={importer.importNormals} lightmapUV={importer.generateSecondaryUV}");

                foreach (var material in materials.OrderBy(m => m.name))
                {
                    var texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                    report.AppendLine($"      material   {material.name,-34} shader={material.shader.name} "
                                      + $"baseMap={(texture == null ? "MISSING" : texture.name)}");
                }

                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    continue;
                }

                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (avatar == null)
                {
                    report.AppendLine("      avatar     MISSING");
                    continue;
                }

                var mapped = importer.humanDescription.human.Select(b => b.humanName).ToHashSet();
                var missing = RequiredBones
                    .Select(b => HumanTrait.BoneName[(int)b])
                    .Where(n => !mapped.Contains(n))
                    .ToArray();
                report.AppendLine($"      avatar     valid={avatar.isValid} human={avatar.isHuman} "
                                  + $"mappedBones={mapped.Count} "
                                  + $"missingRequired={(missing.Length == 0 ? "none" : string.Join(",", missing))}");
            }

            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureRoot });
            var normalMaps = 0;
            long bytes = 0;
            foreach (var guid in textures)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType == TextureImporterType.NormalMap)
                {
                    normalMaps++;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    bytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
                }
            }

            report.AppendLine($"  textures     count={textures.Length} normalMaps={normalMaps} "
                              + $"runtimeMemory={bytes / (1024f * 1024f):F1} MB");
            report.AppendLine("=== END REPORT ===");
            Debug.Log(report.ToString());
        }
    }
}
