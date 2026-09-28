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

        [Test]
        public void SeatedPoseChangesOnlyVisualChildrenAndRestoresTheirAuthoredTransforms()
        {
            actorObject = new GameObject("Story actor");
            actorObject.SetActive(false);
            NavMeshAgent navigation = actorObject.AddComponent<NavMeshAgent>();
            CapsuleCollider collider = actorObject.AddComponent<CapsuleCollider>();
            Transform capsule = new GameObject("Capsule Visual").transform;
            capsule.SetParent(actorObject.transform, false);
            capsule.localPosition = new Vector3(0f, 0.95f, 0f);
            capsule.localScale = new Vector3(0.7f, 0.95f, 0.7f);
            Transform marker = new GameObject("Facing Marker").transform;
            marker.SetParent(actorObject.transform, false);
            marker.localPosition = new Vector3(0f, 1.45f, 0.34f);
            Transform labelRoot = new GameObject("NPC Label").transform;
            labelRoot.SetParent(actorObject.transform, false);
            labelRoot.localPosition = new Vector3(0f, 2.18f, 0f);
            OfficeNpcAgent actor = actorObject.AddComponent<OfficeNpcAgent>();
            Vector3 originalCapsulePosition = capsule.localPosition;
            Vector3 originalCapsuleScale = capsule.localScale;
            Vector3 originalMarkerPosition = marker.localPosition;
            Vector3 originalLabelPosition = labelRoot.localPosition;
            float originalAgentHeight = navigation.height;
            float originalColliderHeight = collider.height;

            actor.SetStorySeated(true);
            Assert.That(actor.StorySeated, Is.True);
            Assert.That(capsule.localScale.y, Is.LessThan(originalCapsuleScale.y));
            Assert.That(capsule.localPosition.y, Is.EqualTo(originalCapsulePosition.y));
            Assert.That(capsule.localPosition.y - capsule.localScale.y, Is.InRange(0.35f, 0.45f));
            Assert.That(marker.localPosition.y, Is.LessThan(originalMarkerPosition.y));
            Assert.That(labelRoot.localPosition.y, Is.LessThan(originalLabelPosition.y));
            Assert.That(navigation.height, Is.EqualTo(originalAgentHeight));
            Assert.That(collider.height, Is.EqualTo(originalColliderHeight));

            actor.SetStorySeated(true);
            actor.SetStorySeated(false);
            Assert.That(actor.StorySeated, Is.False);
            Assert.That(capsule.localPosition, Is.EqualTo(originalCapsulePosition));
            Assert.That(capsule.localScale, Is.EqualTo(originalCapsuleScale));
            Assert.That(marker.localPosition, Is.EqualTo(originalMarkerPosition));
            Assert.That(labelRoot.localPosition, Is.EqualTo(originalLabelPosition));
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
