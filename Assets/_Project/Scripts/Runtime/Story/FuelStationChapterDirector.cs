using System;
using System.Collections;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Persistence;
using SaksiTerakhir.Player;
using SaksiTerakhir.Vehicles;
using UnityEngine;
using UnityEngine.UI;

namespace SaksiTerakhir.Story
{
    public enum FuelStationInteraction { Grandfather, ColleagueA, ColleagueB }

    [DisallowMultipleComponent]
    public sealed class FuelStationChapterDirector : MonoBehaviour
    {
        [SerializeField] private ChapterOneDirector chapter;
        [SerializeField] private StoryDialogueController dialogue;
        [SerializeField] private Transform player;
        [SerializeField] private VehicleKeyUse vehicle;
        [SerializeField] private DialogueSequence grandfatherConversation;
        [SerializeField] private int grandfatherIntroEnd = 7;
        [SerializeField] private int reportLineEnd = 8;
        [SerializeField] private int nightLineEnd = 11;
        [SerializeField] private int morningLineEnd = 12;
        [SerializeField] private int etiquetteLineEnd = 15;
        [SerializeField] private DialogueSequence directReadyDialogue;
        [SerializeField] private DialogueSequence phoneReadyDialogue;
        [SerializeField] private DialogueSequence farewellDialogue;
        [SerializeField] private DialogueSequence photoDialogue;
        [SerializeField] private DialogueSequence colleagueDialogue;
        [SerializeField] private DialogueSequence[] finalConversations = Array.Empty<DialogueSequence>();
        [Header("Chapter ending")]
        [SerializeField, Min(.1f)] private float fadeSeconds = 2.5f;

        private const string ReceiptId = "document.spbu_receipt";
        private const string PhotoId = "document.spbu_trip_photo";
        private const float ClockCheckpointSeconds = 30f;
        private ChapterOneProgress observedProgress;
        private bool pendingGrandfatherChoice;
        private bool ending;
        private float nextClockCheckpoint;
        private Image endingCurtain;
        private Coroutine endingRoutine;
        private DialogueSequence casualReadyConversation;

        private FuelStationProgress State => chapter != null && chapter.Progress != null
            ? chapter.Progress.Story.FuelStation : null;
        private bool Exploring => State != null && !ending && chapter != null && !chapter.GameplaySuspended
            && (chapter.Progress.Stage == ChapterOneStage.AtFuelStation
                || chapter.Progress.Stage == ChapterOneStage.ReturnToVehicle)
            && vehicle != null && !vehicle.IsOccupied;

        private void OnEnable()
        {
            if (chapter == null) chapter = GetComponent<ChapterOneDirector>();
            if (dialogue == null) dialogue = GetComponent<StoryDialogueController>();
            if (dialogue != null) dialogue.LineCompleted += OnLineCompleted;
            if (chapter != null) chapter.ProgressChanged += ObserveProgress;
            ObserveProgress(chapter != null ? chapter.Progress : null);
        }

        private void ObserveProgress(ChapterOneProgress progress)
        {
            if (observedProgress == progress) return;
            bool replacingRun = observedProgress != null;
            observedProgress = progress;
            pendingGrandfatherChoice = false;
            ChapterOneChoiceView.CancelForOwner(this);
            if (replacingRun && endingRoutine != null)
            {
                StopCoroutine(endingRoutine);
                endingRoutine = null;
                ending = false;
                if (endingCurtain != null) Destroy(endingCurtain.transform.root.gameObject);
                endingCurtain = null;
            }
            nextClockCheckpoint = State != null
                ? (Mathf.Floor(State.ElapsedSeconds / ClockCheckpointSeconds) + 1f) * ClockCheckpointSeconds
                : ClockCheckpointSeconds;
        }

