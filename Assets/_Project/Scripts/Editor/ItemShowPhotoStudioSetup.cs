using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.Editor
{
    /// <summary>
    /// Builds the ItemShow scene as a reusable, one-item-at-a-time photo studio.
    /// All presentation changes stay in ItemShow and its own material folder.
    /// </summary>
    public static class ItemShowPhotoStudioSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/ItemShow.unity";
        private const string ModelFolder = "Assets/_Project/Art/Models/Props/Inventory";
        private const string MaterialFolder = "Assets/_Project/Art/Scenes/ItemShow/Materials";
        private const string BeamMeshPath = MaterialFolder + "/ItemShow_FlashlightBeamMesh.asset";

        private sealed class ItemSpec
        {
            public string FileName;
            public string DisplayName;
            public bool ActiveOnOpen;
            public bool PointFlashlightForward;

            public ItemSpec(string fileName, string displayName, bool activeOnOpen = false, bool pointFlashlightForward = false)
            {
                FileName = fileName;
                DisplayName = displayName;
                ActiveOnOpen = activeOnOpen;
                PointFlashlightForward = pointFlashlightForward;
            }
        }

        private static readonly ItemSpec[] Items =
        {
            new ItemSpec("Flashlight.fbx", "Senter", true, true),
            new ItemSpec("Baterai_LiIon_18650.fbx", "Baterai Li-Ion 18650"),
            new ItemSpec("Buku_Agenda_A5_80Lembar.fbx", "Buku Agenda A5 Tertutup"),
            new ItemSpec("Buku_Agenda_A5_Terbuka.fbx", "Buku Agenda A5 Terbuka"),
            new ItemSpec("Kotak_P3K.fbx", "Kotak P3K"),
            new ItemSpec("Nasi_Uduk_Bungkus.fbx", "Nasi Uduk Bungkus"),
            new ItemSpec("Power_Bank_USB_A_to_C.fbx", "Power Bank USB A ke C"),
            new ItemSpec("Remote_Keyless_Separated_Parts.fbx", "Remote Keyless Terpisah"),
            new ItemSpec("Tali_Nilon_10m.fbx", "Tali Nilon 10 m"),
            new ItemSpec("Tali_Tambang_10m.fbx", "Tali Tambang 10 m")
        };

        [MenuItem("Saksi Terakhir/ItemShow/Buat Ulang Studio Foto")]
        private static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Keluar dari Play Mode sebelum membangun studio ItemShow.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!EditorUtility.DisplayDialog(
                    "Buat ulang ItemShow",
                    "Scene ItemShow akan diganti dengan studio foto item. Scene lain tidak diubah.",
                    "Buat ulang",
                    "Batal"))
                return;

            BuildScene();
        }

        // Used by Unity's one-shot batch Editor to generate and validate the saved scene.
        public static void BuildBatch()
        {
            try
            {
                BuildScene();
                Debug.Log("ITEMSHOW_BUILD_RESULT: " + ValidateScene());
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildScene()
        {
            if (!System.IO.File.Exists(ScenePath))
                throw new InvalidOperationException("Scene ItemShow tidak ditemukan: " + ScenePath);

            EnsureFolder("Assets/_Project/Art/Scenes");
            EnsureFolder("Assets/_Project/Art/Scenes/ItemShow");
            EnsureFolder(MaterialFolder);
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
                UnityEngine.Object.DestroyImmediate(root);

            Material floorMaterial = CreateMaterial(
                MaterialFolder + "/ItemShow_Floor.mat", new Color(0.035f, 0.048f, 0.065f), 0.18f, 0.48f);
            Material backdropMaterial = CreateMaterial(
                MaterialFolder + "/ItemShow_Backdrop.mat", new Color(0.022f, 0.032f, 0.046f), 0.05f, 0.35f);
            Material podiumMaterial = CreateMaterial(
                MaterialFolder + "/ItemShow_Podium.mat", new Color(0.075f, 0.09f, 0.11f), 0.62f, 0.72f);
            Material trimMaterial = CreateMaterial(
                MaterialFolder + "/ItemShow_BrassTrim.mat", new Color(0.42f, 0.24f, 0.075f), 0.78f, 0.82f);
            Material beamMaterial = CreateBeamMaterial(MaterialFolder + "/ItemShow_FlashlightBeam.mat");

            ConfigureAtmosphere();
            GameObject setRoot = new GameObject("ItemShow Studio");
            GameObject lightingRoot = new GameObject("Studio Lighting");
            GameObject itemRoot = new GameObject("Preview Items - aktifkan satu item untuk dipotret");

            CreateBackdrop(setRoot.transform, floorMaterial, backdropMaterial);
            CreatePedestal(setRoot.transform, podiumMaterial, trimMaterial);
            CreateCamera(lightingRoot.transform);
            CreateStudioLights(lightingRoot.transform);
            CreateItemModels(itemRoot.transform, beamMaterial);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Unity gagal menyimpan scene ItemShow.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ItemShow studio foto dibangun: 10 model, satu model aktif, senter menyala.");
        }

        private static void ConfigureAtmosphere()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.19f, 0.23f);
            RenderSettings.ambientEquatorColor = new Color(0.075f, 0.095f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.025f, 0.03f, 0.04f);
            RenderSettings.ambientIntensity = 0.52f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.025f, 0.034f, 0.047f);
            RenderSettings.fogStartDistance = 6f;
            RenderSettings.fogEndDistance = 18f;
            RenderSettings.reflectionIntensity = 0.35f;
        }

        private static void CreateBackdrop(Transform parent, Material floorMaterial, Material backdropMaterial)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Lantai studio";
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(0f, -0.025f, 0f);
            floor.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
            RemoveCollider(floor);

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Latar studio tanpa sambungan";
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = new Vector3(0f, 2.6f, 2.65f);
            wall.transform.localScale = new Vector3(12f, 5.25f, 0.12f);
            wall.GetComponent<MeshRenderer>().sharedMaterial = backdropMaterial;
            RemoveCollider(wall);
        }

        private static void CreatePedestal(Transform parent, Material podiumMaterial, Material trimMaterial)
        {
            GameObject baseCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseCylinder.name = "Panggung bundar";
            baseCylinder.transform.SetParent(parent, false);
            baseCylinder.transform.localPosition = new Vector3(0f, 0.075f, 0f);
            baseCylinder.transform.localScale = new Vector3(0.86f, 0.075f, 0.86f);
            baseCylinder.GetComponent<MeshRenderer>().sharedMaterial = podiumMaterial;
            RemoveCollider(baseCylinder);

            GameObject trim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trim.name = "Garis kuningan tipis";
            trim.transform.SetParent(parent, false);
            trim.transform.localPosition = new Vector3(0f, 0.145f, 0f);
            trim.transform.localScale = new Vector3(0.84f, 0.009f, 0.84f);
            trim.GetComponent<MeshRenderer>().sharedMaterial = trimMaterial;
            RemoveCollider(trim);

            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            top.name = "Permukaan panggung";
            top.transform.SetParent(parent, false);
            top.transform.localPosition = new Vector3(0f, 0.157f, 0f);
            top.transform.localScale = new Vector3(0.81f, 0.012f, 0.81f);
            top.GetComponent<MeshRenderer>().sharedMaterial = podiumMaterial;
            RemoveCollider(top);
        }

        private static void CreateCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("ItemShow Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            Vector3 target = new Vector3(0f, 0.68f, 0.05f);
            cameraObject.transform.position = new Vector3(2.25f, 1.72f, -4.2f);
            cameraObject.transform.rotation = Quaternion.LookRotation(target - cameraObject.transform.position, Vector3.up);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.026f, 0.038f);
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 40f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            cameraObject.AddComponent<AudioListener>();
        }

        private static void CreateStudioLights(Transform parent)
        {
            CreateSpot(parent, "Key - soft warm", new Vector3(-2.6f, 3.5f, -2.0f), new Vector3(0f, 0.55f, 0f),
                new Color(1f, 0.82f, 0.64f), 5.5f, 66f, 8f, true);
            CreateSpot(parent, "Fill - cool front", new Vector3(2.4f, 2.3f, -2.6f), new Vector3(0f, 0.58f, 0f),
                new Color(0.58f, 0.73f, 1f), 2.25f, 72f, 7f, false);
            CreateSpot(parent, "Rim - cool edge", new Vector3(0.8f, 3.2f, 1.55f), new Vector3(0f, 0.55f, 0f),
                new Color(0.52f, 0.72f, 1f), 4.25f, 58f, 7f, true);
        }

        private static Light CreateSpot(Transform parent, string name, Vector3 position, Vector3 target,
            Color color, float intensity, float angle, float range, bool softShadows)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = position;
            lightObject.transform.rotation = Quaternion.LookRotation(target - position, Vector3.up);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.spotAngle = angle;
            light.range = range;
            light.shadows = softShadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.55f;
            light.renderMode = LightRenderMode.ForcePixel;
            return light;
        }

        private static void CreateItemModels(Transform parent, Material beamMaterial)
        {
            int found = 0;
            foreach (ItemSpec item in Items)
            {
                string assetPath = ModelFolder + "/" + item.FileName;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (model == null)
                    throw new InvalidOperationException("Model item tidak ditemukan atau gagal diimpor: " + assetPath);

                GameObject instance = PrefabUtility.InstantiatePrefab(model, parent) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException("Gagal membuat instance model: " + assetPath);

                instance.name = item.DisplayName + (item.ActiveOnOpen ? " - AKTIF" : " - nonaktifkan untuk memotret");
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = item.PointFlashlightForward
                    ? Quaternion.FromToRotation(Vector3.up, new Vector3(-0.42f, 0.09f, -0.90f).normalized)
                    : Quaternion.Euler(0f, 22f, 0f);
                instance.transform.localScale = Vector3.one;

                Bounds localBounds = CalculateRootLocalBounds(instance);
                float largestExtent = Mathf.Max(localBounds.size.x, localBounds.size.y, localBounds.size.z);
                if (largestExtent < 0.0001f)
                    throw new InvalidOperationException("Ukuran model tidak terbaca: " + assetPath);

                const float targetSize = 0.96f;
                float fitScale = targetSize / largestExtent;
                instance.transform.localScale = Vector3.one * fitScale;

                Bounds worldBounds = CalculateWorldBounds(instance);
                Vector3 alignment = new Vector3(
                    -worldBounds.center.x,
                    0.17f - worldBounds.min.y,
                    -worldBounds.center.z);
                instance.transform.position += alignment;

                if (item.PointFlashlightForward)
                    ConfigureFlashlight(instance, localBounds, fitScale, beamMaterial);

                instance.SetActive(item.ActiveOnOpen);
                found++;
            }

            if (found != Items.Length)
                throw new InvalidOperationException("Jumlah model item tidak sesuai. Ditemukan " + found + ".");
        }

        private static void ConfigureFlashlight(GameObject flashlight, Bounds localBounds, float fitScale, Material beamMaterial)
        {
            Vector3 lensLocalPosition = new Vector3(localBounds.center.x, localBounds.max.y, localBounds.center.z);
            Material emissiveLens = CreateEmissiveLensMaterial();

            foreach (Renderer renderer in flashlight.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].name.IndexOf("Lens", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        materials[i] = emissiveLens;
                        changed = true;
                    }
                }
                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                }
            }

            Transform lightAnchor = new GameObject("Cahaya dari lensa").transform;
            lightAnchor.SetParent(flashlight.transform, false);
            lightAnchor.localPosition = lensLocalPosition;
            lightAnchor.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            Light beamLight = lightAnchor.gameObject.AddComponent<Light>();
            beamLight.type = LightType.Spot;
            beamLight.color = new Color(1f, 0.79f, 0.46f);
            beamLight.intensity = 11f;
            beamLight.range = 3.2f;
            beamLight.spotAngle = 27f;
            beamLight.innerSpotAngle = 13f;
            beamLight.shadows = LightShadows.None;
            beamLight.renderMode = LightRenderMode.ForcePixel;

            GameObject glowObject = new GameObject("Pijar halus lensa");
            glowObject.transform.SetParent(lightAnchor, false);
            glowObject.transform.localPosition = Vector3.zero;
            Light glow = glowObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.68f, 0.32f);
            glow.intensity = 1.6f;
            glow.range = 0.65f;
            glow.shadows = LightShadows.None;

            GameObject visibleBeam = new GameObject("Berkas cahaya tipis");
            visibleBeam.transform.SetParent(lightAnchor, false);
            visibleBeam.transform.localPosition = Vector3.zero;
            visibleBeam.transform.localRotation = Quaternion.identity;
            MeshFilter filter = visibleBeam.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateOrLoadBeamMesh();
            MeshRenderer beamRenderer = visibleBeam.AddComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = beamMaterial;
            beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;
            beamRenderer.lightProbeUsage = LightProbeUsage.Off;
            beamRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Material CreateEmissiveLensMaterial()
        {
            const string path = MaterialFolder + "/ItemShow_FlashlightLens_Emissive.mat";
            Material lens = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (lens == null)
            {
                Material source = FindFlashlightLensMaterial();
                Shader shader = source != null ? source.shader : FindLitShader();
                lens = source != null ? new Material(source) : new Material(shader);
                lens.name = "ItemShow_FlashlightLens_Emissive";
                AssetDatabase.CreateAsset(lens, path);
            }

            if (lens.HasProperty("_BaseColor")) lens.SetColor("_BaseColor", new Color(1f, 0.83f, 0.56f, 1f));
            if (lens.HasProperty("_Color")) lens.SetColor("_Color", new Color(1f, 0.83f, 0.56f, 1f));
            if (lens.HasProperty("_Surface")) lens.SetFloat("_Surface", 0f);
            if (lens.HasProperty("_Metallic")) lens.SetFloat("_Metallic", 0.05f);
            if (lens.HasProperty("_Smoothness")) lens.SetFloat("_Smoothness", 0.7f);
            lens.EnableKeyword("_EMISSION");
            if (lens.HasProperty("_EmissionColor")) lens.SetColor("_EmissionColor", new Color(1f, 0.58f, 0.22f) * 5f);
            lens.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            lens.renderQueue = (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(lens);
            return lens;
        }

        private static Material FindFlashlightLensMaterial()
        {
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { ModelFolder + "/Materials" });
            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && material.name.IndexOf("Flashlight_Lens", StringComparison.OrdinalIgnoreCase) >= 0)
                    return material;
            }
            return null;
        }

        private static Material CreateMaterial(string path, Color color, float metallic, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(FindLitShader());
                material.name = System.IO.Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateBeamMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(FindLitShader());
                material.name = "ItemShow_FlashlightBeam";
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(1f, 0.75f, 0.34f, 0.17f));
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(1f, 0.75f, 0.34f, 0.17f));
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh CreateOrLoadBeamMesh()
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(BeamMeshPath);
            if (existing != null) return existing;

            const int segments = 32;
            List<Vector3> vertices = new List<Vector3>(segments * 2 + 2);
            List<int> triangles = new List<int>(segments * 12);
            vertices.Add(Vector3.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * 0.46f, Mathf.Sin(angle) * 0.46f, 2.4f));
            }

            for (int i = 0; i < segments; i++)
            {
                int current = i + 1;
                int next = (i + 1) % segments + 1;
                triangles.Add(0); triangles.Add(current); triangles.Add(next);
                triangles.Add(0); triangles.Add(next); triangles.Add(current);
            }

            Mesh mesh = new Mesh { name = "ItemShow_FlashlightBeamMesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, BeamMeshPath);
            return mesh;
        }

        private static Bounds CalculateRootLocalBounds(GameObject root)
        {
            bool initialized = false;
            Bounds combined = new Bounds();
            Matrix4x4 worldToRoot = root.transform.worldToLocalMatrix;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds local = renderer.localBounds;
                Matrix4x4 rendererToRoot = worldToRoot * renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z);
                    point = rendererToRoot.MultiplyPoint3x4(point);
                    if (!initialized)
                    {
                        combined = new Bounds(point, Vector3.zero);
                        initialized = true;
                    }
                    else combined.Encapsulate(point);
                }
            }

            if (!initialized)
                throw new InvalidOperationException("Model tidak memiliki renderer: " + root.name);
            return combined;
        }

        private static Bounds CalculateWorldBounds(GameObject root)
        {
            bool initialized = false;
            Bounds combined = new Bounds();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (!initialized)
                {
                    combined = renderer.bounds;
                    initialized = true;
                }
                else combined.Encapsulate(renderer.bounds);
            }
            if (!initialized)
                throw new InvalidOperationException("Model tidak memiliki renderer aktif: " + root.name);
            return combined;
        }

        private static Shader FindLitShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Shader Lit tidak ditemukan.");
            return shader;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        }

        private static string ValidateScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Scene ItemShow tidak aktif setelah penyimpanan.");

            int modelCount = 0;
            int activeModelCount = 0;
            int cameraCount = 0;
            int lightCount = 0;
            bool lensEmissive = false;
            bool beamMeshPresent = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) cameraCount++;
                foreach (Light light in root.GetComponentsInChildren<Light>(true)) lightCount++;
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Berkas cahaya tipis" && child.GetComponent<MeshRenderer>() != null)
                        beamMeshPresent = true;
                    if (child.name.EndsWith(" - AKTIF", StringComparison.Ordinal))
                    {
                        modelCount++;
                        if (child.gameObject.activeInHierarchy) activeModelCount++;
                    }
                    else if (child.parent != null && child.parent.name.StartsWith("Preview Items", StringComparison.Ordinal))
                    {
                        if (child.name.Contains(" - nonaktifkan untuk memotret")) modelCount++;
                        if (child.gameObject.activeInHierarchy) activeModelCount++;
                    }
                    foreach (Renderer renderer in child.GetComponents<Renderer>())
                    {
                        foreach (Material material in renderer.sharedMaterials)
                        {
                            if (material != null && material.name == "ItemShow_FlashlightLens_Emissive" && material.IsKeywordEnabled("_EMISSION"))
                                lensEmissive = true;
                        }
                    }
                }
            }

            if (modelCount != Items.Length) throw new InvalidOperationException("Validasi model gagal: " + modelCount + "/" + Items.Length);
            if (activeModelCount != 1) throw new InvalidOperationException("Harus ada satu item aktif, ditemukan: " + activeModelCount);
            if (cameraCount != 1) throw new InvalidOperationException("Harus ada satu kamera, ditemukan: " + cameraCount);
            if (!lensEmissive) throw new InvalidOperationException("Material lensa menyala tidak ditemukan.");
            if (!beamMeshPresent) throw new InvalidOperationException("Berkas cahaya visual tidak ditemukan.");
            return "OK; models=" + modelCount + "; active=" + activeModelCount + "; cameras=" + cameraCount + "; lights=" + lightCount + "; lensEmission=true; visibleBeam=true";
        }
    }
}
