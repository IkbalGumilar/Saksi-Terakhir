using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SaksiTerakhir.Inventory
{
    public sealed class InventoryHeldItemView : IDisposable
    {
        private const int HandCount = 2;
        private const float MaximumHeldSize = 0.6f;
        private readonly Transform actor;
        private readonly Transform[] anchors = new Transform[HandCount];
        private readonly GameObject[] models = new GameObject[HandCount];
        private readonly InventoryEntry[] entries = new InventoryEntry[HandCount];
        private bool disposed;

        public InventoryHeldItemView(Transform actor)
        {
            this.actor = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
        }

        public void Refresh(InventoryState state)
        {
            if (disposed || actor == null) return;
            for (int index = 0; index < HandCount; index++)
            {
                InventoryEntry entry = state != null && index < state.HeldItems.Count
                    ? state.HeldItems[index] : null;
                if (entry == entries[index] && (entry == null || models[index] != null)) continue;
                ClearModel(index);
                if (entry?.Definition?.ModelPrefab == null) continue;
                Transform anchor = GetAnchor(index);
                anchor.gameObject.SetActive(false);
                GameObject model = Object.Instantiate(entry.Definition.ModelPrefab, anchor, false);
                model.name = "Held " + entry.InstanceId;
                model.SetActive(false);
                model.transform.localPosition = Vector3.zero;
                InventoryWorldItemFactory.DisableModelPhysicsAndInteraction(model);
                InventoryWorldItemFactory.SetLayerRecursively(model.transform, 2);
                // The compensated hand anchor measures bounds in metres without changing source units.
                if (InventoryWorldItemFactory.TryGetModelBounds(anchor, out Bounds bounds))
                {
                    float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    if (largest > MaximumHeldSize) model.transform.localScale *= MaximumHeldSize / largest;
                    if (InventoryWorldItemFactory.TryGetModelBounds(anchor, out bounds))
                        model.transform.localPosition -= bounds.center;
                }
                models[index] = model;
                entries[index] = entry;
                model.SetActive(true);
                anchor.gameObject.SetActive(true);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int index = 0; index < HandCount; index++)
            {
                ClearModel(index);
                if (anchors[index] != null)
                    InventoryWorldItemFactory.DestroyRuntimeObject(anchors[index].gameObject);
                anchors[index] = null;
            }
        }

        private Transform GetAnchor(int index)
        {
            if (anchors[index] != null) return anchors[index];
            Transform parent = FindHandBone(index);
            bool usesBone = parent != null;
            bool usesCamera = false;
            if (!usesBone)
            {
                Camera camera = actor.GetComponentInChildren<Camera>();
                usesCamera = camera != null;
                parent = camera != null ? camera.transform : actor;
            }
            var anchor = new GameObject(index == 0 ? "Inventory Right Hand" : "Inventory Left Hand");
            anchor.layer = 2;
            anchor.transform.SetParent(parent, false);
            // FBX hand bones can inherit a 100x unit conversion. Held models already carry
            // their own import scale, so compensate only their runtime attachment point.
            Vector3 parentScale = parent.lossyScale;
            Vector3 unitScale = new Vector3(InverseScale(parentScale.x),
                InverseScale(parentScale.y), InverseScale(parentScale.z));
            anchor.transform.localScale = unitScale;
            Vector3 offset = usesBone ? Vector3.zero
                : new Vector3(index == 0 ? 0.22f : -0.22f, usesCamera ? -0.2f : 0.9f, 0.45f);
            anchor.transform.localPosition = Vector3.Scale(offset, unitScale);
            anchor.transform.localRotation = Quaternion.identity;
            anchors[index] = anchor.transform;
            return anchors[index];
        }

        private static float InverseScale(float scale)
            => Mathf.Abs(scale) > 0.00001f ? 1f / scale : 1f;

        private Transform FindHandBone(int index)
        {
            foreach (Animator animator in actor.GetComponentsInChildren<Animator>(true))
            {
                if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) continue;
                Transform bone = animator.GetBoneTransform(index == 0
                    ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                if (bone != null) return bone;
            }
            return null;
        }

        private void ClearModel(int index)
        {
            if (models[index] != null) InventoryWorldItemFactory.DestroyRuntimeObject(models[index]);
            models[index] = null;
            entries[index] = null;
        }
    }
}