        private void Update()
        {
            // A saved, already occupied return checkpoint must still deliver B's
            // pending call before starting the departure conversation.
            if (!ending && chapter != null && !chapter.GameplaySuspended && State != null
                && chapter.Progress.Stage == ChapterOneStage.ReturnToVehicle && Time.timeScale > 0f)
                AnnounceReady();
            if (!Exploring || Time.deltaTime <= 0f) return;
            FuelStationProgress state = State;
            if (!state.ExplorationStarted && state.BeginExploration()) Checkpoint();
            state.AdvanceTime(Time.deltaTime, ColleagueDistance());
            if (state.Ready)
            {
                // The receipt and cargo must be present in the checkpoint that
                // publishes the new return-to-car quest.
                if (chapter.Progress.Stage == ChapterOneStage.AtFuelStation
                    && ChapterOneDocumentRewards.Grant(chapter.PlayerInventory, ReceiptId))
                {
                    chapter.TryNotifyStationReady();
                }
                AnnounceReady();
            }
            else if (state.ElapsedSeconds >= nextClockCheckpoint)
            {
                nextClockCheckpoint += ClockCheckpointSeconds;
                Checkpoint();
            }
            if (pendingGrandfatherChoice && state.CallDelivered && !ChapterOneChoiceView.IsOpen
                && dialogue != null && dialogue.ActiveSequence == grandfatherConversation)
            {
                pendingGrandfatherChoice = false;
                dialogue.Stop();
                ShowGrandfatherChoice();
            }
        }

        private float ColleagueDistance()
        {
            if (player == null || vehicle == null) return float.MaxValue;
            Transform colleague = vehicle.ColleagueBTransform;
            return Vector3.Distance(player.position, colleague != null ? colleague.position : vehicle.transform.position);
        }

        private void AnnounceReady()
        {
            FuelStationProgress state = State;
            if (state == null || !state.Ready || state.CallDelivered || dialogue == null
                || ChapterOneChoiceView.IsOpen || dialogue.HasQueuedInterruption) return;
            DialogueSequence ready = state.PhoneCall ? phoneReadyDialogue : directReadyDialogue;
            if (ready == null || dialogue.ActiveSequence == ready) return;
            void Delivered()
            {
                if (this == null || !isActiveAndEnabled || State != state) return;
                if (state.MarkCallDelivered()) Checkpoint();
            }
            if (dialogue.IsPlaying) dialogue.QueueInterruption(ready, Delivered);
            else dialogue.Play(ready, Delivered);
        }

        public bool CanInteract(FuelStationInteraction kind)
        {
            if (!Exploring || player == null || dialogue == null || dialogue.IsPlaying
                || ChapterOneChoiceView.IsOpen || !Enum.IsDefined(typeof(FuelStationInteraction), kind)) return false;
            return kind != FuelStationInteraction.Grandfather || grandfatherConversation != null
                && State.GrandfatherNextLine < grandfatherConversation.Lines.Count;
        }

        public bool TryInteract(FuelStationInteraction kind, Transform actor)
        {
            if (actor == null || actor != player || !CanInteract(kind)) return false;
            if (kind == FuelStationInteraction.Grandfather) return ContinueGrandfather();
            if (kind == FuelStationInteraction.ColleagueB && !State.PhotoTaken)
                return ChapterOneChoiceView.Show(this, player, Text("Ambil foto perjalanan bertiga?", "Take a travel photo together?"),
                    new ChapterOneChoiceView.Option(Text("Foto bertiga", "Take the photo"), TakePhoto),
                    new ChapterOneChoiceView.Option(Text("Berbincang saja", "Just talk"), () => TalkToColleague(kind)),
                    new ChapterOneChoiceView.Option(Text("Nanti saja", "Maybe later"), null));
            return TalkToColleague(kind);
        }

        private bool ContinueGrandfather()
        {
            if (!Exploring || dialogue == null || dialogue.IsPlaying || grandfatherConversation == null) return false;
            int next = State.GrandfatherNextLine;
            if (next >= grandfatherConversation.Lines.Count) return false;
            return dialogue.PlayFrom(grandfatherConversation, next, CheckpointAfterConversation);
        }

