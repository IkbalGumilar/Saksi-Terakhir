using UnityEngine;

namespace SaksiTerakhir.Interaction
{
    [DisallowMultipleComponent]
    public sealed class DoorInteractable : Interactable
    {
        private const float AngleTolerance = 0.01f;

        [Header("Interaction")]
        [SerializeField] private bool interact = true;

        [Header("Door Motion")]
        [Tooltip("Use a negative value to make this door swing in the opposite direction.")]
        [SerializeField, Range(-180f, 180f)] private float openAngle = 92f;
        [SerializeField, Min(1f)] private float swingSpeed = 180f;

        [Header("Prompt Keys")]
        [SerializeField] private string openPromptKey = "interact.door.open";
        [SerializeField] private string closePromptKey = "interact.door.close";

        private Collider doorCollider;
        private Quaternion closedRotation;
        private Vector3 hingeAxis;
        private float currentAngle;
        private float targetAngle;

        public bool IsOpen => Mathf.Abs(targetAngle) > AngleTolerance;

        private void Awake()
        {
            doorCollider = GetComponent<Collider>();
            closedRotation = transform.localRotation;
            hingeAxis = transform.parent != null
                ? transform.parent.InverseTransformDirection(Vector3.up).normalized
                : Vector3.up;
        }

        public override bool CanInteract(Transform actor)
        {
            return interact && isActiveAndEnabled;
        }

        public override void Interact(Transform actor)
        {
            if (!CanInteract(actor))
            {
                return;
            }

            bool opening = !IsOpen;
            targetAngle = opening ? openAngle : 0f;
            if (opening)
            {
                SetDoorColliderEnabled(false);
            }
        }

        private void Update()
        {
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle,
                swingSpeed * Time.deltaTime);

            if (Mathf.Abs(currentAngle - targetAngle) < AngleTolerance)
            {
                currentAngle = targetAngle;
            }

            ApplyRotation();
            SetDoorColliderEnabled(Mathf.Abs(currentAngle) < AngleTolerance
                && Mathf.Abs(targetAngle) < AngleTolerance);
        }

        protected override string ResolvePromptKey()
        {
            return IsOpen ? closePromptKey : openPromptKey;
        }

        private void ApplyRotation()
        {
            transform.localRotation = Quaternion.AngleAxis(currentAngle, hingeAxis)
                * closedRotation;
        }

        private void SetDoorColliderEnabled(bool value)
        {
            if (doorCollider != null && doorCollider.enabled != value)
            {
                doorCollider.enabled = value;
            }
        }
    }
}
