using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Story;
using SaksiTerakhir.Vehicles;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SaksiTerakhir.Tests
{
    // No session controller or save path is created: these tests cannot replace
    // the player's autosave and do not open the world scene.
    public sealed class FuelStationChapterDirectorTests
    {
        private readonly List<Object> objects = new List<Object>();
        private ChapterOneDirector chapter;
        private FuelStationChapterDirector station;
        private StoryDialogueController dialogue;
        private VehicleKeyUse vehicle;
        private Transform player;
        private ChapterOneProgress progress;
        private DialogueSequence grandfather;
        private DialogueSequence phone;
        private Scene fixtureScene;

        [SetUp]
        public void SetUp()
        {
            fixtureScene = EditorSceneManager.NewPreviewScene();
            GameObject owner = Create("Isolated station story fixture");
            owner.SetActive(false);
            chapter = owner.AddComponent<ChapterOneDirector>();
            chapter.StartupManagedExternally = true;
            progress = new ChapterOneProgress();
            Set(progress, "stage", ChapterOneStage.AtFuelStation);
            progress.Story.FuelStation.BeginExploration();
            Set(chapter, "progress", progress);
            Set(chapter, "initialized", true);
            dialogue = owner.AddComponent<StoryDialogueController>();
            station = owner.AddComponent<FuelStationChapterDirector>();
            player = Create("Isolated station player").transform;
            vehicle = Create("Isolated station vehicle").AddComponent<VehicleKeyUse>();
            grandfather = Sequence("fixture.grandfather", 19);
            phone = Sequence("fixture.phone", 2, phoneConversation: true);
            Set(station, "chapter", chapter);
            Set(station, "dialogue", dialogue);
            Set(station, "player", player);
            Set(station, "vehicle", vehicle);
            Set(station, "grandfatherConversation", grandfather);
            Set(station, "grandfatherIntroEnd", 7);
            Set(station, "reportLineEnd", 8);
            Set(station, "nightLineEnd", 11);
            Set(station, "morningLineEnd", 12);
            Set(station, "etiquetteLineEnd", 15);
            Set(station, "phoneReadyDialogue", phone);
            owner.SetActive(true);
            // Edit Mode does not guarantee runtime MonoBehaviour lifecycle
            // messages. Establish one subscription explicitly for this fixture.
            Invoke(station, "OnDisable");
            Invoke(station, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            dialogue?.Stop();
            if (station != null) Invoke(station, "OnDisable");
            for (int index = objects.Count - 1; index >= 0; index--)
                if (objects[index] != null) Object.DestroyImmediate(objects[index]);
            objects.Clear();
            if (fixtureScene.IsValid() && fixtureScene.isLoaded) EditorSceneManager.ClosePreviewScene(fixtureScene);
        }

        [Test]
        public void ShowingAReportDoesNotGrantItAndLeavingResumesOnlyUnreadLines()
        {
            Assert.That(station.TryInteract(FuelStationInteraction.Grandfather, player), Is.True);
            for (int line = 0; line < 7; line++) Advance();
            Assert.That(dialogue.LineIndex, Is.EqualTo(7));
            Assert.That(progress.Story.FuelStation.GrandfatherTopic, Is.EqualTo(GrandfatherTopic.None));
            Advance();
            Assert.That(dialogue.IsPlaying, Is.False, "The introduction ends at a continue/leave boundary.");
            Assert.That(progress.Story.FuelStation.GrandfatherNextLine, Is.EqualTo(8));
            Assert.That(progress.Story.FuelStation.ReportHeard, Is.False);

            Assert.That(station.TryInteract(FuelStationInteraction.Grandfather, player), Is.True);
            Assert.That(dialogue.LineIndex, Is.EqualTo(8));
            Assert.That(progress.Story.FuelStation.ReportHeard, Is.False, "A visible but unfinished speech is still unheard.");
            Advance();
            Assert.That(progress.Story.FuelStation.ReportHeard, Is.True);
            Assert.That(progress.Story.FuelStation.NightAdviceHeard, Is.False);
            Assert.That(progress.Story.FuelStation.GrandfatherNextLine, Is.EqualTo(9));
            Assert.That(station.TryInteract(FuelStationInteraction.Grandfather, player), Is.True);
            Assert.That(dialogue.LineIndex, Is.EqualTo(9));
        }

        [Test]
        public void ReadyCallInterruptsAtASentenceBoundaryWithoutCompletingLaterFacts()
        {
            station.TryInteract(FuelStationInteraction.Grandfather, player);
            for (int line = 0; line <= 7; line++) Advance();
            station.TryInteract(FuelStationInteraction.Grandfather, player);
            progress.Story.FuelStation.AdvanceTime(150f, 20f);
            Invoke(station, "AnnounceReady");
            Assert.That(dialogue.ActiveSequence, Is.SameAs(grandfather));
            Assert.That(dialogue.HasQueuedInterruption, Is.True);

            Advance();
            Assert.That(dialogue.ActiveSequence, Is.SameAs(phone));
            Assert.That(progress.Story.FuelStation.ReportHeard, Is.True);
            Assert.That(progress.Story.FuelStation.NightAdviceHeard, Is.False);
            Assert.That(progress.Story.FuelStation.CallDelivered, Is.False);
            Advance();
            Assert.That(progress.Story.FuelStation.CallDelivered, Is.False);
            Advance();

            Assert.That(progress.Story.FuelStation.CallDelivered, Is.True);
            Assert.That(dialogue.ActiveSequence, Is.SameAs(grandfather));
            Assert.That(dialogue.LineIndex, Is.EqualTo(9));
            Assert.That(progress.Story.FuelStation.GrandfatherNextLine, Is.EqualTo(9));
            Assert.That(progress.Story.FuelStation.DirectionsComplete, Is.False);
            var loaded = JsonUtility.FromJson<FuelStationProgress>(JsonUtility.ToJson(progress.Story.FuelStation));
            Assert.That(loaded.ReportHeard && loaded.CallDelivered, Is.True);
            Assert.That(loaded.NightAdviceHeard || loaded.MorningAdviceHeard || loaded.DirectionsComplete, Is.False);
        }

        [Test]
        public void VehicleOccupationAndLeavingTheStationPreventOptionalFacts()
        {
            Set(vehicle, "seatedActor", player);
            Assert.That(station.TryInteract(FuelStationInteraction.Grandfather, player), Is.False);
            Invoke(station, "OnLineCompleted", grandfather, 0);
            Assert.That(progress.Story.FuelStation.GrandfatherNextLine, Is.Zero);
            Set(vehicle, "seatedActor", null);
            Set(progress, "stage", ChapterOneStage.LeavingFuelStation);
            Assert.That(station.CanInteract(FuelStationInteraction.ColleagueB), Is.False);
            Invoke(station, "OnLineCompleted", grandfather, 0);
            Assert.That(progress.Story.FuelStation.GrandfatherNextLine, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FinalConversationChoosesOnlyTheHeardReportBranchBeforeDriving(bool reportHeard)
        {
            progress.Story.FuelStation.AdvanceTime(150f, 20f);
            progress.Story.FuelStation.MarkCallDelivered();
            if (reportHeard)
                progress.Story.FuelStation.RecordGrandfatherLine(0, GrandfatherTopic.GiriawasReport);
            Set(progress, "stage", ChapterOneStage.ReturnToVehicle);
            Set(vehicle, "seatedActor", player);
            DialogueSequence map = Sequence("fixture.departure.map", 1);
            DialogueSequence report = Sequence("fixture.departure.report", 1);
            map.Configure(map.Id, new[] { new DialogueLine("PLAYER", "Read the regional map.") },
                new[] { new ChapterOneStoryCondition(ChapterOneConditionId.GrandfatherReportHeard, false) });
            report.Configure(report.Id, new[] { new DialogueLine("PLAYER", "Share only the report I heard.") },
                new[] { new ChapterOneStoryCondition(ChapterOneConditionId.GrandfatherReportHeard) });
            Set(station, "finalConversations", new[] { map, report });
            bool canDrive = false;
            Assert.That(station.TryBeginDeparture(() => canDrive = true), Is.True);
            Assert.That(dialogue.ActiveSequence, Is.SameAs(reportHeard ? report : map));
            Assert.That(canDrive, Is.False);
            Assert.That(progress.Story.FuelStation.PlanSource, Is.EqualTo(GiriawasPlanSource.Unresolved));
            Advance();
            Assert.That(canDrive, Is.True);
            Assert.That(progress.Story.FuelStation.PlanSource, Is.EqualTo(reportHeard
                ? GiriawasPlanSource.GrandfatherReport : GiriawasPlanSource.RegionalMap));
            Assert.That(progress.Story.FuelStation.NightAdviceHeard || progress.Story.FuelStation.DirectionsComplete, Is.False);
        }

        [Test]
        public void RestoredOccupiedReturnCheckpointDeliversThePendingCallBeforeDeparture()
        {
            progress.Story.FuelStation.AdvanceTime(150f, 20f);
            Set(progress, "stage", ChapterOneStage.ReturnToVehicle);
            Set(vehicle, "seatedActor", player);
            Assert.That(station.TryBeginDeparture(null), Is.False);
            Invoke(station, "Update");
            Assert.That(dialogue.ActiveSequence, Is.SameAs(phone));
            Advance();
            Advance();
            Assert.That(progress.Story.FuelStation.CallDelivered, Is.True);
        }

        private GameObject Create(string name)
        {
            var value = new GameObject(name);
            if (value.scene != fixtureScene) SceneManager.MoveGameObjectToScene(value, fixtureScene);
            objects.Add(value);
            return value;
        }

        private DialogueSequence Sequence(string id, int count, bool phoneConversation = false)
        {
            var value = ScriptableObject.CreateInstance<DialogueSequence>();
            objects.Add(value);
            var lines = new DialogueLine[count];
            for (int index = 0; index < count; index++) lines[index] = new DialogueLine("PLAYER", "Fixture speech " + index);
            value.Configure(id, lines, isPhoneConversation: phoneConversation);
            return value;
        }

        private void Advance() => Invoke(dialogue, "Advance");
        private static void Set(object owner, string field, object value)
            => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        private static void Invoke(object owner, string method, params object[] arguments)
            => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, arguments);
    }
}
