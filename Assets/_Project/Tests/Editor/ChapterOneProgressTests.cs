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
            Assert.That(progress.TryApply(ChapterOneEvent.BossMovedToMeeting, "left"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, last), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, last), Is.True);
            Assert.That(progress.RakaMet && progress.SintaMet, Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, last), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.FinalBossBriefing));
            Assert.That(progress.TryApply(ChapterOneEvent.MeetingAssembled), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.PlayerSeated), Is.True);
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
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004"), Is.False,
                "The moving conversation cannot begin before the stationary introduction completes.");
            Assert.That(progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004"), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-004");
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-003");
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.FinalBossBriefing));
        }

        [Test]
        public void LastColleagueIntroMustFinishBeforeTheEscortCanAdvance()
        {
            ChapterOneProgress progress = AtColleagueSearch();
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004"), Is.True);

            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            Assert.That(progress.LastColleagueIntroComplete, Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-004"), Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004"), Is.False);

            Assert.That(progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, "NPC-004"), Is.True);
            Assert.That(progress.LastColleagueIntroComplete, Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, "NPC-004"), Is.False,
                "The stationary introduction is a one-time transition.");
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004"), Is.True);
        }

        [TestCase("left", 1)]
        [TestCase("right", 2)]
        public void FinalBriefingRequiresAChosenBossSeatAndThePlayerSeating(string bossSide,
            int expectedSide)
        {
            ChapterOneProgress progress = AtColleagueSearch();
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-003"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.BossMovedToMeeting, bossSide), Is.True);
            Assert.That(progress.BossMeetingSide, Is.EqualTo(expectedSide));

            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004"), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-004"), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.FinalBossBriefing));

            Assert.That(progress.TryApply(ChapterOneEvent.MeetingAssembled), Is.True);
            Assert.That(progress.MeetingAssembled, Is.True);
            Assert.That(progress.PlayerSeated, Is.False);
            Assert.That(progress.TryApply(ChapterOneEvent.FinalBriefingFinished), Is.False,
                "Boss must invite and seat the player before the four-person briefing can finish.");
            Assert.That(progress.TryApply(ChapterOneEvent.PlayerSeated), Is.True);
            Assert.That(progress.PlayerSeated, Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.FinalBriefingFinished), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.CollectCarKey));
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
