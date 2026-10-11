using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace SaksiTerakhir.Tests
{
    public sealed class NpcConversationSpacingTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private NpcProfile profile;
        private InputActionAsset actions;
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
            GameObject floor = Root("Conversation test floor");
            floor.transform.position = Vector3.down * 0.1f;
            floor.AddComponent<BoxCollider>().size = new Vector3(12f, 0.2f, 12f);
            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one),
                    size = new Vector3(12f, 0.2f, 12f), area = 0
                }
            };
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Vector3.zero, new Vector3(14f, 4f, 14f)), Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject root in objects) if (root != null) Object.DestroyImmediate(root);
            objects.Clear();
            if (instance.valid) instance.Remove();
            if (data != null) Object.DestroyImmediate(data);
            if (profile != null) Object.DestroyImmediate(profile);
            if (actions != null) Object.DestroyImmediate(actions);
            Time.timeScale = oldScale;
            Time.captureDeltaTime = oldCapture;
            yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptionsEnabled = oldOptionsEnabled;
            EditorSettings.enterPlayModeOptions = oldOptions;
        }

        [UnityTest]
        public IEnumerator CloseInteractionWalksBackBeforeTheFirstLineAndKeepsControlLocked()
        {
            NpcInteractable npc = CreateNpc();
            StoryPlayerCinematicMover mover = CreatePlayer(Vector3.forward * 0.8f);
            PlayerController movement = mover.GetComponent<PlayerController>();
            yield return null;

            npc.Interact(mover.transform);

            Assert.That(npc.DialogueActive, Is.False, "The first line must wait until the safe speaking distance is reached.");
            Assert.That(movement.StoryMovementLocked, Is.True);
            float deadline = Time.time + 6f;
            while (!npc.DialogueActive && Time.time < deadline)
            {
                Assert.That(movement.StoryMovementLocked, Is.True, "Retreat must not hand manual control back early.");
                yield return null;
            }
            Assert.That(npc.DialogueActive, Is.True);
            Assert.That(Vector3.ProjectOnPlane(mover.transform.position - npc.transform.position, Vector3.up).magnitude,
                Is.InRange(1.85f, 2.15f));
            Assert.That(movement.StoryMovementLocked, Is.True, "Control remains locked throughout the conversation.");
            npc.CloseDialogue();
            Assert.That(movement.StoryMovementLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator DisablingTheReservedNpcCancelsRetreatAndReleasesControl()
        {
            NpcInteractable npc = CreateNpc();
            StoryPlayerCinematicMover mover = CreatePlayer(Vector3.forward * 0.8f);
            PlayerController movement = mover.GetComponent<PlayerController>();
            yield return null;
            npc.Interact(mover.transform);
            Assert.That(mover.IsActive, Is.True);

            npc.gameObject.SetActive(false);
            yield return null;

            Assert.That(mover.IsActive, Is.False);
            Assert.That(movement.StoryMovementLocked, Is.False);
            Assert.That(npc.DialogueActive, Is.False);
        }

        [UnityTest]
        public IEnumerator InitialCapsuleOverlapCanRetreatOutwardsWithoutStartingDialogueInsideTheNpc()
        {
            NpcInteractable npc = CreateNpc();
            StoryPlayerCinematicMover mover = CreatePlayer(Vector3.forward * 0.45f);
            yield return null;
            npc.Interact(mover.transform);
            Assert.That(npc.DialogueActive, Is.False);
            float deadline = Time.time + 6f;
            while (!npc.DialogueActive && Time.time < deadline) yield return null;
            Assert.That(npc.DialogueActive, Is.True, "Existing actor overlap must not deadlock the safe outward retreat.");
            Assert.That(Vector3.ProjectOnPlane(mover.transform.position - npc.transform.position, Vector3.up).magnitude,
                Is.InRange(1.85f, 2.15f));
        }

        [UnityTest]
        public IEnumerator LosingInteractorAimDuringRetreatDoesNotCancelConversationPreparation()
        {
            NpcInteractable npc = CreateNpc();
            StoryPlayerCinematicMover mover = CreatePlayer(Vector3.forward * 0.8f);
            mover.gameObject.SetActive(false);
            GameObject view = Root("Conversation aim camera");
            view.transform.SetParent(mover.transform, false);
            view.transform.localPosition = Vector3.up * 0.875f;
            view.AddComponent<Camera>();
            PlayerInteractor interactor = mover.gameObject.AddComponent<PlayerInteractor>();
            Set(interactor, "input", mover.GetComponent<PlayerInputReader>());
            Set(interactor, "viewpoint", view.transform);
            mover.gameObject.SetActive(true);
            yield return null;
            typeof(PlayerInteractor).GetMethod("SetCurrent", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(interactor, new object[] { npc });
            npc.Interact(mover.transform);
            typeof(PlayerInteractor).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(interactor, null);
            Assert.That(mover.IsActive, Is.True, "Clearing an aim target must not cancel its owned retreat.");
            Assert.That(npc.ConversationPreparing, Is.True);
            float deadline = Time.time + 6f;
            while (!npc.DialogueActive && Time.time < deadline) yield return null;
            Assert.That(npc.DialogueActive, Is.True);
            Assert.That(Vector3.ProjectOnPlane(mover.transform.position - npc.transform.position, Vector3.up).magnitude,
                Is.InRange(1.85f, 2.15f));
        }

        [UnityTest]
        public IEnumerator ActivePromptCannotCancelRetreatWhenTheInteractorClearsItsTarget()
        {
            NpcInteractable npc = CreateNpc();
            npc.gameObject.layer = 6;
            StoryPlayerCinematicMover mover = CreatePlayer(Vector3.forward * 0.8f);
            mover.gameObject.SetActive(false);
            mover.transform.rotation = Quaternion.LookRotation(Vector3.back);
            GameObject eye = Root("Prompt regression aim camera");
            eye.transform.SetParent(mover.transform, false);
            eye.transform.localPosition = Vector3.up * 1.35f;
            Camera camera = eye.AddComponent<Camera>();
            PlayerInteractor interactor = mover.gameObject.AddComponent<PlayerInteractor>();
            Set(interactor, "input", mover.GetComponent<PlayerInputReader>());
            Set(interactor, "viewpoint", eye.transform);
            Set(interactor, "aimCamera", camera);
            mover.gameObject.SetActive(true);

            GameObject promptHolder = Root("Active conversation prompt view");
            promptHolder.SetActive(false);
            GameObject actionRoot = Root("Conversation action prompt");
            actionRoot.transform.SetParent(promptHolder.transform, false);
            var actionLabel = new GameObject("Conversation action label", typeof(RectTransform),
                typeof(TextMeshProUGUI));
            objects.Add(actionLabel);
            actionLabel.transform.SetParent(actionRoot.transform, false);
            InteractionPromptView prompt = promptHolder.AddComponent<InteractionPromptView>();
            Set(prompt, "interactor", interactor);
            Set(prompt, "promptRoot", actionRoot);
            Set(prompt, "actionLabel", actionLabel.GetComponent<TMP_Text>());
            promptHolder.SetActive(true);
            Physics.SyncTransforms();
            yield return null;

            Assert.That(interactor.Current, Is.SameAs(npc), "The live probe must offer the NPC before pressing E.");
            Assert.That(actionRoot.activeSelf, Is.True);
            typeof(PlayerInteractor).GetMethod("OnInteractPressed", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(interactor, null);
            Assert.That(npc.ConversationPreparing, Is.True);
            yield return null;

            Assert.That(interactor.Current, Is.Null, "Cinematic retreat deliberately clears the aim target.");
            Assert.That(actionRoot.activeSelf, Is.False);
            Assert.That(mover.IsActive, Is.True,
                "The active prompt must not cancel the retreat when the interactor clears its target.");
            Assert.That(npc.ConversationPreparing, Is.True);
            float deadline = Time.time + 6f;
            while (!npc.DialogueActive && Time.time < deadline) yield return null;
            Assert.That(npc.DialogueActive, Is.True);
            Assert.That(Vector3.ProjectOnPlane(mover.transform.position - npc.transform.position, Vector3.up).magnitude,
                Is.InRange(1.85f, 2.15f));
            npc.CloseDialogue();
        }

        [UnityTest]
        public IEnumerator ActivePlayerCameraKeepsLookingAtTheNpcDuringBackwardConversationRetreat()
        {
            NpcInteractable npc = CreateNpc();
            StoryPlayerCinematicMover mover = CreatePlayer(Vector3.forward * 0.8f);
            mover.gameObject.SetActive(false);
            mover.transform.rotation = Quaternion.LookRotation(Vector3.back);
            GameObject eye = Root("Backward retreat camera pivot");
            eye.transform.SetParent(mover.transform, false);
            eye.transform.localPosition = Vector3.up * 1.6f;
            eye.AddComponent<Camera>();
            PlayerCameraController camera = mover.gameObject.AddComponent<PlayerCameraController>();
            Set(camera, "input", mover.GetComponent<PlayerInputReader>());
            Set(camera, "body", mover.GetComponent<CharacterController>());
            Set(camera, "yawTransform", mover.transform);
            Set(camera, "cameraTransform", eye.transform);
            mover.gameObject.SetActive(true);
            yield return null;
            Vector3 start = mover.transform.position;

            npc.Interact(mover.transform);
            Assert.That(npc.ConversationPreparing, Is.True);
            Assert.That(camera.StoryFocusTarget, Is.SameAs(npc.transform));
            float deadline = Time.time + 6f;
            int observedRetreatFrames = 0;
            while (!npc.DialogueActive && Time.time < deadline)
            {
                yield return null;
                Vector3 directionToNpc = Vector3.ProjectOnPlane(npc.transform.position - mover.transform.position,
                    Vector3.up).normalized;
                Vector3 horizontalView = Vector3.ProjectOnPlane(eye.transform.forward, Vector3.up).normalized;
                Assert.That(Vector3.Dot(horizontalView, directionToNpc), Is.GreaterThan(0.95f),
                    "Walking backwards must not turn the shared player/camera yaw away from the NPC.");
                Assert.That(camera.StoryFocusTarget, Is.SameAs(npc.transform));
                observedRetreatFrames++;
            }
            Assert.That(observedRetreatFrames, Is.GreaterThan(2), "Observe the actual retreat across several frames.");
            Assert.That(npc.DialogueActive, Is.True);
            Assert.That(Vector3.Distance(mover.transform.position, start), Is.GreaterThan(0.8f));
            Assert.That(Vector3.ProjectOnPlane(mover.transform.position - npc.transform.position, Vector3.up).magnitude,
                Is.InRange(1.85f, 2.15f));
            npc.CloseDialogue();
            Assert.That(camera.StoryLookLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator TalkingToAStoryMoverPausesItUntilDialogueEndsThenResumesItsStoryOrder()
        {
            NpcInteractable npc = CreateNpc();
            OfficeNpcAgent actor = npc.GetComponent<OfficeNpcAgent>();
            Transform player = Root("Player talking to story mover").transform;
            player.position = Vector3.forward * 2f;
            yield return null;
            Assert.That(actor.TrySetStoryDestination(Vector3.right * 4f, 2f), Is.True);
            actor.Navigation.velocity = Vector3.right;
            actor.BeginPlayerInteraction(player);
            Vector3 start = actor.transform.position;
            Assert.That(actor.Navigation.velocity.sqrMagnitude, Is.LessThan(0.001f));
            Assert.That(actor.MovementIntent, Is.EqualTo(Vector3.zero));
            float deadline = Time.time + 1f;
            while (Time.time < deadline)
            {
                yield return null;
                Assert.That(actor.CurrentState, Is.EqualTo(NpcState.PlayerConversation),
                    "Story order updates must not override an active player conversation.");
                Assert.That(Vector3.Distance(actor.transform.position, start), Is.LessThan(0.03f));
                Assert.That(actor.Navigation.isStopped, Is.True);
            }
            actor.EndPlayerInteraction();
            Assert.That(actor.StoryMoveRequested, Is.True);
            Assert.That(actor.CurrentState, Is.EqualTo(NpcState.StoryMoving));
            Assert.That(actor.Navigation.isStopped, Is.False,
                "StoryMoving must resume as well as ordinary patrol Moving.");
            deadline = Time.time + 6f;
            while (!actor.StoryAtDestination && Time.time < deadline) yield return null;
            Assert.That(actor.StoryAtDestination, Is.True);
        }

        [UnityTest]
        public IEnumerator PlayerInterruptsBothAmbientPartnersAndTheyRetainTheirReservedActivities()
        {
            NpcInteractable npc = CreateNpc();
            OfficeNpcAgent first = npc.GetComponent<OfficeNpcAgent>();
            first.ReleaseStoryHold();
            OfficeNpcAgent second = CreateAmbientNpc(new Vector3(0f, 0f, 2f));
            float deadline = Time.time + 3f;
            while ((first.CurrentState != NpcState.Activity || second.CurrentState != NpcState.Activity)
                && Time.time < deadline) yield return null;
            Assert.That(first.CurrentState, Is.EqualTo(NpcState.Activity));
            Assert.That(second.CurrentState, Is.EqualTo(NpcState.Activity));
            NpcActivityPoint firstHome = first.Home;
            NpcActivityPoint secondHome = second.Home;
            Assert.That(first.Navigation.Warp(Vector3.right * 0.8f), Is.True);
            first.Navigation.velocity = Vector3.right;
            BeginAmbientConversation(first, second);
            BeginAmbientConversation(second, first);
            Assert.That(first.Navigation.velocity.sqrMagnitude, Is.LessThan(0.001f));
            Assert.That(first.CurrentState, Is.EqualTo(NpcState.Conversation));
            Assert.That(second.CurrentState, Is.EqualTo(NpcState.Conversation));
            first.BeginPlayerInteraction(Root("Player interrupting ambient chat").transform);
            Assert.That(first.CurrentState, Is.EqualTo(NpcState.PlayerConversation));
            Assert.That(second.CurrentState, Is.Not.EqualTo(NpcState.Conversation),
                "An interrupted ambient partner must not continue talking to someone who is now speaking to the player.");
            Assert.That(firstHome.Occupant, Is.SameAs(first));
            Assert.That(secondHome.Occupant, Is.SameAs(second));
            Vector3 paused = first.transform.position;
            deadline = Time.time + 0.5f;
            while (Time.time < deadline)
            {
                yield return null;
                Assert.That(Vector3.Distance(first.transform.position, paused), Is.LessThan(0.03f));
            }
            first.EndPlayerInteraction();
            deadline = Time.time + 4f;
            while ((Vector3.Distance(first.transform.position, firstHome.transform.position) > 0.5f
                || first.CurrentState != NpcState.Activity) && Time.time < deadline) yield return null;
            Assert.That(first.CurrentState, Is.EqualTo(NpcState.Activity));
            Assert.That(firstHome.Occupant, Is.SameAs(first));
            Assert.That(Vector3.Distance(first.transform.position, firstHome.transform.position), Is.LessThan(0.5f));
        }

        [UnityTest]
        public IEnumerator ConversationRetreatCanReachItsTwoMetreSpotAtADifferentSlopeHeight()
        {
            BuildConversationSlope();
            NpcInteractable npc = CreateNpc();
            StoryPlayerCinematicMover mover = CreatePlayer(new Vector3(0f, 0.45f, 0.8f));
            yield return null;
            npc.Interact(mover.transform);
            Assert.That(npc.ConversationPreparing, Is.True,
                "A supported uphill retreat must not be rejected by a fixed same-height tolerance.");
            float deadline = Time.time + 8f;
            while (!npc.DialogueActive && Time.time < deadline) yield return null;
            Assert.That(npc.DialogueActive, Is.True);
            Assert.That(Vector3.ProjectOnPlane(mover.transform.position - npc.transform.position, Vector3.up).magnitude,
                Is.InRange(1.85f, 2.15f));
            Assert.That(mover.transform.position.y - npc.transform.position.y, Is.GreaterThan(0.7f));
            Assert.That(mover.GetComponent<PlayerController>().StoryMovementLocked, Is.True);
            npc.CloseDialogue();
            Assert.That(mover.GetComponent<PlayerController>().StoryMovementLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator ANewBlockerDuringRetreatCannotLockPlayerControlForever()
        {
            NpcInteractable npc = CreateNpc();
            StoryPlayerCinematicMover mover = CreatePlayer(Vector3.forward * 0.8f);
            PlayerController movement = mover.GetComponent<PlayerController>();
            yield return null;
            npc.Interact(mover.transform);
            Assert.That(npc.ConversationPreparing, Is.True);
            Assert.That(movement.StoryMovementLocked, Is.True);
            // A physical obstacle can appear after the retreat path was accepted.
            // It is intentionally absent from the baked route, as a crowded
            // doorway or moving prop can be during normal gameplay.
            foreach (Vector3 center in new[]
            {
                new Vector3(-0.7f, 1.5f, 0.8f), new Vector3(0.7f, 1.5f, 0.8f),
                new Vector3(0f, 1.5f, 0.3f), new Vector3(0f, 1.5f, 1.3f)
            })
            {
                GameObject blocker = Root("Late conversation retreat blocker");
                blocker.transform.position = center;
                blocker.AddComponent<BoxCollider>().size = Mathf.Abs(center.x) > 0f
                    ? new Vector3(0.1f, 3f, 1.1f) : new Vector3(1.5f, 3f, 0.1f);
            }
            Physics.SyncTransforms();
            float deadline = Time.time + 9f;
            while (npc.ConversationPreparing && Time.time < deadline) yield return null;
            Assert.That(npc.ConversationPreparing, Is.False);
            Assert.That(npc.DialogueActive, Is.False,
                "No safe two metre position exists; dialogue must not begin inside the crowd.");
            Assert.That(mover.IsActive, Is.False);
            Assert.That(movement.StoryMovementLocked, Is.False);
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
            Assert.That(npc.GetComponent<OfficeNpcAgent>().CurrentState, Is.EqualTo(NpcState.StoryHeld));
        }

        private void BuildConversationSlope()
        {
            if (instance.valid) instance.Remove();
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(objects[0]);
            Quaternion rotation = Quaternion.Euler(-Mathf.Atan(0.5f) * Mathf.Rad2Deg, 0f, 0f);
            Vector3 center = Vector3.down * (0.1f / Mathf.Cos(Mathf.Atan(0.5f)));
            Vector3 size = new Vector3(6f, 0.2f, 12f);
            GameObject ramp = Root("Supported conversation ramp");
            ramp.transform.SetPositionAndRotation(center, rotation);
            ramp.AddComponent<BoxCollider>().size = size;
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(center, rotation, Vector3.one), size = size, area = 0 }
            }, new Bounds(Vector3.zero, new Vector3(16f, 12f, 18f)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            instance = NavMesh.AddNavMeshData(data);
            Assert.That(instance.valid, Is.True);
            Physics.SyncTransforms();
        }

        private OfficeNpcAgent CreateAmbientNpc(Vector3 position)
        {
            GameObject homeRoot = Root("Ambient partner home");
            homeRoot.transform.position = position;
            var home = homeRoot.AddComponent<NpcActivityPoint>();
            home.Configure("Ambient partner home", NpcActivityKind.Work, NpcZone.Interior, 0,
                new Vector2(30f, 30f));
            GameObject root = Root("Ambient conversation partner");
            root.SetActive(false);
            root.transform.position = position;
            var navigation = root.AddComponent<NavMeshAgent>();
            navigation.radius = 0.34f;
            navigation.height = 1.9f;
            var body = root.AddComponent<CapsuleCollider>();
            body.radius = 0.34f;
            body.height = 1.9f;
            body.center = Vector3.up * 0.95f;
            var actor = root.AddComponent<OfficeNpcAgent>();
            actor.Configure("Ambient partner", NpcRole.Worker, home, new[] { home }, true);
            root.SetActive(true);
            return actor;
        }

        private static void BeginAmbientConversation(OfficeNpcAgent first, OfficeNpcAgent second)
            => typeof(OfficeNpcAgent).GetMethod("BeginConversation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(first, new object[] { second, 3f, "Selamat pagi." });

        private NpcInteractable CreateNpc()
        {
            var homeObject = Root("Conversation home");
            NpcActivityPoint home = homeObject.AddComponent<NpcActivityPoint>();
            home.Configure("Home", NpcActivityKind.Work, NpcZone.Interior, 0, new Vector2(30f, 30f));
            GameObject root = Root("Conversation NPC");
            root.SetActive(false);
            NavMeshAgent navigation = root.AddComponent<NavMeshAgent>();
            navigation.radius = 0.34f;
            navigation.height = 1.9f;
            CapsuleCollider body = root.AddComponent<CapsuleCollider>();
            body.radius = 0.34f;
            body.height = 1.9f;
            body.center = Vector3.up * 0.95f;
            profile = ScriptableObject.CreateInstance<NpcProfile>();
            profile.Configure("NPC-SPACING", "Pekerja", 30, 2.5f, NpcRole.Worker, true,
                null, null, new[] { "Selamat pagi." }, new[] { "Selamat pagi." });
            OfficeNpcAgent agent = root.AddComponent<OfficeNpcAgent>();
            agent.SetProfile(profile);
            agent.BindWaypoints(home, new[] { home });
            agent.HoldForStory();
            NpcInteractable npc = root.AddComponent<NpcInteractable>();
            root.SetActive(true);
            return npc;
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
            GameObject root = Root("Conversation player");
            root.SetActive(false);
            root.transform.position = position;
            CharacterController body = root.AddComponent<CharacterController>();
            body.radius = 0.3f;
            body.height = 1.75f;
            body.center = Vector3.up * 0.875f;
            PlayerInputReader input = root.AddComponent<PlayerInputReader>();
            Set(input, "inputActions", actions);
            PlayerController movement = root.AddComponent<PlayerController>();
            Set(movement, "input", input);
            movement.enabled = false;
            StoryPlayerCinematicMover mover = root.AddComponent<StoryPlayerCinematicMover>();
            mover.Configure(movement, body, 0);
            root.SetActive(true);
            input.enabled = false;
            Physics.SyncTransforms();
            return mover;
        }

        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private GameObject Root(string name)
        {
            var root = new GameObject(name);
            objects.Add(root);
            return root;
        }
    }
}
