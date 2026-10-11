using SaksiTerakhir.Interaction;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    public sealed class FuelStationInteractable : Interactable
    {
        [SerializeField] private FuelStationChapterDirector station;
        [SerializeField] private FuelStationInteraction kind;
        public override bool CanInteract(Transform actor)
            => base.CanInteract(actor) && actor != null && station != null && station.CanInteract(kind);
        public override void Interact(Transform actor)
        {
            if (CanInteract(actor)) station.TryInteract(kind, actor);
        }
        protected override string ResolvePromptKey() => "interact.npc.talk";
    }
}
