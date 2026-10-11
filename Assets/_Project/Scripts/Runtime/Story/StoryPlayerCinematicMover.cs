using System;
using SaksiTerakhir.Navigation;
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
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
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
        private Vector3 pathDestination;
        private float moveSpeed;
        private float stoppingDistance;
        private float followDistance;
        private float nextFollowRepathAt;
        private Vector3 lastFollowDestination;
        private bool ownsCinematicLock;
        private Action arrival;
        private Vector3 lastProgressPosition;
        private float nextRecoveryAt;
        private bool waitingForDialogue;
        private Vector3 dialogueWaitPosition;
        private SaksiTerakhir.Npc.OfficeNpcDirector doorDirector;
        private float doorWaitUntil;
        private Transform retreatSpeaker;
        public void SetRetreatSpeaker(Transform speaker) => retreatSpeaker = speaker;

        public bool IsActive => mode != MotionMode.None;
        public bool IsFollowing => mode == MotionMode.Follow;
        public Transform FollowTarget => followTarget;
        public float FollowDistance => followDistance;
        public bool HasDependencies => playerMovement != null && characterController != null;
        public int NavMeshAgentTypeId => navMeshAgentTypeId;
        public bool IsWaitingForDialogue => waitingForDialogue;
        public bool AtDialogueWaitPosition => waitingForDialogue
            && Vector3.ProjectOnPlane(transform.position - dialogueWaitPosition, Vector3.up).sqrMagnitude <= 0.12f * 0.12f
            && Mathf.Abs(transform.position.y - dialogueWaitPosition.y) <= 0.5f;

        /// <summary>Keep the doorway wait point on the player's approach side even when the speaker turns.</summary>
        public bool HoldForDialogue(Transform speaker, float distance = 2f)
        {
            if (speaker == null || mode != MotionMode.Follow || distance <= 0f) return false;
            Vector3 approach = Vector3.ProjectOnPlane(transform.position - speaker.position, Vector3.up);
            if (approach.sqrMagnitude < 0.0001f) approach = -speaker.forward;
            waitingForDialogue = true;
            followDistance = distance;
            stoppingDistance = 0.08f;
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = navMeshAgentTypeId, areaMask = NavMesh.AllAreas
            };
            // Keep every alternative on the original approach side of the speaker.
            foreach (float angle in new[] { 0f, 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f, 75f, -75f })
            {
                Vector3 candidate = speaker.position
                    + Quaternion.AngleAxis(angle, Vector3.up) * approach.normalized * distance;
                if (!TerrainNavigationProbe.TryGetGroundPosition(transform, candidate,
                        characterController.stepOffset, characterController.slopeLimit, out Vector3 ground)
                    || !NavMesh.SamplePosition(ground, out NavMeshHit hit, 0.15f, filter)
                    || Mathf.Abs(hit.position.y - ground.y) > 0.15f
                    || !HasDialogueClearance(hit.position, speaker)
                    || !TryBuildPath(hit.position)) continue;
                dialogueWaitPosition = pathDestination;
                ResetProgressWatch();
                return true;
            }
            // No safe two-metre spot exists. Hold the current position instead of
            // pausing dialogue forever at an unreachable or incorrectly projected point.
            dialogueWaitPosition = transform.position;
            currentPath = null;
            Drive(Vector3.zero);
            ResetProgressWatch();
            return false;
        }

        private bool HasDialogueClearance(Vector3 feet, Transform speaker)
        {
            float radius = characterController.radius;
            foreach (Collider blocker in Physics.OverlapCapsule(feet + Vector3.up * (radius + 0.04f),
                feet + Vector3.up * (characterController.height - radius + 0.04f),
                radius + 0.01f, ~0, QueryTriggerInteraction.Ignore))
                if (!blocker.transform.IsChildOf(transform) && !blocker.transform.IsChildOf(speaker)) return false;
            Vector3 from = feet + Vector3.up * characterController.height * 0.7f;
            Vector3 to = speaker.position + Vector3.up * characterController.height * 0.7f;
            foreach (RaycastHit hit in Physics.RaycastAll(from, (to - from).normalized,
                Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && !hit.collider.transform.IsChildOf(speaker)) return false;
            return true;
        }

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
            destination = pathDestination;
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
            // A delayed cleanup call can retain the managed wrapper after Unity destroys
            // this component. It must not access its transform or release a new owner's lock.
            if (this == null) return;
            TerrainNavigationProbe.ClearDetour(transform);
            mode = MotionMode.None;
            followTarget = null;
            currentPath = null;
            nextCorner = 0;
            waitingForDialogue = false;
            nextFollowRepathAt = 0f;
            lastFollowDestination = Vector3.zero;
            arrival = null;
            nextRecoveryAt = 0f;
            doorWaitUntil = 0f;
            if (!ownsCinematicLock) return;

            ownsCinematicLock = false;
            if (playerMovement != null)
            {
                playerMovement.StopCinematicMotion();
                playerMovement.SetCinematicMovementLocked(false);
            }
        }

        private void Update()
        {
            if (!IsActive || Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
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
            if (mode == MotionMode.Follow && (AtDialogueWaitPosition
                || (!waitingForDialogue && IsWithinFollowDistance(0.1f))))
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

            // Recheck actual space and footing before crossing a mesh seam. Retrying the
            // same push against a wall can never recover a blocked cinematic walk.
            if (currentPath != null && nextCorner < currentPath.corners.Length)
            {
                Vector3 direction = Vector3.ProjectOnPlane(
                    currentPath.corners[nextCorner] - transform.position, Vector3.up).normalized;
                if (TryResolveDirection(direction, RecoveryStepMeters, out Vector3 safeDirection))
                    characterController.Move(safeDirection * RecoveryStepMeters);
            }
            if (mode == MotionMode.Destination) TryBuildPath(destination);
            else if (waitingForDialogue) TryBuildPath(dialogueWaitPosition);
            else if (followTarget != null) TryBuildFollowPath(followTarget, followDistance);
        }

        private void UpdateFollowPath()
        {
            if (followTarget == null)
            {
                StopCinematicMotion();
                return;
            }

            if (waitingForDialogue)
            {
                if (currentPath == null && Time.time >= nextFollowRepathAt)
                {
                    nextFollowRepathAt = Time.time + followRepathInterval;
                    TryBuildPath(dialogueWaitPosition);
                }
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
            if (Time.time < doorWaitUntil)
            {
                Drive(Vector3.zero);
                ResetProgressWatch();
                return;
            }
            // Heading changes at a door must not let a follow path pull the player
            // through the colleague. The radial gap is independent of NPC orientation.
            if (mode == MotionMode.Follow && !waitingForDialogue && IsWithinFollowDistance())
            {
                Drive(Vector3.zero);
                return;
            }
            if (currentPath == null || currentPath.corners == null || currentPath.corners.Length == 0)
            {
                Drive(Vector3.zero);
                return;
            }

            while (nextCorner < currentPath.corners.Length)
            {
                Vector3 corner = currentPath.corners[nextCorner];
                Vector3 flatOffset = Vector3.ProjectOnPlane(corner - transform.position, Vector3.up);
                if (flatOffset.sqrMagnitude > stoppingDistance * stoppingDistance
                    || Mathf.Abs(corner.y - transform.position.y) > 0.5f) break;
                nextCorner++;
            }

            if (nextCorner >= currentPath.corners.Length)
            {
                Drive(Vector3.zero);
                if (mode == MotionMode.Destination && IsNearDestination()) CompleteDestination();
                return;
            }

            Vector3 target = currentPath.corners[nextCorner];
            if (doorDirector == null)
                doorDirector = FindFirstObjectByType<SaksiTerakhir.Npc.OfficeNpcDirector>();
            if (doorDirector != null && doorDirector.TryOpenDoorAhead(transform, target, out doorWaitUntil))
            {
                Drive(Vector3.zero);
                ResetProgressWatch();
                return;
            }
            Vector3 direction = Vector3.ProjectOnPlane(target - transform.position, Vector3.up).normalized;
            float speed = moveSpeed;
            if (mode == MotionMode.Follow && !waitingForDialogue && followTarget != null)
            {
                float gap = Vector3.Distance(transform.position, followTarget.position);
                if (gap > followDistance)
                    speed = Mathf.Min(speed, (gap - followDistance) / Mathf.Max(Time.deltaTime, 0.001f));
            }
            float distance = Mathf.Max(0.65f, speed * Time.deltaTime + characterController.radius * 1.2f);
            if (!TryResolveDirection(direction, distance, out Vector3 safeDirection))
            {
                Drive(Vector3.zero);
                return;
            }
            Drive(safeDirection * speed);
            // The camera owns the shared player yaw while making room for a speaker.
            // Walking backwards must not compete with its focus on that speaker.
            if (retreatSpeaker == null) Face(safeDirection);
        }

        private bool IsNearDestination() => Vector3.ProjectOnPlane(transform.position - destination, Vector3.up).sqrMagnitude
            <= (stoppingDistance + 0.1f) * (stoppingDistance + 0.1f)
            && Mathf.Abs(transform.position.y - destination.y) <= 0.5f;

        private bool IsWithinFollowDistance(float tolerance = 0f)
        {
            if (followTarget == null) return false;
            // Match the colleague's catch-up distance on slopes as well as flat floors.
            Vector3 offset = followTarget.position - transform.position;
            float limit = followDistance + tolerance;
            if (offset.sqrMagnitude > limit * limit) return false;
            // A colleague on the other side of a wall has not been caught up with.
            Vector3 start = transform.position + Vector3.up * characterController.height * 0.7f;
            Vector3 end = followTarget.position + Vector3.up * characterController.height * 0.7f;
            foreach (RaycastHit hit in Physics.RaycastAll(start, (end - start).normalized,
                Vector3.Distance(start, end), ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)
                    || hit.collider.transform.IsChildOf(followTarget)) continue;
                // A crowd member hiding the colleague does not make the MC too far
                // away. Keep the gap while it clears; walls still require a route around.
                if (hit.collider.GetComponentInParent<SaksiTerakhir.Npc.OfficeNpcAgent>() != null) continue;
                return false;
            }
            return true;
        }

        private bool TryResolveDirection(Vector3 desired, float distance, out Vector3 safeDirection)
        {
            SaksiTerakhir.Npc.NpcPassageYield.Request(transform, desired, characterController.radius, 1.8f);
            if (retreatSpeaker != null)
            {
                Vector3 away = Vector3.ProjectOnPlane(transform.position - retreatSpeaker.position, Vector3.up);
                if (away.magnitude < 1.1f && Vector3.Dot(away, desired) > 0f)
                {
                    bool safe = TerrainNavigationProbe.TryResolveDirection(transform, desired, distance,
                        characterController.radius, characterController.height, characterController.stepOffset,
                        characterController.slopeLimit, new NavMeshQueryFilter
                        { agentTypeID = navMeshAgentTypeId, areaMask = NavMesh.AllAreas }, out safeDirection,
                        collider => collider != null && collider.transform.IsChildOf(retreatSpeaker));
                    return safe && Vector3.Dot(away, safeDirection) >= 0f;
                }
            }
            var filter = new NavMeshQueryFilter { agentTypeID = navMeshAgentTypeId, areaMask = NavMesh.AllAreas };
            if (!TerrainNavigationProbe.TryResolveDirection(transform, desired, distance,
                    characterController.radius, characterController.height, characterController.stepOffset,
                    characterController.slopeLimit, filter, out Vector3 terrainDirection,
                    SaksiTerakhir.Npc.NpcPassageYield.IsMovableBody))
            { safeDirection = Vector3.zero; return false; }
            // Clear the checked wall/ramp detour too; workers beside the original
            // ray can block that route. The final check still treats bodies as solid.
            SaksiTerakhir.Npc.NpcPassageYield.Request(transform, terrainDirection, characterController.radius, 1.8f);
            return TerrainNavigationProbe.TryResolveDirection(transform, terrainDirection, distance,
                characterController.radius, characterController.height, characterController.stepOffset,
                characterController.slopeLimit, filter, out safeDirection);
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
                    navMeshSampleRadius, filter)
                || Mathf.Abs(source.position.y - transform.position.y) > 0.5f
                || Mathf.Abs(destinationHit.position.y - target.y) > 0.5f) return false;

            NavMeshPath candidate = new NavMeshPath();
            if (!NavMesh.CalculatePath(source.position, destinationHit.position,
                    filter, candidate)
                || candidate.status != NavMeshPathStatus.PathComplete) return false;

            currentPath = candidate;
            pathDestination = destinationHit.position;
            nextCorner = candidate.corners != null && candidate.corners.Length > 1 ? 1 : 0;
            return true;
        }

        private bool TryBuildFollowPath(Transform target, float trailingDistance)
        {
            Vector3 trailingPoint = ResolveFollowDestination(target, trailingDistance);
            if (TryBuildPath(trailingPoint))
            {
                lastFollowDestination = trailingPoint;
                return true;
            }
            if (!TryBuildPath(target.position)) return false;
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
