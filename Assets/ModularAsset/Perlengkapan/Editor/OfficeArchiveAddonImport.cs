using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Prepare the authored addon FBXs through Unity's native import and prefab APIs.</summary>
[InitializeOnLoad]
public static class OfficeArchiveAddonImport
{
    const string Folder = "Assets/ModularAsset/Perlengkapan";
    const string Source = "SourceArt/OfficeArchiveAddons";
    static double nextCheck;
    static bool busy;
    static OfficeArchiveAddonImport() { EditorApplication.update += CheckPending; }

    [Serializable] public class AssetInfo { public string name, category, description, anchor, fbx; public float snap_m; public float[] size_blender_m; public string[] moving_pivots; }
    [Serializable] public class MaterialInfo { public string name, key; public float[] srgb; public float roughness, metallic; }
    [Serializable] public class Placement { public string asset; public float[] position_blender; public float rotation_z; }
    [Serializable] public class Example { public string name; public Placement[] placements; }
    [Serializable] public class Manifest { public AssetInfo[] assets; public MaterialInfo[] materials; public Example[] examples; }
    [Serializable] public class ModelResult { public string name, prefab; public int renderers, triangles, missingMaterials; public Vector3 size; public bool positiveScale; }
    [Serializable] public class Report { public string checkedUtc; public int models, prefabs, materialCount; public bool pass; public List<string> errors = new List<string>(); public List<ModelResult> assets = new List<ModelResult>(); public List<string> examples = new List<string>(); }

