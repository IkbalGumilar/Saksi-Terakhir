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
        [SerializeField, Range(30f, 180f)] private float seatedYawLimit = 120f;

        [Header("Story Focus")]
        [SerializeField, Min(0.1f)] private float storyFocusSmoothing = 10f;
        [SerializeField] private float storyFocusHeight = 1.35f;

        [Header("Main Menu Preview")]
        [SerializeField, Range(0f, 10f)] private float menuLookAmplitude = 6f;
        [SerializeField, Min(4f)] private float menuLookPeriod = 12f;

        private float pitch;
        private bool storyLookLocked;
        private bool cinematicLookLocked;
        private bool storySeated;
        private float? seatedEyeHeightOverride;
        private float seatedYaw;
        private bool uiLookLocked;
        private Transform storyFocusTarget;
        private PlayerIdleBreathing breathing;
        private bool menuPreview;
        private MenuViewPlanner.View menuView;
        private float menuStartedAt;
        private Vector3 gameplayPosition;
        private Quaternion gameplayRotation;
        private Quaternion gameplayYaw;
        private Vector3 gameplayCameraPosition;
        private bool gameplayControllerEnabled;

        public bool IsLookEnabled { get; set; } = true;
        public bool StoryLookLocked => storyLookLocked || cinematicLookLocked;
        public bool StoryDialogueLookLocked => storyLookLocked;
        public bool StoryCinematicLookLocked => cinematicLookLocked;
        public bool IsLookInputAllowed => IsLookEnabled && !StoryLookLocked && !uiLookLocked;
        public bool UiLookLocked => uiLookLocked;
        public float SavedPitch => pitch;

        public void RestoreSavedPitch(float value)
        {
            pitch = Mathf.Clamp(value, minPitch, maxPitch);
            ApplyEyeHeight();
        }

        public void SetUiLookLocked(bool locked) => uiLookLocked = locked;
        public Transform StoryFocusTarget => storyFocusTarget;

        public void BeginMenuPreview()
        {
            if (menuPreview || !isActiveAndEnabled || body == null || yawTransform == null || cameraTransform == null) return;
            gameplayPosition = body.transform.position;
            gameplayRotation = body.transform.rotation;
            gameplayYaw = yawTransform.rotation;
            gameplayCameraPosition = cameraTransform.localPosition;
            gameplayControllerEnabled = body.enabled;
            menuPreview = true;
            menuStartedAt = Time.unscaledTime;
            float initialYaw = yawTransform.eulerAngles.y;
            menuView = new MenuViewPlanner.View(gameplayPosition, initialYaw, 0f);
            ApplyMenuPose();
            ApplyEyeHeight();
            Camera view = cameraTransform.GetComponentInChildren<Camera>();
            Transform eye = view != null ? view.transform : cameraTransform;
            float horizontalFieldOfView = view != null
                ? 2f * Mathf.Atan(Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f) * view.aspect) * Mathf.Rad2Deg : 80f;
            var seating = GetComponent<SaksiTerakhir.Story.StoryPlayerSeating>();
            menuView = MenuViewPlanner.Choose(body, eye, initialYaw, horizontalFieldOfView,
                menuLookAmplitude, gameplayControllerEnabled && (seating == null || !seating.IsSeated));
            if (menuView.Position != body.transform.position)
            {
                if (body.enabled) body.enabled = false;
                body.transform.position = menuView.Position;
                body.enabled = gameplayControllerEnabled;
            }
            ApplyMenuPose();
            ApplyEyeHeight();
            Physics.SyncTransforms();
        }

        public void EndMenuPreview(bool keepAdjustedPose = true)
        {
            if (!menuPreview) return;
            menuPreview = false;
            if (!keepAdjustedPose && body != null)
            {
                if (body.enabled) body.enabled = false;
                body.transform.SetPositionAndRotation(gameplayPosition, gameplayRotation);
                body.enabled = gameplayControllerEnabled;
            }
            if (keepAdjustedPose) pitch = 0f;
            else if (yawTransform != null) yawTransform.rotation = gameplayYaw;
            if (cameraTransform != null) cameraTransform.localPosition = gameplayCameraPosition;
            ApplyEyeHeight();
            Physics.SyncTransforms();
        }

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
            breathing = GetComponent<PlayerIdleBreathing>();
            if (breathing == null) breathing = gameObject.AddComponent<PlayerIdleBreathing>();
        }

        private Camera cachedCamera;

        private void OnEnable()
        {
            SetCursorLocked(true);
            SaksiTerakhir.Settings.GameSettingsManager.SettingsChanged += OnSettingsChanged;
            UpdateCameraFov();
        }

        private void OnDisable()
        {
            SaksiTerakhir.Settings.GameSettingsManager.SettingsChanged -= OnSettingsChanged;
            EndMenuPreview(false);
            SetCursorLocked(false);
        }

        private void OnSettingsChanged(SaksiTerakhir.Settings.GameSettingsData _)
        {
            UpdateCameraFov();
        }

        private void UpdateCameraFov()
        {
            if (cachedCamera == null && cameraTransform != null)
                cachedCamera = cameraTransform.GetComponentInChildren<Camera>();
            if (cachedCamera != null)
            {
                var settings = SaksiTerakhir.Settings.GameSettingsManager.Current;
                if (settings != null)
                    cachedCamera.fieldOfView = settings.fieldOfView;
            }
        }

        private void LateUpdate()
        {
            if (menuPreview)
            {
                ApplyMenuPose();
                ApplyEyeHeight();
                return;
            }
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

        public void SetStorySeated(bool seated, float eyeHeight = -1f)
        {
            if (storySeated != seated) seatedYaw = 0f;
            storySeated = seated;
            seatedEyeHeightOverride = seated && eyeHeight >= 0.5f ? eyeHeight : (float?)null;
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

            float speedMult = 1f;
            float hSens = 1f;
            float vSens = 1f;
            bool invertY = false;

            if (Application.isPlaying)
            {
                var settings = SaksiTerakhir.Settings.GameSettingsManager.Current;
                if (settings != null)
                {
                    switch (settings.cameraSpeed)
                    {
                        case SaksiTerakhir.Settings.CameraSpeedSetting.Slow: speedMult = 0.75f; break;
                        case SaksiTerakhir.Settings.CameraSpeedSetting.Fast: speedMult = 1.35f; break;
                    }

                    hSens = (settings.horizontalSensitivity / 50f) * speedMult;
                    vSens = (settings.verticalSensitivity / 50f) * speedMult;
                    invertY = settings.invertVerticalAxis;

                    if (input.IsLookFromPointer)
                    {
                        float mouseSens = settings.mouseSensitivity / 50f;
                        hSens *= mouseSens;
                        vSens *= mouseSens;
                    }
                }
            }

            float deltaX = lookDelta.x * scale * hSens;
            float deltaY = lookDelta.y * scale * vSens * (invertY ? -1f : 1f);

            if (storySeated)
                seatedYaw = Mathf.Clamp(seatedYaw + deltaX, -seatedYawLimit, seatedYawLimit);
            else
                yawTransform.Rotate(0f, deltaX, 0f, Space.Self);
            pitch = Mathf.Clamp(pitch - deltaY, minPitch, maxPitch);
        }

        private void ApplyEyeHeight()
        {
            if (body == null || cameraTransform == null) return;
            Vector3 localPosition = cameraTransform.localPosition;
            localPosition.y = (storySeated ? seatedEyeHeightOverride ?? seatedEyeHeight : body.height * eyeHeightRatio)
                + (breathing != null ? breathing.EyeOffset : 0f);
            cameraTransform.localPosition = localPosition;
            cameraTransform.localRotation = Quaternion.Euler(menuPreview ? 0f : pitch,
                storySeated && !menuPreview ? seatedYaw : 0f, 0f);
        }

        private void ApplyMenuPose()
        {
            float phase = (Time.unscaledTime - menuStartedAt) * Mathf.PI * 2f / Mathf.Max(4f, menuLookPeriod);
            yawTransform.rotation = Quaternion.Euler(0f, menuView.Yaw + Mathf.Sin(phase) * menuView.ScanAngle, 0f);
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
