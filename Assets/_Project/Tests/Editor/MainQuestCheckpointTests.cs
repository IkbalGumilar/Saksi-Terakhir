using NUnit.Framework;
using SaksiTerakhir.Persistence;
using SaksiTerakhir.Story;

namespace SaksiTerakhir.Tests
{
    public sealed class MainQuestCheckpointTests
    {
        [Test]
        public void StageChangeRequiresAnotherCheckpoint()
        {
            var progress = new ChapterOneProgress();
            string before = MainQuestCheckpointKey.Create(progress, "rooftop", true);
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            Assert.That(MainQuestCheckpointKey.Create(progress, "boss-call", true), Is.Not.EqualTo(before));
        }

        [Test]
        public void CheckedColleagueObjectiveRequiresAnotherCheckpoint()
        {
            var progress = Colleagues();
            string before = MainQuestCheckpointKey.Create(progress, "find-colleagues", true);
            progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003");
            Assert.That(MainQuestCheckpointKey.Create(progress, "find-colleagues", true), Is.Not.EqualTo(before));
        }

        [Test]
        public void ActorArrivalWithoutVisibleQuestChangeDoesNotDuplicateCheckpoint()
        {
            var progress = Colleagues();
            progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003");
            string before = MainQuestCheckpointKey.Create(progress, "find-colleagues", true);
            progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-003");
            Assert.That(MainQuestCheckpointKey.Create(progress, "find-colleagues", true), Is.EqualTo(before));
        }

        [Test]
        public void SideQuestDoesNotCreateMainQuestCheckpoint()
        {
            Assert.That(MainQuestCheckpointKey.Create(new ChapterOneProgress(), "side", false), Is.Empty);
        }

        [Test]
        public void ArrivalAtFuelStationCreatesAStableCheckpointWithoutAnActiveQuest()
        {
            ChapterOneProgress progress = Journey();
            string before = MainQuestCheckpointKey.Create(progress, "chapter.travel_to_spbu", true);
            Assert.That(progress.TryApply(ChapterOneEvent.StationParked), Is.True);
            Assert.That(progress.ValidateStoryState(out string error), Is.True, error);

            string after = MainQuestCheckpointKey.Create(progress, null, false);
            Assert.That(after, Is.Not.Empty, "Parking must save progress even when its quest is no longer shown.");
            Assert.That(after, Is.Not.EqualTo(before));
            Assert.That(MainQuestCheckpointKey.Create(progress, null, false), Is.EqualTo(after));
        }

        private static ChapterOneProgress Journey()
        {
            ChapterOneProgress progress = Colleagues();
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-003"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.BossMovedToMeeting, "left"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.MeetingAssembled), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.PlayerSeated), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.FinalBriefingFinished), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived), Is.True);
            Assert.That(progress.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.JourneyStarted), Is.True);
            Assert.That(progress.ValidateStoryState(out string error), Is.True, error);
            return progress;
        }

        private static ChapterOneProgress Colleagues()
        {
            var progress = new ChapterOneProgress();
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            progress.TryApply(ChapterOneEvent.BossCallFinished);
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);
            return progress;
        }
    }
}
