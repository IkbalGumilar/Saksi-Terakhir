using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace SaksiTerakhir.Tests
{
    public sealed class NpcEscortSpacingTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();
        private InputActionAsset actions;
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private PlayerController movement;
        private StoryPlayerCinematicMover mover;
        private CharacterController body;
        private bool oldOptionsEnabled;
        private EnterPlayModeOptions oldOptions;
        private float oldScale, oldCapture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            oldOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            yield return new EnterPlayMode(false);
            oldScale = Time.timeScale;
            oldCapture = Time.captureDeltaTime;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 30f;
            Box("Escort spacing floor", Vector3.down * 0.1f, new Vector3(14f, 0.2f, 14f));
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(Vector3.down * 0.1f, Quaternion.identity, Vector3.one),
                    size = new Vector3(14f, 0.2f, 14f), area = 0 }
            }, new Bounds(Vector3.zero, new Vector3(20f, 8f, 20f)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            instance = NavMesh.AddNavMeshData(data);
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player");
            map.AddAction("Move", InputActionType.Value).expectedControlType = "Vector2";
            map.AddAction("Look", InputActionType.Value).expectedControlType = "Vector2";
            foreach (string name in new[] { "Sprint", "Crouch", "Jump", "Interact", "Journal", "Pause" })
                map.AddAction(name, InputActionType.Button);
            actions.AddActionMap(map);
            GameObject player = Root("Escort spacing MC");
            player.SetActive(false);
            player.transform.position = new Vector3(0f, 0.05f, -2.2f);
            body = player.AddComponent<CharacterController>();
            body.radius = 0.3f;
            body.height = 1.75f;
            body.center = Vector3.up * 0.875f;
            body.stepOffset = 0.32f;
            var input = player.AddComponent<PlayerInputReader>();
            Set(input, "inputActions", actions);
            movement = player.AddComponent<PlayerController>();
            Set(movement, "input", input);
            mover = player.AddComponent<StoryPlayerCinematicMover>();
            mover.Configure(movement, body, 0);
            player.SetActive(true);
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject root in roots) if (root != null) Object.DestroyImmediate(root);
            roots.Clear();
            if (instance.valid) instance.Remove();
            if (data != null) Object.DestroyImmediate(data);
            if (actions != null) Object.DestroyImmediate(actions);
            Time.timeScale = oldScale;
            Time.captureDeltaTime = oldCapture;
            yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptionsEnabled = oldOptionsEnabled;
            EditorSettings.enterPlayModeOptions = oldOptions;
        }

        [UnityTest]
        public IEnumerator MovingColleagueKeepsMcWalkingWithoutPeriodicStops()
        {
            const float colleagueSpeed = 2.2f;
            const float escortGap = 2.2f;
            GameObject colleague = Root("Colleague walking at the chapter escort speed");
            AddBody(colleague);
            Assert.That(mover.BeginFollow(colleague.transform, escortGap, 2.5f), Is.True);
            int stoppedFrames = 0;
            int sampledFrames = 0;
            float largestGap = 0f;
            float startedAt = Time.time;
            while (Time.time - startedAt < 2.2f)
            {
                Vector3 previousPosition = movement.transform.position;
                colleague.transform.position += Vector3.forward * colleagueSpeed * Time.deltaTime;
                Physics.SyncTransforms();
                yield return null;
                if (Time.time - startedAt < 0.75f) continue;
                float stride = Vector3.ProjectOnPlane(movement.transform.position - previousPosition,
                    Vector3.up).magnitude;
                if (stride < colleagueSpeed * Time.deltaTime * 0.05f) stoppedFrames++;
                sampledFrames++;
                largestGap = Mathf.Max(largestGap, FlatGap(colleague.transform));
                Assert.That(FlatGap(colleague.transform), Is.GreaterThanOrEqualTo(escortGap - 0.05f),
                    "Following a moving colleague must also retain the safe gap.");
            }
            Assert.That(sampledFrames, Is.GreaterThan(30), "Observe sustained following after startup.");
            Assert.That(stoppedFrames, Is.Zero,
                "On a clear floor, a continuously walking colleague must not cause periodic stops at stale path endpoints.");
            Assert.That(largestGap, Is.LessThan(escortGap + 0.15f));

            float deadline = Time.time + 1.5f;
            while (Time.time < deadline)
            {
                yield return null;
                Assert.That(FlatGap(colleague.transform), Is.GreaterThanOrEqualTo(escortGap - 0.05f),
                    "Continuous following must still preserve the safe distance when the colleague stops.");
            }
            Assert.That(FlatGap(colleague.transform), Is.LessThan(escortGap + 0.05f));
            Assert.That(movement.CurrentSpeed, Is.LessThan(0.02f));
            Assert.That(movement.StoryCinematicMovementLocked, Is.True);
            Assert.That(mover.IsFollowing, Is.True);
        }

        [UnityTest]
        public IEnumerator AnotherNpcBlockingViewDoesNotPullMcCloserThanTheExistingEscortGap()
        {
            GameObject colleague = Root("Escort colleague facing MC");
            colleague.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            AddBody(colleague);
            // An immobile NPC represents a crowd member already wedged in a passage.
            // It must not be able to yield away before the minimum-gap guard is observed.
            GameObject crowd = Root("Wedged NPC occluding colleague");
            crowd.SetActive(false);
            crowd.transform.position = new Vector3(0f, 0f, -0.4f);
            AddBody(crowd);
            var navigation = crowd.AddComponent<NavMeshAgent>();
            navigation.enabled = false;
            var npc = crowd.AddComponent<OfficeNpcAgent>();
            npc.enabled = false;
            crowd.SetActive(true);
            Physics.SyncTransforms();
            float initialGap = FlatGap(colleague.transform);
            Assert.That(initialGap, Is.LessThan(2.5f));
            Assert.That(Physics.Raycast(movement.transform.position + Vector3.up * 1.2f,
                Vector3.forward, out RaycastHit hit, initialGap), Is.True);
            Assert.That(hit.collider, Is.SameAs(crowd.GetComponent<CapsuleCollider>()),
                "The regression needs a third NPC physically blocking the view of the turning colleague.");
            Assert.That(mover.BeginFollow(colleague.transform, 2.5f, 2.2f), Is.True);
            Vector3 start = movement.transform.position;
            float deadline = Time.time + 1.2f;
            while (Time.time < deadline)
            {
                yield return null;
                Assert.That(FlatGap(colleague.transform), Is.GreaterThanOrEqualTo(initialGap - 0.02f),
                    "An NPC obscuring the colleague must not defeat the existing minimum escort gap.");
            }
            Assert.That(Vector3.ProjectOnPlane(movement.transform.position - start, Vector3.up).magnitude,
                Is.LessThan(0.03f), "The MC should wait without crowding the speaker, including stall recovery ticks.");
            Assert.That(movement.StoryCinematicMovementLocked, Is.True);
            Assert.That(mover.IsFollowing, Is.True);
        }

        [UnityTest]
        public IEnumerator StaticWallStillRequiresFollowingAroundItWhenRadialGapIsSmall()
        {
            body.enabled = false;
            movement.transform.position = new Vector3(0f, 0.05f, -1f);
            body.enabled = true;
            GameObject colleague = Root("Colleague beyond wall");
            colleague.transform.position = new Vector3(0f, 0f, 1f);
            colleague.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            AddBody(colleague);
            Collider wall = Box("Static wall between escort participants", new Vector3(0f, 1f, 0f),
                new Vector3(1.4f, 2f, 0.2f)).GetComponent<Collider>();
            Physics.SyncTransforms();
            Assert.That(FlatGap(colleague.transform), Is.LessThan(2.5f));
            Assert.That(mover.BeginFollow(colleague.transform, 2.5f, 2.2f), Is.True);
            Vector3 start = movement.transform.position;
            float maximumTravel = 0f;
            float deadline = Time.time + 3f;
            while (Time.time < deadline)
            {
                yield return null;
                maximumTravel = Mathf.Max(maximumTravel,
                    Vector3.ProjectOnPlane(movement.transform.position - start, Vector3.up).magnitude);
                if (Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    wall, wall.transform.position, wall.transform.rotation, out _, out float depth))
                    Assert.That(depth, Is.LessThan(0.025f), "Following around a wall must retain physical clearance.");
            }
            Assert.That(maximumTravel, Is.GreaterThan(0.3f),
                "A static wall must retain route-around behavior; horizontal distance alone cannot stop the escort.");
            Assert.That(movement.StoryCinematicMovementLocked, Is.True);
        }

        [UnityTest]
        public IEnumerator McClosesTheActualSlopeGapWhenTheColleagueIsWaitingForCatchUp()
        {
            BuildEscortSlope();
            body.enabled = false;
            Assert.That(NavMesh.SamplePosition(new Vector3(0f, 0f, -2.4f), out NavMeshHit playerStart,
                0.5f, NavMesh.AllAreas), Is.True);
            movement.transform.position = playerStart.position + Vector3.up * 0.05f;
            body.enabled = true;
            Assert.That(NavMesh.SamplePosition(new Vector3(0f, 1.2f, 0f), out NavMeshHit npcStart,
                0.5f, NavMesh.AllAreas), Is.True);
            GameObject homeRoot = Root("Slope colleague home");
            homeRoot.transform.position = npcStart.position;
            var home = homeRoot.AddComponent<NpcActivityPoint>();
            home.Configure("Slope colleague home", NpcActivityKind.Work, NpcZone.Interior, 0,
                new Vector2(100f, 100f));
            GameObject colleague = Root("Colleague waiting halfway up slope");
            colleague.SetActive(false);
            colleague.transform.position = npcStart.position;
            var navigation = colleague.AddComponent<NavMeshAgent>();
            navigation.agentTypeID = 0;
            navigation.radius = 0.34f;
            navigation.height = 1.9f;
            navigation.acceleration = 20f;
            AddBody(colleague);
            var rigidbody = colleague.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            var actor = colleague.AddComponent<OfficeNpcAgent>();
            actor.Configure("Slope colleague", NpcRole.Worker, home, new[] { home }, true);
            actor.HoldForStory();
            colleague.SetActive(true);
            yield return null;
            actor.SetStoryFollowDistance(movement.transform, 2.5f);
            Assert.That(actor.TrySetStoryDestination(new Vector3(0f, 3.2f, 4f), 1.5f), Is.True);
            Assert.That(FlatGap(colleague.transform), Is.LessThan(2.5f));
            Assert.That(Mathf.Abs(colleague.transform.position.y - movement.transform.position.y),
                Is.GreaterThan(0.9f), "The fixture must exercise a meaningful change in height.");
            Assert.That(Vector3.Distance(colleague.transform.position, movement.transform.position),
                Is.GreaterThan(2.6f));
            Assert.That(actor.StoryWaitingForPlayer, Is.True,
                "The story actor correctly waits for the actual three-dimensional escort distance.");
            Physics.SyncTransforms();
            Assert.That(mover.BeginFollow(colleague.transform, 2.5f, 2.2f), Is.True);
            Vector3 start = movement.transform.position;
            bool caughtUp = false;
            float deadline = Time.time + 4f;
            while (!caughtUp && Time.time < deadline)
            {
                yield return null;
                caughtUp = !actor.StoryWaitingForPlayer;
                Assert.That(movement.transform.position.y, Is.GreaterThan(-0.12f),
                    "Catch-up must remain on the supported ramp.");
            }
            Assert.That(Vector3.Distance(movement.transform.position, start), Is.GreaterThan(0.12f),
                "MC must walk up the slope instead of considering the smaller horizontal gap sufficient.");
            Assert.That(caughtUp, Is.True,
                "Both escort participants must use the same distance metric so the waiting colleague can resume.");
            Assert.That(movement.StoryCinematicMovementLocked, Is.True);
        }

        private void BuildEscortSlope()
        {
            if (instance.valid) instance.Remove();
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(roots[0]);
            GameObject ramp = Root("Supported escort catch-up ramp");
            Quaternion rotation = Quaternion.Euler(-Mathf.Atan(0.5f) * Mathf.Rad2Deg, 0f, 0f);
            Vector3 center = new Vector3(0f, 1.2f - 0.1f / Mathf.Cos(Mathf.Atan(0.5f)), 0f);
            Vector3 size = new Vector3(6f, 0.2f, 12f);
            ramp.transform.SetPositionAndRotation(center, rotation);
            ramp.AddComponent<BoxCollider>().size = size;
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(center, rotation, Vector3.one), size = size, area = 0 }
            }, new Bounds(new Vector3(0f, 1.2f, 0f), new Vector3(16f, 12f, 18f)),
                Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            instance = NavMesh.AddNavMeshData(data);
            Assert.That(instance.valid, Is.True);
            Physics.SyncTransforms();
        }

        private float FlatGap(Transform target)
            => Vector3.ProjectOnPlane(movement.transform.position - target.position, Vector3.up).magnitude;

        private static void AddBody(GameObject actor)
        {
            var capsule = actor.AddComponent<CapsuleCollider>();
            capsule.radius = 0.34f;
            capsule.height = 1.75f;
            capsule.center = Vector3.up * 0.875f;
        }

        private GameObject Root(string name)
        {
            var root = new GameObject(name);
            roots.Add(root);
            return root;
        }

        private GameObject Box(string name, Vector3 position, Vector3 size)
        {
            GameObject root = Root(name);
            root.transform.position = position;
            root.AddComponent<BoxCollider>().size = size;
            return root;
        }

        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
