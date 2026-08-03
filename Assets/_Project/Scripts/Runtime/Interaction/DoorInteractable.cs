using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.Interaction
{
    public enum DoorSwing
    {
        Auto,
        Forward,
        Back,
    }

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
        [SerializeField] private DoorSwing swing = DoorSwing.Auto;
        [SerializeField] private LayerMask obstacleLayers = ~0;

        [Header("State")]
        [SerializeField] private bool isLocked;
        [SerializeField] private bool startsOpen;

        [Header("Prompt Keys")]
        [SerializeField] private string openPromptKey = "interact.door.open";
        [SerializeField] private string closePromptKey = "interact.door.close";
        [SerializeField] private string lockedPromptKey = "interact.door.locked";

        private readonly Collider[] obstacleBuffer = new Collider[ObstacleBufferSize];

        private Collider leafCollider;
        private Quaternion closedRotation;
        private Vector3 hingeAxis;
        private Vector3 probeCentre;
        private Vector3 probeExtents;
        private float swingAngle;
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
            leafCollider = GetComponent<Collider>();
            closedRotation = transform.localRotation;
            hingeAxis = transform.parent != null
                ? transform.parent.InverseTransformDirection(Vector3.up).normalized
                : Vector3.up;

            BuildSwingProbe();
            swingAngle = ResolveSwingAngle();

            if (startsOpen)
            {
                targetAngle = swingAngle;
                currentAngle = swingAngle;
                ApplyRotation();
                SetColliderEnabled(false);
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

            targetAngle = swingAngle;
            SetColliderEnabled(false);
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
            SetColliderEnabled(Mathf.Abs(currentAngle) < AngleTolerance);
        }

        protected override string ResolvePromptKey()
        {
            if (isLocked)
            {
                return lockedPromptKey;
            }

            return IsOpen ? closePromptKey : openPromptKey;
        }

        private float ResolveSwingAngle()
        {
            if (swing == DoorSwing.Forward)
            {
                return openAngle;
            }

            if (swing == DoorSwing.Back)
            {
                return -openAngle;
            }

            var touching = new HashSet<Collider>(CollidersAt(0f));
            return IsSwingBlocked(openAngle, touching) ? -openAngle : openAngle;
        }

        private IEnumerable<Collider> CollidersAt(float angle)
        {
            Quaternion previous = transform.localRotation;
            int count = OverlapAt(angle);
            var found = new List<Collider>(count);
            for (var index = 0; index < count; index++)
            {
                if (obstacleBuffer[index].transform != transform)
                {
                    found.Add(obstacleBuffer[index]);
                }
            }

            transform.localRotation = previous;
            Physics.SyncTransforms();
            return found;
        }

        private int OverlapAt(float angle)
        {
            transform.localRotation = Quaternion.AngleAxis(angle, hingeAxis) * closedRotation;
            Physics.SyncTransforms();
            return Physics.OverlapBoxNonAlloc(transform.TransformPoint(probeCentre),
                probeExtents, obstacleBuffer, transform.rotation, obstacleLayers,
                QueryTriggerInteraction.Ignore);
        }

        private void SetColliderEnabled(bool value)
        {
            if (leafCollider != null && leafCollider.enabled != value)
            {
                leafCollider.enabled = value;
            }
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

            Vector3 scale = transform.lossyScale;
            Vector3 world = new Vector3(
                extents.x * Mathf.Abs(scale.x),
                extents.y * Mathf.Abs(scale.y),
                extents.z * Mathf.Abs(scale.z));

            probeCentre = centre;
            probeExtents = Vector3.Max(world - Vector3.one * ObstacleMargin,
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

        private bool IsSwingBlocked(float angle, HashSet<Collider> touching)
        {
            Quaternion previous = transform.localRotation;
            var blocked = false;
            foreach (float fraction in SwingProbeFractions)
            {
                int count = OverlapAt(angle * fraction);
                for (var index = 0; index < count && !blocked; index++)
                {
                    Collider hit = obstacleBuffer[index];
                    blocked = hit.transform != transform && !touching.Contains(hit);
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
