using System;
using System.Collections.Generic;
using SaksiTerakhir.Npc;
using UnityEngine;

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
        [SerializeField] private QuestDefinition[] quests = Array.Empty<QuestDefinition>();
        [SerializeField] private DialogueSequence[] sequences = Array.Empty<DialogueSequence>();

        private readonly Dictionary<string, OfficeNpcAgent> actors = new Dictionary<string, OfficeNpcAgent>();
        private ChapterOneProgress progress;
        private bool initialized;
        private bool lastAtDoor;
        private bool lastSentToSeat;
        private OfficeNpcAgent[] activeParticipants = Array.Empty<OfficeNpcAgent>();

        public event Action<ChapterOneProgress> ProgressChanged;
        public event Action<string> NoticeRequested;

        public ChapterOneProgress Progress => progress;
        public bool CallPending => progress != null && progress.Stage == ChapterOneStage.AnswerBossCall;
        public QuestDefinition CurrentQuest => progress != null && (int)progress.Stage < quests.Length
            ? quests[(int)progress.Stage] : null;

        public void ConfigureScene(StoryDialogueController storyDialogue, Transform playerTransform,
            BossOfficeGate gate, OfficeNpcAgent[] storyCast, Transform rakaSeatAnchor,
            Transform sintaSeatAnchor, Transform doorWaitAnchor, QuestDefinition[] questAssets,
            DialogueSequence[] dialogueAssets)
        {
            dialogue = storyDialogue;
            player = playerTransform;
            bossGate = gate;
            cast = storyCast ?? Array.Empty<OfficeNpcAgent>();
            rakaSeat = rakaSeatAnchor;
            sintaSeat = sintaSeatAnchor;
            bossDoorWait = doorWaitAnchor;
            quests = questAssets ?? Array.Empty<QuestDefinition>();
            sequences = dialogueAssets ?? Array.Empty<DialogueSequence>();
            initialized = false;
        }

        public void InitializeNewGame()
        {
            if (initialized) return;
            progress = new ChapterOneProgress();
            BindScene();
            ProgressChanged?.Invoke(progress);
            NoticeRequested?.Invoke("story.quest.started");
        }

        private void Awake() => InitializeNewGame();

        private void OnDisable()
        {
            if (dialogue != null) dialogue.LineChanged -= OnDialogueLineChanged;
            if (bossGate != null) bossGate.Blocked -= OnBossGateBlocked;
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
            }
            initialized = true;
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
                    bossGate?.ClearBackgroundActors();
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
                case ChapterOneStage.FinalBossBriefing when id == "NPC-006":
                    return PlayNpcSequence(profile, "chapter.boss_final", false,
                        () => Apply(ChapterOneEvent.FinalBriefingFinished),
                        Get("NPC-006"), Get(RakaId), Get(SintaId));
                case ChapterOneStage.CollectCarKey when id == "NPC-002":
                    return PlayNpcSequence(profile, "chapter.nadia_key", false,
                        () => Apply(ChapterOneEvent.CarKeyReceived), Get("NPC-002"));
                default:
                    return false;
            }
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
                agent.SetStoryFacing(Get("NPC-006") != null ? Get("NPC-006").transform : player);
            }
        }

        public bool TryReachVehicle(Transform actor)
        {
            return actor != null && actor == player && Apply(ChapterOneEvent.VehicleReached);
        }

        private bool BeginLastColleague(NpcProfile profile, string id)
        {
            string sequenceId = id == RakaId ? "chapter.raka_last" : "chapter.sinta_last";
            DialogueSequence sequence = FindNpcSequence(profile, sequenceId);
            if (sequence == null || dialogue.IsPlaying) return false;
            if (!Apply(ChapterOneEvent.ColleagueDialogueStarted, id)) return false;
            OfficeNpcAgent last = Get(id);
            activeParticipants = new[] { last };
            lastAtDoor = false;
            lastSentToSeat = false;
            if (last != null)
            {
                last.HoldForStory();
                last.SetStoryFollowDistance(player, 5f);
                if (bossDoorWait != null) last.TrySetStoryDestination(bossDoorWait.position, 2.2f);
            }
            return dialogue.Play(sequence,
                () => Apply(ChapterOneEvent.WalkDialogueFinished, id), true);
        }

        private void SendFirstToBoss(string id)
        {
            OfficeNpcAgent first = Get(id);
            Transform seat = SeatFor(id);
            if (first != null && seat != null)
                first.TrySetStoryDestination(seat.position, 4f);
        }

        private void Update()
        {
            if (progress == null) return;
            string firstId = progress.FirstColleagueId;
            OfficeNpcAgent first = Get(firstId);
            if (first != null && first.StoryAtDestination && !HasArrived(firstId))
                OnStoryActorArrived(firstId);

            if (progress.Stage != ChapterOneStage.EscortLastColleague) return;
            OfficeNpcAgent last = Get(progress.LastColleagueId);
            if (last == null) return;
            dialogue?.SetAutoAdvancePaused(last.StoryWaitingForPlayer);
            if (!lastAtDoor && last.StoryAtDestination)
            {
                lastAtDoor = true;
                last.SetStoryFacing(player);
            }
            if (lastAtDoor && !lastSentToSeat && progress.WalkDialogueComplete
                && HasArrived(firstId))
            {
                bossGate?.GetComponent<Interaction.DoorInteractable>()?.ForceOpen();
                Transform seat = SeatFor(progress.LastColleagueId);
                if (seat != null)
                {
                    last.SetStoryFollowDistance(null, 5f);
                    last.TrySetStoryDestination(seat.position, 2.2f);
                    lastSentToSeat = true;
                }
            }
            if (lastSentToSeat && last.StoryAtDestination && !HasArrived(progress.LastColleagueId))
                OnStoryActorArrived(progress.LastColleagueId);
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
            foreach (OfficeNpcAgent participant in activeParticipants)
            {
                if (participant == null) continue;
                participant.SetStoryFacing(participant.transform == speaker ? player : speaker);
            }
        }

        private void OnBossGateBlocked(Transform actor)
        {
            if (actor != player || dialogue == null || dialogue.IsPlaying) return;
            string id = !progress.RakaMet ? "chapter.boss_scold_raka"
                : !progress.SintaMet ? "chapter.boss_scold_sinta" : "chapter.boss_scold_wait";
            DialogueSequence sequence = FindSequence(id);
            if (sequence != null) dialogue.Play(sequence, null);
        }

        private bool Apply(ChapterOneEvent kind, string id = "")
        {
            if (progress == null || !progress.TryApply(kind, id)) return false;
            ProgressChanged?.Invoke(progress);
            NoticeRequested?.Invoke("story.quest.updated");
            return true;
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