    static void CheckPending()
    {
        if (busy || EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        nextCheck = EditorApplication.timeSinceStartup + 5;
        string ready = Source + "/export.ready", report = Source + "/unity-import-verification.json";
        if (!File.Exists(ready) || !File.Exists(Folder + "/manifest.json")) return;
        if (File.Exists(report) && File.GetLastWriteTimeUtc(report) > File.GetLastWriteTimeUtc(ready)) return;
        Prepare();
    }

    [MenuItem("Tools/Modular Asset/Prepare Addon Assets")]
    public static void Prepare()
    {
        if (busy) return;
        busy = true;
        var report = new Report { checkedUtc = DateTime.UtcNow.ToString("O") };
        Scene previousScene = SceneManager.GetActiveScene();
        Scene workScene = default;
        try
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "/manifest.json"));
            if (manifest.assets == null || manifest.assets.Length == 0) throw new Exception("Asset manifest is empty");
            if (manifest.assets.Any(a => !File.Exists(a.fbx))) throw new Exception("Blender export is still incomplete");
            workScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(workScene);
            Directory.CreateDirectory(Folder + "/Materials");
            Directory.CreateDirectory(Folder + "/Prefabs");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new Exception("URP Lit shader was not found");
            var materials = new Dictionary<string, Material>();
            foreach (var info in manifest.materials)
            {
                string path = Folder + "/Materials/" + info.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.name = info.name; mat.shader = shader;
                Color color = new Color(info.srgb[0], info.srgb[1], info.srgb[2], 1);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Metallic", info.metallic);
                mat.SetFloat("_Smoothness", 1 - info.roughness);
                mat.SetFloat("_Cull", 2);
                mat.enableInstancing = true;
                string texturePath = Folder + "/Textures/" + info.name + "_Albedo.png";
                if (File.Exists(texturePath))
                {
                    var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                    textureImporter.wrapMode = TextureWrapMode.Repeat; textureImporter.sRGBTexture = true;
                    textureImporter.maxTextureSize = 512; textureImporter.SaveAndReimport();
                    mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                    mat.SetColor("_BaseColor", Color.white);
                }
                if (info.key == "light") { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * .7f); }
                EditorUtility.SetDirty(mat); materials[info.name] = mat;
            }
            report.materialCount = materials.Count;
            var models = new Dictionary<string, GameObject>();
            foreach (var info in manifest.assets)
            {
                var importer = AssetImporter.GetAtPath(info.fbx) as ModelImporter;
                if (!importer) { report.errors.Add("No model importer: " + info.fbx); continue; }
                importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
                importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                importer.materialSearch = ModelImporterMaterialSearch.Local;
                importer.generateSecondaryUV = true; importer.isReadable = false;
                importer.preserveHierarchy = true; importer.addCollider = false;
                foreach (var mat in materials)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mat.Key), mat.Value);
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(info.fbx);
                if (!model) { report.errors.Add("Model not loaded: " + info.name); continue; }
                models[info.name] = model;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, workScene);
                instance.name = info.name; instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                var result = new ModelResult { name = info.name, renderers = renderers.Length, positiveScale = true };
                Bounds bounds = new Bounds(); bool first = true;
                foreach (var renderer in renderers)
                {
                    if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                    foreach (var mat in renderer.sharedMaterials)
                        if (!mat || !AssetDatabase.GetAssetPath(mat).StartsWith(Folder + "/Materials/", StringComparison.Ordinal)) result.missingMaterials++;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter && filter.sharedMesh) for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++) result.triangles += (int)filter.sharedMesh.GetIndexCount(sub) / 3;
                    // Parked environment geometry uses accurate static collision; moving panels get simple boxes.
                    if (info.category != "Petunjuk" && info.category != "Lampu")
                    {
                        bool moving = renderer.transform.parent && renderer.transform.parent != instance.transform && !renderer.transform.parent.name.StartsWith("OAA_", StringComparison.Ordinal);
                        if (moving) renderer.gameObject.AddComponent<BoxCollider>();
                        else { var collider = renderer.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = filter.sharedMesh; }
                    }
                }
                foreach (Transform t in instance.GetComponentsInChildren<Transform>(true)) if (t.localScale.x <= 0 || t.localScale.y <= 0 || t.localScale.z <= 0) result.positiveScale = false;
                result.size = bounds.size;
                Vector3 expected = new Vector3(info.size_blender_m[0], info.size_blender_m[2], info.size_blender_m[1]);
                if ((expected - result.size).magnitude > .025f) report.errors.Add(info.name + ": imported metric size differs by " + (expected - result.size).magnitude);
                if (result.missingMaterials > 0 || !result.positiveScale || renderers.Length == 0) report.errors.Add(info.name + ": material, scale or renderer issue");
                string prefabFolder = Folder + "/Prefabs/" + info.category;
                Directory.CreateDirectory(prefabFolder); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                result.prefab = prefabFolder + "/" + info.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, result.prefab);
                UnityEngine.Object.DestroyImmediate(instance);
                AssetDatabase.SetLabels(model, new [] { "OfficeArchive", "Addon", info.category });
                report.assets.Add(result); report.models++; report.prefabs++;
            }
            if (manifest.examples != null)
                foreach (var example in manifest.examples)
                {
                    var root = new GameObject(example.name);
                    foreach (var p in example.placements)
                    {
                        if (!models.TryGetValue(p.asset, out var model)) continue;
                        string prefabPath = Folder + "/Prefabs/" + manifest.assets.First(a => a.name == p.asset).category + "/" + p.asset + ".prefab";
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                        var child = (GameObject)PrefabUtility.InstantiatePrefab(prefab, workScene);
                        child.transform.SetParent(root.transform, false);
                        child.transform.localPosition = new Vector3(p.position_blender[0], p.position_blender[2], p.position_blender[1]);
                        child.transform.localRotation = Quaternion.Euler(0, -p.rotation_z * Mathf.Rad2Deg, 0);
                    }
                    string examplePath = Folder + "/Prefabs/" + example.name + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(root, examplePath); UnityEngine.Object.DestroyImmediate(root); report.examples.Add(examplePath);
                }
            report.pass = report.errors.Count == 0 && report.models == manifest.assets.Length;
            AssetDatabase.SaveAssets();
            File.WriteAllText(Source + "/unity-import-verification.json", JsonUtility.ToJson(report, true));
            Debug.Log("Office Archive addons: " + report.models + " models, " + report.prefabs + " prefabs; pass=" + report.pass);
        }
        catch (Exception e)
        {
            report.errors.Add(e.ToString()); report.pass = false;
            File.WriteAllText(Source + "/unity-import-verification.json", JsonUtility.ToJson(report, true));
            Debug.LogError("Office Archive addon import: " + e.Message);
        }
        finally
        {
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (workScene.IsValid() && workScene.isLoaded) EditorSceneManager.CloseScene(workScene, true);
            busy = false;
        }
    }
}
