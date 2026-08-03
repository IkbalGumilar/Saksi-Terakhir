using UnityEngine;

namespace SaksiTerakhir.Interaction
{
    [DisallowMultipleComponent]
    public sealed class DoorInteractable : Interactable
    {
        private const float AngleTolerance = 0.01f;
        private const float ObstacleMargin = 0.02f;
        private const float HingeClearance = 0.25f;
        private const int ObstacleBufferSize = 16;

        private static readonly float[] SwingProbeFractions = { 0.5f, 1f };

        [Header("Swing")]
        [SerializeField] private float openAngle = 92f;
        [SerializeField] private float swingSpeed = 260f;
        [SerializeField] private LayerMask obstacleLayers = ~0;

        [Header("State")]
        [SerializeField] private bool isLocked;
        [SerializeField] private bool startsOpen;

        [Header("Prompt Keys")]
        [SerializeField] private string openPromptKey = "interact.door.open";
        [SerializeField] private string closePromptKey = "interact.door.close";
        [SerializeField] private string lockedPromptKey = "interact.door.locked";

        private readonly Collider[] obstacleBuffer = new Collider[ObstacleBufferSize];

        private Quaternion closedRotation;
        private Vector3 hingeAxis;
        private Vector3 swingDirection;
        private Vector3 probeCentre;
        private Vector3 probeExtents;
        private float currentAngle;
        private float targetAngle;

        public bool IsOpen => Mathf.Abs(targetAngle) > AngleTolerance;

        public bool IsLocked
        {
            get => isLocked;
            set => isLocked = value;
        }

        private void Awake()
        {
            closedRotation = transform.localRotation;
            hingeAxis = transform.parent != null
                ? transform.parent.InverseTransformDirection(Vector3.up).normalized
                : Vector3.up;

            Renderer leafRenderer = GetComponent<Renderer>();
            Vector3 leaf = leafRenderer != null
                ? leafRenderer.bounds.center - transform.position
                : transform.right;
            leaf.y = 0f;
            swingDirection = leaf.sqrMagnitude > Mathf.Epsilon
                ? Vector3.Cross(Vector3.up, leaf.normalized)
                : Vector3.forward;

            BuildSwingProbe();

            if (startsOpen)
            {
                targetAngle = openAngle;
                currentAngle = openAngle;
                ApplyRotation();
            }
        }

        public override bool CanInteract(Transform actor)
        {
            return isActiveAndEnabled;
        }

        public override void Interact(Transform actor)
        {
            if (isLocked)
            {
                return;
            }

            if (IsOpen)
            {
                targetAngle = 0f;
                return;
            }

            float side = Vector3.Dot(actor.position - transform.position, swingDirection);
            float preferred = side > 0f ? -openAngle : openAngle;
            if (!IsSwingBlocked(preferred))
            {
                targetAngle = preferred;
                return;
            }

            if (!IsSwingBlocked(-preferred))
            {
                targetAngle = -preferred;
            }
        }

        private void Update()
        {
            if (Mathf.Abs(currentAngle - targetAngle) < AngleTolerance)
            {
                return;
            }

            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle,
                swingSpeed * Time.deltaTime);
            ApplyRotation();
        }

        protected override string ResolvePromptKey()
        {
            if (isLocked)
            {
                return lockedPromptKey;
            }

            return IsOpen ? closePromptKey : openPromptKey;
        }

        private void BuildSwingProbe()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            Bounds local = filter != null && filter.sharedMesh != null
                ? filter.sharedMesh.bounds
                : new Bounds(Vector3.zero, Vector3.one * 0.1f);

            Vector3 centre = local.center;
            Vector3 extents = local.extents;
            int alongAxis = MiddleAxis(extents);
            float far = centre[alongAxis] + Mathf.Sign(centre[alongAxis]) * extents[alongAxis];
            float near = far * HingeClearance;
            centre[alongAxis] = (near + far) * 0.5f;
            extents[alongAxis] = Mathf.Abs(far - near) * 0.5f;

            probeCentre = centre;
            probeExtents = Vector3.Max(extents - Vector3.one * ObstacleMargin,
                Vector3.one * 0.01f);
        }

        private static int MiddleAxis(Vector3 extents)
        {
            int largest = extents.x >= extents.y
                ? (extents.x >= extents.z ? 0 : 2)
                : (extents.y >= extents.z ? 1 : 2);
            int smallest = extents.x <= extents.y
                ? (extents.x <= extents.z ? 0 : 2)
                : (extents.y <= extents.z ? 1 : 2);
            return 3 - largest - smallest;
        }

        private bool IsSwingBlocked(float angle)
        {
            Quaternion previous = transform.localRotation;
            var blocked = false;
            foreach (float fraction in SwingProbeFractions)
            {
                transform.localRotation =
                    Quaternion.AngleAxis(angle * fraction, hingeAxis) * closedRotation;
                Physics.SyncTransforms();

                int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(probeCentre),
                    probeExtents, obstacleBuffer, transform.rotation, obstacleLayers,
                    QueryTriggerInteraction.Ignore);
                for (var index = 0; index < count && !blocked; index++)
                {
                    blocked = obstacleBuffer[index].transform != transform;
                }

                if (blocked)
                {
                    break;
                }
            }

            transform.localRotation = previous;
            Physics.SyncTransforms();
            return blocked;
        }

        private void ApplyRotation()
        {
            transform.localRotation = Quaternion.AngleAxis(currentAngle, hingeAxis) * closedRotation;
        }
    }
}
