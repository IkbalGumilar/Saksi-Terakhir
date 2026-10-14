using System;
using System.Collections.Generic;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Story;
using SaksiTerakhir.Vehicles;
using UnityEngine;

namespace SaksiTerakhir.Persistence
{
    [Serializable]
    public sealed class SavedPose
    {
        // Unity serializes inline null objects as default instances. This must remain zero
        // until an actual pose is captured, so a missing pose cannot become a valid origin.
        public int schemaVersion;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;

        public SavedPose() { }

        public SavedPose(Vector3 position, Quaternion rotation)
        {
            schemaVersion = 1;
            this.position = position;
            this.rotation = rotation;
        }

        public bool IsValid()
        {
            float length = rotation.x * rotation.x + rotation.y * rotation.y
                + rotation.z * rotation.z + rotation.w * rotation.w;
            return schemaVersion == 1 && Finite(position.x) && Finite(position.y) && Finite(position.z)
                && Finite(rotation.x) && Finite(rotation.y) && Finite(rotation.z) && Finite(rotation.w)
                && length >= 0.9f && length <= 1.1f;
        }

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class SavedInventoryEntry
    {
        public string id;
        public string displayName;
        public string description;
        public string iconName;
        public string instanceId;
        public int slotIndex = -1;
    }

    [Serializable]
    public sealed class SavedInventory
    {
        public int columns = 3;
        public int rows = 3;
        // Zero identifies older saves, whose entries have no explicit slot positions.
        public int slotLayoutVersion;
        public SavedInventoryEntry[] items = Array.Empty<SavedInventoryEntry>();
        public SavedInventoryEntry[] documents = Array.Empty<SavedInventoryEntry>();
        public string[] heldInstanceIds = Array.Empty<string>();
        // Optional additions: old saves restore the original journey-key requirement.
        public string[] releasedQuestItemIds = Array.Empty<string>();
        public string[] retiredStoryItemIds = Array.Empty<string>();

        public bool Validate(out string error)
        {
            if (columns < 1 || columns > 8 || rows < 1 || rows > 8 || items == null
                || documents == null || items.Length > columns * rows)
            { error = "Invalid inventory capacity."; return false; }
            if (slotLayoutVersion < 0 || slotLayoutVersion > 1)
            { error = "Invalid inventory layout version."; return false; }
            var retired = new HashSet<string>(StringComparer.Ordinal);
            if (!ValidateStoryItems(retiredStoryItemIds, retired)
                || !ValidateStoryItems(releasedQuestItemIds, new HashSet<string>(StringComparer.Ordinal)))
            { error = "Invalid or duplicate story inventory rule."; return false; }
            foreach (string id in releasedQuestItemIds ?? Array.Empty<string>())
                if (retired.Contains(InventoryQuestProtection.CanonicalId(id)))
                { error = "Retired inventory item cannot be released."; return false; }
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            var itemInstanceIds = new HashSet<string>(StringComparer.Ordinal);
            var occupiedSlots = new HashSet<int>();
            foreach (SavedInventoryEntry[] entries in new[] { items, documents })
            {
                foreach (SavedInventoryEntry entry in entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                    { error = "Invalid inventory entry."; return false; }
                    if (slotLayoutVersion == 1 && string.IsNullOrWhiteSpace(entry.instanceId))
                    { error = "Inventory instance identity is missing."; return false; }
                    if (!string.IsNullOrWhiteSpace(entry.instanceId) && !instanceIds.Add(entry.instanceId))
                    { error = "Duplicate inventory instance identity."; return false; }
                    if (entries != items) continue;
                    if (retired.Contains(InventoryQuestProtection.CanonicalId(entry.id)))
                    { error = "Retired story item cannot remain in inventory."; return false; }
                    if (!string.IsNullOrWhiteSpace(entry.instanceId)) itemInstanceIds.Add(entry.instanceId);
                    if (slotLayoutVersion == 1 && (entry.slotIndex < 0 || entry.slotIndex >= columns * rows
                        || !occupiedSlots.Add(entry.slotIndex)))
                    { error = "Invalid or duplicate inventory slot."; return false; }
                }
            }
            string[] held = heldInstanceIds ?? Array.Empty<string>();
            if (held.Length > 2)
            { error = "Too many held inventory items."; return false; }
            var heldIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string instanceId in held)
                if (string.IsNullOrWhiteSpace(instanceId) || !itemInstanceIds.Contains(instanceId)
                    || !heldIds.Add(instanceId))
                { error = "Invalid held inventory item."; return false; }
            error = string.Empty;
            return true;
        }

