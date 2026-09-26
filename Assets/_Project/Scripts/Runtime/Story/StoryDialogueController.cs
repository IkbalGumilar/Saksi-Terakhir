using System;
using SaksiTerakhir.Player;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    [DisallowMultipleComponent]
    public sealed class StoryDialogueController : MonoBehaviour
    {
        [SerializeField] private PlayerController playerMovement;
        [SerializeField, Min(0.5f)] private float automaticLineSeconds = 3.5f;

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
            automatic = autoAdvance;
            automaticPaused = false;
            lineIndex = 0;
            nextLineAt = Time.time + automaticLineSeconds;
            if (playerMovement != null) playerMovement.SetStoryMovementLocked(!automatic);
            LineChanged?.Invoke(activeSequence, lineIndex);
            return true;
        }

        public bool TryConsumeInteract()
        {
            if (!IsPlaying) return false;
            if (!automatic) Advance();
            return true;
        }

        public void SetAutoAdvancePaused(bool paused)
        {
            if (automaticPaused == paused) return;
            automaticPaused = paused;
            if (!paused) nextLineAt = Time.time + automaticLineSeconds;
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
                nextLineAt = Time.time + automaticLineSeconds;
                LineChanged?.Invoke(activeSequence, lineIndex);
                return;
            }

            Action finished = completion;
            Stop();
            finished?.Invoke();
        }
    }
}
