using System;
using System.Collections.Generic;
using System.IO;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using UnityEngine;
using UnityEngine.AI;

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

        private readonly Dictionary<string, OfficeNpcAgent> actors = new Dictionary<string, OfficeNpcAgent>();
        private ChapterOneProgress progress;
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

        public event Action<ChapterOneProgress> ProgressChanged;
        public event Action<string> NoticeRequested;

        public ChapterOneProgress Progress => progress;
        public bool CallPending => progress != null && progress.Stage == ChapterOneStage.AnswerBossCall;
        public QuestDefinition CurrentQuest => progress != null && (int)progress.Stage < quests.Length
            ? quests[(int)progress.Stage] : null;
        public CarKeyInventory Inventory => inventory;
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
            // Normal gameplay is session-only: older save files are ignored, and each
            // fresh run begins at the rooftop. The override is used only by PlayMode tests.
            progress = testPersistenceEnabled
                ? ChapterOneSaveStore.Load(SessionSavePathOverride)
                : new ChapterOneProgress();
            inventory = new CarKeyInventory(progress);
            BindScene();
            ProgressChanged?.Invoke(progress);
            if (!hadSave) NoticeRequested?.Invoke("story.quest.started");
        }

        public void RestoreProgress(ChapterOneProgress snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
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

        private void Awake() => InitializeNewGame();

        private void Start()
        {
            sceneStarted = true;
            RestoreBossRoomRestriction();
        }

        private void OnDisable()
        {
            if (genericDialogue != null)
            {
                if (dialogue != null && dialogue.ActiveSequence == genericDialogue)
                    dialogue.Stop();
                else CleanupGenericDialogue();
            }
            if (dialogue != null)
            {
                dialogue.LineChanged -= OnDialogueLineChanged;
                dialogue.DialogueEnded -= OnDialogueEnded;
            }
            if (bossGate != null) bossGate.Blocked -= OnBossGateBlocked;
            playerMover?.StopCinematicMotion();
            ReleaseEscortPendingLock();
            ReleaseSeatPendingLock();
            playerCamera?.SetCinematicLookLocked(false);
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
            ResumeOrders();
            initialized = true;
            if (sceneStarted) RestoreBossRoomRestriction();
        }

        private void RestoreBossRoomRestriction()
        {
            if (progress != null && progress.Stage >= ChapterOneStage.MeetBoss)
                bossGate?.ClearBackgroundActors();
        }

        private void ResumeOrders()
        {
            lastAtDoor = false;
            lastSentToSeat = false;
            escortStarted = false;
            escortAwaitingPath = false;
            escortRequiresManualResume = false;
            playerMovingToSeat = false;
            seatAwaitingPath = false;
            if (progress.RakaArrived || progress.SintaArrived)
            {
                EnsureBossMeetingSide();
                SnapActor(Get("NPC-006"), BossMeetingSeat(), player);
            }
            else SnapActor(Get("NPC-006"), bossChair, player);

            if (progress.Stage == ChapterOneStage.MeetRooftopWorkers) return;
            Release("NPC-021");
            Release("NPC-022");

            foreach (string id in new[] { RakaId, SintaId })
            {
                if (!HasArrived(id)) continue;
                SnapActor(Get(id), SeatFor(id), Get("NPC-006")?.transform);
            }

            if (progress.FirstColleagueId.Length != 0 && !HasArrived(progress.FirstColleagueId))
                SendFirstToBoss(progress.FirstColleagueId);

            if (progress.Stage == ChapterOneStage.FinalBossBriefing)
            {
                EnsureMeetingReady();
                if (progress.PlayerSeated)
                {
                    if (playerSeat != null && playerSeating != null
                        && !playerSeating.SeatAt(playerSeat))
                    {
                        Debug.LogError("Could not restore the player to the meeting seat.", this);
                        return;
                    }
                    bossGate?.GetComponent<Interaction.DoorInteractable>()?.ForceOpen();
                    PlayFinalBriefing();
                }
                return;
            }

            if (progress.Stage != ChapterOneStage.EscortLastColleague) return;
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
            Vector3[] offsets = { -last.transform.forward, last.transform.right,
                -last.transform.right, last.transform.forward };
            foreach (float distance in new[] { 1.5f, 2f })
            {
                foreach (Vector3 offset in offsets)
                {
                    Vector3 candidate = last.transform.position + offset * distance;
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.75f, filter)
                        || Mathf.Abs(hit.position.y - lastHit.position.y) > 0.5f
                        || Vector3.Distance(hit.position, lastHit.position) < 0.8f) continue;
                    var route = new NavMeshPath();
                    if (!NavMesh.CalculatePath(hit.position, lastHit.position, filter, route)
                        || route.status != NavMeshPathStatus.PathComplete) continue;
                    float routeLength = 0f;
                    for (int corner = 1; corner < route.corners.Length; corner++)
                        routeLength += Vector3.Distance(route.corners[corner - 1], route.corners[corner]);
                    if (routeLength > 6f) continue;

                    Vector3 capsuleBottom = hit.position + Vector3.up * (radius + 0.08f);
                    Vector3 capsuleTop = hit.position + Vector3.up * (height - radius - 0.08f);
                    Collider[] blockers = Physics.OverlapCapsule(capsuleBottom, capsuleTop,
                        radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    bool blocked = false;
                    foreach (Collider blocker in blockers)
                        if (blocker != controller && !blocker.transform.IsChildOf(player))
                        {
                            blocked = true;
                            break;
                        }
                    if (blocked) continue;

                    if (controller != null) controller.enabled = false;
                    player.position = hit.position;
                    if (controller != null) controller.enabled = true;
                    Physics.SyncTransforms();
                    return true;
                }
            }
            Debug.LogWarning("No safe NavMesh position for the resumed colleague escort; speak to the colleague to continue.", this);
            return false;
        }

        public bool TryBeginNpcDialogue(NpcInteractable npc, Transform actor)
        {
            if (!initialized) InitializeNewGame();
            if (npc == null || npc.GetComponent<OfficeNpcAgent>()?.Profile == null
                || actor == null || dialogue == null) return false;
            if (dialogue.IsPlaying) return true;
            NpcProfile profile = npc.GetComponent<OfficeNpcAgent>().Profile;
            string id = profile.Id;
            switch (progress.Stage)
            {
                case ChapterOneStage.MeetRooftopWorkers when id == "NPC-021" || id == "NPC-022":
                    return PlayNpcSequence(profile, "chapter.rooftop", false,
                        () =>
                        {
                            Apply(ChapterOneEvent.RooftopDialogueFinished);
                            Release("NPC-021");
                            Release("NPC-022");
                            NoticeRequested?.Invoke("story.call.incoming");
                        }, Get("NPC-021"), Get("NPC-022"));
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
                    return PlayNpcSequence(profile, "chapter.nadia_key", false,
                        ReceiveCarKey, Get("NPC-002"));
                default:
                    return false;
            }
        }

        public bool TryBeginGenericNpcDialogue(NpcInteractable npc, Transform actor)
        {
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
            if (!CallPending || dialogue == null || dialogue.IsPlaying) return false;
            DialogueSequence call = FindSequence("chapter.boss_call");
            if (call == null) return false;
            activeParticipants = Array.Empty<OfficeNpcAgent>();
            return dialogue.Play(call, () => Apply(ChapterOneEvent.BossCallFinished));
        }

        public void OnStoryActorArrived(string npcId)
        {
            if (progress == null || !IsColleague(npcId)) return;
            if (!Apply(ChapterOneEvent.ColleagueArrived, npcId)) return;
            OfficeNpcAgent agent = Get(npcId);
            if (agent != null)
            {
                agent.HoldForStory();
                Transform boss = Get("NPC-006") != null ? Get("NPC-006").transform : player;
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
            return actor != null && actor == player && Apply(ChapterOneEvent.VehicleReached);
        }

        private bool BeginLastColleague(NpcProfile profile, string id)
        {
            string introId = id == RakaId ? "chapter.raka_last_intro" : "chapter.sinta_last_intro";
            if (FindNpcSequence(profile, introId) == null || dialogue.IsPlaying) return false;
            if (!Apply(ChapterOneEvent.ColleagueDialogueStarted, id)) return false;
            OfficeNpcAgent last = Get(id);
            lastAtDoor = false;
            lastSentToSeat = false;
            escortStarted = false;
            return PlayNpcSequence(profile, introId, false,
                () =>
                {
                    if (Apply(ChapterOneEvent.LastColleagueIntroFinished, id))
                    {
                        TryStartEscort(id);
                    }
                }, last);
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
                last.SetStoryFacing(player);
            }
            if (dialogue != null)
            {
                bool holdLastLinesUntilDoor = !lastAtDoor && dialogue.IsPlaying
                    && dialogue.ActiveSequence != null
                    && dialogue.LineIndex >= dialogue.ActiveSequence.Lines.Count - 2;
                dialogue.SetAutoAdvancePaused(last.StoryWaitingForPlayer || holdLastLinesUntilDoor);
            }
            if (lastAtDoor && !lastSentToSeat && progress.WalkDialogueComplete
                && HasArrived(firstId))
            {
                bossGate?.GetComponent<Interaction.DoorInteractable>()?.ForceOpen();
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
            PlayNpcSequence(profile, introId, false,
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
            player?.GetComponent<PlayerController>()?.SetCinematicMovementLocked(false);
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
            if (playerMover != null && approach != null
                && playerMover.BeginMoveTo(approach, 1.6f, 0.3f, CompletePlayerSeating))
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

        private void ReleaseSeatPendingLock()
        {
            if (!seatAwaitingPath) return;
            seatAwaitingPath = false;
            player?.GetComponent<PlayerController>()?.SetCinematicMovementLocked(false);
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
                    if (Apply(ChapterOneEvent.FinalBriefingFinished))
                    {
                        playerSeating?.StandUp();
                        playerCamera?.SetCinematicLookLocked(false);
                        playerCamera?.ClearStoryFocus();
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
                if (!navigation.Warp(anchor.position))
                    agent.transform.position = anchor.position;
            }
            else agent.transform.position = anchor.position;
            agent.transform.rotation = anchor.rotation;
            agent.SetStorySeated(true);
        }

        private bool PlayNpcSequence(NpcProfile profile, string sequenceId, bool automatic,
            Action completed, params OfficeNpcAgent[] participants)
        {
            DialogueSequence sequence = FindNpcSequence(profile, sequenceId);
            if (sequence == null) return false;
            activeParticipants = participants;
            foreach (OfficeNpcAgent participant in participants)
                participant?.HoldForStory();
            return dialogue.Play(sequence, completed, automatic);
        }

        private DialogueSequence FindNpcSequence(NpcProfile profile, string id)
        {
            if (profile == null || profile.StoryDialogues == null) return null;
            foreach (DialogueSequence sequence in profile.StoryDialogues)
                if (sequence != null && sequence.Id == id) return sequence;
            Debug.LogError($"NPC {profile.DisplayName} lacks story dialogue {id}.", profile);
            return null;
        }

        private DialogueSequence FindSequence(string id)
        {
            foreach (DialogueSequence sequence in sequences)
                if (sequence != null && sequence.Id == id) return sequence;
            Debug.LogError($"Chapter one dialogue {id} is not assigned.", this);
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
            genericSpeaker?.EndPlayerInteraction();
            genericSpeaker = null;
            if (genericDialogue == null) return;
            if (Application.isPlaying) Destroy(genericDialogue);
            else DestroyImmediate(genericDialogue);
            genericDialogue = null;
        }

        private void OnBossGateBlocked(Transform actor)
        {
            if (actor != player || dialogue == null || dialogue.IsPlaying || progress == null
                || progress.Stage != ChapterOneStage.FindColleagues
                && progress.Stage != ChapterOneStage.EscortLastColleague) return;
            string id = !progress.RakaMet ? "chapter.boss_scold_raka"
                : !progress.SintaMet ? "chapter.boss_scold_sinta" : "chapter.boss_scold_wait";
            DialogueSequence sequence = FindSequence(id);
            if (sequence != null) dialogue.Play(sequence, null);
        }

        private bool Apply(ChapterOneEvent kind, string id = "")
        {
            if (progress == null || !progress.TryApply(kind, id)) return false;
            if (kind == ChapterOneEvent.BossCallFinished) RestoreBossRoomRestriction();
            PublishProgress();
            return true;
        }

        private void ReceiveCarKey()
        {
            if (inventory != null && inventory.TryAdd(CarKeyInventory.CarKeyId))
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
