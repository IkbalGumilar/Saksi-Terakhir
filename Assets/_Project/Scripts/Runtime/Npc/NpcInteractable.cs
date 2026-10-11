using SaksiTerakhir.Interaction;
using SaksiTerakhir.Story;
using SaksiTerakhir.Player;
using UnityEngine;

namespace SaksiTerakhir.Npc
{
    [DisallowMultipleComponent, RequireComponent(typeof(OfficeNpcAgent))]
    public sealed class NpcInteractable : Interactable
    {
        private OfficeNpcAgent agent;
        private ChapterOneDirector storyDirector;
        private NpcConversationPreparation preparation;
        private NpcDialoguePresenter presenter;

        public void SetStoryDirector(ChapterOneDirector director) => storyDirector = director;

        public NpcProfile Profile => Agent != null ? Agent.Profile : null;
        public string DisplayName => Profile != null ? Profile.DisplayName : string.Empty;
        public bool DialogueActive => presenter != null && presenter.IsDirectConversation && presenter.IsSpeakingWith(this);
        public bool ConversationPreparing => preparation != null && preparation.IsPreparing;
        public string ActiveDialogueLine => DialogueActive ? presenter.CurrentLine : string.Empty;

        private OfficeNpcAgent Agent => agent != null ? agent : agent = GetComponent<OfficeNpcAgent>();

        public override bool CanInteract(Transform actor)
        {
            if (!base.CanInteract(actor) || Profile == null || !Profile.Interactable) return false;
            NpcDialoguePresenter activePresenter = presenter != null ? presenter : NpcDialoguePresenter.Instance;
            return activePresenter == null || !activePresenter.IsDirectConversation;
        }

        public override void Interact(Transform actor)
        {
            if (!CanInteract(actor) || actor == null) return;
            if (actor.GetComponent<PlayerController>() != null)
            {
                NpcDialoguePresenter activePresenter = presenter != null ? presenter : NpcDialoguePresenter.Instance;
                // Finish the caption's ownership before the retreat claims this NPC.
                // Otherwise its cancellation would release the new preparation hold.
                if (activePresenter != null && activePresenter.IsPlaying && !activePresenter.IsDirectConversation)
                    activePresenter.Stop();
                // Use the stable standing location when choosing the two-metre speaking spot.
                // The work routine must not restore a different position after retreat begins.
                Agent.SetAmbientSeated(false);
                preparation = actor.GetComponent<NpcConversationPreparation>();
                if (preparation == null) preparation = actor.gameObject.AddComponent<NpcConversationPreparation>();
                preparation.Begin(this, Agent, () => StartDialogue(actor));
                return;
            }
            StartDialogue(actor);
        }

        private void StartDialogue(Transform actor)
        {
            if (this == null || !isActiveAndEnabled || actor == null) return;
            if (storyDirector != null && storyDirector.isActiveAndEnabled)
            {
                FuelStationChapterDirector station = storyDirector.GetComponent<FuelStationChapterDirector>();
                if (station != null && Profile != null
                    && (Profile.Id == "NPC-003" || Profile.Id == "NPC-004"))
                {
                    FuelStationInteraction kind = Profile.Id == "NPC-003"
                        ? FuelStationInteraction.ColleagueA : FuelStationInteraction.ColleagueB;
                    if (station.TryInteract(kind, actor)) return;
                }
                if (!storyDirector.TryBeginNpcDialogue(this, actor))
                    storyDirector.TryBeginGenericNpcDialogue(this, actor);
                return;
            }

            if (presenter == null) presenter = NpcDialoguePresenter.Ensure(actor);
            presenter?.TryPlayConversation(this, actor);
        }

        public bool TryOneWayGreeting(Transform actor, string stableGreetingId = null)
        {
            if (!isActiveAndEnabled || actor == null || Profile == null) return false;
            // Interactable controls only a player's E prompt; it never silences world behaviour.
            if (presenter == null) presenter = NpcDialoguePresenter.Ensure(actor);
            string id = string.IsNullOrWhiteSpace(stableGreetingId) ? "npc.greeting." + Profile.Id : stableGreetingId;
            return presenter != null && presenter.TryPlayGreeting(this, actor, id);
        }

        public void CloseDialogue(bool force = false)
        {
            if (preparation != null) preparation.Cancel(this);
            // Aim may leave the capsule while control and focus are owned by automatic dialogue.
            if (presenter != null && presenter.IsSpeakingWith(this))
            {
                if (force) presenter.StopFor(this);
                return;
            }
        }

        protected override string ResolvePromptKey() => "interact.npc.talk";

        private void OnDisable() => CloseDialogue(true);
    }
}
