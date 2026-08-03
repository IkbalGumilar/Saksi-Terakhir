using UnityEngine;

namespace SaksiTerakhir.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAnimatorDriver : MonoBehaviour
    {
        public const string MoveXParameter = "MoveX";
        public const string MoveYParameter = "MoveY";
        public const string SpeedParameter = "Speed";
        public const string CrouchParameter = "IsCrouching";

        private static readonly int MoveXId = Animator.StringToHash(MoveXParameter);
        private static readonly int MoveYId = Animator.StringToHash(MoveYParameter);
        private static readonly int SpeedId = Animator.StringToHash(SpeedParameter);
        private static readonly int CrouchId = Animator.StringToHash(CrouchParameter);

        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerController body;
        [SerializeField] private Animator animator;

        [Header("Gait Magnitude")]
        [SerializeField, Range(0f, 1f)] private float crouchGait = 0.6f;
        [SerializeField, Range(0f, 1f)] private float walkGait = 1f;
        [SerializeField, Range(0f, 1f)] private float sprintGait = 1f;

        [Header("Smoothing")]
        [SerializeField] private float damping = 0.12f;

        private void Awake()
        {
            if (input == null || body == null || animator == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerAnimatorDriver)} on '{gameObject.name}' is missing an input "
                    + "reader, body, or animator.",
                    this);
                enabled = false;
            }
        }

        private void Update()
        {
            Vector2 move = Vector2.ClampMagnitude(input.MoveValue, 1f) * ResolveGait();
            animator.SetFloat(MoveXId, move.x, damping, Time.deltaTime);
            animator.SetFloat(MoveYId, move.y, damping, Time.deltaTime);
            animator.SetFloat(SpeedId, body.CurrentSpeed);
            animator.SetBool(CrouchId, body.IsCrouching);
        }

        private float ResolveGait()
        {
            if (body.IsCrouching)
            {
                return crouchGait;
            }

            return body.IsSprinting ? sprintGait : walkGait;
        }
    }
}
