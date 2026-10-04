using System;
using System.Collections.Generic;
using System.Linq;
using SaksiTerakhir.Vehicles;
using UnityEditor;
using UnityEngine;

namespace SaksiTerakhir.EditorTools
{
    [InitializeOnLoad]
    public static class GSX125ParkedPrefabSetup
    {
        private const string ModelPath = "Assets/_Project/Art/Models/GSX_S_125.fbx";
        private const string PrefabPath = "Assets/_Project/Prefabs/Motor GSX S 125.prefab";

        static GSX125ParkedPrefabSetup()
        {
            EditorApplication.delayCall += CreatePrefabIfMissing;
        }

        [MenuItem("Saksi Terakhir/Create GSX S 125 Parked Prefab")]
        public static void CreateParkedPrefab()
        {
            CreatePrefab(overwrite: true);
        }

        internal static void CreatePrefabIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                CreatePrefab(overwrite: false);
            }
        }

        private static void CreatePrefab(bool overwrite)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                return;
            }

            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                return;
            }

            EnsurePrefabFolder();
            var temporaryRoot = new GameObject("Motor GSX S 125");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "Model GSX S 125";
                model.transform.SetParent(temporaryRoot.transform, false);
                temporaryRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var renderers = model.GetComponentsInChildren<Renderer>(true);
                var frontWheel = FindRenderer(renderers, "Pelek Roda Depan");
                var rearWheel = FindRenderer(renderers, "Pelek Roda Belakang");
                var leftFoot = FindRenderer(renderers, "Tapak Standar Tengah Kiri");
                var rightFoot = FindRenderer(renderers, "Tapak Standar Tengah Kanan");
                var stand = FindTransform(model.transform, "Standar Tengah");

                var supports = new List<Collider>
                {
                    CreateSupport(temporaryRoot.transform, "Collider Parkir Roda Depan", frontWheel, wheel: true),
                    CreateSupport(temporaryRoot.transform, "Collider Parkir Roda Belakang", rearWheel, wheel: true),
                    CreateSupport(temporaryRoot.transform, "Collider Parkir Standar Kiri", leftFoot, wheel: false),
                    CreateSupport(temporaryRoot.transform, "Collider Parkir Standar Kanan", rightFoot, wheel: false)
                };

                var body = temporaryRoot.AddComponent<Rigidbody>();
                body.mass = 140f;
                body.useGravity = true;
                body.isKinematic = false;
                body.linearDamping = 0.05f;
                body.angularDamping = 0.8f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var modelBounds = GetCombinedLocalBounds(temporaryRoot.transform, renderers);
                var frontBounds = GetLocalBounds(temporaryRoot.transform, frontWheel);
                var rearBounds = GetLocalBounds(temporaryRoot.transform, rearWheel);
                body.centerOfMass = new Vector3(
                    modelBounds.center.x,
                    modelBounds.min.y + modelBounds.size.y * 0.38f,
                    (frontBounds.center.z + rearBounds.center.z) * 0.5f);

                var parking = temporaryRoot.AddComponent<MotorcycleParkingSupport>();
                parking.Configure(stand.gameObject, supports.ToArray(), startParked: true);

                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(temporaryRoot, PrefabPath, out var success);
                if (!success || savedPrefab == null)
                {
                    throw new InvalidOperationException("Unity gagal menyimpan prefab motor yang sedang parkir.");
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"Prefab motor parkir dibuat: {PrefabPath}. Standar dan empat collider aktif saat parkir.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporaryRoot);
            }
        }

        private static Renderer FindRenderer(IEnumerable<Renderer> renderers, string partName)
        {
            var renderer = renderers.FirstOrDefault(candidate =>
                candidate.gameObject.name == partName || candidate.name == partName);
            if (renderer == null)
            {
                throw new InvalidOperationException($"Bagian model '{partName}' tidak ditemukan.");
            }

            return renderer;
        }

        private static Transform FindTransform(Transform root, string objectName)
        {
            var match = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == objectName);
            if (match == null)
            {
                throw new InvalidOperationException($"Objek model '{objectName}' tidak ditemukan.");
            }

            return match;
        }

        private static BoxCollider CreateSupport(Transform root, string objectName, Renderer reference,
            bool wheel)
        {
            var bounds = GetLocalBounds(root, reference);
            var colliderObject = new GameObject(objectName);
            colliderObject.transform.SetParent(root, false);

            var collider = colliderObject.AddComponent<BoxCollider>();
            var supportX = wheel
                ? Mathf.Max(0.09f, bounds.size.x * 1.2f)
                : Mathf.Max(0.08f, bounds.size.x + 0.02f);
            var supportZ = wheel
                ? Mathf.Clamp(bounds.size.z * 0.28f, 0.10f, 0.18f)
                : Mathf.Max(0.08f, bounds.size.z + 0.02f);

            collider.center = new Vector3(bounds.center.x, bounds.min.y - 0.011f, bounds.center.z);
            collider.size = new Vector3(supportX, 0.024f, supportZ);
            return collider;
        }

        private static Bounds GetCombinedLocalBounds(Transform root, IEnumerable<Renderer> renderers)
        {
            var found = false;
            var combined = new Bounds();
            foreach (var renderer in renderers)
            {
                var local = GetLocalBounds(root, renderer);
                if (!found)
                {
                    combined = local;
                    found = true;
                }
                else
                {
                    combined.Encapsulate(local);
                }
            }

            if (!found)
            {
                throw new InvalidOperationException("Model motor tidak memiliki Renderer.");
            }

            return combined;
        }

        private static Bounds GetLocalBounds(Transform root, Renderer renderer)
        {
            var world = renderer.bounds;
            var found = false;
            var local = new Bounds();
            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            for (var z = 0; z < 2; z++)
            {
                var corner = new Vector3(
                    x == 0 ? world.min.x : world.max.x,
                    y == 0 ? world.min.y : world.max.y,
                    z == 0 ? world.min.z : world.max.z);
                var point = root.InverseTransformPoint(corner);
                if (!found)
                {
                    local = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                {
                    local.Encapsulate(point);
                }
            }

            return local;
        }

        private static void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            }
        }
    }

    public sealed class GSX125PrefabImportHook : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets.Any(path => path == "Assets/_Project/Art/Models/GSX_S_125.fbx"))
            {
                EditorApplication.delayCall += GSX125ParkedPrefabSetup.CreatePrefabIfMissing;
            }
        }
    }
}
