using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Imports the MRP modular stilt house explicitly using native Unity asset APIs.</summary>
public static class ModularStiltHouseImport
{
    const string Folder = "Assets/ModularAsset/Bangunan/RumahPanggung";
    const string Source = "SourceArt/ModularAsset/RumahPanggung";
    const string ReportName = "/unity-import-verification.json";
    const float BoundsTolerance = .025f;
    const int MaximumTriangles = 40000;
    static bool busy;

    [Serializable] public class CollisionBox { public float[] center_blender_m, size_blender_m; }
    [Serializable] public class AssetInfo
    {
        public string name, category, description, anchor, fbx;
        public float snap_m;
        public float[] size_blender_m, bounds_min_blender, bounds_max_blender;
        public int triangles;
        public string[] moving_pivots;
        public CollisionBox[] collision_boxes;
        public bool collision_mesh;
    }
    [Serializable] public class MaterialInfo { public string name, key; public float[] srgb; public float roughness, metallic; }
    [Serializable] public class Placement { public string asset; public float[] position_blender; public float rotation_z; }
    [Serializable] public class Example { public string name; public Placement[] placements; }
    [Serializable] public class Manifest { public AssetInfo[] assets; public MaterialInfo[] materials; public Example[] examples; }
    [Serializable] public class CollisionResult { public string type, path; public Vector3 center, size; public int triangles; }
    [Serializable] public class ModelResult
    {
        public string name, category, model, prefab, modelGuid, prefabGuid, modelSha256, prefabSha256;
        public int renderers, meshes, vertices, triangles, expectedTriangles, colliders, meshColliders, boxColliders;
        public int missingMaterials, meshesMissingNormals, meshesMissingPrimaryUv, meshesMissingLightmapUv;
        public int animators, animations, skinnedRenderers, excludedDetailColliders, lodGroups;
        public string[] declaredMovingPivots, importedMovingPivots;
        public Vector3 size, expectedSize, boundsMin, boundsMax;
        public float sizeDifference, boundsDifference;
        public bool positiveScale, allRenderersEnabled, withinTriangleBudget;
        public List<CollisionResult> collision = new List<CollisionResult>();
    }
    [Serializable] public class ImportMessage { public string asset, severity, message; }
    [Serializable] public class FileResult { public string path, guid, sha256; }
    [Serializable] public class SceneState
    {
        public int handle;
        public string path, name;
        public bool dirty;
        public int[] rootIds;
    }
    [Serializable] public class Report
    {
        public string checkedUtc, completedUtc, exportReadyUtc, projectPath, unityVersion;
        public string folder = Folder;
        public int expectedModels, models, prefabs, materialCount, textureCount, expectedExamples;
        public bool pass, preservedEditorScenes;
        public List<string> errors = new List<string>();
        public List<ImportMessage> unityMessages = new List<ImportMessage>();
        public List<ModelResult> assets = new List<ModelResult>();
        public List<string> examples = new List<string>();
        public List<FileResult> materials = new List<FileResult>();
        public List<FileResult> textures = new List<FileResult>();
        public List<FileResult> exampleFiles = new List<FileResult>();
        public List<SceneState> scenesBefore = new List<SceneState>();
        public List<SceneState> scenesAfter = new List<SceneState>();
    }

