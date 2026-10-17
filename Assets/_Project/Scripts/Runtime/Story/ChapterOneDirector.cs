using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Environment;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace SaksiTerakhir.Story
{
    [DisallowMultipleComponent]
    public sealed class ChapterOneDirector : MonoBehaviour
    {
        private const string RakaId = "NPC-003";
        private const string SintaId = "NPC-004";

        [SerializeField] private StoryDialogueController dialogue;
        [SerializeField] private Transform player;
        [SerializeField] private BossOfficeGate bossGate;
        [SerializeField] private OfficeNpcAgent[] cast = Array.Empty<OfficeNpcAgent>();
        [SerializeField] private Transform rakaSeat;
        [SerializeField] private Transform sintaSeat;
        [SerializeField] private Transform bossDoorWait;
        [SerializeField] private Transform bossChair;
        [SerializeField] private Transform bossMeetingLeft;
        [SerializeField] private Transform bossMeetingRight;
        [SerializeField] private Transform playerSeat;
        [SerializeField] private Transform meetingApproach;
        [SerializeField] private Transform bossWalkStart;
        [SerializeField] private StoryPlayerCinematicMover playerMover;
        [SerializeField] private StoryPlayerSeating playerSeating;
        [SerializeField] private PlayerCameraController playerCamera;
        [SerializeField] private QuestDefinition[] quests = Array.Empty<QuestDefinition>();
        [SerializeField] private DialogueSequence[] sequences = Array.Empty<DialogueSequence>();
        [SerializeField] private InventoryPanelController playerInventory;
        [SerializeField] private bool includeMorningRoutine;
        [SerializeField] private bool vehicleDepartureEnabled = true;
        [SerializeField] private QuestDefinition morningQuest;
        [SerializeField] private TimeOfDayApplier timeOfDay;
        [SerializeField] private TimeOfDayPreset morningTime;
        [SerializeField] private TimeOfDayPreset lunchTime;
        [SerializeField] private TimeOfDayPreset preparationTime;
        [SerializeField] private Transform rakaMealSeat;
        [SerializeField] private Transform sintaMealSeat;
        [SerializeField] private Transform rakaMealApproach;
        [SerializeField] private Transform sintaMealApproach;
        [SerializeField] private Transform workerLunchOne;
        [SerializeField] private Transform workerLunchTwo;
        [SerializeField] private Transform workerRoofOne;
        [SerializeField] private Transform workerRoofTwo;

        private readonly Dictionary<string, OfficeNpcAgent> actors = new Dictionary<string, OfficeNpcAgent>();
        private ChapterOneProgress progress;
        private InventoryState observedInventory;
        private bool initialized;
        private bool sceneStarted;
        private bool lastAtDoor;
        private bool lastSentToSeat;
        private bool escortStarted;
        private bool escortAwaitingPath;
        private bool escortRequiresManualResume;
        private bool playerMovingToSeat;
        private bool seatAwaitingPath;
        private float nextSeatPathRetryAt;
        private OfficeNpcAgent[] activeParticipants = Array.Empty<OfficeNpcAgent>();
        private DialogueSequence genericDialogue;
        private OfficeNpcAgent genericSpeaker;
        private CarKeyInventory inventory;
        private bool morningTransitionRunning;
        private Coroutine morningTransition;
        private Image morningFade;
        private PlayerController transitionMovement;
        private Interaction.PlayerInteractor transitionInteractor;
        private bool transitionMovementLocked;
        private bool transitionLookLocked;
        private bool transitionInteractionBlocked;

        public event Action<ChapterOneProgress> ProgressChanged;
        public event Action<string> NoticeRequested;

        public bool StartupManagedExternally { get; set; }
        public bool GameplaySuspended { get; private set; }

        public void SuspendGameplay()
        {
            CancelMorningTransition();
            GameplaySuspended = true;
        }

        public void ResumeGameplay()
        {
            GameplaySuspended = false;
            if (!initialized) InitializeNewGame();
            else ResumeOrders();
            ProgressChanged?.Invoke(progress);
        }

        public ChapterOneProgress Progress => progress;
        public bool CallPending => progress != null && progress.Stage == ChapterOneStage.AnswerBossCall
            && (!includeMorningRoutine || progress.BossCallAvailable);
        public InventoryPanelController PlayerInventory => playerInventory != null ? playerInventory
            : player != null ? player.GetComponent<InventoryPanelController>() : null;
        public QuestDefinition CurrentQuest
        {
            get
            {
                if (progress == null) return null;
                if (progress.Stage == ChapterOneStage.AtFuelStation || progress.Story.FuelStation.Complete) return null;
                if (progress.Stage == ChapterOneStage.MorningRoutine) return morningQuest;
                if (progress.Stage == ChapterOneStage.AnswerBossCall && !CallPending) return null;
                if ((int)progress.Stage < 0 || (int)progress.Stage >= quests.Length) return null;
                QuestDefinition quest = quests[(int)progress.Stage];
                return quest != null && quest.MeetsConditions(progress, PlayerInventoryState) ? quest : null;
            }
        }
        public CarKeyInventory Inventory => inventory;
        public bool MealColleaguesReady => !includeMorningRoutine
            || IsNearMeal(Get(RakaId), rakaMealApproach) && IsNearMeal(Get(SintaId), sintaMealApproach);

        private static bool IsNearMeal(OfficeNpcAgent actor, Transform approach)
            => actor != null && approach != null && Vector3.Distance(actor.transform.position, approach.position) <= 2f;
        private InventoryState PlayerInventoryState => playerInventory != null ? playerInventory.State
            : player != null ? player.GetComponent<InventoryPanelController>()?.State : null;
        // Test-only persistence seam. It stays empty during normal gameplay.
        public static string SessionSavePathOverride { get; set; }


        public void ConfigureScene(StoryDialogueController storyDialogue, Transform playerTransform,
            BossOfficeGate gate, OfficeNpcAgent[] storyCast, Transform rakaSeatAnchor,
            Transform sintaSeatAnchor, Transform doorWaitAnchor, QuestDefinition[] questAssets,
            DialogueSequence[] dialogueAssets, Transform bossChairAnchor = null,
            Transform bossMeetingLeftAnchor = null, Transform bossMeetingRightAnchor = null,
            Transform playerSeatAnchor = null, StoryPlayerCinematicMover cinematicMover = null,
            StoryPlayerSeating seating = null, PlayerCameraController camera = null,
            Transform meetingApproachAnchor = null, Transform bossWalkStartAnchor = null)
        {
            dialogue = storyDialogue;
            player = playerTransform;
            bossGate = gate;
            cast = storyCast ?? Array.Empty<OfficeNpcAgent>();
            rakaSeat = rakaSeatAnchor;
            sintaSeat = sintaSeatAnchor;
            bossDoorWait = doorWaitAnchor;
            bossChair = bossChairAnchor;
            bossMeetingLeft = bossMeetingLeftAnchor;
            bossMeetingRight = bossMeetingRightAnchor;
            playerSeat = playerSeatAnchor;
            playerMover = cinematicMover;
            playerSeating = seating;
            playerCamera = camera;
            meetingApproach = meetingApproachAnchor;
            bossWalkStart = bossWalkStartAnchor;
            quests = questAssets ?? Array.Empty<QuestDefinition>();
            sequences = dialogueAssets ?? Array.Empty<DialogueSequence>();
            initialized = false;
        }

        public void InitializeNewGame()
        {
            if (initialized) return;
            bool testPersistenceEnabled = Application.isPlaying
                && !string.IsNullOrEmpty(SessionSavePathOverride);
            bool hadSave = testPersistenceEnabled && File.Exists(SessionSavePathOverride);
            // The session controller restores protected autosaves explicitly after startup.
            // This initializer supplies fresh chapter state; the legacy override is test-only.
            progress = testPersistenceEnabled
                ? ChapterOneSaveStore.Load(SessionSavePathOverride)
                : includeMorningRoutine ? ChapterOneProgress.CreateFullChapter() : new ChapterOneProgress();
            inventory = new CarKeyInventory(progress);
            BindScene();
            ProgressChanged?.Invoke(progress);
            if (!hadSave) NoticeRequested?.Invoke("story.quest.started");
        }

        public void RestoreProgress(ChapterOneProgress snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!snapshot.ValidateStoryState(out string error)) throw new InvalidDataException(error);
            CancelMorningTransition();
            dialogue?.Stop();
            playerMover?.StopCinematicMotion();
            ReleaseEscortPendingLock();
            ReleaseSeatPendingLock();
            playerSeating?.StandUp();
            playerCamera?.SetCinematicLookLocked(false);
            playerCamera?.ClearStoryFocus();
            progress = snapshot;
            inventory = new CarKeyInventory(progress);
            BindScene();
            ProgressChanged?.Invoke(progress);
        }

        public bool MeetsCondition(ChapterOneStoryCondition condition)
            => progress != null && progress.Meets(condition, PlayerInventoryState);

        public bool TryApplyStoryEvent(ChapterOneStoryEvent kind)
        {
            if (GameplaySuspended) return false;
            if (!initialized) InitializeNewGame();
            if (!progress.TryApplyStoryEvent(kind)) return false;
            if (kind == ChapterOneStoryEvent.SharedMealStarted) SeatMealColleagues();
            PublishProgress();
            return true;
        }

        public bool TryStartVehicleJourney()
            => !GameplaySuspended && Apply(ChapterOneEvent.JourneyStarted);

        public bool TryParkAtFuelStation()
            => !GameplaySuspended && Apply(ChapterOneEvent.StationParked);

        public bool TryNotifyStationReady()
            => !GameplaySuspended && Apply(ChapterOneEvent.StationReady);

        public bool TryStartStationDeparture()
            => !GameplaySuspended && Apply(ChapterOneEvent.StationDepartureStarted);

        public bool TryCompleteChapter()
            => !GameplaySuspended && Apply(ChapterOneEvent.ChapterFinished);

        public void NotifyStationProgress() => ProgressChanged?.Invoke(progress);

        public bool TryRecordTravelPurchase(string itemId)
        {
            if (GameplaySuspended) return false;
            if (!initialized) InitializeNewGame();
            if (!progress.TryRecordTravelPurchase(itemId)) return false;
            PublishProgress();
            return true;
        }

        public bool TryPlayActivityDialogue(string sequenceId, Action completed)
        {
            if (GameplaySuspended || morningTransitionRunning || dialogue == null || dialogue.IsPlaying) return false;
            if (!initialized) InitializeNewGame();
            DialogueSequence sequence = FindSequence(sequenceId);
            if (sequence == null || !sequence.Validate(out _)) return false;
            activeParticipants = sequenceId == "chapter.shared_meal"
                ? new[] { Get(RakaId), Get(SintaId) } : Array.Empty<OfficeNpcAgent>();
            return dialogue.Play(sequence, completed);
        }

        public bool TryCompleteMorningRoutine()
        {
            if (progress == null || progress.MorningRoutineComplete || GameplaySuspended
                || morningTransitionRunning || player == null || dialogue == null || dialogue.IsPlaying) return false;
            morningTransition = StartCoroutine(JumpToLunch());
            return true;
        }

        private IEnumerator JumpToLunch()
        {
            morningTransitionRunning = true;
            transitionMovement = player.GetComponent<PlayerController>();
            transitionInteractor = player.GetComponent<Interaction.PlayerInteractor>();
            transitionMovementLocked = transitionMovement != null && transitionMovement.UiMovementLocked;
            transitionLookLocked = playerCamera != null && playerCamera.UiLookLocked;
            transitionInteractionBlocked = transitionInteractor != null && transitionInteractor.UiInteractionBlocked;
            try
            {
                if (transitionMovement != null) transitionMovement.SetUiMovementLocked(true);
                if (playerCamera != null) playerCamera.SetUiLookLocked(true);
                if (transitionInteractor != null) transitionInteractor.SetUiInteractionBlocked(true);
                Canvas canvas = null;
                foreach (GameObject sceneRoot in gameObject.scene.GetRootGameObjects())
                    if (sceneRoot.name == "Canvas") canvas = sceneRoot.GetComponent<Canvas>();
                if (canvas != null)
                {
                    GameObject curtain = new GameObject("Chapter Time Jump", typeof(RectTransform), typeof(Image));
                    RectTransform rect = curtain.GetComponent<RectTransform>();
                    Transform safeArea = canvas.transform.Find("Save Area");
                    rect.SetParent(safeArea != null ? safeArea : canvas.transform, false);
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    morningFade = curtain.GetComponent<Image>();
                    morningFade.color = Color.clear;
                    yield return Fade(0f, 1f);
                }
                if (GameplaySuspended) yield break;
                if (timeOfDay != null && lunchTime != null) timeOfDay.Preset = lunchTime;
                PlaceWorker(Get("NPC-021"), workerLunchOne);
                PlaceWorker(Get("NPC-022"), workerLunchTwo);
                yield return Fade(1f, 0f);
            }
            finally
            {
                RestoreMorningControls();
                morningTransition = null;
            }
            TryPlayActivityDialogue("chapter.lunch_invitation", () =>
            {
                if (!Apply(ChapterOneEvent.MorningRoutineFinished)) return;
                ApplyChapterTime();
                SendWorkersToRoof();
            });

            IEnumerator Fade(float start, float end)
            {
                for (float elapsed = 0; elapsed < 0.35f && morningFade != null; elapsed += Time.unscaledDeltaTime)
                {
                    morningFade.color = new Color(0f, 0f, 0f, Mathf.Lerp(start, end, elapsed / 0.35f));
                    yield return null;
                }
                if (morningFade != null) morningFade.color = new Color(0f, 0f, 0f, end);
            }
            void PlaceWorker(OfficeNpcAgent worker, Transform anchor)
            {
                if (worker == null || anchor == null) return;
                worker.HoldForStory(player);
                worker.SetStorySeated(false);
                if (worker.Navigation != null && worker.Navigation.enabled && worker.Navigation.isOnNavMesh)
                    worker.Navigation.Warp(anchor.position);
                worker.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            }
        }

        private void CancelMorningTransition()
        {
            if (morningTransition != null) StopCoroutine(morningTransition);
            morningTransition = null;
            RestoreMorningControls();
        }

        private void RestoreMorningControls()
        {
            if (!morningTransitionRunning) return;
            if (morningFade != null) Destroy(morningFade.gameObject);
            morningFade = null;
            if (transitionMovement != null) transitionMovement.SetUiMovementLocked(transitionMovementLocked);
            if (playerCamera != null) playerCamera.SetUiLookLocked(transitionLookLocked);
            if (transitionInteractor != null) transitionInteractor.SetUiInteractionBlocked(transitionInteractionBlocked);
            transitionMovement = null;
            transitionInteractor = null;
            morningTransitionRunning = false;
        }

        private void SendWorkersToRoof()
        {
            Send(Get("NPC-021"), workerRoofOne);
            Send(Get("NPC-022"), workerRoofTwo);
            void Send(OfficeNpcAgent worker, Transform anchor)
            {
                if (worker == null || anchor == null || Vector3.Distance(worker.transform.position, anchor.position) < 1.2f)
                    return;
                worker.TrySetStoryDestination(anchor.position, 1.6f);
            }
        }

        public void RequestNotice(string key) => NoticeRequested?.Invoke(key);

        public bool TryTriggerBossCall()
        {
            if (GameplaySuspended || progress == null || !progress.TryTriggerBossCall()) return false;
            PublishProgress();
            NoticeRequested?.Invoke("story.call.incoming");
            return true;
        }

        public bool TryEnterOffice()
        {
            if (GameplaySuspended || progress == null || !progress.TryEnterOffice()) return false;
            PublishProgress();
            return true;
        }

        private void ApplyChapterTime()
        {
            if (!includeMorningRoutine || timeOfDay == null || progress == null) return;
            TimeOfDayPreset selected = progress.Stage >= ChapterOneStage.CollectCarKey
                && preparationTime != null ? preparationTime
                : progress.MorningRoutineComplete ? lunchTime : morningTime;
            if (selected != null && timeOfDay.ActivePreset != selected) timeOfDay.Preset = selected;
        }

        private void SeatMealColleagues()
        {
            SnapActor(Get(RakaId), rakaMealSeat, player);
            SnapActor(Get(SintaId), sintaMealSeat, player);
        }

        private void SendColleaguesToLunch()
        {
            bossGate?.GetComponent<Interaction.DoorInteractable>()?.ForceOpen(Get(RakaId)?.transform);
            Send(Get(RakaId), rakaMealApproach);
            Send(Get(SintaId), sintaMealApproach);
            void Send(OfficeNpcAgent colleague, Transform destination)
            {
                if (colleague != null && destination != null && !colleague.StoryMoveRequested)
                {
                    colleague.SetStorySeated(false);
                    colleague.TrySetStoryDestination(destination.position, 1.6f);
                }
            }
        }

        // Legacy chapter-only persistence seam. The menu and automatic checkpoints use
        // GameSessionController's protected complete-world snapshot instead.
        public void SaveProgress(string path)
        {
            if (!initialized) InitializeNewGame();
            ChapterOneSaveStore.Save(progress, path);
        }

        public void LoadProgress(string path) => RestoreProgress(ChapterOneSaveStore.Load(path));

        private void Awake()
        {
            if (!StartupManagedExternally) InitializeNewGame();
        }

        private void Start()
        {
            sceneStarted = true;
            RefreshInventoryBinding();
            RestoreBossRoomRestriction();
        }

        private void OnDisable()
        {
            CancelMorningTransition();
            if (observedInventory != null) observedInventory.Changed -= OnInventoryChanged;
            observedInventory = null;
            // Remove callbacks before stopping dialogue: the player and NPCs may
            // already have been destroyed when Unity tears down the scene.
            if (dialogue != null)
            {
                dialogue.LineChanged -= OnDialogueLineChanged;
                dialogue.DialogueEnded -= OnDialogueEnded;
            }
            if (genericDialogue != null)
            {
                if (dialogue != null && dialogue.ActiveSequence == genericDialogue)
                    dialogue.Stop();
                else CleanupGenericDialogue();
            }
            if (bossGate != null) bossGate.Blocked -= OnBossGateBlocked;
            if (playerMover != null) playerMover.StopCinematicMotion();
            ReleaseEscortPendingLock();
            ReleaseSeatPendingLock();
            if (playerCamera != null) playerCamera.SetCinematicLookLocked(false);
        }

        private void BindScene()
        {
            actors.Clear();
            foreach (OfficeNpcAgent agent in cast)
            {
                if (agent == null || agent.Profile == null) continue;
                actors[agent.Profile.Id] = agent;
                NpcInteractable interactable = agent.GetComponent<NpcInteractable>();
                if (interactable != null) interactable.SetStoryDirector(this);
            }
            Hold("NPC-021");
            Hold("NPC-022");
            Hold(RakaId);
            Hold(SintaId);
            Hold("NPC-006");
            if (bossGate != null)
            {
                bossGate.Configure(progress, bossGate.GetComponent<Interaction.DoorInteractable>());
                bossGate.Blocked -= OnBossGateBlocked;
                bossGate.Blocked += OnBossGateBlocked;
            }
            if (dialogue != null)
            {
                dialogue.LineChanged -= OnDialogueLineChanged;
                dialogue.LineChanged += OnDialogueLineChanged;
                dialogue.DialogueEnded -= OnDialogueEnded;
                dialogue.DialogueEnded += OnDialogueEnded;
            }
            ResumeOrders(!GameplaySuspended);
            initialized = true;
            RefreshInventoryBinding();
            if (sceneStarted) RestoreBossRoomRestriction();
        }

        private void RefreshInventoryBinding()
        {
            InventoryState current = PlayerInventoryState;
            if (ReferenceEquals(current, observedInventory)) return;
            if (observedInventory != null) observedInventory.Changed -= OnInventoryChanged;
            observedInventory = current;
            if (observedInventory != null) observedInventory.Changed += OnInventoryChanged;
            OnInventoryChanged();
        }

        // Item ownership can change eligibility without advancing any story event.
        private void OnInventoryChanged() => ProgressChanged?.Invoke(progress);

        private void RestoreBossRoomRestriction()
        {
            if (progress != null && progress.Stage >= ChapterOneStage.MeetBoss)
                bossGate?.ClearBackgroundActors();
        }

        private void ResumeOrders(bool allowCinematics = true)
        {
            if (allowCinematics) ApplyChapterTime();
            lastAtDoor = false;
            lastSentToSeat = false;
            escortStarted = false;
            escortAwaitingPath = false;
            escortRequiresManualResume = false;
            playerMovingToSeat = false;
            seatAwaitingPath = false;
            if (progress.Stage >= ChapterOneStage.CollectCarKey)
            {
                SnapActor(Get("NPC-006"), bossChair, player);
                // Moving seats and the SPBU stop own the colleagues after departure.
                if (progress.Story.DepartureDriver != ChapterOneDepartureDriver.Unassigned)
                {
                    Release("NPC-021");
                    Release("NPC-022");
                    return;
                }
                if (progress.Story.SharedMealQuest == StoryQuestStatus.Active
                    || progress.Story.SharedMealQuest == StoryQuestStatus.Completed) SeatMealColleagues();
                else if (allowCinematics) SendColleaguesToLunch();
                Release("NPC-021");
                Release("NPC-022");
                return;
            }
            if (progress.RakaArrived || progress.SintaArrived)
            {
                EnsureBossMeetingSide();
                SnapActor(Get("NPC-006"), BossMeetingSeat(), player);
            }
            else SnapActor(Get("NPC-006"), bossChair, player);

            if (progress.Stage == ChapterOneStage.MorningRoutine) return;
            if (progress.Stage == ChapterOneStage.MeetRooftopWorkers)
            {
                if (allowCinematics && includeMorningRoutine) SendWorkersToRoof();
                return;
            }
            Release("NPC-021");
            Release("NPC-022");

            foreach (string id in new[] { RakaId, SintaId })
            {
                if (!HasArrived(id)) continue;
                SnapActor(Get(id), SeatFor(id), Get("NPC-006")?.transform);
            }

            if (allowCinematics && progress.FirstColleagueId.Length != 0 && !HasArrived(progress.FirstColleagueId))
                SendFirstToBoss(progress.FirstColleagueId);

            if (progress.Stage == ChapterOneStage.FinalBossBriefing)
            {
                if (allowCinematics) EnsureMeetingReady();
                if (progress.PlayerSeated)
                {
                    if (playerSeat != null && playerSeating != null
                        && !playerSeating.SeatAt(playerSeat))
                    {
                        Debug.LogError("Could not restore the player to the meeting seat.", this);
                        return;
                    }
                    bossGate?.GetComponent<Interaction.DoorInteractable>()?.ForceOpen();
                    if (allowCinematics) PlayFinalBriefing();
                }
                return;
            }

            if (!allowCinematics || progress.Stage != ChapterOneStage.EscortLastColleague) return;
            string lastId = progress.LastColleagueId;
            OfficeNpcAgent last = Get(lastId);
            if (last == null) return;
            if (!PlacePlayerNearLastColleagueOnResume(last))
            {
                escortRequiresManualResume = true;
                return;
            }
            if (!progress.LastColleagueIntroComplete) PlayLastIntro(lastId);
            else TryStartEscort(lastId);
        }

        private bool PlacePlayerNearLastColleagueOnResume(OfficeNpcAgent last)
        {
            if (!Application.isPlaying) return true;
            if (player == null || last == null) return false;

            var filter = new NavMeshQueryFilter
            {
                agentTypeID = playerMover != null ? playerMover.NavMeshAgentTypeId : 0,
                areaMask = NavMesh.AllAreas
            };
            if (!NavMesh.SamplePosition(last.transform.position, out NavMeshHit lastHit,
                0.75f, filter)) return false;
            CharacterController controller = player.GetComponent<CharacterController>();
            float radius = controller != null ? controller.radius * 0.9f : 0.27f;
            float height = controller != null ? controller.height : 1.75f;
            // Continue keeps the adjusted menu pose. Relocate only when that pose is no
            // longer supported, clear, or connected to the colleague's route.
            if (NavMesh.SamplePosition(player.position, out NavMeshHit savedHit, 0.3f, filter)
                && Mathf.Abs(savedHit.position.y - player.position.y) <= 0.3f
                && ResumePositionIsClear(player.position, lastHit.position)) return true;
            Vector3[] offsets = { -last.transform.forward, last.transform.right,
                -last.transform.right, last.transform.forward };
            foreach (float distance in new[] { 1.5f, 2f })
            {
                foreach (Vector3 offset in offsets)
                {
                    Vector3 candidate = last.transform.position + offset * distance;
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.75f, filter)
                        || Mathf.Abs(hit.position.y - lastHit.position.y) > 0.5f
                        || Vector3.Distance(hit.position, lastHit.position) < 0.8f
                        || !ResumePositionIsClear(hit.position, lastHit.position)) continue;
                    var route = new NavMeshPath();
                    if (!NavMesh.CalculatePath(hit.position, lastHit.position, filter, route)
                        || route.status != NavMeshPathStatus.PathComplete) continue;
                    float routeLength = 0f;
                    Vector3[] routeCorners = route.corners;
                    for (int corner = 1; corner < routeCorners.Length; corner++)
                        routeLength += Vector3.Distance(routeCorners[corner - 1], routeCorners[corner]);
                    if (routeLength > 6f) continue;

                    Vector3 capsuleBottom = hit.position + Vector3.up * (radius + 0.08f);
                    Vector3 capsuleTop = hit.position + Vector3.up * (height - radius - 0.08f);
                    Collider[] blockers = Physics.OverlapCapsule(capsuleBottom, capsuleTop,
                        radius, ~0, QueryTriggerInteraction.Ignore);
                    bool blocked = false;
                    foreach (Collider blocker in blockers)
                        if (blocker != controller && !blocker.transform.IsChildOf(player))
                        {
                            blocked = true;
                            break;
                        }
                    if (blocked) continue;

                    bool enabled = controller != null && controller.enabled;
                    if (controller != null) controller.enabled = false;
                    player.position = hit.position;
                    if (controller != null) controller.enabled = enabled;
                    Physics.SyncTransforms();
                    return true;
                }
            }
            Debug.LogWarning("No safe NavMesh position for the resumed colleague escort; speak to the colleague to continue.", this);
            return false;

            bool ResumePositionIsClear(Vector3 feet, Vector3 colleagueFeet)
            {
                float step = controller != null ? controller.stepOffset : 0.3f;
                float slope = controller != null ? controller.slopeLimit : 45f;
                if (!SaksiTerakhir.Navigation.TerrainNavigationProbe.TryGetGroundPosition(
                    player, feet, step, slope, out _)) return false;
                foreach (Collider blocker in Physics.OverlapCapsule(
                    feet + Vector3.up * (radius + 0.08f),
                    feet + Vector3.up * (height - radius), radius,
                    ~0, QueryTriggerInteraction.Ignore))
                    if (!blocker.transform.IsChildOf(player)) return false;
                if (Mathf.Abs(feet.y - colleagueFeet.y) > 0.5f) return false;
                var route = new NavMeshPath();
                if (!NavMesh.CalculatePath(feet, colleagueFeet, filter, route)
                    || route.status != NavMeshPathStatus.PathComplete) return false;
                float length = 0f;
                Vector3[] routeCorners = route.corners;
                for (int corner = 1; corner < routeCorners.Length; corner++)
                    length += Vector3.Distance(routeCorners[corner - 1], routeCorners[corner]);
                return length <= 6f;
            }
        }

        public bool TryBeginNpcDialogue(NpcInteractable npc, Transform actor)
        {
            if (GameplaySuspended) return false;
            if (!initialized) InitializeNewGame();
            if (npc == null || actor == null || dialogue == null) return false;
            OfficeNpcAgent agent = npc.GetComponent<OfficeNpcAgent>();
            NpcProfile profile = agent != null ? agent.Profile : null;
            if (profile == null) return false;
            if (dialogue.IsPlaying) return true;
            string id = profile.Id;
            switch (progress.Stage)
            {
                case ChapterOneStage.MeetRooftopWorkers when id == "NPC-021" || id == "NPC-022":
                    Transform roof = id == "NPC-021" ? workerRoofOne : workerRoofTwo;
                    if (includeMorningRoutine && roof != null
                        && Vector3.Distance(agent.transform.position, roof.position) > 3f) return false;
                    return PlayNpcSequence(profile, "chapter.rooftop", false,
                        () =>
                        {
                            Apply(ChapterOneEvent.RooftopDialogueFinished);
                            Release("NPC-021");
                            Release("NPC-022");
                            if (!includeMorningRoutine) NoticeRequested?.Invoke("story.call.incoming");
                        }, Get("NPC-021"), Get("NPC-022"));
                case ChapterOneStage.AnswerBossCall when id == "NPC-006":
                    Apply(ChapterOneEvent.BossCallFinished);
                    return PlayNpcSequence(profile, "chapter.boss_first", false,
                        () => Apply(ChapterOneEvent.FirstBriefingFinished), Get("NPC-006"));
                case ChapterOneStage.MeetBoss when id == "NPC-006":
                    return PlayNpcSequence(profile, "chapter.boss_first", false,
                        () => Apply(ChapterOneEvent.FirstBriefingFinished), Get("NPC-006"));
                case ChapterOneStage.FindColleagues when IsColleague(id):
                    if (progress.FirstColleagueId.Length == 0)
                    {
                        string firstId = id;
                        return PlayNpcSequence(profile,
                            id == RakaId ? "chapter.raka_first" : "chapter.sinta_first", false,
                            () =>
                            {
                                if (Apply(ChapterOneEvent.ColleagueDialogueFinished, firstId))
                                    SendFirstToBoss(firstId);
                            }, Get(id));
                    }
                    if (id == progress.FirstColleagueId) return false;
                    return BeginLastColleague(profile, id);
                case ChapterOneStage.EscortLastColleague when id == progress.LastColleagueId
                    && !escortStarted:
                    if (!progress.LastColleagueIntroComplete)
                    {
                        PlayLastIntro(id);
                        if (dialogue.IsPlaying) escortRequiresManualResume = false;
                        return dialogue.IsPlaying;
                    }
                    bool started = TryStartEscort(id);
                    if (started) escortRequiresManualResume = false;
                    return started;
                case ChapterOneStage.FinalBossBriefing when id == "NPC-006":
                    if (!EnsureMeetingReady()) return false;
                    if (progress.PlayerSeated)
                    {
                        PlayFinalBriefing();
                        return dialogue.IsPlaying;
                    }
                    return PlayNpcSequence(profile, "chapter.boss_seating", false,
                        SeatPlayerForMeeting, Get("NPC-006"), Get(RakaId), Get(SintaId));
                case ChapterOneStage.CollectCarKey when id == "NPC-002":
                    if (!progress.Story.TravelReceiptSubmitted
                        && progress.Story.PurchasedSupplyIds.Count > 0)
                        return PlayNpcSequence(profile, "chapter.receipt_submitted", false, () =>
                        {
                            ChapterOneDocumentRewards.TrySubmitReceipts(this, PlayerInventory);
                            PlayNpcSequence(profile, "chapter.nadia_key", false, ReceiveCarKey, Get("NPC-002"));
                        }, Get("NPC-002"));
                    return PlayNpcSequence(profile, "chapter.nadia_key", false, ReceiveCarKey, Get("NPC-002"));
                default:
                    return false;
            }
        }

        public bool TryBeginGenericNpcDialogue(NpcInteractable npc, Transform actor)
        {
            if (GameplaySuspended) return false;
            if (!initialized) InitializeNewGame();
            if (npc == null || actor == null || actor != player || dialogue == null
                || dialogue.IsPlaying || progress == null
                || progress.Stage == ChapterOneStage.EscortLastColleague
                || playerMovingToSeat || seatAwaitingPath
                || playerMover != null && playerMover.IsActive) return false;

            OfficeNpcAgent agent = npc.GetComponent<OfficeNpcAgent>();
            NpcProfile profile = agent != null ? agent.Profile : null;
            if (profile == null || !actors.TryGetValue(profile.Id, out OfficeNpcAgent registered)
                || registered != agent || profile.DialogueLines == null) return false;
            if (agent.StoryMoveRequested || IsColleague(profile.Id)
                && progress.Stage >= ChapterOneStage.FindColleagues
                && progress.Stage <= ChapterOneStage.FinalBossBriefing) return false;

            var lines = new List<DialogueLine>();
            foreach (string text in profile.DialogueLines)
                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add(new DialogueLine(profile.Id, text.Trim()));
            if (lines.Count == 0) return false;

            genericDialogue = ScriptableObject.CreateInstance<DialogueSequence>();
            genericDialogue.hideFlags = HideFlags.DontSave;
            genericDialogue.Configure("npc.generic." + profile.Id, lines.ToArray());
            genericSpeaker = agent;
            activeParticipants = new[] { agent };
            agent.BeginPlayerInteraction(actor);
            if (dialogue.Play(genericDialogue, null, true)) return true;

            CleanupGenericDialogue();
            activeParticipants = Array.Empty<OfficeNpcAgent>();
            return false;
        }

        public bool TryAnswerBossCall()
        {
            if (GameplaySuspended) return false;
            if (!CallPending || dialogue == null || dialogue.IsPlaying) return false;
            DialogueSequence call = FindSequence("chapter.boss_call");
            if (call == null) return false;
            activeParticipants = Array.Empty<OfficeNpcAgent>();
            return dialogue.Play(call, () => Apply(ChapterOneEvent.BossCallFinished));
        }

        public void OnStoryActorArrived(string npcId)
        {
            if (GameplaySuspended) return;
            if (progress == null || !IsColleague(npcId)) return;
            if (!Apply(ChapterOneEvent.ColleagueArrived, npcId)) return;
            OfficeNpcAgent agent = Get(npcId);
            if (agent != null)
            {
                agent.HoldForStory();
                OfficeNpcAgent bossActor = Get("NPC-006");
                Transform boss = bossActor != null ? bossActor.transform : player;
                if (SeatFor(npcId) != null) SnapActor(agent, SeatFor(npcId), boss);
                else
                {
                    agent.SetStoryFacing(boss);
                    agent.SetStorySeated(true);
                }
            }
            if (progress.BossMeetingSide == 0)
            {
                EnsureBossMeetingSide();
                OfficeNpcAgent boss = Get("NPC-006");
                Transform meetingSeat = BossMeetingSeat();
                if (boss != null && meetingSeat != null)
                {
                    boss.SetStorySeated(false);
                    boss.HoldForStory();
                    // Walk from the chair when its NavMesh position is reachable. The
                    // fallback anchor is only for a chair placed off the walkable mesh.
                    bool pathReady = boss.TrySetStoryDestination(meetingSeat.position, 1.6f);
                    if (!pathReady && bossWalkStart != null && boss.Navigation != null
                        && boss.Navigation.enabled && boss.Navigation.Warp(bossWalkStart.position))
                        pathReady = boss.TrySetStoryDestination(meetingSeat.position, 1.6f);
                    if (!pathReady && meetingApproach != null)
                        boss.TrySetStoryDestination(meetingApproach.position, 1.6f);
                }
            }
            if (progress.Stage == ChapterOneStage.FinalBossBriefing)
            {
                EnsureMeetingReady();
                playerMover?.StopCinematicMotion();
                ReleaseEscortPendingLock();
                playerCamera?.SetCinematicLookLocked(false);
                playerCamera?.ClearStoryFocus();
            }
        }

        public bool TryReachVehicle(Transform actor)
        {
            if (!vehicleDepartureEnabled) return false;
            if (GameplaySuspended) return false;
            return actor != null && actor == player && Apply(ChapterOneEvent.VehicleReached);
        }

        private bool BeginLastColleague(NpcProfile profile, string id)
        {
            string introId = id == RakaId ? "chapter.raka_last_intro" : "chapter.sinta_last_intro";
            DialogueSequence intro = FindNpcSequence(profile, introId);
            if (intro == null || dialogue.IsPlaying) return false;
            OfficeNpcAgent last = Get(id);
            if (!PlaySelectedNpcSequence(intro, false,
                () =>
                {
                    if (Apply(ChapterOneEvent.LastColleagueIntroFinished, id))
                        TryStartEscort(id);
                }, last)) return false;
            if (!Apply(ChapterOneEvent.ColleagueDialogueStarted, id, intro.SelectionKey))
            {
                dialogue.Stop();
                return false;
            }
            lastAtDoor = false;
            lastSentToSeat = false;
            escortStarted = false;
            return true;
        }

        private void SendFirstToBoss(string id)
        {
            OfficeNpcAgent first = Get(id);
            Transform approach = meetingApproach != null ? meetingApproach : SeatFor(id);
            if (first != null && approach != null)
            {
                first.SetStorySeated(false);
                first.TrySetStoryDestination(approach.position, 4f);
            }
        }

        private void Update()
        {
            if (GameplaySuspended) return;
            RefreshInventoryBinding();
            if (progress == null) return;
            if (seatAwaitingPath && progress.Stage == ChapterOneStage.FinalBossBriefing
                && Time.time >= nextSeatPathRetryAt)
                TryStartPlayerSeatPath();
            string firstId = progress.FirstColleagueId;
            OfficeNpcAgent first = Get(firstId);
            if (first != null && first.StoryAtDestination && !HasArrived(firstId))
                OnStoryActorArrived(firstId);
            OfficeNpcAgent boss = Get("NPC-006");
            if (boss != null && boss.StoryAtDestination && !boss.StorySeated
                && progress.BossMeetingSide != 0)
                SnapActor(boss, BossMeetingSeat(), player);

            if (progress.Stage != ChapterOneStage.EscortLastColleague) return;
            if (progress.LastColleagueIntroComplete && !escortStarted
                && !escortRequiresManualResume)
                TryStartEscort(progress.LastColleagueId);
            if (!escortStarted) return;
            OfficeNpcAgent last = Get(progress.LastColleagueId);
            if (last == null) return;
            if (!lastAtDoor && last.StoryAtDestination)
            {
                lastAtDoor = true;
                playerMover?.HoldForDialogue(last.transform, 2f);
                last.SetStoryFacing(player);
            }
            if (dialogue != null)
            {
                bool holdLastLinesUntilDoor = !lastAtDoor && dialogue.IsPlaying
                    && dialogue.ActiveSequence != null
                    && dialogue.LineIndex >= dialogue.ActiveSequence.Lines.Count - 2;
                bool playerApproachingDoorWait = lastAtDoor && playerMover != null
                    && playerMover.IsWaitingForDialogue && !playerMover.AtDialogueWaitPosition;
                dialogue.SetAutoAdvancePaused(last.StoryWaitingForPlayer || holdLastLinesUntilDoor || playerApproachingDoorWait);
            }
            if (lastAtDoor && !lastSentToSeat && progress.WalkDialogueComplete
                && HasArrived(firstId))
            {
                bossGate?.GetComponent<Interaction.DoorInteractable>()?.ForceOpen(last.transform);
                Transform approach = meetingApproach != null
                    ? meetingApproach : SeatFor(progress.LastColleagueId);
                if (approach != null)
                {
                    last.SetStoryFollowDistance(null, 5f);
                    last.TrySetStoryDestination(approach.position, 2.2f);
                    lastSentToSeat = true;
                }
            }
            bool lastFullyInside = bossGate != null
                ? bossGate.IsFullyInsideRoom(last)
                : last.StoryAtDestination;
            if (lastSentToSeat && lastFullyInside)
            {
                playerMover?.StopCinematicMotion();
                playerCamera?.SetCinematicLookLocked(false);
                playerCamera?.ClearStoryFocus();
            }
            Transform arrivalApproach = meetingApproach != null
                ? meetingApproach : SeatFor(progress.LastColleagueId);
            bool crossedIntoMeeting = arrivalApproach != null
                && Vector3.Distance(last.transform.position, arrivalApproach.position) <= 1.75f;
            if (lastSentToSeat && lastFullyInside
                && (last.StoryAtDestination || crossedIntoMeeting)
                && !HasArrived(progress.LastColleagueId))
                OnStoryActorArrived(progress.LastColleagueId);
        }

        private void PlayLastIntro(string id)
        {
            OfficeNpcAgent last = Get(id);
            if (last == null || dialogue == null || dialogue.IsPlaying) return;
            NpcProfile profile = last.Profile;
            string introId = id == RakaId ? "chapter.raka_last_intro" : "chapter.sinta_last_intro";
            DialogueSequence intro = string.IsNullOrEmpty(progress.LastColleagueIntroSelectionKey)
                ? FindNpcSequence(profile, introId)
                : FindSelectedNpcSequence(profile, introId, progress.LastColleagueIntroSelectionKey);
            PlaySelectedNpcSequence(intro, false,
                () =>
                {
                    if (Apply(ChapterOneEvent.LastColleagueIntroFinished, id))
                    {
                        TryStartEscort(id);
                    }
                }, last);
        }

        private bool TryStartEscort(string id)
        {
            if (escortStarted || progress == null || !progress.LastColleagueIntroComplete
                || progress.Stage != ChapterOneStage.EscortLastColleague || dialogue == null
                || dialogue.IsPlaying || player == null || bossDoorWait == null) return false;
            OfficeNpcAgent last = Get(id);
            if (last == null) return false;
            string walkId = id == RakaId ? "chapter.raka_last" : "chapter.sinta_last";
            DialogueSequence sequence = progress.WalkDialogueComplete
                ? null : FindNpcSequence(last.Profile, walkId);
            if (!progress.WalkDialogueComplete && sequence == null)
            {
                escortRequiresManualResume = true;
                return false;
            }
            if (Application.isPlaying && playerMover == null)
            {
                Debug.LogError("Chapter-one escort needs the player cinematic mover.", this);
                escortRequiresManualResume = true;
                return false;
            }

            LockEscortPendingPath();
            if (playerMover != null && !playerMover.BeginFollow(last.transform, 2.2f, 2.5f))
            {
                ReleaseEscortPendingLock();
                playerCamera?.SetCinematicLookLocked(false);
                escortRequiresManualResume = true;
                return false;
            }
            escortAwaitingPath = false;
            playerCamera?.SetCinematicLookLocked(true);
            last.SetStorySeated(false);
            // Pause the colleague before the visible gap reaches the five-metre limit.
            last.SetStoryFollowDistance(player, 4.2f);
            last.TrySetStoryDestination(bossDoorWait.position, 2.2f);
            activeParticipants = new[] { last };
            escortStarted = true;
            if (progress.WalkDialogueComplete) return true;
            if (dialogue.Play(sequence, () => Apply(ChapterOneEvent.WalkDialogueFinished, id), true))
                return true;

            escortStarted = false;
            last.HoldForStory(player);
            playerMover?.StopCinematicMotion();
            ReleaseEscortPendingLock();
            playerCamera?.SetCinematicLookLocked(false);
            return false;
        }

        private void LockEscortPendingPath()
        {
            if (escortAwaitingPath) return;
            escortAwaitingPath = true;
            player?.GetComponent<PlayerController>()?.SetCinematicMovementLocked(true);
            playerCamera?.SetCinematicLookLocked(true);
        }

        private void ReleaseEscortPendingLock()
        {
            if (!escortAwaitingPath) return;
            escortAwaitingPath = false;
            if (player == null) return;
            PlayerController movement = player.GetComponent<PlayerController>();
            if (movement != null) movement.SetCinematicMovementLocked(false);
        }

        private bool EnsureBossMeetingSide()
        {
            if (progress == null) return false;
            if (progress.BossMeetingSide != 0) return true;
            return Apply(ChapterOneEvent.BossMovedToMeeting,
                UnityEngine.Random.value < 0.5f ? "left" : "right");
        }

        private Transform BossMeetingSeat() => progress != null && progress.BossMeetingSide == 1
            ? bossMeetingLeft : bossMeetingRight;

        private bool EnsureMeetingReady()
        {
            if (progress == null || progress.Stage != ChapterOneStage.FinalBossBriefing
                || !EnsureBossMeetingSide()) return false;
            if (progress.MeetingAssembled) return true;
            SnapActor(Get("NPC-006"), BossMeetingSeat(), player);
            SnapActor(Get(RakaId), rakaSeat, Get("NPC-006")?.transform);
            SnapActor(Get(SintaId), sintaSeat, Get("NPC-006")?.transform);
            return Apply(ChapterOneEvent.MeetingAssembled);
        }

        private void SeatPlayerForMeeting()
        {
            if (progress == null || progress.Stage != ChapterOneStage.FinalBossBriefing
                || playerMovingToSeat) return;
            if (progress.PlayerSeated)
            {
                PlayFinalBriefing();
                return;
            }
            playerMovingToSeat = true;
            playerCamera?.SetCinematicLookLocked(true);
            playerCamera?.SetStoryFocus(Get("NPC-006")?.transform);
            bossGate?.GetComponent<Interaction.DoorInteractable>()?.ForceOpen();
            if (playerMover == null && !Application.isPlaying)
            {
                CompletePlayerSeating();
                return;
            }
            TryStartPlayerSeatPath();
        }

        private void TryStartPlayerSeatPath()
        {
            Transform approach = meetingApproach != null ? meetingApproach : playerSeat;
            PlayerController movement = player != null ? player.GetComponent<PlayerController>() : null;
            if (seatAwaitingPath) movement?.SetCinematicMovementLocked(false);
            if (TryBeginPlayerSeatApproach(approach))
            {
                seatAwaitingPath = false;
                return;
            }
            if (!seatAwaitingPath)
                Debug.LogWarning("Player seating path is unavailable; retrying without skipping the walk.", this);
            seatAwaitingPath = true;
            nextSeatPathRetryAt = Time.time + 1f;
            movement?.SetCinematicMovementLocked(true);
        }

        private bool TryBeginPlayerSeatApproach(Transform approach)
        {
            if (playerMover == null || approach == null) return false;
            if (playerMover.BeginMoveTo(approach, 1.6f, 0.3f, CompletePlayerSeating)) return true;
            if (playerSeat == null) return false;

            // Furniture can separate the two sofa aisles into different NavMesh
            // islands. Approach the same seat from the other end when needed.
            Vector3 side = Vector3.ProjectOnPlane(playerSeat.right, Vector3.up);
            if (side.sqrMagnitude < 0.0001f) return false;
            side.Normalize();
            Vector3 offset = approach.position - playerSeat.position;
            Vector3 alternate = approach.position - 2f * Vector3.Dot(offset, side) * side;
            if ((alternate - approach.position).sqrMagnitude < 0.01f) return false;
            return playerMover.BeginMoveTo(alternate, 1.6f, 0.3f, CompletePlayerSeating);
        }

        private void ReleaseSeatPendingLock()
        {
            if (!seatAwaitingPath) return;
            seatAwaitingPath = false;
            if (player == null) return;
            PlayerController movement = player.GetComponent<PlayerController>();
            if (movement != null) movement.SetCinematicMovementLocked(false);
        }

        private void CompletePlayerSeating()
        {
            playerMovingToSeat = false;
            ReleaseSeatPendingLock();
            if (playerSeat != null && playerSeating != null && !playerSeating.SeatAt(playerSeat))
            {
                Debug.LogError("Player could not sit at the chapter-one meeting seat.", this);
                playerCamera?.SetCinematicLookLocked(false);
                return;
            }
            if (!Apply(ChapterOneEvent.PlayerSeated)) return;
            PlayFinalBriefing();
        }

        private void PlayFinalBriefing()
        {
            OfficeNpcAgent boss = Get("NPC-006");
            if (boss == null || dialogue == null || dialogue.IsPlaying) return;
            PlayNpcSequence(boss.Profile, "chapter.boss_final", false,
                () =>
                {
                    if (!ChapterOneDocumentRewards.GrantBriefingDocuments(PlayerInventory))
                    {
                        NoticeRequested?.Invoke("inventory.unavailable");
                        return;
                    }
                    if (Apply(ChapterOneEvent.FinalBriefingFinished))
                    {
                        playerSeating?.StandUp();
                        playerCamera?.SetCinematicLookLocked(false);
                        playerCamera?.ClearStoryFocus();
                        ApplyChapterTime();
                        SendColleaguesToLunch();
                    }
                }, boss, Get(RakaId), Get(SintaId));
        }

        private static void SnapActor(OfficeNpcAgent agent, Transform anchor, Transform face)
        {
            if (agent == null || anchor == null) return;
            agent.HoldForStory(face);
            NavMeshAgent navigation = agent.Navigation;
            if (navigation != null && navigation.enabled && navigation.isOnNavMesh)
            {
                // Keep the agent's next standing point on the mesh. Warp can
                // project a furniture seat onto an aisle, so it is not the seated pose.
                navigation.Warp(anchor.position);
            }
            agent.SetStorySeated(true);
            agent.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        private bool PlayNpcSequence(NpcProfile profile, string sequenceId, bool automatic,
            Action completed, params OfficeNpcAgent[] participants)
        {
            DialogueSequence sequence = FindNpcSequence(profile, sequenceId);
            return PlaySelectedNpcSequence(sequence, automatic, completed, participants);
        }

        // Select conditions once, before progress-changing side effects. Rechecking
        // a selected variant after recording a new event can invalidate that variant.
        private bool PlaySelectedNpcSequence(DialogueSequence sequence, bool automatic,
            Action completed, params OfficeNpcAgent[] participants)
        {
            if (sequence == null || dialogue == null || dialogue.IsPlaying
                || !sequence.Validate(out _)) return false;
            activeParticipants = participants;
            foreach (OfficeNpcAgent participant in participants)
                participant?.HoldForStory();
            return dialogue.Play(sequence, completed, automatic);
        }

        private DialogueSequence FindNpcSequence(NpcProfile profile, string id)
        {
            DialogueSequence sequence = profile != null && profile.StoryDialogues != null
                ? FindEligibleSequence(profile.StoryDialogues, id, out _) : null;
            if (sequence != null) return sequence;
            sequence = FindEligibleSequence(sequences, id, out bool assigned);
            if (!assigned) Debug.LogError($"NPC {profile?.DisplayName ?? "unknown"} lacks story dialogue {id}.", this);
            return sequence;
        }

        private DialogueSequence FindSequence(string id)
        {
            DialogueSequence sequence = FindEligibleSequence(sequences, id, out bool assigned);
            if (!assigned) Debug.LogError($"Chapter one dialogue {id} is not assigned.", this);
            return sequence;
        }

        private DialogueSequence FindEligibleSequence(DialogueSequence[] candidates, string id, out bool assigned)
        {
            assigned = false;
            foreach (DialogueSequence sequence in candidates)
            {
                if (sequence == null || sequence.Id != id) continue;
                assigned = true;
                if (sequence.MeetsConditions(progress, PlayerInventoryState)) return sequence;
            }
            return null;
        }

        private DialogueSequence FindSelectedNpcSequence(NpcProfile profile, string id, string selectionKey)
        {
            if (profile?.StoryDialogues != null)
            {
                foreach (DialogueSequence sequence in profile.StoryDialogues)
                    if (sequence != null && sequence.Id == id && sequence.SelectionKey == selectionKey)
                        return sequence;
            }
            foreach (DialogueSequence sequence in sequences)
                if (sequence != null && sequence.Id == id && sequence.SelectionKey == selectionKey)
                    return sequence;
            Debug.LogError($"Saved dialogue branch {id} is no longer assigned to this NPC.", this);
            return null;
        }

        private void OnDialogueLineChanged(DialogueSequence sequence, int lineIndex)
        {
            if (sequence == null || lineIndex < 0 || lineIndex >= sequence.Lines.Count) return;
            string speakerId = sequence.Lines[lineIndex].SpeakerId;
            Transform speaker = speakerId == "PLAYER" ? player : Get(speakerId)?.transform;
            if (activeParticipants.Length == 0)
                playerCamera?.ClearStoryFocus();
            else if (speaker != null && speaker != player)
                playerCamera?.SetStoryFocus(speaker);
            else if (activeParticipants.Length > 0)
            {
                foreach (OfficeNpcAgent participant in activeParticipants)
                {
                    if (participant == null) continue;
                    playerCamera?.SetStoryFocus(participant.transform);
                    break;
                }
            }
            foreach (OfficeNpcAgent participant in activeParticipants)
            {
                if (participant == null) continue;
                participant.SetStoryFacing(participant.transform == speaker ? player : speaker);
            }
        }

        private void OnDialogueEnded()
        {
            CleanupGenericDialogue();
            activeParticipants = Array.Empty<OfficeNpcAgent>();
            if (escortStarted && progress != null
                && progress.Stage == ChapterOneStage.EscortLastColleague)
                playerCamera?.SetStoryFocus(Get(progress.LastColleagueId)?.transform);
            else playerCamera?.ClearStoryFocus();
        }

        private void CleanupGenericDialogue()
        {
            if (genericSpeaker != null) genericSpeaker.EndPlayerInteraction();
            genericSpeaker = null;
            if (genericDialogue == null) return;
            if (Application.isPlaying) Destroy(genericDialogue);
            else DestroyImmediate(genericDialogue);
            genericDialogue = null;
        }

        private void OnBossGateBlocked(Transform actor)
        {
            if (GameplaySuspended) return;
            if (actor != player || dialogue == null || dialogue.IsPlaying || progress == null
                || progress.Stage != ChapterOneStage.FindColleagues
                && progress.Stage != ChapterOneStage.EscortLastColleague) return;
            string id = !progress.RakaMet ? "chapter.boss_scold_raka"
                : !progress.SintaMet ? "chapter.boss_scold_sinta" : "chapter.boss_scold_wait";
            DialogueSequence sequence = FindSequence(id);
            if (sequence != null) dialogue.Play(sequence, null);
        }

        private bool Apply(ChapterOneEvent kind, string id = "", string dialogueSelectionKey = "")
        {
            if (progress == null || !progress.TryApply(kind, id, dialogueSelectionKey)) return false;
            if (kind == ChapterOneEvent.BossCallFinished) RestoreBossRoomRestriction();
            PublishProgress();
            return true;
        }

        private void ReceiveCarKey()
        {
            if (inventory == null || player == null) return;
            InventoryPanelController itemInventory = player.GetComponent<InventoryPanelController>();
            if (itemInventory == null) return;
            ChapterOneDocumentRewards.TrySubmitReceipts(this, itemInventory);
            InventoryEntry addedKey = null;
            if (!itemInventory.HasItem(CarKeyInventory.CarKeyId))
            {
                ItemDefinition keyDefinition = itemInventory.FindDefinition(CarKeyInventory.CarKeyId);
                if (keyDefinition == null) return;
                addedKey = keyDefinition.CreateEntry();
                // Quest completion and its autosave must see the physical key too.
                // If the bag is full, Nadia can offer the same conversation again.
                if (!itemInventory.TryAdd(addedKey)) return;
            }
            if (!inventory.TryAdd(CarKeyInventory.CarKeyId))
            {
                if (addedKey != null) itemInventory.Remove(addedKey);
                return;
            }
            PublishProgress();
        }

        private void PublishProgress()
        {
            bossGate?.RefreshBarrier();
            if (Application.isPlaying && !string.IsNullOrEmpty(SessionSavePathOverride))
            {
                try { ChapterOneSaveStore.Save(progress, SessionSavePathOverride); }
                catch (Exception error) { Debug.LogError($"Chapter one test save failed: {error.Message}", this); }
            }
            ProgressChanged?.Invoke(progress);
            NoticeRequested?.Invoke("story.quest.updated");
        }

        private OfficeNpcAgent Get(string id) => !string.IsNullOrEmpty(id)
            && actors.TryGetValue(id, out OfficeNpcAgent actor) ? actor : null;
        private void Hold(string id) => Get(id)?.HoldForStory();
        private void Release(string id) => Get(id)?.ReleaseStoryHold();
        private Transform SeatFor(string id) => id == RakaId ? rakaSeat : sintaSeat;
        private bool HasArrived(string id) => id == RakaId ? progress.RakaArrived : progress.SintaArrived;
        private static bool IsColleague(string id) => id == RakaId || id == SintaId;
    }
}
