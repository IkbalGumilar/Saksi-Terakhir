using System.Collections.Generic;
using SaksiTerakhir.Interaction;
using UnityEngine;

namespace SaksiTerakhir.Npc
{
    [DisallowMultipleComponent, RequireComponent(typeof(OfficeNpcAgent))]
    public sealed class NpcInteractable : Interactable
    {
        private OfficeNpcAgent agent;
        private int dialogueIndex = -1;

        public string DisplayName => Agent != null && Agent.Profile != null
            ? Agent.Profile.DisplayName
            : string.Empty;
        public bool DialogueActive => dialogueIndex >= 0;
        public string ActiveDialogueLine { get; private set; } = string.Empty;

        private OfficeNpcAgent Agent => agent != null ? agent : agent = GetComponent<OfficeNpcAgent>();

        public override bool CanInteract(Transform actor)
        {
            return base.CanInteract(actor) && Agent != null && Agent.Profile != null;
        }

        public override void Interact(Transform actor)
        {
            if (!CanInteract(actor)) return;

            IReadOnlyList<string> lines = Agent.Profile.DialogueLines;
            if (lines == null || lines.Count == 0 || dialogueIndex >= lines.Count - 1)
            {
                CloseDialogue();
                return;
            }

            if (!DialogueActive) Agent.BeginPlayerInteraction(actor);
            dialogueIndex++;
            ActiveDialogueLine = lines[dialogueIndex] ?? string.Empty;
        }

        public void CloseDialogue()
        {
            if (DialogueActive && Agent != null) Agent.EndPlayerInteraction();
            dialogueIndex = -1;
            ActiveDialogueLine = string.Empty;
        }

        protected override string ResolvePromptKey() => "interact.npc.talk";

        private void OnDisable() => CloseDialogue();
    }
}
