using NUnit.Framework;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class FuelStationProgressTests
    {
        [TestCase(9.99f, false)]
        [TestCase(10f, true)]
        [TestCase(20f, true)]
        public void ReadyCallUsesDistanceAt150SecondsAndOnlyHappensOnce(float distance, bool phone)
        {
            var state = new FuelStationProgress();
            Assert.That(state.BeginExploration(), Is.True);
            Assert.That(state.AdvanceTime(149f, 0), Is.False);
            Assert.That(state.Ready, Is.False);
            Assert.That(state.AdvanceTime(1f, distance), Is.True);
            Assert.That(state.PhoneCall, Is.EqualTo(phone));
            Assert.That(state.SpareFuelCans, Is.EqualTo(2));
            Assert.That(state.AdvanceTime(50f, 0), Is.False);
            Assert.That(state.PhoneCall, Is.EqualTo(phone));
            Assert.That(state.MarkCallDelivered(), Is.True);
            Assert.That(state.MarkCallDelivered(), Is.False);
        }

        [Test]
        public void PausedInvalidTimeAndRepeatedArrivalCannotResetTheCountdown()
        {
            var state = new FuelStationProgress();
            Assert.That(state.AdvanceTime(150, 0), Is.False);
            state.BeginExploration();
            state.AdvanceTime(70, 0);
            foreach (float delta in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                Assert.That(state.AdvanceTime(delta, 0), Is.False);
            Assert.That(state.BeginExploration(), Is.False);
            Assert.That(state.ElapsedSeconds, Is.EqualTo(70));
        }

        [Test]
        public void UnheardAndRepeatedSpeechesCannotGrantGrandfatherFacts()
        {
            var state = new FuelStationProgress();
            state.BeginExploration();
            Assert.That(state.RecordGrandfatherLine(1, GrandfatherTopic.GiriawasReport), Is.False);
            Assert.That(state.ReportHeard, Is.False);
            Assert.That(state.RecordGrandfatherLine(0, GrandfatherTopic.Introduction), Is.True);
            Assert.That(state.RecordGrandfatherLine(0, GrandfatherTopic.GiriawasReport), Is.False);
            Assert.That(state.ReportHeard, Is.False);
            state.RecordGrandfatherLine(1, GrandfatherTopic.GiriawasReport);
            Assert.That(state.ReportHeard, Is.True);
            Assert.That(state.DirectionsComplete, Is.False);
            state.RecordGrandfatherLine(2, GrandfatherTopic.NightTrail);
            Assert.That(state.MorningAdviceHeard, Is.False);
            state.RecordGrandfatherLine(3, GrandfatherTopic.WaitUntilMorning);
            state.RecordGrandfatherLine(4, GrandfatherTopic.GuestEtiquette);
            Assert.That(state.DirectionsComplete, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FinalDestinationPreservesOnlyFactsActuallyHeardAndSurvivesSave(bool reportHeard)
        {
            var state = new FuelStationProgress();
            state.BeginExploration();
            Assert.That(state.Depart(), Is.False);
            state.AdvanceTime(150, 20);
            state.RecordGrandfatherLine(0, reportHeard ? GrandfatherTopic.GiriawasReport : GrandfatherTopic.Introduction);
            Assert.That(state.ChooseDestination(), Is.True);
            Assert.That(state.PlanSource, Is.EqualTo(reportHeard ? GiriawasPlanSource.GrandfatherReport : GiriawasPlanSource.RegionalMap));
            Assert.That(state.DirectionsComplete, Is.False);
            Assert.That(state.Depart(), Is.True);
            Assert.That(state.Finish(), Is.True);
            var loaded = JsonUtility.FromJson<FuelStationProgress>(JsonUtility.ToJson(state));
            Assert.That(loaded.Validate(out string error), Is.True, error);
            Assert.That(loaded.Complete, Is.True);
            Assert.That(loaded.GrandfatherInformationShared, Is.EqualTo(reportHeard));
            Assert.That(loaded.GrandfatherNextLine, Is.EqualTo(1));
            Assert.That(loaded.Finish(), Is.False);
        }
    }
}
