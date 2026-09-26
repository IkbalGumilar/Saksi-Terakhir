using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Npc;
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
            FinishDialogue();
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.AnswerBossCall));
            Assert.That(director.TryAnswerBossCall(), Is.True);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_call"));
            FinishDialogue();
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.MeetBoss));
            Assert.That(director.TryBeginNpcDialogue(cast["NPC-006"], player), Is.True);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_first"));
            FinishDialogue();
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
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
            FinishDialogue();
            Assert.That(director.Progress.FirstColleagueId, Is.EqualTo(first));
            Assert.That(director.TryBeginNpcDialogue(cast[last], player), Is.True);
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo(lastDialogue));
            FinishAutoDialogue();
            director.OnStoryActorArrived(first);
            director.OnStoryActorArrived(last);
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.FinalBossBriefing));
            director.TryBeginNpcDialogue(cast["NPC-006"], player);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_final"));
            FinishDialogue();
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.CollectCarKey));
            director.TryBeginNpcDialogue(cast["NPC-002"], player);
            FinishDialogue();
            Assert.That(director.Progress.HasCarKey, Is.True);
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(director.TryBeginNpcDialogue(cast["NPC-002"], player), Is.False);
            Assert.That(director.TryReachVehicle(player), Is.True);
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.Complete));
            Assert.That(director.TryReachVehicle(player), Is.False);
        }

        private void CreateChapter()
        {
            string[] ids = { "chapter.rooftop", "chapter.boss_call", "chapter.boss_first",
                "chapter.raka_first", "chapter.sinta_first", "chapter.raka_last",
                "chapter.sinta_last", "chapter.boss_final", "chapter.nadia_key" };
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
                    null, null, null, null);
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
            director.ConfigureScene(dialogue, player, null,
                new[] { cast["NPC-021"].GetComponent<OfficeNpcAgent>(),
                    cast["NPC-022"].GetComponent<OfficeNpcAgent>(),
                    cast["NPC-003"].GetComponent<OfficeNpcAgent>(),
                    cast["NPC-004"].GetComponent<OfficeNpcAgent>(),
                    cast["NPC-006"].GetComponent<OfficeNpcAgent>(),
                    cast["NPC-002"].GetComponent<OfficeNpcAgent>() },
                null, null, null, null, new List<DialogueSequence>(sequences.Values).ToArray());
            directorObject.SetActive(true);
            director.InitializeNewGame();
        }

        private void ReachColleagueSearch()
        {
            director.TryBeginNpcDialogue(cast["NPC-021"], player);
            FinishDialogue();
            director.TryAnswerBossCall();
            FinishDialogue();
            director.TryBeginNpcDialogue(cast["NPC-006"], player);
            FinishDialogue();
            Assert.That(director.Progress.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
        }

        private void FinishDialogue()
        {
            while (dialogue.IsPlaying) dialogue.TryConsumeInteract();
        }

        private void FinishAutoDialogue()
        {
            MethodInfo advance = typeof(StoryDialogueController).GetMethod("Advance",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(advance, Is.Not.Null);
            while (dialogue.IsPlaying) advance.Invoke(dialogue, null);
        }
    }
}
