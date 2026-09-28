using System;
using SaksiTerakhir.Player;
using UnityEngine;
using UnityEngine.AI;

namespace SaksiTerakhir.Story
{
    /// <summary>
    /// Moves the player along a complete NavMesh path while ordinary input remains locked.
    /// This component intentionally owns only its cinematic lock, so dialogue can end during
    /// an escort without returning control to the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryPlayerCinematicMover : MonoBehaviour
    {
        private const float StalledRecoverySeconds = 0.75f;
        private const float MinimumProgressMeters = 0.12f;
        private const float RecoveryStepMeters = 0.15f;
        private enum MotionMode { None, Destination, Follow }

        [SerializeField] private PlayerController playerMovement;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private int navMeshAgentTypeId;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float defaultMoveSpeed = 2.2f;
        [SerializeField, Min(0.05f)] private float defaultStoppingDistance = 0.25f;
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 1.2f;
        [SerializeField, Min(0.05f)] private float followRepathInterval = 0.2f;
        [SerializeField, Min(0.05f)] private float followRepathDistance = 0.4f;
        [SerializeField, Min(0.1f)] private float turnSmoothing = 12f;

        private MotionMode mode;
        private NavMeshPath currentPath;
        private int nextCorner;
        private Transform followTarget;
        private Vector3 destination;
        private float moveSpeed;
        private float stoppingDistance;
        private float followDistance;
        private float nextFollowRepathAt;
        private Vector3 lastFollowDestination;
        private bool followingDirectTarget;
        private bool ownsCinematicLock;
        private Action arrival;
        private Vector3 lastProgressPosition;
        private float nextRecoveryAt;

        public bool IsActive => mode != MotionMode.None;
        public bool IsFollowing => mode == MotionMode.Follow;
        public Transform FollowTarget => followTarget;
        public float FollowDistance => followDistance;
        public bool HasDependencies => playerMovement != null && characterController != null;
        public int NavMeshAgentTypeId => navMeshAgentTypeId;

        private void Awake()
        {
            ResolveDependencies();
        }

        private void OnDisable()
        {
            StopCinematicMotion();
        }

        public bool Configure(PlayerController player, CharacterController controller,
            int agentTypeId = 0)
        {
            StopCinematicMotion();
            playerMovement = player;
            characterController = controller;
            navMeshAgentTypeId = agentTypeId;
            return HasDependencies;
        }

        public bool BeginMoveTo(Transform target, float speed = -1f,
            float stopDistance = -1f, Action onArrived = null)
        {
            return target != null && BeginMoveTo(target.position, speed, stopDistance, onArrived);
        }

        public bool BeginMoveTo(Vector3 target, float speed = -1f,
            float stopDistance = -1f, Action onArrived = null)
        {
            if (!CanBegin(speed, stopDistance)) return false;
            StopCinematicMotion();
            if (!TryBuildPath(target)) return false;
            destination = target;
            moveSpeed = ResolveSpeed(speed);
            stoppingDistance = ResolveStopDistance(stopDistance);
            arrival = onArrived;
            followTarget = null;
            mode = MotionMode.Destination;
            AcquireCinematicLock();
            ResetProgressWatch();
            return true;
        }

        public bool BeginFollow(Transform target, float trailingDistance, float speed = -1f)
        {
            if (target == null || trailingDistance <= 0f || !CanBegin(speed, -1f)) return false;

            StopCinematicMotion();
            if (!TryBuildFollowPath(target, trailingDistance)) return false;

            followTarget = target;
            followDistance = trailingDistance;
            moveSpeed = ResolveSpeed(speed);
            stoppingDistance = defaultStoppingDistance;
            arrival = null;
            mode = MotionMode.Follow;
            nextFollowRepathAt = Time.time + followRepathInterval;
            AcquireCinematicLock();
            ResetProgressWatch();
            return true;
        }

        public void StopCinematicMotion()
        {
            mode = MotionMode.None;
            followTarget = null;
            currentPath = null;
            nextCorner = 0;
            followingDirectTarget = false;
            nextFollowRepathAt = 0f;
            lastFollowDestination = Vector3.zero;
            arrival = null;
            nextRecoveryAt = 0f;
            if (!ownsCinematicLock) return;

            ownsCinematicLock = false;
            playerMovement?.StopCinematicMotion();
            playerMovement?.SetCinematicMovementLocked(false);
        }

        private void Update()
        {
            if (!IsActive) return;
            if (!HasDependencies || !characterController.enabled)
            {
                StopCinematicMotion();
                return;
            }

            if (mode == MotionMode.Follow) UpdateFollowPath();
            DriveCurrentPath();
            RecoverIfStalled();
        }

        private void ResetProgressWatch()
        {
            lastProgressPosition = transform.position;
            nextRecoveryAt = Time.time + StalledRecoverySeconds;
        }

        private void RecoverIfStalled()
        {
            if (!IsActive) return;
            if (mode == MotionMode.Follow && followTarget != null
                && Vector3.Distance(transform.position, followTarget.position) <= followDistance)
            {
                ResetProgressWatch();
                return;
            }
            if (Vector3.Distance(transform.position, lastProgressPosition) >= MinimumProgressMeters)
            {
                ResetProgressWatch();
                return;
            }
            if (Time.time < nextRecoveryAt) return;
            nextRecoveryAt = Time.time + StalledRecoverySeconds;

            // Keep physical collision handling. A short full-size Move can step across a
            // mesh seam that blocks the ordinary small per-frame motion.
            if (currentPath != null && nextCorner < currentPath.corners.Length)
            {
                Vector3 direction = Vector3.ProjectOnPlane(
                    currentPath.corners[nextCorner] - transform.position, Vector3.up).normalized;
                characterController.Move(direction * RecoveryStepMeters);
            }
            if (mode == MotionMode.Destination) TryBuildPath(destination);
            else if (followTarget != null) TryBuildFollowPath(followTarget, followDistance);
        }

        private void UpdateFollowPath()
        {
            if (followTarget == null)
            {
                StopCinematicMotion();
                return;
            }

            if (Time.time < nextFollowRepathAt) return;
            nextFollowRepathAt = Time.time + followRepathInterval;
            Vector3 nextDestination = ResolveFollowDestination(followTarget, followDistance);
            bool destinationChanged = Vector3.SqrMagnitude(nextDestination - lastFollowDestination)
                >= followRepathDistance * followRepathDistance;
            if (destinationChanged || currentPath == null)
                TryBuildFollowPath(followTarget, followDistance);
        }

        private void DriveCurrentPath()
        {
            if (currentPath == null || currentPath.corners == null || currentPath.corners.Length == 0)
            {
                Drive(Vector3.zero);
                if (mode == MotionMode.Destination) CompleteDestination();
                return;
            }

            while (nextCorner < currentPath.corners.Length)
            {
                Vector3 corner = currentPath.corners[nextCorner];
                Vector3 flatOffset = Vector3.ProjectOnPlane(corner - transform.position, Vector3.up);
                if (flatOffset.sqrMagnitude > stoppingDistance * stoppingDistance) break;
                nextCorner++;
            }

            if (mode == MotionMode.Follow && followingDirectTarget && followTarget != null
                && nextCorner >= currentPath.corners.Length - 1
                && Vector3.Distance(transform.position, followTarget.position) <= followDistance)
            {
                Drive(Vector3.zero);
                return;
            }

            if (nextCorner >= currentPath.corners.Length)
            {
                Drive(Vector3.zero);
                if (mode == MotionMode.Destination) CompleteDestination();
                return;
            }

            Vector3 target = currentPath.corners[nextCorner];
            Vector3 direction = Vector3.ProjectOnPlane(target - transform.position, Vector3.up).normalized;
            Drive(direction * moveSpeed);
            Face(direction);
        }

        private void Drive(Vector3 velocity)
        {
            if (playerMovement != null && playerMovement.isActiveAndEnabled)
            {
                playerMovement.SetCinematicVelocity(velocity);
                return;
            }

            // The fallback keeps editor and integration probes deterministic if their
            // ordinary player controller is temporarily disabled.
            if (characterController != null && characterController.enabled)
                characterController.Move(velocity * Time.deltaTime);
        }

        private void Face(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f) return;
            float blend = 1f - Mathf.Exp(-turnSmoothing * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up), blend);
        }

        private bool CanBegin(float speed, float stopDistance)
        {
            ResolveDependencies();
            return HasDependencies && characterController.enabled
                && ResolveSpeed(speed) > 0f && ResolveStopDistance(stopDistance) > 0f;
        }

        private bool TryBuildPath(Vector3 target)
        {
            if (!HasDependencies) return false;
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = navMeshAgentTypeId,
                areaMask = NavMesh.AllAreas
            };
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit source,
                    navMeshSampleRadius, filter)
                || !NavMesh.SamplePosition(target, out NavMeshHit destinationHit,
                    navMeshSampleRadius, filter)) return false;

            NavMeshPath candidate = new NavMeshPath();
            if (!NavMesh.CalculatePath(source.position, destinationHit.position,
                    filter, candidate)
                || candidate.status != NavMeshPathStatus.PathComplete) return false;

            currentPath = candidate;
            nextCorner = candidate.corners != null && candidate.corners.Length > 1 ? 1 : 0;
            return true;
        }

        private bool TryBuildFollowPath(Transform target, float trailingDistance)
        {
            Vector3 trailingPoint = ResolveFollowDestination(target, trailingDistance);
            if (TryBuildPath(trailingPoint))
            {
                followingDirectTarget = false;
                lastFollowDestination = trailingPoint;
                return true;
            }
            if (!TryBuildPath(target.position)) return false;
            followingDirectTarget = true;
            lastFollowDestination = trailingPoint;
            return true;
        }

        private void CompleteDestination()
        {
            Action completed = arrival;
            StopCinematicMotion();
            completed?.Invoke();
        }

        private void AcquireCinematicLock()
        {
            ownsCinematicLock = true;
            playerMovement.SetCinematicMovementLocked(true);
            playerMovement.StopCinematicMotion();
        }

        private void ResolveDependencies()
        {
            if (playerMovement == null) playerMovement = GetComponent<PlayerController>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
        }

        private float ResolveSpeed(float requestedSpeed) => requestedSpeed > 0f
            ? requestedSpeed : defaultMoveSpeed;

        private float ResolveStopDistance(float requestedDistance) => requestedDistance > 0f
            ? requestedDistance : defaultStoppingDistance;

        private static Vector3 ResolveFollowDestination(Transform target, float distance)
        {
            return target.position - target.forward * distance;
        }
    }
}
