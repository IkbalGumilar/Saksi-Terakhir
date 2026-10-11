using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using SaksiTerakhir.Vehicles;
using UnityEngine;
using UnityEngine.AI;

namespace SaksiTerakhir.Tests
{
    public sealed class VehiclePassengerAttachmentTests
    {
        private static readonly Vector3 Origin = new Vector3(12000f, 0f, 12000f);
        private readonly List<GameObject> objects = new List<GameObject>();
        private VehicleKeyUse vehicle;
        private GameObject car, player;
        private ChapterOneProgress progress;
        private PlayerController movement;
        private CharacterController playerBody;
        private StoryPlayerSeating seating;
        private OfficeNpcAgent colleagueA, colleagueB;
        private CapsuleCollider colliderA, colliderB;
        private BoxCollider disabledAccessoryCollider;
        private Transform driverSeat, frontSeat, rearPlayerSeat, rearColleagueSeat, exit;
        private int boardedEvents;

        [SetUp]
        public void SetUp()
        {
            car = Create("Passenger vehicle test");
            car.transform.position = Origin;
            vehicle = car.AddComponent<VehicleKeyUse>();
            var chapter = car.AddComponent<ChapterOneDirector>();
            progress = new ChapterOneProgress();
            Set(progress, "stage", ChapterOneStage.TravelToFuelStation);
            Set(progress, "hasCarKey", true);
            Set(progress.Story, "sharedMealQuest", StoryQuestStatus.Skipped);
            Set(progress.Story, "departureDriver", ChapterOneDepartureDriver.Player);
            Set(chapter, "progress", progress);
            Set(vehicle, "chapter", chapter);

            driverSeat = Anchor("Driver seat", new Vector3(-.45f, .2f, .25f));
            frontSeat = Anchor("Front passenger", new Vector3(.45f, .2f, .25f));
            rearPlayerSeat = Anchor("Rear player", new Vector3(-.45f, .2f, -.9f));
            rearColleagueSeat = Anchor("Rear colleague", new Vector3(.45f, .2f, -.9f));
            exit = Anchor("Safe station exit", new Vector3(-3f, .04f, .25f));
            Set(vehicle, "driverSeat", driverSeat);
            Set(vehicle, "frontPassengerSeat", frontSeat);
            Set(vehicle, "rearPlayerSeat", rearPlayerSeat);
            Set(vehicle, "rearColleagueSeat", rearColleagueSeat);
            Set(vehicle, "exitPoint", exit);

            player = Create("Passenger MC");
            player.transform.position = Origin;
            playerBody = player.AddComponent<CharacterController>();
            movement = player.AddComponent<PlayerController>();
            seating = player.AddComponent<StoryPlayerSeating>();
            colleagueA = CreateNpc("Colleague A", out colliderA);
            colleagueB = CreateNpc("Colleague B", out colliderB);
            var accessory = Create("NPC accessory collider originally disabled");
            accessory.transform.SetParent(colleagueA.transform, false);
            disabledAccessoryCollider = accessory.AddComponent<BoxCollider>();
            disabledAccessoryCollider.enabled = false;
            Set(vehicle, "colleagueA", colleagueA);
            Set(vehicle, "colleagueB", colleagueB);
            vehicle.Boarded += () => boardedEvents++;
        }

