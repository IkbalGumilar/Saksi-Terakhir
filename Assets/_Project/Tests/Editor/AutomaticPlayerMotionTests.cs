using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Interaction;
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
    public sealed class AutomaticPlayerMotionTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();
        private InputActionAsset actions;
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private NavMeshLinkInstance stairLink;
        private PlayerController movement;
        private StoryPlayerCinematicMover mover;
        private CharacterController body;
        private bool oldOptionsEnabled;
        private EnterPlayModeOptions oldOptions;
        private float oldTimeScale;
        private float oldCaptureDelta;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            oldOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            yield return new EnterPlayMode(false);
            oldTimeScale = Time.timeScale;
            oldCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            Time.timeScale = 1f;
            Box("Floor", new Vector3(0f, -0.1f, 0f), new Vector3(14f, 0.2f, 14f));
            var settings = NavMesh.GetSettingsByIndex(0);
            data = NavMeshBuilder.BuildNavMeshData(settings, new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(new Vector3(0f, -0.1f, 0f), Quaternion.identity, Vector3.one),
                    size = new Vector3(14f, 0.2f, 14f), area = 0 }
            }, new Bounds(Vector3.zero, new Vector3(20f, 10f, 20f)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            instance = NavMesh.AddNavMeshData(data);
            Assert.That(instance.valid, Is.True);
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player");
            map.AddAction("Move", InputActionType.Value).expectedControlType = "Vector2";
            map.AddAction("Look", InputActionType.Value).expectedControlType = "Vector2";
            foreach (string name in new[] { "Sprint", "Crouch", "Jump", "Interact", "Journal", "Pause" })
                map.AddAction(name, InputActionType.Button);
            actions.AddActionMap(map);
            GameObject player = Root("Automatic Motion Player");
            player.SetActive(false);
            player.transform.position = new Vector3(0f, 0.05f, -3f);
            body = player.AddComponent<CharacterController>();
            body.radius = 0.3f;
            body.height = 1.75f;
            body.center = Vector3.up * body.height * 0.5f;
            body.stepOffset = 0.32f;
            var input = player.AddComponent<PlayerInputReader>();
            Set(input, "inputActions", actions);
            movement = player.AddComponent<PlayerController>();
            Set(movement, "input", input);
            mover = player.AddComponent<StoryPlayerCinematicMover>();
            mover.Configure(movement, body, settings.agentTypeID);
            player.SetActive(true);
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject root in roots)
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            roots.Clear();
            if (NavMesh.IsLinkValid(stairLink)) NavMesh.RemoveLink(stairLink);
            if (instance.valid) instance.Remove();
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            if (actions != null) UnityEngine.Object.DestroyImmediate(actions);
            Time.timeScale = oldTimeScale;
            Time.captureDeltaTime = oldCaptureDelta;
            yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptionsEnabled = oldOptionsEnabled;
            EditorSettings.enterPlayModeOptions = oldOptions;
        }

        [UnityTest]
        public IEnumerator TurningColleagueAtDoorDoesNotPullPlayerInsideTwoMetres()
        {
            body.enabled = false;
            movement.transform.position = new Vector3(0f, 0.05f, -2.05f);
            body.enabled = true;
            GameObject colleague = Root("Colleague At Door");
            var capsule = colleague.AddComponent<CapsuleCollider>();
            capsule.radius = 0.34f;
            capsule.height = 1.75f;
            capsule.center = Vector3.up * 0.875f;
            Assert.That(mover.BeginFollow(colleague.transform, 2f, 2.5f), Is.True);
            yield return null;
            colleague.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Physics.SyncTransforms();
            float deadline = Time.time + 1.5f;
            while (Time.time < deadline) yield return null;
            float gap = Vector3.ProjectOnPlane(movement.transform.position - colleague.transform.position, Vector3.up).magnitude;
            Assert.That(gap, Is.GreaterThanOrEqualTo(1.95f), "Facing the player must not reverse the approach-side waiting point.");
            Assert.That(movement.transform.position.z, Is.LessThan(-1.9f));
            Assert.That(movement.StoryCinematicMovementLocked, Is.True);
        }

        [UnityTest]
        public IEnumerator AutomaticPlayerDetoursAroundColliderAddedAfterBake()
        {
            Box("New Physical Obstacle", new Vector3(0f, 0.8f, 0f), new Vector3(1.2f, 1.6f, 0.6f));
            Physics.SyncTransforms();
            bool arrived = false;
            Assert.That(mover.BeginMoveTo(new Vector3(0f, 0f, 3f), 2.2f, 0.25f, () => arrived = true), Is.True);
            float deadline = Time.time + 7f;
            while (Time.time < deadline && !arrived) yield return null;
            Assert.That(arrived, Is.True, "A local physical obstruction must produce a safe detour instead of repeating the same push.");
            Assert.That(movement.transform.position.z, Is.GreaterThan(2.6f));
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator MoveToOpensAClosedPhysicalDoorAndCrossesItsDoorway()
        {
            DoorInteractable door = CreateAutomaticDoorway(out Collider[] walls, out _);
            Set(door, "swingSpeed", 46f);
            Assert.That(door.IsOpen, Is.False);
            Assert.That(door.GetComponent<Collider>().enabled, Is.True);
            bool arrived = false;
            bool opened = false;
            bool observedOpeningSwing = false;
            float fullyOpenedAt = -1f;
            float automaticCloseDelay = -1f;
            float passageClearedAt = -1f;
            Vector3 openingWaitPosition = Vector3.zero;
            Assert.That(mover.BeginMoveTo(new Vector3(0f, 0f, 3f), 2.2f, 0.25f, () => arrived = true), Is.True);
            float deadline = Time.time + 8f;
            while (!arrived && Time.time < deadline)
            {
                opened |= door.IsOpen;
                if (fullyOpenedAt < 0f && door.IsOpen
                    && Quaternion.Angle(door.transform.localRotation, Quaternion.Euler(0f, 92f, 0f)) <= 0.1f)
                    fullyOpenedAt = Time.time;
                if (fullyOpenedAt >= 0f && door.PassageOccupied()) passageClearedAt = -1f;
                else if (fullyOpenedAt >= 0f && passageClearedAt < 0f
                    && movement.transform.position.z > 0.8f) passageClearedAt = Time.time;
                if (door.IsOpen && Quaternion.Angle(door.transform.localRotation, Quaternion.Euler(0f, 92f, 0f)) > 0.1f)
                {
                    if (!observedOpeningSwing) openingWaitPosition = movement.transform.position;
                    observedOpeningSwing = true;
                    Assert.That(movement.transform.position.z, Is.LessThan(-0.2f),
                        "The MC must wait outside while a slow door is still swinging open.");
                    Assert.That(Vector3.ProjectOnPlane(movement.transform.position - openingWaitPosition, Vector3.up).magnitude,
                        Is.LessThan(0.03f), "Waiting must cover the complete opening swing rather than a fixed short delay.");
                }
                foreach (Collider wall in walls) AssertPlayerClearance(wall);
                AssertPlayerClearance(door.GetComponent<Collider>());
                yield return null;
            }
            Assert.That(opened, Is.True, "Automatic MC movement must open an ordinary closed door ahead.");
            Assert.That(observedOpeningSwing, Is.True, "This fixture must exercise the slow native door swing.");
            Assert.That(arrived, Is.True, "The MC must actually pass the physical doorway and finish its movement.");
            while (door.IsOpen && Time.time < deadline + 2f) yield return null;
            automaticCloseDelay = Time.time - passageClearedAt;
            Assert.That(passageClearedAt, Is.GreaterThan(fullyOpenedAt));
            Assert.That(automaticCloseDelay, Is.InRange(0.95f, 1.15f),
                "The automatic MC gets a full second after clearing the physical doorway.");
            Assert.That(movement.transform.position.z, Is.GreaterThan(2.6f));
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator FollowOpensAClosedDoorBeforeReachingTheTargetOnItsOtherSide()
        {
            DoorInteractable door = CreateAutomaticDoorway(out Collider[] walls, out _);
            Transform colleague = Root("Colleague Beyond Closed Door").transform;
            colleague.position = new Vector3(0f, 0f, 3f);
            var colleagueBody = colleague.gameObject.AddComponent<CapsuleCollider>();
            colleagueBody.radius = 0.34f;
            colleagueBody.height = 1.75f;
            colleagueBody.center = Vector3.up * 0.875f;
            Physics.SyncTransforms();
            bool opened = false;
            Assert.That(mover.BeginFollow(colleague, 1.2f, 2.2f), Is.True);
            float deadline = Time.time + 8f;
            while (movement.transform.position.z < 1.5f && Time.time < deadline)
            {
                opened |= door.IsOpen;
                foreach (Collider wall in walls) AssertPlayerClearance(wall);
                AssertPlayerClearance(door.GetComponent<Collider>());
                AssertPlayerClearance(colleagueBody);
                yield return null;
            }
            Assert.That(opened, Is.True, "Following a colleague must open doors as well as direct MoveTo orders.");
            Assert.That(movement.transform.position.z, Is.GreaterThanOrEqualTo(1.5f));
            Assert.That(mover.IsFollowing, Is.True);
            Assert.That(movement.StoryCinematicMovementLocked, Is.True, "An ongoing escort retains its cinematic lock.");
            mover.StopCinematicMotion();
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator AlreadyOpeningSlowDoorWaitsForItsCompleteSwingBeforeAutomaticPassage()
        {
            DoorInteractable door = CreateAutomaticDoorway(out Collider[] walls, out _);
            Set(door, "swingSpeed", 46f);
            body.enabled = false;
            movement.transform.position = new Vector3(0f, 0.05f, -1.5f);
            body.enabled = true;
            Physics.SyncTransforms();
            Vector3 waitingPosition = movement.transform.position;
            door.ForceOpen();
            Assert.That(door.IsOpen, Is.True);
            Assert.That(door.GetComponent<Collider>().enabled, Is.False);
            bool arrived = false;
            bool observedOpeningSwing = false;
            Assert.That(mover.BeginMoveTo(new Vector3(0f, 0f, 3f), 2.2f, 0.25f, () => arrived = true), Is.True);
            float deadline = Time.time + 8f;
            while (!arrived && Time.time < deadline)
            {
                if (door.IsOpen && Quaternion.Angle(door.transform.localRotation, Quaternion.Euler(0f, 92f, 0f)) > 0.1f)
                {
                    observedOpeningSwing = true;
                    Assert.That(Vector3.ProjectOnPlane(movement.transform.position - waitingPosition, Vector3.up).magnitude,
                        Is.LessThan(0.03f), "A door already opened by another script still needs its full remaining swing time.");
                }
                foreach (Collider wall in walls) AssertPlayerClearance(wall);
                AssertPlayerClearance(door.GetComponent<Collider>());
                yield return null;
            }
            Assert.That(observedOpeningSwing, Is.True);
            Assert.That(arrived, Is.True, "Waiting for an existing opening must eventually resume and complete the MC route.");
            Assert.That(movement.transform.position.z, Is.GreaterThan(2.6f));
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator DeniedBossGateKeepsAutomaticPlayerOutsideItsRealPhysicalBarrier()
        {
            DoorInteractable door = CreateAutomaticDoorway(out Collider[] walls, out OfficeNpcDirector director);
            BossOfficeGate gate = ConfigureAutomaticBossGate(door, director, new ChapterOneProgress(), out Collider barrier);
            Assert.That(gate.CanOpenFor(movement.transform), Is.False);
            Assert.That(barrier.enabled, Is.True, "A denied gate must include the real independently configured player barrier.");
            bool arrived = false;
            bool blocked = false;
            gate.Blocked += actor => blocked |= actor == movement.transform;
            Assert.That(mover.BeginMoveTo(new Vector3(0f, 0f, 3f), 2.2f, 0.25f, () => arrived = true), Is.True);
            float deadline = Time.time + 6f;
            while (Time.time < deadline)
            {
                Assert.That(door.IsOpen, Is.False, "Automatic door handling must respect the boss room's story permission.");
                Assert.That(barrier.enabled, Is.True);
                Assert.That(movement.transform.position.z, Is.LessThan(-0.2f), "An automatic recovery must remain on the corridor side.");
                foreach (Collider wall in walls) AssertPlayerClearance(wall);
                AssertPlayerClearance(door.GetComponent<Collider>());
                AssertPlayerClearance(barrier);
                yield return null;
            }
            Assert.That(blocked, Is.True, "The real gate must receive and reject the automatic player's opening request.");
            Assert.That(arrived, Is.False);
            Assert.That(gate.PlayerInside, Is.False);
            Assert.That(mover.IsActive, Is.True);
            Assert.That(movement.StoryCinematicMovementLocked, Is.True);
        }

        [UnityTest]
        public IEnumerator PermittedBossGateOpensAndCompletesAutomaticPlayerMovement()
        {
            DoorInteractable door = CreateAutomaticDoorway(out Collider[] walls, out OfficeNpcDirector director);
            var progress = new ChapterOneProgress();
            Assert.That(progress.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.True);
            Assert.That(progress.TryApply(ChapterOneEvent.BossCallFinished), Is.True);
            BossOfficeGate gate = ConfigureAutomaticBossGate(door, director, progress, out Collider barrier);
            Assert.That(gate.CanOpenFor(movement.transform), Is.True);
            Assert.That(barrier.enabled, Is.False);
            bool arrived = false;
            bool opened = false;
            Assert.That(mover.BeginMoveTo(new Vector3(0f, 0f, 3f), 2.2f, 0.25f, () => arrived = true), Is.True);
            float deadline = Time.time + 8f;
            while (!arrived && Time.time < deadline)
            {
                opened |= door.IsOpen;
                Assert.That(barrier.enabled, Is.False);
                foreach (Collider wall in walls) AssertPlayerClearance(wall);
                AssertPlayerClearance(door.GetComponent<Collider>());
                yield return null;
            }
            Assert.That(opened, Is.True, "A permitted boss-room gate should open for the cinematic MC.");
            Assert.That(arrived, Is.True);
            Assert.That(movement.transform.position.z, Is.GreaterThan(2.6f));
            Assert.That(gate.PlayerInside, Is.True);
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
        }

        [Test]
        public void TargetAboveUnrelatedFloorIsRejectedWithoutFalseArrival()
        {
            Assert.That(mover.BeginMoveTo(new Vector3(2f, 1.0f, 0f)), Is.False,
                "Sampling must not silently accept a different floor under the requested destination.");
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator ObstructedDoorwayWaitChoosesAReachableTwoMetreAlternative()
        {
            body.enabled = false;
            movement.transform.position = new Vector3(0f, 0.05f, -1f);
            body.enabled = true;
            GameObject colleague = Root("Colleague With Obstructed Wait Point");
            Box("Obstacle At Requested Wait Point", new Vector3(0f, 0.9f, -2f), new Vector3(0.8f, 1.8f, 0.8f));
            Physics.SyncTransforms();
            Assert.That(mover.BeginFollow(colleague.transform, 2f, 2.2f), Is.True);
            Assert.That(mover.HoldForDialogue(colleague.transform, 2f), Is.True,
                "The approach side has clear two-metre alternatives beside the small obstacle.");
            float deadline = Time.time + 6f;
            while (Time.time < deadline && !mover.AtDialogueWaitPosition) yield return null;
            Assert.That(mover.AtDialogueWaitPosition, Is.True,
                "The dialogue must not wait forever for a point inside a physical obstacle.");
            float gap = Vector3.ProjectOnPlane(movement.transform.position - colleague.transform.position, Vector3.up).magnitude;
            Assert.That(gap, Is.EqualTo(2f).Within(0.16f));
            Assert.That(movement.transform.position.z, Is.LessThan(0f), "Keep the player on the original approach side.");
        }

        [Test]
        public void UnreachableDoorwayWaitCannotLeaveDialogueWaitingForAnImpossiblePoint()
        {
            body.enabled = false;
            movement.transform.position = new Vector3(0f, 0.05f, -0.7f);
            body.enabled = true;
            GameObject colleague = Root("Colleague In Cramped Doorway");
            Box("Cramped Room Front", new Vector3(0f, 1f, 1.2f), new Vector3(3f, 2f, 0.2f));
            Box("Cramped Room Back", new Vector3(0f, 1f, -1.2f), new Vector3(3f, 2f, 0.2f));
            Box("Cramped Room Left", new Vector3(-1.2f, 1f, 0f), new Vector3(0.2f, 2f, 3f));
            Box("Cramped Room Right", new Vector3(1.2f, 1f, 0f), new Vector3(0.2f, 2f, 3f));
            Physics.SyncTransforms();
            Assert.That(mover.BeginFollow(colleague.transform, 2f, 2.2f), Is.True);
            Assert.That(mover.HoldForDialogue(colleague.transform, 2f), Is.False,
                "There is no supported unobstructed two-metre position in this cramped room.");
            Assert.That(!mover.IsWaitingForDialogue || mover.AtDialogueWaitPosition, Is.True,
                "Either clear the failed wait or hold the current position, so the final lines can continue.");
        }

        [Test]
        public void ResumedEscortPreservesAnAlreadySupportedClearAndReachableSavedPose()
        {
            GameObject colleague = Root("Saved Escort Colleague");
            colleague.SetActive(false);
            OfficeNpcAgent npc = colleague.AddComponent<OfficeNpcAgent>();
            GameObject chapterRoot = Root("Saved Escort Chapter");
            chapterRoot.SetActive(false);
            ChapterOneDirector chapter = chapterRoot.AddComponent<ChapterOneDirector>();
            Set(chapter, "player", movement.transform);
            Set(chapter, "playerMover", mover);
            body.enabled = false;
            Vector3 savedPosition = new Vector3(1f, 0.05f, -2.4f);
            Quaternion savedRotation = Quaternion.Euler(0f, 71f, 0f);
            movement.transform.SetPositionAndRotation(savedPosition, savedRotation);
            body.enabled = true;
            Physics.SyncTransforms();
            bool resumed = (bool)typeof(ChapterOneDirector).GetMethod("PlacePlayerNearLastColleagueOnResume",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(chapter, new object[] { npc });
            Assert.That(resumed, Is.True, "The saved pose is supported, clear and connected to the colleague.");
            Assert.That(movement.transform.position, Is.EqualTo(savedPosition),
                "An already valid save must not be replaced by the helper's first 1.5m offset.");
            Assert.That(movement.transform.rotation, Is.EqualTo(savedRotation));
            Assert.That(body.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator ProjectedHorizontalDestinationCompletesAndReleasesTheCinematicLock()
        {
            var filter = new NavMeshQueryFilter { agentTypeID = mover.NavMeshAgentTypeId, areaMask = NavMesh.AllAreas };
            Vector3 requested = new Vector3(7.2f, 0f, 0f);
            Assert.That(NavMesh.SamplePosition(requested, out NavMeshHit projected, 1.2f, filter), Is.True);
            Assert.That(Vector3.ProjectOnPlane(projected.position - requested, Vector3.up).magnitude,
                Is.GreaterThan(0.4f), "The fixture must project farther than the arrival tolerance.");
            bool arrived = false;
            Assert.That(mover.BeginMoveTo(requested, 2.2f, 0.2f, () => arrived = true), Is.True);
            float deadline = Time.time + 7f;
            while (Time.time < deadline && !arrived) yield return null;
            Assert.That(arrived, Is.True,
                "A complete path to the accepted projected endpoint must invoke arrival once.");
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
            Assert.That(Vector3.ProjectOnPlane(movement.transform.position - projected.position, Vector3.up).magnitude,
                Is.LessThan(0.35f));
        }

        [UnityTest]
        public IEnumerator SupportedRampBetweenLinkedNavigationIslandsRemainsTraversable()
        {
            if (instance.valid) instance.Remove();
            UnityEngine.Object.DestroyImmediate(data);
            foreach (GameObject item in roots)
                if (item != null && item.name == "Floor") UnityEngine.Object.DestroyImmediate(item);
            Box("Lower Landing", new Vector3(0f, -0.1f, -3f), new Vector3(4f, 0.2f, 4f));
            Box("Upper Landing", new Vector3(0f, 0.9f, 3f), new Vector3(4f, 0.2f, 4f));
            GameObject ramp = Root("Physical Stair Ramp");
            float angle = Mathf.Atan2(1f, 3.2f) * Mathf.Rad2Deg;
            ramp.transform.SetPositionAndRotation(new Vector3(0f, 0.5f, 0f), Quaternion.Euler(-angle, 0f, 0f));
            ramp.AddComponent<BoxCollider>().size = new Vector3(2f, 0.1f, Mathf.Sqrt(3.2f * 3.2f + 1f));
            var settings = NavMesh.GetSettingsByIndex(0);
            var sources = new List<NavMeshBuildSource>();
            foreach (Vector3 center in new[] { new Vector3(0f, -0.1f, -3f), new Vector3(0f, 0.9f, 3f) })
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one),
                    size = new Vector3(4f, 0.2f, 4f), area = 0 });
            data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(Vector3.zero, new Vector3(14f, 8f, 14f)), Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
            var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };
            Assert.That(NavMesh.SamplePosition(new Vector3(0f, 0f, -1.65f), out NavMeshHit lower, 0.6f, filter), Is.True);
            Assert.That(NavMesh.SamplePosition(new Vector3(0f, 1f, 1.65f), out NavMeshHit upper, 0.6f, filter), Is.True);
            stairLink = NavMesh.AddLink(new NavMeshLinkData { startPosition = lower.position,
                endPosition = upper.position, width = 1f, bidirectional = true, agentTypeID = settings.agentTypeID });
            Assert.That(NavMesh.IsLinkValid(stairLink), Is.True);
            body.enabled = false;
            movement.transform.position = new Vector3(0f, 0.05f, -3f);
            body.enabled = true;
            Physics.SyncTransforms();
            yield return null;
            bool arrived = false;
            Assert.That(mover.BeginMoveTo(new Vector3(0f, 1f, 3f), 2f, 0.25f, () => arrived = true), Is.True);
            float deadline = Time.time + 9f;
            while (Time.time < deadline && !arrived) yield return null;
            Assert.That(arrived, Is.True, "Terrain probing must preserve supported stairs that bridge authored navigation links.");
            Assert.That(movement.transform.position.y, Is.GreaterThan(0.9f));
        }

        [UnityTest]
        public IEnumerator SeatingRetriesAfterControllerRecoveryAndWalksBeforeUsingOffMeshSeat()
        {
            GameObject chapterRoot = Root("Seating Retry Director");
            chapterRoot.SetActive(false);
            var director = chapterRoot.AddComponent<ChapterOneDirector>();
            director.StartupManagedExternally = true;
            var progress = new ChapterOneProgress();
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            progress.TryApply(ChapterOneEvent.BossCallFinished);
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);
            progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003");
            progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-003");
            progress.TryApply(ChapterOneEvent.BossMovedToMeeting, "left");
            progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004");
            progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, "NPC-004");
            progress.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004");
            progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-004");
            progress.TryApply(ChapterOneEvent.MeetingAssembled);
            Assert.That(progress.Stage, Is.EqualTo(ChapterOneStage.FinalBossBriefing));
            Transform approach = Root("Walkable Meeting Approach").transform;
            approach.position = new Vector3(0f, 0f, 1f);
            Transform seat = Root("Sofa Outside Navigation").transform;
            seat.position = new Vector3(10f, 0f, 1f);
            Box("Physical Floor Under Off Mesh Seat", new Vector3(10f, -0.1f, 1f), new Vector3(2f, 0.2f, 2f));
            var seating = movement.gameObject.AddComponent<StoryPlayerSeating>();
            Assert.That(seating.Configure(movement, body, null), Is.True);
            Set(director, "progress", progress);
            Set(director, "player", movement.transform);
            Set(director, "playerMover", mover);
            Set(director, "playerSeating", seating);
            Set(director, "playerSeat", seat);
            Set(director, "meetingApproach", approach);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            body.enabled = false;
            LogAssert.Expect(LogType.Warning, "Player seating path is unavailable; retrying without skipping the walk.");
            typeof(ChapterOneDirector).GetMethod("SeatPlayerForMeeting", flags).Invoke(director, null);
            Assert.That(progress.PlayerSeated, Is.False);
            Assert.That(movement.transform.position.z, Is.LessThan(-2.9f));
            body.enabled = true;
            typeof(ChapterOneDirector).GetMethod("TryStartPlayerSeatPath", flags).Invoke(director, null);
            Assert.That(mover.IsActive, Is.True);
            float deadline = Time.time + 6f;
            while (Time.time < deadline && !progress.PlayerSeated) yield return null;
            Assert.That(progress.PlayerSeated, Is.True, "Recovery must finish the approach walk before placing the player in the off-mesh seat.");
            Assert.That(Vector3.ProjectOnPlane(movement.transform.position - seat.position, Vector3.up).magnitude, Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(movement.transform.position.y - seat.position.y), Is.LessThan(0.1f));
            Assert.That(seating.IsSeated, Is.True);
        }

        [UnityTest]
        public IEnumerator SeatingFromDisconnectedLeftAisleUsesTheReachableEndOfTheSameSofa()
        {
            if (instance.valid) instance.Remove();
            UnityEngine.Object.DestroyImmediate(data);
            var settings = NavMesh.GetSettingsByIndex(0);
            var sources = new List<NavMeshBuildSource>();
            foreach (float x in new[] { -2.5f, 2.5f })
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(new Vector3(x, -0.1f, 0f), Quaternion.identity, Vector3.one),
                    size = new Vector3(2f, 0.2f, 10f), area = 0 });
            data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(Vector3.zero, new Vector3(14f, 10f, 14f)), Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
            body.enabled = false;
            movement.transform.position = new Vector3(-2.5f, 0.05f, -3f);
            body.enabled = true;
            Transform approach = Root("Right Aisle Approach").transform;
            approach.position = new Vector3(2.5f, 0f, 0f);
            Transform seat = Root("Central Sofa Seat").transform;
            seat.SetPositionAndRotation(new Vector3(0f, 0f, 1f), Quaternion.Euler(0f, 180f, 0f));
            ChapterOneDirector director = CreateMeetingDirector(approach, seat);
            var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };
            var rightRoute = new NavMeshPath();
            NavMesh.CalculatePath(movement.transform.position, approach.position, filter, rightRoute);
            Assert.That(rightRoute.status, Is.EqualTo(NavMeshPathStatus.PathPartial),
                "The two sofa aisles must be disconnected to reproduce the authored-room warning.");
            Physics.SyncTransforms();
            typeof(ChapterOneDirector).GetMethod("SeatPlayerForMeeting", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, null);
            Assert.That(mover.IsActive, Is.True,
                "A player in the left aisle must approach that end of the sofa instead of waiting for the disconnected right aisle.");
            Assert.That(director.Progress.PlayerSeated, Is.False, "The walk cannot be skipped.");
            yield return null;
            Assert.That(movement.transform.position.x, Is.LessThan(-2f));
            Assert.That(director.Progress.PlayerSeated, Is.False);
            float deadline = Time.time + 6f;
            while (Time.time < deadline && !director.Progress.PlayerSeated) yield return null;
            Assert.That(director.Progress.PlayerSeated, Is.True);
            Assert.That(Vector3.ProjectOnPlane(movement.transform.position - seat.position, Vector3.up).magnitude, Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator SeatingPrefersTheAuthoredApproachWhenBothEndsAreReachable()
        {
            Transform approach = Root("Preferred Sofa Approach").transform;
            approach.position = new Vector3(2f, 0f, 0f);
            Transform seat = Root("Shared Sofa Seat").transform;
            seat.position = new Vector3(0f, 0f, 1f);
            ChapterOneDirector director = CreateMeetingDirector(approach, seat);
            typeof(ChapterOneDirector).GetMethod("SeatPlayerForMeeting", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, null);
            Assert.That(mover.IsActive, Is.True);
            Vector3 target = (Vector3)typeof(StoryPlayerCinematicMover).GetField("pathDestination", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mover);
            Assert.That(target.x, Is.EqualTo(approach.position.x).Within(0.1f));
            Assert.That(director.Progress.PlayerSeated, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnreachableSofaEndsKeepThePlayerWaitingWithoutSkippingTheWalk()
        {
            Transform approach = Root("Unreachable Sofa Approach").transform;
            approach.position = new Vector3(2f, 3f, 0f);
            Transform seat = Root("Unreachable Sofa Seat").transform;
            seat.position = new Vector3(0f, 3f, 1f);
            ChapterOneDirector director = CreateMeetingDirector(approach, seat);
            Vector3 before = movement.transform.position;
            LogAssert.Expect(LogType.Warning, "Player seating path is unavailable; retrying without skipping the walk.");
            typeof(ChapterOneDirector).GetMethod("SeatPlayerForMeeting", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, null);
            Assert.That(mover.IsActive, Is.False);
            Assert.That(director.Progress.PlayerSeated, Is.False);
            Assert.That(movement.StoryCinematicMovementLocked, Is.True);
            yield return null;
            Assert.That(Vector3.ProjectOnPlane(movement.transform.position - before, Vector3.up).magnitude, Is.LessThan(0.001f));
            Assert.That(director.Progress.PlayerSeated, Is.False);
        }

        private ChapterOneDirector CreateMeetingDirector(Transform approach, Transform seat)
        {
            GameObject chapterRoot = Root("Reachable Sofa Director");
            chapterRoot.SetActive(false);
            var director = chapterRoot.AddComponent<ChapterOneDirector>();
            director.StartupManagedExternally = true;
            var progress = new ChapterOneProgress();
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            progress.TryApply(ChapterOneEvent.BossCallFinished);
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);
            progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003");
            progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-003");
            progress.TryApply(ChapterOneEvent.BossMovedToMeeting, "left");
            progress.TryApply(ChapterOneEvent.ColleagueDialogueStarted, "NPC-004");
            progress.TryApply(ChapterOneEvent.LastColleagueIntroFinished, "NPC-004");
            progress.TryApply(ChapterOneEvent.WalkDialogueFinished, "NPC-004");
            progress.TryApply(ChapterOneEvent.ColleagueArrived, "NPC-004");
            progress.TryApply(ChapterOneEvent.MeetingAssembled);
            var seating = movement.GetComponent<StoryPlayerSeating>();
            if (seating == null) seating = movement.gameObject.AddComponent<StoryPlayerSeating>();
            seating.Configure(movement, body, null);
            Set(director, "progress", progress);
            Set(director, "player", movement.transform);
            Set(director, "playerMover", mover);
            Set(director, "playerSeating", seating);
            Set(director, "playerSeat", seat);
            Set(director, "meetingApproach", approach);
            return director;
        }

        private GameObject Root(string name)
        {
            var root = new GameObject(name);
            roots.Add(root);
            return root;
        }

        private DoorInteractable CreateAutomaticDoorway(out Collider[] walls, out OfficeNpcDirector director)
        {
            walls = new Collider[2];
            for (int i = 0; i < walls.Length; i++)
            {
                GameObject wall = Root("Automatic Doorway Wall " + i);
                wall.transform.position = new Vector3(i == 0 ? -3.8f : 3.8f, 1f, 0f);
                walls[i] = wall.AddComponent<BoxCollider>();
                ((BoxCollider)walls[i]).size = new Vector3(6.4f, 2f, 0.2f);
            }
            GameObject doorRoot = Root("Closed Automatic Motion Door");
            doorRoot.transform.position = Vector3.up;
            doorRoot.AddComponent<BoxCollider>().size = new Vector3(1.2f, 2f, 0.2f);
            DoorInteractable door = doorRoot.AddComponent<DoorInteractable>();
            door.RestoreSavedState(false);
            body.skinWidth = 0.02f;
            Physics.SyncTransforms();
            // Awake must discover the already authored door bounds and a valid building.
            GameObject directorRoot = Root("Automatic Doorway NPC Director");
            directorRoot.SetActive(false);
            director = directorRoot.AddComponent<OfficeNpcDirector>();
            director.SetBuildingRoot(directorRoot.transform);
            directorRoot.SetActive(true);
            return door;
        }

        private BossOfficeGate ConfigureAutomaticBossGate(DoorInteractable door, OfficeNpcDirector director,
            ChapterOneProgress progress, out Collider barrier)
        {
            GameObject room = Root("Automatic Boss Room Zone");
            room.transform.position = new Vector3(0f, 1f, 2f);
            var zone = room.AddComponent<BoxCollider>();
            zone.size = new Vector3(12f, 2f, 3.6f);
            zone.isTrigger = true;
            GameObject barrierRoot = Root("Automatic Boss Player Barrier");
            barrierRoot.transform.position = Vector3.up;
            var physicalBarrier = barrierRoot.AddComponent<BoxCollider>();
            physicalBarrier.size = new Vector3(1.2f, 2f, 0.26f);
            barrier = physicalBarrier;
            Transform exit = Root("Automatic Boss Corridor Exit").transform;
            exit.position = new Vector3(0f, 0f, -2f);
            BossOfficeGate gate = door.gameObject.AddComponent<BossOfficeGate>();
            gate.ConfigureScene(zone, barrier, exit, movement.transform, director, null, null, null);
            gate.Configure(progress, door);
            Physics.SyncTransforms();
            return gate;
        }

        private void AssertPlayerClearance(Collider blocker)
        {
            if (!blocker.enabled || blocker.isTrigger) return;
            if (Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                blocker, blocker.transform.position, blocker.transform.rotation, out _, out float depth))
                Assert.That(depth, Is.LessThan(0.06f), "Automatic doorway movement must preserve physical wall and actor clearance.");
        }

        private void Box(string name, Vector3 position, Vector3 size)
        {
            GameObject root = Root(name);
            root.transform.position = position;
            root.AddComponent<BoxCollider>().size = size;
        }

        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
