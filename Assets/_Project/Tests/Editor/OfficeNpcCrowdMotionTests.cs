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
    public sealed class OfficeNpcCrowdMotionTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private InputActionAsset playerActions;
        private bool previousPlayOptionsEnabled;
        private EnterPlayModeOptions previousPlayOptions;
        private float previousScale;
        private float previousCaptureDeltaTime;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousPlayOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousPlayOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            yield return new EnterPlayMode(false);
            previousScale = Time.timeScale;
            previousCaptureDeltaTime = Time.captureDeltaTime;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 30f;
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "NPC crowd test floor";
            floor.transform.position = Vector3.down * 0.1f;
            floor.transform.localScale = new Vector3(12f, 0.2f, 12f);
            objects.Add(floor);
            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one),
                    size = floor.transform.localScale,
                    area = 0
                }
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
            if (instance.valid) instance.Remove();
            if (data != null) Object.DestroyImmediate(data);
            if (playerActions != null) Object.DestroyImmediate(playerActions);
            Time.timeScale = previousScale;
            Time.captureDeltaTime = previousCaptureDeltaTime;
            yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptionsEnabled = previousPlayOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousPlayOptions;
        }

        [UnityTest]
        public IEnumerator HeadOnStoryActorsBothReachTheirExchangedDestinations()
        {
            OfficeNpcAgent first = CreateActor("First", new Vector3(-3f, 0f, 0f));
            OfficeNpcAgent second = CreateActor("Second", new Vector3(3f, 0f, 0f));
            yield return null;
            Vector3 firstGoal = second.transform.position;
            Vector3 secondGoal = first.transform.position;
            Assert.That(first.TrySetStoryDestination(firstGoal, 2f), Is.True);
            Assert.That(second.TrySetStoryDestination(secondGoal, 2f), Is.True);
            float overlappingSeconds = 0f;
            float deadline = Time.time + 12f;
            while ((!first.StoryAtDestination || !second.StoryAtDestination) && Time.time < deadline)
            {
                AssertNoSustainedPenetration(first.GetComponent<Collider>(), second.GetComponent<Collider>(),
                    ref overlappingSeconds);
                yield return null;
            }
            Assert.That(first.StoryAtDestination, Is.True, "The first NPC must pass oncoming traffic and finish its order.");
            Assert.That(second.StoryAtDestination, Is.True, "The second NPC must also finish instead of mutual deadlock.");
            Assert.That(Vector3.Distance(first.transform.position, firstGoal), Is.LessThan(0.6f));
            Assert.That(Vector3.Distance(second.transform.position, secondGoal), Is.LessThan(0.6f));
        }

        [UnityTest]
        public IEnumerator HeadOnTrafficBesideAnUnbakedWallPreservesPhysicalClearance()
        {
            Collider wall = CreateWall(new Vector3(0f, 1f, -0.7f), new Vector3(12f, 2f, 0.4f));
            OfficeNpcAgent first = CreateActor("First beside wall", new Vector3(-3f, 0f, 0f));
            OfficeNpcAgent second = CreateActor("Second beside wall", new Vector3(3f, 0f, 0f));
            yield return null;
            Assert.That(first.TrySetStoryDestination(second.transform.position, 2f), Is.True);
            Assert.That(second.TrySetStoryDestination(first.transform.position, 2f), Is.True);
            float overlappingSeconds = 0f;
            float deadline = Time.time + 12f;
            while ((!first.StoryAtDestination || !second.StoryAtDestination) && Time.time < deadline)
            {
                AssertNoSustainedPenetration(first.GetComponent<Collider>(), second.GetComponent<Collider>(),
                    ref overlappingSeconds);
                AssertNoPenetration(first.GetComponent<Collider>(), wall);
                AssertNoPenetration(second.GetComponent<Collider>(), wall);
                yield return null;
            }
            Assert.That(first.StoryAtDestination, Is.True,
                "Native avoidance must also finish when the preferred passing side has a physical wall.");
            Assert.That(second.StoryAtDestination, Is.True);
        }

        [UnityTest]
        public IEnumerator StationaryAmbientWorkerClearsANarrowDoorwayForAnNpc()
        {
            // Only the floor was baked. This one-metre physical doorway cannot fit
            // two 68cm capsules abreast; the idle occupant must leave its opening.
            Collider[] walls = CreateDoorway();
            OfficeNpcAgent worker = CreateActor("Doorway worker", Vector3.zero, true);
            OfficeNpcAgent passer = CreateActor("Passing NPC", new Vector3(-3f, 0f, 0f));
            NpcActivityPoint authoredHome = worker.Home;
            worker.ReleaseStoryHold();
            float readyDeadline = Time.time + 3f;
            while (worker.CurrentState != NpcState.Activity && Time.time < readyDeadline) yield return null;
            Assert.That(worker.CurrentState, Is.EqualTo(NpcState.Activity));
            Assert.That(passer.TrySetStoryDestination(new Vector3(3f, passer.transform.position.y, 0f), 1.5f), Is.True);
            float overlappingSeconds = 0f;
            float deadline = Time.time + 15f;
            while (!passer.StoryAtDestination && Time.time < deadline)
            {
                Assert.That(authoredHome.Occupant, Is.SameAs(worker),
                    "Temporarily yielding must retain the worker's existing activity reservation.");
                AssertNoSustainedPenetration(worker.GetComponent<Collider>(), passer.GetComponent<Collider>(), ref overlappingSeconds);
                foreach (Collider wall in walls)
                {
                    AssertNoPenetration(worker.GetComponent<Collider>(), wall);
                    AssertNoPenetration(passer.GetComponent<Collider>(), wall);
                }
                yield return null;
            }
            Assert.That(passer.StoryAtDestination, Is.True,
                "An idle worker must safely clear its doorway so the moving NPC can finish instead of deadlocking.");
            Assert.That(worker.Stationary, Is.True, "Yielding must preserve the worker's authored stationary role.");
            Assert.That(worker.Home, Is.SameAs(authoredHome));
            Assert.That(worker.Role, Is.EqualTo(NpcRole.Worker));
            deadline = Time.time + 8f;
            while ((worker.CurrentState != NpcState.Activity
                || Vector3.Distance(worker.transform.position, authoredHome.transform.position) >= 0.6f)
                && Time.time < deadline)
            {
                Assert.That(authoredHome.Occupant, Is.SameAs(worker));
                AssertNoSustainedPenetration(worker.GetComponent<Collider>(), passer.GetComponent<Collider>(), ref overlappingSeconds);
                foreach (Collider wall in walls) AssertNoPenetration(worker.GetComponent<Collider>(), wall);
                yield return null;
            }
            Assert.That(worker.CurrentState, Is.EqualTo(NpcState.Activity),
                "After traffic clears, the worker must resume its original activity.");
            Assert.That(Vector3.Distance(worker.transform.position, authoredHome.transform.position), Is.LessThan(0.6f));
            Assert.That(authoredHome.Occupant, Is.SameAs(worker));
        }

        [UnityTest]
        public IEnumerator DialogueInterruptingYieldStaysStillThenReturnsToTheOriginalActivity()
        {
            Collider[] walls = CreateDoorway();
            OfficeNpcAgent worker = CreateActor("Interrupted doorway worker", Vector3.zero, true);
            OfficeNpcAgent passer = CreateActor("Passing interrupted worker", new Vector3(-3f, 0f, 0f));
            NpcActivityPoint authoredHome = worker.Home;
            worker.ReleaseStoryHold();
            float readyDeadline = Time.time + 3f;
            while (worker.CurrentState != NpcState.Activity && Time.time < readyDeadline) yield return null;
            Assert.That(worker.CurrentState, Is.EqualTo(NpcState.Activity));
            Vector3 homePosition = authoredHome.transform.position;
            Assert.That(passer.TrySetStoryDestination(new Vector3(3f, passer.transform.position.y, 0f), 1.5f), Is.True);
            float deadline = Time.time + 8f;
            float overlappingSeconds = 0f;
            while (Mathf.Abs(worker.transform.position.z - homePosition.z) <= 0.8f && Time.time < deadline)
            {
                AssertNoSustainedPenetration(worker.GetComponent<Collider>(), passer.GetComponent<Collider>(), ref overlappingSeconds);
                foreach (Collider wall in walls) AssertNoPenetration(worker.GetComponent<Collider>(), wall);
                yield return null;
            }
            Assert.That(Mathf.Abs(worker.transform.position.z - homePosition.z), Is.GreaterThan(0.8f),
                "The regression must interrupt an actual yield away from the authored home, rather than ordinary idle dialogue.");
            var dialogueTarget = new GameObject("Yield interruption dialogue target");
            objects.Add(dialogueTarget);
            dialogueTarget.transform.position = worker.transform.position + Vector3.back;
            Vector3 dialoguePosition = worker.transform.position;
            worker.BeginPlayerInteraction(dialogueTarget.transform);
            deadline = Time.time + 12f;
            float minimumDialogueUntil = Time.time + 2f;
            while ((!passer.StoryAtDestination || Time.time < minimumDialogueUntil) && Time.time < deadline)
            {
                Assert.That(worker.CurrentState, Is.EqualTo(NpcState.PlayerConversation));
                Assert.That(Vector3.Distance(worker.transform.position, dialoguePosition), Is.LessThan(0.03f),
                    "An active yield must stop immediately and remain stopped throughout dialogue.");
                Assert.That(authoredHome.Occupant, Is.SameAs(worker));
                AssertNoSustainedPenetration(worker.GetComponent<Collider>(), passer.GetComponent<Collider>(), ref overlappingSeconds);
                foreach (Collider wall in walls)
                {
                    AssertNoPenetration(worker.GetComponent<Collider>(), wall);
                    AssertNoPenetration(passer.GetComponent<Collider>(), wall);
                }
                yield return null;
            }
            Assert.That(passer.StoryAtDestination, Is.True, "Traffic should pass while the yielding worker remains in dialogue.");
            worker.EndPlayerInteraction();
            deadline = Time.time + 8f;
            while ((worker.CurrentState != NpcState.Activity
                || Vector3.Distance(worker.transform.position, homePosition) >= 0.6f)
                && Time.time < deadline)
            {
                Assert.That(authoredHome.Occupant, Is.SameAs(worker));
                AssertNoSustainedPenetration(worker.GetComponent<Collider>(), passer.GetComponent<Collider>(), ref overlappingSeconds);
                foreach (Collider wall in walls) AssertNoPenetration(worker.GetComponent<Collider>(), wall);
                yield return null;
            }
            Assert.That(worker.CurrentState, Is.EqualTo(NpcState.Activity),
                "Ending a yield-interrupted conversation must restore the original activity instead of leaving a distant idle NPC.");
            Assert.That(worker.Home, Is.SameAs(authoredHome));
            Assert.That(authoredHome.Occupant, Is.SameAs(worker));
            Assert.That(Vector3.Distance(worker.transform.position, homePosition), Is.LessThan(0.6f));
        }

        [UnityTest]
        public IEnumerator CinematicPlayerCrossesOccupiedDoorwayAndWorkerReturnsToItsActivity()
        {
            Collider[] walls = CreateDoorway();
            OfficeNpcAgent worker = CreateActor("Cinematic player doorway worker", Vector3.zero, true);
            NpcActivityPoint authoredHome = worker.Home;
            worker.ReleaseStoryHold();
            StoryPlayerCinematicMover mover = CreateCinematicPlayer(new Vector3(-3f, 0.05f, 0f));
            CharacterController playerBody = mover.GetComponent<CharacterController>();
            float deadline = Time.time + 3f;
            while (worker.CurrentState != NpcState.Activity && Time.time < deadline) yield return null;
            Assert.That(worker.CurrentState, Is.EqualTo(NpcState.Activity));
            bool arrived = false;
            Assert.That(mover.BeginMoveTo(new Vector3(3f, 0f, 0f), 1.5f, 0.25f, () => arrived = true), Is.True);
            float overlappingSeconds = 0f;
            deadline = Time.time + 15f;
            while (!arrived && Time.time < deadline)
            {
                Assert.That(authoredHome.Occupant, Is.SameAs(worker));
                AssertNoSustainedPenetration(worker.GetComponent<Collider>(), playerBody, ref overlappingSeconds);
                foreach (Collider wall in walls)
                {
                    AssertNoPenetration(worker.GetComponent<Collider>(), wall);
                    AssertNoPenetration(playerBody, wall);
                }
                yield return null;
            }
            Assert.That(arrived, Is.True,
                "The cinematic MC must request passage and cross a doorway occupied by an idle ambient worker.");
            Assert.That(mover.transform.position.x, Is.GreaterThan(2.6f));
            Assert.That(mover.GetComponent<PlayerController>().StoryCinematicMovementLocked, Is.False);
            deadline = Time.time + 8f;
            while ((worker.CurrentState != NpcState.Activity
                || Vector3.Distance(worker.transform.position, authoredHome.transform.position) >= 0.6f)
                && Time.time < deadline)
            {
                Assert.That(authoredHome.Occupant, Is.SameAs(worker));
                AssertNoSustainedPenetration(worker.GetComponent<Collider>(), playerBody, ref overlappingSeconds);
                foreach (Collider wall in walls) AssertNoPenetration(worker.GetComponent<Collider>(), wall);
                yield return null;
            }
            Assert.That(worker.CurrentState, Is.EqualTo(NpcState.Activity));
            Assert.That(worker.Home, Is.SameAs(authoredHome));
            Assert.That(authoredHome.Occupant, Is.SameAs(worker));
            Assert.That(Vector3.Distance(worker.transform.position, authoredHome.transform.position), Is.LessThan(0.6f),
                PassageReturnState(worker, mover));
        }

        [UnityTest]
        public IEnumerator StoryHeldSeatedAndPlayerDialogueActorsKeepTheirPositions()
        {
            var protectedActors = new[]
            {
                CreateActor("Held", new Vector3(0f, 0f, -3f)),
                CreateActor("Seated", Vector3.zero),
                CreateActor("Dialogue", new Vector3(0f, 0f, 3f), true)
            };
            protectedActors[1].SetStorySeated(true);
            protectedActors[2].ReleaseStoryHold();
            float readyDeadline = Time.time + 3f;
            while (protectedActors[2].CurrentState != NpcState.Activity && Time.time < readyDeadline) yield return null;
            Assert.That(protectedActors[2].CurrentState, Is.EqualTo(NpcState.Activity));
            var dialogueTarget = new GameObject("Dialogue player target");
            objects.Add(dialogueTarget);
            dialogueTarget.transform.position = new Vector3(0f, 0f, 4f);
            protectedActors[2].BeginPlayerInteraction(dialogueTarget.transform);
            var starts = new Vector3[protectedActors.Length];
            var passers = new OfficeNpcAgent[protectedActors.Length];
            for (int i = 0; i < protectedActors.Length; i++)
            {
                starts[i] = protectedActors[i].transform.position;
                passers[i] = CreateActor("Passing protected actor " + i, starts[i] + Vector3.left * 3f);
            }
            yield return null;
            for (int i = 0; i < passers.Length; i++)
                Assert.That(passers[i].TrySetStoryDestination(starts[i] + Vector3.right * 3f, 1.5f), Is.True);
            float deadline = Time.time + 10f;
            while (Time.time < deadline)
            {
                for (int i = 0; i < protectedActors.Length; i++)
                    Assert.That(Vector3.Distance(protectedActors[i].transform.position, starts[i]), Is.LessThan(0.03f),
                        "Crowd yielding must never displace a held, seated, or dialogue actor.");
                yield return null;
            }
            Assert.That(protectedActors[0].CurrentState, Is.EqualTo(NpcState.StoryHeld));
            Assert.That(protectedActors[1].StorySeated, Is.True);
            Assert.That(protectedActors[2].CurrentState, Is.EqualTo(NpcState.PlayerConversation));
        }

        [UnityTest]
        public IEnumerator CrowdRecoveryCannotCrossAFullWidthUnbakedWall()
        {
            Collider wall = CreateWall(new Vector3(0f, 1f, 0f), new Vector3(0.4f, 2f, 12f));
            OfficeNpcAgent actor = CreateActor("Blocked NPC", new Vector3(-3f, 0f, 0f));
            yield return null;
            Assert.That(actor.TrySetStoryDestination(new Vector3(3f, actor.transform.position.y, 0f), 2f), Is.True,
                "The saved floor mesh has a complete path, making the physical wall guard essential.");
            float deadline = Time.time + 7f;
            while (Time.time < deadline)
            {
                AssertNoPenetration(actor.GetComponent<Collider>(), wall);
                Assert.That(actor.transform.position.x, Is.LessThan(-0.45f),
                    "A congestion retry must never teleport or push an NPC through a real wall.");
                yield return null;
            }
            Assert.That(actor.StoryAtDestination, Is.False);
            Assert.That(actor.StoryMoveRequested, Is.True, "A blocked story order should remain pending.");
        }

        private OfficeNpcAgent CreateActor(string name, Vector3 requested, bool stationary = false)
        {
            Assert.That(NavMesh.SamplePosition(requested, out NavMeshHit hit, 0.5f,
                new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas }), Is.True);
            var homeObject = new GameObject(name + " home");
            objects.Add(homeObject);
            homeObject.transform.position = hit.position;
            NpcActivityPoint home = homeObject.AddComponent<NpcActivityPoint>();
            home.Configure("Home", NpcActivityKind.Work, NpcZone.Interior, 0, new Vector2(30f, 30f));
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.position = hit.position;
            objects.Add(root);
            NavMeshAgent navigation = root.AddComponent<NavMeshAgent>();
            navigation.agentTypeID = 0;
            navigation.radius = 0.34f;
            navigation.height = 1.9f;
            navigation.stoppingDistance = 0.22f;
            navigation.acceleration = 20f;
            navigation.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            var body = root.AddComponent<CapsuleCollider>();
            body.radius = 0.34f;
            body.height = 1.9f;
            body.center = Vector3.up * 0.95f;
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            OfficeNpcAgent actor = root.AddComponent<OfficeNpcAgent>();
            actor.Configure(name, NpcRole.Worker, home, new[] { home }, stationary);
            actor.HoldForStory();
            root.SetActive(true);
            return actor;
        }

        private Collider CreateWall(Vector3 position, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(wall);
            wall.name = "Unbaked crowd test wall";
            wall.transform.position = position;
            wall.transform.localScale = scale;
            Physics.SyncTransforms();
            return wall.GetComponent<Collider>();
        }

        private Collider[] CreateDoorway() => new[]
        {
            CreateWall(new Vector3(0f, 1f, -3.25f), new Vector3(0.4f, 2f, 5.5f)),
            CreateWall(new Vector3(0f, 1f, 3.25f), new Vector3(0.4f, 2f, 5.5f))
        };

        private StoryPlayerCinematicMover CreateCinematicPlayer(Vector3 position)
        {
            playerActions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player");
            map.AddAction("Move", InputActionType.Value).expectedControlType = "Vector2";
            map.AddAction("Look", InputActionType.Value).expectedControlType = "Vector2";
            foreach (string name in new[] { "Sprint", "Crouch", "Jump", "Interact", "Journal", "Pause" })
                map.AddAction(name, InputActionType.Button);
            playerActions.AddActionMap(map);
            var root = new GameObject("Cinematic crowd test player");
            objects.Add(root);
            root.SetActive(false);
            root.transform.position = position;
            CharacterController body = root.AddComponent<CharacterController>();
            body.radius = 0.3f;
            body.height = 1.75f;
            body.center = Vector3.up * 0.875f;
            body.stepOffset = 0.32f;
            PlayerInputReader input = root.AddComponent<PlayerInputReader>();
            typeof(PlayerInputReader).GetField("inputActions", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(input, playerActions);
            PlayerController movement = root.AddComponent<PlayerController>();
            typeof(PlayerController).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(movement, input);
            movement.enabled = false;
            StoryPlayerCinematicMover mover = root.AddComponent<StoryPlayerCinematicMover>();
            Assert.That(mover.Configure(movement, body, 0), Is.True);
            root.SetActive(true);
            input.enabled = false;
            Physics.SyncTransforms();
            return mover;
        }

        private static void AssertNoPenetration(Collider first, Collider second)
        {
            if (Physics.ComputePenetration(first, first.transform.position, first.transform.rotation,
                second, second.transform.position, second.transform.rotation, out _, out float depth))
                Assert.That(depth, Is.LessThan(0.06f), "Crowd steering must preserve clearance from solid walls.");
        }

        private static string PassageReturnState(OfficeNpcAgent worker, StoryPlayerCinematicMover mover)
        {
            NpcPassageYield passage = worker.GetComponent<NpcPassageYield>();
            object Field(string name) => typeof(NpcPassageYield).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(passage);
            return $"Worker failed to return: worker={worker.transform.position}, MC={mover.transform.position}, "
                + $"phase={Field("phase")}, original={Field("originalPosition")}, pocket={Field("pocketPosition")}, "
                + $"escape={Field("escapePosition")}, progressAt={Field("yieldProgressTime")}, now={Time.time}, "
                + $"lastRequest={Field("lastRequest")}, hasPath={worker.Navigation.hasPath}, "
                + $"stopped={worker.Navigation.isStopped}, velocity={worker.Navigation.velocity}, "
                + $"steering={worker.Navigation.steeringTarget}.";
        }

        private static void AssertNoSustainedPenetration(Collider first, Collider second, ref float overlappingSeconds)
        {
            bool deepOverlap = Physics.ComputePenetration(first, first.transform.position, first.transform.rotation,
                second, second.transform.position, second.transform.rotation, out _, out float depth) && depth > 0.06f;
            overlappingSeconds = deepOverlap ? overlappingSeconds + Time.deltaTime : 0f;
            Assert.That(overlappingSeconds, Is.LessThan(0.25f), "Two physical bodies must not remain pushed into each other.");
        }
    }
}
