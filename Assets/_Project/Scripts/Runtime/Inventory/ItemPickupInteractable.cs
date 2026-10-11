using System;
using System.Collections.Generic;
using SaksiTerakhir.Interaction;
using UnityEngine;

namespace SaksiTerakhir.Inventory
{
    [DisallowMultipleComponent]
    public sealed class ItemPickupInteractable : Interactable
    {
        [SerializeField] private ItemDefinition definition;
        [Tooltip("Optional stable identity for a scene pickup. Leave empty to use its scene hierarchy path.")]
        [SerializeField] private string instanceId;
        [SerializeField, HideInInspector] private bool collected;
        [SerializeField, HideInInspector] private bool runtimeSpawned;

        public ItemDefinition Definition => definition;
        public string InstanceId
        {
            get
            {
                if (string.IsNullOrEmpty(instanceId)) instanceId = CreateInstanceId();
                return instanceId;
            }
        }
        public bool Collected => collected;
        public bool RuntimeSpawned => runtimeSpawned;

        public void Configure(ItemDefinition itemDefinition, string persistentInstanceId,
            bool spawnedAtRuntime)
        {
            definition = itemDefinition;
            instanceId = persistentInstanceId;
            runtimeSpawned = spawnedAtRuntime;
            collected = false;
        }

        public void RestoreCollected(bool isCollected)
        {
            collected = isCollected;
            gameObject.SetActive(!collected);
        }

        public override bool CanInteract(Transform actor)
        {
            return base.CanInteract(actor) && !collected && definition != null
                && actor != null && actor.GetComponentInParent<InventoryPanelController>() != null;
        }

        public override void Interact(Transform actor)
        {
            if (!CanInteract(actor)) return;
            InventoryPanelController inventory = actor.GetComponentInParent<InventoryPanelController>();
            if (!inventory.TryAdd(definition, InstanceId)) return;
            collected = true;
            gameObject.SetActive(false);
        }

        protected override string ResolvePromptKey() => "interact.item.pickup";

        private string CreateInstanceId()
        {
            if (runtimeSpawned) return Guid.NewGuid().ToString("N");
            var path = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent)
                path.Push(current.GetSiblingIndex() + ":" + current.name);
            string sceneId = string.IsNullOrEmpty(gameObject.scene.path)
                ? gameObject.scene.name : gameObject.scene.path;
            return "scene:" + sceneId + "/" + string.Join("/", path);
        }
    }
}
