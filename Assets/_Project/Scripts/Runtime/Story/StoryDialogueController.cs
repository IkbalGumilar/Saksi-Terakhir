using System;
using SaksiTerakhir.Player;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    [DisallowMultipleComponent]
    public sealed class StoryDialogueController : MonoBehaviour
    {
        [SerializeField] private PlayerController playerMovement;
        [SerializeField] private PlayerCameraController playerCamera;
        [Header("Automatic Playback")]
        [SerializeField, Min(1f)] private float charactersPerSecond = 30f;
        [SerializeField, Min(0.05f)] private float minimumLineSeconds = 0.5f;
        [SerializeField, Min(0f)] private float postRevealSeconds = 0.25f;

        private DialogueSequence activeSequence;
        private Action completion;
        private int lineIndex = -1;
        private bool automatic;
        private bool automaticPaused;
        private float nextLineAt;

        public event Action<DialogueSequence, int> LineChanged;
        public event Action DialogueEnded;

        public bool IsPlaying => activeSequence != null;
        public DialogueSequence ActiveSequence => activeSequence;
        public int LineIndex => lineIndex;
        public float CharactersPerSecond => charactersPerSecond;
        public DialogueLine CurrentLine => activeSequence != null && lineIndex >= 0
            && lineIndex < activeSequence.Lines.Count ? activeSequence.Lines[lineIndex] : null;

        public bool Play(DialogueSequence sequence, Action onComplete, bool autoAdvance = false)
        {
            if (IsPlaying || sequence == null || !sequence.Validate(out string error))
            {
                if (sequence != null && !sequence.Validate(out error))
                    Debug.LogError(error, sequence);
                return false;
            }

            activeSequence = sequence;
            completion = onComplete;
            // Story dialogue is deliberately hands-free. Keep the parameter so existing callers
            // remain source-compatible while every story sequence follows the same presentation.
            automatic = true;
            automaticPaused = false;
            lineIndex = 0;
            ScheduleCurrentLine();
            if (playerMovement != null) playerMovement.SetStoryMovementLocked(true);
            if (playerCamera != null) playerCamera.SetStoryLookLocked(true);
            LineChanged?.Invoke(activeSequence, lineIndex);
            return true;
        }

        public bool TryConsumeInteract()
        {
            if (!IsPlaying) return false;
            return true;
        }

        public void SetAutoAdvancePaused(bool paused)
        {
            if (automaticPaused == paused) return;
            automaticPaused = paused;
            if (!paused) ScheduleCurrentLine();
        }

        public float GetLineDuration(string text)
        {
            int characterCount = string.IsNullOrEmpty(text) ? 0 : text.Length;
            float revealSeconds = characterCount / Mathf.Max(1f, charactersPerSecond);
            return Mathf.Max(minimumLineSeconds, revealSeconds + postRevealSeconds);
        }

        public void Stop()
        {
            if (!IsPlaying) return;
            activeSequence = null;
            lineIndex = -1;
            automatic = false;
            automaticPaused = false;
            completion = null;
            if (playerMovement != null) playerMovement.SetStoryMovementLocked(false);
            if (playerCamera != null)
            {
                playerCamera.SetStoryLookLocked(false);
                playerCamera.ClearStoryFocus();
            }
            DialogueEnded?.Invoke();
        }

        private void Update()
        {
            if (automatic && IsPlaying && !automaticPaused && Time.time >= nextLineAt)
                Advance();
        }

        private void OnDisable() => Stop();

        private void Advance()
        {
            if (!IsPlaying) return;
            if (lineIndex + 1 < activeSequence.Lines.Count)
            {
                lineIndex++;
                ScheduleCurrentLine();
                LineChanged?.Invoke(activeSequence, lineIndex);
                return;
            }

            Action finished = completion;
            Stop();
            finished?.Invoke();
        }

        private void ScheduleCurrentLine()
        {
            nextLineAt = Time.time + GetLineDuration(CurrentLine != null ? CurrentLine.Text : string.Empty);
        }
    }
}
