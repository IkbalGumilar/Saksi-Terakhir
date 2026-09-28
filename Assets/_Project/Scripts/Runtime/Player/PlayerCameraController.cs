using UnityEngine;

namespace SaksiTerakhir.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private CharacterController body;
        [SerializeField] private Transform yawTransform;
        [SerializeField] private Transform cameraTransform;

        [Header("Sensitivity")]
        [SerializeField] private float pointerSensitivity = 0.07f;
        [SerializeField] private float stickSensitivity = 130f;

        [Header("Pitch Limits")]
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Eye Height")]
        [SerializeField, Range(0.5f, 1f)] private float eyeHeightRatio = 0.92f;
        [SerializeField, Min(0.5f)] private float seatedEyeHeight = 1.05f;

        [Header("Story Focus")]
        [SerializeField, Min(0.1f)] private float storyFocusSmoothing = 10f;
        [SerializeField] private float storyFocusHeight = 1.35f;

        private float pitch;
        private bool storyLookLocked;
        private bool cinematicLookLocked;
        private bool storySeated;
        private Transform storyFocusTarget;

        public bool IsLookEnabled { get; set; } = true;
        public bool StoryLookLocked => storyLookLocked || cinematicLookLocked;
        public bool StoryDialogueLookLocked => storyLookLocked;
        public bool StoryCinematicLookLocked => cinematicLookLocked;
        public bool IsLookInputAllowed => IsLookEnabled && !StoryLookLocked;
        public Transform StoryFocusTarget => storyFocusTarget;

        private void Awake()
        {
            if (input == null || body == null || yawTransform == null || cameraTransform == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraController)} on '{gameObject.name}' is missing an input reader, body, yaw transform, or camera transform.",
                    this);
                enabled = false;
                return;
            }

            pitch = NormalizeAngle(cameraTransform.localEulerAngles.x);
        }

        private void OnEnable()
        {
            SetCursorLocked(true);
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private void LateUpdate()
        {
            if (StoryLookLocked && storyFocusTarget != null)
            {
                ApplyStoryFocus();
            }
            else if (IsLookInputAllowed)
            {
                ApplyLook();
            }

            ApplyEyeHeight();
        }

        public void SetCursorLocked(bool isLocked)
        {
            Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isLocked;
        }

        // Dialogue and cinematic focus deliberately have separate locks. This
        // prevents a dialogue ending from restoring mouse look during an escort.
        public void SetStoryLookLocked(bool locked) => storyLookLocked = locked;

        public void SetCinematicLookLocked(bool locked) => cinematicLookLocked = locked;

        public void SetStoryFocus(Transform target) => storyFocusTarget = target;

        public void ClearStoryFocus() => storyFocusTarget = null;

        public void SetStorySeated(bool seated)
        {
            storySeated = seated;
            ApplyEyeHeight();
        }

        private void ApplyLook()
        {
            Vector2 lookDelta = input.LookValue;
            if (lookDelta.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            float scale = input.IsLookFromPointer
                ? pointerSensitivity
                : stickSensitivity * Time.deltaTime;

            yawTransform.Rotate(0f, lookDelta.x * scale, 0f, Space.Self);
            pitch = Mathf.Clamp(pitch - lookDelta.y * scale, minPitch, maxPitch);
        }

        private void ApplyEyeHeight()
        {
            if (body == null || cameraTransform == null) return;
            Vector3 localPosition = cameraTransform.localPosition;
            localPosition.y = storySeated ? seatedEyeHeight : body.height * eyeHeightRatio;
            cameraTransform.localPosition = localPosition;
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void ApplyStoryFocus()
        {
            if (storyFocusTarget == null || yawTransform == null || cameraTransform == null) return;

            Vector3 focusPoint = storyFocusTarget.position + Vector3.up * storyFocusHeight;
            Vector3 direction = focusPoint - cameraTransform.position;
            if (direction.sqrMagnitude <= 0.0001f) return;

            Vector3 horizontalDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            float blend = 1f - Mathf.Exp(-storyFocusSmoothing * Time.deltaTime);
            if (horizontalDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetYaw = Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
                yawTransform.rotation = Quaternion.Slerp(yawTransform.rotation, targetYaw, blend);
            }

            Vector3 localDirection = Quaternion.Inverse(yawTransform.rotation) * direction.normalized;
            float flatMagnitude = new Vector2(localDirection.x, localDirection.z).magnitude;
            float targetPitch = -Mathf.Atan2(localDirection.y, flatMagnitude) * Mathf.Rad2Deg;
            pitch = Mathf.LerpAngle(pitch, Mathf.Clamp(targetPitch, minPitch, maxPitch), blend);
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
