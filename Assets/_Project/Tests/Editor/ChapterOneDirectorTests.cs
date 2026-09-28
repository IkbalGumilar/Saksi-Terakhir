using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using UnityEngine;
using UnityEngine.AI;

namespace SaksiTerakhir.Tests
{
    public sealed class ChapterOneDirectorTests
    {
        private readonly List<Object> created = new List<Object>();
        private readonly Dictionary<string, NpcInteractable> cast = new Dictionary<string, NpcInteractable>();
        private readonly Dictionary<string, DialogueSequence> sequences = new Dictionary<string, DialogueSequence>();
        private ChapterOneDirector director;
        private StoryDialogueController dialogue;
        private Transform player;

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
                if (item != null) Object.DestroyImmediate(item);
            created.Clear();
            cast.Clear();
            sequences.Clear();
        }

        [Test]
        public void RooftopWorkersShareOneOpeningAndBossCallCannotStartEarly()
        {
            CreateChapter();
            Assert.That(director.TryAnswerBossCall(), Is.False);
            Assert.That(director.TryBeginNpcDialogue(cast["NPC-021"], player), Is.True);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.rooftop"));
            Assert.That(director.TryBeginNpcDialogue(cast["NPC-022"], player), Is.True);
            FinishCurrentDialogue();
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.AnswerBossCall));
            Assert.That(director.TryAnswerBossCall(), Is.True);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_call"));
            FinishCurrentDialogue();
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.MeetBoss));
            Assert.That(director.TryBeginNpcDialogue(cast["NPC-006"], player), Is.True);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_first"));
            FinishCurrentDialogue();
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
        }

        [Test]
        public void BossPhoneCallDoesNotTurnCameraTowardTheRemoteOffice()
        {
            CreateChapter();
            var cameraObject = new GameObject("Phone camera probe");
            cameraObject.SetActive(false);
            PlayerCameraController camera = cameraObject.AddComponent<PlayerCameraController>();
            created.Add(cameraObject);
            typeof(ChapterOneDirector).GetField("playerCamera",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(director, camera);

            director.TryBeginNpcDialogue(cast["NPC-021"], player);
            FinishCurrentDialogue();
            Assert.That(director.TryAnswerBossCall(), Is.True);
            typeof(StoryDialogueController).GetMethod("Advance",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(dialogue, null);
            Assert.That(dialogue.CurrentLine.SpeakerId, Is.EqualTo("NPC-006"));
            Assert.That(camera.StoryFocusTarget, Is.Null);
        }

        [Test]
        public void RestoringCompletedCallDoesNotReplayItOrTheRooftopConversation()
        {
            CreateChapter();
            ChapterOneProgress loaded = new ChapterOneProgress();
            loaded.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            loaded.TryApply(ChapterOneEvent.BossCallFinished);
            director.RestoreProgress(loaded);

            Assert.That(dialogue.IsPlaying, Is.False);
            Assert.That(director.CallPending, Is.False);
            Assert.That(director.TryAnswerBossCall(), Is.False);
            Assert.That(director.TryBeginNpcDialogue(cast["NPC-021"], player), Is.False);
            Assert.That(director.TryBeginNpcDialogue(cast["NPC-006"], player), Is.True);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_first"));
        }

        [TestCase("NPC-003", "NPC-004", "chapter.raka_first", "chapter.sinta_last")]
        [TestCase("NPC-004", "NPC-003", "chapter.sinta_first", "chapter.raka_last")]
        public void ColleaguesUseDifferentBranchesAndNadiaGivesOneKey(string first,
            string last, string firstDialogue, string lastDialogue)
        {
            CreateChapter();
            ReachColleagueSearch();
            Assert.That(director.TryBeginNpcDialogue(cast[first], player), Is.True);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo(firstDialogue));
            FinishCurrentDialogue();
            Assert.That(director.Progress.FirstColleagueId, Is.EqualTo(first));
            Assert.That(director.TryBeginNpcDialogue(cast[last], player), Is.True);
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo(lastDialogue + "_intro"));
            FinishCurrentDialogue();
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo(lastDialogue));
            FinishCurrentDialogue();
            director.OnStoryActorArrived(first);
            director.OnStoryActorArrived(last);
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.FinalBossBriefing));
            director.TryBeginNpcDialogue(cast["NPC-006"], player);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_seating"));
            FinishCurrentDialogue();
            Assert.That(director.Progress.PlayerSeated, Is.True);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_final"));
            FinishCurrentDialogue();
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.CollectCarKey));
            director.TryBeginNpcDialogue(cast["NPC-002"], player);
            FinishCurrentDialogue();
            Assert.That(director.Progress.HasCarKey, Is.True);
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(director.TryBeginNpcDialogue(cast["NPC-002"], player), Is.False);
            Assert.That(director.TryReachVehicle(player), Is.True);
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.Complete));
            Assert.That(director.TryReachVehicle(player), Is.False);
        }

        [TestCase("NPC-003", "NPC-004", "chapter.sinta_last_intro")]
        [TestCase("NPC-004", "NPC-003", "chapter.raka_last_intro")]
        public void LastColleagueStaysStationaryUntilTheirShortIntroductionCompletes(string first,
            string last, string introDialogue)
        {
            CreateChapter();
            ReachColleagueSearch();

            Assert.That(director.TryBeginNpcDialogue(cast[first], player), Is.True);
            FinishCurrentDialogue();
            OfficeNpcAgent lastAgent = cast[last].GetComponent<OfficeNpcAgent>();
            Assert.That(director.TryBeginNpcDialogue(cast[last], player), Is.True);

            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo(introDialogue));
            Assert.That(director.Progress.LastColleagueIntroComplete, Is.False);
            Assert.That(lastAgent.CurrentState, Is.EqualTo(NpcState.StoryHeld));
            Assert.That(lastAgent.StoryMoveRequested, Is.False,
                "The last colleague must remain still while the short introduction is playing.");

            FinishCurrentDialogue();

            Assert.That(director.Progress.LastColleagueIntroComplete, Is.True);
            Assert.That(lastAgent.StoryMoveRequested, Is.True,
                "Only completion of the stationary introduction may request the escort route.");
        }

        [Test]
        public void LoadingAfterWalkDialogueDoesNotRepeatTheConversation()
        {
            CreateChapter();
            ChapterOneProgress loaded = new ChapterOneProgress();
            loaded.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            loaded.TryApply(ChapterOneEvent.BossCallFinished);
            loaded.TryApply(ChapterOneEvent.FirstBriefingFinished);
            loaded.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003");
            loaded.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-003");
            loaded.TryApply(ChapterOneEvent.BossMovedToMeeting, "left");
            loaded.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004");
            loaded.TryApply(ChapterOneEvent.LastColleagueIntroFinished, "NPC-004");
            loaded.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004");

            director.RestoreProgress(loaded);

            Assert.That(dialogue.IsPlaying, Is.False);
            Assert.That(cast["NPC-004"].GetComponent<OfficeNpcAgent>().StoryMoveRequested, Is.True);
            Assert.That(director.Progress.WalkDialogueComplete, Is.True);
        }

        [Test]
        public void LoadingAfterPlayerSatResumesFinalBriefingWithoutAnotherInteraction()
        {
            CreateChapter();
            ChapterOneProgress loaded = new ChapterOneProgress();
            loaded.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            loaded.TryApply(ChapterOneEvent.BossCallFinished);
            loaded.TryApply(ChapterOneEvent.FirstBriefingFinished);
            loaded.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003");
            loaded.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-003");
            loaded.TryApply(ChapterOneEvent.BossMovedToMeeting, "right");
            loaded.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004");
            loaded.TryApply(ChapterOneEvent.LastColleagueIntroFinished, "NPC-004");
            loaded.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004");
            loaded.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-004");
            loaded.TryApply(ChapterOneEvent.MeetingAssembled);
            loaded.TryApply(ChapterOneEvent.PlayerSeated);

            director.RestoreProgress(loaded);
            Assert.That(dialogue.IsPlaying, Is.True,
                "The seated player cannot reach the boss's interaction prompt after loading.");
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_final"));
        }

        [Test]
        public void OrdinaryNpcUsesAutomaticDialogueAndReleasesConversationOnCompletion()
        {
            CreateChapter();
            NpcInteractable npc = cast["NPC-002"];
            OfficeNpcAgent agent = npc.GetComponent<OfficeNpcAgent>();

            npc.Interact(player);

            Assert.That(dialogue.IsPlaying, Is.True);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("npc.generic.NPC-002"));
            Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("Selamat datang."));
            Assert.That(agent.CurrentState, Is.EqualTo(NpcState.PlayerConversation));
            Assert.That(dialogue.TryConsumeInteract(), Is.True);
            Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("Selamat datang."));
            FinishCurrentDialogue();
            Assert.That(agent.CurrentState, Is.Not.EqualTo(NpcState.PlayerConversation));
            Assert.That(npc.DialogueActive, Is.False,
                "The old press-to-advance panel must stay hidden during automatic playback.");
        }

        [Test]
        public void StoppingOrdinaryDialogueReleasesNpcAndAllowsAnotherConversation()
        {
            CreateChapter();
            NpcInteractable npc = cast["NPC-002"];
            OfficeNpcAgent agent = npc.GetComponent<OfficeNpcAgent>();
            npc.Interact(player);
            Assert.That(agent.CurrentState, Is.EqualTo(NpcState.PlayerConversation));

            dialogue.Stop();

            Assert.That(agent.CurrentState, Is.Not.EqualTo(NpcState.PlayerConversation));
            npc.Interact(player);
            Assert.That(dialogue.IsPlaying, Is.True);
            Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("Selamat datang."));
        }

        [Test]
        public void OrdinaryNpcCannotInterruptStoryDialogueOrCinematicEscort()
        {
            CreateChapter();
            NpcInteractable worker = cast["NPC-002"];
            Assert.That(director.TryBeginNpcDialogue(cast["NPC-021"], player), Is.True);
            DialogueSequence rooftop = dialogue.ActiveSequence;

            worker.Interact(player);
            Assert.That(dialogue.ActiveSequence, Is.SameAs(rooftop));
            FinishCurrentDialogue();
            director.TryAnswerBossCall();
            FinishCurrentDialogue();
            director.TryBeginNpcDialogue(cast["NPC-006"], player);
            FinishCurrentDialogue();
            director.TryBeginNpcDialogue(cast["NPC-003"], player);
            FinishCurrentDialogue();
            director.TryBeginNpcDialogue(cast["NPC-004"], player);
            FinishCurrentDialogue();
            FinishCurrentDialogue();
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            Assert.That(dialogue.IsPlaying, Is.False);

            worker.Interact(player);
            Assert.That(dialogue.IsPlaying, Is.False,
                "An ordinary chat must not interrupt the automatic walk to the boss.");
            Assert.That(worker.GetComponent<OfficeNpcAgent>().CurrentState,
                Is.Not.EqualTo(NpcState.PlayerConversation));
        }

        [Test]
        public void FoundColleagueCannotBeStoppedByGenericConversation()
        {
            CreateChapter();
            ReachColleagueSearch();
            NpcInteractable raka = cast["NPC-003"];
            Assert.That(director.TryBeginNpcDialogue(raka, player), Is.True);
            FinishCurrentDialogue();
            Assert.That(director.Progress.FirstColleagueId, Is.EqualTo("NPC-003"));

            raka.Interact(player);

            Assert.That(dialogue.IsPlaying, Is.False);
            Assert.That(raka.GetComponent<OfficeNpcAgent>().CurrentState,
                Is.Not.EqualTo(NpcState.PlayerConversation));
        }

        private void CreateChapter()
        {
            string[] ids = { "chapter.rooftop", "chapter.boss_call", "chapter.boss_first",
                "chapter.raka_first", "chapter.sinta_first", "chapter.raka_last_intro",
                "chapter.sinta_last_intro", "chapter.raka_last", "chapter.sinta_last",
                "chapter.boss_seating", "chapter.boss_final", "chapter.nadia_key" };
            foreach (string id in ids)
            {
                DialogueSequence asset = ScriptableObject.CreateInstance<DialogueSequence>();
                asset.Configure(id, new[] { new DialogueLine("PLAYER", "Satu."),
                    new DialogueLine("NPC-006", "Dua.") });
                created.Add(asset);
                sequences.Add(id, asset);
            }
            foreach (string id in new[] { "NPC-021", "NPC-022", "NPC-003", "NPC-004", "NPC-006", "NPC-002" })
            {
                GameObject go = new GameObject(id);
                go.SetActive(false);
                go.AddComponent<NavMeshAgent>();
                OfficeNpcAgent agent = go.AddComponent<OfficeNpcAgent>();
                NpcProfile profile = ScriptableObject.CreateInstance<NpcProfile>();
                profile.Configure(id, id, 30, 2.6f, NpcRole.Worker, false,
                    null, null, new[] { "Selamat datang.", "Ada apa lagi?" }, null);
                profile.SetStoryOffers(null, new List<DialogueSequence>(sequences.Values).ToArray());
                agent.SetProfile(profile);
                NpcInteractable interactable = go.AddComponent<NpcInteractable>();
                created.Add(go);
                created.Add(profile);
                cast.Add(id, interactable);
                go.SetActive(true);
            }
            GameObject playerObject = new GameObject("Player");
            player = playerObject.transform;
            created.Add(playerObject);
            GameObject dialogueObject = new GameObject("Dialogue");
            dialogue = dialogueObject.AddComponent<StoryDialogueController>();
            created.Add(dialogueObject);
            GameObject directorObject = new GameObject("Director");
            directorObject.SetActive(false);
            director = directorObject.AddComponent<ChapterOneDirector>();
            created.Add(directorObject);
            GameObject bossDoorWait = new GameObject("Boss Door Wait");
            created.Add(bossDoorWait);
            director.ConfigureScene(dialogue, player, null,
                new[] { cast["NPC-021"].GetComponent<OfficeNpcAgent>(),
                    cast["NPC-022"].GetComponent<OfficeNpcAgent>(),
                    cast["NPC-003"].GetComponent<OfficeNpcAgent>(),
                    cast["NPC-004"].GetComponent<OfficeNpcAgent>(),
                    cast["NPC-006"].GetComponent<OfficeNpcAgent>(),
                    cast["NPC-002"].GetComponent<OfficeNpcAgent>() },
                null, null, bossDoorWait.transform, null,
                new List<DialogueSequence>(sequences.Values).ToArray());
            directorObject.SetActive(true);
            director.InitializeNewGame();
        }

        private void ReachColleagueSearch()
        {
            director.TryBeginNpcDialogue(cast["NPC-021"], player);
            FinishCurrentDialogue();
            director.TryAnswerBossCall();
            FinishCurrentDialogue();
            director.TryBeginNpcDialogue(cast["NPC-006"], player);
            FinishCurrentDialogue();
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
        }

        private void FinishCurrentDialogue()
        {
            MethodInfo advance = typeof(StoryDialogueController).GetMethod("Advance",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(advance, Is.Not.Null);
            DialogueSequence current = dialogue.ActiveSequence;
            Assert.That(current, Is.Not.Null);
            int guard = 100;
            while (dialogue.IsPlaying && dialogue.ActiveSequence == current && guard-- > 0)
                advance.Invoke(dialogue, null);
            Assert.That(guard, Is.GreaterThan(0), "The active dialogue did not complete.");
        }
    }
}
