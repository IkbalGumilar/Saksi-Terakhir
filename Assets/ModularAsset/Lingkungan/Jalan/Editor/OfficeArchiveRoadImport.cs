using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Imports the modular street kit with Unity's native model, material and prefab APIs.</summary>
[InitializeOnLoad]
public static class OfficeArchiveRoadImport
{
    const string Folder = "Assets/ModularAsset/Lingkungan/Jalan";
    const string Source = "SourceArt/OfficeArchiveRoads";
    const float SizeTolerance = .025f;
    static double nextCheck;
    static bool busy;

    static OfficeArchiveRoadImport() { EditorApplication.update += CheckPending; }

    [Serializable] public class AssetInfo
    {
        public string name, category, description, anchor, fbx;
        public float snap_m;
        public float[] size_blender_m;
        public float[] bounds_min_blender, bounds_max_blender;
        public string[] moving_pivots;
    }
    [Serializable] public class MaterialInfo { public string name, key; public float[] srgb; public float roughness, metallic; }
    [Serializable] public class Placement { public string asset; public float[] position_blender; public float rotation_z; }
    [Serializable] public class Example { public string name; public Placement[] placements; }
    [Serializable] public class Manifest { public AssetInfo[] assets; public MaterialInfo[] materials; public Example[] examples; }
    [Serializable] public class ModelResult
    {
        public string name, category, prefab;
        public int renderers, triangles, colliders, markingRenderers, markingColliders, missingMaterials;
        public Vector3 size, expectedSize;
        public Vector3 boundsMin, boundsMax;
        public float sizeDifference;
        public float boundsDifference;
        public bool positiveScale;
    }
    [Serializable] public class ImportMessage { public string asset, severity, message; }
    [Serializable] public class Report
    {
        public string checkedUtc;
        public int expectedModels, models, prefabs, materialCount;
        public bool pass;
        public List<string> errors = new List<string>();
        public List<ImportMessage> unityMessages = new List<ImportMessage>();
        public List<ModelResult> assets = new List<ModelResult>();
        public List<string> examples = new List<string>();
    }

    static string DiskPath(string relative) { return Path.Combine(Path.GetDirectoryName(Application.dataPath), relative); }

