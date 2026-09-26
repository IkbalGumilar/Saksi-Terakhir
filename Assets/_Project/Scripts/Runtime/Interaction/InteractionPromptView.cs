using SaksiTerakhir.Localization;
using SaksiTerakhir.Npc;
using TMPro;
using UnityEngine;

namespace SaksiTerakhir.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private string keyGlyph = "E";

        [Header("NPC Dialogue")]
        [SerializeField] private GameObject npcDialogueRoot;
        [SerializeField] private TMP_Text npcNameLabel;
        [SerializeField] private TMP_Text npcLineLabel;

        private Interactable previousTarget;
        private bool storyDialogueActive;

        public bool StoryDialogueActive => storyDialogueActive;

        public void ConfigureStoryVisibility(GameObject actionRoot, GameObject genericDialogueRoot)
        {
            promptRoot = actionRoot;
            npcDialogueRoot = genericDialogueRoot;
        }

        public void SetStoryDialogueActive(bool active)
        {
            storyDialogueActive = active;
            if (active)
            {
                if (promptRoot != null) promptRoot.SetActive(false);
                if (npcDialogueRoot != null) npcDialogueRoot.SetActive(false);
            }
            else if (interactor != null) Refresh();
        }

        public void ConfigureNpcDialogue(GameObject dialogueRoot, TMP_Text nameLabel, TMP_Text lineLabel)
        {
            npcDialogueRoot = dialogueRoot;
            npcNameLabel = nameLabel;
            npcLineLabel = lineLabel;
            if (npcDialogueRoot != null) npcDialogueRoot.SetActive(false);
            if (Application.isPlaying && isActiveAndEnabled && interactor != null) Refresh();
        }

        private void Awake()
        {
            if (interactor == null || promptRoot == null || actionLabel == null)
            {
                Debug.LogError(
                    $"{nameof(InteractionPromptView)} on '{gameObject.name}' is missing a reference "
                    + "(interactor, prompt root or action label).",
                    this);
                enabled = false;
                return;
            }

            promptRoot.SetActive(false);
            if (npcDialogueRoot != null) npcDialogueRoot.SetActive(false);
        }

        private void OnEnable()
        {
            interactor.TargetChanged += OnTargetChanged;
            LocalizationManager.LanguageChanged += Refresh;
            OnTargetChanged(interactor.Current);
        }

        private void OnDisable()
        {
            if (interactor != null) interactor.TargetChanged -= OnTargetChanged;
            LocalizationManager.LanguageChanged -= Refresh;
            if (previousTarget is NpcInteractable npc) npc.CloseDialogue();
            previousTarget = null;
            if (npcDialogueRoot != null) npcDialogueRoot.SetActive(false);
            if (promptRoot != null) promptRoot.SetActive(false);
        }

        private void OnTargetChanged(Interactable target)
        {
            if (target != previousTarget)
            {
                if (previousTarget is NpcInteractable npc) npc.CloseDialogue();
                previousTarget = target;
            }
            promptRoot.SetActive(target != null && !storyDialogueActive);
            Refresh();
        }

        private void Refresh()
        {
            if (storyDialogueActive)
            {
                if (promptRoot != null) promptRoot.SetActive(false);
                if (npcDialogueRoot != null) npcDialogueRoot.SetActive(false);
                return;
            }
            Interactable target = interactor.Current;
            if (target == null)
            {
                if (npcDialogueRoot != null) npcDialogueRoot.SetActive(false);
                return;
            }

            if (keyLabel != null)
            {
                keyLabel.text = keyGlyph;
            }

            string prompt = LocalizationManager.Get(target.PromptKey);
            if (target is NpcInteractable npc)
            {
                actionLabel.text = prompt.Contains("{0}")
                    ? prompt.Replace("{0}", npc.DisplayName)
                    : $"{prompt} {npc.DisplayName}";

                bool dialogueActive = npc.DialogueActive;
                if (npcNameLabel != null) npcNameLabel.text = npc.DisplayName;
                if (npcLineLabel != null) npcLineLabel.text = dialogueActive ? npc.ActiveDialogueLine : string.Empty;
                if (npcDialogueRoot != null) npcDialogueRoot.SetActive(dialogueActive);
                return;
            }

            actionLabel.text = prompt;
            if (npcDialogueRoot != null) npcDialogueRoot.SetActive(false);
        }
    }
}
