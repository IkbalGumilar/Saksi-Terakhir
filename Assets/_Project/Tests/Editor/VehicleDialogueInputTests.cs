using System;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SaksiTerakhir.Tests
{
    public sealed class VehicleDialogueInputTests
    {
        private GameObject actor;
        private GameObject story;
        private DialogueSequence sequence;
        private PlayerController movement;
        private PlayerCameraController camera;
        private StoryDialogueController dialogue;
        private Transform eye;
        private float originalTimeScale;
        private CursorLockMode originalCursorLock;
        private bool originalCursorVisible;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            originalCursorLock = Cursor.lockState;
            originalCursorVisible = Cursor.visible;
            actor = new GameObject("Vehicle dialogue input actor");
            actor.SetActive(false);
            actor.AddComponent<CharacterController>();
            movement = actor.AddComponent<PlayerController>();
            camera = actor.AddComponent<PlayerCameraController>();
            eye = new GameObject("Vehicle dialogue input eye").transform;
            eye.SetParent(actor.transform, false);
            Set(camera, "body", actor.GetComponent<CharacterController>());
            Set(camera, "yawTransform", actor.transform);
            Set(camera, "cameraTransform", eye);
            story = new GameObject("Vehicle dialogue input story");
            dialogue = story.AddComponent<StoryDialogueController>();
            Set(dialogue, "playerMovement", movement);
            Set(dialogue, "playerCamera", camera);
            sequence = ScriptableObject.CreateInstance<DialogueSequence>();
            sequence.Configure("test.vehicle", new[] { new DialogueLine("PLAYER", "Percakapan perjalanan.") });
        }

        [TearDown]
        public void TearDown()
        {
            dialogue?.Stop();
            if (actor != null) Object.DestroyImmediate(actor);
            if (story != null) Object.DestroyImmediate(story);
            if (sequence != null) Object.DestroyImmediate(sequence);
            Time.timeScale = originalTimeScale;
            Cursor.lockState = originalCursorLock;
            Cursor.visible = originalCursorVisible;
        }

        [Test]
        public void BossPhoneCallDoesNotLockWalkingOrLook()
        {
            sequence.Configure("chapter.boss_call", new[] { new DialogueLine("NPC-006", "Kembali ke ruangan saya.") });
            camera.SetStoryFocus(story.transform);

            Assert.That(dialogue.Play(sequence, null), Is.True);

            Assert.That(movement.StoryDialogueMovementLocked, Is.False);
            Assert.That(camera.StoryDialogueLookLocked, Is.False);
            Assert.That(camera.StoryFocusTarget, Is.Null);
        }

        [Test]
        public void VehicleDialogueKeepsLookFreeAndAllowsInventory()
        {
            Call(dialogue, "SetVehicleTravelActive", true);
            movement.SetCinematicMovementLocked(true);
            var inventory = actor.AddComponent<InventoryPanelController>();
            Set(inventory, "movement", movement);
            Set(inventory, "cameraController", camera);
            Set(inventory, "dialogue", dialogue);

            Assert.That(dialogue.Play(sequence, null), Is.True);

            Assert.That(camera.StoryDialogueLookLocked, Is.False);
            Assert.That(movement.StoryCinematicMovementLocked, Is.True, "Walking remains owned by the seat.");
            Assert.That(Call(inventory, "IsStoryBusy"), Is.False);
            dialogue.Stop();
            Assert.That(Call(inventory, "IsStoryBusy"), Is.False, "Inventory remains available between car lines.");
        }

        [Test]
        public void PhoneCallBlocksInventoryEvenInsideVehicle()
        {
            Call(dialogue, "SetVehicleTravelActive", true);
            sequence.Configure("chapter.boss_call", new[] { new DialogueLine("NPC-006", "Kembali.") });
            var inventory = actor.AddComponent<InventoryPanelController>();
            Set(inventory, "dialogue", dialogue);

            Assert.That(dialogue.Play(sequence, null), Is.True);

            Assert.That(Call(inventory, "IsStoryBusy"), Is.True);
            dialogue.Stop();
            Assert.That(Call(inventory, "IsStoryBusy"), Is.False);
        }

        [Test]
        public void SeatedLookRotatesCameraInsteadOfPlayerBody()
        {
            var input = actor.AddComponent<PlayerInputReader>();
            Set(camera, "input", input);
            Set(input, "<LookValue>k__BackingField", new Vector2(600f, -200f));
            Set(input, "<IsLookFromPointer>k__BackingField", true);
            actor.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            camera.SetStorySeated(true);

            Call(camera, "ApplyLook");
            Call(camera, "ApplyEyeHeight");

            Assert.That(actor.transform.eulerAngles.y, Is.EqualTo(35f).Within(0.01f));
            Assert.That(Mathf.DeltaAngle(0f, eye.localEulerAngles.y), Is.EqualTo(42f).Within(0.01f));
            Assert.That(Mathf.DeltaAngle(0f, eye.localEulerAngles.x), Is.EqualTo(14f).Within(0.01f));
            camera.SetStorySeated(false);
            Assert.That(Mathf.DeltaAngle(0f, eye.localEulerAngles.y), Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void PauseRestoresTimeAndLocksAfterResume()
        {
            Component pause = CreatePause();
            Set(pause, "movement", movement);
            Set(pause, "cameraController", camera);
            Time.timeScale = 0.75f;

            Assert.That(Call(pause, "TryPause"), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(movement.UiMovementLocked, Is.True);
            Assert.That(camera.UiLookLocked, Is.True);

            Call(pause, "Resume");
            Assert.That(Time.timeScale, Is.EqualTo(0.75f));
            Assert.That(movement.UiMovementLocked, Is.False);
            Assert.That(camera.UiLookLocked, Is.False);
        }

        [Test]
        public void PhoneCallRejectsPauseWithoutFreezingGame()
        {
            Component pause = CreatePause();
            Set(pause, "dialogue", dialogue);
            sequence.Configure("chapter.boss_call", new[] { new DialogueLine("NPC-006", "Kembali.") });
            dialogue.Play(sequence, null);
            Time.timeScale = 1f;

            Assert.That(Call(pause, "TryPause"), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void EscapeOnlyRequestsFuturePauseUiWhenNoPanelIsAssigned()
        {
            Component pause = CreatePause();
            int requests = 0;
            EventInfo requested = pause.GetType().GetEvent("PauseRequested");
            Assert.That(requested, Is.Not.Null);
            Action listener = () => requests++;
            requested.AddEventHandler(pause, listener);
            Time.timeScale = 1f;

            Call(pause, "OnPausePressed");

            Assert.That(requests, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "An unimplemented pause menu must not trap the player.");
        }

        [Test]
        public void StandingLookStillTurnsPlayerBody()
        {
            var input = actor.AddComponent<PlayerInputReader>();
            Set(camera, "input", input);
            Set(input, "<LookValue>k__BackingField", new Vector2(600f, 0f));
            Set(input, "<IsLookFromPointer>k__BackingField", true);
            actor.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

            Call(camera, "ApplyLook");
            Call(camera, "ApplyEyeHeight");

            Assert.That(actor.transform.eulerAngles.y, Is.EqualTo(77f).Within(0.01f));
            Assert.That(Mathf.DeltaAngle(0f, eye.localEulerAngles.y), Is.EqualTo(0f).Within(0.01f));
        }

        private Component CreatePause()
        {
            Type type = typeof(PlayerController).Assembly.GetType("SaksiTerakhir.Player.PlayerPauseController");
            Assert.That(type, Is.Not.Null, "Pause controller must provide future UI hookup.");
            return actor.AddComponent(type);
        }

        private static object Call(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(target, arguments);
        }

        private static void Set(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