    static void CheckPending()
    {
        if (busy || EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        nextCheck = EditorApplication.timeSinceStartup + 5;
        string ready = DiskPath(Source + "/export.ready"), report = DiskPath(Source + "/unity-import-verification.json");
        if (!File.Exists(ready) || !File.Exists(DiskPath(Folder + "/manifest.json"))) return;
        if (File.Exists(report) && File.GetLastWriteTimeUtc(report) >= File.GetLastWriteTimeUtc(ready)) return;
        Prepare();
    }

    static bool IsMarking(Transform part, Transform root)
    {
        for (var t = part; t && t != root; t = t.parent)
            if (t.name == "Markings" || t.name.StartsWith("Markings_", StringComparison.Ordinal)) return true;
        return false;
    }

    static bool IsStructure(string category)
    {
        return category == "Jalan" || category == "Trotoar" || category == "Kanstin" || category == "Tembok";
    }

    static bool PositiveScale(Transform transform)
    {
        Vector3 scale = transform.localScale;
        return scale.x > 0 && scale.y > 0 && scale.z > 0 && transform.localToWorldMatrix.determinant > 0;
    }

    [MenuItem("Tools/Modular Asset/Prepare Road Assets")]
    public static void Prepare()
    {
        if (busy || EditorApplication.isPlayingOrWillChangePlaymode) return;
        busy = true;
        var report = new Report { checkedUtc = DateTime.UtcNow.ToString("O") };
        Scene previousScene = SceneManager.GetActiveScene();
        Scene workScene = default;
        string currentAsset = "Preparation";
        Application.LogCallback capture = (message, stack, type) =>
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert && type != LogType.Warning) return;
            report.unityMessages.Add(new ImportMessage { asset = currentAsset, severity = type.ToString(), message = message });
            if (type != LogType.Warning) report.errors.Add(currentAsset + ": Unity " + type + ": " + message);
        };
        Application.logMessageReceived += capture;
        try
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(DiskPath(Folder + "/manifest.json")));
            if (manifest == null || manifest.assets == null || manifest.assets.Length == 0) throw new Exception("Asset manifest is empty");
            if (manifest.materials == null || manifest.materials.Length == 0) throw new Exception("Material manifest is empty");
            report.expectedModels = manifest.assets.Length;
            if (manifest.assets.Any(a => !File.Exists(DiskPath(a.fbx)))) throw new Exception("Blender export is still incomplete");
            if (manifest.assets.GroupBy(a => a.name).Any(g => g.Count() > 1)) throw new Exception("Asset names are not unique");

            // A preview scene works even when the user's active scene is untitled/unsaved.
            workScene = EditorSceneManager.NewPreviewScene();
            Directory.CreateDirectory(DiskPath(Folder + "/Materials"));
            Directory.CreateDirectory(DiskPath(Folder + "/Prefabs"));
            foreach (string category in manifest.assets.Select(a => a.category).Distinct())
                Directory.CreateDirectory(DiskPath(Folder + "/Prefabs/" + category));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new Exception("URP Lit shader was not found");
            var materials = new Dictionary<string, Material>();
            foreach (var info in manifest.materials)
            {
                currentAsset = info.name;
                if (info.srgb == null || info.srgb.Length < 3) throw new Exception("Invalid material color: " + info.name);
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
                if (File.Exists(DiskPath(texturePath)))
                {
                    var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if (!textureImporter) throw new Exception("No texture importer: " + texturePath);
                    textureImporter.wrapMode = TextureWrapMode.Repeat;
                    textureImporter.sRGBTexture = true;
                    textureImporter.maxTextureSize = 1024;
                    textureImporter.mipmapEnabled = true;
                    textureImporter.SaveAndReimport();
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (!texture) throw new Exception("Texture not loaded: " + texturePath);
                    mat.SetTexture("_BaseMap", texture);
                    mat.SetColor("_BaseColor", Color.white);
                }
                if (info.key == "light") { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * .7f); }
                EditorUtility.SetDirty(mat);
                materials.Add(info.name, mat);
            }
            report.materialCount = materials.Count;
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var info in manifest.assets)
            {
                currentAsset = info.name;
                GameObject instance = null;
                try
                {
                    if (info.size_blender_m == null || info.size_blender_m.Length != 3) throw new Exception("Expected metric dimensions are missing");
                    var importer = AssetImporter.GetAtPath(info.fbx) as ModelImporter;
                    if (!importer) throw new Exception("No model importer: " + info.fbx);
                    importer.globalScale = 1;
                    importer.useFileScale = true;
                    importer.bakeAxisConversion = true;
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
                    if (!model) throw new Exception("Model not loaded: " + info.fbx);
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(model, workScene);
                    instance.name = info.name;
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                    var result = new ModelResult
                    {
                        name = info.name, category = info.category, renderers = renderers.Length, positiveScale = true,
                        expectedSize = new Vector3(info.size_blender_m[0], info.size_blender_m[2], info.size_blender_m[1])
                    };
                    Bounds bounds = new Bounds(); bool first = true;
                    foreach (var renderer in renderers)
                    {
                        if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                        foreach (var mat in renderer.sharedMaterials)
                            if (!mat || !AssetDatabase.GetAssetPath(mat).StartsWith(Folder + "/Materials/", StringComparison.Ordinal)) result.missingMaterials++;
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (!filter || !filter.sharedMesh) { report.errors.Add(info.name + ": renderer has no mesh: " + renderer.name); continue; }
                        for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++) result.triangles += (int)filter.sharedMesh.GetIndexCount(sub) / 3;
                        bool marking = info.category == "Marka" || IsMarking(renderer.transform, instance.transform);
                        if (marking) result.markingRenderers++;
                        if (IsStructure(info.category) && !marking)
                        {
                            var collider = renderer.gameObject.AddComponent<MeshCollider>();
                            collider.sharedMesh = filter.sharedMesh;
                            collider.convex = false;
                        }
                        int colliderCount = renderer.GetComponents<Collider>().Length;
                        result.colliders += colliderCount;
                        if (marking) result.markingColliders += colliderCount;
                    }
                    foreach (Transform t in instance.GetComponentsInChildren<Transform>(true)) if (!PositiveScale(t)) result.positiveScale = false;
                    result.size = bounds.size;
                    result.boundsMin = bounds.min; result.boundsMax = bounds.max;
                    result.sizeDifference = (result.expectedSize - result.size).magnitude;
                    if (info.bounds_min_blender != null && info.bounds_max_blender != null)
                    {
                        var lo = info.bounds_min_blender; var hi = info.bounds_max_blender;
                        var expectedMin = new Vector3(lo[0], lo[2], lo[1]);
                        var expectedMax = new Vector3(hi[0], hi[2], hi[1]);
                        result.boundsDifference = Mathf.Max((expectedMin - bounds.min).magnitude, (expectedMax - bounds.max).magnitude);
                        if (result.boundsDifference > SizeTolerance) report.errors.Add(info.name + ": imported pivot/orientation bounds differ by " + result.boundsDifference.ToString("F5") + " m");
                    }
                    if (result.sizeDifference > SizeTolerance) report.errors.Add(info.name + ": imported metric size differs by " + result.sizeDifference.ToString("F5") + " m");
                    if (result.missingMaterials > 0 || !result.positiveScale || result.renderers == 0 || result.triangles == 0)
                        report.errors.Add(info.name + ": material, scale, renderer or geometry issue");
                    if (result.markingColliders > 0) report.errors.Add(info.name + ": markings unexpectedly have colliders");
                    if (IsStructure(info.category) && result.colliders == 0) report.errors.Add(info.name + ": structural geometry has no collider");
                    result.prefab = Folder + "/Prefabs/" + info.category + "/" + info.name + ".prefab";
                    var prefab = PrefabUtility.SaveAsPrefabAsset(instance, result.prefab);
                    if (!prefab) throw new Exception("Prefab was not saved: " + result.prefab);
                    prefabs.Add(info.name, prefab);
                    AssetDatabase.SetLabels(model, new[] { "OfficeArchive", "Roads", info.category });
                    report.assets.Add(result); report.models++; report.prefabs++;
                }
                catch (Exception e) { report.errors.Add(info.name + ": " + e); }
                finally { if (instance) UnityEngine.Object.DestroyImmediate(instance); }
            }
            if (manifest.examples != null)
                foreach (var example in manifest.examples)
                {
                    currentAsset = example.name;
                    var root = new GameObject(example.name);
                    SceneManager.MoveGameObjectToScene(root, workScene);
                    try
                    {
                        foreach (var p in example.placements ?? new Placement[0])
                        {
                            if (!prefabs.TryGetValue(p.asset, out var prefab)) { report.errors.Add(example.name + ": missing example asset " + p.asset); continue; }
                            if (p.position_blender == null || p.position_blender.Length != 3) { report.errors.Add(example.name + ": invalid position for " + p.asset); continue; }
                            var child = (GameObject)PrefabUtility.InstantiatePrefab(prefab, workScene);
                            child.transform.SetParent(root.transform, false);
                            child.transform.localPosition = new Vector3(p.position_blender[0], p.position_blender[2], p.position_blender[1]);
                            child.transform.localRotation = Quaternion.Euler(0, -p.rotation_z * Mathf.Rad2Deg, 0);
                        }
                        string examplePath = Folder + "/Prefabs/" + example.name + ".prefab";
                        if (!PrefabUtility.SaveAsPrefabAsset(root, examplePath)) throw new Exception("Example prefab was not saved: " + examplePath);
                        report.examples.Add(examplePath);
                    }
                    catch (Exception e) { report.errors.Add(example.name + ": " + e); }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
            currentAsset = "Saving";
            AssetDatabase.SaveAssets();
            report.pass = report.errors.Count == 0 && report.models == manifest.assets.Length && report.prefabs == manifest.assets.Length;
        }
        catch (Exception e) { report.errors.Add(e.ToString()); report.pass = false; }
        finally
        {
            Application.logMessageReceived -= capture;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (workScene.IsValid() && workScene.isLoaded) EditorSceneManager.ClosePreviewScene(workScene);
            busy = false;
            Directory.CreateDirectory(DiskPath(Source));
            File.WriteAllText(DiskPath(Source + "/unity-import-verification.json"), JsonUtility.ToJson(report, true));
            if (report.pass) Debug.Log("Office Archive roads: " + report.models + " models, " + report.prefabs + " prefabs; pass=True");
            else Debug.LogError("Office Archive road import failed: " + report.errors.Count + " errors; see " + Source + "/unity-import-verification.json");
        }
    }
}
