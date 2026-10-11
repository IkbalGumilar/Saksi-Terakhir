using System;
using SaksiTerakhir.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SaksiTerakhir.Inventory
{
    public static class InventoryWorldItemFactory
    {
        private const int InteractableLayer = 6;
        private const float Clearance = 0.025f;
        private static readonly float[] ForwardDistances = { 0.65f, 0.9f, 1.15f, 1.45f };
        private static readonly float[] SideDistances = { 0f, -0.35f, 0.35f };

        public static bool TrySpawn(ItemDefinition definition, InventoryEntry entry,
            Transform actor, out ItemPickupInteractable pickup)
        {
            pickup = null;
            if (actor == null || entry == null || definition == null || definition.IsDocument
                || entry.Id != definition.Id) return false;
            if (!TryCreateInactive(definition, entry.InstanceId, out ItemPickupInteractable staged,
                    out BoxCollider shape)) return false;
            try
            {
                Camera camera = actor.GetComponentInChildren<Camera>();
                Transform viewpoint = camera != null ? camera.transform : actor;
                Vector3 direction = Vector3.ProjectOnPlane(viewpoint.forward, Vector3.up).normalized;
                if (direction.sqrMagnitude < 0.01f) direction = actor.forward;
                direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
                Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
                Vector3 origin = camera != null ? camera.transform.position
                    : actor.position + Vector3.up * 1.1f;
                Vector3 right = Vector3.Cross(Vector3.up, direction);
                Vector3 halfExtents = shape.size * 0.5f + Vector3.one * Clearance;
                Physics.SyncTransforms();

                foreach (float forward in ForwardDistances)
                {
                    foreach (float side in SideDistances)
                    {
                        Vector3 center = origin + direction * forward + right * side;
                        if (!IsSpaceFree(center, halfExtents, rotation)
                            || !IsPathFree(origin, center, halfExtents, rotation, actor)) continue;
                        staged.transform.SetPositionAndRotation(center - rotation * shape.center, rotation);
                        if (actor.gameObject.scene.IsValid())
                            SceneManager.MoveGameObjectToScene(staged.gameObject, actor.gameObject.scene);
                        Activate(staged, shape);
                        pickup = staged;
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Item drop could not be created: " + exception.Message);
            }
            DestroyRuntimeObject(staged.gameObject);
            return false;
        }

        // Save restoration uses the root pose captured from an existing runtime drop.
        public static ItemPickupInteractable CreateAtPose(ItemDefinition definition, string instanceId,
            Vector3 position, Quaternion rotation)
        {
            if (!IsFinite(position) || !IsFinite(rotation)
                || !TryCreateInactive(definition, instanceId, out ItemPickupInteractable pickup,
                    out BoxCollider shape)) return null;
            pickup.transform.SetPositionAndRotation(position, rotation);
            Activate(pickup, shape);
            return pickup;
        }

        private static bool TryCreateInactive(ItemDefinition definition, string instanceId,
            out ItemPickupInteractable pickup, out BoxCollider shape)
        {
            pickup = null;
            shape = null;
            if (definition == null || definition.IsDocument || definition.ModelPrefab == null) return false;
            var root = new GameObject("Dropped " + definition.Id);
            root.SetActive(false);
            try
            {
                GameObject model = Object.Instantiate(definition.ModelPrefab, root.transform, false);
                model.name = definition.ModelPrefab.name;
                model.transform.localPosition = Vector3.zero;
                model.SetActive(true);
                DisableModelPhysicsAndInteraction(model);
                if (!TryGetModelBounds(root.transform, out Bounds bounds))
                {
                    DestroyRuntimeObject(root);
                    return false;
                }
                shape = root.AddComponent<BoxCollider>();
                shape.center = bounds.center;
                shape.size = new Vector3(Mathf.Max(0.025f, bounds.size.x),
                    Mathf.Max(0.025f, bounds.size.y), Mathf.Max(0.025f, bounds.size.z));
                shape.enabled = false;
                Rigidbody body = root.AddComponent<Rigidbody>();
                body.useGravity = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                pickup = root.AddComponent<ItemPickupInteractable>();
                pickup.Configure(definition, instanceId, true);
                SetLayerRecursively(root.transform, InteractableLayer);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Item model could not be created: " + exception.Message);
                DestroyRuntimeObject(root);
                pickup = null;
                shape = null;
                return false;
            }
        }

        private static void Activate(ItemPickupInteractable pickup, BoxCollider shape)
        {
            shape.enabled = true;
            Rigidbody body = pickup.GetComponent<Rigidbody>();
            body.isKinematic = false;
            pickup.gameObject.SetActive(true);
        }

        private static bool IsSpaceFree(Vector3 center, Vector3 halfExtents, Quaternion rotation)
        {
            return !Physics.CheckBox(center, halfExtents, rotation, ~0, QueryTriggerInteraction.Ignore);
        }

        private static bool IsPathFree(Vector3 origin, Vector3 center, Vector3 halfExtents,
            Quaternion rotation, Transform actor)
        {
            foreach (Collider collider in Physics.OverlapBox(origin, halfExtents, rotation,
                         ~0, QueryTriggerInteraction.Ignore))
                if (!collider.transform.IsChildOf(actor)) return false;
            Vector3 travel = center - origin;
            float distance = travel.magnitude;
            if (distance < 0.01f) return false;
            foreach (RaycastHit hit in Physics.BoxCastAll(origin, halfExtents, travel / distance,
                         rotation, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(actor)) continue;
                return false;
            }
            return true;
        }

        internal static bool TryGetModelBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                Bounds local = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    point = root.InverseTransformPoint(renderer.transform.TransformPoint(point));
                    if (!IsFinite(point)) return false;
                    if (hasBounds) bounds.Encapsulate(point);
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                }
            }
            return hasBounds && bounds.size.sqrMagnitude > 0.000001f;
        }

        internal static void DisableModelPhysicsAndInteraction(GameObject model)
        {
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = false;
            }
            foreach (Interactable interactable in model.GetComponentsInChildren<Interactable>(true))
                interactable.enabled = false;
        }

        internal static void SetLayerRecursively(Transform root, int layer)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        internal static void DestroyRuntimeObject(GameObject value)
        {
            if (value == null) return;
            value.SetActive(false);
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w)
                && value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > 0.01f;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