        private static bool ValidateStoryItems(string[] ids, HashSet<string> seen)
        {
            foreach (string id in ids ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(InventoryQuestProtection.CanonicalId(id))) return false;
            return true;
        }

        public InventoryState CreateState(Func<string, Sprite> resolveIcon = null,
            Func<string, ItemDefinition> resolveDefinition = null)
        {
            if (!Validate(out string error)) throw new InvalidOperationException(error);
            if (resolveDefinition == null)
            {
                ItemDefinitionCatalog catalog = ItemDefinitionCatalog.LoadDefault();
                if (catalog != null) resolveDefinition = catalog.Find;
            }
            var result = new InventoryState(columns, rows);
            foreach (string id in releasedQuestItemIds ?? Array.Empty<string>()) result.ReleaseQuestItem(id);
            foreach (string id in retiredStoryItemIds ?? Array.Empty<string>()) result.RetireStoryItem(id);
            var restoredItems = new Dictionary<string, InventoryEntry>(StringComparer.Ordinal);
            Add(result, items, InventoryEntryType.Item, resolveIcon, resolveDefinition, restoredItems);
            Add(result, documents, InventoryEntryType.Document, resolveIcon, resolveDefinition, restoredItems);
            if (slotLayoutVersion == 1)
                foreach (SavedInventoryEntry entry in items)
                    result.TryMoveToSlot(restoredItems[entry.instanceId], entry.slotIndex);
            foreach (string instanceId in heldInstanceIds ?? Array.Empty<string>())
                result.TryHold(restoredItems[instanceId]);
            return result;
        }

        public static SavedInventory Capture(InventoryState state)
        {
            if (state == null) return null;
            var heldIds = new string[state.HeldItems.Count];
            for (int index = 0; index < heldIds.Length; index++)
                heldIds[index] = state.HeldItems[index].InstanceId;
            return new SavedInventory { columns = state.Columns, rows = state.Rows, slotLayoutVersion = 1,
                items = CaptureEntries(state, state.Items), documents = CaptureEntries(state, state.Documents),
                heldInstanceIds = heldIds, releasedQuestItemIds = CopyIds(state.ReleasedQuestItemIds),
                retiredStoryItemIds = CopyIds(state.RetiredStoryItemIds) };
        }

        private static string[] CopyIds(IReadOnlyList<string> ids)
        {
            var result = new string[ids.Count];
            for (int index = 0; index < ids.Count; index++) result[index] = ids[index];
            return result;
        }

        private static SavedInventoryEntry[] CaptureEntries(InventoryState state, IReadOnlyList<InventoryEntry> entries)
        {
            var result = new SavedInventoryEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                result[i] = new SavedInventoryEntry { id = entries[i].Id, displayName = entries[i].DisplayName,
                    description = entries[i].Description,
                    instanceId = entries[i].InstanceId, slotIndex = state.GetSlotIndex(entries[i]),
                    iconName = entries[i].Icon != null ? entries[i].Icon.name : string.Empty };
            return result;
        }

        private static void Add(InventoryState state, SavedInventoryEntry[] entries,
            InventoryEntryType type, Func<string, Sprite> resolveIcon,
            Func<string, ItemDefinition> resolveDefinition, IDictionary<string, InventoryEntry> restoredItems)
        {
            SavedInventoryEntry archive = type == InventoryEntryType.Document ? SelectArchiveDocument(entries) : null;
            foreach (SavedInventoryEntry entry in entries)
            {
                string id = type == InventoryEntryType.Document ? ItemDefinitionCatalog.CanonicalId(entry.id) : entry.id;
                if (archive != null && id == ItemDefinitionCatalog.WanasetaArchiveId
                    && !ReferenceEquals(entry, archive)) continue;
                ItemDefinition definition = resolveDefinition?.Invoke(id);
                InventoryEntry restored = definition != null && definition.Type == type
                    && string.Equals(definition.Id, id, StringComparison.Ordinal)
                    ? new InventoryEntry(definition, entry.instanceId)
                    : new InventoryEntry(id, entry.displayName, type,
                        resolveIcon?.Invoke(entry.iconName), entry.description, entry.instanceId);
                if (!state.TryAdd(restored)) throw new InvalidOperationException("Inventory entry could not be restored.");
                if (type == InventoryEntryType.Item) restoredItems.Add(restored.InstanceId, restored);
            }
        }

