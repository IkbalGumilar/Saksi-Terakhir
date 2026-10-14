using System;
using System.IO;
using NUnit.Framework;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Persistence;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class GameSaveRepositoryTests
    {
        private string directory;
        private string path;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "saksi-game-save-repository-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "checkpoint.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NativeJsonRoundTripRestoresChapterStoryPlayerInventoryAndWorld(bool completedMeal)
        {
            GameSaveSnapshot original = Snapshot(CompletedChapter(completedMeal));
            Assert.That(original.Validate(out string validationError), Is.True, validationError);
            WriteSnapshot(original);

            Assert.That(File.ReadAllText(path), Does.Not.Contain("Archive note"));
            Assert.That(GameSaveRepository.TryLoadLatest(path, out GameSaveSnapshot loaded, out string error),
                Is.True, error);
            Assert.That(error, Is.Empty);
            Assert.That(loaded.version, Is.EqualTo(1));
            Assert.That(loaded.runId, Is.EqualTo("e1e6da64-5500-4cc2-b7bd-4ca8679b4995"));
            Assert.That(loaded.scenePath, Is.EqualTo("Assets/_Project/Scenes/Regional Archive Office.unity"));
            Assert.That(loaded.savedUtc, Is.EqualTo("2026-10-04T12:00:00Z"));
            Assert.That(loaded.player.position, Is.EqualTo(new Vector3(13.5f, -2.25f, 41.75f)));
            Assert.That(loaded.player.rotation, Is.EqualTo(new Quaternion(0, 1, 0, 0)));
            Assert.That(loaded.cameraPitch, Is.EqualTo(-23.5f));

            ChapterOneProgress chapter = loaded.chapter;
            Assert.That(chapter.Version, Is.EqualTo(1));
            Assert.That(chapter.Stage, Is.EqualTo(ChapterOneStage.Complete));
            Assert.That(chapter.FirstColleagueId, Is.EqualTo(completedMeal ? "NPC-003" : "NPC-004"));
            Assert.That(chapter.LastColleagueId, Is.EqualTo(completedMeal ? "NPC-004" : "NPC-003"));
            Assert.That(chapter.LastColleagueIntroSelectionKey, Is.EqualTo("archive-opportunity"));
            Assert.That(chapter.RakaMet, Is.True);
            Assert.That(chapter.SintaMet, Is.True);
            Assert.That(chapter.RakaArrived, Is.True);
            Assert.That(chapter.SintaArrived, Is.True);
            Assert.That(chapter.LastColleagueIntroComplete, Is.True);
            Assert.That(chapter.WalkDialogueComplete, Is.True);
            Assert.That(chapter.BossMeetingSide, Is.EqualTo(1));
            Assert.That(chapter.MeetingAssembled, Is.True);
            Assert.That(chapter.PlayerSeated, Is.True);
            Assert.That(chapter.HasCarKey, Is.True);
            Assert.That(chapter.Story.Breakfast, Is.EqualTo(completedMeal ? BreakfastChoice.Paid : BreakfastChoice.Owed));
            Assert.That(chapter.Story.WrongRouteQuest,
                Is.EqualTo(completedMeal ? StoryQuestStatus.Completed : StoryQuestStatus.Skipped));
            Assert.That(chapter.Story.Agenda, Is.EqualTo(completedMeal ? AgendaChoice.Written : AgendaChoice.Skipped));
            Assert.That(chapter.Story.DeskPreparationQuest, Is.EqualTo(StoryQuestStatus.Completed));
            Assert.That(chapter.Story.SuppliesQuest, Is.EqualTo(StoryQuestStatus.Completed));
            Assert.That(chapter.Story.SharedMealQuest,
                Is.EqualTo(completedMeal ? StoryQuestStatus.Completed : StoryQuestStatus.Skipped));
            Assert.That(chapter.Story.PurchasedSupplyIds, Is.EqualTo(new[] { "water", "medicine" }));
            Assert.That(chapter.Story.TravelReceiptSubmitted, Is.True);
            Assert.That(chapter.Story.AtmVisited, Is.EqualTo(completedMeal));
            Assert.That(chapter.Story.TravelCashPerPerson, Is.EqualTo(completedMeal ? 2250000 : 0));
            Assert.That(chapter.Story.NasiUdukReceived, Is.EqualTo(!completedMeal));
            Assert.That(chapter.Story.DepartureDriver,
                Is.EqualTo(completedMeal ? ChapterOneDepartureDriver.ColleagueA : ChapterOneDepartureDriver.Player));
            Assert.That(chapter.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.False);
            Assert.That(chapter.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted), Is.False);

            Assert.That(loaded.inventory.columns, Is.EqualTo(4));
            Assert.That(loaded.inventory.rows, Is.EqualTo(5));
            Assert.That(loaded.inventory.items.Length, Is.EqualTo(2));
            Assert.That(loaded.inventory.items[0].id, Is.EqualTo("car-key"));
            Assert.That(loaded.inventory.items[0].displayName, Is.EqualTo("Car key"));
            Assert.That(loaded.inventory.items[0].description, Is.EqualTo("Office handoff"));
            Assert.That(loaded.inventory.items[0].iconName, Is.EqualTo("car-key-icon"));
            Assert.That(loaded.inventory.items[1].id, Is.EqualTo("water"));
            Assert.That(loaded.inventory.documents.Length, Is.EqualTo(2));
            Assert.That(loaded.inventory.documents[0].id, Is.EqualTo("agenda"));
            Assert.That(loaded.inventory.documents[1].id, Is.EqualTo("archive-note"));
            Assert.That(loaded.inventory.documents[1].displayName, Is.EqualTo("Archive note"));
            Assert.That(loaded.inventory.documents[1].description, Is.EqualTo("Wanaseta inscription"));
            InventoryState restoredInventory = loaded.inventory.CreateState();
            Assert.That(restoredInventory.Capacity, Is.EqualTo(20));
            Assert.That(restoredInventory.Items[0].Type, Is.EqualTo(InventoryEntryType.Item));
            Assert.That(restoredInventory.Items[1].Id, Is.EqualTo("water"));
            Assert.That(restoredInventory.Documents[1].Type, Is.EqualTo(InventoryEntryType.Document));
            Assert.That(restoredInventory.Documents[1].Description, Is.EqualTo("Wanaseta inscription"));

            Assert.That(loaded.npcs.Length, Is.EqualTo(2));
            Assert.That(loaded.npcs[0].id, Is.EqualTo("NPC-003"));
            Assert.That(loaded.npcs[0].pose.position, Is.EqualTo(new Vector3(10, 2, -5)));
            Assert.That(loaded.npcs[0].seated, Is.True);
            Assert.That(loaded.npcs[1].id, Is.EqualTo("NPC-004"));
            Assert.That(loaded.npcs[1].pose.rotation, Is.EqualTo(new Quaternion(0, 0, 1, 0)));
            Assert.That(loaded.npcs[1].seated, Is.False);
            Assert.That(loaded.doors.Length, Is.EqualTo(2));
            Assert.That(loaded.doors[0].path, Is.EqualTo("Office/ReceptionDoor"));
            Assert.That(loaded.doors[0].open, Is.True);
            Assert.That(loaded.doors[1].path, Is.EqualTo("Office/ArchiveDoor"));
            Assert.That(loaded.doors[1].open, Is.False);
        }

        [Test]
        public void ReloadedActivePreparationCanContinueThroughNormalStoryEvents()
        {
            var chapter = new ChapterOneProgress();
            Apply(chapter, ChapterOneStoryEvent.BreakfastPaid);
            Apply(chapter, ChapterOneStoryEvent.WrongRouteStarted);
            AdvanceToCarKey(chapter, "NPC-003", "NPC-004");
            Apply(chapter, ChapterOneStoryEvent.AgendaWritten);
            Apply(chapter, ChapterOneStoryEvent.DeskPreparationStarted);
            Apply(chapter, ChapterOneStoryEvent.SuppliesStarted);
            Apply(chapter, ChapterOneStoryEvent.SharedMealStarted);
            Assert.That(chapter.TryRecordTravelPurchase("water"), Is.True);
            WriteSnapshot(Snapshot(chapter));

            Assert.That(GameSaveRepository.TryLoadLatest(path, out GameSaveSnapshot loaded, out string error),
                Is.True, error);
            Assert.That(loaded.chapter.Stage, Is.EqualTo(ChapterOneStage.CollectCarKey));
            Assert.That(loaded.chapter.Story.WrongRouteQuest, Is.EqualTo(StoryQuestStatus.Active));
            Assert.That(loaded.chapter.Story.DeskPreparationQuest, Is.EqualTo(StoryQuestStatus.Active));
            Assert.That(loaded.chapter.Story.SuppliesQuest, Is.EqualTo(StoryQuestStatus.Active));
            Assert.That(loaded.chapter.Story.SharedMealQuest, Is.EqualTo(StoryQuestStatus.Active));
            Assert.That(loaded.chapter.Story.PurchasedSupplyIds, Is.EqualTo(new[] { "water" }));
            Apply(loaded.chapter, ChapterOneStoryEvent.WrongRouteCompleted);
            Apply(loaded.chapter, ChapterOneStoryEvent.DeskPreparationCompleted);
            Apply(loaded.chapter, ChapterOneStoryEvent.SuppliesCompleted);
            Apply(loaded.chapter, ChapterOneStoryEvent.SharedMealCompleted);
            Assert.That(loaded.chapter.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            Assert.That(loaded.chapter.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(loaded.chapter.Story.SharedMealQuest, Is.EqualTo(StoryQuestStatus.Completed));
        }

        [Test]
        public void ProtectedCheckpointRestoresCapturedSparseSlotsAndHeldPairWithoutDuplicatingInstances()
        {
            var inventory = new InventoryState(3, 3);
            var firstBattery = new InventoryEntry("battery", "First battery", InventoryEntryType.Item,
                instanceId: "battery:first");
            var secondBattery = new InventoryEntry("battery", "Second battery", InventoryEntryType.Item,
                instanceId: "battery:second");
            var key = new InventoryEntry(CarKeyInventory.CarKeyId, "Car key", InventoryEntryType.Item,
                instanceId: "reception:key");
            var document = new InventoryEntry("archive-note", "Archive note", InventoryEntryType.Document,
                instanceId: "briefing:archive-note");
            Assert.That(inventory.TryAdd(firstBattery), Is.True);
            Assert.That(inventory.TryAdd(secondBattery), Is.True);
            Assert.That(inventory.TryAdd(key), Is.True);
            Assert.That(inventory.TryAdd(document), Is.True);
            Assert.That(inventory.TryMoveToSlot(firstBattery, 8), Is.True);
            Assert.That(inventory.TryMoveToSlot(secondBattery, 5), Is.True);
            Assert.That(inventory.TryMoveToSlot(key, 0), Is.True);
            Assert.That(inventory.TryHold(secondBattery), Is.True);
            Assert.That(inventory.TryHold(firstBattery), Is.True);
            GameSaveSnapshot original = Snapshot(CompletedChapter(false));
            original.inventory = SavedInventory.Capture(inventory);
            WriteSnapshot(original);

            Assert.That(GameSaveRepository.TryLoadLatest(path, out GameSaveSnapshot loaded, out string error),
                Is.True, error);
            InventoryState restored = loaded.inventory.CreateState(resolveDefinition: _ => null);
            Assert.That(restored.Items.Count, Is.EqualTo(3));
            Assert.That(restored.Documents.Count, Is.EqualTo(1));
            Assert.That(restored.GetItemAtSlot(0).InstanceId, Is.EqualTo("reception:key"));
            Assert.That(restored.GetItemAtSlot(5).InstanceId, Is.EqualTo("battery:second"));
            Assert.That(restored.GetItemAtSlot(8).InstanceId, Is.EqualTo("battery:first"));
            Assert.That(restored.GetItemAtSlot(1), Is.Null);
            Assert.That(restored.GetItemAtSlot(2), Is.Null);
            Assert.That(restored.HeldItems.Count, Is.EqualTo(2));
            Assert.That(restored.HeldItems[0], Is.SameAs(restored.GetItemAtSlot(5)));
            Assert.That(restored.HeldItems[1], Is.SameAs(restored.GetItemAtSlot(8)));
            Assert.That(restored.Documents[0].InstanceId, Is.EqualTo("briefing:archive-note"));
            Assert.That(restored.TryAdd(new InventoryEntry("battery", "Duplicate", InventoryEntryType.Item,
                instanceId: "battery:first")), Is.False);
            Assert.That(restored.Items.Count, Is.EqualTo(3));
        }

        [TestCase("version")]
        [TestCase("run-id")]
        [TestCase("player")]
        [TestCase("chapter")]
        [TestCase("story")]
        [TestCase("inventory")]
        [TestCase("npcs")]
        [TestCase("npc-pose")]
        [TestCase("doors")]
        public void AuthenticatedPayloadWithInvalidSchemaIsRejected(string invalidField)
        {
            GameSaveSnapshot snapshot = Snapshot();
            Invalidate(snapshot, invalidField);
            WriteSnapshot(snapshot);
            Assert.That(ProtectedJsonSaveStore.TryLoad(path, out _, out string transportError), Is.True, transportError);

            Assert.That(GameSaveRepository.TryLoadLatest(path, out GameSaveSnapshot loaded, out string error), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [TestCase("")]
        [TestCase("{oops")]
        [TestCase("{}")]
        [TestCase("[]")]
        public void AuthenticatedEmptyMalformedOrNonSnapshotPayloadIsRejected(string payload)
        {
            ProtectedJsonSaveStore.Save(path, payload);
            Assert.That(ProtectedJsonSaveStore.TryLoad(path, out string transported, out string transportError),
                Is.True, transportError);
            Assert.That(transported, Is.EqualTo(payload));

            Assert.That(GameSaveRepository.TryLoadLatest(path, out GameSaveSnapshot loaded, out string error), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [TestCase("")]
        [TestCase(",\"player\":null")]
        [TestCase(",\"player\":{}")]
        [TestCase(",\"player\":{\"position\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1}}")]
        [TestCase(",\"player\":{\"schemaVersion\":999,\"position\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1}}")]
        public void AuthenticatedPayloadWithoutCapturedPlayerPoseIsRejected(string playerField)
        {
            string payload = "{\"version\":1,\"runId\":\"e1e6da64-5500-4cc2-b7bd-4ca8679b4995\","
                + "\"scenePath\":\"Assets/_Project/Scenes/Regional Archive Office.unity\","
                + "\"savedUtc\":\"2026-10-04T12:00:00Z\",\"chapter\":{\"version\":1,\"stage\":0},"
                + "\"cameraPitch\":0,\"npcs\":[],\"doors\":[]" + playerField + "}";
            ProtectedJsonSaveStore.Save(path, payload);

            Assert.That(GameSaveRepository.TryLoadLatest(path, out GameSaveSnapshot loaded, out string error), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void ExplicitlyCapturedPlayerAtWorldOriginRemainsValid()
        {
            GameSaveSnapshot original = Snapshot();
            original.player = new SavedPose(Vector3.zero, Quaternion.identity);
            WriteSnapshot(original);

            Assert.That(GameSaveRepository.TryLoadLatest(path, out GameSaveSnapshot loaded, out string error), Is.True, error);
            Assert.That(error, Is.Empty);
            Assert.That(loaded.player.position, Is.EqualTo(Vector3.zero));
            Assert.That(loaded.player.rotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void ValidPrimaryIsPreferredOverAnOlderValidBackup()
        {
            WriteSnapshot(Snapshot());
            GameSaveSnapshot newer = Snapshot();
            Assert.That(newer.chapter.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.True);
            newer.player.position = new Vector3(30, 0, 5);
            WriteSnapshot(newer);

            Assert.That(GameSaveRepository.TryLoadLatest(path, out GameSaveSnapshot loaded, out string error),
                Is.True, error);
            Assert.That(error, Is.Empty);
            Assert.That(loaded.chapter.Stage, Is.EqualTo(ChapterOneStage.AnswerBossCall));
            Assert.That(loaded.player.position, Is.EqualTo(new Vector3(30, 0, 5)));
        }

        [Test]
        public void ValidPrimaryDoesNotDependOnTheBackupPassingSchemaValidation()
        {
            WriteSnapshot(Snapshot());
            ProtectedJsonSaveStore.Save(path + ".bak", "{}");

            Assert.That(GameSaveRepository.TryLoadLatest(path, out GameSaveSnapshot loaded, out string error),
                Is.True, error);
            Assert.That(error, Is.Empty);
            Assert.That(loaded.player.position, Is.EqualTo(new Vector3(13.5f, -2.25f, 41.75f)));
        }

        [TestCase("version")]
        [TestCase("story")]
        [TestCase("inventory")]
        public void SemanticInvalidPrimaryFallsBackToThePreviousValidCheckpoint(string invalidField)
        {
            WriteSnapshot(Snapshot());
            GameSaveSnapshot invalid = Snapshot();
            Invalidate(invalid, invalidField);
            WriteSnapshot(invalid);
            Assert.That(ProtectedJsonSaveStore.TryLoad(path, out _, out string transportError), Is.True, transportError);

            Assert.That(GameSaveRepository.TryLoadLatest(path, out GameSaveSnapshot loaded, out string warning),
                Is.True, warning);
            Assert.That(warning, Does.Contain("backup"));
            Assert.That(loaded.version, Is.EqualTo(1));
            Assert.That(loaded.chapter.Stage, Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
            Assert.That(loaded.chapter.Story.TravelCashPerPerson, Is.EqualTo(0));
            Assert.That(loaded.inventory.columns, Is.EqualTo(4));
            Assert.That(loaded.inventory.items[1].id, Is.EqualTo("water"));
            Assert.That(loaded.player.position, Is.EqualTo(new Vector3(13.5f, -2.25f, 41.75f)));
        }

        [Test]
        public void AuthenticatedMalformedPrimaryFallsBackToAValidBackup()
        {
            WriteSnapshot(Snapshot());
            ProtectedJsonSaveStore.Save(path, "{oops");

            Assert.That(GameSaveRepository.TryLoadLatest(path, out GameSaveSnapshot loaded, out string warning),
                Is.True, warning);
            Assert.That(warning, Does.Contain("backup"));
            Assert.That(loaded.runId, Is.EqualTo("e1e6da64-5500-4cc2-b7bd-4ca8679b4995"));
        }

        [Test]
        public void InvalidAuthenticatedPrimaryAndBackupCannotBecomeASnapshot()
        {
            GameSaveSnapshot invalid = Snapshot();
            Invalidate(invalid, "inventory");
            WriteSnapshot(invalid);
            ProtectedJsonSaveStore.Save(path + ".bak", "{}");

            Assert.That(GameSaveRepository.TryLoadLatest(path, out GameSaveSnapshot loaded, out string error), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void MissingCheckpointAndBackupReturnAnEmptyResult()
        {
            Assert.That(GameSaveRepository.TryLoadLatest(path, out GameSaveSnapshot loaded, out string error), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(error, Is.Empty);
        }

        private void WriteSnapshot(GameSaveSnapshot snapshot)
        {
            ProtectedJsonSaveStore.Save(path, JsonUtility.ToJson(snapshot));
        }

        private static void Invalidate(GameSaveSnapshot snapshot, string field)
        {
            switch (field)
            {
                case "version": snapshot.version = 999; break;
                case "run-id": snapshot.runId = "not-a-run-id"; break;
                case "player": snapshot.player = null; break;
                case "chapter":
                    snapshot.chapter = JsonUtility.FromJson<ChapterOneProgress>("{\"version\":1,\"stage\":999}");
                    break;
                case "story":
                    snapshot.chapter = JsonUtility.FromJson<ChapterOneProgress>(
                        "{\"version\":1,\"stage\":0,\"story\":{\"travelCashPerPerson\":2250000}}");
                    break;
                case "inventory": snapshot.inventory.columns = 0; break;
                case "npcs": snapshot.npcs[1].id = "NPC-003"; break;
                case "npc-pose": snapshot.npcs[0].pose = null; break;
                case "doors": snapshot.doors[1].path = "Office/ReceptionDoor"; break;
                default: throw new ArgumentException("Unknown invalid snapshot field.", nameof(field));
            }
        }

        private static GameSaveSnapshot Snapshot(ChapterOneProgress chapter = null)
        {
            return new GameSaveSnapshot
            {
                runId = "e1e6da64-5500-4cc2-b7bd-4ca8679b4995",
                scenePath = "Assets/_Project/Scenes/Regional Archive Office.unity",
                savedUtc = "2026-10-04T12:00:00Z",
                chapter = chapter ?? new ChapterOneProgress(),
                player = new SavedPose(new Vector3(13.5f, -2.25f, 41.75f), new Quaternion(0, 1, 0, 0)),
                cameraPitch = -23.5f,
                inventory = new SavedInventory
                {
                    columns = 4, rows = 5,
                    items = new[]
                    {
                        Entry("car-key", "Car key", "Office handoff", "car-key-icon"),
                        Entry("water", "Water", "Travel supplies", "water-icon")
                    },
                    documents = new[]
                    {
                        Entry("agenda", "Agenda", "24 August", "agenda-icon"),
                        Entry("archive-note", "Archive note", "Wanaseta inscription", "archive-icon")
                    }
                },
                npcs = new[]
                {
                    new SavedNpcPose { id = "NPC-003", pose = new SavedPose(new Vector3(10, 2, -5), Quaternion.identity), seated = true },
                    new SavedNpcPose { id = "NPC-004", pose = new SavedPose(new Vector3(5, 3, 2), new Quaternion(0, 0, 1, 0)) }
                },
                doors = new[]
                {
                    new SavedDoor { path = "Office/ReceptionDoor", open = true },
                    new SavedDoor { path = "Office/ArchiveDoor", open = false }
                }
            };
        }

        private static SavedInventoryEntry Entry(string id, string name, string description, string icon)
        {
            return new SavedInventoryEntry { id = id, displayName = name, description = description, iconName = icon };
        }

        private static ChapterOneProgress CompletedChapter(bool completedMeal)
        {
            var chapter = new ChapterOneProgress();
            Apply(chapter, completedMeal ? ChapterOneStoryEvent.BreakfastPaid : ChapterOneStoryEvent.BreakfastOwed);
            if (completedMeal)
            {
                Apply(chapter, ChapterOneStoryEvent.WrongRouteStarted);
                Apply(chapter, ChapterOneStoryEvent.WrongRouteCompleted);
            }
            else Apply(chapter, ChapterOneStoryEvent.WrongRouteSkipped);
            AdvanceToCarKey(chapter, completedMeal ? "NPC-003" : "NPC-004", completedMeal ? "NPC-004" : "NPC-003");
            Apply(chapter, completedMeal ? ChapterOneStoryEvent.AgendaWritten : ChapterOneStoryEvent.AgendaSkipped);
            Apply(chapter, ChapterOneStoryEvent.DeskPreparationStarted);
            Apply(chapter, ChapterOneStoryEvent.DeskPreparationCompleted);
            Apply(chapter, ChapterOneStoryEvent.SuppliesStarted);
            Assert.That(chapter.TryRecordTravelPurchase("water"), Is.True);
            Assert.That(chapter.TryRecordTravelPurchase("medicine"), Is.True);
            Apply(chapter, ChapterOneStoryEvent.SuppliesCompleted);
            Apply(chapter, ChapterOneStoryEvent.TravelReceiptSubmitted);
            if (completedMeal)
            {
                Apply(chapter, ChapterOneStoryEvent.SharedMealStarted);
                Apply(chapter, ChapterOneStoryEvent.SharedMealCompleted);
            }
            else Apply(chapter, ChapterOneStoryEvent.SharedMealSkipped);
            Assert.That(chapter.TryApply(ChapterOneEvent.CarKeyReceived), Is.True);
            if (completedMeal)
            {
                Apply(chapter, ChapterOneStoryEvent.AtmVisited);
                Apply(chapter, ChapterOneStoryEvent.TravelCashDistributed);
            }
            else Apply(chapter, ChapterOneStoryEvent.NasiUdukReceived);
            Apply(chapter, ChapterOneStoryEvent.DepartureStarted);
            Assert.That(chapter.TryApply(ChapterOneEvent.VehicleReached), Is.True);
            return chapter;
        }

        private static void AdvanceToCarKey(ChapterOneProgress chapter, string first, string last)
        {
            Assert.That(chapter.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.True);
            Assert.That(chapter.TryApply(ChapterOneEvent.BossCallFinished), Is.True);
            Assert.That(chapter.TryApply(ChapterOneEvent.FirstBriefingFinished), Is.True);
            Assert.That(chapter.TryApply(ChapterOneEvent.ColleagueDialogueFinished, first), Is.True);
            Assert.That(chapter.TryApply(ChapterOneEvent.ColleagueArrived, first), Is.True);
            Assert.That(chapter.TryApply(ChapterOneEvent.BossMovedToMeeting, "left"), Is.True);
            Assert.That(chapter.TryApply(ChapterOneEvent.ColleagueDialogueStarted, last, "archive-opportunity"), Is.True);
            Assert.That(chapter.TryApply(ChapterOneEvent.LastColleagueIntroFinished, last), Is.True);
            Assert.That(chapter.TryApply(ChapterOneEvent.WalkDialogueFinished, last), Is.True);
            Assert.That(chapter.TryApply(ChapterOneEvent.ColleagueArrived, last), Is.True);
            Assert.That(chapter.TryApply(ChapterOneEvent.MeetingAssembled), Is.True);
            Assert.That(chapter.TryApply(ChapterOneEvent.PlayerSeated), Is.True);
            Assert.That(chapter.TryApply(ChapterOneEvent.FinalBriefingFinished), Is.True);
        }

        private static void Apply(ChapterOneProgress chapter, ChapterOneStoryEvent storyEvent)
        {
            Assert.That(chapter.TryApplyStoryEvent(storyEvent), Is.True, storyEvent.ToString());
        }
    }
}
