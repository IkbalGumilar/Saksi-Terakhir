using NUnit.Framework;
using SaksiTerakhir.Npc;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class NpcTrafficPriorityTests
    {
        [TestCase(1, 10, 11)]
        [TestCase(10, 1, 22)]
        [TestCase(-5, 2, 11)]
        [TestCase(15, 8, 22)]
        public void LowerPriorityYieldsWithoutDrawingALottery(int firstPriority, int secondPriority, int expected)
        {
            var decisions = new NpcTrafficDecision();
            Assert.That(decisions.ChooseYielding(11, firstPriority, 22, secondPriority, 0f,
                () => { Assert.Fail("Different priorities must not draw a lottery."); return false; }), Is.EqualTo(expected));
        }

        [TestCase(true, 11)]
        [TestCase(false, 22)]
        public void EqualPriorityLotteryIsSharedWhenTheOtherNpcChecksFromTheOppositeDirection(bool draw, int expected)
        {
            var decisions = new NpcTrafficDecision();
            int draws = 0;
            Assert.That(decisions.ChooseYielding(11, 1, 22, 1, 0f, () => { draws++; return draw; }), Is.EqualTo(expected));
            for (int i = 1; i < 40; i++)
                Assert.That(decisions.ChooseYielding(22, 1, 11, 1, i * 0.1f,
                    () => { draws++; return !draw; }), Is.EqualTo(expected));
            Assert.That(draws, Is.EqualTo(1), "The same pair must not alternate or both yield each frame.");
        }

        [Test]
        public void PassingEndsTheOldLotteryAndTheNextEncounterCanChooseTheOtherNpc()
        {
            var decisions = new NpcTrafficDecision();
            Assert.That(decisions.ChooseYielding(11, 1, 22, 1, 0f, () => true), Is.EqualTo(11));
            decisions.Release(22, 11);
            Assert.That(decisions.ChooseYielding(22, 1, 11, 1, 1f, () => false), Is.EqualTo(22));
        }

        [Test]
        public void WaitingAtANarrowStairRetainsTheChoiceUntilTheyPass()
        {
            var decisions = new NpcTrafficDecision();
            decisions.ChooseYielding(11, 1, 22, 1, 0f, () => true);
            decisions.Retain(22, 11, 9f);
            decisions.Expire(10f);
            Assert.That(decisions.ChooseYielding(22, 1, 11, 1, 10f,
                () => { Assert.Fail("An actor still waiting must not redraw."); return false; }), Is.EqualTo(11));
            decisions.Expire(14f);
            Assert.That(decisions.ChooseYielding(11, 1, 22, 1, 14f, () => false), Is.EqualTo(22));
        }

        [Test]
        public void ProfileStartsAtOneAndClampsToTheInspectorRange()
        {
            var profile = ScriptableObject.CreateInstance<NpcProfile>();
            try
            {
                Assert.That(profile.TrafficPriority, Is.EqualTo(1));
                profile.SetTrafficPriority(-1);
                Assert.That(profile.TrafficPriority, Is.Zero);
                profile.SetTrafficPriority(99);
                Assert.That(profile.TrafficPriority, Is.EqualTo(10));
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
