using NUnit.Framework;
using SaksiTerakhir.Npc;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class NpcInteractableTests
    {
        private GameObject actorObject;
        private GameObject playerObject;
        private NpcProfile profile;

        [TearDown]
        public void TearDown()
        {
            if (actorObject != null) Object.DestroyImmediate(actorObject);
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            if (profile != null) Object.DestroyImmediate(profile);
        }

        [Test]
        public void PressingInteractCyclesDialogueAndReleasesTheNpcAfterTheLastLine()
        {
            profile = ScriptableObject.CreateInstance<NpcProfile>();
            profile.Configure("NPC-TEST", "Resepsionis", 31, 2.5f, NpcRole.Receptionist,
                true, null, null,
                new[] { "Selamat datang.", "Silakan isi buku tamu.", "Ruang arsip ada di dalam." },
                new[] { "Selamat pagi." });
            actorObject = new GameObject("Receptionist test");
            actorObject.SetActive(false);
            OfficeNpcAgent agent = actorObject.AddComponent<OfficeNpcAgent>();
            agent.SetProfile(profile);
            NpcInteractable npc = actorObject.AddComponent<NpcInteractable>();
            playerObject = new GameObject("Player test");
            actorObject.SetActive(true);

            Assert.That(npc.CanInteract(playerObject.transform), Is.True);
            Assert.That(npc.DisplayName, Is.EqualTo("Resepsionis"));
            Assert.That(npc.PromptKey, Is.EqualTo("interact.npc.talk"));

            npc.Interact(playerObject.transform);
            Assert.That(npc.ActiveDialogueLine, Is.EqualTo("Selamat datang."));
            Assert.That(agent.CurrentState, Is.EqualTo(NpcState.PlayerConversation));

            npc.Interact(playerObject.transform);
            Assert.That(npc.ActiveDialogueLine, Is.EqualTo("Silakan isi buku tamu."));
            npc.Interact(playerObject.transform);
            Assert.That(npc.ActiveDialogueLine, Is.EqualTo("Ruang arsip ada di dalam."));

            npc.Interact(playerObject.transform);
            Assert.That(npc.DialogueActive, Is.False);
            Assert.That(agent.CurrentState, Is.Not.EqualTo(NpcState.PlayerConversation));

            npc.Interact(playerObject.transform);
            Assert.That(npc.ActiveDialogueLine, Is.EqualTo("Selamat datang."));
            npc.CloseDialogue();
            Assert.That(agent.CurrentState, Is.Not.EqualTo(NpcState.PlayerConversation));
        }
    }
}
