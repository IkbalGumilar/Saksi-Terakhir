using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.Environment
{
    public enum ElevatorState
    {
        Idle,           // Doors closed, stationary, waiting for button press
        DoorsOpening,   // Doors currently sliding open
        DoorsOpen,      // Doors fully open, 5-second countdown active
        DoorsClosing,   // Doors currently sliding closed
        Moving          // Doors fully closed, traveling vertically to target floor
    }

    [System.Serializable]
    public struct ElevatorFloor
    {
        public string name;
        public float height;
        public Transform landingLeftDoor;
        public Transform landingRightDoor;
    }

    [DisallowMultipleComponent]
    public sealed class OfficeElevator : MonoBehaviour
    {
        private const float AutoCloseDelay = 5.0f;
        private const float DefaultSlideDistance = 0.85f;
        private const string CabinRootName = "Kabin_Lift_PROTOTIPE";
        private const string CabinLeftDoorName = "Pintu_Kabin_Kiri";
        private const string CabinRightDoorName = "Pintu_Kabin_Kanan";
        private const string LandingLeftDoorName = "DoorSetLeft_SlidePivot";
        private const string LandingRightDoorName = "DoorSetRight_SlidePivot";

        [Header("Cabin & Movement")]
        [SerializeField] private Transform cabinTransform;
        [SerializeField] private float moveSpeed = 2.5f;

        [Header("Doors")]
        [SerializeField] private Transform cabinLeftDoor;
        [SerializeField] private Transform cabinRightDoor;
        [SerializeField] private float slideDistance = DefaultSlideDistance;
        [SerializeField] private float doorOpenSpeed = 1.0f; // seconds to full open/close

        [Header("Floors Configuration")]
        [SerializeField] private List<ElevatorFloor> floors = new List<ElevatorFloor>
        {
            new ElevatorFloor { name = "Lantai 1 - Lobi", height = 0.0f },
            new ElevatorFloor { name = "Lantai 2 - Pegawai", height = 4.2f },
            new ElevatorFloor { name = "Lantai 3 - Pimpinan", height = 8.0f },
            new ElevatorFloor { name = "Lantai 4 - Arsip", height = 11.8f },
            new ElevatorFloor { name = "Rooftop - Atap", height = 15.6f }
        };

        [Header("Optional Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip arrivalChime;
        [SerializeField] private AudioClip doorMoveSound;

        private int currentFloor = 0;
        private int targetFloor = 0;
        private ElevatorState state = ElevatorState.Idle;
        private float doorFraction = 0.0f; // 0 = closed, 1 = fully open
        private float doorTimer = 0.0f;

        // Initial local door positions
        private Vector3 cabinLeftClosedPos;
        private Vector3 cabinRightClosedPos;
        private readonly List<Vector3> landingLeftClosedPos = new List<Vector3>();
        private readonly List<Vector3> landingRightClosedPos = new List<Vector3>();

        // Player movement synchronization
        private readonly HashSet<CharacterController> occupants = new HashSet<CharacterController>();
        private Vector3 lastCabinPosition;

        public int CurrentFloor => currentFloor;
        public int TargetFloor => targetFloor;
        public ElevatorState State => state;
        public bool AreDoorsOpen => state == ElevatorState.DoorsOpen;

        private void Awake()
        {
            ResolveCabinReferences();
            ResolveLandingDoorReferences();

            // Cache cabin doors closed positions
            if (cabinLeftDoor != null) cabinLeftClosedPos = cabinLeftDoor.localPosition;
            if (cabinRightDoor != null) cabinRightClosedPos = cabinRightDoor.localPosition;

            // Cache landing doors closed positions
            landingLeftClosedPos.Clear();
            landingRightClosedPos.Clear();
            for (int i = 0; i < floors.Count; i++)
            {
                landingLeftClosedPos.Add(floors[i].landingLeftDoor != null ? floors[i].landingLeftDoor.localPosition : Vector3.zero);
                landingRightClosedPos.Add(floors[i].landingRightDoor != null ? floors[i].landingRightDoor.localPosition : Vector3.zero);
            }

            lastCabinPosition = cabinTransform.position;
        }

        private void ResolveCabinReferences()
        {
            Transform searchRoot = transform.root;
            if (cabinTransform == null || cabinTransform.name != CabinRootName)
            {
                cabinTransform = FindDescendant(searchRoot, CabinRootName);
            }

            if (cabinTransform == null)
            {
                cabinTransform = transform;
            }

            if (cabinLeftDoor == null || cabinLeftDoor.name != CabinLeftDoorName)
            {
                cabinLeftDoor = FindDescendant(cabinTransform, CabinLeftDoorName);
            }

            if (cabinRightDoor == null || cabinRightDoor.name != CabinRightDoorName)
            {
                cabinRightDoor = FindDescendant(cabinTransform, CabinRightDoorName);
            }
        }

        private void ResolveLandingDoorReferences()
        {
            Transform[] transforms = transform.root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < floors.Count; index++)
            {
                ElevatorFloor floor = floors[index];
                float expectedHeight = floor.height;

                if (!HasName(floor.landingLeftDoor, LandingLeftDoorName))
                {
                    floor.landingLeftDoor = FindClosestLandingDoor(transforms, LandingLeftDoorName, expectedHeight);
                }

                if (!HasName(floor.landingRightDoor, LandingRightDoorName))
                {
                    floor.landingRightDoor = FindClosestLandingDoor(transforms, LandingRightDoorName, expectedHeight);
                }

                floors[index] = floor;
            }
        }

        private static Transform FindClosestLandingDoor(Transform[] transforms, string namePart, float expectedHeight)
        {
            Transform closest = null;
            float closestHeightDifference = float.MaxValue;

            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (!HasName(candidate, namePart) || candidate.name.Contains("_Mesh"))
                {
                    continue;
                }

                float heightDifference = Mathf.Abs(candidate.position.y - expectedHeight);
                if (heightDifference >= closestHeightDifference)
                {
                    continue;
                }

                closest = candidate;
                closestHeightDifference = heightDifference;
            }

            return closestHeightDifference <= 1.5f ? closest : null;
        }

        private static Transform FindDescendant(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == targetName)
                {
                    return transforms[index];
                }
            }

            return null;
        }

        private static bool HasName(Transform target, string name)
        {
            return target != null && target.name.Contains(name);
        }

        private void Start()
        {
            // Snap cabin to starting floor
            if (currentFloor >= 0 && currentFloor < floors.Count)
            {
                Vector3 pos = cabinTransform.position;
                pos.y = floors[currentFloor].height;
                cabinTransform.position = pos;
            }

            // Ensure all doors start fully closed
            doorFraction = 0.0f;
            ApplyDoorPositions(0.0f);
            state = ElevatorState.Idle;
        }

        public void Configure(Transform cabin, Transform leftDoor, Transform rightDoor, List<ElevatorFloor> floorList)
        {
            cabinTransform = cabin;
            cabinLeftDoor = leftDoor;
            cabinRightDoor = rightDoor;
            floors = floorList;
            Awake();
        }

        /// <summary>Called when a player interacts with an outside hall call button.</summary>
        public void CallElevator(int floorIndex)
        {
            if (floorIndex < 0 || floorIndex >= floors.Count) return;

            // If already at this floor
            if (currentFloor == floorIndex)
            {
                if (state == ElevatorState.Idle || state == ElevatorState.DoorsClosing)
                {
                    OpenDoors();
                }
                else if (state == ElevatorState.DoorsOpen)
                {
                    // Reset 5-second timer
                    doorTimer = AutoCloseDelay;
                }
                return;
            }

            // At another floor: close doors first, then travel
            targetFloor = floorIndex;
            if (state == ElevatorState.DoorsOpen || state == ElevatorState.DoorsOpening)
            {
                CloseDoors();
            }
            else if (state == ElevatorState.Idle)
            {
                state = ElevatorState.Moving;
            }
        }

        /// <summary>Called when a player presses any floor button inside the cabin.</summary>
        public void SelectFloor(int floorIndex)
        {
            if (floorIndex < 0 || floorIndex >= floors.Count) return;

            // If player selected the floor the elevator is already on
            if (currentFloor == floorIndex)
            {
                if (state == ElevatorState.Idle || state == ElevatorState.DoorsClosing)
                {
                    OpenDoors();
                }
                else if (state == ElevatorState.DoorsOpen)
                {
                    doorTimer = AutoCloseDelay;
                }
                return;
            }

            targetFloor = floorIndex;

            // Rule: "pemain menekan tombol manapun disana lift tertutup sepenuhnya dulu lalu naik atau turun"
            if (state == ElevatorState.DoorsOpen || state == ElevatorState.DoorsOpening)
            {
                CloseDoors();
            }
            else if (state == ElevatorState.Idle)
            {
                state = ElevatorState.Moving;
            }
        }

        private void OpenDoors()
        {
            state = ElevatorState.DoorsOpening;
            PlaySound(doorMoveSound);
        }

        private void CloseDoors()
        {
            state = ElevatorState.DoorsClosing;
            PlaySound(doorMoveSound);
        }

        private void Update()
        {
            switch (state)
            {
                case ElevatorState.DoorsOpening:
                    doorFraction = Mathf.MoveTowards(doorFraction, 1.0f, (1.0f / Mathf.Max(0.05f, doorOpenSpeed)) * Time.deltaTime);
                    ApplyDoorPositions(doorFraction);

                    // Rule: "waktu lift tertutup di mulai saat lift terbuka sepenuhnya"
                    if (doorFraction >= 1.0f)
                    {
                        doorFraction = 1.0f;
                        state = ElevatorState.DoorsOpen;
                        doorTimer = AutoCloseDelay; // Start 5 second countdown
                    }
                    break;

                case ElevatorState.DoorsOpen:
                    // Rule: "lift tertutup otomatis setelah 5 detik dan harus di panggil lagi untuk buka"
                    doorTimer -= Time.deltaTime;
                    if (doorTimer <= 0.0f)
                    {
                        CloseDoors();
                    }
                    break;

                case ElevatorState.DoorsClosing:
                    doorFraction = Mathf.MoveTowards(doorFraction, 0.0f, (1.0f / Mathf.Max(0.05f, doorOpenSpeed)) * Time.deltaTime);
                    ApplyDoorPositions(doorFraction);

                    // When doors reach fully closed
                    if (doorFraction <= 0.0f)
                    {
                        doorFraction = 0.0f;

                        // If player chose a target floor, move now that doors are fully closed
                        if (targetFloor != currentFloor)
                        {
                            state = ElevatorState.Moving;
                        }
                        else
                        {
                            state = ElevatorState.Idle; // Must be called again to open
                        }
                    }
                    break;

                case ElevatorState.Moving:
                    UpdateMoving();
                    break;
            }
        }

        private void UpdateMoving()
        {
            if (cabinTransform == null || targetFloor < 0 || targetFloor >= floors.Count) return;

            float targetY = floors[targetFloor].height;
            Vector3 pos = cabinTransform.position;
            pos.y = Mathf.MoveTowards(pos.y, targetY, moveSpeed * Time.deltaTime);
            cabinTransform.position = pos;

            // Check if arrived at target floor
            if (Mathf.Abs(pos.y - targetY) < 0.005f)
            {
                pos.y = targetY;
                cabinTransform.position = pos;
                currentFloor = targetFloor;

                PlaySound(arrivalChime);

                // Open doors automatically upon arrival
                OpenDoors();
            }
        }

        private void ApplyDoorPositions(float fraction)
        {
            Vector3 offset = Vector3.right * (slideDistance * fraction);

            // Cabin doors
            if (cabinLeftDoor != null) cabinLeftDoor.localPosition = cabinLeftClosedPos - offset;
            if (cabinRightDoor != null) cabinRightDoor.localPosition = cabinRightClosedPos + offset;

            // Current floor landing doors
            if (currentFloor >= 0 && currentFloor < floors.Count)
            {
                var floor = floors[currentFloor];
                if (floor.landingLeftDoor != null && currentFloor < landingLeftClosedPos.Count)
                {
                    floor.landingLeftDoor.localPosition = landingLeftClosedPos[currentFloor] - offset;
                }
                if (floor.landingRightDoor != null && currentFloor < landingRightClosedPos.Count)
                {
                    floor.landingRightDoor.localPosition = landingRightClosedPos[currentFloor] + offset;
                }
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        // --- Cabin Occupant Tracking & Synchronization ---

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || other.isTrigger) return;
            var cc = other.GetComponentInParent<CharacterController>();
            if (cc != null) occupants.Add(cc);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null) return;
            var cc = other.GetComponentInParent<CharacterController>();
            if (cc != null) occupants.Remove(cc);
        }

        private void LateUpdate()
        {
            if (cabinTransform == null) return;

            Vector3 delta = cabinTransform.position - lastCabinPosition;
            lastCabinPosition = cabinTransform.position;

            if (delta.sqrMagnitude > 0.000001f && occupants.Count > 0)
            {
                occupants.RemoveWhere(cc => cc == null || !cc.gameObject.activeInHierarchy);
                foreach (var cc in occupants)
                {
                    cc.Move(delta);
                }
            }
        }
    }
}
