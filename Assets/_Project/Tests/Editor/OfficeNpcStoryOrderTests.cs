using NUnit.Framework;
using SaksiTerakhir.Npc;
using UnityEngine;
using UnityEngine.AI;

namespace SaksiTerakhir.Tests
{
    public sealed class OfficeNpcStoryOrderTests
    {
        private GameObject actorObject;
        private GameObject speakerObject;
        private NpcProfile profile;

        [TearDown]
        public void TearDown()
        {
            if (actorObject != null) Object.DestroyImmediate(actorObject);
            if (speakerObject != null) Object.DestroyImmediate(speakerObject);
            if (profile != null) Object.DestroyImmediate(profile);
        }

        [Test]
        public void HoldBeforeStartBlocksPatrolAndFacesTheSpeaker()
        {
            OfficeNpcAgent actor = CreateActor(false);
            speakerObject = new GameObject("Speaker");
            speakerObject.transform.position = new Vector3(3f, 0f, 0f);
            actor.HoldForStory(speakerObject.transform);

            Assert.That(actor.CurrentState, Is.EqualTo(NpcState.StoryHeld));
            Assert.That(actor.CanConverse, Is.False);
            Assert.That(actor.StoryAtDestination, Is.False);
            actor.ReleaseStoryHold();
            Assert.That(actor.CurrentState, Is.EqualTo(NpcState.Waiting));
        }

        [Test]
        public void FollowPauseUsesFiveMeterThresholdEvenWhenPathIsPending()
        {
            OfficeNpcAgent actor = CreateActor(true);
            actor.HoldForStory();
            Assert.That(actor.TrySetStoryDestination(new Vector3(3f, 0f, 0f), 2.2f), Is.False);
            speakerObject = new GameObject("Player");
            speakerObject.transform.position = new Vector3(20f, 0f, 0f);
            actor.SetStoryFollowDistance(speakerObject.transform, 5f);
            Assert.That(actor.StoryWaitingForPlayer, Is.True);
            speakerObject.transform.position = new Vector3(2f, 0f, 0f);
            Assert.That(actor.StoryWaitingForPlayer, Is.False);
            actor.ReleaseStoryHold();
            Assert.That(actor.CurrentState, Is.EqualTo(NpcState.Waiting));
        }

        [Test]
        public void IncompletePathCannotMarkStoryArrival()
        {
            OfficeNpcAgent actor = CreateActor(true);
            actor.HoldForStory();
            Assert.That(actor.TrySetStoryDestination(new Vector3(100f, 0f, 100f), 4f), Is.False);
            Assert.That(actor.StoryAtDestination, Is.False);
        }

        private OfficeNpcAgent CreateActor(bool active)
        {
            profile = ScriptableObject.CreateInstance<NpcProfile>();
            profile.Configure("NPC-TEST", "Uji NPC", 30, 2.6f, NpcRole.Worker,
                false, null, null, null, null);
            actorObject = new GameObject("Story actor");
            actorObject.SetActive(false);
            actorObject.transform.position = Vector3.zero;
            actorObject.AddComponent<NavMeshAgent>();
            OfficeNpcAgent actor = actorObject.AddComponent<OfficeNpcAgent>();
            actor.SetProfile(profile);
            if (active) actorObject.SetActive(true);
            return actor;
        }

    }
}
