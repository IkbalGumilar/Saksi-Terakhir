using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SaksiTerakhir.Tests
{
    public sealed class ChapterOneOfficePlayTests
    {
        private string savePath;

        [Test]
        public void FlagRoomApproachIsReachableFromBothColleaguesAndBoss()
        {
            Transform story = GameObject.Find("Chapter One Story").transform;
            OfficeNpcAgent[] actors = UnityEngine.Object.FindObjectsByType<OfficeNpcAgent>(
                FindObjectsSortMode.None);
            OfficeNpcAgent raka = actors.Single(actor => actor.Profile.Id == "NPC-003");
            OfficeNpcAgent sinta = actors.Single(actor => actor.Profile.Id == "NPC-004");
            var query = new NavMeshQueryFilter
            {
                agentTypeID = raka.Navigation.agentTypeID,
                areaMask = raka.Navigation.areaMask
            };
            Vector3 destination = story.Find("Meeting Approach").position;
            Assert.That(NavMesh.SamplePosition(destination, out NavMeshHit target, 0.4f, query),
                Is.True, "The meeting approach must be on the NPC NavMesh.");
            foreach (Vector3 origin in new[]
            {
                raka.transform.position,
                sinta.transform.position,
                story.Find("Boss Walk Start").position
            })
            {
                Assert.That(NavMesh.SamplePosition(origin, out NavMeshHit start, 1.2f, query),
                    Is.True, $"No NavMesh near {origin}.");
                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(start.position, target.position, query, path),
                    Is.True, $"Could not calculate path from {origin}.");
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete),
                    $"Path to the meeting sofa is incomplete from {origin}.");
            }
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator CinematicPlayerCanClimbTheUpperOfficeStairs()
        {
            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            StoryPlayerCinematicMover mover = player.GetComponent<StoryPlayerCinematicMover>();
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = new Vector3(5.04f, 8.55f, -26.49f);
            controller.enabled = true;
            yield return null;
            Assert.That(mover.BeginMoveTo(new Vector3(5.81f, 13.25f, -26.54f),
                2.0f, 0.3f), Is.True);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline && player.transform.position.y < 12.8f)
                yield return null;
            Assert.That(player.transform.position.y, Is.GreaterThan(12.8f),
                $"Cinematic movement stopped below the upper landing at {player.transform.position}; "
                + DescribeMover(mover, player.transform));
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator CinematicPlayerCanLeaveUpperStairLanding()
        {
            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            StoryPlayerCinematicMover mover = player.GetComponent<StoryPlayerCinematicMover>();
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = new Vector3(4.68f, 10.92f, -22.22f);
            controller.enabled = true;
            yield return null;
            Vector3 target = new Vector3(5.81f, 13.25f, -26.54f);
            Assert.That(mover.BeginMoveTo(target, 2.0f, 0.3f), Is.True,
                "The upper-stair player position should have a complete path to the colleague.");
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline && player.transform.position.y < 12.8f)
                yield return null;
            Assert.That(player.transform.position.y, Is.GreaterThan(12.8f),
                $"Cinematic player remained on the stair landing at {player.transform.position}.");
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator CinematicPlayerCanFollowAcrossUpperStairLanding()
        {
            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            StoryPlayerCinematicMover mover = player.GetComponent<StoryPlayerCinematicMover>();
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = new Vector3(4.68f, 10.92f, -22.22f);
            controller.enabled = true;
            var target = new GameObject("Upper stair follow target");
            target.transform.position = new Vector3(5.81f, 13.25f, -26.54f);
            target.transform.rotation = Quaternion.LookRotation(
                new Vector3(0.415f, 0f, -0.91f));
            yield return null;
            Assert.That(mover.BeginFollow(target.transform, 2.2f, 2.0f), Is.True);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline && player.transform.position.y < 12.8f)
                yield return null;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var path = (NavMeshPath)typeof(StoryPlayerCinematicMover)
                .GetField("currentPath", flags).GetValue(mover);
            int corner = (int)typeof(StoryPlayerCinematicMover)
                .GetField("nextCorner", flags).GetValue(mover);
            bool direct = (bool)typeof(StoryPlayerCinematicMover)
                .GetField("followingDirectTarget", flags).GetValue(mover);
            Assert.That(player.transform.position.y, Is.GreaterThan(12.8f),
                $"Follower remained on the stair landing at {player.transform.position}; "
                + $"direct={direct}, corner={corner}, path={path?.status}, "
                + $"corners={string.Join(";", path?.corners.Select(p => p.ToString("F2")) ?? Array.Empty<string>())}, "
                + $"velocity={player.CinematicVelocity:F2}, grounded={controller.isGrounded}.");
            UnityEngine.Object.Destroy(target);
        }

        [UnityTest]
        [Timeout(35000)]
        public IEnumerator PlayerCanWalkThroughOpenBossDoorToMeetingSofa()
        {
            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            StoryPlayerCinematicMover mover = player.GetComponent<StoryPlayerCinematicMover>();
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = new Vector3(0.37f, 15.86f, -30.76f);
            controller.enabled = true;
            DoorInteractable door = UnityEngine.Object.FindObjectsByType<DoorInteractable>(
                FindObjectsSortMode.None).Single(candidate =>
                candidate.name == "Office_ArchiveHQ_DoorLeaf_12");
            door.GetComponent<BossOfficeGate>().enabled = false;
            GameObject.Find("Boss Player Barrier").GetComponent<Collider>().enabled = false;
            door.ForceOpen();
            yield return null;
            Transform approach = GameObject.Find("Chapter One Story").transform.Find("Meeting Approach");
            Assert.That(mover.BeginMoveTo(approach, 1.6f, 0.3f), Is.True);
            float deadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < deadline && mover.IsActive)
                yield return null;
            Assert.That(mover.IsActive, Is.False,
                $"Player stopped at {player.transform.position} before entering the office; "
                + DescribeMover(mover, player.transform));
            Assert.That(Vector3.Distance(player.transform.position, approach.position),
                Is.LessThan(0.7f));
        }

        [UnityTest]
        public IEnumerator LoadingColleagueSearchRestoresExecutiveRoomRestriction()
        {
            var snapshot = new ChapterOneProgress();
            Assert.That(snapshot.TryApply(ChapterOneEvent.RooftopDialogueFinished), Is.True);
            Assert.That(snapshot.TryApply(ChapterOneEvent.BossCallFinished), Is.True);
            Assert.That(snapshot.TryApply(ChapterOneEvent.FirstBriefingFinished), Is.True);
            ChapterOneSaveStore.Save(snapshot, savePath);

            yield return SceneManager.LoadSceneAsync("Regional Archive Office", LoadSceneMode.Single);
            yield return null;

            ChapterOneDirector chapter = UnityEngine.Object.FindFirstObjectByType<ChapterOneDirector>();
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
            OfficeNpcDirector npcDirector = UnityEngine.Object.FindFirstObjectByType<OfficeNpcDirector>();
            NpcActivityPoint executivePoint = npcDirector.GetComponentsInChildren<NpcActivityPoint>()
                .First(point => point.name.StartsWith("executive_talk", StringComparison.Ordinal));
            Assert.That(npcDirector.IsStoryRestricted(executivePoint), Is.True,
                "A loaded chapter must keep background workers out of the boss office.");
        }

        [UnityTest]
        public IEnumerator BossDoorDoesNotScoldAboutUnassignedColleagues()
        {
            ChapterOneDirector chapter = UnityEngine.Object.FindFirstObjectByType<ChapterOneDirector>();
            StoryDialogueController dialogue = UnityEngine.Object.FindFirstObjectByType<StoryDialogueController>();
            Transform player = UnityEngine.Object.FindFirstObjectByType<PlayerController>().transform;
            BossOfficeGate gate = UnityEngine.Object.FindFirstObjectByType<BossOfficeGate>();

            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
            gate.OnBlocked(player);
            Assert.That(dialogue.IsPlaying, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BossDoorWaitsUntilColleagueIsFullyInsideRoom()
        {
            ChapterOneDirector chapter = UnityEngine.Object.FindFirstObjectByType<ChapterOneDirector>();
            BossOfficeGate gate = UnityEngine.Object.FindFirstObjectByType<BossOfficeGate>();
            DoorInteractable door = gate.GetComponent<DoorInteractable>();
            OfficeNpcAgent raka = UnityEngine.Object.FindObjectsByType<OfficeNpcAgent>(
                FindObjectsSortMode.None).Single(agent => agent.Profile.Id == "NPC-003");
            var progress = new ChapterOneProgress();
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            progress.TryApply(ChapterOneEvent.BossCallFinished);
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);
            progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003");
            chapter.RestoreProgress(progress);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            CharacterController body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = new Vector3(0.5f, 15.86f, -31f);
            body.enabled = true;
            gate.SetPlayerInside(false);
            raka.HoldForStory();
            raka.Navigation.enabled = false;
            Bounds room = GameObject.Find("Boss Room Zone").GetComponent<Collider>().bounds;
            raka.transform.position = new Vector3(room.max.x - 0.05f, 15.84f, -31f);
            Physics.SyncTransforms();
            Assert.That(room.Contains(raka.transform.position), Is.True);
            Assert.That(gate.IsFullyInsideRoom(raka), Is.False);
            door.ForceOpen();

            for (int frame = 0; frame < 3; frame++) yield return null;
            Assert.That(gate.PlayerInside, Is.False);
            Assert.That(door.IsOpen, Is.True,
                "The door must stay open while the colleague's capsule straddles the threshold.");

            raka.transform.position = new Vector3(room.max.x - 0.6f, 15.84f, -31f);
            Physics.SyncTransforms();
            Assert.That(gate.IsFullyInsideRoom(raka), Is.True);
            for (int frame = 0; frame < 3; frame++) yield return null;
            Assert.That(door.IsOpen, Is.False);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savePath = Path.Combine(Application.temporaryCachePath,
                "saksi-chapter-play-test-" + Guid.NewGuid() + ".json");
            ChapterOneDirector.SessionSavePathOverride = savePath;
            yield return SceneManager.LoadSceneAsync("Regional Archive Office", LoadSceneMode.Single);
            MonoBehaviour[] eventSystems = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsSortMode.None).Where(component => component.GetType().Name == "EventSystem").ToArray();
            for (int index = 1; index < eventSystems.Length; index++) eventSystems[index].enabled = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ChapterOneDirector.SessionSavePathOverride = null;
            if (File.Exists(savePath)) File.Delete(savePath);
            if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RooftopCallBossGateAndFirstColleagueWorkInTheSavedOffice()
        {
            ChapterOneDirector chapter = UnityEngine.Object.FindFirstObjectByType<ChapterOneDirector>();
            StoryDialogueController dialogue = UnityEngine.Object.FindFirstObjectByType<StoryDialogueController>();
            OfficeNpcAgent[] cast = UnityEngine.Object.FindObjectsByType<OfficeNpcAgent>(FindObjectsSortMode.None);
            OfficeNpcAgent Actor(string id) => cast.Single(agent => agent.Profile.Id == id);
            PlayerController movement = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            PlayerCameraController camera = movement.GetComponent<PlayerCameraController>();
            Transform player = movement.transform;
            BossOfficeGate gate = UnityEngine.Object.FindFirstObjectByType<BossOfficeGate>();

            Transform saveArea = GameObject.Find("Save Area").transform;
            GameObject questTracker = saveArea.Find("Chapter One Quest Tracker").gameObject;
            GameObject storyPanel = saveArea.Find("Chapter One Dialogue Panel").gameObject;

            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.MeetRooftopWorkers));
            Assert.That(questTracker.activeSelf, Is.True);
            Assert.That(questTracker.transform.Find("Quest Title").GetComponent<TMP_Text>().text,
                Is.Not.Empty);
            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-021").GetComponent<NpcInteractable>(), player), Is.True);
            Assert.That(storyPanel.activeSelf, Is.True);
            TMP_Text line = storyPanel.transform.Find("Dialogue Line").GetComponent<TMP_Text>();
            Assert.That(line.text, Is.Not.Empty);
            Assert.That(line.maxVisibleCharacters, Is.EqualTo(0),
                "The first line should begin hidden and reveal one character at a time.");
            Assert.That(movement.StoryDialogueMovementLocked, Is.True);
            Assert.That(camera.StoryDialogueLookLocked, Is.True);
            Assert.That(camera.StoryFocusTarget, Is.EqualTo(Actor("NPC-021").transform));
            int firstLineIndex = dialogue.LineIndex;
            Assert.That(dialogue.TryConsumeInteract(), Is.True);
            Assert.That(dialogue.LineIndex, Is.EqualTo(firstLineIndex),
                "E is consumed during dialogue but must not skip the automatic conversation.");
            yield return new WaitForSeconds(0.2f);
            Assert.That(line.maxVisibleCharacters, Is.GreaterThan(0).And.LessThan(line.text.Length));
            AdvanceOneLine(dialogue);
            Assert.That(camera.StoryFocusTarget, Is.EqualTo(Actor("NPC-022").transform),
                "The player camera should focus on the current speaker.");
            FinishCurrentSequence(dialogue);
            Assert.That(storyPanel.activeSelf, Is.False);
            Assert.That(movement.StoryMovementLocked, Is.False);
            Assert.That(camera.StoryLookLocked, Is.False);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.AnswerBossCall));
            Assert.That(chapter.TryAnswerBossCall(), Is.True);
            FinishCurrentSequence(dialogue);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.MeetBoss));
            OfficeNpcDirector npcDirector = UnityEngine.Object.FindFirstObjectByType<OfficeNpcDirector>();
            NpcActivityPoint executivePoint = npcDirector.GetComponentsInChildren<NpcActivityPoint>()
                .First(point => point.name.StartsWith("executive_talk", StringComparison.Ordinal));
            Assert.That(npcDirector.IsStoryRestricted(executivePoint), Is.True,
                "The boss office must be cleared as soon as the call ends.");
            yield return null;
            Assert.That(npcDirector.Actors.Any(actor => actor.Destination != null
                && npcDirector.IsStoryRestricted(actor.Destination)), Is.False,
                "Workers already heading into the boss office must abandon that route.");
            Assert.That(gate.CanOpenFor(player), Is.True);
            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-006").GetComponent<NpcInteractable>(), player), Is.True);
            FinishCurrentSequence(dialogue);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
            yield return null;
            Assert.That(gate.CanOpenFor(player), Is.False);
            gate.OnBlocked(player);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_scold_raka"));
            FinishCurrentSequence(dialogue);

            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-003").GetComponent<NpcInteractable>(), player), Is.True);
            FinishCurrentSequence(dialogue);
            Assert.That(chapter.Progress.FirstColleagueId, Is.EqualTo("NPC-003"));
            Assert.That(File.Exists(savePath), Is.True);
            ChapterOneProgress loaded = ChapterOneSaveStore.Load(savePath);
            Assert.That(loaded.FirstColleagueId, Is.EqualTo("NPC-003"));
            Assert.That(loaded.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
            yield return null;
            Assert.That(Actor("NPC-003").Navigation.speed, Is.EqualTo(4f).Within(0.1f));
            Assert.That(Actor("NPC-003").CurrentState, Is.EqualTo(NpcState.StoryMoving));
            Assert.That(gate.BlockedPromptKey, Is.EqualTo("interact.story.find_sinta"));
            Assert.That(GameObject.Find("Boss Player Barrier").GetComponent<Collider>().enabled, Is.True);
            gate.OnBlocked(player);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_scold_sinta"));
            FinishCurrentSequence(dialogue);
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator RakaThenSintaCompletesAtTheVehicle() =>
            RunWholeChapter("NPC-003", "NPC-004");

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator SintaThenRakaCompletesAtTheVehicle() =>
            RunWholeChapter("NPC-004", "NPC-003");

        private IEnumerator RunWholeChapter(string firstId, string lastId)
        {
            ChapterOneDirector chapter = UnityEngine.Object.FindFirstObjectByType<ChapterOneDirector>();
            StoryDialogueController dialogue = UnityEngine.Object.FindFirstObjectByType<StoryDialogueController>();
            OfficeNpcAgent[] cast = UnityEngine.Object.FindObjectsByType<OfficeNpcAgent>(FindObjectsSortMode.None);
            OfficeNpcAgent Actor(string id) => cast.Single(agent => agent.Profile.Id == id);
            PlayerController movement = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            PlayerCameraController camera = movement.GetComponent<PlayerCameraController>();
            StoryPlayerCinematicMover mover = movement.GetComponent<StoryPlayerCinematicMover>();
            StoryPlayerSeating seating = movement.GetComponent<StoryPlayerSeating>();
            Transform player = movement.transform;
            Transform chapterRoot = GameObject.Find("Chapter One Story").transform;

            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-021").GetComponent<NpcInteractable>(), player), Is.True);
            FinishCurrentSequence(dialogue);
            Assert.That(chapter.TryAnswerBossCall(), Is.True);
            FinishCurrentSequence(dialogue);
            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-006").GetComponent<NpcInteractable>(), player), Is.True);
            FinishCurrentSequence(dialogue);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.FindColleagues));
            Assert.That(Vector3.Distance(Actor("NPC-006").transform.position,
                chapterRoot.Find("Boss Chair").position), Is.LessThan(0.8f),
                "The boss should begin at the desk in the Indonesian-flag office.");

            Assert.That(chapter.TryBeginNpcDialogue(Actor(firstId).GetComponent<NpcInteractable>(), player), Is.True);
            FinishCurrentSequence(dialogue);
            float firstDeadline = Time.realtimeSinceStartup + 105f;
            float nextReport = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < firstDeadline && !Arrived(chapter.Progress, firstId))
            {
                if (Time.realtimeSinceStartup >= nextReport)
                {
                    OfficeNpcAgent actor = Actor(firstId);
                    Debug.Log($"FIRST {firstId} at {actor.transform.position:F2} state {actor.CurrentState} "
                        + $"nav {actor.Navigation.isOnNavMesh} path {actor.Navigation.pathStatus} "
                        + $"remaining {actor.Navigation.remainingDistance:F2} steering {actor.Navigation.steeringTarget:F2} "
                        + $"stopped {actor.Navigation.isStopped} pending {actor.Navigation.pathPending} "
                        + $"door17 {UnityEngine.Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None).Single(d => d.name == "Office_ArchiveHQ_DoorLeaf_17").IsOpen}");
                    nextReport = Time.realtimeSinceStartup + 10f;
                }
                yield return null;
            }
            Assert.That(Arrived(chapter.Progress, firstId), Is.True,
                $"{firstId} did not reach the executive room. State: {Actor(firstId).CurrentState}, "
                + $"position {Actor(firstId).transform.position:F2}, path {Actor(firstId).Navigation.pathStatus}");
            BossOfficeGate gate = UnityEngine.Object.FindFirstObjectByType<BossOfficeGate>();
            Assert.That(gate.GetComponent<DoorInteractable>().IsOpen, Is.False,
                "The executive door must close after the first invited colleague enters.");
            Assert.That(GameObject.Find("Boss Player Barrier").GetComponent<Collider>().enabled, Is.True,
                "The player must not be able to slip through while one colleague is missing.");
            Assert.That(Vector3.Distance(Actor(firstId).transform.position,
                chapterRoot.Find(firstId == "NPC-003" ? "Raka Seat" : "Sinta Seat").position),
                Is.LessThan(1f));
            Assert.That(chapter.Progress.BossMeetingSide, Is.EqualTo(1).Or.EqualTo(2),
                "The boss should choose a front-sofa side after the first colleague enters.");
            OfficeNpcAgent bossActor = Actor("NPC-006");
            float bossWalkDeadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < bossWalkDeadline && !bossActor.StorySeated)
                yield return null;
            Assert.That(bossActor.StorySeated, Is.True,
                "The boss should walk from his chair to the front sofa after the first colleague arrives.");
            Assert.That(Vector3.Distance(bossActor.transform.position,
                chapterRoot.Find(chapter.Progress.BossMeetingSide == 1
                    ? "Boss Meeting Left" : "Boss Meeting Right").position), Is.LessThan(0.8f));
            Collider bossRoom = GameObject.Find("Boss Room Zone").GetComponent<Collider>();
            Assert.That(bossRoom.bounds.Contains(Actor("NPC-017").transform.position), Is.False,
                "Background archive guard should leave the boss office before the colleagues gather.");

            chapter.RestoreProgress(ChapterOneSaveStore.Load(savePath));
            Assert.That(chapter.Progress.FirstColleagueId, Is.EqualTo(firstId));
            Assert.That(Arrived(chapter.Progress, firstId), Is.True);
            Vector3? lastEntryPosition = null;
            chapter.ProgressChanged += snapshot =>
            {
                if (!lastEntryPosition.HasValue
                    && snapshot.Stage == ChapterOneStage.FinalBossBriefing)
                    lastEntryPosition = Actor(lastId).transform.position;
            };
            PositionPlayerNear(player, Actor(lastId).transform);
            Assert.That(chapter.TryBeginNpcDialogue(Actor(lastId).GetComponent<NpcInteractable>(), player), Is.True);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.EscortLastColleague));
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo(lastId == "NPC-003"
                ? "chapter.raka_last_intro" : "chapter.sinta_last_intro"));
            Assert.That(Actor(lastId).StoryMoveRequested, Is.False,
                "The last colleague must finish the short introduction before walking.");
            Assert.That(movement.StoryDialogueMovementLocked, Is.True);
            Assert.That(camera.StoryDialogueLookLocked, Is.True);
            FinishCurrentSequence(dialogue);
            Assert.That(chapter.Progress.LastColleagueIntroComplete, Is.True);
            float escortStartDeadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < escortStartDeadline && !mover.IsFollowing)
                yield return null;
            Assert.That(mover.IsFollowing, Is.True,
                "The player should automatically follow the last colleague after the introduction.");
            Assert.That(mover.FollowTarget, Is.EqualTo(Actor(lastId).transform));
            Assert.That(Actor(lastId).StoryMoveRequested, Is.True);
            Assert.That(movement.StoryCinematicMovementLocked, Is.True);
            Assert.That(camera.StoryCinematicLookLocked, Is.True);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo(lastId == "NPC-003"
                ? "chapter.raka_last" : "chapter.sinta_last"));
            Vector3 playerEscortStart = player.position;

            float escortDeadline = Time.realtimeSinceStartup + 180f;
            nextReport = Time.realtimeSinceStartup + 10f;
            float maximumGap = 0f;
            while (Time.realtimeSinceStartup < escortDeadline
                   && chapter.Progress.Stage != ChapterOneStage.FinalBossBriefing)
            {
                maximumGap = Mathf.Max(maximumGap,
                    Vector3.Distance(player.position, Actor(lastId).transform.position));
                if (Time.realtimeSinceStartup >= nextReport)
                {
                    OfficeNpcAgent actor = Actor(lastId);
                    Debug.Log($"ESCORT {lastId} at {actor.transform.position:F2} state {actor.CurrentState} "
                        + $"line {dialogue.LineIndex} complete {chapter.Progress.WalkDialogueComplete} "
                        + $"path {actor.Navigation.pathStatus} remaining {actor.Navigation.remainingDistance:F2} "
                        + $"player {player.position:F2} grounded {player.GetComponent<CharacterController>().isGrounded} "
                        + $"following {mover.IsFollowing} gap {Vector3.Distance(player.position, actor.transform.position):F2}");
                    nextReport = Time.realtimeSinceStartup + 10f;
                }
                yield return null;
            }
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.FinalBossBriefing),
                $"Escort failed for {lastId}; state {Actor(lastId).CurrentState}, dialogue line {dialogue.LineIndex}");
            Assert.That(lastEntryPosition.HasValue, Is.True);
            Bounds executiveRoom = GameObject.Find("Boss Room Zone").GetComponent<Collider>().bounds;
            Assert.That(lastEntryPosition.Value.x,
                Is.LessThanOrEqualTo(executiveRoom.max.x - 0.45f),
                "The last colleague must clear the doorway before being seated.");
            Assert.That(Vector3.Distance(playerEscortStart, player.position), Is.GreaterThan(1f),
                "The player should walk automatically; the test never moves the player during escort.");
            Assert.That(maximumGap, Is.LessThan(6f),
                "The automated follower should stay within roughly five metres of the colleague.");
            Assert.That(Arrived(chapter.Progress, lastId), Is.True);
            Assert.That(mover.IsActive, Is.False);
            Assert.That(chapter.Progress.MeetingAssembled, Is.True);
            Assert.That(chapter.Progress.PlayerSeated, Is.False);
            Assert.That(Vector3.Distance(Actor("NPC-003").transform.position,
                chapterRoot.Find("Raka Seat").position), Is.LessThan(0.8f));
            Assert.That(Vector3.Distance(Actor("NPC-004").transform.position,
                chapterRoot.Find("Sinta Seat").position), Is.LessThan(0.8f));
            Assert.That(Vector3.Distance(Actor("NPC-006").transform.position,
                chapterRoot.Find(chapter.Progress.BossMeetingSide == 1
                    ? "Boss Meeting Left" : "Boss Meeting Right").position), Is.LessThan(0.8f));
            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-006").GetComponent<NpcInteractable>(), player), Is.True);
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_seating"));
            Assert.That(movement.StoryDialogueMovementLocked, Is.True);
            Assert.That(camera.StoryFocusTarget, Is.EqualTo(Actor("NPC-006").transform));
            FinishCurrentSequence(dialogue);
            Assert.That(gate.GetComponent<DoorInteractable>().IsOpen, Is.True,
                "The boss should open the office door before the player walks to the sofa.");
            float seatingDeadline = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < seatingDeadline && !chapter.Progress.PlayerSeated)
                yield return null;
            Assert.That(chapter.Progress.PlayerSeated, Is.True,
                "The player should automatically take the middle seat after the invitation.");
            Assert.That(seating.IsSeated, Is.True);
            Assert.That(Vector3.Distance(player.position, chapterRoot.Find("Player Seat").position),
                Is.LessThan(0.4f));
            Assert.That(dialogue.ActiveSequence.Id, Is.EqualTo("chapter.boss_final"));
            Assert.That(movement.StoryMovementLocked, Is.True);
            FinishCurrentSequence(dialogue);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.CollectCarKey));
            Assert.That(seating.IsSeated, Is.False);
            Assert.That(movement.StoryMovementLocked, Is.False);
            Assert.That(camera.StoryLookLocked, Is.False);
            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-002").GetComponent<NpcInteractable>(), player), Is.True);
            FinishCurrentSequence(dialogue);
            Assert.That(chapter.Inventory.Has(CarKeyInventory.CarKeyId), Is.True);
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.ReachVehicle));
            Assert.That(chapter.TryBeginNpcDialogue(Actor("NPC-002").GetComponent<NpcInteractable>(), player), Is.False);

            VehicleArrivalZone arrival = UnityEngine.Object.FindFirstObjectByType<VehicleArrivalZone>();
            player.position = arrival.transform.position + Vector3.right * 1.5f;
            yield return null;
            Assert.That(chapter.Progress.Stage, Is.EqualTo(ChapterOneStage.Complete));
            Assert.That(ChapterOneSaveStore.Load(savePath).Stage, Is.EqualTo(ChapterOneStage.Complete));
        }

        private static bool Arrived(ChapterOneProgress progress, string id) =>
            id == "NPC-003" ? progress.RakaArrived : progress.SintaArrived;

        private static string DescribeMover(StoryPlayerCinematicMover mover, Transform player)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var path = (NavMeshPath)typeof(StoryPlayerCinematicMover)
                .GetField("currentPath", flags).GetValue(mover);
            int corner = (int)typeof(StoryPlayerCinematicMover)
                .GetField("nextCorner", flags).GetValue(mover);
            string route = path == null ? "none" : string.Join(";",
                path.corners.Select(point => point.ToString("F2")));
            string blockers = string.Join(",",
                Physics.OverlapSphere(player.position + Vector3.up * 0.9f, 1f,
                    ~0, QueryTriggerInteraction.Ignore)
                    .Where(collider => collider.transform != player)
                    .Take(10).Select(collider => collider.name));
            CharacterController body = player.GetComponent<CharacterController>();
            PlayerController movement = player.GetComponent<PlayerController>();
            Vector3 next = path != null && corner < path.corners.Length
                ? Vector3.ProjectOnPlane(path.corners[corner] - player.position, Vector3.up)
                : Vector3.zero;
            string contacts = next.sqrMagnitude < 0.001f ? "none" : string.Join(",",
                Physics.CapsuleCastAll(player.position + Vector3.up * (body.radius + 0.05f),
                    player.position + Vector3.up * (body.height - body.radius - 0.05f),
                    body.radius * 0.9f, next.normalized, 1.2f,
                    ~0, QueryTriggerInteraction.Ignore)
                    .Where(hit => hit.collider.transform != player)
                    .OrderBy(hit => hit.distance).Take(8)
                    .Select(hit => $"{hit.collider.name}@{hit.distance:F2}/{hit.normal:F2}"));
            return $"active={mover.IsActive}, path={path?.status}, corner={corner}, "
                + $"route={route}, velocity={body.velocity:F2}, cinematic={movement.CinematicVelocity:F2}, "
                + $"cinematicLock={movement.StoryCinematicMovementLocked}, grounded={body.isGrounded}, "
                + $"collisions={body.collisionFlags}, nearby={blockers}, cast={contacts}";
        }

        private static void PositionPlayerNear(Transform player, Transform colleague)
        {
            var offsets = new[] { -colleague.forward, colleague.right, -colleague.right,
                colleague.forward };
            foreach (Vector3 offset in offsets)
            {
                Vector3 candidate = colleague.position + offset * 1.5f;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.75f, NavMesh.AllAreas)
                    || Mathf.Abs(hit.position.y - colleague.position.y) > 0.5f
                    || Vector3.Distance(hit.position, colleague.position) < 0.8f) continue;
                CharacterController controller = player.GetComponent<CharacterController>();
                controller.enabled = false;
                player.position = hit.position;
                controller.enabled = true;
                Physics.SyncTransforms();
                return;
            }
            Assert.Fail("Could not place the player on the NavMesh near the last colleague before escort.");
        }

        private static void AdvanceOneLine(StoryDialogueController dialogue)
        {
            MethodInfo advance = typeof(StoryDialogueController).GetMethod("Advance",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(advance, Is.Not.Null);
            advance.Invoke(dialogue, null);
        }

        private static void FinishCurrentSequence(StoryDialogueController dialogue)
        {
            DialogueSequence current = dialogue.ActiveSequence;
            Assert.That(current, Is.Not.Null);
            int guard = 100;
            while (dialogue.IsPlaying && dialogue.ActiveSequence == current && guard-- > 0)
                AdvanceOneLine(dialogue);
            Assert.That(guard, Is.GreaterThan(0), "The active dialogue did not complete.");
            Assert.That(dialogue.ActiveSequence, Is.Not.EqualTo(current),
                "The requested sequence must end, even if its completion starts the next scene.");
        }
    }
}
