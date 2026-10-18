using UnityEngine;
using SaksiTerakhir.Interaction;

namespace SaksiTerakhir.Environment
{
    public enum ElevatorButtonType
    {
        Call,
        Floor
    }

    [DisallowMultipleComponent]
    public sealed class ElevatorButton : Interactable
    {
        private const string InteractableLayerName = "Interactable";

        [Header("Elevator Link")]
        [SerializeField] private OfficeElevator elevator;
        [SerializeField] private ElevatorButtonType buttonType = ElevatorButtonType.Call;
        [SerializeField] private int targetFloor = 0;
        [SerializeField] private string customPromptKey;

        private bool elevatorLookupAttempted;

        public int TargetFloor => targetFloor;
        public ElevatorButtonType ButtonType => buttonType;

        private void Awake()
        {
            int interactableLayer = LayerMask.NameToLayer(InteractableLayerName);
            if (interactableLayer < 0)
            {
                Debug.LogError($"Layer '{InteractableLayerName}' is missing; elevator buttons cannot be targeted by the player.", this);
                return;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                Debug.LogWarning("Elevator button has no collider, so the player cannot target it.", this);
                return;
            }

            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].gameObject.layer = interactableLayer;
                }
            }
        }

        public void Configure(OfficeElevator elev, ElevatorButtonType type, int floor, string promptKey = null)
        {
            elevator = elev;
            buttonType = type;
            targetFloor = floor;
            customPromptKey = promptKey;
            elevatorLookupAttempted = elev != null;
        }

        public override bool CanInteract(Transform actor)
        {
            OfficeElevator targetElevator = ResolveElevator();
            if (!base.CanInteract(actor) || targetElevator == null) return false;
            if (targetElevator.State == ElevatorState.Moving) return false;
            return true;
        }

        public override void Interact(Transform actor)
        {
            OfficeElevator targetElevator = ResolveElevator();
            if (targetElevator == null || !CanInteract(actor)) return;

            if (buttonType == ElevatorButtonType.Call)
            {
                targetElevator.CallElevator(targetFloor);
            }
            else
            {
                targetElevator.SelectFloor(targetFloor);
            }
        }

        private OfficeElevator ResolveElevator()
        {
            if (elevator != null || elevatorLookupAttempted)
            {
                return elevator;
            }

            elevatorLookupAttempted = true;
            elevator = GetComponentInParent<OfficeElevator>();
            if (elevator == null)
            {
                elevator = FindFirstObjectByType<OfficeElevator>();
            }

            return elevator;
        }

        protected override string ResolvePromptKey()
        {
            if (!string.IsNullOrEmpty(customPromptKey))
            {
                return customPromptKey;
            }

            if (buttonType == ElevatorButtonType.Call)
            {
                return "interact.elevator.call";
            }

            switch (targetFloor)
            {
                case 0: return "interact.elevator.floor_1";
                case 1: return "interact.elevator.floor_2";
                case 2: return "interact.elevator.floor_3";
                case 3: return "interact.elevator.floor_4";
                case 4: return "interact.elevator.floor_r";
                default: return "interact.generic";
            }
        }
    }
}
