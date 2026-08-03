using UnityEngine;

namespace SaksiTerakhir.Player
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonBody : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Range(0.001f, 0.2f)] private float headScale = 0.02f;

        private Transform head;

        private void Awake()
        {
            if (animator == null)
            {
                Debug.LogError(
                    $"{nameof(FirstPersonBody)} on '{gameObject.name}' has no animator assigned.",
                    this);
                enabled = false;
                return;
            }

            head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null)
            {
                Debug.LogError(
                    $"{nameof(FirstPersonBody)} on '{gameObject.name}' found no Head bone. "
                    + "The avatar must be Humanoid with the head mapped.",
                    this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            head.localScale = Vector3.one * headScale;
        }
    }
}
