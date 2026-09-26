using System.IO;
using NUnit.Framework;
using SaksiTerakhir.Story;
using UnityEngine;
using UnityEngine.TestTools;

namespace SaksiTerakhir.Tests
{
    public sealed class ChapterOneSaveTests
    {
        private string path;

        [SetUp]
        public void SetUp() => path = Path.Combine(Path.GetTempPath(), "saksi-chapter-test-" + System.Guid.NewGuid() + ".json");

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }

        [TestCase("NPC-003", "NPC-004")]
        [TestCase("NPC-004", "NPC-003")]
        public void RoundTripPreservesColleagueOrderArrivalsAndKey(string first, string last)
        {
            ChapterOneProgress progress = ReachColleagues();
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, first), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, last), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, first), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, last), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, last), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.FinalBriefingFinished), Is.True);
            Assert.That(new CarKeyInventory(progress).TryAdd("car-key"), Is.True);

            ChapterOneSaveStore.Save(progress, path);
            ChapterOneProgress loaded = ChapterOneSaveStore.Load(path);
            Assert.That(loaded.Version, Is.EqualTo(1));
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(loaded.FirstColleagueId, Is.EqualTo(first));
            Assert.That(loaded.LastColleagueId, Is.EqualTo(last));
            Assert.That(loaded.RakaMet && loaded.SintaMet && loaded.RakaArrived && loaded.SintaArrived,
                Is.True);
            Assert.That(loaded.WalkDialogueComplete && loaded.HasCarKey, Is.True);
            Assert.That(new CarKeyInventory(loaded).TryAdd("car-key"), Is.False);
        }

        [Test]
        public void InventoryAcceptsOnlyOneStoryKeyAtTheCorrectStage()
        {
            ChapterOneProgress progress = ReachColleagues();
            CarKeyInventory inventory = new CarKeyInventory(progress);
            Assert.That(inventory.TryAdd("car-key"), Is.False);
            Assert.That(inventory.TryAdd("archive-key"), Is.False);
            Assert.That(inventory.Has("car-key"), Is.False);
        }

        [Test]
        public void MissingSaveBeginsFreshWithoutAnError()
        {
            ChapterOneProgress loaded = ChapterOneSaveStore.Load(path);
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
        }

        [Test]
        public void SavingAgainReplacesPreviousSnapshotWithoutTemporaryFile()
        {
            ChapterOneProgress progress = new ChapterOneProgress();
            ChapterOneSaveStore.Save(progress, path);
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            ChapterOneSaveStore.Save(progress, path);

            Assert.That(ChapterOneSaveStore.Load(path).Stage,
                Is.EqualTo(ChapterOneStage.AnswerBossCall));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void MalformedOrFutureSaveBeginsFreshWithError()
        {
            File.WriteAllText(path, "{oops");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Chapter one save"));
            Assert.That(ChapterOneSaveStore.Load(path).Stage,
                Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));

            File.WriteAllText(path, "{\"version\":2,\"progress\":{}}");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Chapter one save"));
            Assert.That(ChapterOneSaveStore.Load(path).Stage,
                Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
        }

        [Test]
        public void CompletedCallAndRooftopDialogueAreNotReopenedByLoading()
        {
            ChapterOneProgress progress = new ChapterOneProgress();
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            progress.TryApply(ChapterOneEvent.BossCallFinished);
            ChapterOneSaveStore.Save(progress, path);
            ChapterOneProgress loaded = ChapterOneSaveStore.Load(path);
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.MeetBoss));
            Assert.That(loaded.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.False);
            Assert.That(loaded.TryApply(ChapterOneEvent.BossCallFinished), Is.False);
        }

        private static ChapterOneProgress ReachColleagues()
        {
            ChapterOneProgress progress = new ChapterOneProgress();
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            progress.TryApply(ChapterOneEvent.BossCallFinished);
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);
            return progress;
        }
    }
}
