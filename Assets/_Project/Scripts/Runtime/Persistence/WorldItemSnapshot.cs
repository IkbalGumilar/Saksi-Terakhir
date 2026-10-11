using System;
using System.Collections.Generic;
using SaksiTerakhir.Inventory;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.Persistence
{
    [Serializable]
    public sealed class SavedWorldItem
    {
        public string definitionId;
        public string instanceId;
        public bool runtimeSpawned;
        public bool collected;
        public bool activeSelf;
        public SavedPose pose;
    }

    public static class WorldItemSnapshot
    {
        private static readonly Dictionary<ItemPickupInteractable, bool> nativeActiveBaseline =
            new Dictionary<ItemPickupInteractable, bool>();

        public static SavedWorldItem[] Capture(Scene scene)
        {
            PruneBaselines();
            var records = new List<SavedWorldItem>();
            foreach (ItemPickupInteractable pickup in GameWorldSnapshot.FindAll<ItemPickupInteractable>(scene))
            {
                if (!IsWorldPickup(pickup)) continue;
                if (!pickup.RuntimeSpawned) RememberNativeBaseline(pickup);
                // Collected runtime roots are disposable history. Their identity is already
                // owned by the inventory or a later drop, and they must never be recreated.
                else if (pickup.Collected) continue;
                records.Add(new SavedWorldItem {
                    definitionId = pickup.Definition.Id, instanceId = pickup.InstanceId,
                    runtimeSpawned = pickup.RuntimeSpawned, collected = pickup.Collected,
                    activeSelf = pickup.gameObject.activeSelf,
                    pose = new SavedPose(pickup.transform.position, pickup.transform.rotation) });
            }
            return records.ToArray();
        }

        public static bool Validate(SavedWorldItem[] records, SavedInventory inventory, out string error)
        {
            if (records == null) { error = string.Empty; return true; }
            var definitionsByInstance = new Dictionary<string, string>(StringComparer.Ordinal);
            var availableOwners = new HashSet<string>(StringComparer.Ordinal);
            if (inventory != null)
            {
                foreach (SavedInventoryEntry[] entries in new[] { inventory.items, inventory.documents })
                {
                    if (entries == null) continue;
                    foreach (SavedInventoryEntry entry in entries)
                    {
                        if (entry == null || string.IsNullOrWhiteSpace(entry.instanceId)) continue;
                        definitionsByInstance[entry.instanceId] = entry.id;
                        availableOwners.Add(entry.instanceId);
                    }
                }
            }
            var nativeRecords = new HashSet<string>(StringComparer.Ordinal);
            var runtimeRecords = new HashSet<string>(StringComparer.Ordinal);
            foreach (SavedWorldItem record in records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.definitionId)
                    || string.IsNullOrWhiteSpace(record.instanceId) || record.pose == null || !record.pose.IsValid())
                { error = "Invalid world inventory item."; return false; }
                HashSet<string> identities = record.runtimeSpawned ? runtimeRecords : nativeRecords;
                if (!identities.Add(record.instanceId))
                { error = "Duplicate world inventory record."; return false; }
                if (definitionsByInstance.TryGetValue(record.instanceId, out string definitionId)
                    && !string.Equals(definitionId, record.definitionId, StringComparison.Ordinal))
                { error = "World inventory identity belongs to another definition."; return false; }
                definitionsByInstance[record.instanceId] = record.definitionId;
                // A collected native pickup and its later runtime drop share identity.
                // Only one uncollected world root or carried entry may own that identity.
                if (!record.collected && !availableOwners.Add(record.instanceId))
                { error = "Inventory item has more than one available owner."; return false; }
            }
            error = string.Empty;
            return true;
        }

        public static void Restore(Scene scene, SavedWorldItem[] records, SavedInventory inventory = null,
            Func<string, ItemDefinition> resolveDefinition = null)
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("World inventory scene is unavailable.");
            if (!Validate(records, inventory, out string error)) throw new InvalidOperationException(error);
            PruneBaselines();
            var previousRuntime = new List<ItemPickupInteractable>();
            var natives = new List<ItemPickupInteractable>();
            foreach (ItemPickupInteractable pickup in GameWorldSnapshot.FindAll<ItemPickupInteractable>(scene))
            {
                if (!IsWorldPickup(pickup)) continue;
                if (pickup.RuntimeSpawned) previousRuntime.Add(pickup);
                else { RememberNativeBaseline(pickup); natives.Add(pickup); }
            }
            var savedNative = new Dictionary<string, SavedWorldItem>(StringComparer.Ordinal);
            var ownedInstances = CarriedInstances(inventory);
            foreach (SavedWorldItem record in records ?? Array.Empty<SavedWorldItem>())
            {
                if (!record.runtimeSpawned) savedNative.Add(record.instanceId, record);
                else if (!record.collected) ownedInstances.Add(record.instanceId);
            }
            if (resolveDefinition == null)
            {
                ItemDefinitionCatalog catalog = ItemDefinitionCatalog.LoadDefault();
                if (catalog != null) resolveDefinition = catalog.Find;
            }

            // Prepare every replacement first so an unavailable definition/model cannot
            // remove the current world's drops or change native pickup ownership.
            var replacements = new List<ItemPickupInteractable>();
            try
            {
                foreach (SavedWorldItem record in records ?? Array.Empty<SavedWorldItem>())
                {
                    if (!record.runtimeSpawned || record.collected) continue;
                    ItemDefinition definition = resolveDefinition?.Invoke(record.definitionId);
                    if (definition == null || !string.Equals(definition.Id, record.definitionId, StringComparison.Ordinal))
                        throw new InvalidOperationException("World item definition is unavailable: " + record.definitionId);
                    ItemPickupInteractable restored = InventoryWorldItemFactory.CreateAtPose(definition,
                        record.instanceId, record.pose.position, record.pose.rotation);
                    if (restored == null) throw new InvalidOperationException("World item model could not be restored: " + record.definitionId);
                    replacements.Add(restored);
                    restored.gameObject.SetActive(false);
                    SceneManager.MoveGameObjectToScene(restored.gameObject, scene);
                }
            }
            catch
            {
                foreach (ItemPickupInteractable replacement in replacements)
                    if (replacement != null) InventoryWorldItemFactory.DestroyRuntimeObject(replacement.gameObject);
                throw;
            }
            foreach (ItemPickupInteractable previous in previousRuntime)
                if (previous != null) InventoryWorldItemFactory.DestroyRuntimeObject(previous.gameObject);
            foreach (ItemPickupInteractable native in natives)
            {
                bool hasRecord = savedNative.TryGetValue(native.InstanceId, out SavedWorldItem saved);
                bool collected = hasRecord ? saved.collected : ownedInstances.Contains(native.InstanceId);
                native.RestoreCollected(collected);
                if (!collected)
                    native.gameObject.SetActive(hasRecord ? saved.activeSelf : nativeActiveBaseline[native]);
            }
            int replacementIndex = 0;
            foreach (SavedWorldItem record in records ?? Array.Empty<SavedWorldItem>())
                if (record.runtimeSpawned && !record.collected)
                    replacements[replacementIndex++].gameObject.SetActive(record.activeSelf);
            Physics.SyncTransforms();
        }

        private static bool IsWorldPickup(ItemPickupInteractable pickup) =>
            pickup != null && pickup.enabled && pickup.Definition != null
            && !string.IsNullOrWhiteSpace(pickup.Definition.Id);

        private static HashSet<string> CarriedInstances(SavedInventory inventory)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (inventory == null) return result;
            foreach (SavedInventoryEntry[] entries in new[] { inventory.items, inventory.documents })
                if (entries != null)
                    foreach (SavedInventoryEntry entry in entries)
                        if (entry != null && !string.IsNullOrWhiteSpace(entry.instanceId)) result.Add(entry.instanceId);
            return result;
        }

        private static void RememberNativeBaseline(ItemPickupInteractable pickup)
        {
            if (!nativeActiveBaseline.ContainsKey(pickup))
                nativeActiveBaseline.Add(pickup, pickup.Collected || pickup.gameObject.activeSelf);
        }

        private static void PruneBaselines()
        {
            var destroyed = new List<ItemPickupInteractable>();
            foreach (ItemPickupInteractable pickup in nativeActiveBaseline.Keys)
                if (pickup == null) destroyed.Add(pickup);
            foreach (ItemPickupInteractable pickup in destroyed) nativeActiveBaseline.Remove(pickup);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => nativeActiveBaseline.Clear();
    }
}