        private static SavedInventoryEntry SelectArchiveDocument(SavedInventoryEntry[] documents)
        {
            SavedInventoryEntry translation = null;
            foreach (SavedInventoryEntry document in documents)
            {
                // Prefer the original record's identity even when the old translation came first.
                if (document.id == ItemDefinitionCatalog.WanasetaArchiveId) return document;
                if (translation == null && document.id == ItemDefinitionCatalog.LegacyWanasetaTranslationId)
                    translation = document;
            }
            return translation;
        }
    }

    [Serializable]
    public sealed class SavedNpcPose
    {
        public string id;
        public SavedPose pose;
        public bool seated;
        public string spawnPointId;
        public string[] missionPins = Array.Empty<string>();
        public bool storyHeld;
        public string OwnershipKey => string.IsNullOrEmpty(spawnPointId) ? "profile:" + id
            : "point:" + spawnPointId.Length + ":" + spawnPointId + ":" + id;
    }

    [Serializable]
    public sealed class SavedDoor
    {
        public string path;
        public bool open;
    }

    [Serializable]
    public sealed class GameSaveSnapshot
    {
        public int version = 1;
        public string runId;
        public string scenePath;
        public string savedUtc;
        public ChapterOneProgress chapter;
        public SavedPose player;
        // Older saves omit this and need pose migration after the office rebuild.
        public string worldLayoutId = string.Empty;
        public float cameraPitch;
        public SavedInventory inventory;
        public SavedNpcPose[] npcs = Array.Empty<SavedNpcPose>();
        public SavedDoor[] doors = Array.Empty<SavedDoor>();
        public SavedWorldItem[] worldItems = Array.Empty<SavedWorldItem>();
        public string[] npcGreetings = Array.Empty<string>();
        public SavedVehicleJourney vehicleJourney;

        public bool Validate(out string error)
        {
            if (version != 1 || !Guid.TryParse(runId, out _) || string.IsNullOrEmpty(scenePath)
                || !scenePath.StartsWith("Assets/", StringComparison.Ordinal)
                || !scenePath.EndsWith(".unity", StringComparison.Ordinal)
                || !DateTimeOffset.TryParse(savedUtc, out _) || player == null || !player.IsValid()
                || !SavedPose.Finite(cameraPitch) || cameraPitch < -90 || cameraPitch > 90)
            { error = "Invalid save metadata or player pose."; return false; }
            if (chapter == null) { error = "Chapter state is missing."; return false; }
            if (!chapter.ValidateStoryState(out error)) return false;
            if (vehicleJourney != null && (!vehicleJourney.Validate() || !vehicleJourney.MatchesChapter(chapter)))
            { error = "Invalid vehicle journey state."; return false; }
            if (inventory != null && !inventory.Validate(out error)) return false;
            if (!WorldItemSnapshot.Validate(worldItems, inventory, out error)) return false;
            if (npcs == null || doors == null) { error = "World state is missing."; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (SavedNpcPose npc in npcs)
                if (npc == null || string.IsNullOrEmpty(npc.id) || !ids.Add(npc.OwnershipKey)
                    || npc.pose == null || !npc.pose.IsValid())
                { error = "Invalid or duplicate NPC state."; return false; }
            foreach (SavedNpcPose npc in npcs)
            {
                if (npc.missionPins == null) continue; // Older JSON has no population metadata.
                foreach (string reason in npc.missionPins)
                    if (string.IsNullOrWhiteSpace(reason) || reason.Length > 200)
                    { error = "Invalid NPC mission pin."; return false; }
            }
            ids.Clear();
            foreach (string greeting in npcGreetings ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(greeting) || greeting.Length > 256 || !ids.Add(greeting))
                { error = "Invalid or duplicate NPC greeting."; return false; }
            ids.Clear();
            foreach (SavedDoor door in doors)
                if (door == null || string.IsNullOrEmpty(door.path) || !ids.Add(door.path))
                { error = "Invalid or duplicate door state."; return false; }
            error = string.Empty;
            return true;
        }
    }
}
