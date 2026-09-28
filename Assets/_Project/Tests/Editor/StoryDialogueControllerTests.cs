using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class StoryDialogueControllerTests
    {
        private GameObject storyObject;
        private GameObject playerObject;
        private GameObject cameraObject;
        private GameObject doorObject;
        private DialogueSequence sequence;

        [TearDown]
        public void TearDown()
        {
            if (storyObject != null) Object.DestroyImmediate(storyObject);
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (doorObject != null) Object.DestroyImmediate(doorObject);
            if (sequence != null) Object.DestroyImmediate(sequence);
        }

        [Test]
        public void InteractIsConsumedButNeverAdvancesAnAutomaticStoryLine()
        {
            StoryDialogueController dialogue = CreateDialogue();
            int completed = 0;
            Assert.That(dialogue.Play(sequence, () => completed++), Is.True);
            Assert.That(dialogue.IsPlaying, Is.True);
            Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("Kalimat pertama."));
            Assert.That(dialogue.TryConsumeInteract(), Is.True);
            Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("Kalimat pertama."));
            Assert.That(completed, Is.Zero);
            Advance(dialogue);
            Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("Kalimat kedua."));
            Advance(dialogue);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(dialogue.IsPlaying, Is.False);
            Assert.That(dialogue.TryConsumeInteract(), Is.False);
        }

        [Test]
        public void InteractDuringStoryDoesNotAlsoOpenTheDoor()
        {
            StoryDialogueController dialogue = CreateDialogue();
            doorObject = new GameObject("Door");
            doorObject.AddComponent<BoxCollider>();
            DoorInteractable door = doorObject.AddComponent<DoorInteractable>();
            playerObject = new GameObject("Player");
            playerObject.SetActive(false);
            PlayerInteractor interactor = playerObject.AddComponent<PlayerInteractor>();
            SetPrivate(interactor, "storyDialogue", dialogue);
            SetPrivate(interactor, "current", door);
            dialogue.Play(sequence, null);

            typeof(PlayerInteractor).GetMethod("OnInteractPressed", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(interactor, null);

            Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("Kalimat pertama."));
            Assert.That(door.IsOpen, Is.False);
        }

        [Test]
        public void EveryStoryDialogueLocksAndRestoresPlayerTranslation()
        {
            playerObject = new GameObject("Player");
            playerObject.SetActive(false);
            playerObject.AddComponent<CharacterController>();
            PlayerController player = playerObject.AddComponent<PlayerController>();
            StoryDialogueController dialogue = CreateDialogue();
            SetPrivate(dialogue, "playerMovement", player);

            dialogue.Play(sequence, null, autoAdvance: true);
            Assert.That(player.StoryMovementLocked, Is.True);
            Advance(dialogue);
            Advance(dialogue);
            Assert.That(player.StoryMovementLocked, Is.False);
        }

        [Test]
        public void EveryStoryDialogueLocksAndRestoresPlayerLook()
        {
            cameraObject = new GameObject("Story Camera");
            cameraObject.SetActive(false);
            PlayerCameraController camera = cameraObject.AddComponent<PlayerCameraController>();
            StoryDialogueController dialogue = CreateDialogue();
            SetPrivate(dialogue, "playerCamera", camera);

            dialogue.Play(sequence, null);
            Assert.That(camera.StoryDialogueLookLocked, Is.True);
            Advance(dialogue);
            Advance(dialogue);
            Assert.That(camera.StoryDialogueLookLocked, Is.False);
            Assert.That(camera.StoryFocusTarget, Is.Null);
        }

        [Test]
        public void LineDurationScalesWithTextLengthAndIncludesRevealTime()
        {
            StoryDialogueController dialogue = CreateDialogue();
            SetPrivate(dialogue, "charactersPerSecond", 20f);
            SetPrivate(dialogue, "minimumLineSeconds", 0.5f);
            SetPrivate(dialogue, "postRevealSeconds", 0.25f);

            Assert.That(dialogue.GetLineDuration("pendek"), Is.EqualTo(0.55f).Within(0.001f));
            Assert.That(dialogue.GetLineDuration("12345678901234567890"), Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(dialogue.GetLineDuration("x"), Is.EqualTo(0.5f).Within(0.001f));
        }

        private StoryDialogueController CreateDialogue()
        {
            sequence = ScriptableObject.CreateInstance<DialogueSequence>();
            sequence.Configure("test.story", new[]
            {
                new DialogueLine("NPC-021", "Kalimat pertama."),
                new DialogueLine("NPC-022", "Kalimat kedua.")
            });
            storyObject = new GameObject("Story Dialogue");
            return storyObject.AddComponent<StoryDialogueController>();
        }

        private static void SetPrivate(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void Advance(StoryDialogueController dialogue)
        {
            MethodInfo method = typeof(StoryDialogueController).GetMethod("Advance",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(dialogue, null);
        }
    }
}
