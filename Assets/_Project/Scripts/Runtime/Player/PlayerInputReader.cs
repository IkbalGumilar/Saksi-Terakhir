using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SaksiTerakhir.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private const string PlayerMapName = "Player";
        private const string MoveActionName = "Move";
        private const string LookActionName = "Look";
        private const string SprintActionName = "Sprint";
        private const string CrouchActionName = "Crouch";
        private const string JumpActionName = "Jump";

        [SerializeField] private InputActionAsset inputActions;

        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction jumpAction;

        public event Action JumpPerformed;
        public event Action CrouchPerformed;
        public event Action CrouchCanceled;

        public Vector2 MoveValue { get; private set; }
        public Vector2 LookValue { get; private set; }
        public bool IsSprintHeld { get; private set; }
        public bool IsLookFromPointer { get; private set; }

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerInputReader)} on '{gameObject.name}' has no Input Action Asset assigned.",
                    this);
                enabled = false;
                return;
            }

            playerMap = inputActions.FindActionMap(PlayerMapName, false);
            if (playerMap == null)
            {
                Debug.LogError($"Action map '{PlayerMapName}' was not found in '{inputActions.name}'.", this);
                enabled = false;
                return;
            }

            moveAction = ResolveAction(MoveActionName);
            lookAction = ResolveAction(LookActionName);
            sprintAction = ResolveAction(SprintActionName);
            crouchAction = ResolveAction(CrouchActionName);
            jumpAction = ResolveAction(JumpActionName);

            enabled = moveAction != null
                && lookAction != null
                && sprintAction != null
                && crouchAction != null
                && jumpAction != null;
        }

        private void OnEnable()
        {
            playerMap.Enable();
            jumpAction.performed += OnJumpPerformed;
            crouchAction.performed += OnCrouchPerformed;
            crouchAction.canceled += OnCrouchCanceled;
        }

        private void OnDisable()
        {
            jumpAction.performed -= OnJumpPerformed;
            crouchAction.performed -= OnCrouchPerformed;
            crouchAction.canceled -= OnCrouchCanceled;
            playerMap.Disable();

            MoveValue = Vector2.zero;
            LookValue = Vector2.zero;
            IsSprintHeld = false;
        }

        private void Update()
        {
            MoveValue = moveAction.ReadValue<Vector2>();
            LookValue = lookAction.ReadValue<Vector2>();
            IsSprintHeld = sprintAction.IsPressed();

            InputControl activeLookControl = lookAction.activeControl;
            if (activeLookControl != null)
            {
                IsLookFromPointer = activeLookControl.device is Pointer;
            }
        }

        private InputAction ResolveAction(string actionName)
        {
            InputAction action = playerMap.FindAction(actionName, false);
            if (action == null)
            {
                Debug.LogError(
                    $"Action '{actionName}' was not found in map '{PlayerMapName}' of '{inputActions.name}'.",
                    this);
            }

            return action;
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            JumpPerformed?.Invoke();
        }

        private void OnCrouchPerformed(InputAction.CallbackContext context)
        {
            CrouchPerformed?.Invoke();
        }

        private void OnCrouchCanceled(InputAction.CallbackContext context)
        {
            CrouchCanceled?.Invoke();
        }
    }
}