        [TearDown]
        public void TearDown()
        {
            // Release ownership while every actor is still alive, then destroy fixtures.
            if (vehicle != null) vehicle.PrepareRestore();
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void RestoredPassengersFollowTranslationAndTurnsInBothDriverBranches()
        {
            foreach (bool colleagueDrives in new[] { false, true })
            {
                Set(progress.Story, "departureDriver", colleagueDrives
                    ? ChapterOneDepartureDriver.ColleagueA : ChapterOneDepartureDriver.Player);
                Assert.That(vehicle.RestoreOccupants(player.transform), Is.True);
                car.transform.SetPositionAndRotation(Origin + new Vector3(8f, 0f, 5f), Quaternion.Euler(0f, 90f, 0f));
                vehicle.RefreshPassengers();

                AssertPose(player.transform, colleagueDrives ? rearPlayerSeat : driverSeat);
                AssertPose(colleagueA.transform, colleagueDrives ? driverSeat : frontSeat);
                AssertPose(colleagueB.transform, rearColleagueSeat);
                Assert.That(seating.CurrentSeat, Is.SameAs(colleagueDrives ? rearPlayerSeat : driverSeat));
                Assert.That(movement.enabled, Is.False);
                Assert.That(playerBody.enabled, Is.False);
                Assert.That(colleagueA.StorySeated && colleagueB.StorySeated, Is.True);
                Assert.That(colliderA.enabled || colliderB.enabled, Is.False);
                Assert.That(boardedEvents, Is.Zero, "Restoration must not restart departure dialogue.");
                vehicle.PrepareRestore();
            }
        }

        [TestCase(ChapterOneDepartureDriver.Player)]
        [TestCase(ChapterOneDepartureDriver.ColleagueA)]
        public void VehicleViewClearsTheDashboardWithoutRaisingThePassengerBody(ChapterOneDepartureDriver departureDriver)
        {
            Transform eye = CreatePassengerView();
            Set(progress.Story, "departureDriver", departureDriver);

            Assert.That(vehicle.RestoreOccupants(player.transform), Is.True);
            car.transform.SetPositionAndRotation(Origin + new Vector3(8f, 0f, 5f), Quaternion.Euler(0f, 90f, 0f));
            vehicle.RefreshPassengers();

            Assert.That(eye.localPosition.y, Is.EqualTo(1.30f).Within(.001f));
            Assert.That(car.transform.InverseTransformPoint(eye.position).y, Is.EqualTo(1.50f).Within(.003f));
            AssertPose(player.transform, departureDriver == ChapterOneDepartureDriver.Player ? driverSeat : rearPlayerSeat);
        }

        [Test]
        public void VehicleViewOverrideIsReleasedBeforeWalkingOrUsingAnOrdinaryStorySeat()
        {
            playerBody.height = 1.7f;
            Transform eye = CreatePassengerView();
            Assert.That(vehicle.RestoreOccupants(player.transform), Is.True);
            Assert.That(eye.localPosition.y, Is.EqualTo(1.30f).Within(.001f));

            Physics.SyncTransforms();
            Assert.That(vehicle.ForceExitAtStation(), Is.True);
            Assert.That(eye.localPosition.y, Is.EqualTo(1.564f).Within(.001f));

            Transform ordinarySeat = Create("Ordinary story seat after vehicle exit").transform;
            ordinarySeat.position = Origin + Vector3.right * 10f;
            Assert.That(seating.SeatAt(ordinarySeat), Is.True);
            Assert.That(eye.localPosition.y, Is.EqualTo(1.05f).Within(.001f),
                "The vehicle view must not alter the height of a sofa or meal seat.");
            seating.StandUp();
        }

        [Test]
        public void StationExitRestoresControlAndOnlyPreviouslyEnabledNpcColliders()
        {
            Assert.That(vehicle.RestoreOccupants(player.transform), Is.True);
            Physics.SyncTransforms();
            Assert.That(vehicle.ForceExitAtStation(), Is.True);

            Assert.That(vehicle.IsOccupied, Is.False);
            Assert.That(movement.enabled && playerBody.enabled, Is.True);
            Assert.That(movement.StoryCinematicMovementLocked, Is.False);
            Assert.That(seating.IsSeated, Is.False);
            AssertPose(player.transform, exit);
            Assert.That(colleagueA.StorySeated || colleagueB.StorySeated, Is.False);
            Assert.That(colliderA.enabled && colliderB.enabled, Is.True);
            Assert.That(disabledAccessoryCollider.enabled, Is.False);
            Assert.That(vehicle.ContainsPassenger(player.transform), Is.False);
            Assert.That(vehicle.ContainsPassenger(colleagueA.transform), Is.False);
            Assert.That(boardedEvents, Is.Zero);
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void StationExitSeparatesAllOccupantsOnEitherSideOfTheCar(float exitSide)
        {
            exit.localPosition = new Vector3(exitSide * 1.95f, .04f, .18f);
            Assert.That(vehicle.RestoreOccupants(player.transform), Is.True);
            Physics.SyncTransforms();

            Assert.That(vehicle.ForceExitAtStation(), Is.True);

            Assert.That(PlanarDistance(player.transform, colleagueA.transform),
                Is.GreaterThan(playerBody.radius + colliderA.radius), "MC and colleague A must not overlap after exiting.");
            Assert.That(PlanarDistance(player.transform, colleagueB.transform),
                Is.GreaterThan(playerBody.radius + colliderB.radius), "MC and colleague B must not overlap after exiting.");
            Assert.That(PlanarDistance(colleagueA.transform, colleagueB.transform),
                Is.GreaterThan(colliderA.radius + colliderB.radius), "The colleagues need separate exit positions.");
        }

        [Test]
        public void OccupantsStayInTheCarUntilStationExitSpaceIsClear()
        {
            Assert.That(vehicle.RestoreOccupants(player.transform), Is.True);
            var obstacle = Create("Blocked station exit");
            obstacle.transform.position = exit.position + Vector3.up * .9f;
            obstacle.AddComponent<BoxCollider>().size = new Vector3(1f, 1.8f, 1f);
            Physics.SyncTransforms();
            Assert.That(vehicle.ForceExitAtStation(), Is.False);
            Assert.That(vehicle.IsOccupied, Is.True);
            Assert.That(seating.IsSeated, Is.True);
            Assert.That(playerBody.enabled || movement.enabled, Is.False);
            Object.DestroyImmediate(obstacle);
            Physics.SyncTransforms();
            Assert.That(vehicle.ForceExitAtStation(), Is.True);
            Assert.That(vehicle.IsOccupied, Is.False);
        }

        [Test]
        public void PreparingRestoreReleasesOwnershipWithoutReplayingBoarding()
        {
            Assert.That(vehicle.RestoreOccupants(player.transform), Is.True);
            vehicle.PrepareRestore();
            Vector3 previousPlayer = player.transform.position;
            Vector3 previousColleague = colleagueA.transform.position;
            car.transform.position += Vector3.forward * 20f;
            vehicle.RefreshPassengers();

            Assert.That(vehicle.IsOccupied, Is.False);
            Assert.That(movement.enabled && playerBody.enabled, Is.True);
            Assert.That(colliderA.enabled && colliderB.enabled, Is.True);
            Assert.That(disabledAccessoryCollider.enabled, Is.False);
            Assert.That(player.transform.position, Is.EqualTo(previousPlayer));
            Assert.That(colleagueA.transform.position, Is.EqualTo(previousColleague));
            Assert.That(vehicle.ContainsPassenger(colleagueA.transform), Is.False);
            Assert.That(boardedEvents, Is.Zero);
            Assert.That(vehicle.RestoreOccupants(player.transform), Is.True,
                "Preparing a restore must leave the vehicle able to accept the restored session.");
            Assert.That(boardedEvents, Is.Zero);
        }

        [Test]
        public void PassengerHierarchyIsExcludedFromTheVehicleObstacleProbeOnlyWhileOccupied()
        {
            var hand = Create("Passenger hand collider hierarchy");
            hand.transform.SetParent(player.transform, false);
            var outsider = Create("Unrelated pedestrian");
            Assert.That(vehicle.RestoreOccupants(player.transform), Is.True);
            Assert.That(vehicle.ContainsPassenger(hand.transform), Is.True);
            Assert.That(vehicle.ContainsPassenger(disabledAccessoryCollider.transform), Is.True);
            Assert.That(vehicle.ContainsPassenger(outsider.transform), Is.False);
            vehicle.PrepareRestore();
            Assert.That(vehicle.ContainsPassenger(hand.transform), Is.False);
            Assert.That(vehicle.ContainsPassenger(disabledAccessoryCollider.transform), Is.False);
        }

        [Test]
        public void DisablingVehicleReleasesPlayerAndNpcSeatOwnership()
        {
            Assert.That(vehicle.RestoreOccupants(player.transform), Is.True);
            vehicle.enabled = false;
            typeof(VehicleKeyUse).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(vehicle, null);
            Assert.That(vehicle.IsOccupied, Is.False);
            Assert.That(seating.IsSeated, Is.False);
            Assert.That(movement.enabled && playerBody.enabled, Is.True);
            Assert.That(colleagueA.StorySeated || colleagueB.StorySeated, Is.False);
            Assert.That(colliderA.enabled && colliderB.enabled, Is.True);
            Assert.That(disabledAccessoryCollider.enabled, Is.False);
        }

        [Test]
        public void ExitDoesNotEnableAPlayerControllerThatWasAlreadyDisabledBeforeBoarding()
        {
            movement.enabled = false;
            playerBody.enabled = false;
            Assert.That(vehicle.RestoreOccupants(player.transform), Is.True);
            Physics.SyncTransforms();
            Assert.That(vehicle.ForceExitAtStation(), Is.True);
            Assert.That(movement.enabled, Is.False);
            Assert.That(playerBody.enabled, Is.False);
        }

        private OfficeNpcAgent CreateNpc(string name, out CapsuleCollider collider)
        {
            GameObject item = Create(name);
            item.SetActive(false);
            item.transform.position = Origin + Vector3.forward * 4f;
            NavMeshAgent navigation = item.AddComponent<NavMeshAgent>();
            navigation.enabled = false;
            collider = item.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * .9f;
            collider.height = 1.8f;
            collider.radius = .3f;
            Rigidbody body = item.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            OfficeNpcAgent npc = item.AddComponent<OfficeNpcAgent>();
            item.SetActive(true);
            return npc;
        }

        private Transform Anchor(string name, Vector3 localPosition)
        {
            Transform anchor = Create(name).transform;
            anchor.SetParent(car.transform, false);
            anchor.localPosition = localPosition;
            return anchor;
        }

        private Transform CreatePassengerView()
        {
            Transform eye = Create("Passenger eye").transform;
            eye.SetParent(player.transform, false);
            var camera = player.AddComponent<PlayerCameraController>();
            Set(camera, "input", player.AddComponent<PlayerInputReader>());
            Set(camera, "body", playerBody);
            Set(camera, "yawTransform", player.transform);
            Set(camera, "cameraTransform", eye);
            return eye;
        }

        private GameObject Create(string name)
        {
            var item = new GameObject(name);
            objects.Add(item);
            return item;
        }

        private static void AssertPose(Transform actor, Transform expected)
        {
            Assert.That(Vector3.Distance(actor.position, expected.position), Is.LessThan(.002f));
            Assert.That(Quaternion.Angle(actor.rotation, expected.rotation), Is.LessThan(.01f));
        }

        private static float PlanarDistance(Transform first, Transform second)
            => Vector3.ProjectOnPlane(first.position - second.position, Vector3.up).magnitude;

        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
