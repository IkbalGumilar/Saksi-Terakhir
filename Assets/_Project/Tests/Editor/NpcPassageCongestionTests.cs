using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Npc;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SaksiTerakhir.Tests
{
    public sealed class NpcPassageCongestionTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<NpcProfile> profiles = new List<NpcProfile>();
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private NavMeshLinkInstance seamLink;
        private bool previousOptionsEnabled;
        private EnterPlayModeOptions previousOptions;
        private float previousScale, previousCapture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            yield return new EnterPlayMode(false);
            previousScale = Time.timeScale;
            previousCapture = Time.captureDeltaTime;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 30f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(floor);
            floor.name = "Congestion test floor";
            floor.transform.position = Vector3.down * 0.1f;
            floor.transform.localScale = new Vector3(12f, 0.2f, 12f);
            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one),
                    size = floor.transform.localScale, area = 0 }
            };
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Vector3.zero, new Vector3(14f, 4f, 14f)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            instance = NavMesh.AddNavMeshData(data);
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in objects) if (item != null) Object.DestroyImmediate(item);
            objects.Clear();
            foreach (NpcProfile profile in profiles) if (profile != null) Object.DestroyImmediate(profile);
            profiles.Clear();
            if (NavMesh.IsLinkValid(seamLink)) NavMesh.RemoveLink(seamLink);
            if (instance.valid) instance.Remove();
            if (data != null) Object.DestroyImmediate(data);
            Time.timeScale = previousScale;
            Time.captureDeltaTime = previousCapture;
            yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousOptions;
        }

        [UnityTest]
        public IEnumerator LowerPriorityWorkerYieldsAndResumesItsReservedDestination()
        {
            OfficeNpcAgent worker = CreateActor("Moving ambient worker", Vector3.zero);
            OfficeNpcAgent story = CreateActor("Story traveller", new Vector3(-1.6f, 0f, 0f));
            SetTrafficPriority(worker, 1);
            SetTrafficPriority(story, 8);
            NpcActivityPoint destination = CreatePoint("Reserved work destination", new Vector3(-4f, 0f, 0f));
            yield return null;
            worker.ReleaseStoryHold();
            Assert.That((bool)typeof(OfficeNpcAgent).GetMethod("TryBeginJourney",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(worker, new object[] { destination }), Is.True);
            Assert.That(story.TrySetStoryDestination(new Vector3(4f, 0f, 0f), 1.5f), Is.True);
            NpcPassageYield.Request(story.transform, Vector3.right, 0.34f, 1.8f);
            Assert.That(worker.GetComponent<NpcPassageYield>().IsActive, Is.True,
                "The lower-priority actor must yield while retaining its reserved work destination.");
            float deadline = Time.time + 18f;
            while ((!story.StoryAtDestination || worker.CurrentState != NpcState.Activity) && Time.time < deadline)
            {
                Assert.That(destination.Occupant, Is.SameAs(worker));
                yield return null;
            }
            Assert.That(story.StoryAtDestination, Is.True);
            Assert.That(worker.CurrentState, Is.EqualTo(NpcState.Activity));
            Assert.That(worker.Destination, Is.SameAs(destination));
            Assert.That(Vector3.Distance(worker.transform.position, destination.transform.position), Is.LessThan(0.6f),
                "A temporary yield must resume the original destination rather than returning to a stale path or idling in its pocket.");
        }

        [UnityTest]
        public IEnumerator InitiallyOverlappingPlayerAndIdleWorkerSeparateWithoutMovingThePlayer()
        {
            OfficeNpcAgent worker = CreateActor("Overlapping worker", Vector3.zero);
            var player = new GameObject("Overlapping player body");
            objects.Add(player);
            player.transform.position = new Vector3(-0.25f, 0f, 0f);
            var body = player.AddComponent<CharacterController>();
            body.height = 1.75f;
            body.radius = 0.3f;
            body.center = Vector3.up * 0.875f;
            worker.ReleaseStoryHold();
            float deadline = Time.time + 3f;
            while (worker.CurrentState != NpcState.Activity && Time.time < deadline) yield return null;
            // Automatic crowd recovery can already separate the bodies while the
            // worker becomes ready. Establish the overlap at the assertion point.
            Assert.That(worker.Navigation.Warp(Vector3.zero), Is.True);
            player.transform.position = new Vector3(-0.25f, 0f, 0f);
            Physics.SyncTransforms();
            Assert.That(Penetration(worker.GetComponent<CapsuleCollider>(), body), Is.GreaterThan(0.25f),
                "The fixture must begin with a real body overlap, not merely a nearby actor.");
            Vector3 playerStart = player.transform.position;
            Vector3 previous = worker.transform.position;
            deadline = Time.time + 3f;
            while (Penetration(worker.GetComponent<CapsuleCollider>(), body) > 0.02f && Time.time < deadline)
            {
                NpcPassageYield.Request(player.transform, Vector3.right, 0.3f, 1.8f);
                yield return null;
                Assert.That(Vector3.Distance(worker.transform.position, previous), Is.LessThan(0.16f),
                    "Initial overlap recovery must move in bounded steps instead of teleporting the actor out of congestion.");
                previous = worker.transform.position;
            }
            Assert.That(Penetration(worker.GetComponent<CapsuleCollider>(), body), Is.LessThanOrEqualTo(0.02f));
            Assert.That(player.transform.position, Is.EqualTo(playerStart));
            Assert.That(worker.Home.Occupant, Is.SameAs(worker));
        }

        [UnityTest]
        public IEnumerator EqualPriorityActorsKeepOneRandomRightOfWayAndFinishBothOrders()
        {
            OfficeNpcAgent first = CreateActor("A story traveller", new Vector3(-0.7f, 0f, 0f));
            OfficeNpcAgent second = CreateActor("B story traveller", new Vector3(0.7f, 0f, 0f));
            yield return null;
            Assert.That(first.TrySetStoryDestination(new Vector3(4f, 0f, 0f), 1.5f), Is.True);
            Assert.That(second.TrySetStoryDestination(new Vector3(-4f, 0f, 0f), 1.5f), Is.True);
            NpcPassageYield.Request(first.transform, Vector3.right, 0.34f, 1.8f);
            NpcPassageYield.Request(second.transform, Vector3.left, 0.34f, 1.8f);
            NpcPassageYield firstYield = first.GetComponent<NpcPassageYield>();
            NpcPassageYield secondYield = second.GetComponent<NpcPassageYield>();
            Assert.That(firstYield.IsActive ^ secondYield.IsActive, Is.True,
                "Equal priorities must choose exactly one random yielding participant.");
            bool firstWasYielding = firstYield.IsActive;
            for (int i = 0; i < 20; i++)
            {
                NpcPassageYield.Request(second.transform, Vector3.left, 0.34f, 1.8f);
                NpcPassageYield.Request(first.transform, Vector3.right, 0.34f, 1.8f);
                Assert.That(firstYield.IsActive, Is.EqualTo(firstWasYielding));
                Assert.That(secondYield.IsActive, Is.EqualTo(!firstWasYielding));
            }
            float deadline = Time.time + 18f;
            while ((!first.StoryAtDestination || !second.StoryAtDestination) && Time.time < deadline) yield return null;
            Assert.That(first.StoryAtDestination, Is.True);
            Assert.That(second.StoryAtDestination, Is.True);
        }

        [UnityTest]
        public IEnumerator OverlapRecoveryDoesNotPushAWorkerThroughAnUnbakedWall()
        {
            OfficeNpcAgent worker = CreateActor("Pinned overlapping worker", Vector3.zero);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(wall);
            wall.name = "Unbaked recovery wall";
            wall.transform.position = new Vector3(0.58f, 1f, 0f);
            wall.transform.localScale = new Vector3(0.4f, 2f, 6f);
            var player = new GameObject("Player pinning worker toward wall");
            objects.Add(player);
            player.transform.position = new Vector3(-0.25f, 0f, 0f);
            var body = player.AddComponent<CharacterController>();
            body.height = 1.75f;
            body.radius = 0.3f;
            body.center = Vector3.up * 0.875f;
            worker.ReleaseStoryHold();
            yield return null;
            Physics.SyncTransforms();
            float deadline = Time.time + 3f;
            while (Time.time < deadline)
            {
                NpcPassageYield.Request(player.transform, Vector3.right, 0.3f, 1.8f);
                yield return null;
                Assert.That(Penetration(worker.GetComponent<CapsuleCollider>(), wall.GetComponent<Collider>()),
                    Is.LessThan(0.025f), "Cooperative crowd recovery must retain the physical wall guard.");
                Assert.That(worker.transform.position.x, Is.LessThan(0.25f));
            }
        }

        [UnityTest]
        public IEnumerator TemporaryYieldPocketCannotCrossASupportedAuthoredNavigationLink()
        {
            BuildSupportedNavigationSeam();
            OfficeNpcAgent worker = CreateActor("Worker beside temporary-yield seam", new Vector3(-1.3f, 0f, 0f));
            yield return null;
            var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            Assert.That(NavMesh.SamplePosition(new Vector3(1.3f, 0f, 0f),
                out NavMeshHit target, 0.3f, filter), Is.True);
            var route = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(worker.transform.position, target.position, filter, route), Is.True);
            Assert.That(route.status, Is.EqualTo(NavMeshPathStatus.PathComplete),
                "The rejected temporary pocket must otherwise have a complete route across the supported authored seam.");
            Assert.That(NavMesh.Raycast(worker.transform.position, target.position, out _, filter), Is.True,
                "This route must cross an authored link between separate polygons, not an ordinary connected floor.");
            NpcPassageYield yielding = worker.GetComponent<NpcPassageYield>();
            MethodInfo safePath = typeof(NpcPassageYield).GetMethod("SafePath",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object[] acrossSeam = { worker.transform.position, target.position, Vector3.zero };
            Assert.That((bool)safePath.Invoke(yielding, acrossSeam), Is.False,
                "A short traffic sidestep must stay on its connected landing; entering a temporary link can make cancellation complete it by teleportation.");
            object[] sameLanding = { worker.transform.position,
                worker.transform.position + Vector3.forward, Vector3.zero };
            Assert.That((bool)safePath.Invoke(yielding, sameLanding), Is.True,
                "Rejecting link crossings must preserve ordinary connected landing pockets.");
        }

        [UnityTest]
        public IEnumerator CancellingALegacyYieldOnALiveLinkPreservesPoseAndResumesTheStoryOrder()
        {
            BuildSupportedNavigationSeam();
            OfficeNpcAgent worker = CreateActor("Interrupted legacy yield on seam", new Vector3(-1.8f, 0f, 0f));
            yield return null;
            Vector3 goal = new Vector3(1.8f, 0f, 0f);
            Assert.That(worker.TrySetStoryDestination(goal, 1.5f), Is.True);
            float deadline = Time.time + 5f;
            while ((!worker.Navigation.isOnOffMeshLink || worker.Navigation.updatePosition
                || Mathf.Abs(worker.transform.position.x) > 0.3f) && Time.time < deadline)
                yield return null;
            Assert.That(worker.Navigation.isOnOffMeshLink, Is.True);
            Assert.That(worker.Navigation.updatePosition, Is.False);
            Assert.That(Mathf.Abs(worker.transform.position.x), Is.LessThanOrEqualTo(0.3f),
                "The cancellation must occur mid-seam, where ResetPath would jump to its far endpoint.");
            NpcPassageYield yielding = worker.GetComponent<NpcPassageYield>();
            // A legacy/restored yield can already be on a link before the new
            // same-landing planner guard is applied. Exercise that defensive boundary.
            SetYieldField(yielding, "originalState", NpcState.StoryMoving);
            SetYieldField(yielding, "originalDestination", worker.Destination);
            SetYieldField(yielding, "originalSpeed", worker.Navigation.speed);
            SetYieldField(yielding, "originalStoppingDistance", worker.Navigation.stoppingDistance);
            SetYieldField(yielding, "originalStopped", false);
            SetYieldField(yielding, "originalNavigationTarget", goal);
            SetYieldField(yielding, "phase", 2);
            Vector3 beforeCancel = worker.transform.position;
            yielding.Cancel();
            Assert.That(yielding.IsActive, Is.False);
            Assert.That(worker.Navigation.isOnOffMeshLink, Is.True,
                "Cancelling a legacy yield must retain the supported live seam instead of immediately completing it.");
            Assert.That(Vector3.Distance(worker.transform.position, beforeCancel), Is.LessThan(0.02f));
            Assert.That(worker.StoryMoveRequested, Is.True);
            Vector3 previous = worker.transform.position;
            deadline = Time.time + 8f;
            while (!worker.StoryAtDestination && Time.time < deadline)
            {
                Assert.That(Vector3.Distance(worker.transform.position, previous), Is.LessThan(0.3f),
                    "After cancellation the retained link and original story goal must be walked without an endpoint snap.");
                previous = worker.transform.position;
                yield return null;
            }
            Assert.That(worker.StoryAtDestination, Is.True);
            Assert.That(Vector3.Distance(worker.transform.position, goal), Is.LessThan(0.6f));
        }

        private void BuildSupportedNavigationSeam()
        {
            if (instance.valid) instance.Remove();
            Object.DestroyImmediate(data);
            // The original continuous physical floor supports the link, while
            // the saved navigation bake contains two separate connected landings.
            var sources = new List<NavMeshBuildSource>();
            foreach (float x in new[] { -1.75f, 1.75f })
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(new Vector3(x, -0.1f, 0f), Quaternion.identity, Vector3.one),
                    size = new Vector3(3f, 0.2f, 4f), area = 0 });
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Vector3.zero, new Vector3(12f, 4f, 8f)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            instance = NavMesh.AddNavMeshData(data);
            var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            Assert.That(NavMesh.SamplePosition(new Vector3(-1f, 0f, 0f), out NavMeshHit first, 0.3f, filter), Is.True);
            Assert.That(NavMesh.SamplePosition(new Vector3(1f, 0f, 0f), out NavMeshHit second, 0.3f, filter), Is.True);
            seamLink = NavMesh.AddLink(new NavMeshLinkData { startPosition = first.position,
                endPosition = second.position, width = 1f, bidirectional = true, agentTypeID = 0 });
            Assert.That(NavMesh.IsLinkValid(seamLink), Is.True);
            Physics.SyncTransforms();
        }

        private static void SetYieldField(NpcPassageYield yielding, string name, object value)
            => typeof(NpcPassageYield).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(yielding, value);

        private OfficeNpcAgent CreateActor(string name, Vector3 requested)
        {
            Assert.That(NavMesh.SamplePosition(requested, out NavMeshHit hit, 0.5f,
                new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas }), Is.True);
            NpcActivityPoint home = CreatePoint(name + " home", hit.position);
            var root = new GameObject(name);
            objects.Add(root);
            root.SetActive(false);
            root.transform.position = hit.position;
            var navigation = root.AddComponent<NavMeshAgent>();
            navigation.agentTypeID = 0;
            navigation.radius = 0.34f;
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
            var actor = root.AddComponent<OfficeNpcAgent>();
            actor.Configure(name, NpcRole.Worker, home, new[] { home }, true);
            actor.HoldForStory();
            root.SetActive(true);
            return actor;
        }

        private void SetTrafficPriority(OfficeNpcAgent actor, int priority)
        {
            NpcActivityPoint home = actor.Home;
            NpcActivityPoint[] route = actor.Route;
            var profile = ScriptableObject.CreateInstance<NpcProfile>();
            profiles.Add(profile);
            profile.Configure(actor.name, actor.DisplayName, 25, 2f, NpcRole.Worker, true,
                null, null, null, null);
            profile.SetTrafficPriority(priority);
            actor.SetProfile(profile);
            actor.BindWaypoints(home, route);
        }

        private NpcActivityPoint CreatePoint(string name, Vector3 position)
        {
            var root = new GameObject(name);
            objects.Add(root);
            root.transform.position = position;
            var point = root.AddComponent<NpcActivityPoint>();
            point.Configure(name, NpcActivityKind.Work, NpcZone.Interior, 0, new Vector2(100f, 100f));
            return point;
        }

        private static float Penetration(Collider first, Collider second)
            => Physics.ComputePenetration(first, first.transform.position, first.transform.rotation,
                second, second.transform.position, second.transform.rotation, out _, out float depth) ? depth : 0f;
    }
}
