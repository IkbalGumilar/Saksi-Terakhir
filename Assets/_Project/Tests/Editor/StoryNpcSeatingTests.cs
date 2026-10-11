using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SaksiTerakhir.Npc;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SaksiTerakhir.Tests
{
    public sealed class StoryNpcSeatingTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private bool previousPlayOptionsEnabled;
        private EnterPlayModeOptions previousPlayOptions;
        private float previousTimeScale;
        private float previousCaptureDeltaTime;
        private readonly Vector3 seatPosition = new Vector3(7.2f, 0.3f, 0f);
        private readonly Quaternion seatRotation = Quaternion.Euler(0f, 90f, 0f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousPlayOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousPlayOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            yield return new EnterPlayMode(false);
            previousTimeScale = Time.timeScale;
            previousCaptureDeltaTime = Time.captureDeltaTime;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 30f;

            var floor = new GameObject("Seating test walkable floor");
            objects.Add(floor);
            floor.transform.position = Vector3.down * 0.1f;
            floor.AddComponent<BoxCollider>().size = new Vector3(12f, 0.2f, 12f);
            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one),
                    size = new Vector3(12f, 0.2f, 12f),
                    area = 0
                }
            };
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Vector3.zero, new Vector3(16f, 5f, 14f)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            instance = NavMesh.AddNavMeshData(data);

            // Furniture provides physical support but was not included in the walkable bake.
            var sofa = new GameObject("Off-mesh sofa");
            objects.Add(sofa);
            sofa.transform.position = new Vector3(7.2f, 0.15f, 0f);
            sofa.AddComponent<BoxCollider>().size = new Vector3(1.4f, 0.3f, 1.4f);
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in objects) if (item != null) Object.DestroyImmediate(item);
            objects.Clear();
            if (instance.valid) instance.Remove();
            if (data != null) Object.DestroyImmediate(data);
            Time.timeScale = previousTimeScale;
            Time.captureDeltaTime = previousCaptureDeltaTime;
            yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptionsEnabled = previousPlayOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousPlayOptions;
        }

        [UnityTest]
        public IEnumerator OffMeshSofaPoseRemainsExactAndItsCapsuleDoesNotBlockTheApproach()
        {
            OfficeNpcAgent actor = CreateActor(true);
            yield return null;
            Vector3 approach = actor.Navigation.nextPosition;
            Assert.That(NavMesh.SamplePosition(seatPosition, out _, 0.3f,
                new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas }), Is.False,
                "This regression must place the sofa outside the saved walkable polygons.");

            SeatAtFurniture(actor);
            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;
                Assert.That(Vector3.Distance(actor.transform.position, seatPosition), Is.LessThan(0.001f),
                    "A stopped NavMeshAgent must not drag a seated actor back to its standing approach.");
                Assert.That(Quaternion.Angle(actor.transform.rotation, seatRotation), Is.LessThan(0.01f));
            }
            Physics.SyncTransforms();
            Collider[] atApproach = Physics.OverlapCapsule(approach + Vector3.up * 0.4f,
                approach + Vector3.up * 1.5f, 0.35f, ~0, QueryTriggerInteraction.Ignore);
            Assert.That(atApproach, Has.No.Member(actor.GetComponent<CapsuleCollider>()),
                "The physical NPC capsule must follow the sofa pose and free the meeting approach.");
        }

        [UnityTest]
        public IEnumerator StandingRestoresNativePositionUpdatesAndACompleteStoryWalk()
        {
            OfficeNpcAgent actor = CreateActor(true);
            yield return null;
            Vector3 standingPosition = actor.Navigation.nextPosition;
            SeatAtFurniture(actor);
            actor.SetStorySeated(true);
            yield return null;
            actor.SetStorySeated(false);
            Assert.That(actor.Navigation.updatePosition, Is.True,
                "Repeated seating must retain the original standing position ownership.");
            yield return null;
            Assert.That(Vector3.Distance(actor.transform.position, standingPosition), Is.LessThan(0.1f));
            Assert.That(actor.TrySetStoryDestination(new Vector3(2f, standingPosition.y, 0f), 2f), Is.True);
            float deadline = Time.time + 5f;
            while (!actor.StoryAtDestination && Time.time < deadline) yield return null;
            Assert.That(actor.StoryAtDestination, Is.True,
                "Standing after an off-mesh seated pose must resume real NavMesh movement.");
            Assert.That(actor.transform.position.x, Is.EqualTo(2f).Within(0.6f));
        }

        [UnityTest]
        public IEnumerator ReleasingTheStoryHoldRestoresTheStandingNavigationOwner()
        {
            OfficeNpcAgent actor = CreateActor(true);
            yield return null;
            SeatAtFurniture(actor);
            yield return null;
            actor.ReleaseStoryHold();
            Assert.That(actor.StorySeated, Is.False,
                "Returning to ambient work must also release a story-only seated pose.");
            Assert.That(actor.Navigation.updatePosition, Is.True);
            yield return null;
            Assert.That(actor.Navigation.isOnNavMesh, Is.True);
        }

        [UnityTest]
        public IEnumerator ANewStoryDestinationStandsTheActorWithoutASeparateStandCall()
        {
            OfficeNpcAgent actor = CreateActor(true);
            yield return null;
            float floorHeight = actor.Navigation.nextPosition.y;
            SeatAtFurniture(actor);
            yield return null;
            Assert.That(actor.TrySetStoryDestination(new Vector3(2f, floorHeight, 0f), 2f), Is.True);
            Assert.That(actor.StorySeated, Is.False);
            Assert.That(actor.Navigation.updatePosition, Is.True);
            float deadline = Time.time + 5f;
            while (!actor.StoryAtDestination && Time.time < deadline) yield return null;
            Assert.That(actor.StoryAtDestination, Is.True,
                "A new walk order must restore native root movement before following the path.");
        }

        [UnityTest]
        public IEnumerator AnActorWithoutCapsuleVisualStillOwnsItsSeatedStateAndNavigation()
        {
            OfficeNpcAgent actor = CreateActor(false);
            yield return null;
            SeatAtFurniture(actor);
            Assert.That(actor.StorySeated, Is.True,
                "Humanoid actors without the capsule prototype visual still need a seated navigation state.");
            Assert.That(actor.Navigation.updatePosition, Is.False);
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.That(Vector3.Distance(actor.transform.position, seatPosition), Is.LessThan(0.001f));
            Assert.DoesNotThrow(() => actor.SetStorySeated(false));
            Assert.That(actor.StorySeated, Is.False);
            Assert.That(actor.Navigation.updatePosition, Is.True);
        }

        [UnityTest]
        public IEnumerator SeatingDoesNotClaimPositionUpdatesFromAnExistingExternalOwner()
        {
            OfficeNpcAgent actor = CreateActor(false);
            yield return null;
            actor.Navigation.updatePosition = false;
            SeatAtFurniture(actor);
            actor.SetStorySeated(true);
            yield return null;
            actor.SetStorySeated(false);
            Assert.That(actor.Navigation.updatePosition, Is.False,
                "An authored external root-motion owner must survive the seat cycle.");
            actor.ReleaseStoryHold();
            Assert.That(actor.Navigation.updatePosition, Is.False);
        }

        [UnityTest]
        public IEnumerator InterpolatedRigidBodyCannotReapplyItsStandingPoseDuringPausedMenu()
        {
            OfficeNpcAgent actor = CreateActor(true);
            var body = actor.GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            yield return new WaitForFixedUpdate();
            yield return null;
            Time.timeScale = 0f;
            SeatAtFurniture(actor);
            for (int frame = 0; frame < 15; frame++)
            {
                yield return null;
                Assert.That(Vector3.Distance(actor.transform.position, seatPosition), Is.LessThan(0.001f),
                    "Paused physics interpolation must not overwrite the exact saved sofa pose.");
            }
            Time.timeScale = 1f;
            actor.SetStorySeated(false);
            yield return new WaitForFixedUpdate();
            Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate),
                "Standing must return the body's original interpolation setting.");
        }

        private OfficeNpcAgent CreateActor(bool withCapsuleVisual)
        {
            Assert.That(NavMesh.SamplePosition(new Vector3(5f, 0f, 0f), out NavMeshHit hit, 0.5f,
                new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas }), Is.True);
            var homeObject = new GameObject("Seating test home");
            objects.Add(homeObject);
            homeObject.transform.position = hit.position;
            NpcActivityPoint home = homeObject.AddComponent<NpcActivityPoint>();
            home.Configure("Home", NpcActivityKind.Work, NpcZone.Interior, 0, new Vector2(10f, 10f));
            var root = new GameObject("Seating test actor");
            objects.Add(root);
            root.SetActive(false);
            root.transform.position = hit.position;
            NavMeshAgent navigation = root.AddComponent<NavMeshAgent>();
            navigation.agentTypeID = 0;
            navigation.radius = 0.3f;
            navigation.height = 1.9f;
            navigation.stoppingDistance = 0.22f;
            navigation.acceleration = 20f;
            var body = root.AddComponent<CapsuleCollider>();
            body.radius = 0.34f;
            body.height = 1.9f;
            body.center = Vector3.up * 0.95f;
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            if (withCapsuleVisual)
            {
                var visual = new GameObject("Capsule Visual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.up * 0.95f;
                visual.transform.localScale = new Vector3(0.7f, 0.95f, 0.7f);
            }
            OfficeNpcAgent actor = root.AddComponent<OfficeNpcAgent>();
            actor.Configure("Seating test", NpcRole.Worker, home, new[] { home }, false);
            actor.HoldForStory();
            root.SetActive(true);
            return actor;
        }

        private void SeatAtFurniture(OfficeNpcAgent actor)
        {
            Assert.That(actor.Navigation.isOnNavMesh, Is.True);
            actor.HoldForStory();
            // The director retains a valid internal standing point, then applies the exact chair pose.
            Assert.That(actor.Navigation.Warp(actor.transform.position), Is.True);
            actor.SetStorySeated(true);
            actor.transform.SetPositionAndRotation(seatPosition, seatRotation);
            Physics.SyncTransforms();
        }
    }
}
