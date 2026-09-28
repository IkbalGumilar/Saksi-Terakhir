using System;
using UnityEngine;
using UnityEngine.AI;

namespace SaksiTerakhir.Npc
{
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class OfficeNpcAgent : MonoBehaviour
    {
        [SerializeField] private NpcProfile profile;
        // Hidden legacy fields keep authored routes readable during the one-time scene migration.
        [SerializeField, HideInInspector] private string displayName = "Pekerja";
        [SerializeField, HideInInspector] private NpcRole role = NpcRole.Worker;
        [SerializeField, HideInInspector] private NpcActivityPoint home;
        [SerializeField, HideInInspector] private NpcActivityPoint[] route = Array.Empty<NpcActivityPoint>();
        [SerializeField, HideInInspector] private bool stationary;

        private NavMeshAgent navigation;
        private NavMeshPath candidatePath;
        private NpcActivityPoint destination;
        private OfficeNpcDirector director;
        private NpcLabel label;
        private OfficeNpcAgent conversationPartner;
        private Transform playerConversationPartner;
        private NpcState resumeState;
        private float activityRemaining, nextDecision, nextPathCheck, doorWaitUntil, conversationUntil;
        private float stuckSince, nextConversationTime;
        private Vector3 progressPosition, lastPosition;
        private int routeIndex;
        private bool started;
        private bool storyHeld;
        private bool storyMoveRequested;
        private bool storyAtDestination;
        private Vector3 storyDestination;
        private float storySpeed;
        private float nextStoryRetry;
        private float nextStoryWarning;
        private Transform storyFaceTarget;
        private Transform storyFollowTarget;
        private float storyFollowDistance;
        private Transform storyCapsuleVisual;
        private Transform storyFacingMarker;
        private Transform storyLabelRoot;
        private Vector3 standingCapsulePosition;
        private Vector3 standingCapsuleScale;
        private Vector3 standingMarkerPosition;
        private Vector3 standingLabelPosition;
        private bool storySeated;

        public NpcProfile Profile => profile;
        public string DisplayName => profile != null ? profile.DisplayName : displayName;
        public NpcRole Role => profile != null ? profile.Role : role;
        public NpcActivityPoint Home => home;
        public NpcActivityPoint[] Route => route;
        public bool Stationary => profile != null ? profile.Stationary : stationary;
        public NpcState CurrentState { get; private set; } = NpcState.Waiting;
        public int CompletedActivities { get; private set; }
        public int Conversations { get; private set; }
        public float TravelMeters { get; private set; }
        public int VisitedFloorMask { get; private set; }
        public int FailedPathAttempts { get; private set; }
        public NpcActivityPoint Destination => destination;
        public NavMeshAgent Navigation => navigation;
        public bool CanConverse => isActiveAndEnabled && navigation != null && navigation.isOnNavMesh
            && CurrentState == NpcState.Activity && Time.time >= nextConversationTime;
        public bool StoryAtDestination => storyHeld && storyAtDestination;
        public bool StoryMoveRequested => storyMoveRequested;
        public bool StorySeated => storySeated;
        public bool StoryWaitingForPlayer => storyMoveRequested && storyFollowTarget != null
            && Vector3.Distance(transform.position, storyFollowTarget.position) > storyFollowDistance;

        public void HoldForStory(Transform faceTarget = null)
        {
            if (navigation == null) navigation = GetComponent<NavMeshAgent>();
            if (CurrentState == NpcState.Conversation) StopConversation();
            if (CurrentState == NpcState.PlayerConversation) EndPlayerInteraction();
            ReleaseDestination();
            storyHeld = true;
            storyMoveRequested = false;
            storyAtDestination = false;
            storyFaceTarget = faceTarget;
            storyFollowTarget = null;
            CurrentState = NpcState.StoryHeld;
            if (navigation != null && navigation.enabled && navigation.isOnNavMesh)
            {
                navigation.isStopped = true;
                navigation.ResetPath();
            }
        }

        public void ReleaseStoryHold()
        {
            if (!storyHeld) return;
            if (navigation != null && navigation.enabled)
            {
                if (navigation.isOnNavMesh)
                {
                    navigation.isStopped = true;
                    navigation.ResetPath();
                }
                if (profile != null) navigation.speed = profile.MovementSpeed;
            }
            storyHeld = false;
            storyMoveRequested = false;
            storyAtDestination = false;
            storyFaceTarget = null;
            storyFollowTarget = null;
            CurrentState = NpcState.Waiting;
            nextDecision = Time.time + 0.5f;
        }

        public bool TrySetStoryDestination(Vector3 target, float speed)
        {
            if (!storyHeld) HoldForStory();
            storyDestination = target;
            storySpeed = Mathf.Max(0.1f, speed);
            storyMoveRequested = true;
            storyAtDestination = false;
            return TryStartStoryPath();
        }

        public void SetStoryFacing(Transform target) => storyFaceTarget = target;

        public void SetStorySeated(bool seated)
        {
            if (storySeated == seated) return;
            if (seated)
            {
                storyCapsuleVisual = transform.Find("Capsule Visual");
                if (storyCapsuleVisual == null) return;
                storyFacingMarker = transform.Find("Facing Marker");
                storyLabelRoot = transform.Find("NPC Label");
                standingCapsulePosition = storyCapsuleVisual.localPosition;
                standingCapsuleScale = storyCapsuleVisual.localScale;
                if (storyFacingMarker != null) standingMarkerPosition = storyFacingMarker.localPosition;
                if (storyLabelRoot != null) standingLabelPosition = storyLabelRoot.localPosition;

                storyCapsuleVisual.localPosition = standingCapsulePosition;
                storyCapsuleVisual.localScale = new Vector3(standingCapsuleScale.x,
                    standingCapsuleScale.y * 0.58f, standingCapsuleScale.z);
                if (storyFacingMarker != null)
                    storyFacingMarker.localPosition = standingMarkerPosition + Vector3.down * 0.25f;
                if (storyLabelRoot != null)
                    storyLabelRoot.localPosition = standingLabelPosition + Vector3.down * 0.4f;
            }
            else
            {
                storyCapsuleVisual.localPosition = standingCapsulePosition;
                storyCapsuleVisual.localScale = standingCapsuleScale;
                if (storyFacingMarker != null) storyFacingMarker.localPosition = standingMarkerPosition;
                if (storyLabelRoot != null) storyLabelRoot.localPosition = standingLabelPosition;
            }
            storySeated = seated;
        }

        public void SetStoryFollowDistance(Transform player, float maximumDistance)
        {
            storyFollowTarget = player;
            storyFollowDistance = Mathf.Max(0.1f, maximumDistance);
        }

        public void Configure(string name, NpcRole actorRole, NpcActivityPoint homePoint,
            NpcActivityPoint[] activityRoute, bool isStationary)
        {
            displayName = name;
            role = actorRole;
            home = homePoint;
            route = activityRoute ?? Array.Empty<NpcActivityPoint>();
            stationary = isStationary;
        }

        public void SetDirector(OfficeNpcDirector owner) => director = owner;

        public void SetProfile(NpcProfile asset)
        {
            profile = asset;
            if (asset == null) return;
            displayName = asset.DisplayName;
            role = asset.Role;
            stationary = asset.Stationary;
            home = null;
            route = Array.Empty<NpcActivityPoint>();
        }

        public void BindWaypoints(NpcActivityPoint homePoint, NpcActivityPoint[] activityRoute)
        {
            home = homePoint;
            route = activityRoute ?? Array.Empty<NpcActivityPoint>();
        }

        private void Awake()
        {
            navigation = GetComponent<NavMeshAgent>();
            if (profile != null) navigation.speed = profile.MovementSpeed;
            label = GetComponentInChildren<NpcLabel>(true);
            candidatePath = new NavMeshPath();
        }

        private void Start()
        {
            if (home == null)
            {
                Debug.LogError($"NPC {DisplayName} has no home waypoint. Check its profile and OfficeNpcDirector.", this);
                enabled = false;
                return;
            }
            started = true;
            routeIndex = route.Length > 0 ? UnityEngine.Random.Range(0, route.Length) : 0;
            lastPosition = progressPosition = transform.position;
            nextConversationTime = Time.time + UnityEngine.Random.Range(5f, 20f);
            if (label != null) label.SetName(DisplayName);
            if (storyHeld)
            {
                TryAttach();
                CurrentState = NpcState.StoryHeld;
                return;
            }
            if (TryAttach())
            {
                if (!TryBeginJourney(home)) ScheduleNext(0.5f, 2f);
            }
        }

        private void OnEnable()
        {
            if (!started) return;
            lastPosition = transform.position;
            CurrentState = storyHeld ? NpcState.StoryHeld : NpcState.Waiting;
            nextDecision = Time.time + 0.5f;
        }

        private void OnDisable()
        {
            ReleaseDestination();
            StopConversation();
            EndPlayerInteraction();
            if (navigation != null && navigation.enabled && navigation.isOnNavMesh) navigation.ResetPath();
        }

        private void Update()
        {
            Vector3 currentPosition = transform.position;
            float displacement = Vector3.Distance(currentPosition, lastPosition);
            if (displacement < 2f) TravelMeters += displacement;
            lastPosition = currentPosition;

            if (storyHeld)
            {
                UpdateStoryOrder();
                return;
            }

            if (CurrentState == NpcState.PlayerConversation)
            {
                if (playerConversationPartner != null)
                    Face(playerConversationPartner.position - transform.position);
                return;
            }

            if (CurrentState == NpcState.Conversation)
            {
                if (conversationPartner == null || !conversationPartner.isActiveAndEnabled || Time.time >= conversationUntil)
                    StopConversation();
                else Face(conversationPartner.transform.position - transform.position);
                return;
            }

            if (navigation == null || !navigation.enabled) return;
            if (!navigation.isOnNavMesh)
            {
                if (Time.time >= nextDecision && TryAttach()) ScheduleNext(0.1f, 0.5f);
                return;
            }
            if (CurrentState == NpcState.Moving) UpdateJourney();
            else if (CurrentState == NpcState.Activity) UpdateActivity();
            else if (Time.time >= nextDecision) ChooseNextActivity();
        }

        private bool TryAttach()
        {
            if (navigation == null || !navigation.enabled) return false;
            if (navigation.isOnNavMesh) return true;
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = navigation.agentTypeID, areaMask = navigation.areaMask };
            // Attachment is local only; a missed stair/floor is never repaired by teleporting to another storey.
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 0.6f, filter)
                && Mathf.Abs(hit.position.y - transform.position.y) <= 0.4f && navigation.Warp(hit.position))
            {
                lastPosition = progressPosition = transform.position;
                return true;
            }
            ReleaseDestination();
            CurrentState = NpcState.OffNavMesh;
            nextDecision = Time.time + 5f;
            return false;
        }

        private bool TryStartStoryPath()
        {
            if (navigation == null || !navigation.enabled || !navigation.isOnNavMesh)
                return false;
            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = navigation.agentTypeID,
                areaMask = navigation.areaMask
            };
            if (!NavMesh.SamplePosition(storyDestination, out NavMeshHit targetHit, 0.8f, filter)
                || Mathf.Abs(targetHit.position.y - storyDestination.y) > 0.4f
                || !navigation.CalculatePath(targetHit.position, candidatePath)
                || candidatePath.status != NavMeshPathStatus.PathComplete)
            {
                WarnStoryPath();
                return false;
            }
            navigation.speed = storySpeed;
            navigation.isStopped = false;
            if (!navigation.SetPath(candidatePath))
            {
                WarnStoryPath();
                return false;
            }
            CurrentState = NpcState.StoryMoving;
            progressPosition = transform.position;
            stuckSince = Time.time;
            return true;
        }

        private void UpdateStoryOrder()
        {
            if (storyFaceTarget != null && CurrentState != NpcState.StoryMoving)
                Face(storyFaceTarget.position - transform.position);
            if (!storyMoveRequested) return;
            if (navigation == null || !navigation.enabled || !navigation.isOnNavMesh)
            {
                if (Time.time >= nextStoryRetry)
                {
                    nextStoryRetry = Time.time + 2f;
                    TryAttach();
                    TryStartStoryPath();
                }
                return;
            }
            if (CurrentState != NpcState.StoryMoving || !navigation.hasPath)
            {
                if (Time.time >= nextStoryRetry)
                {
                    nextStoryRetry = Time.time + 2f;
                    TryStartStoryPath();
                }
                return;
            }
            if (StoryWaitingForPlayer)
            {
                navigation.isStopped = true;
                stuckSince = Time.time;
                return;
            }
            if (Time.time < doorWaitUntil) return;
            if (navigation.isStopped) navigation.isStopped = false;
            if (navigation.pathPending) return;
            if (director != null && director.TryOpenDoorAhead(this, navigation.steeringTarget, out float waitUntil))
            {
                doorWaitUntil = waitUntil;
                navigation.isStopped = true;
                return;
            }
            if (navigation.pathStatus != NavMeshPathStatus.PathComplete)
            {
                navigation.ResetPath();
                CurrentState = NpcState.StoryHeld;
                WarnStoryPath();
                return;
            }
            if (!float.IsInfinity(navigation.remainingDistance)
                && navigation.remainingDistance <= navigation.stoppingDistance + 0.15f)
            {
                navigation.isStopped = true;
                navigation.ResetPath();
                storyMoveRequested = false;
                storyAtDestination = true;
                CurrentState = NpcState.StoryHeld;
                return;
            }
            if ((transform.position - progressPosition).sqrMagnitude > 0.08f * 0.08f)
            {
                progressPosition = transform.position;
                stuckSince = Time.time;
            }
            else if (Time.time - stuckSince > 12f)
            {
                navigation.ResetPath();
                CurrentState = NpcState.StoryHeld;
                WarnStoryPath();
            }
        }

        private void WarnStoryPath()
        {
            if (Time.time < nextStoryWarning) return;
            Debug.LogWarning($"NPC {DisplayName} cannot reach story destination {storyDestination}; retrying.", this);
            nextStoryWarning = Time.time + 15f;
        }

        private void ChooseNextActivity()
        {
            if (Stationary)
            {
                if (!TryBeginJourney(home)) ScheduleNext(2f, 4f);
                return;
            }

            // Round-robin through the authored route makes cross-floor duties eventual, not a random chance.
            for (int i = 0; i < route.Length; i++)
            {
                NpcActivityPoint point = route[routeIndex];
                routeIndex = (routeIndex + 1) % route.Length;
                if (TryBeginJourney(point)) return;
            }
            if (!TryBeginJourney(home)) ScheduleNext(3f, 6f);
        }

        private bool TryBeginJourney(NpcActivityPoint point)
        {
            if (director != null && director.IsStoryRestricted(point)) return false;
            if (point == null || navigation == null || !navigation.enabled || !navigation.isOnNavMesh || !point.TryReserve(this))
                return false;
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = navigation.agentTypeID, areaMask = navigation.areaMask };
            if (!NavMesh.SamplePosition(point.transform.position, out NavMeshHit targetHit, 0.6f, filter)
                || Mathf.Abs(targetHit.position.y - point.transform.position.y) > 0.4f
                || !navigation.CalculatePath(targetHit.position, candidatePath)
                || candidatePath.status != NavMeshPathStatus.PathComplete)
            {
                point.Release(this);
                FailedPathAttempts++;
                return false;
            }
            ReleaseDestination();
            destination = point;
            // ReleaseDestination may have released this same point after a retry.
            point.TryReserve(this);
            navigation.isStopped = false;
            if (!navigation.SetPath(candidatePath))
            {
                ReleaseDestination();
                FailedPathAttempts++;
                return false;
            }
            CurrentState = NpcState.Moving;
            stuckSince = Time.time;
            progressPosition = transform.position;
            nextPathCheck = Time.time + 0.2f;
            return true;
        }

        private void UpdateJourney()
        {
            if (destination == null || !destination.isActiveAndEnabled) { AbandonJourney(); return; }
            if (director != null && director.IsStoryRestricted(destination)) { AbandonJourney(); return; }
            if (Time.time < doorWaitUntil) return;
            if (navigation.isStopped) navigation.isStopped = false;
            if (Time.time < nextPathCheck || navigation.pathPending) return;
            nextPathCheck = Time.time + 0.2f;

            if (director != null && director.TryOpenDoorAhead(this, navigation.steeringTarget, out float waitUntil))
            {
                doorWaitUntil = waitUntil;
                navigation.isStopped = true;
                stuckSince = Time.time + Mathf.Max(0f, waitUntil - Time.time);
                return;
            }
            if (navigation.pathStatus != NavMeshPathStatus.PathComplete) { AbandonJourney(); return; }
            if (!float.IsInfinity(navigation.remainingDistance)
                && navigation.remainingDistance <= navigation.stoppingDistance + 0.15f)
            {
                navigation.isStopped = true;
                navigation.ResetPath();
                CurrentState = NpcState.Activity;
                activityRemaining = destination.ChooseDwellTime();
                VisitedFloorMask |= 1 << Mathf.Clamp(destination.Floor, 0, 30);
                return;
            }
            if (!navigation.hasPath) { AbandonJourney(); return; }
            if ((transform.position - progressPosition).sqrMagnitude > 0.08f * 0.08f)
            {
                progressPosition = transform.position;
                stuckSince = Time.time;
            }
            else if (Time.time - stuckSince > 12f) AbandonJourney();
        }

        private void UpdateActivity()
        {
            if (destination == null || !destination.isActiveAndEnabled) { AbandonJourney(); return; }
            Face(destination.transform.forward);
            activityRemaining -= Time.deltaTime;
            if (activityRemaining > 0f) return;
            CompletedActivities++;
            if (Stationary) activityRemaining = destination.ChooseDwellTime();
            else { ReleaseDestination(); ScheduleNext(0.5f, 1.5f); }
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 220f * Time.deltaTime);
        }

        private void AbandonJourney()
        {
            if (navigation.isOnNavMesh) { navigation.ResetPath(); navigation.isStopped = true; }
            FailedPathAttempts++;
            ReleaseDestination();
            ScheduleNext(2f, 5f);
        }

        private void ScheduleNext(float minimum, float maximum)
        {
            CurrentState = NpcState.Waiting;
            nextDecision = Time.time + UnityEngine.Random.Range(minimum, maximum);
        }

        private void ReleaseDestination()
        {
            if (destination != null) destination.Release(this);
            destination = null;
        }

        internal void BeginConversation(OfficeNpcAgent other, float duration, string speech)
        {
            resumeState = CurrentState;
            CurrentState = NpcState.Conversation;
            conversationPartner = other;
            conversationUntil = Time.time + duration;
            navigation.isStopped = true;
            Conversations++;
            if (label != null) label.Say(speech, duration);
        }

        public void BeginPlayerInteraction(Transform player)
        {
            if (CurrentState == NpcState.PlayerConversation)
            {
                playerConversationPartner = player;
                return;
            }
            if (CurrentState == NpcState.Conversation) StopConversation();
            resumeState = CurrentState;
            CurrentState = NpcState.PlayerConversation;
            playerConversationPartner = player;
            if (navigation != null && navigation.enabled && navigation.isOnNavMesh)
                navigation.isStopped = true;
        }

        public void EndPlayerInteraction()
        {
            if (CurrentState != NpcState.PlayerConversation) return;
            playerConversationPartner = null;
            CurrentState = resumeState;
            if (navigation != null && navigation.enabled && navigation.isOnNavMesh)
                navigation.isStopped = CurrentState != NpcState.Moving;
        }

        private void StopConversation()
        {
            if (CurrentState != NpcState.Conversation) return;
            conversationPartner = null;
            CurrentState = resumeState;
            nextConversationTime = Time.time + UnityEngine.Random.Range(25f, 50f);
            if (label != null) label.ClearSpeech();
            if (navigation != null && navigation.enabled && navigation.isOnNavMesh)
                navigation.isStopped = CurrentState != NpcState.Moving;
        }
    }
}
