using UnityEngine;

namespace SaksiTerakhir.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;

        [Header("Speed")]
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float sprintSpeed = 4.4f;
        [SerializeField] private float crouchSpeed = 1.1f;
        [SerializeField] private float acceleration = 14f;

        [Header("Jump And Gravity")]
        [SerializeField] private float jumpHeight = 0.8f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Crouch")]
        [SerializeField] private bool crouchIsToggle;
        [SerializeField] private float standingHeight = 1.75f;
        [SerializeField] private float crouchHeight = 1.05f;
        [SerializeField] private float crouchTransitionSpeed = 8f;
        [SerializeField] private LayerMask ceilingBlockingLayers = ~0;

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private Vector3 cinematicVelocity;
        private float verticalVelocity;
        private bool isCrouchRequested;
        private bool storyDialogueMovementLocked;
        private bool storyCinematicMovementLocked;

        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public float CurrentSpeed => horizontalVelocity.magnitude;
        public bool StoryMovementLocked => storyDialogueMovementLocked || storyCinematicMovementLocked;
        public bool StoryDialogueMovementLocked => storyDialogueMovementLocked;
        public bool StoryCinematicMovementLocked => storyCinematicMovementLocked;
        public Vector3 CinematicVelocity => cinematicVelocity;

        // Dialogue owns this lock. Cinematic movement has a separate owner so a
        // dialogue may finish without handing input back during an escort.
        public void SetStoryMovementLocked(bool locked)
        {
            storyDialogueMovementLocked = locked;
            if (locked)
            {
                StopManualMotion();
            }
        }

        public void SetCinematicMovementLocked(bool locked)
        {
            if (storyCinematicMovementLocked == locked) return;
            storyCinematicMovementLocked = locked;
            if (locked)
            {
                StopManualMotion();
                return;
            }

            cinematicVelocity = Vector3.zero;
            if (!storyDialogueMovementLocked) StopManualMotion();
        }

        public void SetCinematicVelocity(Vector3 worldVelocity)
        {
            if (!storyCinematicMovementLocked) return;
            cinematicVelocity = Vector3.ProjectOnPlane(worldVelocity, Vector3.up);
        }

        public void StopCinematicMotion()
        {
            cinematicVelocity = Vector3.zero;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (input == null)
            {
                Debug.LogError($"{nameof(PlayerController)} on '{gameObject.name}' has no input reader assigned.", this);
                enabled = false;
                return;
            }

            if (crouchHeight > standingHeight)
            {
                crouchHeight = standingHeight;
            }

            ApplyHeight(standingHeight);
        }

        private void OnEnable()
        {
            input.JumpPerformed += OnJumpPerformed;
            input.CrouchPerformed += OnCrouchPerformed;
            input.CrouchCanceled += OnCrouchCanceled;
        }

        private void OnDisable()
        {
            input.JumpPerformed -= OnJumpPerformed;
            input.CrouchPerformed -= OnCrouchPerformed;
            input.CrouchCanceled -= OnCrouchCanceled;

            horizontalVelocity = Vector3.zero;
            cinematicVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }

        private void Update()
        {
            UpdateCrouchState();
            UpdateHorizontalVelocity();
            UpdateVerticalVelocity();

            Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }

        private void UpdateCrouchState()
        {
            float targetHeight;
            if (isCrouchRequested)
            {
                targetHeight = crouchHeight;
            }
            else
            {
                targetHeight = CanStandUp() ? standingHeight : crouchHeight;
            }

            if (!Mathf.Approximately(controller.height, targetHeight))
            {
                float nextHeight = Mathf.MoveTowards(
                    controller.height,
                    targetHeight,
                    crouchTransitionSpeed * Time.deltaTime);
                ApplyHeight(nextHeight);
            }

            IsCrouching = controller.height < standingHeight - 0.01f;
        }

        private void UpdateHorizontalVelocity()
        {
            if (storyCinematicMovementLocked)
            {
                horizontalVelocity = cinematicVelocity;
                IsSprinting = false;
                return;
            }

            if (storyDialogueMovementLocked)
            {
                StopManualMotion();
                return;
            }

            Vector2 moveInput = Vector2.ClampMagnitude(input.MoveValue, 1f);
            Vector3 desiredDirection = transform.right * moveInput.x + transform.forward * moveInput.y;

            IsSprinting = input.IsSprintHeld && !IsCrouching && moveInput.y > 0.1f;

            float targetSpeed;
            if (IsCrouching)
            {
                targetSpeed = crouchSpeed;
            }
            else
            {
                targetSpeed = IsSprinting ? sprintSpeed : walkSpeed;
            }

            Vector3 targetVelocity = desiredDirection * targetSpeed;
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                targetVelocity,
                acceleration * Time.deltaTime);
        }

        private void UpdateVerticalVelocity()
        {
            if (controller.isGrounded && verticalVelocity <= 0f)
            {
                verticalVelocity = groundedStickForce;
                return;
            }

            verticalVelocity += gravity * Time.deltaTime;
        }

        private void ApplyHeight(float height)
        {
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private bool CanStandUp()
        {
            float missingHeight = standingHeight - controller.height;
            if (missingHeight <= 0.01f)
            {
                return true;
            }

            int mask = ceilingBlockingLayers & ~(1 << gameObject.layer);
            float radius = controller.radius * 0.95f;
            Vector3 feet = transform.position + controller.center - Vector3.up * (controller.height * 0.5f);
            Vector3 capsuleBottom = feet + Vector3.up * radius;
            Vector3 capsuleTop = feet + Vector3.up * (standingHeight - radius);

            return !Physics.CheckCapsule(
                capsuleBottom,
                capsuleTop,
                radius,
                mask,
                QueryTriggerInteraction.Ignore);
        }

        private void OnJumpPerformed()
        {
            if (StoryMovementLocked || !controller.isGrounded || IsCrouching)
            {
                return;
            }

            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        private void OnCrouchPerformed()
        {
            if (StoryMovementLocked) return;
            isCrouchRequested = crouchIsToggle ? !isCrouchRequested : true;
        }

        private void OnCrouchCanceled()
        {
            if (!crouchIsToggle)
            {
                isCrouchRequested = false;
            }
        }

        private void StopManualMotion()
        {
            horizontalVelocity = Vector3.zero;
            IsSprinting = false;
        }
    }
}
