using SaksiTerakhir.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace SaksiTerakhir.Inventory
{
    [DisallowMultipleComponent]
    public sealed class ItemUseInteractable : Interactable, IInventoryItemUseTarget
    {
        [SerializeField] private ItemDefinition requiredItem;
        [SerializeField, Min(0.1f)] private float interactionRange = 2f;
        [SerializeField] private UnityEvent onUsed = new UnityEvent();
        [Header("Audio (optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip lockedClip;
        [SerializeField] private AudioClip usedClip;
        private bool invokingUse;

        public ItemDefinition RequiredItem => requiredItem;
        public UnityEvent OnUsed => onUsed;

        public bool AcceptsItem(string itemId)
        {
            return isActiveAndEnabled && requiredItem != null
                && !string.IsNullOrEmpty(requiredItem.Id) && requiredItem.Id == itemId;
        }

        public override bool CanInteract(Transform actor)
        {
            return base.CanInteract(actor) && requiredItem != null && actor != null
                && actor.GetComponentInParent<InventoryPanelController>() != null
                && IsWithinReach(actor);
        }

        public override void Interact(Transform actor)
        {
            if (!CanInteract(actor)) return;
            InventoryPanelController inventory = actor.GetComponentInParent<InventoryPanelController>();
            if (!HasMatchingHeldItem(inventory)) PlayClip(lockedClip);
            inventory.RequestUseForTarget(this);
        }

        public bool TryUseItem(InventoryEntry entry, Transform actor)
        {
            if (invokingUse || entry == null || actor == null || !AcceptsItem(entry.Id)
                || !IsWithinReach(actor)) return false;
            invokingUse = true;
            try
            {
                PlayClip(usedClip);
                onUsed?.Invoke();
                return true;
            }
            finally
            {
                invokingUse = false;
            }
        }

        protected override string ResolvePromptKey() => "interact.item.use";

        private bool HasMatchingHeldItem(InventoryPanelController inventory)
        {
            if (inventory.State == null) return false;
            foreach (InventoryEntry entry in inventory.State.HeldItems)
                if (AcceptsItem(entry.Id)) return true;
            return false;
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null) return;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
            }
            audioSource.PlayOneShot(clip);
        }

        private bool IsWithinReach(Transform actor)
        {
            Camera camera = actor.GetComponentInChildren<Camera>();
            Vector3 origin = camera != null ? camera.transform.position : actor.position;
            Vector3 closest = transform.position;
            float closestDistance = float.MaxValue;
            foreach (Collider collider in GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Vector3 point = collider.ClosestPoint(origin);
                float distance = (point - origin).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closest = point;
            }
            float range = Mathf.Max(0.1f, interactionRange);
            if ((closest - origin).sqrMagnitude > range * range) return false;
            Vector3 direction = closest - origin;
            float length = direction.magnitude;
            if (length < 0.01f) return true;
            InventoryPanelController inventory = actor.GetComponentInParent<InventoryPanelController>();
            Transform actorRoot = inventory != null ? inventory.transform : actor;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction / length, length,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                Transform hitTransform = hit.collider.transform;
                if (hitTransform.IsChildOf(actorRoot) || hitTransform.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }
    }
}
