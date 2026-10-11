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
    public sealed class NpcCrowdPileupTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private NavMeshLinkInstance stairLink;
        private InputActionAsset actions;
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
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(floor);
            floor.name = "Pileup regression floor";
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
            if (NavMesh.IsLinkValid(stairLink)) NavMesh.RemoveLink(stairLink);
            if (instance.valid) instance.Remove();
            if (data != null) Object.DestroyImmediate(data);
            if (actions != null) Object.DestroyImmediate(actions);
            Time.timeScale = previousScale;
            Time.captureDeltaTime = previousCapture;
            yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousOptions;
        }

        [UnityTest]
        public IEnumerator PendingStoryMoveCanClearTrafficDuringPathRetry()
        {
            OfficeNpcAgent worker = CreateActor("Retrying story worker", Vector3.zero);
            var player = new GameObject("Player blocked by retrying story worker");
            objects.Add(player);
            player.transform.position = new Vector3(-1.1f, 0f, 0f);
            OfficeNpcAgent heldActor = CreateActor("Genuinely held story actor", new Vector3(0f, 0f, 3f));
            yield return null;
            Vector3 heldStart = heldActor.transform.position;
            player.transform.position = heldStart + Vector3.left * 1.1f;
            NpcPassageYield.Request(player.transform, Vector3.right, 0.3f, 1.8f);
            Assert.That(heldActor.StoryMoveRequested, Is.False);
            Assert.That(heldActor.GetComponent<NpcPassageYield>().IsActive, Is.False,
                "A genuinely held story actor must stay protected; pending path retry is the eligibility exception.");
            Assert.That(heldActor.transform.position, Is.EqualTo(heldStart));
            player.transform.position = new Vector3(-1.1f, 0f, 0f);
            Assert.That(worker.TrySetStoryDestination(new Vector3(4f, 0f, 0f), 1.5f), Is.True);
            // A real stall resets the path and enters StoryHeld while preserving the
            // pending story order. Reproduce that state before its next retry tick.
            worker.Navigation.ResetPath();
            worker.Navigation.isStopped = true;
            worker.Navigation.velocity = Vector3.zero;
            typeof(OfficeNpcAgent).GetField("<CurrentState>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(worker, NpcState.StoryHeld);
            Assert.That(worker.StoryMoveRequested, Is.True);
            NpcPassageYield.Request(player.transform, Vector3.right, 0.3f, 1.8f);
            Assert.That(worker.GetComponent<NpcPassageYield>().IsActive, Is.True,
                "A path-retrying story mover must remain eligible to clear traffic; a genuinely held actor has no pending move and stays protected.");
            float deadline = Time.time + 4f;
            while (Mathf.Abs(worker.transform.position.z) < 0.75f && Time.time < deadline)
            {
                NpcPassageYield.Request(player.transform, Vector3.right, 0.3f, 1.8f);
                yield return null;
            }
            Assert.That(Mathf.Abs(worker.transform.position.z), Is.GreaterThanOrEqualTo(0.75f));
            Object.DestroyImmediate(player);
            deadline = Time.time + 12f;
            while (!worker.StoryAtDestination && Time.time < deadline) yield return null;
            Assert.That(worker.StoryAtDestination, Is.True,
                "After clearing traffic, the retrying worker must resume and finish its pending story order.");
        }

        [UnityTest]
        public IEnumerator CinematicPlayerGetsThroughThreeWorkersInitiallyPiledInOneDoorway()
        {
            Collider[] walls = CreateDoorway();
            OfficeNpcAgent[] workers =
            {
                CreateActor("Piled doorway worker A", new Vector3(-2f, 0f, -1f)),
                CreateActor("Piled doorway worker B", new Vector3(-2f, 0f, 0f)),
                CreateActor("Piled doorway worker C", new Vector3(-2f, 0f, 1f))
            };
            foreach (OfficeNpcAgent worker in workers) worker.ReleaseStoryHold();
            float deadline = Time.time + 4f;
            while (!AllActive(workers) && Time.time < deadline) yield return null;
            Assert.That(AllActive(workers), Is.True);
            Vector3[] pile = { new Vector3(0f, 0f, -0.12f), new Vector3(0.08f, 0f, 0f),
                new Vector3(0f, 0f, 0.12f) };
            for (int i = 0; i < workers.Length; i++) Assert.That(workers[i].Navigation.Warp(pile[i]), Is.True);
            Physics.SyncTransforms();
            Assert.That(Penetration(workers[0].GetComponent<Collider>(), workers[1].GetComponent<Collider>()),
                Is.GreaterThan(0.3f), "This fixture must exercise a genuine multi-body pile, rather than three separate nearby actors.");
            Assert.That(Penetration(workers[1].GetComponent<Collider>(), workers[2].GetComponent<Collider>()),
                Is.GreaterThan(0.3f));
            StoryPlayerCinematicMover mover = CreatePlayer(new Vector3(-3f, 0.05f, 0f));
            bool arrived = false;
            Assert.That(mover.BeginMoveTo(new Vector3(3f, 0f, 0f), 1.5f, 0.25f, () => arrived = true), Is.True);
            deadline = Time.time + 20f;
            while (!arrived && Time.time < deadline)
            {
                foreach (Collider wall in walls)
                {
                    AssertWallClear(mover.GetComponent<Collider>(), wall);
                    foreach (OfficeNpcAgent worker in workers) AssertWallClear(worker.GetComponent<Collider>(), wall);
                }
                yield return null;
            }
            Assert.That(arrived, Is.True,
                "A player must cross the initial pile. MC=" + mover.transform.position + " crowd=" + Describe(workers));
            Assert.That(mover.transform.position.x, Is.GreaterThan(2.6f));
            Assert.That(mover.GetComponent<PlayerController>().StoryCinematicMovementLocked, Is.False);
            foreach (OfficeNpcAgent worker in workers)
                Assert.That(Penetration(worker.GetComponent<Collider>(), mover.GetComponent<Collider>()), Is.LessThan(0.03f));
        }

        [UnityTest]
        public IEnumerator ThreeStoryMoversEscapeAnInitialPileAndKeepTheirSeparateOrders()
        {
            Collider[] walls = CreateDoorway();
            OfficeNpcAgent[] workers =
            {
                CreateActor("A piled story mover", new Vector3(-0.8f, 0f, -0.14f)),
                CreateActor("B piled story mover", new Vector3(-0.72f, 0f, 0f)),
                CreateActor("C piled story mover", new Vector3(-0.8f, 0f, 0.14f))
            };
            yield return null;
            Vector3[] goals = { new Vector3(3.5f, 0f, -1.7f), new Vector3(4f, 0f, 0f), new Vector3(3.5f, 0f, 1.7f) };
            Vector3[] pile = { new Vector3(-0.8f, 0f, -0.14f), new Vector3(-0.72f, 0f, 0f), new Vector3(-0.8f, 0f, 0.14f) };
            for (int i = 0; i < workers.Length; i++)
            {
                Assert.That(workers[i].Navigation.Warp(pile[i]), Is.True);
                Assert.That(workers[i].TrySetStoryDestination(goals[i], 1.5f), Is.True);
            }
            Physics.SyncTransforms();
            Assert.That(Penetration(workers[0].GetComponent<Collider>(), workers[1].GetComponent<Collider>()), Is.GreaterThan(0.3f));
            float deadline = Time.time + 20f;
            while (!AllArrived(workers) && Time.time < deadline)
            {
                foreach (OfficeNpcAgent worker in workers)
                    foreach (Collider wall in walls) AssertWallClear(worker.GetComponent<Collider>(), wall);
                yield return null;
            }
            for (int i = 0; i < workers.Length; i++)
            {
                Assert.That(workers[i].StoryAtDestination, Is.True,
                    workers[i].name + " must finish its order: " + Describe(workers));
                Assert.That(Vector3.Distance(workers[i].transform.position, goals[i]), Is.LessThan(0.6f));
                for (int j = i + 1; j < workers.Length; j++)
                    Assert.That(Penetration(workers[i].GetComponent<Collider>(), workers[j].GetComponent<Collider>()), Is.LessThan(0.03f));
            }
        }

        [UnityTest]
        public IEnumerator EscortGapOnStairsDoesNotMoveMcCloserOrDisplaceTheLeader()
        {
            BuildSlopedPassage(true);
            OfficeNpcAgent leader = CreateActor("Escorted stair colleague", new Vector3(0f, 0.92f, 1.1f));
            StoryPlayerCinematicMover mover = CreatePlayer(new Vector3(0f, 0.13f, -1.1f));
            yield return null;
            Assert.That(leader.TrySetStoryDestination(new Vector3(0f, 1f, 3f), 1.5f), Is.True);
            Assert.That(mover.BeginFollow(leader.transform, 2.5f, 1.5f), Is.True);
            Vector3 start = mover.transform.position;
            Assert.That(Mathf.Abs(leader.transform.position.y - start.y), Is.GreaterThan(0.65f));
            Assert.That(Vector3.Distance(leader.transform.position, start), Is.LessThan(2.5f));
            yield return null;
            Assert.That(Vector3.ProjectOnPlane(mover.transform.position - start, Vector3.up).magnitude,
                Is.LessThan(0.02f), "A height difference on the same stair flight must not collapse the escort gap.");
            Assert.That(leader.GetComponent<NpcPassageYield>().IsActive, Is.False,
                "The automatic follower must not ask its own escort leader to abandon the route and yield.");
        }

        [UnityTest]
        public IEnumerator WorkerClearsASlopedSingleFilePassageUsingADifferentHeightPocket()
        {
            BuildSlopedPassage(true);
            OfficeNpcAgent worker = CreateActor("Worker standing halfway up stairs", new Vector3(0f, 0.55f, 0f));
            worker.ReleaseStoryHold();
            float deadline = Time.time + 4f;
            while (worker.CurrentState != NpcState.Activity && Time.time < deadline) yield return null;
            Assert.That(worker.CurrentState, Is.EqualTo(NpcState.Activity));
            Vector3 start = worker.transform.position;
            StoryPlayerCinematicMover mover = CreatePlayer(new Vector3(0f, 0.05f, -3f));
            bool arrived = false;
            Assert.That(mover.BeginMoveTo(new Vector3(0f, 1f, 3f), 1.5f, 0.25f, () => arrived = true), Is.True);
            float greatestYieldHeightChange = 0f;
            deadline = Time.time + 20f;
            while (!arrived && Time.time < deadline)
            {
                greatestYieldHeightChange = Mathf.Max(greatestYieldHeightChange,
                    Mathf.Abs(worker.transform.position.y - start.y));
                // The sloping bridge is physically supported but is too narrow for
                // side-by-side capsules. A usable side pocket exists only at a landing.
                if (Mathf.Abs(worker.transform.position.z) < 0.7f)
                    Assert.That(Mathf.Abs(worker.transform.position.x), Is.LessThan(0.42f),
                        "A stair-yield must walk onto its landing instead of sidestepping off the unsupported edge.");
                Assert.That(worker.transform.position.y, Is.GreaterThan(-0.12f));
                Assert.That(mover.transform.position.y, Is.GreaterThan(-0.12f));
                yield return null;
            }
            Assert.That(arrived, Is.True,
                "A different-height landing must remain usable. MC=" + mover.transform.position + " worker=" + Describe(new[] { worker }));
            Assert.That(greatestYieldHeightChange, Is.GreaterThan(0.3f),
                "The regression must actually exercise a supported height-changing retreat rather than a flat sidestep.");
            Assert.That(mover.transform.position.z, Is.GreaterThan(2.6f));
            Assert.That(Penetration(worker.GetComponent<Collider>(), mover.GetComponent<Collider>()), Is.LessThan(0.03f));
        }

        [UnityTest]
        public IEnumerator StoryActorUsesAPhysicalDetourInsteadOfFreezingOnANavigationLink()
        {
            BuildSlopedPassage(false);
            GameObject ramp = objects.Find(x => x != null && x.name == "Supported single-file sloping bridge");
            Vector3 size = ramp.GetComponent<BoxCollider>().size;
            size.x = 3f;
            ramp.GetComponent<BoxCollider>().size = size;
            Collider railing = CreateWall(new Vector3(0f, 1.1f, 0f), new Vector3(0.2f, 1.5f, 0.4f));
            OfficeNpcAgent actor = CreateActor("Story traveller beside stair railing", new Vector3(0f, 0f, -3f));
            yield return null;
            Assert.That(actor.TrySetStoryDestination(new Vector3(0f, 1f, 3f), 1.5f), Is.True);
            Vector3 previous = actor.transform.position;
            bool entered = false;
            float deadline = Time.time + 15f;
            while (!actor.StoryAtDestination && Time.time < deadline)
            {
                entered |= actor.Navigation.isOnOffMeshLink;
                AssertWallClear(actor.GetComponent<Collider>(), railing);
                Assert.That(Vector3.Distance(actor.transform.position, previous), Is.LessThan(0.16f),
                    "Link recovery must use bounded supported steps rather than snapping across its railing.");
                previous = actor.transform.position;
                yield return null;
            }
            Assert.That(entered, Is.True);
            Assert.That(actor.StoryAtDestination, Is.True, "The physical detour must finish: " + Describe(new[] { actor }));
        }

        [UnityTest]
        public IEnumerator OppositeStoryActorsQueueAndBothCrossANarrowSupportedNavigationLink()
        {
            BuildSlopedPassage(false);
            OfficeNpcAgent first = CreateActor("A ascending stair traveller", new Vector3(0f, 0f, -3f));
            OfficeNpcAgent second = CreateActor("B descending stair traveller", new Vector3(0f, 1f, 3f));
            yield return null;
            Assert.That(first.TrySetStoryDestination(new Vector3(-0.85f, 1f, 3f), 1.5f), Is.True);
            Assert.That(second.TrySetStoryDestination(new Vector3(0.85f, 0f, -3f), 1.5f), Is.True);
            float overlappingSeconds = 0f;
            bool usedLink = false;
            float deadline = Time.time + 20f;
            while ((!first.StoryAtDestination || !second.StoryAtDestination) && Time.time < deadline)
            {
                usedLink |= first.Navigation.isOnOffMeshLink || second.Navigation.isOnOffMeshLink;
                overlappingSeconds = Penetration(first.GetComponent<Collider>(), second.GetComponent<Collider>()) > 0.06f
                    ? overlappingSeconds + Time.deltaTime : 0f;
                Assert.That(overlappingSeconds, Is.LessThan(0.25f),
                    "Opposite directions on a single-file link must take turns instead of overlapping halfway across the stair bridge.");
                Assert.That(first.transform.position.y, Is.GreaterThan(-0.12f));
                Assert.That(second.transform.position.y, Is.GreaterThan(-0.12f));
                yield return null;
            }
            Assert.That(usedLink, Is.True, "This regression must traverse the explicit navigation link between separate landings.");
            Assert.That(first.StoryAtDestination, Is.True, "The ascending actor must finish its order after crossing queued traffic.");
            Assert.That(second.StoryAtDestination, Is.True, "The descending actor must finish instead of being permanently excluded from the link.");
        }

        [UnityTest]
        public IEnumerator LinkDetourKeepsNearbyCandidatesInsideATightClearanceWindow()
        {
            BuildSlopedPassage(false);
            GameObject ramp = objects.Find(x => x != null && x.name == "Supported single-file sloping bridge");
            Vector3 size = ramp.GetComponent<BoxCollider>().size;
            size.x = 3f;
            ramp.GetComponent<BoxCollider>().size = size;
            var walls = new[]
            {
                CreateWall(new Vector3(0f, 1f, 0f), new Vector3(0.2f, 2f, 0.4f)),
                CreateWall(new Vector3(1.08f, 1f, 0f), new Vector3(0.25f, 2f, 1.5f)),
                CreateWall(new Vector3(-1.08f, 1f, 0f), new Vector3(0.25f, 2f, 1.5f))
            };
            OfficeNpcAgent actor = CreateActor("Traveller through a tight detour window", new Vector3(0f, 0f, -3f));
            yield return null;
            Assert.That(actor.TrySetStoryDestination(new Vector3(0f, 1f, 3f), 1.5f), Is.True);
            float deadline = Time.time + 15f;
            while (!actor.StoryAtDestination && Time.time < deadline)
            {
                foreach (Collider wall in walls) AssertWallClear(actor.GetComponent<Collider>(), wall);
                yield return null;
            }
            Assert.That(actor.StoryAtDestination, Is.True,
                "Nearby waypoint choices cannot be merged when only one clears the narrow passage: " + Describe(new[] { actor }));
        }

        [UnityTest]
        public IEnumerator OwnedLinkCanBridgeASeamNarrowerThanItsFootStance()
        {
            BuildSlopedPassage(false);
            SplitPhysicalRamp(0.16f);
            OfficeNpcAgent actor = CreateActor("Traveller across a narrow floor seam", new Vector3(0f, 0f, -3f));
            yield return null;
            Assert.That(actor.TrySetStoryDestination(new Vector3(0f, 1f, 3f), 1.5f), Is.True);
            Vector3 previous = actor.transform.position;
            float deadline = Time.time + 15f;
            while (!actor.StoryAtDestination && Time.time < deadline)
            {
                Assert.That(Vector3.Distance(actor.transform.position, previous), Is.LessThan(0.16f));
                previous = actor.transform.position;
                yield return null;
            }
            Assert.That(actor.StoryAtDestination, Is.True,
                "Both opposing foot supports should bridge this 16cm authored-link seam: " + Describe(new[] { actor }));
        }

        [UnityTest]
        public IEnumerator OwnedLinkCannotCrossAWideUnsupportedGap()
        {
            BuildSlopedPassage(false);
            SplitPhysicalRamp(1.4f);
            OfficeNpcAgent actor = CreateActor("Traveller before unsupported link", new Vector3(0f, 0f, -3f));
            yield return null;
            Assert.That(actor.TrySetStoryDestination(new Vector3(0f, 1f, 3f), 1.5f), Is.True);
            float deadline = Time.time + 10f;
            while (Time.time < deadline) yield return null;
            Assert.That(actor.StoryAtDestination, Is.False);
            Assert.That(actor.StoryMoveRequested, Is.True);
            Assert.That(actor.transform.position.z, Is.LessThan(-0.65f),
                "Owning a NavMesh link does not authorize walking over a wide missing floor.");
            Assert.That(actor.transform.position.y, Is.GreaterThan(-0.12f));
        }

        private void SplitPhysicalRamp(float gap)
        {
            GameObject ramp = objects.Find(x => x != null && x.name == "Supported single-file sloping bridge");
            BoxCollider collider = ramp.GetComponent<BoxCollider>();
            Vector3 size = collider.size;
            collider.enabled = false;
            float length = (size.z - gap) * 0.5f;
            foreach (int side in new[] { -1, 1 })
            {
                var half = new GameObject("Physical bridge beside floor seam");
                objects.Add(half);
                half.transform.SetPositionAndRotation(ramp.transform.position
                    + ramp.transform.forward * ((gap * 0.5f + length * 0.5f) * side), ramp.transform.rotation);
                half.AddComponent<BoxCollider>().size = new Vector3(size.x, size.y, length);
            }
            Physics.SyncTransforms();
        }

        private void BuildSlopedPassage(bool bakeRamp)
        {
            if (instance.valid) instance.Remove();
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(objects[0]);
            var sources = new List<NavMeshBuildSource>();
            foreach (Vector3 center in new[] { new Vector3(0f, -0.1f, -3f), new Vector3(0f, 0.9f, 3f) })
            {
                var landing = new GameObject("Pileup sloped-passage landing");
                objects.Add(landing);
                landing.transform.position = center;
                landing.AddComponent<BoxCollider>().size = new Vector3(4f, 0.2f, 4f);
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one),
                    size = new Vector3(4f, 0.2f, 4f), area = 0 });
            }
            var ramp = new GameObject("Supported single-file sloping bridge");
            objects.Add(ramp);
            float angle = Mathf.Atan2(1f, 3.2f) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(-angle, 0f, 0f);
            ramp.transform.SetPositionAndRotation(new Vector3(0f, 0.5f, 0f), rotation);
            Vector3 size = new Vector3(1.5f, 0.1f, Mathf.Sqrt(3.2f * 3.2f + 1f));
            ramp.AddComponent<BoxCollider>().size = size;
            if (bakeRamp) sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(ramp.transform.position, rotation, Vector3.one), size = size, area = 0 });
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Vector3.zero, new Vector3(14f, 8f, 14f)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            instance = NavMesh.AddNavMeshData(data);
            if (!bakeRamp)
            {
                var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
                Assert.That(NavMesh.SamplePosition(new Vector3(0f, 0f, -1.65f), out NavMeshHit lower, 0.6f, filter), Is.True);
                Assert.That(NavMesh.SamplePosition(new Vector3(0f, 1f, 1.65f), out NavMeshHit upper, 0.6f, filter), Is.True);
                stairLink = NavMesh.AddLink(new NavMeshLinkData { startPosition = lower.position,
                    endPosition = upper.position, width = 0.2f, bidirectional = true, agentTypeID = 0 });
                Assert.That(NavMesh.IsLinkValid(stairLink), Is.True);
            }
            Physics.SyncTransforms();
        }

        private static bool AllActive(OfficeNpcAgent[] actors)
        {
            foreach (OfficeNpcAgent actor in actors) if (actor.CurrentState != NpcState.Activity) return false;
            return true;
        }

        private static string Describe(OfficeNpcAgent[] actors)
        {
            var result = new List<string>();
            foreach (OfficeNpcAgent actor in actors)
            {
                var n = actor.Navigation;
                var settings = NavMesh.GetSettingsByID(n.agentTypeID);
                var filter = new NavMeshQueryFilter { agentTypeID = n.agentTypeID, areaMask = n.areaMask };
                Vector3 desired = n.isOnOffMeshLink ? n.currentOffMeshLinkData.endPos - actor.transform.position
                    : n.steeringTarget - actor.transform.position;
                bool world = SaksiTerakhir.Navigation.TerrainNavigationProbe.TryResolveDirection(actor.transform, desired,
                    0.65f, n.radius, n.height, settings.agentClimb, settings.agentSlope, filter, out Vector3 direction,
                    c => c.GetComponentInParent<OfficeNpcAgent>() != null);
                bool solid = SaksiTerakhir.Navigation.TerrainNavigationProbe.TryResolveDirection(actor.transform, desired,
                    0.65f, n.radius, n.height, settings.agentClimb, settings.agentSlope, filter, out _);
                bool terrainOnly = SaksiTerakhir.Navigation.TerrainNavigationProbe.TryResolveDirection(actor.transform, desired,
                    0.65f, n.radius, n.height, settings.agentClimb, settings.agentSlope, filter, out _,
                    c => c is CharacterController || c.GetComponentInParent<OfficeNpcAgent>() != null);
                bool physicalOnly = SaksiTerakhir.Navigation.TerrainNavigationProbe.TryResolveDirection(actor.transform, desired,
                    0.65f, n.radius, n.height, settings.agentClimb, settings.agentSlope, filter, out _,
                    c => c is CharacterController || c.GetComponentInParent<OfficeNpcAgent>() != null, false);
                bool ground = SaksiTerakhir.Navigation.TerrainNavigationProbe.TryGetGroundPosition(actor.transform,
                    actor.transform.position, settings.agentClimb, settings.agentSlope, out _);
                var nearby = new List<string>();
                foreach (RaycastHit hit in Physics.RaycastAll(actor.transform.position + Vector3.up,
                    Vector3.down, 2f))
                    if (!hit.collider.transform.IsChildOf(actor.transform))
                        nearby.Add("ray " + hit.collider.name + " y=" + hit.point.y + " normal=" + hit.normal);
                foreach (Collider c in Physics.OverlapCapsule(actor.transform.position + Vector3.up * (n.radius + 0.05f),
                    actor.transform.position + Vector3.up * (n.height - n.radius), n.radius + 0.035f))
                    if (!c.transform.IsChildOf(actor.transform)) nearby.Add(c.name + " bounds=" + c.bounds);
                result.Add(actor.name + " " + actor.transform.position + " " + actor.CurrentState
                    + " stopped=" + actor.Navigation.isStopped + " velocity=" + actor.Navigation.velocity
                    + " yield=" + actor.GetComponent<NpcPassageYield>().IsActive + " destination=" + actor.Navigation.destination
                    + " steer=" + actor.Navigation.steeringTarget + " remaining=" + actor.Navigation.remainingDistance
                    + " phase=" + typeof(NpcPassageYield).GetField("phase", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(actor.GetComponent<NpcPassageYield>()) + " link=" + n.isOnOffMeshLink
                    + " world=" + world + " solids=" + solid + " dir=" + direction
                    + " terrainOnly=" + terrainOnly + " nearby=" + string.Join(",", nearby)
                    + " physicalOnly=" + physicalOnly + " grounded=" + ground);
            }
            return string.Join("; ", result);
        }

        private static bool AllArrived(OfficeNpcAgent[] actors)
        {
            foreach (OfficeNpcAgent actor in actors) if (!actor.StoryAtDestination) return false;
            return true;
        }

        private OfficeNpcAgent CreateActor(string name, Vector3 requested)
        {
            Assert.That(NavMesh.SamplePosition(requested, out NavMeshHit hit, 0.5f,
                new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas }), Is.True);
            var homeObject = new GameObject(name + " home");
            objects.Add(homeObject);
            homeObject.transform.position = hit.position;
            NpcActivityPoint home = homeObject.AddComponent<NpcActivityPoint>();
            home.Configure(name, NpcActivityKind.Work, NpcZone.Interior, 0, new Vector2(100f, 100f));
            var root = new GameObject(name);
            objects.Add(root);
            root.SetActive(false);
            root.transform.position = hit.position;
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
            actor.Configure(name, NpcRole.Worker, home, new[] { home }, true);
            actor.HoldForStory();
            root.SetActive(true);
            return actor;
        }

        private Collider[] CreateDoorway() => new[]
        {
            CreateWall(new Vector3(0f, 1f, -3.25f), new Vector3(0.4f, 2f, 5.5f)),
            CreateWall(new Vector3(0f, 1f, 3.25f), new Vector3(0.4f, 2f, 5.5f))
        };

        private Collider CreateWall(Vector3 position, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(wall);
            wall.name = "Unbaked pileup test wall";
            wall.transform.position = position;
            wall.transform.localScale = scale;
            Physics.SyncTransforms();
            return wall.GetComponent<Collider>();
        }

        private StoryPlayerCinematicMover CreatePlayer(Vector3 position)
        {
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player");
            map.AddAction("Move", InputActionType.Value).expectedControlType = "Vector2";
            map.AddAction("Look", InputActionType.Value).expectedControlType = "Vector2";
            foreach (string name in new[] { "Sprint", "Crouch", "Jump", "Interact", "Journal", "Pause" })
                map.AddAction(name, InputActionType.Button);
            actions.AddActionMap(map);
            var root = new GameObject("Cinematic pileup test player");
            objects.Add(root);
            root.SetActive(false);
            root.transform.position = position;
            var body = root.AddComponent<CharacterController>();
            body.radius = 0.3f;
            body.height = 1.75f;
            body.center = Vector3.up * 0.875f;
            body.stepOffset = 0.32f;
            PlayerInputReader input = root.AddComponent<PlayerInputReader>();
            typeof(PlayerInputReader).GetField("inputActions", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(input, actions);
            PlayerController movement = root.AddComponent<PlayerController>();
            typeof(PlayerController).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, input);
            movement.enabled = false;
            StoryPlayerCinematicMover mover = root.AddComponent<StoryPlayerCinematicMover>();
            Assert.That(mover.Configure(movement, body, 0), Is.True);
            root.SetActive(true);
            input.enabled = false;
            Physics.SyncTransforms();
            return mover;
        }

        private static float Penetration(Collider first, Collider second)
            => Physics.ComputePenetration(first, first.transform.position, first.transform.rotation,
                second, second.transform.position, second.transform.rotation, out _, out float depth) ? depth : 0f;

        private static void AssertWallClear(Collider first, Collider wall)
            => Assert.That(Penetration(first, wall), Is.LessThan(0.06f),
                "Pileup recovery must preserve the physical wall boundary: " + first.name
                + " at " + first.transform.position + " against " + wall.transform.position + " at time " + Time.time);
    }
}
