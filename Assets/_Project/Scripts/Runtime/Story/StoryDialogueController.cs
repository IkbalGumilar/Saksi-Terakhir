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
        private bool automaticPaused;
        private float nextLineAt;
        private bool vehicleTravelActive;
        private DialogueSequence pendingInterruption;
        private Action interruptionCompletion;
        private DialogueSequence interruptedSequence;
        private Action interruptedCompletion;
        private int resumeLine;
        private int playbackGeneration;

        public event Action<DialogueSequence, int> LineChanged;
        public event Action<DialogueSequence, int> LineCompleted;
        public event Action DialogueEnded;

        public bool IsPlaying => activeSequence != null;
        public bool VehicleTravelActive => vehicleTravelActive;
        public bool IsPhoneConversation => IsPlaying && activeSequence.IsPhoneConversation;
        public bool BlocksInventory => IsPlaying && (IsPhoneConversation || !vehicleTravelActive);
        public bool BlocksPause => BlocksInventory;
        public DialogueSequence ActiveSequence => activeSequence;
        public int LineIndex => lineIndex;
        public bool HasQueuedInterruption => pendingInterruption != null;
        public float CharactersPerSecond => charactersPerSecond;
        public DialogueLine CurrentLine => activeSequence != null && lineIndex >= 0
            && lineIndex < activeSequence.Lines.Count ? activeSequence.Lines[lineIndex] : null;

        public bool PlayFrom(DialogueSequence sequence, int firstLine, Action onComplete)
        {
            if (!ValidSequence(sequence) || IsPlaying || firstLine < 0 || firstLine >= sequence.Lines.Count)
                return false;
            BeginSequence(sequence, firstLine, onComplete);
            return true;
        }

        public bool QueueInterruption(DialogueSequence sequence, Action onComplete)
        {
            if (!ValidSequence(sequence) || pendingInterruption != null || interruptedSequence != null) return false;
            if (!IsPlaying) return Play(sequence, onComplete);
            pendingInterruption = sequence;
            interruptionCompletion = onComplete;
            return true;
        }

        // Keep the legacy flag source-compatible; all story playback is hands-free.
        public bool Play(DialogueSequence sequence, Action onComplete, bool autoAdvance = false)
            => PlayFrom(sequence, 0, onComplete);

        private static bool ValidSequence(DialogueSequence sequence)
        {
            if (sequence == null) return false;
            if (!sequence.Validate(out string error))
            {
                Debug.LogError(error, sequence);
                return false;
            }
            return true;
        }

        private void BeginSequence(DialogueSequence sequence, int firstLine, Action onComplete)
        {
            playbackGeneration++;
            activeSequence = sequence;
            completion = onComplete;
            automaticPaused = false;
            lineIndex = firstLine;
            ScheduleCurrentLine();
            LineChanged?.Invoke(activeSequence, lineIndex);
            ApplyPlaybackLocks();
        }

        public void SetVehicleTravelActive(bool active)
        {
            vehicleTravelActive = active;
            if (active && playerCamera != null) playerCamera.SetCinematicLookLocked(false);
            ApplyPlaybackLocks();
        }

        public bool TryConsumeInteract() => IsPlaying;

        public void SetAutoAdvancePaused(bool paused)
        {
            if (automaticPaused == paused) return;
            automaticPaused = paused;
            if (!paused) ScheduleCurrentLine();
        }

        public float GetLineDuration(string text)
        {
            int characterCount = string.IsNullOrEmpty(text) ? 0 : text.Length;
            float cps = charactersPerSecond;
            float postReveal = postRevealSeconds;
            if (Application.isPlaying)
            {
                var settings = SaksiTerakhir.Settings.GameSettingsManager.Current;
                if (settings != null)
                {
                    cps = settings.textSpeed;
                    postReveal = settings.autoAdvanceDelay;
                }
            }
            cps = Mathf.Max(10f, cps);
            float revealSeconds = characterCount / cps;
            return Mathf.Max(minimumLineSeconds, revealSeconds + postReveal);
        }

        public void Stop()
        {
            pendingInterruption = null;
            interruptionCompletion = null;
            interruptedSequence = null;
            interruptedCompletion = null;
            playbackGeneration++;
            if (!IsPlaying) return;
            activeSequence = null;
            lineIndex = -1;
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
            if (Time.timeScale > 0f && IsPlaying && !automaticPaused)
            {
                var settings = SaksiTerakhir.Settings.GameSettingsManager.Current;
                bool shouldAuto = settings == null || settings.autoAdvance;
                if (shouldAuto && Time.time >= nextLineAt)
                    Advance();
            }
        }

        private void OnDisable() => Stop();

        private void Advance()
        {
            if (!IsPlaying) return;
            DialogueSequence spokenSequence = activeSequence;
            int generation = playbackGeneration;
            LineCompleted?.Invoke(spokenSequence, lineIndex);
            // A listener may stop playback to offer a choice, or start a new conversation.
            if (!IsPlaying || generation != playbackGeneration) return;
            if (pendingInterruption != null)
            {
                interruptedSequence = spokenSequence;
                interruptedCompletion = completion;
                resumeLine = lineIndex + 1;
                DialogueSequence call = pendingInterruption;
                Action callCompletion = interruptionCompletion;
                pendingInterruption = null;
                interruptionCompletion = null;
                BeginSequence(call, 0, callCompletion);
                return;
            }
            if (lineIndex + 1 < activeSequence.Lines.Count)
            {
                lineIndex++;
                ScheduleCurrentLine();
                LineChanged?.Invoke(activeSequence, lineIndex);
                ApplyPlaybackLocks();
                return;
            }

            Action finished = completion;
            DialogueSequence resume = interruptedSequence;
            Action resumeFinished = interruptedCompletion;
            int nextUnreadLine = resumeLine;
            Stop();
            finished?.Invoke();
            if (resume == null || IsPlaying || !isActiveAndEnabled) return;
            if (nextUnreadLine < resume.Lines.Count) PlayFrom(resume, nextUnreadLine, resumeFinished);
            else resumeFinished?.Invoke();
        }

        private void ScheduleCurrentLine()
        {
            nextLineAt = Time.time + GetLineDuration(CurrentLine != null ? CurrentLine.Text : string.Empty);
        }

        private void ApplyPlaybackLocks()
        {
            bool locked = IsPlaying && !IsPhoneConversation && !vehicleTravelActive;
            if (playerMovement != null) playerMovement.SetStoryMovementLocked(locked);
            if (playerCamera == null) return;
            playerCamera.SetStoryLookLocked(locked);
            if (!locked) playerCamera.ClearStoryFocus();
        }
    }
}
