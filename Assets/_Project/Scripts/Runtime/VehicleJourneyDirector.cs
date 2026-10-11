using System;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Persistence;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Vehicles
{
    public enum VehicleJourneyPhase
    {
        ParkedOffice = 0, DepartureDialogue = 1, OnRoad = 2, FuelDialogue = 3,
        ToParking = 4, ParkedStation = 5,
        // These values are serialized in version-one autosaves; append new phases.
        StationDepartureDialogue = 6, LeavingStation = 7, ChapterFinished = 8
    }

    [Serializable]
    public sealed class SavedVehicleJourney
    {
        public int version;
        public VehicleJourneyPhase phase;
        public float routeDistance;
        public bool playerSeated;
        public bool keyUnlocked;

        public bool Validate()
            => version == 0 || version == 1 && Enum.IsDefined(typeof(VehicleJourneyPhase), phase)
                && SavedPose.Finite(routeDistance) && routeDistance >= 0 && routeDistance <= 10000
                && (phase == VehicleJourneyPhase.ParkedOffice || phase == VehicleJourneyPhase.ParkedStation || playerSeated);

        public bool MatchesChapter(ChapterOneProgress chapter)
        {
            if (version == 0) return true;
            switch (phase)
            {
                case VehicleJourneyPhase.DepartureDialogue: return chapter.Stage == ChapterOneStage.ReachVehicle;
                case VehicleJourneyPhase.OnRoad:
                case VehicleJourneyPhase.FuelDialogue:
                case VehicleJourneyPhase.ToParking: return chapter.Stage == ChapterOneStage.TravelToFuelStation;
                case VehicleJourneyPhase.ParkedStation: return chapter.Stage == ChapterOneStage.AtFuelStation
                    || chapter.Stage == ChapterOneStage.ReturnToVehicle;
                case VehicleJourneyPhase.StationDepartureDialogue: return chapter.Stage == ChapterOneStage.ReturnToVehicle;
                case VehicleJourneyPhase.LeavingStation: return chapter.Stage == ChapterOneStage.LeavingFuelStation;
                case VehicleJourneyPhase.ChapterFinished: return chapter.Stage == ChapterOneStage.Complete
                    || chapter.Stage == ChapterOneStage.LeavingFuelStation;
                default: return chapter.Stage != ChapterOneStage.TravelToFuelStation && chapter.Stage != ChapterOneStage.AtFuelStation
                    && chapter.Stage != ChapterOneStage.ReturnToVehicle && chapter.Stage != ChapterOneStage.LeavingFuelStation;
            }
        }
    }

    [DisallowMultipleComponent, RequireComponent(typeof(AutomaticVehicleDriver), typeof(VehicleKeyUse))]
    public sealed class VehicleJourneyDirector : MonoBehaviour
    {
        [SerializeField] private ChapterOneDirector chapter;
        [SerializeField] private StoryDialogueController dialogue;
        [SerializeField] private FuelStationChapterDirector fuelStation;
        [SerializeField] private Transform player;
        [SerializeField] private DialogueSequence departureMealComplete;
        [SerializeField] private DialogueSequence departureMealSkipped;
        [SerializeField] private DialogueSequence roadDialogue;
        [SerializeField] private DialogueSequence fuelDialogue;
        [SerializeField] private ItemDefinition nasiUduk;
        [SerializeField] private Vector3 officePosition;
        [SerializeField] private Vector3 officeEuler;

        private VehicleKeyUse seats;
        private AutomaticVehicleDriver driver;
        private bool sequenceStarted;
        private bool roadConversationPending;
        private int rewardBlockedAtItemCount = -1;
        private bool waitingForRewardSpace;
        public VehicleJourneyPhase Phase { get; private set; }
        public bool GameplaySuspended => chapter != null && chapter.GameplaySuspended;
        public bool CanExit => Phase == VehicleJourneyPhase.ParkedOffice || Phase == VehicleJourneyPhase.ParkedStation
            || Phase == VehicleJourneyPhase.DepartureDialogue && waitingForRewardSpace;

        private void Awake() => ResolveDependencies();

        private void OnEnable()
        {
            ResolveDependencies();
            seats.Boarded += OnBoarded;
            driver.Arrived += OnArrived;
        }

        private void OnDisable()
        {
            if (seats != null) seats.Boarded -= OnBoarded;
            if (driver != null) driver.Arrived -= OnArrived;
            SetPassengerContext(false);
        }

        private void ResolveDependencies()
        {
            if (seats == null) seats = GetComponent<VehicleKeyUse>();
            if (driver == null) driver = GetComponent<AutomaticVehicleDriver>();
            if (fuelStation == null && chapter != null) fuelStation = chapter.GetComponent<FuelStationChapterDirector>();
        }

        public void SetPassengerContext(bool active)
        {
            dialogue?.SetVehicleTravelActive(active);
            if (!active && Phase == VehicleJourneyPhase.DepartureDialogue)
            {
                Phase = VehicleJourneyPhase.ParkedOffice;
                waitingForRewardSpace = sequenceStarted = false;
                rewardBlockedAtItemCount = -1;
            }
        }

        private void OnBoarded()
        {
            Phase = chapter != null && chapter.Progress != null && chapter.Progress.Stage == ChapterOneStage.ReturnToVehicle
                ? VehicleJourneyPhase.StationDepartureDialogue : VehicleJourneyPhase.DepartureDialogue;
            sequenceStarted = false;
            waitingForRewardSpace = false;
            rewardBlockedAtItemCount = -1;
            SetPassengerContext(true);
        }

        private void Update()
        {
            if (GameplaySuspended || Time.deltaTime <= 0 || dialogue == null || dialogue.IsPlaying) return;
            if (Phase == VehicleJourneyPhase.DepartureDialogue && !sequenceStarted) PlayDeparture();
            else if (Phase == VehicleJourneyPhase.OnRoad && roadConversationPending)
            {
                roadConversationPending = false;
                dialogue.Play(roadDialogue, null);
            }
            else if (Phase == VehicleJourneyPhase.FuelDialogue && !sequenceStarted)
            {
                sequenceStarted = dialogue.Play(fuelDialogue, BeginParking);
                if (!sequenceStarted) BeginParking();
            }
            else if (Phase == VehicleJourneyPhase.ParkedStation && seats.IsOccupied)
                seats.ForceExitAtStation(); // Wait safely if a pedestrian is standing at the exit.
            else if (Phase == VehicleJourneyPhase.StationDepartureDialogue && !sequenceStarted && fuelStation != null)
                sequenceStarted = fuelStation.TryBeginDeparture(BeginStationDeparture);
            else if (Phase == VehicleJourneyPhase.ChapterFinished && !sequenceStarted && fuelStation != null)
            {
                sequenceStarted = true;
                fuelStation.FinishChapter();
            }
            if ((Phase == VehicleJourneyPhase.OnRoad || Phase == VehicleJourneyPhase.ToParking || Phase == VehicleJourneyPhase.LeavingStation)
                && !driver.IsMoving && driver.Distance >= CurrentRoute().Length)
                OnArrived();
        }

        private void PlayDeparture()
        {
            if (sequenceStarted || dialogue == null || dialogue.IsPlaying || chapter == null || chapter.Progress == null) return;
            bool sharedMeal = chapter.Progress.Story.SharedMealQuest == StoryQuestStatus.Completed;
            if (sharedMeal)
            {
                chapter.TryApplyStoryEvent(ChapterOneStoryEvent.AtmVisited);
                chapter.TryApplyStoryEvent(ChapterOneStoryEvent.TravelCashDistributed);
            }
            else if (!chapter.Progress.Story.NasiUdukReceived)
            {
                if (nasiUduk == null || chapter.PlayerInventory == null) return;
                InventoryState bag = chapter.PlayerInventory.State;
                if (rewardBlockedAtItemCount == bag.Items.Count && bag.Items.Count >= bag.Capacity) return;
                InventoryEntry entry = nasiUduk.CreateEntry();
                if (!chapter.PlayerInventory.TryAdd(entry))
                {
                    // Wait for space or let MC exit. Never leave a reward behind on departure.
                    waitingForRewardSpace = true;
                    rewardBlockedAtItemCount = chapter.PlayerInventory.State.Items.Count;
                    return;
                }
                waitingForRewardSpace = false;
                rewardBlockedAtItemCount = -1;
                chapter.TryApplyStoryEvent(ChapterOneStoryEvent.NasiUdukReceived);
            }
            sequenceStarted = dialogue.Play(sharedMeal ? departureMealComplete : departureMealSkipped, BeginRoad);
            if (!sequenceStarted) BeginRoad();
        }

        private void BeginRoad()
        {
            if (GameplaySuspended || !seats.IsOccupied) { sequenceStarted = false; return; }
            chapter.TryApplyStoryEvent(ChapterOneStoryEvent.DepartureStarted);
            if (chapter.Progress.Story.DepartureDriver == ChapterOneDepartureDriver.Unassigned) return;
            Phase = VehicleJourneyPhase.OnRoad;
            driver.Begin(false);
            roadConversationPending = true;
            chapter.TryStartVehicleJourney();
        }

        private void OnArrived()
        {
            if (Phase == VehicleJourneyPhase.OnRoad)
            {
                Phase = VehicleJourneyPhase.FuelDialogue;
                sequenceStarted = false;
                roadConversationPending = false;
            }
            else if (Phase == VehicleJourneyPhase.ToParking)
            {
                Phase = VehicleJourneyPhase.ParkedStation;
                seats.RefreshPassengers();
                seats.ForceExitAtStation();
                chapter.TryParkAtFuelStation();
            }
            else if (Phase == VehicleJourneyPhase.LeavingStation)
            {
                Phase = VehicleJourneyPhase.ChapterFinished;
                sequenceStarted = false;
                fuelStation?.FinishChapter();
                sequenceStarted = fuelStation != null;
            }
        }

        private void BeginParking()
        {
            if (GameplaySuspended) { sequenceStarted = false; return; }
            Phase = VehicleJourneyPhase.ToParking;
            driver.Begin(true);
        }

        private void BeginStationDeparture()
        {
            if (GameplaySuspended || !seats.IsOccupied || chapter == null)
            {
                sequenceStarted = false;
                return;
            }
            // Publish the outgoing route together with the chapter checkpoint.
            Phase = VehicleJourneyPhase.LeavingStation;
            if (!chapter.TryStartStationDeparture())
            {
                Phase = VehicleJourneyPhase.StationDepartureDialogue;
                sequenceStarted = false;
                return;
            }
            driver.BeginStationDeparture();
        }

        private VehicleRoutePath CurrentRoute()
            => Phase == VehicleJourneyPhase.LeavingStation || Phase == VehicleJourneyPhase.ChapterFinished
                ? driver.BuildStationDeparturePath()
                : driver.BuildPath(Phase == VehicleJourneyPhase.ToParking || Phase == VehicleJourneyPhase.ParkedStation
                    || Phase == VehicleJourneyPhase.StationDepartureDialogue);

        public SavedVehicleJourney Capture()
            => new SavedVehicleJourney { version = 1, phase = Phase, routeDistance = CaptureRouteDistance(),
                playerSeated = seats != null && seats.IsOccupied, keyUnlocked = seats != null && seats.KeyLock != null && !seats.KeyLock.IsLocked };

        private float CaptureRouteDistance()
        {
            if (driver == null) return 0f;
            // ProgressChanged can save synchronously before BeginStationDeparture resets the previous cursor.
            if (Phase == VehicleJourneyPhase.LeavingStation && !driver.IsStationDepartureRoute) return 0f;
            return driver.Distance;
        }

        public void PrepareRestore()
        {
            ResolveDependencies();
            driver.Stop();
            seats.PrepareRestore();
            sequenceStarted = roadConversationPending = false;
        }

        public void Restore(SavedVehicleJourney saved)
        {
            ResolveDependencies();
            Phase = saved != null && saved.version == 1 ? saved.phase : VehicleJourneyPhase.ParkedOffice;
            if (saved == null || saved.version == 0)
            {
                if (chapter.Progress.Stage == ChapterOneStage.TravelToFuelStation) Phase = VehicleJourneyPhase.OnRoad;
                if (chapter.Progress.Stage == ChapterOneStage.AtFuelStation) Phase = VehicleJourneyPhase.ParkedStation;
                if (chapter.Progress.Stage == ChapterOneStage.ReturnToVehicle) Phase = VehicleJourneyPhase.ParkedStation;
                if (chapter.Progress.Stage == ChapterOneStage.LeavingFuelStation) Phase = VehicleJourneyPhase.LeavingStation;
                if (chapter.Progress.Stage == ChapterOneStage.Complete && chapter.Progress.Story.FuelStation.Complete)
                    Phase = VehicleJourneyPhase.ChapterFinished;
            }
            bool parking = Phase == VehicleJourneyPhase.ToParking || Phase == VehicleJourneyPhase.ParkedStation
                || Phase == VehicleJourneyPhase.StationDepartureDialogue;
            if (Phase == VehicleJourneyPhase.ParkedOffice || Phase == VehicleJourneyPhase.DepartureDialogue)
            {
                Rigidbody body = GetComponent<Rigidbody>();
                transform.SetPositionAndRotation(officePosition, Quaternion.Euler(officeEuler));
                if (body != null) { body.position = transform.position; body.rotation = transform.rotation; }
            }
            else
            {
                VehicleRoutePath route = CurrentRoute();
                float position = Phase == VehicleJourneyPhase.ParkedStation || Phase == VehicleJourneyPhase.FuelDialogue
                    || Phase == VehicleJourneyPhase.StationDepartureDialogue || Phase == VehicleJourneyPhase.ChapterFinished
                    ? route.Length : saved != null && saved.version == 1 ? Mathf.Min(saved.routeDistance, route.Length) : 0;
                if (Phase == VehicleJourneyPhase.LeavingStation || Phase == VehicleJourneyPhase.ChapterFinished)
                    driver.BeginStationDeparture(position);
                else driver.Begin(parking, position);
                if (Phase != VehicleJourneyPhase.OnRoad && Phase != VehicleJourneyPhase.ToParking && Phase != VehicleJourneyPhase.LeavingStation)
                    driver.Stop();
            }
            seats.KeyLock?.RestoreSavedState(saved != null && saved.version == 1 && saved.keyUnlocked);
            bool occupied = saved != null && saved.version == 1 ? saved.playerSeated
                : Phase != VehicleJourneyPhase.ParkedOffice && Phase != VehicleJourneyPhase.ParkedStation;
            if (occupied) seats.RestoreOccupants(player);
            SetPassengerContext(occupied);
            roadConversationPending = Phase == VehicleJourneyPhase.OnRoad;
            sequenceStarted = false;
        }
    }
}
