using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using UnityEngine;
using UnityEngine.TestTools;

namespace SaksiTerakhir.Tests
{
    public sealed class StoryPlayerLifecycleTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();
        private GameObject playerRoot;
        private PlayerController movement;
        private CharacterController body;
        private PlayerCameraController camera;
        private Transform eye;
        private StoryPlayerCinematicMover mover;
        private StoryPlayerSeating seating;
        private ChapterOneDirector director;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Application.isPlaying, Is.False);
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            playerRoot = InactiveRoot("Lifecycle Player");
            body = playerRoot.AddComponent<CharacterController>();
            body.height = 1.75f;
            body.center = Vector3.up * 0.875f;
            var input = playerRoot.AddComponent<PlayerInputReader>();
            movement = playerRoot.AddComponent<PlayerController>();
            Set(movement, "input", input);
            camera = playerRoot.AddComponent<PlayerCameraController>();
            eye = new GameObject("Lifecycle Eye").transform;
            eye.SetParent(playerRoot.transform, false);
            Set(camera, "input", input);
            Set(camera, "body", body);
            Set(camera, "yawTransform", playerRoot.transform);
            Set(camera, "cameraTransform", eye);
            mover = playerRoot.AddComponent<StoryPlayerCinematicMover>();
            Assert.That(mover.Configure(movement, body), Is.True);
            seating = playerRoot.AddComponent<StoryPlayerSeating>();
            Assert.That(seating.Configure(movement, body, null), Is.True);

            // Keep the director inactive so this isolated cleanup fixture never initializes
            // authored story state, loads a save, or starts a cinematic during EditMode.
            director = InactiveRoot("Lifecycle Director").AddComponent<ChapterOneDirector>();
            director.StartupManagedExternally = true;
            Set(director, "player", playerRoot.transform);
            Set(director, "playerMover", mover);
            Set(director, "playerSeating", seating);
            Set(director, "playerCamera", camera);
            Assert.That(director.Progress, Is.Null);
        }

        [TearDown]
        public void TearDown()
        {
            // Remove the inactive owner first; each test invokes the relevant cleanup
            // deliberately, rather than relying on Editor callback ordering at disposal.
            for (int index = roots.Count - 1; index >= 0; index--)
                if (roots[index] != null) Object.DestroyImmediate(roots[index]);
            roots.Clear();
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void DirectorCleanupAfterPlayerDestructionClearsPendingFlags(bool seatPending, bool escortPending)
        {
            Set(director, "seatAwaitingPath", seatPending);
            Set(director, "escortAwaitingPath", escortPending);
            Object.DestroyImmediate(playerRoot);
            Assert.That(mover == null, Is.True, "Keep the destroyed Unity wrapper in the director's serialized reference.");

            Assert.DoesNotThrow(() => Disable(director));
            Assert.That(Field<bool>(director, "seatAwaitingPath"), Is.False);
            Assert.That(Field<bool>(director, "escortAwaitingPath"), Is.False);
            Assert.DoesNotThrow(() => Disable(director), "Repeated cleanup must remain safe.");
            Assert.That(director.StartupManagedExternally, Is.True);
            Assert.That(director.Progress, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DirectorCleanupSkipsDestroyedMoverWhileReleasingLivePendingLocks()
        {
            Set(director, "seatAwaitingPath", true);
            Set(director, "escortAwaitingPath", true);
            movement.SetCinematicMovementLocked(true);
            Object.DestroyImmediate(mover);
            Assert.That(mover == null, Is.True);

            Assert.DoesNotThrow(() => Disable(director));
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
            Assert.That(Field<bool>(director, "seatAwaitingPath"), Is.False);
            Assert.That(Field<bool>(director, "escortAwaitingPath"), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NormalDirectorCleanupReleasesOnlyItsCinematicLocks()
        {
            SeedSeparateLocks();
            Set(director, "seatAwaitingPath", true);
            Set(director, "escortAwaitingPath", true);
            camera.SetCinematicLookLocked(true);
            camera.SetStoryLookLocked(true);
            camera.SetUiLookLocked(true);

            Assert.DoesNotThrow(() => Disable(director));
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
            Assert.That(movement.CinematicVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(movement.StoryDialogueMovementLocked, Is.True);
            Assert.That(movement.UiMovementLocked, Is.True);
            Assert.That(camera.StoryCinematicLookLocked, Is.False);
            Assert.That(camera.StoryDialogueLookLocked, Is.True);
            Assert.That(camera.UiLookLocked, Is.True);
            Assert.That(Field<bool>(director, "seatAwaitingPath"), Is.False);
            Assert.That(Field<bool>(director, "escortAwaitingPath"), Is.False);
            Assert.DoesNotThrow(() => Disable(director));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NormalMoverCleanupStopsVelocityAndReleasesOnlyItsCinematicLock()
        {
            SeedSeparateLocks();
            Set(mover, "ownsCinematicLock", true);

            Assert.DoesNotThrow(() => Disable(mover));
            Assert.That(movement.CinematicVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
            Assert.That(movement.StoryDialogueMovementLocked, Is.True);
            Assert.That(movement.UiMovementLocked, Is.True);
            Assert.That(Field<bool>(mover, "ownsCinematicLock"), Is.False);
            Assert.DoesNotThrow(mover.StopCinematicMotion);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DestroyedMoverCleanupCannotReleaseANewCinematicOwner()
        {
            Object.DestroyImmediate(mover);
            Assert.That(mover == null, Is.True);
            // Ensure this stale wrapper has managed ownership state even if destroying
            // its component already ran OnDisable; a stale call must not affect a new owner.
            Set(mover, "ownsCinematicLock", true);
            movement.SetCinematicMovementLocked(true);
            movement.SetCinematicVelocity(Vector3.forward);

            Assert.DoesNotThrow(mover.StopCinematicMotion);
            Assert.That(movement.StoryCinematicMovementLocked, Is.True);
            Assert.That(movement.CinematicVelocity, Is.EqualTo(Vector3.forward));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NormalSeatingCleanupClearsItsPoseAndOnlyItsCinematicLock()
        {
            SeatPlayer();
            movement.SetStoryMovementLocked(true);
            movement.SetUiMovementLocked(true);

            Assert.DoesNotThrow(() => Disable(seating));
            Assert.That(seating.IsSeated, Is.False);
            Assert.That(seating.CurrentSeat, Is.Null);
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
            Assert.That(movement.StoryDialogueMovementLocked, Is.True);
            Assert.That(movement.UiMovementLocked, Is.True);
            Assert.That(eye.localPosition.y, Is.EqualTo(body.height * 0.92f).Within(0.0001f));
            Assert.DoesNotThrow(seating.StandUp);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DestroyedSeatingCleanupCannotChangeLiveEyeOrReleaseANewCinematicOwner()
        {
            SeatPlayer();
            Object.DestroyImmediate(seating);
            Assert.That(seating == null, Is.True);
            // Explicit managed state prevents a false GREEN from earlier native cleanup.
            Set(seating, "<IsSeated>k__BackingField", true);
            Set(seating, "ownsCinematicLock", true);
            movement.SetCinematicMovementLocked(true);
            camera.SetStorySeated(true);
            Vector3 seatedEye = eye.localPosition;

            Assert.DoesNotThrow(seating.StandUp);
            Assert.That(movement.StoryCinematicMovementLocked, Is.True);
            Assert.That(eye.localPosition, Is.EqualTo(seatedEye));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LiveSeatingCleanupSkipsDestroyedCameraWithSurvivingEyeTransform()
        {
            SeatPlayer();
            Object.DestroyImmediate(camera);
            Assert.That(camera == null, Is.True);
            Vector3 previousEye = eye.localPosition;

            Assert.DoesNotThrow(seating.StandUp);
            Assert.That(seating.IsSeated, Is.False);
            Assert.That(seating.CurrentSeat, Is.Null);
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
            Assert.That(eye.localPosition, Is.EqualTo(previousEye),
                "Cleanup must not call a destroyed camera wrapper that still holds live transforms.");
            LogAssert.NoUnexpectedReceived();
        }

        private void SeatPlayer()
        {
            Transform seat = InactiveRoot("Lifecycle Seat").transform;
            seat.SetPositionAndRotation(new Vector3(2f, 0f, 3f), Quaternion.Euler(0f, 35f, 0f));
            Assert.That(seating.SeatAt(seat), Is.True);
            Assert.That(seating.IsSeated, Is.True);
            Assert.That(movement.StoryCinematicMovementLocked, Is.True);
            Assert.That(eye.localPosition.y, Is.EqualTo(1.05f).Within(0.0001f));
        }

        private void SeedSeparateLocks()
        {
            movement.SetStoryMovementLocked(true);
            movement.SetUiMovementLocked(true);
            movement.SetCinematicMovementLocked(true);
            movement.SetCinematicVelocity(new Vector3(1f, 0f, 2f));
        }

        private GameObject InactiveRoot(string name)
        {
            var root = new GameObject(name);
            root.SetActive(false);
            roots.Add(root);
            return root;
        }

        private static void Disable(object component)
        {
            MethodInfo method = component.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(component, null);
        }

        private static T Field<T>(object component, string name)
            => (T)FindField(component, name).GetValue(component);

        private static void Set(object component, string name, object value)
            => FindField(component, name).SetValue(component, value);

        private static FieldInfo FindField(object component, string name)
        {
            FieldInfo field = component.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return field;
        }
    }
}
