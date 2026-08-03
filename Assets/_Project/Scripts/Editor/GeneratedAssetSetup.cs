using System.Collections.Generic;
using System.Linq;
using System.Text;
using SaksiTerakhir.Interaction;
using UnityEditor;
using UnityEngine;

namespace SaksiTerakhir.EditorTools
{
    public static class GeneratedAssetSetup
    {
        private const string ModelRoot = "Assets/_Project/Art/Models";
        private const string PrefabRoot = "Assets/_Project/Prefabs";
        private const string BuildingPrefab = "Office_ArchiveHQ";
        private const string BuildingModel = "Office_ArchiveHQ_Textured";
        private const string DoorLeafPrefix = "Office_ArchiveHQ_DoorLeaf";

        private const StaticEditorFlags StaticFlags =
            StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic
            | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic
            | StaticEditorFlags.NavigationStatic | StaticEditorFlags.ReflectionProbeStatic;

        private static readonly Vector3 GateLocal = new Vector3(-6.70f, 1.20f, -0.75f);
        private static readonly Vector3 EntranceLocal = new Vector3(-6.50f, 1.20f, -8.40f);

        private static readonly (string model, string prefab)[] Prefabs =
        {
            ("Office_ArchiveHQ_Textured", BuildingPrefab),
            ("MC_ArchiveOfficer_Textured", "MC_ArchiveOfficer"),
            ("B_Colleague_Textured", "B_Colleague"),
        };

        [MenuItem("Saksi Terakhir/Create Prefabs")]
        public static void CreatePrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabRoot))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            }

            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("=== PREFABS ===");

            foreach (var (modelName, prefabName) in Prefabs)
            {
                var modelPath = $"{ModelRoot}/{modelName}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null)
                {
                    report.AppendLine($"  {prefabName,-22} MODEL MISSING at {modelPath}");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = prefabName;

                var colliders = 0;
                var doors = 0;
                if (prefabName == BuildingPrefab)
                {
                    colliders = AddColliders(instance);
                    doors = AddDoors(instance);
                    foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
                    {
                        if (IsDoorLeaf(transform.gameObject))
                        {
                            continue;
                        }

                        GameObjectUtility.SetStaticEditorFlags(transform.gameObject, StaticFlags);
                    }
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(
                    instance, $"{PrefabRoot}/{prefabName}.prefab", out var success);
                Object.DestroyImmediate(instance);

                var bounds = MeasureBounds(saved);
                report.AppendLine($"  {prefabName,-22} {(success ? "ok" : "FAILED")}  "
                                  + $"size=({bounds.size.x:F2}, {bounds.size.y:F2}, {bounds.size.z:F2})  "
                                  + $"colliders={colliders}  doors={doors}");
            }

            AssetDatabase.SaveAssets();
            report.AppendLine(DescribeBuildingParts());
            report.AppendLine("=== END PREFABS ===");
            Debug.Log(report.ToString());
        }

        [MenuItem("Saksi Terakhir/Probe Player Route")]
        public static void ProbePlayerRoute()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var building = scene.GetRootGameObjects()
                .FirstOrDefault(o => o.name == BuildingPrefab || o.name == BuildingModel);
            var player = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "Player");

            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("=== PLAYER ROUTE PROBE ===");
            if (building == null || player == null)
            {
                report.AppendLine($"  needs both '{BuildingPrefab}' and 'Player' in the open scene "
                                  + $"(building={building != null}, player={player != null})");
                report.AppendLine("=== END PLAYER ROUTE PROBE ===");
                Debug.Log(report.ToString());
                return;
            }

            var colliders = building.GetComponentsInChildren<MeshCollider>(true).Length;
            var meshes = building.GetComponentsInChildren<MeshFilter>(true).Length;
            report.AppendLine($"  source {building.name}  meshColliders={colliders}/{meshes}"
                              + (colliders == 0
                                  ? "  <- nothing solid: the player will fall through"
                                  : string.Empty));

            Physics.SyncTransforms();
            var start = player.transform.position;
            var gate = building.transform.TransformPoint(GateLocal);
            var entrance = building.transform.TransformPoint(EntranceLocal);
            report.AppendLine($"  player {start}  gate {gate}  entrance {entrance}");

            foreach (var (label, target) in new[] { ("player->gate", gate), ("gate->entrance", entrance) })
            {
                var from = label.StartsWith("player") ? start : gate;
                var worst = float.MaxValue;
                var gap = 0f;
                for (var step = 0; step <= 12; step++)
                {
                    var at = Vector3.Lerp(from, target, step / 12f);
                    var above = new Vector3(at.x, at.y + 2.5f, at.z);
                    worst = Physics.Raycast(above, Vector3.down, out var hit, 6f)
                        ? Mathf.Min(worst, hit.point.y)
                        : float.NaN;
                    if (Physics.CheckCapsule(new Vector3(at.x, at.y - 0.55f, at.z),
                            new Vector3(at.x, at.y + 0.35f, at.z), 0.3f))
                    {
                        gap++;
                    }
                }

                report.AppendLine($"  {label,-16} lowest ground y={worst:F2}  "
                                  + $"capsule blocked at {gap}/13 samples");
            }

            report.AppendLine("=== END PLAYER ROUTE PROBE ===");
            Debug.Log(report.ToString());
        }

        private static int AddColliders(GameObject root)
        {
            var added = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshCollider>() != null)
                {
                    continue;
                }

                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = IsDoorLeaf(filter.gameObject);
                added++;
            }

            return added;
        }

        private static int AddDoors(GameObject root)
        {
            var added = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!IsDoorLeaf(filter.gameObject)
                    || filter.GetComponent<DoorInteractable>() != null)
                {
                    continue;
                }

                filter.gameObject.AddComponent<DoorInteractable>();
                added++;
            }

            return added;
        }

        private static bool IsDoorLeaf(GameObject candidate)
        {
            return candidate.name.StartsWith(DoorLeafPrefix, System.StringComparison.Ordinal);
        }

        private static string DescribeBuildingParts()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{ModelRoot}/Office_ArchiveHQ_Textured.fbx");
            if (model == null)
            {
                return "  building parts: model missing";
            }

            var lines = new List<string> { "  building parts (prefab local space):" };
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true)
                         .OrderBy(r => r.name))
            {
                var bounds = renderer.bounds;
                lines.Add($"    {renderer.name.Replace("Office_ArchiveHQ_", ""),-12} "
                          + $"x[{bounds.min.x,7:F2},{bounds.max.x,7:F2}] "
                          + $"y[{bounds.min.y,7:F2},{bounds.max.y,7:F2}] "
                          + $"z[{bounds.min.z,7:F2},{bounds.max.z,7:F2}]");
            }

            return string.Join("\n", lines);
        }

        private static Bounds MeasureBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }
    }
}
