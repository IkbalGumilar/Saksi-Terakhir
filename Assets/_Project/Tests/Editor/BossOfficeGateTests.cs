using NUnit.Framework;
using System.Reflection;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Story;
using UnityEngine;
using UnityEngine.AI;

namespace SaksiTerakhir.Tests
{
    public sealed class BossOfficeGateTests
    {
        private GameObject doorObject;
        private GameObject playerObject;
        private GameObject npcObject;
        private NpcProfile profile;
        private GameObject zoneObject;
        private GameObject barrierObject;

        [TearDown]
        public void TearDown()
        {
            if (doorObject != null) Object.DestroyImmediate(doorObject);
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            if (npcObject != null) Object.DestroyImmediate(npcObject);
            if (profile != null) Object.DestroyImmediate(profile);
            if (zoneObject != null) Object.DestroyImmediate(zoneObject);
            if (barrierObject != null) Object.DestroyImmediate(barrierObject);
        }

        [Test]
        public void FirstBossVisitAllowsPlayerThenLocksAfterLeaving()
        {
            ChapterOneProgress progress = AtBossVisit();
            (DoorInteractable door, BossOfficeGate gate) = CreateGate(progress);
            Assert.That(gate.CanOpenFor(playerObject.transform), Is.True);
            door.Interact(playerObject.transform);
            Assert.That(door.IsOpen, Is.True);
            gate.SetPlayerInside(true);
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);
            Assert.That(gate.CanOpenFor(playerObject.transform), Is.True,
                "Player still inside must be able to leave.");
            gate.SetPlayerInside(false);
            door.ForceClose();
            Assert.That(gate.CanOpenFor(playerObject.transform), Is.False);
            Assert.That(door.CanInteract(playerObject.transform), Is.True,
                "Locked door must remain detectable by the interaction sphere.");
            door.Interact(playerObject.transform);
            Assert.That(door.IsOpen, Is.False);
            Assert.That(gate.BlockedPromptKey, Is.EqualTo("interact.story.find_both"));
        }

        [TestCase("NPC-003", "interact.story.find_sinta")]
        [TestCase("NPC-004", "interact.story.find_raka")]
        public void OneColleagueDoorPromptNamesTheMissingPerson(string first, string prompt)
        {
            ChapterOneProgress progress = AtBossVisit();
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);
            progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, first);
            (DoorInteractable door, BossOfficeGate gate) = CreateGate(progress);
            int blockedCount = 0;
            gate.Blocked += _ => blockedCount++;

            door.Interact(playerObject.transform);
            door.Interact(playerObject.transform);

            Assert.That(door.IsOpen, Is.False);
            Assert.That(gate.BlockedPromptKey, Is.EqualTo(prompt));
            Assert.That(door.PromptKey, Is.EqualTo(prompt));
            Assert.That(blockedCount, Is.EqualTo(1));
        }

        [Test]
        public void InvitedNpcCanPassWithoutGrantingPlayerAccess()
        {
            ChapterOneProgress progress = AtBossVisit();
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);
            progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003");
            (DoorInteractable door, BossOfficeGate gate) = CreateGate(progress);
            npcObject = new GameObject("Raka");
            npcObject.SetActive(false);
            npcObject.AddComponent<NavMeshAgent>();
            OfficeNpcAgent npc = npcObject.AddComponent<OfficeNpcAgent>();
            profile = ScriptableObject.CreateInstance<NpcProfile>();
            profile.Configure("NPC-003", "Raka", 28, 2.6f, NpcRole.ColleagueA,
                false, null, null, null, null);
            npc.SetProfile(profile);
            npcObject.SetActive(true);

            Assert.That(gate.CanOpenFor(npc.transform), Is.True);
            Assert.That(gate.CanOpenFor(playerObject.transform), Is.False);
            door.Interact(npc.transform);
            Assert.That(door.IsOpen, Is.True);
            door.ForceClose();
            door.Interact(playerObject.transform);
            Assert.That(door.IsOpen, Is.False);
        }

        [Test]
        public void BarrierStaysSolidForPlayerAndDoorClosesBehindFirstColleague()
        {
            ChapterOneProgress progress = AtBossVisit();
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);
            progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003");
            (DoorInteractable door, BossOfficeGate gate) = CreateGate(progress);
            zoneObject = new GameObject("Room");
            zoneObject.transform.position = new Vector3(5f, 0f, 0f);
            BoxCollider zone = zoneObject.AddComponent<BoxCollider>();
            zone.size = new Vector3(4f, 3f, 4f);
            barrierObject = new GameObject("Barrier");
            BoxCollider barrier = barrierObject.AddComponent<BoxCollider>();
            npcObject = new GameObject("Raka");
            npcObject.SetActive(false);
            npcObject.AddComponent<NavMeshAgent>();
            OfficeNpcAgent npc = npcObject.AddComponent<OfficeNpcAgent>();
            profile = ScriptableObject.CreateInstance<NpcProfile>();
            profile.Configure("NPC-003", "Raka", 28, 2.6f, NpcRole.ColleagueA,
                false, null, null, null, null);
            npc.SetProfile(profile);
            npcObject.SetActive(true);
            gate.ConfigureScene(zone, barrier, null, playerObject.transform, null, null, npc, null);

            door.Interact(npc.transform);
            Assert.That(door.IsOpen, Is.True);
            npc.transform.position = zoneObject.transform.position;
            typeof(BossOfficeGate).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(gate, null);

            Assert.That(barrier.enabled, Is.True);
            Assert.That(door.IsOpen, Is.False);
            Assert.That(gate.CanOpenFor(playerObject.transform), Is.False);
        }

        private (DoorInteractable, BossOfficeGate) CreateGate(ChapterOneProgress progress)
        {
            doorObject = new GameObject("Executive Door");
            doorObject.AddComponent<BoxCollider>();
            DoorInteractable door = doorObject.AddComponent<DoorInteractable>();
            BossOfficeGate gate = doorObject.AddComponent<BossOfficeGate>();
            gate.Configure(progress, door);
            playerObject = new GameObject("Player");
            return (door, gate);
        }

        private static ChapterOneProgress AtBossVisit()
        {
            var progress = new ChapterOneProgress();
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            progress.TryApply(ChapterOneEvent.BossCallFinished);
            return progress;
        }
    }
}
