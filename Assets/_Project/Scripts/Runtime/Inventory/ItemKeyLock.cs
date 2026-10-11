using UnityEngine;

namespace SaksiTerakhir.Inventory
{
    [DisallowMultipleComponent]
    public sealed class ItemKeyLock : MonoBehaviour
    {
        [SerializeField] private ItemDefinition requiredKey;
        [SerializeField] private bool unlocked;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip lockedClip;
        [SerializeField] private AudioClip unlockClip;

        public ItemDefinition RequiredKey => requiredKey;
        public bool IsLocked => requiredKey != null && !unlocked;
        public bool AcceptsItem(string id) => IsLocked && requiredKey.Id == id;

        public bool TryUnlock(InventoryEntry entry)
        {
            if (entry == null || !AcceptsItem(entry.Id)) return false;
            unlocked = true;
            PlayUnlockFeedback();
            return true;
        }

        public void PlayUnlockFeedback() => Play(unlockClip);
        public void RestoreSavedState(bool isUnlocked) => unlocked = isUnlocked;

        public void RequestUnlock(IInventoryItemUseTarget target, Transform actor)
        {
            if (!IsLocked || actor == null) return;
            InventoryPanelController inventory = actor.GetComponentInParent<InventoryPanelController>();
            if (inventory == null || inventory.State == null) return;
            bool held = false;
            foreach (InventoryEntry entry in inventory.State.HeldItems)
                if (AcceptsItem(entry.Id)) { held = true; break; }
            if (!held) Play(lockedClip);
            inventory.RequestUseForTarget(target);
        }

        private void Play(AudioClip clip)
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
    }
}
