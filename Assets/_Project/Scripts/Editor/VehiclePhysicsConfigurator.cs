using System;
using System.Linq;
using SaksiTerakhir.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    public static class VehiclePhysicsConfigurator
    {
        private const string VehicleName = "SUV_Fleet_Black";
        private const string WheelObjectName = "SUV_Fleet_Wheels";
        private const string ArchiveScenePath = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string GeneratedRoot = "Assets/_Project/Art/Generated/VehiclePhysics";
        [MenuItem("Saksi Terakhir/Configure SUV Physics")]
        public static void ConfigureActiveSceneVehicle()
        {
            var scene = SceneManager.GetActiveScene();
            var vehicle = scene.GetRootGameObjects().FirstOrDefault(root => root.name == VehicleName);
            if (vehicle == null)
            {
                Debug.LogError($"Open a scene containing a root GameObject named '{VehicleName}' first.");
                return;
            }

            ConfigureVehicle(vehicle, scene);
        }

        public static void ConfigureArchiveSceneForVerification()
        {
            var scene = EditorSceneManager.OpenScene(ArchiveScenePath, OpenSceneMode.Single);
            var vehicle = scene.GetRootGameObjects().FirstOrDefault(root => root.name == VehicleName);
            if (vehicle == null)
            {
                throw new InvalidOperationException($"'{VehicleName}' was not found in {ArchiveScenePath}.");
            }

            ConfigureVehicle(vehicle, scene);
            VerifySuspensionContact(vehicle);
        }

        private static void ConfigureVehicle(GameObject vehicle, Scene scene)
        {
            if (vehicle.GetComponent<VehicleWheelVisualSync>() != null)
            {
                Debug.Log("SUV already has its four-wheel physics rig; no changes were made.");
                return;
            }

            try
            {
                var wasDirty = scene.isDirty;
                var wheelFilter = vehicle.GetComponentsInChildren<MeshFilter>(true)
                    .FirstOrDefault(filter => filter.name == WheelObjectName && filter.sharedMesh != null);
                var wheelRenderer = wheelFilter != null ? wheelFilter.GetComponent<MeshRenderer>() : null;
                if (wheelFilter == null || wheelRenderer == null)
                {
                    throw new InvalidOperationException("The merged SUV wheel mesh or renderer is missing.");
                }

                EnsureReadable(wheelFilter.sharedMesh);
                wheelFilter = vehicle.GetComponentsInChildren<MeshFilter>(true)
                    .First(filter => filter.name == WheelObjectName && filter.sharedMesh != null);
                wheelRenderer = wheelFilter.GetComponent<MeshRenderer>();

                var toRoot = vehicle.transform.worldToLocalMatrix * wheelFilter.transform.localToWorldMatrix;
                var parts = VehicleWheelMeshSplitter.Split(wheelFilter.sharedMesh, toRoot);
                if (parts.Length != 4 || parts.Any(part => part.radius < 0.20f || part.radius > 0.75f))
                {
                    DestroyParts(parts);
                    throw new InvalidOperationException("The wheel mesh did not produce four plausible tire radii.");
                }

                EnsureFolder(GeneratedRoot);
                var savedMeshes = SaveWheelMeshes(parts);
                BuildRig(vehicle, wheelFilter, wheelRenderer, parts, savedMeshes);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!wasDirty)
                {
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("SUV physics configured and saved: four independent WheelColliders, dynamic chassis, and split wheel visuals.");
                }
                else
                {
                    Debug.LogWarning("SUV physics configured in the open scene. The scene already had unsaved edits, so save it manually after review.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void EnsureReadable(Mesh mesh)
        {
            var path = AssetDatabase.GetAssetPath(mesh);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null || importer.isReadable)
            {
                if (!mesh.isReadable)
                {
                    throw new InvalidOperationException($"Wheel mesh '{mesh.name}' is not readable.");
                }
                return;
            }

            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static Mesh[] SaveWheelMeshes(VehicleWheelMeshPart[] parts)
        {
            var saved = new Mesh[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                var path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{GeneratedRoot}/{VehicleName}_Wheel_{parts[i].name}.asset");
                AssetDatabase.CreateAsset(parts[i].mesh, path);
                saved[i] = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }

            AssetDatabase.SaveAssets();
            return saved;
        }

        private static void BuildRig(GameObject vehicle, MeshFilter mergedFilter, MeshRenderer mergedRenderer,
            VehicleWheelMeshPart[] parts, Mesh[] savedMeshes)
        {
            var bodyColliders = vehicle.GetComponentsInChildren<Collider>(true)
                .Where(collider => collider.transform != mergedFilter.transform)
                .ToArray();
            foreach (var meshCollider in bodyColliders.OfType<MeshCollider>())
            {
                if (!meshCollider.convex)
                {
                    meshCollider.enabled = false;
                    Debug.LogWarning($"Disabled unsupported non-convex vehicle collider on '{meshCollider.name}'.");
                }
            }

            foreach (var wheelCollider in vehicle.GetComponentsInChildren<WheelCollider>(true))
            {
                UnityEngine.Object.DestroyImmediate(wheelCollider);
            }

            var wheelMeshCollider = mergedFilter.GetComponent<MeshCollider>();
            if (wheelMeshCollider != null) wheelMeshCollider.enabled = false;
            mergedRenderer.enabled = false;

            var body = vehicle.GetComponent<Rigidbody>();
            if (body == null) body = Undo.AddComponent<Rigidbody>(vehicle);
            Undo.RecordObject(body, "Configure SUV rigidbody");
            body.mass = 1500f;
            body.useGravity = true;
            body.isKinematic = false;
            body.linearDamping = 0.02f;
            body.angularDamping = 0.8f;
            body.centerOfMass = new Vector3(0f, 0.72f, -0.05f);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations = 12;
            body.solverVelocityIterations = 8;
            body.maxAngularVelocity = 7f;

            var wheelColliders = new WheelCollider[4];
            var visuals = new Transform[4];
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var anchor = new GameObject($"PhysicsWheel_{part.name}");
                Undo.RegisterCreatedObjectUndo(anchor, "Create SUV wheel suspension");
                anchor.transform.SetParent(vehicle.transform, false);
                anchor.transform.localPosition = part.center;
                anchor.transform.localRotation = Quaternion.identity;
                anchor.transform.localScale = Vector3.one;

                var wheel = anchor.AddComponent<WheelCollider>();
                wheel.radius = part.radius * 0.97f;
                wheel.suspensionDistance = 0.30f;
                wheel.mass = 20f;
                wheel.wheelDampingRate = 0.35f;
                wheel.forceAppPointDistance = 0.12f;
                wheel.suspensionSpring = new JointSpring
                {
                    spring = 35000f,
                    damper = 5200f,
                    targetPosition = 0.50f
                };
                wheel.forwardFriction = new WheelFrictionCurve
                {
                    extremumSlip = 0.40f,
                    extremumValue = 1f,
                    asymptoteSlip = 0.80f,
                    asymptoteValue = 0.50f,
                    stiffness = 1.05f
                };
                wheel.sidewaysFriction = new WheelFrictionCurve
                {
                    extremumSlip = 0.20f,
                    extremumValue = 1f,
                    asymptoteSlip = 0.50f,
                    asymptoteValue = 0.75f,
                    stiffness = 1.0f
                };

                var visual = new GameObject($"WheelVisual_{part.name}");
                Undo.RegisterCreatedObjectUndo(visual, "Create SUV wheel visual");
                visual.transform.SetParent(vehicle.transform, false);
                visual.transform.localPosition = part.center;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                visual.AddComponent<MeshFilter>().sharedMesh = savedMeshes[i];
                var renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = mergedRenderer.sharedMaterials;
                renderer.shadowCastingMode = mergedRenderer.shadowCastingMode;
                renderer.receiveShadows = mergedRenderer.receiveShadows;
                renderer.lightProbeUsage = mergedRenderer.lightProbeUsage;
                renderer.reflectionProbeUsage = mergedRenderer.reflectionProbeUsage;
                renderer.motionVectorGenerationMode = mergedRenderer.motionVectorGenerationMode;

                wheelColliders[i] = wheel;
                visuals[i] = visual.transform;
            }

            var sync = vehicle.GetComponent<VehicleWheelVisualSync>();
            if (sync == null) sync = Undo.AddComponent<VehicleWheelVisualSync>(vehicle);
            Undo.RecordObject(sync, "Link SUV wheel visuals");
            sync.Configure(wheelColliders, visuals);
            wheelColliders[0].ConfigureVehicleSubsteps(5f, 12, 15);
            EditorUtility.SetDirty(vehicle);
            Debug.Log("Wheel anchors: " + string.Join("; ", wheelColliders.Select(wheel =>
                $"{wheel.name} @ {wheel.transform.localPosition}, r={wheel.radius:F3}, "
                + $"spring={wheel.suspensionSpring.spring:F0}, damper={wheel.suspensionSpring.damper:F0}")));
        }

        private static void VerifySuspensionContact(GameObject vehicle)
        {
            var body = vehicle.GetComponent<Rigidbody>();
            var wheels = vehicle.GetComponentsInChildren<WheelCollider>(true);
            if (body == null || wheels.Length != 4 || body.isKinematic || !body.useGravity)
            {
                throw new InvalidOperationException("Vehicle physics setup is incomplete.");
            }

            var startPosition = body.position;
            var startRotation = body.rotation;
            var previousMode = Physics.simulationMode;
            var contacts = 0;
            var lowestNormalY = 1f;
            try
            {
                Physics.SyncTransforms();
                Physics.simulationMode = SimulationMode.Script;
                for (var frame = 0; frame < 180; frame++)
                {
                    Physics.Simulate(Time.fixedDeltaTime);
                }

                foreach (var wheel in wheels)
                {
                    if (wheel.GetGroundHit(out var hit))
                    {
                        contacts++;
                        lowestNormalY = Mathf.Min(lowestNormalY, hit.normal.y);
                    }
                }

                Debug.Log($"Suspension check: grounded wheels {contacts}/4; "
                          + $"body moved {(body.position - startPosition).magnitude:F3} m; "
                          + $"lowest contact normal Y={lowestNormalY:F3}.");
                if (contacts != 4)
                {
                    throw new InvalidOperationException($"Only {contacts} of four tires contacted the ground after 3.6 seconds.");
                }
            }
            finally
            {
                Physics.simulationMode = previousMode;
                body.position = startPosition;
                body.rotation = startRotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                Physics.SyncTransforms();
            }
        }

        private static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static void DestroyParts(VehicleWheelMeshPart[] parts)
        {
            foreach (var part in parts)
            {
                if (part.mesh != null) UnityEngine.Object.DestroyImmediate(part.mesh);
            }
        }
    }
}