        private void OnLineCompleted(DialogueSequence sequence, int index)
        {
            if (sequence != grandfatherConversation || !Exploring) return;
            FuelStationProgress state = State;
            GrandfatherTopic previous = state.GrandfatherTopic;
            GrandfatherTopic topic = TopicAt(index, previous);
            if (!state.RecordGrandfatherLine(index, topic)) return;
            if (topic == previous) return;
            Checkpoint();
            if (index + 1 >= grandfatherConversation.Lines.Count) return;
            if (state.Ready && !state.CallDelivered)
            {
                // Let B speak at the sentence boundary first. The interrupted
                // next line stays unread and the choice opens after it resumes.
                pendingGrandfatherChoice = true;
                return;
            }
            dialogue.Stop();
            ShowGrandfatherChoice();
        }

        private GrandfatherTopic TopicAt(int index, GrandfatherTopic previous)
        {
            if (index == etiquetteLineEnd) return GrandfatherTopic.GuestEtiquette;
            if (index == morningLineEnd) return GrandfatherTopic.WaitUntilMorning;
            if (index == nightLineEnd) return GrandfatherTopic.NightTrail;
            if (index == reportLineEnd) return GrandfatherTopic.GiriawasReport;
            if (index == grandfatherIntroEnd) return GrandfatherTopic.Introduction;
            return previous;
        }

        private void ShowGrandfatherChoice()
        {
            if (!Exploring || player == null) return;
            ChapterOneChoiceView.Show(this, player, Text("Lanjutkan obrolan dengan kakek?", "Continue talking to the grandfather?"),
                new ChapterOneChoiceView.Option(Text("Dengarkan lagi", "Keep listening"), () => ContinueGrandfather()),
                new ChapterOneChoiceView.Option(Text("Berpamitan dulu", "Say goodbye for now"), LeaveGrandfather));
        }

        private void LeaveGrandfather()
        {
            pendingGrandfatherChoice = false;
            if (!Exploring || dialogue == null) return;
            // The manuscript's farewell says the colleagues are waiting; it
            // applies only after their ready call, not to an early pause.
            if (!State.CallDelivered || !dialogue.Play(farewellDialogue, CheckpointAfterConversation))
                CheckpointAfterConversation();
        }

        private void TakePhoto()
        {
            if (!Exploring || State.PhotoTaken || dialogue == null || dialogue.IsPlaying) return;
            dialogue.Play(photoDialogue, () =>
            {
                if (this == null || !Exploring || !ChapterOneDocumentRewards.Grant(chapter.PlayerInventory, PhotoId)) return;
                if (State.TakePhoto()) Checkpoint();
            });
        }

        private bool TalkToColleague(FuelStationInteraction kind)
        {
            if (!Exploring || dialogue == null || dialogue.IsPlaying) return false;
            if (!State.Ready) return dialogue.Play(colleagueDialogue, null);
            DialogueSequence source = kind == FuelStationInteraction.ColleagueA ? directReadyDialogue : phoneReadyDialogue;
            int index = kind == FuelStationInteraction.ColleagueA ? 1 : 0;
            if (source == null || source.Lines.Count <= index) return false;
            if (casualReadyConversation == null)
            {
                casualReadyConversation = ScriptableObject.CreateInstance<DialogueSequence>();
                casualReadyConversation.hideFlags = HideFlags.DontSave;
            }
            casualReadyConversation.Configure("chapter.spbu_casual_ready", new[] { source.Lines[index] });
            return dialogue.Play(casualReadyConversation, null);
        }

        public bool TryBeginDeparture(Action completed)
        {
            if (ending || chapter == null || chapter.GameplaySuspended || chapter.Progress == null
                || chapter.Progress.Stage != ChapterOneStage.ReturnToVehicle || State == null || !State.Ready
                || !State.CallDelivered
                || vehicle == null || !vehicle.IsOccupied || dialogue == null || dialogue.IsPlaying) return false;
            DialogueSequence chosen = null;
            foreach (DialogueSequence sequence in finalConversations)
                if (sequence != null && sequence.MeetsConditions(chapter.Progress, chapter.PlayerInventory != null ? chapter.PlayerInventory.State : null))
                {
                    if (chosen != null)
                    {
                        Debug.LogError("More than one SPBU departure conversation matches the saved facts.", this);
                        return false;
                    }
                    chosen = sequence;
                }
            if (chosen == null) return false;
            return dialogue.Play(chosen, () =>
            {
                if (this == null || chapter == null || chapter.GameplaySuspended || vehicle == null || !vehicle.IsOccupied) return;
                State.ChooseDestination();
                Checkpoint();
                completed?.Invoke();
            });
        }

