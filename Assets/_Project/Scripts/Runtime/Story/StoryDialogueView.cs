using SaksiTerakhir.Interaction;
using SaksiTerakhir.Npc;
using TMPro;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    [DisallowMultipleComponent]
    public sealed class StoryDialogueView : MonoBehaviour
    {
        [SerializeField] private StoryDialogueController dialogue;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text lineLabel;
        [SerializeField] private InteractionPromptView genericPrompt;
        [SerializeField] private NpcProfile[] speakerProfiles;
        [Header("Typewriter")]
        [SerializeField, Min(1f)] private float fallbackCharactersPerSecond = 30f;

        private int visibleCharacterCount;
        private float revealedCharacters;
        private bool isRevealing;

        public void Configure(StoryDialogueController controller, GameObject root,
            TMP_Text speaker, TMP_Text line, InteractionPromptView prompt,
            NpcProfile[] profiles)
        {
            dialogue = controller;
            panelRoot = root;
            speakerLabel = speaker;
            lineLabel = line;
            genericPrompt = prompt;
            speakerProfiles = profiles;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void Render(DialogueSequence sequence, int lineIndex)
        {
            if (sequence == null || lineIndex < 0 || lineIndex >= sequence.Lines.Count)
            {
                Hide();
                return;
            }
            if (panelRoot != null) panelRoot.SetActive(true);
            if (genericPrompt != null) genericPrompt.SetStoryDialogueActive(true);
            DialogueLine line = sequence.Lines[lineIndex];
            if (speakerLabel != null) speakerLabel.text = SpeakerName(line.SpeakerId);
            StartTypewriter(line.Text);
        }

        public void Hide()
        {
            isRevealing = false;
            visibleCharacterCount = 0;
            revealedCharacters = 0f;
            if (panelRoot != null) panelRoot.SetActive(false);
            if (genericPrompt != null) genericPrompt.SetStoryDialogueActive(false);
        }

        private void Update()
        {
            AdvanceTypewriter(Time.deltaTime);
        }

        private void StartTypewriter(string text)
        {
            if (lineLabel == null) return;

            lineLabel.text = text ?? string.Empty;
            lineLabel.maxVisibleCharacters = 0;
            lineLabel.ForceMeshUpdate();
            // TextMeshPro has no generated mesh yet in some editor/UI test contexts.
            // Dialogue content is plain text, so source length is the stable reveal count.
            visibleCharacterCount = lineLabel.text.Length;
            revealedCharacters = 0f;
            isRevealing = visibleCharacterCount > 0;
        }

        private void AdvanceTypewriter(float deltaTime)
        {
            if (!isRevealing || lineLabel == null) return;

            float rate = dialogue != null ? dialogue.CharactersPerSecond : fallbackCharactersPerSecond;
            revealedCharacters += Mathf.Max(0f, deltaTime) * Mathf.Max(1f, rate);
            int nextVisibleCount = Mathf.Min(visibleCharacterCount, Mathf.FloorToInt(revealedCharacters));
            lineLabel.maxVisibleCharacters = nextVisibleCount;
            if (nextVisibleCount >= visibleCharacterCount)
                isRevealing = false;
        }

        private string SpeakerName(string id)
        {
            if (id == "PLAYER") return "Kamu";
            if (speakerProfiles != null)
                foreach (NpcProfile profile in speakerProfiles)
                    if (profile != null && profile.Id == id) return profile.DisplayName;
            return id;
        }

        private void OnEnable()
        {
            if (dialogue == null) return;
            dialogue.LineChanged += Render;
            dialogue.DialogueEnded += Hide;
            if (dialogue.IsPlaying) Render(dialogue.ActiveSequence, dialogue.LineIndex);
        }

        private void OnDisable()
        {
            if (dialogue != null)
            {
                dialogue.LineChanged -= Render;
                dialogue.DialogueEnded -= Hide;
            }
            Hide();
        }
    }
}
