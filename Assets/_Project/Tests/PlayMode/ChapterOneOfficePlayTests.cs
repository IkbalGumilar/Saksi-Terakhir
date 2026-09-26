using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SaksiTerakhir.Tests
{
    public sealed class ChapterOneOfficePlayTests
    {
        private string savePath;

        [UnityTest]
        public IEnumerator LoadingColleagueSearchRestoresExecutiveRoomRestriction()
        {
            var snapshot = new ChapterOneProgress();
            Assert.That(snapshot.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.True);
            Assert.That(snapshot.TryApply(ChapterOneEvent.BossCallFinished), Is.True);
            Assert.That(snapshot.TryApply(ChapterOneEvent.FirstBriefingFinished), Is.True);
            ChapterOneSaveStore.Save(snapshot, savePath);

            yield return SceneManager.LoadSceneAsync("Regional Archive Office", LoadSceneMode.Single);
            yield return null;

            ChapterOneDirector chapter = UnityEngine.Object.FindFirstObjectByType<ChapterOneDirector>();
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
            OfficeNpcDirector npcDirector = UnityEngine.Object.FindFirstObjectByType<OfficeNpcDirector>();
            NpcActivityPoint executivePoint = npcDirector.GetComponentsInChildren<NpcActivityPoint>()
                .First(point => point.name.StartsWith("executive_talk", StringComparison.Ordinal));
            Assert.That(npcDirector.IsStoryRestricted(executivePoint), Is.True,
                "A loaded chapter must keep background workers out of the boss office.");
        }

        [UnityTest]
        public IEnumerator BossDoorDoesNotScoldAboutUnassignedColleagues()
        {
            ChapterOneDirector chapter = UnityEngine.Object.FindFirstObjectByType<ChapterOneDirector>();
            StoryDialogueController dialogue = UnityEngine.Object.FindFirstObjectByType<StoryDialogueController>();
            Transform player = UnityEngine.Object.FindFirstObjectByType<PlayerController>().transform;
            BossOfficeGate gate = UnityEngine.Object.FindFirstObjectByType<BossOfficeGate>();

            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
            gate.OnBlocked(player);
            Assert.That(dialogue.IsPlaying, Is.False);
            yield return null;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savePath = Path.Combine(Application.temporaryCachePath,
                "saksi-chapter-play-test-" + Guid.NewGuid() + ".json");
            ChapterOneDirector.SessionSavePathOverride = savePath;
            yield return SceneManager.LoadSceneAsync("Regional Archive Office", LoadSceneMode.Single);
            MonoBehaviour[] eventSystems = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsSortMode.None).Where(component => component.GetType().Name == "EventSystem").ToArray();
            for (int index = 1; index < eventSystems.Length; index++) eventSystems[index].enabled = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ChapterOneDirector.SessionSavePathOverride = null;
            if (File.Exists(savePath)) File.Delete(savePath);
            if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RooftopCallBossGateAndFirstColleagueWorkInTheSavedOffice()
        {
            ChapterOneDirector chapter = UnityEngine.Object.FindFirstObjectByType<ChapterOneDirector>();
            StoryDialogueController dialogue = UnityEngine.Object.FindFirstObjectByType<StoryDialogueController>();
            OfficeNpcAgent[] cast = UnityEngine.Object.FindObjectsByType<OfficeNpcAgent>(FindObjectsSortMode.None);
            OfficeNpcAgent Actor(string id) => cast.Single(agent => agent.Profile.Id == id);
            Transform player = UnityEngine.Object.FindFirstObjectByType<PlayerController>().transform;
            player.GetComponent<PlayerController>().enabled = false;
            BossOfficeGate gate = UnityEngine.Object.FindFirstObjectByType<BossOfficeGate>();

            Transform saveArea = GameObject.Find("Save Area").transform;
            GameObject questTracker = saveArea.Find("Chapter One Quest Tracker").gameObject;
            GameObject storyPanel = saveArea.Find("Chapter One Dialogue Panel").gameObject;

            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
            Assert.That(questTracker.activeSelf, Is.True);
            Assert.That(questTracker.transform.Find("Quest Title").GetComponent<TMP_Text>().text,
                Is.Not.Empty);
            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-021").GetComponent<NpcInteractable>(), player), Is.True);
            Assert.That(storyPanel.activeSelf, Is.True);
            Assert.That(storyPanel.transform.Find("Dialogue Line").GetComponent<TMP_Text>().text,
                Is.Not.Empty);
            Finish(dialogue);
            Assert.That(storyPanel.activeSelf, Is.False);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.AnswerBossCall));
            Assert.That(chapter.TryAnswerBossCall(), Is.True);
            Finish(dialogue);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.MeetBoss));
            OfficeNpcDirector npcDirector = UnityEngine.Object.FindFirstObjectByType<OfficeNpcDirector>();
            NpcActivityPoint executivePoint = npcDirector.GetComponentsInChildren<NpcActivityPoint>()
                .First(point => point.name.StartsWith("executive_talk", StringComparison.Ordinal));
            Assert.That(npcDirector.IsStoryRestricted(executivePoint), Is.True,
                "The boss office must be cleared as soon as the call ends.");
            yield return null;
            Assert.That(npcDirector.Actors.Any(actor => actor.Destination != null
                && npcDirector.IsStoryRestricted(actor.Destination)), Is.False,
                "Workers already heading into the boss office must abandon that route.");
            Assert.That(gate.CanOpenFor(player), Is.True);
            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-006").GetComponent<NpcInteractable>(), player), Is.True);
            Finish(dialogue);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
            yield return null;
            Assert.That(gate.CanOpenFor(player), Is.False);
            gate.OnBlocked(player);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_scold_raka"));
            Finish(dialogue);

            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-003").GetComponent<NpcInteractable>(), player), Is.True);
            Finish(dialogue);
            Assert.That(chapter.Progress.FirstColleagueId, Is.EqualTo("NPC-003"));
            Assert.That(File.Exists(savePath), Is.True);
            ChapterOneProgress loaded = ChapterOneSaveStore.Load(savePath);
            Assert.That(loaded.FirstColleagueId, Is.EqualTo("NPC-003"));
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
            yield return null;
            Assert.That(Actor("NPC-003").Navigation.speed, Is.EqualTo(4f).Within(0.1f));
            Assert.That(Actor("NPC-003").CurrentState, Is.EqualTo(NpcState.StoryMoving));
            Assert.That(gate.BlockedPromptKey, Is.EqualTo("interact.story.find_sinta"));
            Assert.That(GameObject.Find("Boss Player Barrier").GetComponent<Collider>().enabled, Is.True);
            gate.OnBlocked(player);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_scold_sinta"));
            Finish(dialogue);
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator RakaThenSintaCompletesAtTheVehicle() =>
            RunWholeChapter("NPC-003", "NPC-004");

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator SintaThenRakaCompletesAtTheVehicle() =>
            RunWholeChapter("NPC-004", "NPC-003");

        private IEnumerator RunWholeChapter(string firstId, string lastId)
        {
            ChapterOneDirector chapter = UnityEngine.Object.FindFirstObjectByType<ChapterOneDirector>();
            StoryDialogueController dialogue = UnityEngine.Object.FindFirstObjectByType<StoryDialogueController>();
            OfficeNpcAgent[] cast = UnityEngine.Object.FindObjectsByType<OfficeNpcAgent>(FindObjectsSortMode.None);
            OfficeNpcAgent Actor(string id) => cast.Single(agent => agent.Profile.Id == id);
            Transform player = UnityEngine.Object.FindFirstObjectByType<PlayerController>().transform;
            player.GetComponent<PlayerController>().enabled = false;

            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-021").GetComponent<NpcInteractable>(), player), Is.True);
            Finish(dialogue);
            Assert.That(chapter.TryAnswerBossCall(), Is.True);
            Finish(dialogue);
            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-006").GetComponent<NpcInteractable>(), player), Is.True);
            Finish(dialogue);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));

            Assert.That(chapter.TryBeginNpcDialogue(Actor(firstId).GetComponent<NpcInteractable>(), player), Is.True);
            Finish(dialogue);
            float firstDeadline = Time.realtimeSinceStartup + 105f;
            float nextReport = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < firstDeadline && !Arrived(chapter.Progress, firstId))
            {
                if (Time.realtimeSinceStartup >= nextReport)
                {
                    OfficeNpcAgent actor = Actor(firstId);
                    Debug.Log($"FIRST {firstId} at {actor.transform.position:F2} state {actor.CurrentState} "
                        + $"nav {actor.Navigation.isOnNavMesh} path {actor.Navigation.pathStatus} "
                        + $"remaining {actor.Navigation.remainingDistance:F2} steering {actor.Navigation.steeringTarget:F2} "
                        + $"stopped {actor.Navigation.isStopped} pending {actor.Navigation.pathPending} "
                        + $"door17 {UnityEngine.Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None).Single(d => d.name == "Office_ArchiveHQ_DoorLeaf_17").IsOpen}");
                    nextReport = Time.realtimeSinceStartup + 10f;
                }
                yield return null;
            }
            Assert.That(Arrived(chapter.Progress, firstId), Is.True,
                $"{firstId} did not reach the executive room. State: {Actor(firstId).CurrentState}, "
                + $"position {Actor(firstId).transform.position:F2}, path {Actor(firstId).Navigation.pathStatus}");
            BossOfficeGate gate = UnityEngine.Object.FindFirstObjectByType<BossOfficeGate>();
            Assert.That(gate.GetComponent<DoorInteractable>().IsOpen, Is.False,
                "The executive door must close after the first invited colleague enters.");
            Assert.That(GameObject.Find("Boss Player Barrier").GetComponent<Collider>().enabled, Is.True,
                "The player must not be able to slip through while one colleague is missing.");
            Assert.That(Vector3.Distance(Actor(firstId).transform.position,
                firstId == "NPC-003" ? new Vector3(4.27f, 15.84f, -32.06f)
                    : new Vector3(4.55f, 15.84f, -31f)), Is.LessThan(1f));
            Collider bossRoom = GameObject.Find("Boss Room Zone").GetComponent<Collider>();
            Assert.That(bossRoom.bounds.Contains(Actor("NPC-017").transform.position), Is.False,
                "Background archive guard should leave the boss office before the colleagues gather.");

            chapter.RestoreProgress(ChapterOneSaveStore.Load(savePath));
            Assert.That(chapter.Progress.FirstColleagueId, Is.EqualTo(firstId));
            Assert.That(Arrived(chapter.Progress, firstId), Is.True);
            Assert.That(chapter.TryBeginNpcDialogue(Actor(lastId).GetComponent<NpcInteractable>(), player), Is.True);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            int pausedAtLine = dialogue.LineIndex;
            yield return new WaitForSeconds(4.2f);
            Assert.That(Actor(lastId).StoryWaitingForPlayer, Is.True);
            Assert.That(dialogue.LineIndex, Is.EqualTo(pausedAtLine));

            float escortDeadline = Time.realtimeSinceStartup + 180f;
            nextReport = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < escortDeadline
                   && chapter.Progress.Stage != ChapterOneStage.FinalBossBriefing)
            {
                player.position = Actor(lastId).transform.position + Vector3.back * 1.2f;
                if (Time.realtimeSinceStartup >= nextReport)
                {
                    OfficeNpcAgent actor = Actor(lastId);
                    Debug.Log($"ESCORT {lastId} at {actor.transform.position:F2} state {actor.CurrentState} "
                        + $"line {dialogue.LineIndex} complete {chapter.Progress.WalkDialogueComplete} "
                        + $"path {actor.Navigation.pathStatus} remaining {actor.Navigation.remainingDistance:F2}");
                    nextReport = Time.realtimeSinceStartup + 10f;
                }
                yield return null;
            }
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.FinalBossBriefing),
                $"Escort failed for {lastId}; state {Actor(lastId).CurrentState}, dialogue line {dialogue.LineIndex}");
            Assert.That(Arrived(chapter.Progress, lastId), Is.True);
            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-006").GetComponent<NpcInteractable>(), player), Is.True);
            Finish(dialogue);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.CollectCarKey));
            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-002").GetComponent<NpcInteractable>(), player), Is.True);
            Finish(dialogue);
            Assert.That(chapter.Inventory.Has(CarKeyInventory.CarKeyId), Is.True);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-002").GetComponent<NpcInteractable>(), player), Is.False);

            VehicleArrivalZone arrival = UnityEngine.Object.FindFirstObjectByType<VehicleArrivalZone>();
            player.position = arrival.transform.position + Vector3.right * 1.5f;
            yield return null;
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.Complete));
            Assert.That(ChapterOneSaveStore.Load(savePath).Stage, Is.EqualTo(ChapterOneStage.Complete));
        }

        private static bool Arrived(ChapterOneProgress progress, string id) =>
            id == "NPC-003" ? progress.RakaArrived : progress.SintaArrived;

        private static void Finish(StoryDialogueController dialogue)
        {
            int guard = 100;
            while (dialogue.IsPlaying && guard-- > 0) dialogue.TryConsumeInteract();
            Assert.That(dialogue.IsPlaying, Is.False);
        }
    }
}
