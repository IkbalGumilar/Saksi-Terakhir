using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class PlayerCinematicCoreTests
    {
        private GameObject playerObject;
        private GameObject targetObject;
        private GameObject moverObject;
        private GameObject cameraObject;

        [TearDown]
        public void TearDown()
        {
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (moverObject != null) Object.DestroyImmediate(moverObject);
            if (targetObject != null) Object.DestroyImmediate(targetObject);
            if (playerObject != null) Object.DestroyImmediate(playerObject);
        }

        [Test]
        public void DialogueAndCinematicMovementLocksRemainIndependent()
        {
            PlayerController player = CreatePlayer();

            player.SetStoryMovementLocked(true);
            player.SetCinematicMovementLocked(true);
            Assert.That(player.StoryDialogueMovementLocked, Is.True);
            Assert.That(player.StoryCinematicMovementLocked, Is.True);
            Assert.That(player.StoryMovementLocked, Is.True);

            player.SetStoryMovementLocked(false);
            Assert.That(player.StoryDialogueMovementLocked, Is.False);
            Assert.That(player.StoryCinematicMovementLocked, Is.True);
            Assert.That(player.StoryMovementLocked, Is.True,
                "Ending dialogue must not restore input while an automatic walk owns the player.");

            player.SetCinematicMovementLocked(false);
            Assert.That(player.StoryMovementLocked, Is.False);
        }

        [Test]
        public void CinematicMoverRejectsMissingTargetWithoutTakingControl()
        {
            PlayerController player = CreatePlayer();
            moverObject = new GameObject("Story Mover");
            StoryPlayerCinematicMover mover = moverObject.AddComponent<StoryPlayerCinematicMover>();
            mover.Configure(player, playerObject.GetComponent<CharacterController>());

            Assert.That(mover.BeginFollow(null, 2f), Is.False);
            Assert.That(mover.IsActive, Is.False);
            Assert.That(player.StoryCinematicMovementLocked, Is.False);
        }

        [Test]
        public void StoryFocusCanLockLookAndClearWithoutChangingTheBaseSetting()
        {
            cameraObject = new GameObject("Camera Controller");
            PlayerCameraController camera = cameraObject.AddComponent<PlayerCameraController>();
            targetObject = new GameObject("Speaker");

            camera.SetStoryFocus(targetObject.transform);
            camera.SetStoryLookLocked(true);

            Assert.That(camera.StoryFocusTarget, Is.EqualTo(targetObject.transform));
            Assert.That(camera.StoryLookLocked, Is.True);
            Assert.That(camera.IsLookInputAllowed, Is.False);

            camera.ClearStoryFocus();
            camera.SetStoryLookLocked(false);
            Assert.That(camera.StoryFocusTarget, Is.Null);
            Assert.That(camera.StoryLookLocked, Is.False);
            Assert.That(camera.IsLookEnabled, Is.True);
        }

        [Test]
        public void SeatingHelperDoesNothingWhenNoSeatWasProvided()
        {
            PlayerController player = CreatePlayer();
            StoryPlayerSeating seating = playerObject.AddComponent<StoryPlayerSeating>();
            seating.Configure(player, playerObject.GetComponent<CharacterController>(), null);

            Assert.That(seating.SeatAt(null), Is.False);
            Assert.That(seating.IsSeated, Is.False);
            Assert.That(player.StoryCinematicMovementLocked, Is.False);
        }

        [Test]
        public void SeatingLowersTheFirstPersonViewWithoutChangingThePlayerCapsule()
        {
            PlayerController player = CreatePlayer();
            CharacterController body = playerObject.GetComponent<CharacterController>();
            body.height = 1.7f;
            PlayerCameraController camera = CreatePlayerCamera(body);
            StoryPlayerSeating seating = playerObject.AddComponent<StoryPlayerSeating>();
            seating.Configure(player, body, null);
            targetObject = new GameObject("Player Sofa Seat");

            Assert.That(seating.SeatAt(targetObject.transform), Is.True);
            Assert.That(cameraObject.transform.localPosition.y, Is.EqualTo(1.05f).Within(0.001f));
            Assert.That(body.height, Is.EqualTo(1.7f).Within(0.001f));
        }

        [Test]
        public void StandingUpRestoresTheFirstPersonViewAndPlayerInput()
        {
            PlayerController player = CreatePlayer();
            CharacterController body = playerObject.GetComponent<CharacterController>();
            body.height = 1.7f;
            CreatePlayerCamera(body);
            StoryPlayerSeating seating = playerObject.AddComponent<StoryPlayerSeating>();
            seating.Configure(player, body, null);
            targetObject = new GameObject("Player Sofa Seat");
            Assert.That(seating.SeatAt(targetObject.transform), Is.True);

            seating.StandUp();

            Assert.That(cameraObject.transform.localPosition.y, Is.EqualTo(1.564f).Within(0.001f));
            Assert.That(player.StoryCinematicMovementLocked, Is.False);
        }

        private PlayerCameraController CreatePlayerCamera(CharacterController body)
        {
            cameraObject = new GameObject("First Person View");
            cameraObject.transform.SetParent(playerObject.transform);
            PlayerCameraController camera = playerObject.AddComponent<PlayerCameraController>();
            SetPrivate(camera, "input", playerObject.AddComponent<PlayerInputReader>());
            SetPrivate(camera, "body", body);
            SetPrivate(camera, "yawTransform", playerObject.transform);
            SetPrivate(camera, "cameraTransform", cameraObject.transform);
            return camera;
        }

        private static void SetPrivate(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private PlayerController CreatePlayer()
        {
            playerObject = new GameObject("Player");
            playerObject.SetActive(false);
            playerObject.AddComponent<CharacterController>();
            return playerObject.AddComponent<PlayerController>();
        }
    }
}
