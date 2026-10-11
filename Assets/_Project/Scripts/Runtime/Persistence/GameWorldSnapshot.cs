using System;
using System.Collections.Generic;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using SaksiTerakhir.Vehicles;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.Persistence
{
    public static class GameWorldSnapshot
    {
        public static GameSaveSnapshot Capture(Scene scene, ChapterOneDirector chapter,
            PlayerController player, string runId)
        {
            var npcs = new List<SavedNpcPose>();
            foreach (OfficeNpcAgent npc in FindAll<OfficeNpcAgent>(scene))
            {
                NpcPopulationMember population = npc.GetComponent<NpcPopulationMember>();
                if (population != null && population.IsManaged && (!npc.isActiveAndEnabled
                    || !population.IsMissionPinned && !npc.HasStoryOrder)) continue;
                if (npc.Profile != null)
                    npcs.Add(new SavedNpcPose { id = npc.Profile.Id, pose = Pose(npc.transform),
                        seated = npc.StorySeated, spawnPointId = population != null && population.IsManaged
                            ? population.SpawnPoint?.Id : null,
                        missionPins = population != null ? population.CaptureMissionPins() : Array.Empty<string>(),
                        storyHeld = npc.HasStoryOrder });
            }
            var doors = new List<SavedDoor>();
            foreach (DoorInteractable door in FindAll<DoorInteractable>(scene))
                doors.Add(new SavedDoor { path = ObjectPath(door.transform), open = door.IsOpen });
            return new GameSaveSnapshot { runId = runId, scenePath = scene.path,
                worldLayoutId = chapter.GetComponent<ChapterOneWorldLayout>()?.Id ?? string.Empty,
                savedUtc = DateTime.UtcNow.ToString("O"), chapter = chapter.Progress,
                player = Pose(player.transform), cameraPitch = player.GetComponent<PlayerCameraController>()?.SavedPitch ?? 0,
                inventory = SavedInventory.Capture(player.GetComponent<InventoryPanelController>()?.State),
                npcs = npcs.ToArray(), doors = doors.ToArray(), worldItems = WorldItemSnapshot.Capture(scene),
                npcGreetings = NpcGreetingMemory.Capture(),
                vehicleJourney = Find<VehicleJourneyDirector>(scene)?.Capture() };
        }

        public static void Restore(Scene scene, GameSaveSnapshot snapshot,
            ChapterOneDirector chapter, PlayerController player)
        {
            if (!snapshot.Validate(out string error)) throw new InvalidOperationException(error);
            if (snapshot.scenePath != scene.path) throw new InvalidOperationException("Save belongs to another scene.");
            VehicleJourneyDirector vehicleJourney = Find<VehicleJourneyDirector>(scene);
            vehicleJourney?.PrepareRestore();
            ChapterOneWorldLayout layout = chapter.GetComponent<ChapterOneWorldLayout>();
            bool migrateLayout = layout != null && layout.NeedsMigration(snapshot.worldLayoutId);
            NpcGreetingMemory.Restore(snapshot.npcGreetings);
            foreach (NpcPopulationManager population in FindAll<NpcPopulationManager>(scene))
                if (population.isActiveAndEnabled) population.RestorePinnedActors(snapshot.npcs);
            InventoryPanelController inventory = player.GetComponent<InventoryPanelController>();
            if (snapshot.inventory != null && inventory != null)
            {
                var icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (Sprite icon in Resources.FindObjectsOfTypeAll<Sprite>())
                    if (icon != null && !icons.ContainsKey(icon.name)) icons.Add(icon.name, icon);
                ItemDefinitionCatalog catalog = ItemDefinitionCatalog.LoadDefault();
                inventory.RestoreState(snapshot.inventory.CreateState(name =>
                    !string.IsNullOrEmpty(name) && icons.TryGetValue(name, out Sprite icon) ? icon : null,
                    catalog != null ? new Func<string, ItemDefinition>(catalog.Find) : null));
            }
            snapshot.chapter.TryMigrateLegacyVehicleReachedCheckpoint();
            chapter.RestoreProgress(snapshot.chapter);
            Transform resumeAnchor = migrateLayout ? layout.ResumeAnchor(snapshot.chapter) : null;
            SavedPose playerPose = resumeAnchor != null ? Pose(resumeAnchor) : snapshot.player;
            if (migrateLayout && snapshot.chapter.Stage >= ChapterOneStage.CollectCarKey)
                ChapterOneDocumentRewards.GrantBriefingDocuments(inventory);
            CharacterController body = player.GetComponent<CharacterController>();
            bool wasEnabled = body != null && body.enabled;
            if (wasEnabled) body.enabled = false;
            player.transform.SetPositionAndRotation(playerPose.position, playerPose.rotation);
            if (wasEnabled) body.enabled = true;
            player.GetComponent<PlayerCameraController>()?.RestoreSavedPitch(snapshot.cameraPitch);

            var npcs = new Dictionary<string, OfficeNpcAgent>(StringComparer.Ordinal);
            foreach (OfficeNpcAgent npc in FindAll<OfficeNpcAgent>(scene))
                if (npc.Profile != null && (npc.GetComponent<NpcPopulationMember>() == null
                    || !npc.GetComponent<NpcPopulationMember>().IsManaged || npc.isActiveAndEnabled))
                {
                    NpcPopulationMember member = npc.GetComponent<NpcPopulationMember>();
                    var identity = new SavedNpcPose { id = npc.Profile.Id,
                        spawnPointId = member != null && member.IsManaged ? member.SpawnPoint?.Id : null };
                    npcs[identity.OwnershipKey] = npc;
                }
            foreach (SavedNpcPose pose in snapshot.npcs)
            {
                // The chapter has already reconstructed valid homes/seats in the new office.
                if (migrateLayout) continue;
                if (!npcs.TryGetValue(pose.OwnershipKey, out OfficeNpcAgent npc)) continue;
                // Restore native standing ownership before choosing a new pose.
                // Re-enabling it after placement could overwrite the saved position.
                npc.SetStorySeated(false);
                bool warped = npc.Navigation != null && npc.Navigation.enabled
                    && npc.Navigation.isOnNavMesh && npc.Navigation.Warp(pose.pose.position);
                npc.SetStorySeated(pose.seated);
                // A seat may be on furniture outside the walkable polygons. Keep
                // its exact saved pose after parking, rather than Warp's projection.
                if (!warped || pose.seated) npc.transform.position = pose.pose.position;
                npc.transform.rotation = pose.pose.rotation;
                Rigidbody npcBody = npc.GetComponent<Rigidbody>();
                if (npcBody != null && npcBody.isKinematic)
                {
                    // Reset interpolation history as well as the transform: a menu
                    // preview restores while physics is paused and must not show an old pose.
                    RigidbodyInterpolation interpolation = npcBody.interpolation;
                    npcBody.interpolation = RigidbodyInterpolation.None;
                    npcBody.position = npc.transform.position;
                    npcBody.rotation = npc.transform.rotation;
                    npcBody.interpolation = interpolation;
                }
            }
            var doors = new Dictionary<string, DoorInteractable>(StringComparer.Ordinal);
            foreach (DoorInteractable door in FindAll<DoorInteractable>(scene))
                doors[ObjectPath(door.transform)] = door;
            foreach (SavedDoor door in snapshot.doors)
                if (doors.TryGetValue(door.path, out DoorInteractable target)) target.RestoreSavedState(door.open);
            SavedWorldItem[] worldItems = migrateLayout
                ? RelocateLegacyDrops(snapshot.worldItems, playerPose.position) : snapshot.worldItems;
            WorldItemSnapshot.Restore(scene, worldItems, snapshot.inventory);
            RestoreLegacyReceptionKey(snapshot, inventory, player.transform);
            vehicleJourney?.Restore(snapshot.vehicleJourney);
            Physics.SyncTransforms();
        }

        private static SavedWorldItem[] RelocateLegacyDrops(SavedWorldItem[] records, Vector3 resumePosition)
        {
            if (records == null) return null;
            var relocated = new SavedWorldItem[records.Length];
            int dropped = 0;
            for (int i = 0; i < records.Length; i++)
            {
                SavedWorldItem record = records[i];
                if (!record.runtimeSpawned || record.collected) { relocated[i] = record; continue; }
                float angle = dropped * 45f * Mathf.Deg2Rad;
                float radius = 1.2f + .25f * (dropped / 8);
                Vector3 position = resumePosition + new Vector3(Mathf.Cos(angle) * radius, .35f, Mathf.Sin(angle) * radius);
                dropped++;
                relocated[i] = new SavedWorldItem { definitionId = record.definitionId, instanceId = record.instanceId,
                    runtimeSpawned = true, collected = false, activeSelf = record.activeSelf,
                    pose = new SavedPose(position, record.pose.rotation) };
            }
            return relocated;
        }

        private static void RestoreLegacyReceptionKey(GameSaveSnapshot snapshot,
            InventoryPanelController inventory, Transform player)
        {
            // Old saves stored reception ownership only as a story flag. New saves
            // already record the actual item, which may intentionally be on the ground.
            if (snapshot.chapter == null || !snapshot.chapter.HasCarKey || inventory == null
                || (snapshot.inventory != null && snapshot.inventory.slotLayoutVersion > 0)
                || inventory.HasItem(CarKeyInventory.CarKeyId)) return;
            ItemDefinition definition = inventory.FindDefinition(CarKeyInventory.CarKeyId);
            if (definition == null)
                throw new InvalidOperationException("Reception key definition is unavailable for legacy save migration.");
            InventoryEntry key = definition.CreateEntry("legacy:car-key:" + snapshot.runId);
            foreach (ItemPickupInteractable existing in FindAll<ItemPickupInteractable>(player.gameObject.scene))
                if (existing != null && existing.enabled && !existing.Collected
                    && existing.InstanceId == key.InstanceId && existing.Definition == definition) return;
            if (inventory.State != null && inventory.State.TryAdd(key)) return;
            // Preserve an already full bag instead of deleting another item or
            // enlarging the player's capacity during migration.
            if (!InventoryWorldItemFactory.TrySpawn(definition, key, player, out _))
                throw new InvalidOperationException("Legacy reception key could not be placed safely.");
        }

        public static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        public static List<T> FindAll<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }

        private static SavedPose Pose(Transform value) => new SavedPose(value.position, value.rotation);

        private static string ObjectPath(Transform value)
        {
            string result = string.Empty;
            for (Transform node = value; node != null; node = node.parent)
                result = "/" + Uri.EscapeDataString(node.name) + "[" + node.GetSiblingIndex() + "]" + result;
            return result;
        }
    }
}