        public void FinishChapter()
        {
            if (!Application.isPlaying || ending || chapter == null || chapter.GameplaySuspended || State == null
                || (chapter.Progress.Stage != ChapterOneStage.LeavingFuelStation && !State.Complete)) return;
            ending = true;
            endingRoutine = StartCoroutine(FadeToChapterEnd());
        }

        private IEnumerator FadeToChapterEnd()
        {
            ChapterOneChoiceView.CancelForOwner(this);
            chapter.PlayerInventory?.Close();
            player?.GetComponent<PlayerPauseController>()?.Resume();
            dialogue?.Stop();
            player?.GetComponent<PlayerController>()?.SetUiMovementLocked(true);
            player?.GetComponent<PlayerCameraController>()?.SetUiLookLocked(true);
            player?.GetComponent<PlayerInteractor>()?.SetUiInteractionBlocked(true);
            CreateEndingCurtain();
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                endingCurtain.color = new Color(0f, 0f, 0f, Mathf.Clamp01(elapsed / Mathf.Max(.1f, fadeSeconds)));
                yield return null;
            }
            endingCurtain.color = Color.black;
            yield return null; // Give the opaque frame to the renderer before stopping the run.
            if (!State.Complete && !chapter.TryCompleteChapter())
            {
                Debug.LogError("The Chapter 1 ending could not finalize its story state.", this);
                yield break;
            }
            bool saved = SaveCheckpoint();
            if (!saved)
                Debug.LogWarning("Chapter 1 is black; the editor will remain in Play Mode until its ending can be saved.", this);
            while (!saved)
            {
                yield return new WaitForSecondsRealtime(1f);
                saved = SaveCheckpoint();
            }
            chapter.SuspendGameplay();
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            yield return new WaitForSecondsRealtime(.5f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            endingRoutine = null;
        }

        private void CreateEndingCurtain()
        {
            var canvasObject = new GameObject("Chapter One Black Screen", typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var imageObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            RectTransform rectangle = imageObject.GetComponent<RectTransform>();
            rectangle.SetParent(canvasObject.transform, false);
            rectangle.anchorMin = Vector2.zero;
            rectangle.anchorMax = Vector2.one;
            rectangle.offsetMin = rectangle.offsetMax = Vector2.zero;
            endingCurtain = imageObject.GetComponent<Image>();
            endingCurtain.color = Color.clear;
            endingCurtain.raycastTarget = true;
        }

        private void CheckpointAfterConversation()
        {
            if (this != null && isActiveAndEnabled && State != null) Checkpoint();
        }

        private void Checkpoint()
        {
            chapter?.NotifyStationProgress();
            GameSessionController session = chapter != null ? chapter.GetComponent<GameSessionController>() : null;
            if (session != null) session.RequestAutosave();
        }

        private bool SaveCheckpoint()
        {
            GameSessionController session = chapter != null ? chapter.GetComponent<GameSessionController>() : null;
            return session != null && session.TrySaveGame();
        }

        private static string Text(string indonesian, string english)
            => LocalizationManager.CurrentLanguage == GameLanguage.Indonesian ? indonesian : english;

        private void OnDisable()
        {
            if (dialogue != null) dialogue.LineCompleted -= OnLineCompleted;
            if (chapter != null) chapter.ProgressChanged -= ObserveProgress;
            ChapterOneChoiceView.CancelForOwner(this);
            if (endingRoutine != null) StopCoroutine(endingRoutine);
            endingRoutine = null;
            if (endingCurtain != null) Destroy(endingCurtain.transform.root.gameObject);
            endingCurtain = null;
            if (casualReadyConversation != null) Destroy(casualReadyConversation);
            casualReadyConversation = null;
        }
    }
}
