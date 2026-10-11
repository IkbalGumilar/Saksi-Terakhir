using NUnit.Framework;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class ChapterOneSuspensionTests
    {
        private GameObject owner;
        private GameObject player;

        [TearDown]
        public void TearDown()
        {
            if (owner != null) Object.DestroyImmediate(owner);
            if (player != null) Object.DestroyImmediate(player);
        }

        [Test]
        public void VehicleProximityCannotCompleteQuestBeforeContinue()
        {
            ChapterOneDirector chapter = Create();
            ChapterOneProgress progress = ReachVehicle();
            chapter.SuspendGameplay();
            chapter.RestoreProgress(progress);
            Assert.That(chapter.TryReachVehicle(player.transform), Is.False);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            chapter.ResumeGameplay();
            Assert.That(chapter.TryReachVehicle(player.transform), Is.True);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.Complete));
        }

        [Test]
        public void SuspendedNpcArrivalAndBranchEventsCannotMutatePreview()
        {
            ChapterOneDirector chapter = Create();
            var progress = new ChapterOneProgress();
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            progress.TryApply(ChapterOneEvent.BossCallFinished);
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);
            progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003");
            chapter.SuspendGameplay();
            chapter.RestoreProgress(progress);
            chapter.OnStoryActorArrived("NPC-003");
            Assert.That(chapter.Progress.RakaArrived, Is.False);
            Assert.That(chapter.TryApplyStoryEvent(ChapterOneStoryEvent.BreakfastPaid), Is.False);
        }

        private ChapterOneDirector Create()
        {
            owner = new GameObject("Suspended chapter");
            player = new GameObject("Player");
            ChapterOneDirector chapter = owner.AddComponent<ChapterOneDirector>();
            chapter.ConfigureScene(null, player.transform, null, null, null, null, null, null, null);
            return chapter;
        }

        private static ChapterOneProgress ReachVehicle()
        {
            var progress = new ChapterOneProgress();
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            progress.TryApply(ChapterOneEvent.BossCallFinished);
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);
            progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003");
            progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004");
            progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-003");
            progress.TryApply(ChapterOneEvent.BossMovedToMeeting, "left");
            progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, "NPC-004");
            progress.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004");
            progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-004");
            progress.TryApply(ChapterOneEvent.MeetingAssembled);
            progress.TryApply(ChapterOneEvent.PlayerSeated);
            progress.TryApply(ChapterOneEvent.FinalBriefingFinished);
            progress.TryApply(ChapterOneEvent.CarKeyReceived);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            return progress;
        }
    }
}