    static string DiskPath(string relative) { return Path.Combine(Path.GetDirectoryName(Application.dataPath), relative); }
    static string FileHash(string relative)
    {
        using (var hash = SHA256.Create()) using (var stream = File.OpenRead(DiskPath(relative)))
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    static bool IsVector(float[] value) { return value != null && value.Length == 3 && value.All(v => !float.IsNaN(v) && !float.IsInfinity(v)); }
    static Vector3 ToUnity(float[] value) { return new Vector3(value[0], value[2], value[1]); }
    static bool SafeName(string value) { return !string.IsNullOrEmpty(value) && value != "." && value != ".." && value.IndexOfAny(new[] { '/', '\\' }) < 0; }
    static bool PositiveScale(Transform transform)
    {
        var scale = transform.localScale;
        return scale.x > 0 && scale.y > 0 && scale.z > 0 && transform.localToWorldMatrix.determinant > 0;
    }
    static List<SceneState> CaptureScenes()
    {
        var result = new List<SceneState>();
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
            result.Add(new SceneState { handle = scene.handle, path = scene.path, name = scene.name, dirty = scene.isDirty,
                rootIds = scene.GetRootGameObjects().Select(root => root.GetInstanceID()).OrderBy(id => id).ToArray() });
        }
        return result;
    }
    static bool SameScenes(List<SceneState> before, List<SceneState> after)
    {
        if (before.Count != after.Count) return false;
        return before.All(old => after.Any(current => old.handle == current.handle && old.path == current.path
            && old.name == current.name && old.dirty == current.dirty && old.rootIds.SequenceEqual(current.rootIds)));
    }
    static int MeshTriangles(Mesh mesh)
    {
        int count = 0;
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            if (mesh.GetTopology(sub) != MeshTopology.Triangles) throw new Exception("Non-triangle imported submesh: " + mesh.name);
            count += (int)mesh.GetIndexCount(sub) / 3;
        }
        return count;
    }
    static bool ExcludedCollisionDetail(Transform part, Transform root)
    {
        for (var current = part; current && current != root; current = current.parent)
        {
            string name = current.name.ToLowerInvariant();
            if (name.Contains("label") || name.Contains("letter") || name.Contains("text") || name.Contains("hose") || name.Contains("nozzle")) return true;
        }
        return false;
    }
    static void ConfigureMaterial(Material material, Shader shader, MaterialInfo info)
    {
        material.name = info.name;
        material.shader = shader;
        var color = new Color(info.srgb[0], info.srgb[1], info.srgb[2], 1);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", Mathf.Clamp01(info.metallic));
        material.SetFloat("_Smoothness", 1 - Mathf.Clamp01(info.roughness));
        material.SetFloat("_Surface", 0);
        material.SetFloat("_Blend", 0);
        material.SetFloat("_AlphaClip", 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.One);
        material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        material.SetFloat("_ZWrite", 1);
        material.SetFloat("_Cull", (float)CullMode.Back);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetOverrideTag("RenderType", "Opaque");
        material.SetShaderPassEnabled("ShadowCaster", true);
        material.renderQueue = (int)RenderQueue.Geometry;
        material.doubleSidedGI = false;
        material.enableInstancing = true;
        if (info.key == "light")
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * .7f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        else { material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black); }
    }
    static void AddCollision(GameObject instance, AssetInfo info, ModelResult result)
    {
        if (instance.GetComponentsInChildren<Collider>(true).Length != 0) throw new Exception("Imported model already contains colliders");
        bool hasBoxes = info.collision_boxes != null && info.collision_boxes.Length > 0;
        if (hasBoxes == info.collision_mesh) throw new Exception("Declare either collision_boxes or collision_mesh for each structural asset");
        if (hasBoxes)
        {
            foreach (var box in info.collision_boxes)
            {
                if (box == null || !IsVector(box.center_blender_m) || !IsVector(box.size_blender_m) || box.size_blender_m.Any(v => v <= 0))
                    throw new Exception("Collision proxy requires finite centre and positive metric dimensions");
                var collider = instance.AddComponent<BoxCollider>();
                collider.center = ToUnity(box.center_blender_m);
                collider.size = ToUnity(box.size_blender_m);
                result.collision.Add(new CollisionResult { type = "BoxCollider", path = instance.name, center = collider.center, size = collider.size });
            }
        }
        else
        {
            // Colliders remain on each imported leaf mesh under its hinge; no Rigidbody is added.
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (ExcludedCollisionDetail(renderer.transform, instance.transform)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) throw new Exception("Collision mesh is missing: " + renderer.name);
                var collider = renderer.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                result.collision.Add(new CollisionResult { type = "MeshCollider", path = renderer.name, center = filter.sharedMesh.bounds.center,
                    size = filter.sharedMesh.bounds.size, triangles = MeshTriangles(filter.sharedMesh) });
            }
        }
        result.colliders = instance.GetComponentsInChildren<Collider>(true).Length;
        result.meshColliders = instance.GetComponentsInChildren<MeshCollider>(true).Length;
        result.boxColliders = instance.GetComponentsInChildren<BoxCollider>(true).Length;
        foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            if (ExcludedCollisionDetail(renderer.transform, instance.transform)) result.excludedDetailColliders += renderer.GetComponents<Collider>().Length;
        if (result.colliders == 0 || result.excludedDetailColliders != 0 || (hasBoxes && result.boxColliders != info.collision_boxes.Length))
            throw new Exception("Structural collision configuration is invalid");
    }
    static GameObject CreateExampleRoot(GameObject template, Scene workScene, string name)
    {
        // Instantiation into the preview scene avoids briefly adding a new root to the user's unsaved active scene.
        var root = (GameObject)PrefabUtility.InstantiatePrefab(template, workScene);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        foreach (var component in root.GetComponents<Component>()) if (!(component is Transform)) UnityEngine.Object.DestroyImmediate(component);
        root.name = name;
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        return root;
    }

    [MenuItem("Tools/Modular Asset/Prepare RumahPanggung Assets")]
    public static void Prepare()
    {
        if (busy || EditorApplication.isPlayingOrWillChangePlaymode) return;
        busy = true;
        var report = new Report { checkedUtc = DateTime.UtcNow.ToString("O"), projectPath = Path.GetDirectoryName(Application.dataPath),
            unityVersion = Application.unityVersion, scenesBefore = CaptureScenes() };
        Scene previousScene = SceneManager.GetActiveScene(), workScene = default;
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
            string readyPath = DiskPath(Source + "/export.ready");
            if (!File.Exists(readyPath)) throw new Exception("Blender export is not ready");
            report.exportReadyUtc = File.GetLastWriteTimeUtc(readyPath).ToString("O");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(DiskPath(Folder + "/manifest.json")));
            if (manifest == null || manifest.assets == null || manifest.assets.Length == 0) throw new Exception("Asset manifest is empty");
            if (manifest.materials == null || manifest.materials.Length == 0) throw new Exception("Material manifest is empty");
            if (manifest.assets.Any(info => !SafeName(info.name) || !info.name.StartsWith("MRP_", StringComparison.Ordinal)
                || !SafeName(info.category) || string.IsNullOrEmpty(info.fbx) || !info.fbx.StartsWith(Folder + "/Models/", StringComparison.Ordinal)
                || info.fbx.Contains("..") || !File.Exists(DiskPath(info.fbx)))) throw new Exception("Model names, paths or export are invalid");
            if (manifest.assets.GroupBy(info => info.name).Any(group => group.Count() > 1)) throw new Exception("Asset names are duplicated");
            if (manifest.materials.Any(info => !SafeName(info.name) || !info.name.StartsWith("MRP_", StringComparison.Ordinal))
                || manifest.materials.GroupBy(info => info.name).Any(group => group.Count() > 1)) throw new Exception("Material names are invalid or duplicated");
            report.expectedModels = manifest.assets.Length;
            report.expectedExamples = manifest.examples == null ? 0 : manifest.examples.Length;
            workScene = EditorSceneManager.NewPreviewScene();
            Directory.CreateDirectory(DiskPath(Folder + "/Materials"));
            Directory.CreateDirectory(DiskPath(Folder + "/Prefabs"));
            foreach (string category in manifest.assets.Select(info => info.category).Distinct()) Directory.CreateDirectory(DiskPath(Folder + "/Prefabs/" + category));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new Exception("URP Lit shader was not found");
            string texturesFolder = DiskPath(Folder + "/Textures");
            foreach (string textureDiskPath in Directory.Exists(texturesFolder) ? Directory.GetFiles(texturesFolder, "*.png") : new string[0])
            {
                string path = Folder + "/Textures/" + Path.GetFileName(textureDiskPath);
                currentAsset = path;
                var textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!textureImporter) throw new Exception("No texture importer: " + path);
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.wrapMode = TextureWrapMode.Repeat;
                textureImporter.sRGBTexture = true;
                textureImporter.maxTextureSize = 1024;
                textureImporter.mipmapEnabled = true;
                textureImporter.alphaIsTransparency = false;
                textureImporter.alphaSource = TextureImporterAlphaSource.None;
                textureImporter.SaveAndReimport();
                report.textures.Add(new FileResult { path = path, guid = AssetDatabase.AssetPathToGUID(path), sha256 = FileHash(path) });
            }
            report.textureCount = report.textures.Count;
            var materials = new Dictionary<string, Material>();
            foreach (var info in manifest.materials)
            {
                currentAsset = info.name;
                if (!IsVector(info.srgb) || info.srgb.Any(value => value < 0 || value > 1)) throw new Exception("Material colour must be finite sRGB values from zero to one");
                string path = Folder + "/Materials/" + info.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                ConfigureMaterial(material, shader, info);
                string texturePath = Folder + "/Textures/" + info.name + "_Albedo.png";
                if (File.Exists(DiskPath(texturePath)))
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (!texture) throw new Exception("Texture was not loaded: " + texturePath);
                    material.SetTexture("_BaseMap", texture);
                    material.SetColor("_BaseColor", Color.white);
                }
                EditorUtility.SetDirty(material);
                materials.Add(info.name, material);
            }
            report.materialCount = materials.Count;
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var info in manifest.assets)
            {
                currentAsset = info.name;
                GameObject instance = null;
                try
                {
                    if (!IsVector(info.size_blender_m) || info.size_blender_m.Any(value => value <= 0)
                        || !IsVector(info.bounds_min_blender) || !IsVector(info.bounds_max_blender)) throw new Exception("Metric dimensions or pivot bounds are invalid");
                    var importer = AssetImporter.GetAtPath(info.fbx) as ModelImporter;
                    if (!importer) throw new Exception("No model importer: " + info.fbx);
                    importer.globalScale = 1;
                    importer.useFileScale = true;
                    importer.bakeAxisConversion = true;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.importAnimation = false;
                    importer.animationType = ModelImporterAnimationType.None;
                    importer.importBlendShapes = false;
                    importer.importConstraints = false;
                    importer.importVisibility = false;
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                    importer.materialSearch = ModelImporterMaterialSearch.Local;
                    importer.generateSecondaryUV = true;
                    importer.isReadable = false;
                    importer.preserveHierarchy = true;
                    importer.addCollider = false;
                    foreach (var material in materials) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.Key), material.Value);
                    importer.SaveAndReimport();
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(info.fbx);
                    if (!model) throw new Exception("Model was not loaded: " + info.fbx);
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(model, workScene);
                    // Store complete prefab components while retaining GUID references to the imported model meshes.
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    instance.name = info.name;
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                    var result = new ModelResult { name = info.name, category = info.category, model = info.fbx,
                        modelGuid = AssetDatabase.AssetPathToGUID(info.fbx), renderers = renderers.Length, positiveScale = true,
                        allRenderersEnabled = true, expectedSize = ToUnity(info.size_blender_m), expectedTriangles = info.triangles };
                    result.declaredMovingPivots = info.moving_pivots ?? new string[0];
                    var importedNames = new HashSet<string>(instance.GetComponentsInChildren<Transform>(true).Select(transform => transform.name));
                    result.importedMovingPivots = result.declaredMovingPivots.Where(importedNames.Contains).ToArray();
                    if (result.importedMovingPivots.Length != result.declaredMovingPivots.Length) report.errors.Add(info.name + ": declared moving pivot was not imported");
                    var meshes = new HashSet<Mesh>();
                    Bounds bounds = new Bounds(); bool first = true;
                    foreach (var renderer in renderers)
                    {
                        result.allRenderersEnabled &= renderer.enabled && renderer.gameObject.activeInHierarchy;
                        if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                        foreach (var material in renderer.sharedMaterials)
                            if (!material || !AssetDatabase.GetAssetPath(material).StartsWith(Folder + "/Materials/", StringComparison.Ordinal)
                                || material.shader != shader || material.GetFloat("_Surface") != 0 || material.GetFloat("_Cull") != (float)CullMode.Back) result.missingMaterials++;
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (!filter || !filter.sharedMesh) throw new Exception("Renderer has no mesh: " + renderer.name);
                        Mesh mesh = filter.sharedMesh;
                        if (renderer.sharedMaterials.Length != mesh.subMeshCount) throw new Exception("Material slots differ from submeshes: " + renderer.name);
                        if (!mesh.HasVertexAttribute(VertexAttribute.Normal)) result.meshesMissingNormals++;
                        if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord0)) result.meshesMissingPrimaryUv++;
                        if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord1)) result.meshesMissingLightmapUv++;
                        result.triangles += MeshTriangles(mesh);
                        result.vertices += mesh.vertexCount;
                        meshes.Add(mesh);
                    }
                    result.meshes = meshes.Count;
                    result.withinTriangleBudget = result.triangles <= MaximumTriangles;
                    AddCollision(instance, info, result);
                    result.animators = instance.GetComponentsInChildren<Animator>(true).Length;
                    result.animations = instance.GetComponentsInChildren<Animation>(true).Length;
                    result.skinnedRenderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                    result.lodGroups = instance.GetComponentsInChildren<LODGroup>(true).Length;
                    foreach (var transform in instance.GetComponentsInChildren<Transform>(true)) if (!PositiveScale(transform)) result.positiveScale = false;
                    result.size = bounds.size;
                    result.boundsMin = bounds.min; result.boundsMax = bounds.max;
                    result.sizeDifference = (result.expectedSize - bounds.size).magnitude;
                    result.boundsDifference = Mathf.Max((ToUnity(info.bounds_min_blender) - bounds.min).magnitude, (ToUnity(info.bounds_max_blender) - bounds.max).magnitude);
                    if (result.boundsDifference > BoundsTolerance || result.sizeDifference > BoundsTolerance) report.errors.Add(info.name + ": metric size or pivot bounds exceed " + BoundsTolerance + " m tolerance");
                    if (result.missingMaterials != 0 || !result.positiveScale || !result.allRenderersEnabled || result.renderers == 0 || result.triangles == 0)
                        report.errors.Add(info.name + ": material, positive scale, visibility or geometry issue");
                    if (result.meshesMissingNormals != 0 || result.meshesMissingPrimaryUv != 0 || result.meshesMissingLightmapUv != 0)
                        report.errors.Add(info.name + ": mesh is missing normals, UV0 or generated lightmap UVs");
                    if (!result.withinTriangleBudget || (info.triangles > 0 && info.triangles != result.triangles)) report.errors.Add(info.name + ": triangle count differs from source or exceeds the budget");
                    if (result.animators != 0 || result.animations != 0 || result.skinnedRenderers != 0 || result.lodGroups != 0)
                        report.errors.Add(info.name + ": static house asset imported unexpected animation, skinning or LOD groups");
                    result.prefab = Folder + "/Prefabs/" + info.category + "/" + info.name + ".prefab";
                    var prefab = PrefabUtility.SaveAsPrefabAsset(instance, result.prefab);
                    if (!prefab) throw new Exception("Prefab was not saved: " + result.prefab);
                    result.prefabGuid = AssetDatabase.AssetPathToGUID(result.prefab);
                    result.modelSha256 = FileHash(result.model);
                    result.prefabSha256 = FileHash(result.prefab);
                    prefabs.Add(info.name, prefab);
                    string[] labels = { "ModularAsset", "RumahPanggung", info.category };
                    AssetDatabase.SetLabels(model, labels); AssetDatabase.SetLabels(prefab, labels);
                    report.assets.Add(result); report.models++; report.prefabs++;
                }
                catch (Exception exception) { report.errors.Add(info.name + ": " + exception); }
                finally { if (instance) UnityEngine.Object.DestroyImmediate(instance); }
            }
            foreach (var example in manifest.examples ?? new Example[0])
            {
                currentAsset = example.name;
                GameObject root = null;
                try
                {
                    if (!SafeName(example.name)) throw new Exception("Invalid example name");
                    if (prefabs.Count == 0) throw new Exception("No component prefab is available for example construction");
                    root = CreateExampleRoot(prefabs.Values.First(), workScene, example.name);
                    foreach (var placement in example.placements ?? new Placement[0])
                    {
                        if (!prefabs.TryGetValue(placement.asset, out var prefab)) throw new Exception("Missing example asset " + placement.asset);
                        if (!IsVector(placement.position_blender) || float.IsNaN(placement.rotation_z) || float.IsInfinity(placement.rotation_z)) throw new Exception("Invalid example transform for " + placement.asset);
                        var child = (GameObject)PrefabUtility.InstantiatePrefab(prefab, workScene);
                        child.transform.SetParent(root.transform, false);
                        child.transform.localPosition = ToUnity(placement.position_blender);
                        child.transform.localRotation = Quaternion.Euler(0, -placement.rotation_z * Mathf.Rad2Deg, 0);
                    }
                    string path = Folder + "/Prefabs/" + example.name + ".prefab";
                    var examplePrefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                    if (!examplePrefab) throw new Exception("Example prefab was not saved: " + path);
                    AssetDatabase.SetLabels(examplePrefab, new[] { "ModularAsset", "RumahPanggung", "Example" });
                    report.examples.Add(path);
                    report.exampleFiles.Add(new FileResult { path = path, guid = AssetDatabase.AssetPathToGUID(path), sha256 = FileHash(path) });
                }
                catch (Exception exception) { report.errors.Add(example.name + ": " + exception); }
                finally { if (root) UnityEngine.Object.DestroyImmediate(root); }
            }
            currentAsset = "Saving";
            foreach (var material in materials.Values) AssetDatabase.SaveAssetIfDirty(material);
            foreach (var material in materials.Values)
            {
                string path = AssetDatabase.GetAssetPath(material);
                report.materials.Add(new FileResult { path = path, guid = AssetDatabase.AssetPathToGUID(path), sha256 = FileHash(path) });
            }
            if (report.exportReadyUtc != File.GetLastWriteTimeUtc(readyPath).ToString("O")) report.errors.Add("Blender export changed during native import; run the importer again");
            report.pass = report.errors.Count == 0 && report.models == report.expectedModels && report.prefabs == report.expectedModels
                && report.examples.Count == report.expectedExamples;
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); report.pass = false; }
        finally
        {
            Application.logMessageReceived -= capture;
            if (workScene.IsValid() && workScene.isLoaded) EditorSceneManager.ClosePreviewScene(workScene);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            report.scenesAfter = CaptureScenes();
            report.preservedEditorScenes = SameScenes(report.scenesBefore, report.scenesAfter) && SceneManager.GetActiveScene().handle == previousScene.handle;
            if (!report.preservedEditorScenes) { report.errors.Add("Loaded editor scene state changed during preparation"); report.pass = false; }
            busy = false;
            report.completedUtc = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory(DiskPath(Source));
            File.WriteAllText(DiskPath(Source + ReportName), JsonUtility.ToJson(report, true));
            if (report.pass) Debug.Log("ModularAsset RumahPanggung: " + report.models + " models, " + report.prefabs + " component prefabs, " + report.examples.Count + " examples; pass=True");
            else Debug.LogError("ModularAsset RumahPanggung import failed: " + report.errors.Count + " errors; see " + Source + ReportName);
        }
    }
}
