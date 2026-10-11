using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SaksiTerakhir.Editor
{
    public static class InventoryItemAssetSetup
    {
        public const string CatalogPath = "Assets/_Project/Resources/ItemDefinitionCatalog.asset";
        public const string PickupFolder = "Assets/_Project/Prefabs/Inventory/Pickups";
        private const string DefinitionFolder = "Assets/_Project/Items";
        private const int PickupLayer = 6;
        private static readonly string[] DefinitionNames =
        {
            "Kunci_Mobil_Keyless", "Baterai_LiIon_18650", "Buku_Agenda_A5", "Kotak_P3K",
            "Nasi_Uduk_Bungkus", "Power_Bank", "Senter", "Tali_Nilon_10m", "Tali_Tambang_10m"
        };

        [MenuItem("Saksi Terakhir/Inventory/Buat Catalog dan Prefab Pickup")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Generate inventory assets while the Editor is stopped.");
            ItemDefinition[] definitions = LoadDefinitions();
            Object existingCatalog = AssetDatabase.LoadMainAssetAtPath(CatalogPath);
            if (existingCatalog != null && !(existingCatalog is ItemDefinitionCatalog))
                throw new InvalidOperationException("Catalog destination already contains another asset type.");
            EnsureFolder(Path.GetDirectoryName(CatalogPath)?.Replace('\\', '/'));
            EnsureFolder(PickupFolder);

            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (ItemDefinition definition in definitions) CreatePickup(definition, preview);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }

            ItemDefinitionCatalog catalog = existingCatalog as ItemDefinitionCatalog;
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemDefinitionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            // Pickup regeneration must retain story documents and later additions.
            ItemDefinition[] catalogDefinitions = definitions.Concat(catalog.Definitions)
                .Where(definition => definition != null).Distinct().ToArray();
            var serialized = new SerializedObject(catalog);
            SerializedProperty list = serialized.FindProperty("definitions");
            list.arraySize = catalogDefinitions.Length;
            for (int index = 0; index < catalogDefinitions.Length; index++)
                list.GetArrayElementAtIndex(index).objectReferenceValue = catalogDefinitions[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(Validate());
        }

        [MenuItem("Saksi Terakhir/Inventory/Periksa Catalog dan Prefab Pickup")]
        public static void ValidateGeneratedAssets() => Debug.Log(Validate());

        public static string Validate()
        {
            ItemDefinition[] definitions = LoadDefinitions();
            ItemDefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(CatalogPath);
            Require(catalog != null, "Inventory catalog was not imported at " + CatalogPath);
            var report = new StringBuilder("Inventory assets validated\n");
            report.AppendLine("Catalog: " + CatalogPath + " (" + catalog.Definitions.Count + " definitions)");
            for (int index = 0; index < definitions.Length; index++)
            {
                ItemDefinition definition = definitions[index];
                Require(catalog.Definitions.Contains(definition) && catalog.Find(definition.Id) == definition,
                    "Catalog reference mismatch for " + definition.name);
                string path = GetPickupPath(definition);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(prefab != null, "Pickup prefab was not imported at " + path);
                ItemPickupInteractable pickup = prefab.GetComponent<ItemPickupInteractable>();
                Require(pickup != null && pickup.enabled && pickup.Definition == definition,
                    "Pickup definition/component mismatch at " + path);
                var serialized = new SerializedObject(pickup);
                Require(string.IsNullOrEmpty(serialized.FindProperty("instanceId").stringValue)
                    && !pickup.RuntimeSpawned && !pickup.Collected,
                    "Native pickup must start without a runtime identity or collection state: " + path);
                Require(prefab.activeSelf, "Pickup prefab must be active when placed: " + path);
                Require(prefab.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == PickupLayer),
                    "Pickup layer mismatch at " + path);
                Collider[] activeColliders = prefab.GetComponentsInChildren<Collider>(true)
                    .Where(c => c.enabled).ToArray();
                BoxCollider shape = prefab.GetComponent<BoxCollider>();
                Require(shape != null && shape.enabled && !shape.isTrigger
                    && activeColliders.Length == 1 && activeColliders[0] == shape,
                    "Pickup requires exactly one active root BoxCollider: " + path);
                Rigidbody body = prefab.GetComponent<Rigidbody>();
                Require(body != null && body.isKinematic && !body.useGravity && body.detectCollisions,
                    "Placed pickup must use a stationary root Rigidbody: " + path);
                Require(prefab.GetComponentsInChildren<Rigidbody>(true).All(b => b == body
                    || (b.isKinematic && !b.useGravity && !b.detectCollisions)),
                    "Source model physics is still active at " + path);
                Require(prefab.GetComponentsInChildren<Interactable>(true).All(i => i == pickup || !i.enabled),
                    "Source model interaction is still active at " + path);
                Require(prefab.GetComponentsInChildren<Renderer>(true).Length
                    == definition.ModelPrefab.GetComponentsInChildren<Renderer>(true).Length,
                    "Pickup does not contain the full source model: " + path);
                Require(TryGetLocalBounds(prefab.transform, out Bounds bounds),
                    "Pickup has no renderable model bounds: " + path);
                Vector3 rootScale = prefab.transform.lossyScale;
                rootScale = new Vector3(Mathf.Abs(rootScale.x), Mathf.Abs(rootScale.y), Mathf.Abs(rootScale.z));
                Require(IsFinite(rootScale) && rootScale.x > 0f && rootScale.y > 0f && rootScale.z > 0f,
                    "Pickup root has an invalid imported scale: " + path);
                Vector3 tolerance = new Vector3(0.001f / rootScale.x, 0.001f / rootScale.y, 0.001f / rootScale.z);
                Require(shape.size.x > 0f && shape.size.y > 0f && shape.size.z > 0f
                    && ContainsBounds(new Bounds(shape.center, shape.size + tolerance), bounds),
                    "Root collider does not contain all model renderers: " + path);
                report.AppendLine(definition.Id + " | " + path);
                report.AppendLine("  definition=" + DescribeReference(definition)
                    + " icon=" + DescribeReference(definition.Icon)
                    + " model=" + DescribeReference(definition.ModelPrefab));
                report.AppendLine("  renderers=" + prefab.GetComponentsInChildren<Renderer>(true).Length
                    + " local collider size=" + shape.size.ToString("F6")
                    + " world collider size=" + Vector3.Scale(shape.size, rootScale).ToString("F4"));
            }
            return report.ToString();
        }

        private static ItemDefinition[] LoadDefinitions()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var definitions = new ItemDefinition[DefinitionNames.Length];
            for (int index = 0; index < DefinitionNames.Length; index++)
            {
                string path = DefinitionFolder + "/" + DefinitionNames[index] + ".asset";
                ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                Require(definition != null, "Missing ItemDefinition at " + path);
                Require(!string.IsNullOrWhiteSpace(definition.Id) && ids.Add(definition.Id),
                    "Missing or duplicate inventory ID at " + path);
                Require(!definition.IsDocument && definition.Icon != null && definition.ModelPrefab != null,
                    "Pickup definition must have an imported icon and model: " + path);
                Require(AssetDatabase.Contains(definition.Icon) && AssetDatabase.Contains(definition.ModelPrefab),
                    "Pickup icon and model must reference saved source assets: " + path);
                definitions[index] = definition;
            }
            return definitions;
        }

        private static void CreatePickup(ItemDefinition definition, Scene preview)
        {
            GameObject root = null;
            try
            {
                root = Object.Instantiate(definition.ModelPrefab, new InstantiateParameters { scene = preview });
                root.SetActive(false);
                root.name = "Pickup_" + definition.name;
                root.transform.position = Vector3.zero;
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (Rigidbody sourceBody in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    sourceBody.isKinematic = true;
                    sourceBody.useGravity = false;
                    sourceBody.detectCollisions = false;
                }
                foreach (Interactable interactable in root.GetComponentsInChildren<Interactable>(true))
                    interactable.enabled = false;
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = PickupLayer;
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                }
                Require(TryGetLocalBounds(root.transform, out Bounds bounds),
                    "Cannot build collider for " + definition.name);
                ConfigureCollider(root, bounds);
                Rigidbody body = root.GetComponent<Rigidbody>();
                if (body == null) body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = true;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                ItemPickupInteractable pickup = root.GetComponent<ItemPickupInteractable>();
                if (pickup == null) pickup = root.AddComponent<ItemPickupInteractable>();
                pickup.enabled = true;
                pickup.Configure(definition, string.Empty, false);
                root.SetActive(true);
                string path = GetPickupPath(definition);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                Require(success && saved != null, "Failed to save pickup prefab at " + path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        private static BoxCollider ConfigureCollider(GameObject root, Bounds bounds)
        {
            Vector3 scale = root.transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Require(IsFinite(scale) && scale.x > 0f && scale.y > 0f && scale.z > 0f,
                "Pickup model root must have a finite, nonzero scale.");
            BoxCollider shape = root.GetComponent<BoxCollider>();
            if (shape == null) shape = root.AddComponent<BoxCollider>();
            shape.center = bounds.center;
            // FBX import roots can scale tiny mesh-local units by 100. The minimum is 2.5 cm in the world.
            shape.size = new Vector3(Mathf.Max(0.025f / scale.x, bounds.size.x),
                Mathf.Max(0.025f / scale.y, bounds.size.y), Mathf.Max(0.025f / scale.z, bounds.size.z));
            shape.isTrigger = false;
            shape.enabled = true;
            return shape;
        }

        private static bool TryGetLocalBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                Bounds local = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    point = root.InverseTransformPoint(renderer.transform.TransformPoint(point));
                    if (!IsFinite(point)) return false;
                    if (found) bounds.Encapsulate(point);
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                }
            }
            return found && (bounds.size.x > 0f || bounds.size.y > 0f || bounds.size.z > 0f);
        }

        private static bool IsFinite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static bool ContainsBounds(Bounds outer, Bounds inner)
            => outer.Contains(inner.min) && outer.Contains(inner.max);

        private static string DescribeReference(Object asset)
        {
            Require(asset != null, "Unresolved imported asset reference.");
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,
                out string guid, out long localId), "Unresolved imported asset reference.");
            return AssetDatabase.GetAssetPath(asset) + " [" + guid + ":" + localId + "]";
        }

        private static string GetPickupPath(ItemDefinition definition)
            => PickupFolder + "/Pickup_" + definition.name + ".prefab";

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))),
                "Could not create inventory asset folder " + path);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
