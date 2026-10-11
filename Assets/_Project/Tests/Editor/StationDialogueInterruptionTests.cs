using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class StationDialogueInterruptionTests
    {
        [Test]
        public void StationCallWaitsForSpeechBoundaryAndResumesOnlyTheNextUnheardLine()
        {
            var root = new GameObject("Station dialogue test");
            var controller = root.AddComponent<StoryDialogueController>();
            var story = ScriptableObject.CreateInstance<DialogueSequence>();
            var call = ScriptableObject.CreateInstance<DialogueSequence>();
            story.Configure("station.grandfather", new[] { new DialogueLine("Kakek", "First speech"), new DialogueLine("Kakek", "Unheard speech") });
            call.Configure("station.call", new[] { new DialogueLine("NPC-004", "We are ready") }, isPhoneConversation: true);
            int completed = 0, calls = 0, storyFinished = 0;
            controller.LineCompleted += (sequence, _) => { if (sequence == story) completed++; };
            try
            {
                Assert.That(controller.Play(story, () => storyFinished++), Is.True);
                Assert.That(controller.QueueInterruption(call, () => calls++), Is.True);
                Assert.That(controller.ActiveSequence, Is.SameAs(story));
                Assert.That(completed, Is.Zero);
                Advance(controller);
                Assert.That(controller.ActiveSequence, Is.SameAs(call));
                Assert.That(completed, Is.EqualTo(1));
                Advance(controller);
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(controller.ActiveSequence, Is.SameAs(story));
                Assert.That(controller.LineIndex, Is.EqualTo(1));
                Advance(controller);
                Assert.That(storyFinished, Is.EqualTo(1));
                Assert.That(completed, Is.EqualTo(2));
                Assert.That(controller.IsPlaying, Is.False);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(story); Object.DestroyImmediate(call); }
        }

        private static void Advance(StoryDialogueController controller)
            => typeof(StoryDialogueController).GetMethod("Advance", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);

        [Test]
        public void AChoiceAtASpeechBoundaryCancelsTheQueuedCallWithoutAdvancingTheStoppedSequence()
        {
            var root = new GameObject("Dialogue choice fixture");
            var controller = root.AddComponent<StoryDialogueController>();
            var story = ScriptableObject.CreateInstance<DialogueSequence>();
            var call = ScriptableObject.CreateInstance<DialogueSequence>();
            story.Configure("fixture.story", new[] { new DialogueLine("PLAYER", "Heard"), new DialogueLine("PLAYER", "Unread") });
            call.Configure("fixture.call", new[] { new DialogueLine("NPC-004", "Ready") });
            int heard = 0, calls = 0;
            controller.LineCompleted += (_, _) => { heard++; controller.Stop(); };
            try
            {
                controller.Play(story, null);
                controller.QueueInterruption(call, () => calls++);
                Advance(controller);
                Assert.That(controller.IsPlaying || controller.HasQueuedInterruption, Is.False);
                Assert.That(heard, Is.EqualTo(1));
                Assert.That(calls, Is.Zero);
                Assert.That(controller.PlayFrom(story, 1, null), Is.True);
                Assert.That(controller.LineIndex, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(story); Object.DestroyImmediate(call); }
        }
    }
}
