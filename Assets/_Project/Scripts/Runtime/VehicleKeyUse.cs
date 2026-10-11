using System;
using System.Collections.Generic;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using UnityEngine;
using UnityEngine.AI;

namespace SaksiTerakhir.Vehicles
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed class VehicleKeyUse : MonoBehaviour
    {
        [SerializeField] private ItemKeyLock keyLock;
        [SerializeField] private VehicleSignalFeedback signalFeedback;
        [SerializeField, Min(0.1f)] private float remoteRange = 5f;
        [SerializeField, Min(0.1f)] private float driverDoorRange = 2f;
        [SerializeField] private Collider driverDoorTrigger;
        [SerializeField] private ChapterOneDirector chapter;
        [Header("Passenger seats")]
        [SerializeField, Min(0.5f)] private float passengerEyeHeight = 1.30f;
        [SerializeField] private Transform driverSeat;
        [SerializeField] private Transform frontPassengerSeat;
        [SerializeField] private Transform rearPlayerSeat;
        [SerializeField] private Transform rearColleagueSeat;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private OfficeNpcAgent colleagueA;
        [SerializeField] private OfficeNpcAgent colleagueB;

        private Transform seatedActor;
        private StoryPlayerSeating playerSeating;
        private PlayerController playerMovement;
        private CharacterController playerBody;
        private bool movementWasEnabled;
        private bool bodyWasEnabled;
        private int boardedFrame;
        private Transform currentPlayerSeat;
        private Transform currentSeatA;
        private Transform currentSeatB;
        private readonly List<ColliderState> passengerColliders = new List<ColliderState>();
        private readonly Collider[] exitObstacles = new Collider[32];
        private struct ColliderState { public Collider collider; public bool enabled; }

        public ItemKeyLock KeyLock => keyLock;
        public float RemoteRange => remoteRange;
        public bool IsOccupied => seatedActor != null;
        public Transform PlayerActor => seatedActor;
        public Transform ColleagueBTransform => colleagueB != null ? colleagueB.transform : null;
        public bool CanBoard => chapter != null && !chapter.GameplaySuspended && chapter.Progress != null
            && (chapter.Progress.Stage == ChapterOneStage.ReachVehicle
                || chapter.Progress.Stage == ChapterOneStage.ReturnToVehicle
                    && chapter.Progress.Story.FuelStation.Ready && chapter.Progress.Story.FuelStation.CallDelivered);
        public event Action Boarded;
        public bool HandlesKey(string id) => keyLock != null && keyLock.RequiredKey != null
            && keyLock.RequiredKey.Id == id;
        public bool TryUseRemote(InventoryEntry entry, Transform actor)
        {
            if (!isActiveAndEnabled || actor == null || !OwnsKey(entry, actor)
                || (chapter != null && chapter.GameplaySuspended)) return false;
            if ((actor.position - transform.position).sqrMagnitude > remoteRange * remoteRange) return false;
            if (signalFeedback != null) signalFeedback.SignalRemote();
            return true;
        }

        public bool IsWithinDriverDoorReach(Transform actor)
        {
            if (actor == null) return false;
            Vector3 point = driverDoorTrigger != null ? driverDoorTrigger.ClosestPoint(actor.position) : transform.position;
            return (point - actor.position).sqrMagnitude <= driverDoorRange * driverDoorRange;
        }

        public bool TryUnlockAndBoard(InventoryEntry entry, Transform actor)
        {
            if (!isActiveAndEnabled || !IsWithinDriverDoorReach(actor) || !OwnsKey(entry, actor)
                || (chapter != null && chapter.GameplaySuspended)
                || keyLock == null || !keyLock.TryUnlock(entry)) return false;
            if (signalFeedback != null) signalFeedback.Flash();
            TryBoard(actor);
            return true;
        }

        public bool TryBoardFromDoor(Transform actor)
        {
            if (!TryBoard(actor)) return false;
            keyLock.PlayUnlockFeedback();
            if (signalFeedback != null) signalFeedback.Flash();
            return true;
        }

        public bool TryBoard(Transform actor)
        {
            if (actor == null || IsOccupied || keyLock == null || keyLock.IsLocked || !IsWithinDriverDoorReach(actor)
                || !CanBoard) return false;
            StoryPlayerSeating seating = actor.GetComponent<StoryPlayerSeating>();
            bool colleagueDrives = chapter.Progress.Story.SharedMealQuest == StoryQuestStatus.Completed;
            Transform seat = colleagueDrives ? rearPlayerSeat : driverSeat;
            if (seating == null || !Occupy(actor, seat, colleagueDrives)) return false;
            Boarded?.Invoke();
            return true;
        }

        public bool RestoreOccupants(Transform actor)
        {
            if (actor == null || IsOccupied || chapter == null || chapter.Progress == null) return false;
            bool colleagueDrives = chapter.Progress.Story.DepartureDriver == ChapterOneDepartureDriver.ColleagueA
                || chapter.Progress.Story.SharedMealQuest == StoryQuestStatus.Completed;
            return Occupy(actor, colleagueDrives ? rearPlayerSeat : driverSeat, colleagueDrives);
        }

        private bool Occupy(Transform actor, Transform seat, bool colleagueDrives)
        {
            StoryPlayerSeating seating = actor.GetComponent<StoryPlayerSeating>();
            if (seating == null || !seating.SeatAt(seat, passengerEyeHeight)) return false;
            seatedActor = actor;
            currentPlayerSeat = seat;
            playerSeating = seating;
            playerMovement = actor.GetComponent<PlayerController>();
            playerBody = actor.GetComponent<CharacterController>();
            movementWasEnabled = playerMovement != null && playerMovement.enabled;
            bodyWasEnabled = playerBody != null && playerBody.enabled;
            // Seats own passenger poses; cabin overlaps must not resolve through physics.
            if (playerMovement != null) playerMovement.enabled = false;
            if (playerBody != null) playerBody.enabled = false;
            boardedFrame = Time.frameCount;
            currentSeatA = colleagueDrives ? driverSeat : frontPassengerSeat;
            currentSeatB = rearColleagueSeat;
            SeatColleague(colleagueA, currentSeatA);
            SeatColleague(colleagueB, currentSeatB);
            GetComponent<VehicleJourneyDirector>()?.SetPassengerContext(true);
            return true;
        }

        public bool TryExit(Transform actor)
        {
            if (actor == null || actor != seatedActor || exitPoint == null || Time.frameCount == boardedFrame
                || (chapter != null && chapter.GameplaySuspended)) return false;
            VehicleJourneyDirector journey = GetComponent<VehicleJourneyDirector>();
            if (journey != null && !journey.CanExit) return false;
            if (playerMovement != null && (playerMovement.UiMovementLocked || playerMovement.StoryDialogueMovementLocked)) return false;
            Vector3 position = exitPoint.position;
            if (!ExitIsClear(position, actor)) return false;
            ReleasePassengers(position, exitPoint.rotation);
            return true;
        }

        public bool ContainsPassenger(Transform value)
            => value != null && (seatedActor != null && value.IsChildOf(seatedActor)
                || currentSeatA != null && colleagueA != null && value.IsChildOf(colleagueA.transform)
                || currentSeatB != null && colleagueB != null && value.IsChildOf(colleagueB.transform));

        private void LateUpdate()
        {
            if (chapter != null && chapter.GameplaySuspended) return;
            RefreshPassengers();
        }

        public void RefreshPassengers()
        {
            if (!IsOccupied) return;
            if (currentPlayerSeat != null) seatedActor.SetPositionAndRotation(currentPlayerSeat.position, currentPlayerSeat.rotation);
            FollowColleague(colleagueA, currentSeatA);
            FollowColleague(colleagueB, currentSeatB);
        }

        private static void FollowColleague(OfficeNpcAgent npc, Transform seat)
        {
            if (npc == null || seat == null) return;
            npc.transform.SetPositionAndRotation(seat.position, seat.rotation);
            Rigidbody body = npc.GetComponent<Rigidbody>();
            if (body != null && body.isKinematic) { body.position = seat.position; body.rotation = seat.rotation; }
        }

        public bool ForceExitAtStation()
        {
            if (!IsOccupied || exitPoint == null || !ExitIsClear(exitPoint.position, seatedActor)) return false;
            ReleasePassengers(exitPoint.position, exitPoint.rotation);
            return true;
        }

        public void PrepareRestore()
        {
            if (IsOccupied) ReleasePlayer(seatedActor.position, seatedActor.rotation);
            RestorePassengerColliders();
            currentSeatA = currentSeatB = null;
            GetComponent<VehicleJourneyDirector>()?.SetPassengerContext(false);
        }

        private bool ExitIsClear(Vector3 position, Transform actor)
        {
            float radius = playerBody != null ? playerBody.radius : 0.35f;
            float height = playerBody != null ? playerBody.height : 1.8f;
            Vector3 bottom = position + Vector3.up * (radius + 0.04f);
            Vector3 top = position + Vector3.up * Mathf.Max(radius + 0.04f, height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, exitObstacles, ~0, QueryTriggerInteraction.Ignore);
            if (count == exitObstacles.Length) return false;
            for (int i = 0; i < count; i++)
                if (exitObstacles[i] != null && !exitObstacles[i].transform.IsChildOf(actor)
                    && !exitObstacles[i].transform.IsChildOf(transform) && !ContainsPassenger(exitObstacles[i].transform)) return false;
            return true;
        }

        private void OnDisable()
        {
            if (seatedActor == null) return;
            Transform point = exitPoint != null ? exitPoint : seatedActor;
            ReleasePassengers(point.position, point.rotation);
        }

        private void ReleasePassengers(Vector3 position, Quaternion rotation)
        {
            // Leave the player's exit side clear when the companions get out.
            float colleagueSide = Vector3.Dot(position - transform.position, transform.right) >= 0f ? -1f : 1f;
            Vector3 colleagueExit = transform.position + transform.right * (2.1f * colleagueSide);
            ReleasePlayer(position, rotation);
            PlaceColleagueOutside(colleagueA, colleagueExit + transform.forward * .3f);
            PlaceColleagueOutside(colleagueB, colleagueExit - transform.forward * 1.8f);
            RestorePassengerColliders();
            currentSeatA = currentSeatB = null;
            GetComponent<VehicleJourneyDirector>()?.SetPassengerContext(false);
        }

        private static void PlaceColleagueOutside(OfficeNpcAgent npc, Vector3 position)
        {
            if (npc == null) return;
            position.y = Mathf.Max(.02f, position.y);
            npc.SetStorySeated(false);
            npc.HoldForStory();
            if (npc.Navigation != null && npc.Navigation.enabled)
            {
                bool placed = NavMesh.SamplePosition(position, out NavMeshHit ground, .6f, npc.Navigation.areaMask)
                    && npc.Navigation.Warp(ground.position);
                // A station without a baked mesh must not pull the actor back to its old office polygon.
                if (!placed) { npc.Navigation.updatePosition = false; npc.Navigation.enabled = false; }
            }
            npc.transform.position = position;
            Rigidbody body = npc.GetComponent<Rigidbody>();
            if (body != null && body.isKinematic) body.position = position;
        }

        private void ReleasePlayer(Vector3 position, Quaternion rotation)
        {
            Transform actor = seatedActor;
            if (playerSeating != null) playerSeating.StandUp();
            if (actor != null) actor.SetPositionAndRotation(position, rotation);
            if (playerBody != null) playerBody.enabled = bodyWasEnabled;
            if (playerMovement != null) playerMovement.enabled = movementWasEnabled;
            seatedActor = null;
            currentPlayerSeat = null;
            playerSeating = null;
            playerBody = null;
            playerMovement = null;
        }

        private bool OwnsKey(InventoryEntry entry, Transform actor)
        {
            if (entry == null || !HandlesKey(entry.Id)) return false;
            InventoryPanelController inventory = actor.GetComponentInParent<InventoryPanelController>();
            return inventory != null && inventory.State != null && inventory.State.GetSlotIndex(entry) >= 0;
        }

        private void SeatColleague(OfficeNpcAgent colleague, Transform seat)
        {
            if (colleague == null || seat == null) return;
            colleague.HoldForStory();
            colleague.SetStorySeated(true);
            colleague.transform.SetPositionAndRotation(seat.position, seat.rotation);
            foreach (Collider collider in colleague.GetComponentsInChildren<Collider>(true))
            {
                passengerColliders.Add(new ColliderState { collider = collider, enabled = collider.enabled });
                collider.enabled = false;
            }
        }

        private void RestorePassengerColliders()
        {
            foreach (ColliderState state in passengerColliders)
                if (state.collider != null) state.collider.enabled = state.enabled;
            passengerColliders.Clear();
        }
    }
}
