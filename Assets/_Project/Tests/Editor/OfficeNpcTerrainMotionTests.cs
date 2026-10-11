using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SaksiTerakhir.Tests
{
    public sealed class OfficeNpcTerrainMotionTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private NavMeshLinkInstance stairLink;
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
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "NPC test floor";
            floor.transform.position = Vector3.down * 0.1f;
            floor.transform.localScale = new Vector3(12f, 0.2f, 12f);
            objects.Add(floor);
            var settings = NavMesh.GetSettingsByID(0);
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
            data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(Vector3.zero, new Vector3(14f, 4f, 14f)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            instance = NavMesh.AddNavMeshData(data);
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var item in objects) if (item != null) Object.DestroyImmediate(item);
            objects.Clear();
            if (NavMesh.IsLinkValid(stairLink)) NavMesh.RemoveLink(stairLink);
            if (instance.valid) instance.Remove();
            if (data != null) Object.DestroyImmediate(data);
            Time.timeScale = previousScale;
            Time.captureDeltaTime = previousCaptureDeltaTime;
            yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptionsEnabled = previousPlayOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousPlayOptions;
        }

        [UnityTest]
        public IEnumerator PhysicallySupportedStepProjectionUsesTheAllowedClimbHeight()
        {
            instance.Remove();
            Object.DestroyImmediate(data);
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            // A saved stair polygon can be slightly above the actual supported
            // step. The projection is greater than the old fixed 18 cm limit.
            data = NavMeshBuilder.BuildNavMeshData(settings, new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(new Vector3(0f, 0.12f, 0f), Quaternion.identity, Vector3.one),
                    size = new Vector3(12f, 0.2f, 12f), area = 0 }
            }, new Bounds(Vector3.zero, new Vector3(14f, 4f, 14f)), Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
            var root = new GameObject("Supported stair projection probe");
            objects.Add(root);
            var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            Assert.That(NavMesh.SamplePosition(Vector3.zero, out NavMeshHit projection, 1f, filter), Is.True);
            Assert.That(projection.position.y, Is.GreaterThan(0.18f));
            Assert.That(projection.position.y, Is.LessThan(0.35f));
            Physics.SyncTransforms();
            yield return null;
            Assert.That(TerrainNavigationProbe.TryResolveDirection(root.transform, Vector3.right, 0.65f,
                0.34f, 1.9f, 0.32f, settings.agentSlope, filter, out _), Is.True,
                "A continuously supported step must not be rejected solely because its polygon is more than 18 cm above it.");
        }

        [UnityTest]
        public IEnumerator StoryGoalAlreadyAtTheActorMustReportArrival()
        {
            OfficeNpcAgent actor = CreateActor(new Vector3(-3f, 0f, 0f));
            yield return null;
            Assert.That(actor.Navigation.isOnNavMesh, Is.True);
            Assert.That(actor.TrySetStoryDestination(actor.transform.position, 2f), Is.True);
            float deadline = Time.time + 1f;
            while (!actor.StoryAtDestination && Time.time < deadline) yield return null;
            Assert.That(actor.StoryAtDestination, Is.True,
                "A zero-distance story path must finish instead of endlessly retrying a path with hasPath=false.");
            Assert.That(actor.StoryMoveRequested, Is.False);
        }

        [UnityTest]
        public IEnumerator PhysicalObstacleOnABakedRouteMustBeAvoided()
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Collider added after NavMesh bake";
            obstacle.transform.position = new Vector3(0f, 1f, 0f);
            obstacle.transform.localScale = new Vector3(1.6f, 2f, 2.2f);
            objects.Add(obstacle);
            Physics.SyncTransforms();
            OfficeNpcAgent actor = CreateActor(new Vector3(-3f, 0f, 0f));
            yield return null;
            Assert.That(actor.TrySetStoryDestination(new Vector3(3f, actor.transform.position.y, 0f), 2f), Is.True);
            CapsuleCollider body = actor.GetComponent<CapsuleCollider>();
            Collider block = obstacle.GetComponent<Collider>();
            float deadline = Time.time + 10f;
            while (!actor.StoryAtDestination && Time.time < deadline)
            {
                if (Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    block, block.transform.position, block.transform.rotation, out _, out float penetration))
                    Assert.That(penetration, Is.LessThan(0.06f),
                        "NPC navigation must not walk a physical capsule through a new collider absent from the saved bake.");
                yield return null;
            }
            Assert.That(actor.StoryAtDestination, Is.True,
                "The NPC should safely take the open route around this bounded obstruction.");
            Assert.That(Vector3.Distance(actor.transform.position, new Vector3(3f, actor.transform.position.y, 0f)),
                Is.LessThan(0.6f));
        }

        [UnityTest]
        public IEnumerator AmbientJourneyAvoidsAnUnbakedPhysicalObstacle()
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = new Vector3(0f, 1f, 0f);
            obstacle.transform.localScale = new Vector3(1.6f, 2f, 2.2f);
            objects.Add(obstacle);
            Physics.SyncTransforms();
            OfficeNpcAgent actor = CreateActor(new Vector3(-3f, 0f, 0f));
            yield return null;
            actor.Home.transform.position = new Vector3(3f, actor.transform.position.y, 0f);
            actor.Navigation.speed = 2f;
            actor.ReleaseStoryHold();
            CapsuleCollider body = actor.GetComponent<CapsuleCollider>();
            Collider block = obstacle.GetComponent<Collider>();
            float deadline = Time.time + 10f;
            while (actor.CurrentState != NpcState.Activity && Time.time < deadline)
            {
                if (Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    block, block.transform.position, block.transform.rotation, out _, out float penetration))
                    Assert.That(penetration, Is.LessThan(0.06f), "Ambient NPCs need the same physical guard as story orders.");
                yield return null;
            }
            Assert.That(actor.CurrentState, Is.EqualTo(NpcState.Activity));
            Assert.That(Vector3.Distance(actor.transform.position, actor.Home.transform.position), Is.LessThan(0.6f));
        }

        [UnityTest]
        public IEnumerator ProbeIgnoresOwnChildCollidersAndTriggers()
        {
            OfficeNpcAgent actor = CreateActor(new Vector3(-3f, 0f, -3f));
            yield return null;
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(actor.transform, false);
            child.transform.localPosition = new Vector3(0f, 1f, 0.4f);
            child.transform.localScale = new Vector3(1f, 2f, 0.2f);
            var trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trigger.transform.position = actor.transform.position + new Vector3(0f, 1f, 0.5f);
            trigger.transform.localScale = new Vector3(1f, 2f, 0.2f);
            trigger.GetComponent<Collider>().isTrigger = true;
            objects.Add(trigger);
            Physics.SyncTransforms();
            var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            Assert.That(TerrainNavigationProbe.TryResolveDirection(actor.transform, Vector3.forward,
                0.8f, 0.34f, 1.9f, 0.32f, 45f, filter, out Vector3 direction), Is.True);
            Assert.That(Vector3.Dot(direction, Vector3.forward), Is.GreaterThan(0.99f));
        }

        [UnityTest]
        public IEnumerator LayerTwoPhysicalBarrierIsDetectedButCanBeExplicitlyExcluded()
        {
            OfficeNpcAgent actor = CreateActor(new Vector3(-3f, 0f, -3f));
            yield return null;
            var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.layer = 2;
            barrier.transform.position = actor.transform.position + new Vector3(0f, 1f, 0.55f);
            barrier.transform.localScale = new Vector3(4f, 2f, 0.2f);
            Collider collider = barrier.GetComponent<Collider>();
            objects.Add(barrier);
            Physics.SyncTransforms();
            var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            bool canResolve = TerrainNavigationProbe.TryResolveDirection(actor.transform, Vector3.forward,
                0.8f, 0.34f, 1.9f, 0.32f, 45f, filter, out Vector3 blockedDirection);
            Assert.That(!canResolve || Vector3.Dot(blockedDirection, Vector3.forward) < 0.99f, Is.True,
                "IgnoreRaycast is a layer name, not permission to cross a solid quest barrier.");
            Assert.That(TerrainNavigationProbe.TryResolveDirection(actor.transform, Vector3.forward,
                0.8f, 0.34f, 1.9f, 0.32f, 45f, filter, out Vector3 allowedDirection,
                hit => hit == collider), Is.True);
            Assert.That(Vector3.Dot(allowedDirection, Vector3.forward), Is.GreaterThan(0.99f));
        }

        [UnityTest]
        public IEnumerator MissingPhysicalFloorStopsTheOrderAndRestoredGroundResumesIt()
        {
            OfficeNpcAgent actor = CreateActor(new Vector3(-3f, 0f, 0f));
            yield return null;
            Collider floor = objects[0].GetComponent<Collider>();
            floor.enabled = false;
            Physics.SyncTransforms();
            Vector3 start = actor.transform.position;
            Assert.That(actor.TrySetStoryDestination(start + Vector3.right * 2f, 2f), Is.True,
                "A valid saved path remains requested while physical ground is temporarily unavailable.");
            float pauseDeadline = Time.time + 0.25f;
            while (Time.time < pauseDeadline) yield return null;
            Assert.That(Vector3.Distance(actor.transform.position, start), Is.LessThan(0.03f));
            Assert.That(actor.StoryMoveRequested, Is.True);
            Assert.That(actor.StoryAtDestination, Is.False);
            floor.enabled = true;
            Physics.SyncTransforms();
            float deadline = Time.time + 4f;
            while (!actor.StoryAtDestination && Time.time < deadline) yield return null;
            Assert.That(actor.StoryAtDestination, Is.True,
                "Restoring safe physical support should resume an existing story order without teleporting.");
        }

        [UnityTest]
        public IEnumerator StoryNpcCrossesSupportedRampBetweenLinkedNavigationIslands()
        {
            BuildSupportedLinkedRamp();
            OfficeNpcAgent actor = CreateActor(new Vector3(0f, 0f, -3f));
            yield return null;
            Assert.That(actor.TrySetStoryDestination(new Vector3(0f, 1f, 3f), 2f), Is.True);
            Vector3 previous = actor.transform.position;
            bool traversedLink = false;
            float deadline = Time.time + 10f;
            while (!actor.StoryAtDestination && Time.time < deadline)
            {
                traversedLink |= actor.Navigation.isOnOffMeshLink;
                float permittedStep = Mathf.Max(0.4f, 2f * Time.deltaTime + 0.15f);
                Assert.That(Vector3.Distance(actor.transform.position, previous), Is.LessThan(permittedStep),
                    "A live navigation link must be physically traversed, not completed by ResetPath teleportation.");
                previous = actor.transform.position;
                yield return null;
            }
            Assert.That(traversedLink, Is.True, "This regression must exercise native agent link traversal.");
            Assert.That(actor.StoryAtDestination, Is.True,
                "NPC physical steering must preserve supported authored NavMesh links as well as normal polygons.");
            Assert.That(actor.transform.position.y, Is.GreaterThan(0.9f));
        }

        [UnityTest]
        public IEnumerator HoldingAnNpcDuringManualLinkTraversalPreservesItsPoseAndRestoresMovement()
        {
            BuildSupportedLinkedRamp();
            OfficeNpcAgent actor = CreateActor(new Vector3(0f, 0f, -3f));
            yield return null;
            Vector3 goal = new Vector3(0f, 1f, 3f);
            Assert.That(actor.TrySetStoryDestination(goal, 1.5f), Is.True);
            float deadline = Time.time + 8f;
            while ((!actor.Navigation.isOnOffMeshLink || actor.Navigation.updatePosition
                || Mathf.Abs(actor.transform.position.z) > 0.5f) && Time.time < deadline)
                yield return null;
            Assert.That(actor.Navigation.isOnOffMeshLink, Is.True,
                "The fixture must interrupt an actual link, not an ordinary polygon path.");
            Assert.That(actor.Navigation.updatePosition, Is.False,
                "The fixture must interrupt manual checked traversal while it owns position.");
            Assert.That(Mathf.Abs(actor.transform.position.z), Is.LessThanOrEqualTo(0.5f),
                "Interrupt the middle of the supported bridge, where ResetPath would jump to its endpoint.");
            Vector3 heldPosition = actor.transform.position;
            actor.HoldForStory();
            Assert.That(actor.CurrentState, Is.EqualTo(NpcState.StoryHeld));
            Assert.That(actor.StoryMoveRequested, Is.False);
            Assert.That(actor.Navigation.updatePosition, Is.True,
                "Cancelling a manual traversal must restore the previously authored position update setting.");
            deadline = Time.time + 0.5f;
            while (Time.time < deadline)
            {
                Assert.That(Vector3.Distance(actor.transform.position, heldPosition), Is.LessThan(0.08f),
                    "A story hold must retain the checked mid-link pose instead of completing the link by teleportation.");
                yield return null;
            }
            actor.TrySetStoryDestination(goal, 1.5f);
            Assert.That(actor.StoryMoveRequested, Is.True,
                "The replacement order must remain pending even if path validation needs a retry.");
            Vector3 previous = actor.transform.position;
            deadline = Time.time + 12f;
            while (!actor.StoryAtDestination && Time.time < deadline)
            {
                Assert.That(Vector3.Distance(actor.transform.position, previous), Is.LessThan(0.4f),
                    "Restoring movement after a link hold must use physical steps rather than snap to a landing.");
                previous = actor.transform.position;
                yield return null;
            }
            Assert.That(actor.StoryAtDestination, Is.True,
                "An interrupted manual traversal must not leave its replacement story order permanently frozen.");
            Assert.That(actor.Navigation.updatePosition, Is.True);
            Assert.That(Vector3.Distance(actor.transform.position, goal), Is.LessThan(0.6f));
        }

        private void BuildSupportedLinkedRamp()
        {
            if (instance.valid) instance.Remove();
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(objects[0]);
            var sources = new List<NavMeshBuildSource>();
            foreach (Vector3 center in new[] { new Vector3(0f, -0.1f, -3f), new Vector3(0f, 0.9f, 3f) })
            {
                var landing = new GameObject("NPC ramp landing");
                objects.Add(landing);
                landing.transform.position = center;
                landing.AddComponent<BoxCollider>().size = new Vector3(4f, 0.2f, 4f);
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one),
                    size = new Vector3(4f, 0.2f, 4f), area = 0 });
            }
            var ramp = new GameObject("NPC supported physical link ramp");
            objects.Add(ramp);
            float angle = Mathf.Atan2(1f, 3.2f) * Mathf.Rad2Deg;
            ramp.transform.SetPositionAndRotation(new Vector3(0f, 0.5f, 0f), Quaternion.Euler(-angle, 0f, 0f));
            ramp.AddComponent<BoxCollider>().size = new Vector3(2f, 0.1f, Mathf.Sqrt(3.2f * 3.2f + 1f));
            var settings = NavMesh.GetSettingsByID(0);
            data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(Vector3.zero, new Vector3(14f, 8f, 14f)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            instance = NavMesh.AddNavMeshData(data);
            var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            Assert.That(NavMesh.SamplePosition(new Vector3(0f, 0f, -1.65f), out NavMeshHit lower, 0.6f, filter), Is.True);
            Assert.That(NavMesh.SamplePosition(new Vector3(0f, 1f, 1.65f), out NavMeshHit upper, 0.6f, filter), Is.True);
            stairLink = NavMesh.AddLink(new NavMeshLinkData { startPosition = lower.position,
                endPosition = upper.position, width = 1f, bidirectional = true, agentTypeID = 0 });
            Assert.That(NavMesh.IsLinkValid(stairLink), Is.True);
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator ReleasingAMidLinkStoryHoldSafelyResumesTheAmbientActivity()
        {
            BuildSupportedLinkedRamp();
            OfficeNpcAgent actor = CreateActor(new Vector3(0f, 0f, -3f));
            yield return null;
            Assert.That(actor.TrySetStoryDestination(new Vector3(0f, 1f, 3f), 1.5f), Is.True);
            float deadline = Time.time + 8f;
            while ((!actor.Navigation.isOnOffMeshLink || actor.Navigation.updatePosition
                || Mathf.Abs(actor.transform.position.z) > 0.5f) && Time.time < deadline) yield return null;
            Assert.That(actor.Navigation.isOnOffMeshLink, Is.True);
            Assert.That(Mathf.Abs(actor.transform.position.z), Is.LessThanOrEqualTo(0.5f));
            actor.HoldForStory();
            actor.ReleaseStoryHold();
            Vector3 previous = actor.transform.position;
            deadline = Time.time + 20f;
            while (actor.CurrentState != NpcState.Activity && Time.time < deadline)
            {
                Assert.That(Vector3.Distance(actor.transform.position, previous), Is.LessThan(0.4f),
                    "Ambient resumption must finish its retained link before replacing the path.");
                previous = actor.transform.position;
                yield return null;
            }
            Assert.That(actor.CurrentState, Is.EqualTo(NpcState.Activity));
            Assert.That(actor.Navigation.updatePosition, Is.True);
            Assert.That(Vector3.Distance(actor.transform.position, actor.Home.transform.position), Is.LessThan(0.6f));
        }

        [UnityTest]
        public IEnumerator GroundSeamWithinSkinCanUseTheWalkableLanding()
        {
            Transform actor = CreateGroundSeamProbe(0f);
            Physics.SyncTransforms();
            yield return null;
            Assert.That(TerrainNavigationProbe.TryGetGroundPosition(actor, actor.position,
                0.32f, 45f, out Vector3 ground), Is.True,
                "A 2.15cm steep ramp end cap may use the immediately underlying flat landing.");
            Assert.That(ground.y, Is.EqualTo(0f).Within(0.005f));
        }

        [UnityTest]
        public IEnumerator ThinRampEndCapCanUseItsImmediatelyUnderlyingLanding()
        {
            Transform actor = CreateGroundSeamProbe(0.023f);
            Physics.SyncTransforms();
            yield return null;
            Assert.That(TerrainNavigationProbe.TryGetGroundPosition(actor, actor.position,
                0.32f, 45f, out Vector3 ground), Is.True,
                "A 4.45cm ramp end cap must not trap an actor beside its flat landing.");
            Assert.That(ground.y, Is.EqualTo(0f).Within(0.005f));
        }

        [UnityTest]
        public IEnumerator SteepSurfaceCannotBorrowAWalkableFloorFarBelowIt()
        {
            Transform actor = CreateGroundSeamProbe(0.12f);
            Physics.SyncTransforms();
            yield return null;
            Assert.That(TerrainNavigationProbe.TryGetGroundPosition(actor, actor.position,
                0.32f, 45f, out _), Is.False,
                "The seam exception must not skip a real steep surface 14cm above the floor.");
        }

        private Transform CreateGroundSeamProbe(float extraHeight)
        {
            var actor = new GameObject("Ground seam probe actor");
            objects.Add(actor);
            actor.transform.position = new Vector3(0f, 0.1276f, 0f);
            var ramp = new GameObject("Ground seam steep ramp cap");
            objects.Add(ramp);
            float angle = Mathf.Atan2(1f, 3.2f) * Mathf.Rad2Deg;
            ramp.transform.SetPositionAndRotation(new Vector3(0f, -0.5f + extraHeight, -1.5933f),
                Quaternion.Euler(-angle, 0f, 0f));
            ramp.AddComponent<BoxCollider>().size = new Vector3(2f, 0.1f, Mathf.Sqrt(3.2f * 3.2f + 1f));
            return actor.transform;
        }

        private OfficeNpcAgent CreateActor(Vector3 requested)
        {
            Assert.That(NavMesh.SamplePosition(requested, out NavMeshHit hit, 0.5f,
                new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas }), Is.True);
            var homeObject = new GameObject("NPC test home");
            objects.Add(homeObject);
            homeObject.transform.position = hit.position;
            NpcActivityPoint home = homeObject.AddComponent<NpcActivityPoint>();
            home.Configure("Home", NpcActivityKind.Work, NpcZone.Interior, 0, new Vector2(10f, 10f));
            var root = new GameObject("NPC terrain test actor");
            root.SetActive(false);
            root.transform.position = hit.position;
            objects.Add(root);
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
            OfficeNpcAgent actor = root.AddComponent<OfficeNpcAgent>();
            actor.Configure("Test", NpcRole.Worker, home, new[] { home }, false);
            actor.HoldForStory();
            root.SetActive(true);
            return actor;
        }
    }
}
