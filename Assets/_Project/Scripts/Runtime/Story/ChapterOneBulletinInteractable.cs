using UnityEngine;
using SaksiTerakhir.Interaction;

namespace SaksiTerakhir.Story
{
    public sealed class ChapterOneBulletinInteractable : Interactable
    {
        [SerializeField] private ChapterOneDirector chapter;
        public void Configure(ChapterOneDirector director) => chapter = director;
        public override bool CanInteract(Transform actor) => base.CanInteract(actor)
            && chapter != null && chapter.Progress != null && !chapter.GameplaySuspended;
        public override void Interact(Transform actor)
        {
            if (CanInteract(actor)) chapter.TryPlayActivityDialogue("chapter.bulletin", null);
        }
        protected override string ResolvePromptKey() => "interact.chapter.bulletin";
    }
}
