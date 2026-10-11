using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Npc;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SaksiTerakhir.Tests
{
    public sealed class DoorAutoCloseTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();
        private bool oldOptionsEnabled;
        private EnterPlayModeOptions oldOptions;
        private float oldScale, oldCapture;
        private Random.State oldRandom;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            oldOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            yield return new EnterPlayMode(false);
            oldScale = Time.timeScale;
            oldCapture = Time.captureDeltaTime;
            oldRandom = Random.state;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 30f;
            Random.InitState(20261005);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject root in roots) if (root != null) Object.DestroyImmediate(root);
            roots.Clear();
            Time.timeScale = oldScale;
            Time.captureDeltaTime = oldCapture;
            Random.state = oldRandom;
            yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptionsEnabled = oldOptionsEnabled;
            EditorSettings.enterPlayModeOptions = oldOptions;
        }

        [UnityTest]
        public IEnumerator NpcDoorWaitsUntilThePlayerCapsuleClearsTheDoorway()
        {
            DoorInteractable door = Door();
            GameObject crossing = Root("Player crossing doorway");
            CharacterController body = crossing.AddComponent<CharacterController>();
            body.height = 1.75f;
            body.radius = 0.3f;
            body.center = Vector3.up * 0.875f;
            door.Interact(NpcChild());
            Physics.SyncTransforms();
            float requestedAt = Time.time;
            while (Time.time < requestedAt + 2.2f) yield return null;
            Assert.That(door.IsOpen, Is.True, "An NPC deadline must never shut a door on a crossing player.");

            crossing.transform.position = Vector3.forward * 3f;
            Physics.SyncTransforms();
            float clearedAt = Time.time;
            while (Time.time < clearedAt + 0.85f) yield return null;
            Assert.That(door.IsOpen, Is.True, "The full NPC delay begins after passage is clear.");
            while (Time.time < clearedAt + 1.15f) yield return null;
            Assert.That(door.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator RequestedClosingCannotEnableTheDoorColliderThroughAnActor()
        {
            DoorInteractable door = Door();
            GameObject crossing = Root("Crossing actor");
            CharacterController body = crossing.AddComponent<CharacterController>();
            body.height = 1.75f;
            body.radius = 0.3f;
            body.center = Vector3.up * 0.875f;
            door.ForceOpen();
            yield return null;
            Physics.SyncTransforms();
            door.ForceClose();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(door.IsOpen, Is.True);
            Assert.That(door.GetComponent<Collider>().enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator PlayerDoorsCloseAtIndependentRandomTimesBetweenFourAndSevenSeconds()
        {
            Transform player = Root("Player").transform;
            var doors = new List<DoorInteractable>();
            var closedAt = new Dictionary<DoorInteractable, float>();
            for (int i = 0; i < 12; i++)
            {
                DoorInteractable door = Door();
                door.transform.position = Vector3.right * i * 3f;
                doors.Add(door);
                door.Interact(player);
            }
            yield return null;
            float openedAt = Time.time;
            while (Time.time < openedAt + 7.15f)
            {
                foreach (DoorInteractable door in doors)
                    if (!door.IsOpen && !closedAt.ContainsKey(door)) closedAt.Add(door, Time.time - openedAt);
                yield return null;
            }
            Assert.That(closedAt.Count, Is.EqualTo(doors.Count), "Every player-opened door should close automatically.");
            var distinctTimes = new HashSet<int>();
            foreach (float elapsed in closedAt.Values)
            {
                Assert.That(elapsed, Is.InRange(3.95f, 7.1f));
                distinctTimes.Add(Mathf.RoundToInt(elapsed * 10f));
            }
            Assert.That(distinctTimes.Count, Is.GreaterThan(1), "Each opening draws its own delay.");
            foreach (DoorInteractable door in doors) Assert.That(door.GetComponent<Collider>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator NpcChildInteractionClosesOneSecondAfterTheDoorIsFullyOpen()
        {
            DoorInteractable door = Door();
            door.Interact(NpcChild());
            yield return null;
            float openedAt = Time.time;
            while (Time.time < openedAt + 0.85f) yield return null;
            Assert.That(door.IsOpen, Is.True);
            while (Time.time < openedAt + 1.1f) yield return null;
            Assert.That(door.IsOpen, Is.False, "NPC-opened doors use one second, not the player delay.");
            yield return null;
            Assert.That(door.GetComponent<Collider>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator SlowNpcDoorDoesNotCountItsOpeningAnimationAsTheOneSecondDelay()
        {
            DoorInteractable door = Door(46f);
            door.Interact(NpcChild());
            float requestedAt = Time.time;
            while (Time.time < requestedAt + 1.5f) yield return null;
            Assert.That(door.IsOpen, Is.True, "Opening still takes two seconds at this speed.");
            while (Quaternion.Angle(door.transform.localRotation, Quaternion.Euler(0f, 92f, 0f)) > 0.02f
                && Time.time < requestedAt + 2.5f) yield return null;
            Assert.That(Quaternion.Angle(door.transform.localRotation, Quaternion.Euler(0f, 92f, 0f)), Is.LessThan(0.02f));
            float fullyOpenedAt = Time.time;
            while (Time.time < fullyOpenedAt + 0.85f) yield return null;
            Assert.That(door.IsOpen, Is.True);
            while (Time.time < fullyOpenedAt + 1.1f) yield return null;
            Assert.That(door.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator ClosingAndReopeningAsPlayerCancelsTheEarlierNpcDeadline()
        {
            DoorInteractable door = Door();
            Transform player = Root("Player").transform;
            door.Interact(NpcChild());
            yield return null;
            door.Interact(player);
            Assert.That(door.IsOpen, Is.False);
            yield return null;
            door.Interact(player);
            yield return null;
            float reopenedAt = Time.time;
            while (Time.time < reopenedAt + 1.2f) yield return null;
            Assert.That(door.IsOpen, Is.True, "The old NPC countdown must not close a new player opening.");
            while (Time.time < reopenedAt + 7.15f) yield return null;
            Assert.That(door.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator PausedMenuDoesNotAdvanceTheDoorTimer()
        {
            DoorInteractable door = Door();
            door.Interact(NpcChild());
            yield return null;
            Quaternion pausedRotation = door.transform.localRotation;
            Time.timeScale = 0f;
            for (int i = 0; i < 40; i++) yield return null;
            Assert.That(door.IsOpen, Is.True);
            Assert.That(door.transform.localRotation, Is.EqualTo(pausedRotation));
            Time.timeScale = 1f;
            float resumedAt = Time.time;
            while (Time.time < resumedAt + 0.8f) yield return null;
            Assert.That(door.IsOpen, Is.True);
            while (Time.time < resumedAt + 1.1f) yield return null;
            Assert.That(door.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator RestoredOpenDoorWaitsForGameplayAndThenClosesNormally()
        {
            DoorInteractable door = Door();
            Time.timeScale = 0f;
            door.RestoreSavedState(true);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(door.IsOpen, Is.True);
            Time.timeScale = 1f;
            float resumedAt = Time.time;
            while (Time.time < resumedAt + 3.8f) yield return null;
            Assert.That(door.IsOpen, Is.True);
            while (Time.time < resumedAt + 7.15f) yield return null;
            Assert.That(door.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator ForcedOpeningGetsANewDefaultTimerInsteadOfKeepingTheNpcDeadline()
        {
            DoorInteractable door = Door();
            door.Interact(NpcChild());
            yield return null;
            door.ForceOpen();
            float storyOpenedAt = Time.time;
            while (Time.time < storyOpenedAt + 1.2f) yield return null;
            Assert.That(door.IsOpen, Is.True, "The previous NPC timer must be replaced.");
            while (Time.time < storyOpenedAt + 7.5f) yield return null;
            Assert.That(door.IsOpen, Is.False, "Every open door, including a scripted opening, gets an automatic deadline.");
            yield return null;
            Assert.That(door.GetComponent<Collider>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator ClosedDoorRemainsSolidWhenAnActorApproaches()
        {
            DoorInteractable door = Door();
            GameObject player = Root("Player approaching a closed door");
            player.transform.position = Vector3.forward * 0.7f;
            CharacterController body = player.AddComponent<CharacterController>();
            body.height = 1.75f;
            body.radius = 0.3f;
            body.center = Vector3.up * 0.875f;
            Physics.SyncTransforms();
            Assert.That(door.PassageOccupied(), Is.True);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(door.IsOpen, Is.False);
            Assert.That(door.GetComponent<Collider>().enabled, Is.True,
                "Occupancy may delay closing, but it must never unlock a closed door.");
        }

        private DoorInteractable Door(float speed = 10000f)
        {
            GameObject root = Root("Automatic Door");
            root.AddComponent<BoxCollider>();
            DoorInteractable door = root.AddComponent<DoorInteractable>();
            typeof(DoorInteractable).GetField("swingSpeed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(door, speed);
            return door;
        }

        private Transform NpcChild()
        {
            GameObject npc = Root("NPC Opening A Door");
            npc.SetActive(false);
            npc.AddComponent<OfficeNpcAgent>();
            GameObject hand = new GameObject("NPC Interaction Child");
            hand.transform.SetParent(npc.transform, false);
            return hand.transform;
        }

        private GameObject Root(string name)
        {
            var root = new GameObject(name);
            roots.Add(root);
            return root;
        }
    }
}
