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
        private GameObject doorObject;
        private DialogueSequence sequence;

        [TearDown]
        public void TearDown()
        {
            if (storyObject != null) Object.DestroyImmediate(storyObject);
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            if (doorObject != null) Object.DestroyImmediate(doorObject);
            if (sequence != null) Object.DestroyImmediate(sequence);
        }

        [Test]
        public void InteractAdvancesOneLineThenReleasesInput()
        {
            StoryDialogueController dialogue = CreateDialogue();
            int completed = 0;
            Assert.That(dialogue.Play(sequence, () => completed++), Is.True);
            Assert.That(dialogue.IsPlaying, Is.True);
            Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("Kalimat pertama."));
            Assert.That(dialogue.TryConsumeInteract(), Is.True);
            Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("Kalimat kedua."));
            Assert.That(completed, Is.Zero);
            Assert.That(dialogue.TryConsumeInteract(), Is.True);
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

            Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("Kalimat kedua."));
            Assert.That(door.IsOpen, Is.False);
        }

        [Test]
        public void StandingDialogueLocksAndRestoresPlayerTranslation()
        {
            playerObject = new GameObject("Player");
            playerObject.SetActive(false);
            playerObject.AddComponent<CharacterController>();
            PlayerController player = playerObject.AddComponent<PlayerController>();
            StoryDialogueController dialogue = CreateDialogue();
            SetPrivate(dialogue, "playerMovement", player);

            dialogue.Play(sequence, null);
            Assert.That(player.StoryMovementLocked, Is.True);
            dialogue.TryConsumeInteract();
            dialogue.TryConsumeInteract();
            Assert.That(player.StoryMovementLocked, Is.False);
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
    }
}
