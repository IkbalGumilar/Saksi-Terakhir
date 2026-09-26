using NUnit.Framework;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class ChapterOneProgressTests
    {
        [Test]
        public void NewGameStartsOnRooftop()
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
            Assert.That(progress.RakaMet, Is.False);
            Assert.That(progress.SintaMet, Is.False);
            Assert.That(progress.HasCarKey, Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.AnswerBossCall));
            Assert.That(progress.TryApply(ChapterOneEvent.BossCallFinished), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.MeetBoss));
            Assert.That(progress.TryApply(ChapterOneEvent.FirstBriefingFinished), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
        }

        [TestCase("NPC-003", "NPC-004")]
        [TestCase("NPC-004", "NPC-003")]
        public void RakaThenSintaAndReverseOrderAreBothValid(string first, string last)
        {
            ChapterOneProgress progress = AtColleagueSearch();
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, first), Is.True);
            Assert.That(progress.FirstColleagueId, Is.EqualTo(first));
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, last), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            Assert.That(progress.RakaMet && progress.SintaMet, Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, first), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, last), Is.True);
            Assert.That(progress.RakaMet && progress.SintaMet, Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, last), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.FinalBossBriefing));
            Assert.That(progress.TryApply(ChapterOneEvent.FinalBriefingFinished), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.CollectCarKey));
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(progress.HasCarKey, Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(progress.TryApply(ChapterOneEvent.VehicleReached), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.Complete));
        }

        [Test]
        public void DuplicateAndOutOfOrderEventsDoNothing()
        {
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApply(ChapterOneEvent.CarKeyReceived), Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.VehicleReached), Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.False);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.AnswerBossCall));
            Assert.That(progress.TryApply(ChapterOneEvent.BossCallFinished), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.FirstBriefingFinished), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-999"), Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003"), Is.False);
            Assert.That(progress.FirstColleagueId, Is.EqualTo("NPC-003"));
        }

        [Test]
        public void FinalBriefingWaitsForBothArrivalAndWalkDialogue()
        {
            ChapterOneProgress progress = AtColleagueSearch();
            progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003");
            progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004");
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-004"), Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004"), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-004");
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-003");
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.FinalBossBriefing));
        }

        [Test]
        public void QuestDefinitionRejectsMissingAndDuplicateObjectiveKeys()
        {
            QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
            try
            {
                definition.Configure("", QuestCategory.Main, "quest.title", new[] { "one" });
                Assert.That(definition.Validate(out _), Is.False);
                definition.Configure("chapter.search", QuestCategory.Main, "quest.title", new[] { "one", "one" });
                Assert.That(definition.Validate(out _), Is.False);
                definition.Configure("chapter.search", QuestCategory.Main, "quest.title", new[] { "one", "two" });
                Assert.That(definition.Validate(out _), Is.True);
            }
            finally { Object.DestroyImmediate(definition); }
        }

        private static ChapterOneProgress AtColleagueSearch()
        {
            var progress = new ChapterOneProgress();
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            progress.TryApply(ChapterOneEvent.BossCallFinished);
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);
            return progress;
        }
    }
}
