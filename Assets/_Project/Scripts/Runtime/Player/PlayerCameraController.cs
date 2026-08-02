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

        private float pitch;

        public bool IsLookEnabled { get; set; } = true;

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
            if (IsLookEnabled)
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
            Vector3 localPosition = cameraTransform.localPosition;
            localPosition.y = body.height * eyeHeightRatio;
            cameraTransform.localPosition = localPosition;
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
