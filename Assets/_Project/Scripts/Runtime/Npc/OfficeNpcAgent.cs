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
            if (TryAttach())
            {
                if (!TryBeginJourney(home)) ScheduleNext(0.5f, 2f);
            }
        }

        private void OnEnable()
        {
            if (!started) return;
            lastPosition = transform.position;
            CurrentState = NpcState.Waiting;
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
