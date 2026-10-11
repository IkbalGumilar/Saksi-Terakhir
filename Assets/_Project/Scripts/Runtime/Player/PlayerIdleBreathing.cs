using UnityEngine;

namespace SaksiTerakhir.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerIdleBreathing : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.008f)] private float eyeAmplitude = 0.006f;
        [SerializeField, Min(1f)] private float cycleSeconds = 4.2f;
        [SerializeField, Range(0f, 0.35f)] private float chestAmplitudeDegrees = 0.18f;

        private PlayerController player;
        private Animator animator;
        private Transform breathingBone;
        private Transform appliedBone;
        private Quaternion baseBoneRotation;
        private Quaternion appliedBoneRotation;
        private Vector3 previousPosition;
        private float phase;
        private float idleWeight;
        private bool boneOffsetApplied;

        public float EyeOffset { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            ResolveBreathingBone();
            previousPosition = transform.position;
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            idleWeight = 0f;
            EyeOffset = 0f;
        }

        private void Update()
        {
            // Remove our previous addition before the Animator evaluates its next
            // pose. This also keeps paused and culled Animators from accumulating it.
            RestoreBonePose();
        }

        private void LateUpdate()
        {
            float deltaTime = Time.timeScale <= 0f || (player != null && player.UiMovementLocked)
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
            StepBreathing(deltaTime);
        }

        private void OnDisable()
        {
            RestoreBonePose();
            EyeOffset = 0f;
            idleWeight = 0f;
        }

        private void StepBreathing(float deltaTime)
        {
            // LateUpdate may also run when animation evaluation is skipped. The
            // second cleanup makes the addition independent of Animator ownership.
            RestoreBonePose();
            Vector3 displacement = transform.position - previousPosition;
            previousPosition = transform.position;
            // A menu lock freezes the body; a retained cinematic velocity is a
            // request for the suspended walk, not actual motion in the preview.
            bool controllerMoving = player != null && !player.UiMovementLocked
                && (player.CurrentSpeed > 0.05f || player.CinematicVelocity.sqrMagnitude > 0.0025f);
            bool rootMoving = deltaTime > 0f
                && displacement.sqrMagnitude > 0.0025f * deltaTime * deltaTime;
            if (controllerMoving || rootMoving)
            {
                idleWeight = 0f;
                EyeOffset = 0f;
                return;
            }

            deltaTime = Mathf.Max(0f, deltaTime);
            idleWeight = Mathf.MoveTowards(idleWeight, 1f, deltaTime * 3f);
            phase = Mathf.Repeat(phase + deltaTime * (Mathf.PI * 2f / Mathf.Max(1f, cycleSeconds)), Mathf.PI * 2f);
            float breath = Mathf.Sin(phase) * idleWeight;
            EyeOffset = breath * eyeAmplitude;

            if (breathingBone == null) ResolveBreathingBone();
            if (breathingBone == null || chestAmplitudeDegrees <= 0f) return;

            appliedBone = breathingBone;
            baseBoneRotation = appliedBone.localRotation;
            appliedBoneRotation = baseBoneRotation * Quaternion.Euler(breath * chestAmplitudeDegrees, 0f, 0f);
            appliedBone.localRotation = appliedBoneRotation;
            boneOffsetApplied = true;
        }

        private void ResolveBreathingBone()
        {
            if (breathingBone != null) return;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                return;

            breathingBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (breathingBone == null) breathingBone = animator.GetBoneTransform(HumanBodyBones.Spine);
        }

        private void RestoreBonePose()
        {
            if (!boneOffsetApplied) return;
            // Another animation owner may have already written a new pose. Only
            // remove the rotation when the bone still contains our own addition.
            if (appliedBone != null && SameRotation(appliedBone.localRotation, appliedBoneRotation))
                appliedBone.localRotation = baseBoneRotation;

            appliedBone = null;
            boneOffsetApplied = false;
        }

        private static bool SameRotation(Quaternion first, Quaternion second)
        {
            var a = new Vector4(first.x, first.y, first.z, first.w);
            var b = new Vector4(second.x, second.y, second.z, second.w);
            // q and -q represent the same rotation; allow only normalization noise.
            return (a - b).sqrMagnitude < 0.0000000001f || (a + b).sqrMagnitude < 0.0000000001f;
        }
    }
}
