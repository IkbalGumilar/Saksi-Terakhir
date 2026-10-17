using System;
using System.Collections.Generic;
using SaksiTerakhir.Navigation;
using SaksiTerakhir.Story;
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
        private bool populationPrepared;
        private bool ambientSeated;
        private Vector3 ambientStandingPosition, ambientVisualScale;
        private bool ambientUpdatePosition;
        private Transform ambientVisual;
        private static readonly List<OfficeNpcAgent> activeActors = new List<OfficeNpcAgent>();
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
        private bool standingNavigationUpdatePosition;
        private Rigidbody seatedBody;
        private RigidbodyInterpolation standingBodyInterpolation;
        private CapsuleCollider physicalCapsule;
        private BossOfficeGate[] officeGates = Array.Empty<BossOfficeGate>();
        private Predicate<Collider> ignoreAuthorizedBarrier;
        private Predicate<Collider> ignoreNavigatedActors;
        private Predicate<Vector3> acceptPopulationGround;
        private NpcPopulationMember populationMember;
        private NpcPassageYield passageYield;
        private bool manualSteering;
        private Vector3 resolvedStoryDestination, resolvedActivityDestination;
        private bool storyPathValidated;
        private bool traversingLink, linkUpdatePosition;
        private readonly List<Vector3> linkRoute = new List<Vector3>();
        private int linkRouteIndex;
        private float nextLinkRouteAttempt;
        private Vector3 linkProgressPosition;
        private float linkStuckSince;
        private bool replaceStoryPathAfterLink;
        private bool resumeActivityAfterLink;
        private bool conversationOwnsPosition, conversationUpdatePosition;
        private NavMeshPath bypassTailPath;
        private bool bypassWaypointActive;
        private Vector3 bypassWaypoint;
        private float bypassStartedAt, nextBypassAttempt, detourActiveSince;
        private float vehicleEvadeUntil, nextVehicleEvadeRepath;
        private Vector3 vehicleEvadeTarget;
        private bool vehicleEvadeMoving;
        private static readonly Collider[] OverlapScratch = new Collider[32];
        private static readonly float[] BypassAngles = { 35f, -35f, 60f, -60f, 85f, -85f, 115f, -115f };
        private static readonly float[] BypassDistances = { 1.15f, 1.85f, 2.6f };
        private const float StallRetrySeconds = 2.5f;

        public NpcProfile Profile => profile;
        public string DisplayName => profile != null ? profile.DisplayName : displayName;
        public NpcRole Role => profile != null ? profile.Role : role;
        public NpcActivityPoint Home => home;
        public NpcActivityPoint[] Route => route;
        public bool Stationary => profile != null ? profile.Stationary : stationary;
        public NpcPopulationMember PopulationMember
        {
            get => populationMember != null ? populationMember : (populationMember = GetComponent<NpcPopulationMember>());
            internal set => populationMember = value;
        }
        public NpcState CurrentState { get; private set; } = NpcState.Waiting;
        public int CompletedActivities { get; private set; }
        public int Conversations { get; private set; }
        public float TravelMeters { get; private set; }
        public int VisitedFloorMask { get; private set; }
        public int FailedPathAttempts { get; private set; }
        public NpcActivityPoint Destination => destination;
        public NavMeshAgent Navigation => navigation;
        public static IReadOnlyList<OfficeNpcAgent> ActiveActors => activeActors;
        public bool HasStoryOrder => storyHeld || storyMoveRequested || storySeated;
        public bool IsDialogueBusy => CurrentState == NpcState.Conversation
            || CurrentState == NpcState.PlayerConversation;
        public bool IsAmbientSeated => ambientSeated;
        internal bool IsCurrentlyStalled => Time.time - stuckSince > 0.65f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPopulationRegistry() => activeActors.Clear();
        public int TrafficPriority => profile != null ? profile.TrafficPriority : 1;
        public Vector3 MovementIntent
        {
            get
            {
                if (!isActiveAndEnabled || storySeated || IsDialogueBusy || storyHeld && !storyMoveRequested
                    || navigation == null || !navigation.enabled || !navigation.isOnNavMesh)
                    return Vector3.zero;
                if (navigation.isOnOffMeshLink && navigation.currentOffMeshLinkData.valid)
                    return navigation.currentOffMeshLinkData.endPos - transform.position;
                if (navigation.hasPath && !navigation.pathPending)
                    return navigation.steeringTarget - transform.position;
                if (storyMoveRequested) return storyDestination - transform.position;
                if (CurrentState == NpcState.Moving && destination != null)
                    return destination.transform.position - transform.position;
                return Vector3.zero;
            }
        }
        public bool CanConverse => isActiveAndEnabled && navigation != null && navigation.isOnNavMesh
            && CurrentState == NpcState.Activity && Time.time >= nextConversationTime
            && !storyHeld && !storySeated && !resumeActivityAfterLink
            && (passageYield == null || !passageYield.IsActive);
        public bool StoryAtDestination => storyHeld && storyAtDestination;
        public bool StoryMoveRequested => storyMoveRequested;
        public bool StorySeated => storySeated;
        public bool StoryWaitingForPlayer => storyMoveRequested && storyFollowTarget != null
            && Vector3.Distance(transform.position, storyFollowTarget.position) > storyFollowDistance;

        public void HoldForStory(Transform faceTarget = null)
        {
            SetAmbientSeated(false);
            if (passageYield != null) passageYield.Cancel();
            if (navigation == null) navigation = GetComponent<NavMeshAgent>();
            StopNavigationSafely();
            if (CurrentState == NpcState.Conversation) StopConversation();
            if (CurrentState == NpcState.PlayerConversation) EndPlayerInteraction();
            ReleaseDestination();
            storyHeld = true;
            storyMoveRequested = false;
            resumeActivityAfterLink = false;
            storyAtDestination = false;
            storyPathValidated = false;
            bypassWaypointActive = false;
            detourActiveSince = 0f;
            vehicleEvadeMoving = false;
            vehicleEvadeUntil = 0f;
            TerrainNavigationProbe.ClearDetour(transform);
            storyFaceTarget = faceTarget;
            storyFollowTarget = null;
            CurrentState = NpcState.StoryHeld;
            if (navigation != null && navigation.enabled && navigation.isOnNavMesh)
            {
                navigation.isStopped = true;
                navigation.velocity = Vector3.zero;
                if (!navigation.isOnOffMeshLink) navigation.ResetPath();
            }
        }

        public void ReleaseStoryHold()
        {
            StopNavigationSafely();
            SetStorySeated(false);
            if (!storyHeld) return;
            if (navigation != null && navigation.enabled)
            {
                if (navigation.isOnNavMesh)
                {
                    navigation.isStopped = true;
                    navigation.velocity = Vector3.zero;
                    if (!navigation.isOnOffMeshLink) navigation.ResetPath();
                }
                if (profile != null) navigation.speed = profile.MovementSpeed;
            }
            storyHeld = false;
            storyMoveRequested = false;
            storyAtDestination = false;
            storyPathValidated = false;
            bypassWaypointActive = false;
            detourActiveSince = 0f;
            TerrainNavigationProbe.ClearDetour(transform);
            storyFaceTarget = null;
            storyFollowTarget = null;
            CurrentState = NpcState.Waiting;
            resumeActivityAfterLink = navigation != null && navigation.enabled
                && navigation.isOnNavMesh && navigation.isOnOffMeshLink;
            nextDecision = Time.time + 0.5f;
        }

        public bool TrySetStoryDestination(Vector3 target, float speed)
        {
            if (passageYield != null) passageYield.Cancel();
            RestoreLinkPosition();
            SetStorySeated(false);
            bool talking = IsDialogueBusy;
            if (!storyHeld)
            {
                if (talking)
                {
                    storyHeld = true;
                    ReleaseDestination();
                    resumeActivityAfterLink = false;
                }
                else HoldForStory();
            }
            storyDestination = target;
            storySpeed = Mathf.Max(0.1f, speed);
            storyMoveRequested = true;
            storyAtDestination = false;
            storyPathValidated = false;
            bypassWaypointActive = false;
            detourActiveSince = 0f;
            vehicleEvadeMoving = false;
            vehicleEvadeUntil = 0f;
            TerrainNavigationProbe.ClearDetour(transform);
            if (talking) { PauseForConversation(); return true; }
            return TryStartStoryPath();
        }

        public void SetStoryFacing(Transform target) => storyFaceTarget = target;

        public void SetStorySeated(bool seated)
        {
            if (storySeated == seated) return;
            if (navigation == null) navigation = GetComponent<NavMeshAgent>();
            ReleaseConversationPosition();
            RestoreLinkPosition();
            if (seated)
            {
                seatedBody = GetComponent<Rigidbody>();
                if (seatedBody != null && seatedBody.isKinematic)
                {
                    standingBodyInterpolation = seatedBody.interpolation;
                    // A paused menu has no physics ticks to refresh interpolation's
                    // old standing pose. Let the exact seated transform own the body.
                    seatedBody.interpolation = RigidbodyInterpolation.None;
                }
                if (navigation != null)
                {
                    standingNavigationUpdatePosition = navigation.updatePosition;
                    if (navigation.enabled && navigation.isOnNavMesh)
                    {
                        navigation.isStopped = true;
                        navigation.velocity = Vector3.zero;
                        if (!navigation.isOnOffMeshLink) navigation.ResetPath();
                    }
                    // Keep a valid internal standing point without pulling the authored chair pose onto the mesh.
                    navigation.updatePosition = false;
                }
                storyCapsuleVisual = transform.Find("Capsule Visual");
                storyFacingMarker = transform.Find("Facing Marker");
                storyLabelRoot = transform.Find("NPC Label");
                if (storyCapsuleVisual != null)
                {
                    standingCapsulePosition = storyCapsuleVisual.localPosition;
                    standingCapsuleScale = storyCapsuleVisual.localScale;
                    storyCapsuleVisual.localPosition = standingCapsulePosition;
                    storyCapsuleVisual.localScale = new Vector3(standingCapsuleScale.x,
                        standingCapsuleScale.y * 0.58f, standingCapsuleScale.z);
                }
                if (storyFacingMarker != null) standingMarkerPosition = storyFacingMarker.localPosition;
                if (storyLabelRoot != null) standingLabelPosition = storyLabelRoot.localPosition;
                if (storyFacingMarker != null)
                    storyFacingMarker.localPosition = standingMarkerPosition + Vector3.down * 0.25f;
                if (storyLabelRoot != null)
                    storyLabelRoot.localPosition = standingLabelPosition + Vector3.down * 0.4f;
            }
            else
            {
                if (storyCapsuleVisual != null)
                {
                    storyCapsuleVisual.localPosition = standingCapsulePosition;
                    storyCapsuleVisual.localScale = standingCapsuleScale;
                }
                if (storyFacingMarker != null) storyFacingMarker.localPosition = standingMarkerPosition;
                if (storyLabelRoot != null) storyLabelRoot.localPosition = standingLabelPosition;
                if (navigation != null)
                {
                    navigation.updatePosition = standingNavigationUpdatePosition;
                    if (navigation.enabled && navigation.isOnNavMesh)
                    {
                        Vector3 standingPos = ResolveClearStandingPosition(navigation.nextPosition);
                        if ((standingPos - navigation.nextPosition).sqrMagnitude > 0.0001f)
                            navigation.Warp(standingPos);
                        transform.position = standingPos;
                    }
                }
                if (seatedBody != null && seatedBody.isKinematic)
                    seatedBody.interpolation = standingBodyInterpolation;
            }
            storySeated = seated;
        }

        private Vector3 ResolveClearStandingPosition(Vector3 candidate)
        {
            if (HasStandingClearance(candidate)) return candidate;
            var filter = new NavMeshQueryFilter { agentTypeID = navigation.agentTypeID, areaMask = navigation.areaMask };
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            else forward.Normalize();
            foreach (float radius in new[] { 0.25f, 0.45f, 0.65f })
            {
                foreach (float angle in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
                {
                    Vector3 offset = Quaternion.AngleAxis(angle, Vector3.up) * forward * radius;
                    if (NavMesh.SamplePosition(candidate + offset, out NavMeshHit hit, 0.4f, filter)
                        && Mathf.Abs(hit.position.y - candidate.y) <= 0.35f
                        && HasStandingClearance(hit.position))
                        return hit.position;
                }
            }
            return candidate;
        }

        private bool HasStandingClearance(Vector3 feet)
        {
            float step = navigation != null ? Mathf.Max(0.05f, NavMesh.GetSettingsByID(navigation.agentTypeID).agentClimb) : 0.2f;
            float r = PhysicalRadius;
            float h = PhysicalHeight;
            float lower = Mathf.Min(h - r, r + step) + 0.035f;
            float upper = Mathf.Max(lower, h - r);
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * lower,
                feet + Vector3.up * upper, r + 0.02f, OverlapScratch, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = OverlapScratch[i];
                if (c == null || c.transform == transform || c.transform.IsChildOf(transform)
                    || IsNavigatedActorOrAuthorizedBarrier(c)) continue;
                return false;
            }
            return true;
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

        /// <summary>Called on an inactive pooled actor before it is shown at another post.</summary>
        public void PrepareForPopulationSpawn(NpcProfile asset, NpcActivityPoint homePoint,
            NpcActivityPoint[] activityRoute, OfficeNpcDirector owner)
        {
            EnsureComponents();
            SetAmbientSeated(false);
            SetStorySeated(false);
            StopConversation(); EndPlayerInteraction(); ReleaseDestination();
            if (passageYield != null) passageYield.Cancel();
            StopNavigationSafely(); TerrainNavigationProbe.ClearDetour(transform);
            storyHeld = storyMoveRequested = storyAtDestination = storyPathValidated = false;
            storyFaceTarget = storyFollowTarget = null;
            resumeActivityAfterLink = replaceStoryPathAfterLink = false;
            bypassWaypointActive = vehicleEvadeMoving = false;
            detourActiveSince = vehicleEvadeUntil = nextBypassAttempt = 0f;
            conversationPartner = null; playerConversationPartner = null;
            doorWaitUntil = nextStoryRetry = nextStoryWarning = 0f;
            CompletedActivities = Conversations = VisitedFloorMask = FailedPathAttempts = 0;
            TravelMeters = 0f; CurrentState = NpcState.Waiting;
            SetProfile(asset); BindWaypoints(homePoint, activityRoute); SetDirector(owner);
            if (navigation != null && asset != null) navigation.speed = asset.MovementSpeed;
            navigation.updatePosition = true;
            if (label != null) { label.SetName(DisplayName); label.ClearSpeech(); }
            started = false; populationPrepared = true; enabled = true;
        }

        public void SetAmbientSeated(bool seated)
        {
            if (ambientSeated == seated || seated && HasStoryOrder) return;
            EnsureComponents();
            if (seated)
            {
                ambientStandingPosition = transform.position;
                ambientUpdatePosition = navigation.updatePosition;
                StopNavigationSafely(); navigation.updatePosition = false;
                ambientVisual = transform.Find("Capsule Visual");
                if (ambientVisual != null)
                {
                    ambientVisualScale = ambientVisual.localScale;
                    ambientVisual.localScale = new Vector3(ambientVisualScale.x, ambientVisualScale.y * 0.58f, ambientVisualScale.z);
                }
            }
            else
            {
                if (ambientVisual != null) ambientVisual.localScale = ambientVisualScale;
                transform.position = ambientStandingPosition;
                if (navigation != null)
                {
                    if (navigation.enabled && navigation.isOnNavMesh) navigation.Warp(ambientStandingPosition);
                    navigation.updatePosition = ambientUpdatePosition;
                }
            }
            ambientSeated = seated;
        }

        public void SetAmbientSeatPosition(Vector3 floorPosition)
        {
            if (ambientSeated && !HasStoryOrder) transform.position = floorPosition;
        }

        private void Awake() => EnsureComponents();

        private void EnsureComponents()
        {
            if (navigation != null && candidatePath != null) return;
            navigation = GetComponent<NavMeshAgent>();
            physicalCapsule = GetComponent<CapsuleCollider>();
            officeGates = FindObjectsByType<BossOfficeGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            ignoreAuthorizedBarrier = IsAuthorizedBarrier;
            ignoreNavigatedActors = IsNavigatedActorOrAuthorizedBarrier;
            acceptPopulationGround = IsAllowedPopulationGround;
            // Agent avoidance and physical clearance must use the same authored capsule width.
            if (physicalCapsule != null) navigation.radius = PhysicalRadius;
            navigation.autoTraverseOffMeshLink = false;
            if (profile != null) navigation.speed = profile.MovementSpeed;
            label = GetComponentInChildren<NpcLabel>(true);
            candidatePath = new NavMeshPath();
            bypassTailPath = new NavMeshPath();
            passageYield = GetComponent<NpcPassageYield>();
            if (passageYield == null) passageYield = gameObject.AddComponent<NpcPassageYield>();
        }

        private void Start()
        {
            if (started) return;
            InitializeJourney();
        }

        private void InitializeJourney()
        {
            EnsureComponents();
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
            if (!activeActors.Contains(this)) activeActors.Add(this);
            if (populationPrepared) { populationPrepared = false; InitializeJourney(); return; }
            if (!started) return;
            lastPosition = transform.position;
            CurrentState = storyHeld ? NpcState.StoryHeld : NpcState.Waiting;
            nextDecision = Time.time + 0.5f;
        }

        private void OnDisable()
        {
            activeActors.Remove(this);
            SetAmbientSeated(false);
            if (passageYield != null) passageYield.Cancel();
            TerrainNavigationProbe.ClearDetour(transform);
            ReleaseDestination();
            StopConversation();
            EndPlayerInteraction();
            StopNavigationSafely();
        }

        private void Update()
        {
            Vector3 currentPosition = transform.position;
            float displacement = Vector3.Distance(currentPosition, lastPosition);
            if (displacement < 2f) TravelMeters += displacement;
            lastPosition = currentPosition;

            if (Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
            if (CurrentState == NpcState.PlayerConversation)
            {
                PauseForConversation();
                if (playerConversationPartner != null)
                    Face(playerConversationPartner.position - transform.position);
                return;
            }

            if (CurrentState == NpcState.Conversation)
            {
                PauseForConversation();
                if (conversationPartner == null || !conversationPartner.isActiveAndEnabled
                    || conversationPartner.CurrentState != NpcState.Conversation
                    || conversationPartner.conversationPartner != this || Time.time >= conversationUntil)
                    StopConversation();
                else Face(conversationPartner.transform.position - transform.position);
                return;
            }

            // Dialogue owns the actor before a stored story order or traffic sidestep.
            // A genuinely held or seated actor never participates in patrol yielding.
            if (storySeated || storyHeld && !storyMoveRequested)
            {
                UpdateStoryOrder();
                return;
            }
            if (passageYield != null && passageYield.TryHandleMovement()) return;
            if (storyHeld)
            {
                UpdateStoryOrder();
                return;
            }
            if (Time.time < vehicleEvadeUntil)
            {
                UpdateVehicleEvade();
                return;
            }
            if (vehicleEvadeMoving)
            {
                EndVehicleEvade();
            }

            if (resumeActivityAfterLink && navigation != null && navigation.enabled && navigation.isOnNavMesh)
            {
                if (navigation.isOnOffMeshLink) ApplyPhysicalSteering();
                else
                {
                    resumeActivityAfterLink = false;
                    RestoreLinkPosition();
                    if (destination == null || !TryBeginJourney(destination)) ScheduleNext(0.1f, 0.3f);
                }
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
            if (IsDialogueBusy)
                return false;
            if (navigation == null || !navigation.enabled || !navigation.isOnNavMesh)
                return false;
            if (navigation.isOnOffMeshLink)
            {
                // Finish the currently supported seam before replacing its path.
                // ResetPath would immediately teleport its internal position to the far end.
                replaceStoryPathAfterLink = true;
                navigation.speed = storySpeed;
                CurrentState = NpcState.StoryMoving;
                return true;
            }
            RestoreLinkPosition();
            replaceStoryPathAfterLink = false;
            bypassWaypointActive = false;
            detourActiveSince = 0f;
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
            resolvedStoryDestination = targetHit.position;
            storyPathValidated = true;
            if (IsAtDestination(resolvedStoryDestination))
            {
                FinishStoryArrival();
                return true;
            }
            navigation.isStopped = true;
            if (!navigation.SetPath(candidatePath))
            {
                WarnStoryPath();
                return false;
            }
            CurrentState = NpcState.StoryMoving;
            progressPosition = transform.position;
            stuckSince = Time.time;
            ApplyPhysicalSteering();
            return true;
        }

        private void UpdateStoryOrder()
        {
            if (storyFaceTarget != null && (storySeated || CurrentState != NpcState.StoryMoving))
                Face(storyFaceTarget.position - transform.position);
            if (storySeated || !storyMoveRequested) return;
            if (replaceStoryPathAfterLink && navigation != null && navigation.enabled
                && navigation.isOnNavMesh && !navigation.isOnOffMeshLink)
            { TryStartStoryPath(); return; }
            if (navigation == null || !navigation.enabled || !navigation.isOnNavMesh)
            {
                if (Time.time >= nextStoryRetry)
                {
                    nextStoryRetry = Time.time + 0.5f;
                    TryAttach();
                    TryStartStoryPath();
                }
                return;
            }
            // A completed zero-length path legitimately has hasPath=false.
            if (storyPathValidated && IsAtDestination(resolvedStoryDestination))
            {
                FinishStoryArrival();
                return;
            }
            RefreshBypassWaypoint(resolvedStoryDestination);
            if (CurrentState != NpcState.StoryMoving || !navigation.hasPath)
            {
                if (Time.time >= nextStoryRetry)
                {
                    nextStoryRetry = Time.time + 0.5f;
                    TryStartStoryPath();
                }
                return;
            }
            if (StoryWaitingForPlayer)
            {
                navigation.isStopped = true;
                navigation.velocity = Vector3.zero;
                stuckSince = Time.time;
                return;
            }
            if (Time.time < doorWaitUntil)
            {
                stuckSince = Time.time;
                return;
            }
            if (navigation.pathPending) return;
            if (director != null && director.TryOpenDoorAhead(this, GetDoorProbeTarget(), out float waitUntil))
            {
                doorWaitUntil = waitUntil;
                navigation.isStopped = true;
                navigation.velocity = Vector3.zero;
                stuckSince = Time.time;
                return;
            }
            if (navigation.pathStatus != NavMeshPathStatus.PathComplete)
            {
                navigation.ResetPath();
                navigation.isStopped = true;
                navigation.velocity = Vector3.zero;
                CurrentState = NpcState.StoryHeld;
                WarnStoryPath();
                return;
            }
            ApplyPhysicalSteering();
            if (HasTravelStalled())
            {
                navigation.ResetPath();
                navigation.isStopped = true;
                navigation.velocity = Vector3.zero;
                CurrentState = NpcState.StoryHeld;
                nextStoryRetry = Time.time + 0.5f;
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
            if (navigation != null && navigation.enabled && navigation.isOnNavMesh && navigation.isOnOffMeshLink)
            { resumeActivityAfterLink = true; return false; }
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
            NpcPopulationMember population = GetComponent<NpcPopulationMember>();
            if (population != null && population.IsManaged && !population.LifetimeProtected
                && Time.time >= vehicleEvadeUntil + 4f
                && population.SpawnPoint != null && !population.SpawnPoint.AllowsPath(candidatePath.corners))
            {
                point.Release(this); FailedPathAttempts++; return false;
            }
            bool changedDestination = destination != point;
            ReleaseDestination();
            destination = point;
            // ReleaseDestination may have released this same point after a retry.
            point.TryReserve(this);
            resolvedActivityDestination = targetHit.position;
            bypassWaypointActive = false;
            detourActiveSince = 0f;
            if (changedDestination) TerrainNavigationProbe.ClearDetour(transform);
            navigation.isStopped = true;
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
            ApplyPhysicalSteering();
            return true;
        }

        private void UpdateJourney()
        {
            if (destination == null || !destination.isActiveAndEnabled) { AbandonJourney(); return; }
            if (director != null && director.IsStoryRestricted(destination)) { AbandonJourney(); return; }
            if (Time.time < doorWaitUntil) { stuckSince = Time.time; return; }
            if (navigation.pathPending) return;
            RefreshBypassWaypoint(resolvedActivityDestination);

            if (Time.time >= nextPathCheck)
            {
                nextPathCheck = Time.time + 0.2f;
                if (director != null && director.TryOpenDoorAhead(this, GetDoorProbeTarget(), out float waitUntil))
                {
                    doorWaitUntil = waitUntil;
                    navigation.isStopped = true;
                    navigation.velocity = Vector3.zero;
                    stuckSince = Time.time + Mathf.Max(0f, waitUntil - Time.time);
                    return;
                }
            }
            if (navigation.pathStatus != NavMeshPathStatus.PathComplete) { AbandonJourney(); return; }
            if (IsAtDestination(resolvedActivityDestination))
            {
                navigation.isStopped = true;
                navigation.velocity = Vector3.zero;
                navigation.ResetPath();
                CurrentState = NpcState.Activity;
                // Do not loiter inside a zebra cross or vehicle intersection lane.
                activityRemaining = IsOnZebraCrossOrRoadway(transform.position)
                    ? 0.05f : destination.ChooseDwellTime();
                VisitedFloorMask |= 1 << Mathf.Clamp(destination.Floor, 0, 30);
                return;
            }
            if (!navigation.hasPath) { AbandonJourney(); return; }
            ApplyPhysicalSteering();
            if (HasTravelStalled())
            {
                NpcActivityPoint previousDestination = destination;
                if (!TryBeginJourney(previousDestination)) AbandonJourney();
            }
        }

        private Vector3 GetDoorProbeTarget()
        {
            Vector3 corner = navigation.steeringTarget;
            Vector3 toCorner = corner - transform.position;
            toCorner.y = 0f;
            if (toCorner.sqrMagnitude >= 1.35f * 1.35f || toCorner.sqrMagnitude < 0.0004f || !navigation.hasPath)
                return corner;
            Vector3 toDest = navigation.destination - corner;
            toDest.y = 0f;
            if (toDest.sqrMagnitude > 0.04f && Vector3.Dot(toCorner.normalized, toDest.normalized) > 0.25f)
                return transform.position + toCorner.normalized * 1.5f;
            return corner;
        }

        private bool HasTravelStalled()
        {
            if ((transform.position - progressPosition).sqrMagnitude > 0.08f * 0.08f)
            {
                progressPosition = transform.position;
                stuckSince = Time.time;
                return false;
            }
            return Time.time - stuckSince > StallRetrySeconds && !navigation.isOnOffMeshLink;
        }

        private float PhysicalRadius => physicalCapsule != null
            ? physicalCapsule.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z))
            : navigation.radius;

        private float PhysicalHeight => physicalCapsule != null
            ? Mathf.Max(PhysicalRadius * 2f, physicalCapsule.height * Mathf.Abs(transform.lossyScale.y))
            : navigation.height;

        private bool IsAuthorizedBarrier(Collider collider)
        {
            foreach (BossOfficeGate gate in officeGates)
                if (gate != null && gate.IsPlayerBarrier(collider) && gate.CanOpenFor(transform)) return true;
            return false;
        }

        private bool IsNavigatedActorOrAuthorizedBarrier(Collider collider)
        {
            if (IsAuthorizedBarrier(collider)) return true;
            OfficeNpcAgent other = collider.GetComponentInParent<OfficeNpcAgent>();
            if (other == null || other == this || other.StorySeated || other.Navigation == null
                || !other.Navigation.isActiveAndEnabled || !other.Navigation.isOnNavMesh) return false;
            if (NpcPassageYield.IsMovableBody(collider)) return true;
            // Link actors keep their physical support guard while taking turns, but
            // stationary conversation/seated actors must remain solid obstacles.
            return other.Navigation.isOnOffMeshLink
                && (other.CurrentState == NpcState.StoryMoving || other.CurrentState == NpcState.Moving);
        }

        private bool IsAtDestination(Vector3 target)
        {
            // ResetPath completes a live link immediately; wait for normal traversal instead.
            if (navigation.isOnOffMeshLink) return false;
            Vector3 delta = target - transform.position;
            float vertical = Mathf.Abs(delta.y);
            delta.y = 0f;
            float tolerance = navigation.stoppingDistance + 0.15f;
            return vertical <= 0.4f && delta.sqrMagnitude <= tolerance * tolerance;
        }

        private void FinishStoryArrival()
        {
            navigation.isStopped = true;
            navigation.velocity = Vector3.zero;
            navigation.ResetPath();
            storyMoveRequested = false;
            storyAtDestination = true;
            bypassWaypointActive = false;
            detourActiveSince = 0f;
            CurrentState = NpcState.StoryHeld;
            TerrainNavigationProbe.ClearDetour(transform);
        }

        internal bool ApplyPhysicalSteering()
        {
            if (storySeated || IsDialogueBusy || storyHeld && !storyMoveRequested)
            {
                StopSteering();
                return false;
            }
            if (!navigation.hasPath || navigation.pathPending) return false;
            if (passageYield != null && passageYield.WaitForLinkTurn()) return false;
            if (navigation.isOnOffMeshLink) return TraverseSupportedLink();
            RestoreLinkPosition();
            Vector3 cornerDelta = navigation.steeringTarget - transform.position;
            Vector3 desired = cornerDelta;
            float planarCornerDistance = Vector3.ProjectOnPlane(cornerDelta, Vector3.up).magnitude;
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(navigation.agentTypeID);
            float step = Mathf.Max(0.05f, settings.agentClimb);
            float slope = settings.agentSlope;
            var filter = new NavMeshQueryFilter { agentTypeID = navigation.agentTypeID, areaMask = navigation.areaMask };
            float distance = Mathf.Max(0.65f, navigation.speed * Mathf.Max(Time.deltaTime, 0.02f) + PhysicalRadius * 1.2f);
            float cornerProbeDistance = planarCornerDistance > 0.05f && planarCornerDistance < distance
                ? Mathf.Max(0.18f, planarCornerDistance) : distance;
            bool alreadyYielding = passageYield != null && passageYield.IsActive;
            NpcPassageYield.Request(transform, desired, PhysicalRadius, 1.8f);
            if (WaitForNewTrafficDecision(alreadyYielding)) return false;
            // Reciprocal avoidance can steer away from the path towards a collider
            // absent from the bake. Guard its actual proposed heading as well.
            if (!manualSteering && !navigation.isOnOffMeshLink
                && navigation.desiredVelocity.sqrMagnitude > 0.01f)
                desired = navigation.desiredVelocity;

            // Detect both static terrain/furniture and physical NPC bodies ahead first.
            bool safe = TerrainNavigationProbe.TryResolveDirection(transform, desired, distance,
                PhysicalRadius, PhysicalHeight, step, slope, filter, out Vector3 direction, ignoreAuthorizedBarrier,
                acceptGround: acceptPopulationGround);
            if (!safe && cornerProbeDistance < distance)
            {
                safe = TerrainNavigationProbe.TryResolveDirection(transform, desired, cornerProbeDistance,
                    PhysicalRadius, PhysicalHeight, step, slope, filter, out direction, ignoreAuthorizedBarrier,
                    acceptGround: acceptPopulationGround);
            }
            if (safe)
            {
                NpcPassageYield.Request(transform, direction, PhysicalRadius, 1.8f);
                if (WaitForNewTrafficDecision(alreadyYielding)) return false;
                navigation.isStopped = false;
                if (Vector3.Angle(Vector3.ProjectOnPlane(desired, Vector3.up), direction) < 1f)
                {
                    // Let native navigation handle acceleration and reciprocal crowd avoidance.
                    // Writing velocity every frame overrides both, even on a clear route.
                    detourActiveSince = 0f;
                    if (manualSteering) navigation.velocity = Vector3.zero;
                    manualSteering = false;
                    return true;
                }
                if (detourActiveSince <= 0f) detourActiveSince = Time.time;
                else if (Time.time - detourActiveSince > 0.4f)
                    TryStartBypassPath(direction, filter, step, slope);
                navigation.velocity = direction * navigation.speed;
                Face(direction);
                manualSteering = true;
                return true;
            }

            // If blocked by an initial actor overlap (e.g. piled movers) or a tight obstacle,
            // verify whether the terrain itself is safe when ignoring movable actors.
            bool terrainSafe = TerrainNavigationProbe.TryResolveDirection(transform, desired, cornerProbeDistance,
                PhysicalRadius, PhysicalHeight, step, slope, filter, out Vector3 terrainDirection, ignoreNavigatedActors,
                acceptGround: acceptPopulationGround);
            if (terrainSafe)
            {
                NpcPassageYield.Request(transform, terrainDirection, PhysicalRadius, 1.8f);
                if (WaitForNewTrafficDecision(alreadyYielding)) return false;
                if (HasMovableActorOverlap(transform.position))
                {
                    navigation.isStopped = false;
                    if (Vector3.Angle(Vector3.ProjectOnPlane(desired, Vector3.up), terrainDirection) < 1f)
                    {
                        if (manualSteering) navigation.velocity = Vector3.zero;
                        manualSteering = false;
                    }
                    else
                    {
                        navigation.velocity = terrainDirection * navigation.speed;
                        Face(terrainDirection);
                        manualSteering = true;
                    }
                    return true;
                }
                if (TryStartBypassPath(terrainDirection, filter, step, slope))
                    return true;
            }
            else
            {
                TryStartBypassPath(desired, filter, step, slope);
            }

            navigation.isStopped = true;
            navigation.velocity = Vector3.zero;
            manualSteering = false;
            return false;
        }

        private bool HasMovableActorOverlap(Vector3 feet)
        {
            float step = navigation != null ? Mathf.Max(0.05f, NavMesh.GetSettingsByID(navigation.agentTypeID).agentClimb) : 0.2f;
            float r = PhysicalRadius;
            float h = PhysicalHeight;
            float lower = Mathf.Min(h - r, r + step) + 0.035f;
            float upper = Mathf.Max(lower, h - r);
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * lower,
                feet + Vector3.up * upper, r + 0.035f, OverlapScratch, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = OverlapScratch[i];
                if (c == null || c.transform == transform || c.transform.IsChildOf(transform)) continue;
                if (IsNavigatedActorOrAuthorizedBarrier(c) && !IsAuthorizedBarrier(c)) return true;
            }
            return false;
        }

        private void RefreshBypassWaypoint(Vector3 finalTarget)
        {
            if (!bypassWaypointActive || navigation == null || !navigation.enabled || !navigation.isOnNavMesh
                || navigation.isOnOffMeshLink) return;
            Vector3 delta = bypassWaypoint - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.45f * 0.45f && Time.time - bypassStartedAt < 2.2f && navigation.hasPath)
                return;
            bypassWaypointActive = false;
            if (navigation.CalculatePath(finalTarget, candidatePath)
                && candidatePath.status == NavMeshPathStatus.PathComplete)
            {
                navigation.SetPath(candidatePath);
                navigation.isStopped = false;
            }
        }

        private bool TryStartBypassPath(Vector3 preferredDirection, NavMeshQueryFilter filter, float step, float slope)
        {
            if (Time.time < nextBypassAttempt || navigation == null || !navigation.enabled
                || !navigation.isOnNavMesh || navigation.isOnOffMeshLink
                || passageYield != null && passageYield.IsActive) return false;
            nextBypassAttempt = Time.time + 0.55f;
            Vector3 finalTarget = storyMoveRequested ? resolvedStoryDestination
                : CurrentState == NpcState.Moving ? resolvedActivityDestination
                : navigation.destination;
            Vector3 baseHeading = preferredDirection;
            baseHeading.y = 0f;
            if (baseHeading.sqrMagnitude < 0.001f)
                baseHeading = Vector3.ProjectOnPlane(finalTarget - transform.position, Vector3.up);
            if (baseHeading.sqrMagnitude < 0.001f) return false;
            baseHeading.Normalize();

            NpcPopulationMember population = GetComponent<NpcPopulationMember>();
            bool checkPopulationPath = !HasStoryOrder && Time.time >= vehicleEvadeUntil + 4f
                && population != null && population.IsManaged && !population.LifetimeProtected
                && population.SpawnPoint != null;

            foreach (float dist in BypassDistances)
            {
                for (int i = -1; i < BypassAngles.Length; i++)
                {
                    Vector3 dir = i < 0 ? baseHeading
                        : Quaternion.AngleAxis(BypassAngles[i], Vector3.up) * baseHeading;
                    Vector3 candidatePos = transform.position + dir * dist;
                    if (!TerrainNavigationProbe.TryGetSupportedSegment(transform, transform.position, candidatePos,
                            PhysicalRadius, PhysicalHeight, step, slope, out Vector3 grounded,
                            ignoreAuthorizedBarrier, acceptPopulationGround)
                        || !NavMesh.SamplePosition(grounded, out NavMeshHit midHit, 0.4f, filter)
                        || Mathf.Abs(midHit.position.y - grounded.y) > 0.35f) continue;
                    if (!navigation.CalculatePath(midHit.position, candidatePath)
                        || candidatePath.status != NavMeshPathStatus.PathComplete) continue;
                    if (checkPopulationPath && !population.SpawnPoint.AllowsPath(candidatePath.corners)) continue;
                    if (!NavMesh.CalculatePath(midHit.position, finalTarget, filter, bypassTailPath)
                        || bypassTailPath.status != NavMeshPathStatus.PathComplete) continue;
                    if (checkPopulationPath && !population.SpawnPoint.AllowsPath(bypassTailPath.corners)) continue;

                    bypassWaypoint = midHit.position;
                    bypassWaypointActive = true;
                    bypassStartedAt = Time.time;
                    detourActiveSince = 0f;
                    navigation.SetPath(candidatePath);
                    navigation.isStopped = false;
                    navigation.velocity = dir * navigation.speed;
                    Face(dir);
                    manualSteering = true;
                    return true;
                }
            }
            return false;
        }

        public static bool IsOnZebraCrossOrRoadway(Vector3 position)
        {
            if (Mathf.Abs(position.y) > 1.6f) return false;
            // Point 5 North Arm Zebra Cross & intersection throat at (30, 0, -55.6) and main street lane.
            if (position.x >= 24.5f && position.x <= 38.5f && position.z >= -61f && position.z <= -51f)
                return true;
            return position.z >= -63.2f && position.z <= -57.2f && position.x >= 28f && position.x <= 46f;
        }

        public void NotifyApproachingVehicle(Vector3 vehiclePosition, Vector3 vehicleForward, float safeHalfWidth)
        {
            if (!isActiveAndEnabled || storySeated || CurrentState == NpcState.PlayerConversation || HasStoryOrder)
                return;
            EnsureComponents();
            if (CurrentState == NpcState.Conversation) StopConversation();
            if (ambientSeated) SetAmbientSeated(false);
            if (passageYield != null && passageYield.IsActive) passageYield.Cancel();
            vehicleEvadeUntil = Mathf.Max(vehicleEvadeUntil, Time.time + 2.4f);
            if (navigation == null || !navigation.enabled || !navigation.isOnNavMesh || navigation.isOnOffMeshLink)
                return;
            float hurrySpeed = Mathf.Max(profile != null ? profile.MovementSpeed : 2f, 3.8f);
            navigation.speed = hurrySpeed;
            if (vehicleEvadeMoving && Time.time < nextVehicleEvadeRepath && navigation.hasPath)
                return;
            nextVehicleEvadeRepath = Time.time + 0.45f;
            Vector3 escapePoint = ResolveVehicleEscapePoint(vehiclePosition, vehicleForward, safeHalfWidth);
            var filter = new NavMeshQueryFilter { agentTypeID = navigation.agentTypeID, areaMask = navigation.areaMask };
            if (NavMesh.SamplePosition(escapePoint, out NavMeshHit hit, 3f, filter)
                && Mathf.Abs(hit.position.y - transform.position.y) <= 0.6f
                && navigation.CalculatePath(hit.position, candidatePath)
                && candidatePath.status == NavMeshPathStatus.PathComplete)
            {
                vehicleEvadeTarget = hit.position;
                vehicleEvadeMoving = true;
                TerrainNavigationProbe.ClearDetour(transform);
                navigation.SetPath(candidatePath);
                navigation.isStopped = false;
                stuckSince = Time.time;
                progressPosition = transform.position;
            }
        }

        private Vector3 ResolveVehicleEscapePoint(Vector3 vehiclePosition, Vector3 vehicleForward, float safeHalfWidth)
        {
            Vector3 pos = transform.position;
            // If on the Point 5 North Arm zebra cross, hurry straight to the nearest sidewalk curb.
            if (pos.x >= 23.5f && pos.x <= 39.5f && pos.z >= -61.5f && pos.z <= -50.5f)
            {
                float targetX = pos.x <= 32f ? 21.5f : 41.5f;
                return new Vector3(targetX, pos.y, Mathf.Clamp(pos.z, -57.5f, -55.5f));
            }
            vehicleForward.y = 0f;
            if (vehicleForward.sqrMagnitude < 0.001f) vehicleForward = Vector3.forward;
            else vehicleForward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, vehicleForward).normalized;
            Vector3 rel = pos - vehiclePosition;
            rel.y = 0f;
            float lateral = Vector3.Dot(rel, right);
            float side = lateral >= 0f ? 1f : -1f;
            float push = Mathf.Max(safeHalfWidth + 2f, Mathf.Abs(lateral) + 2f);
            return vehiclePosition + vehicleForward * Mathf.Max(0f, Vector3.Dot(rel, vehicleForward))
                + right * (side * push);
        }

        private void UpdateVehicleEvade()
        {
            if (navigation == null || !navigation.enabled || !navigation.isOnNavMesh) return;
            float hurrySpeed = Mathf.Max(profile != null ? profile.MovementSpeed : 2f, 3.8f);
            navigation.speed = hurrySpeed;
            if (vehicleEvadeMoving && navigation.hasPath)
            {
                if (IsAtDestination(vehicleEvadeTarget))
                {
                    navigation.isStopped = true;
                    navigation.velocity = Vector3.zero;
                    navigation.ResetPath();
                    vehicleEvadeMoving = false;
                }
                else
                {
                    ApplyPhysicalSteering();
                }
            }
        }

        private void EndVehicleEvade()
        {
            vehicleEvadeMoving = false;
            if (navigation != null && navigation.enabled)
            {
                navigation.speed = profile != null ? profile.MovementSpeed : 2f;
                if (navigation.isOnNavMesh && !navigation.isOnOffMeshLink)
                {
                    if (destination == null || !TryBeginJourney(destination))
                        ScheduleNext(0.1f, 0.4f);
                }
            }
        }

        private bool IsAllowedPopulationGround(Vector3 ground)
        {
            if (HasStoryOrder || Time.time < vehicleEvadeUntil + 4f || vehicleEvadeMoving) return true;
            if (populationMember == null) populationMember = GetComponent<NpcPopulationMember>();
            return populationMember == null || !populationMember.IsManaged || populationMember.IsMissionPinned
                || populationMember.SpawnPoint == null || populationMember.SpawnPoint.AllowsPosition(ground);
        }

        private bool WaitForNewTrafficDecision(bool alreadyYielding)
        {
            if (alreadyYielding || passageYield == null
                || !passageYield.IsActive && !passageYield.MustWaitForTraffic) return false;
            // Request can make this actor the loser. Its new sidestep path must
            // survive this call; the next update lets the traffic routine drive it.
            StopSteering();
            return true;
        }

        private void StopSteering()
        {
            manualSteering = false;
            if (navigation == null || !navigation.enabled || !navigation.isOnNavMesh) return;
            navigation.isStopped = true;
            navigation.velocity = Vector3.zero;
        }

        private bool TraverseSupportedLink()
        {
            OffMeshLinkData link = navigation.currentOffMeshLinkData;
            if (!link.valid) return false;
            Vector3 from = transform.position;
            if (!traversingLink)
            {
                traversingLink = true;
                linkUpdatePosition = navigation.updatePosition;
                navigation.updatePosition = false;
                linkRoute.Clear();
                linkRouteIndex = 0;
                nextLinkRouteAttempt = 0f;
                linkProgressPosition = from;
                linkStuckSince = Time.time;
            }
            else if ((from - linkProgressPosition).sqrMagnitude > 0.05f * 0.05f)
            {
                linkProgressPosition = from;
                linkStuckSince = Time.time;
            }
            else if (Time.time - linkStuckSince > 1.2f)
            {
                linkRoute.Clear();
                linkStuckSince = Time.time;
            }
            navigation.isStopped = true;
            navigation.velocity = Vector3.zero;
            Vector3 delta = link.endPos - from;
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(navigation.agentTypeID);
            float gap = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
            var filter = new NavMeshQueryFilter { agentTypeID = navigation.agentTypeID, areaMask = navigation.areaMask };
            float low = Mathf.Min(link.startPos.y, link.endPos.y) - settings.agentClimb;
            float high = Mathf.Max(link.startPos.y, link.endPos.y) + settings.agentClimb;
            Predicate<Vector3> allowedGround = point =>
            {
                if (from.y < low && point.y < from.y - 0.01f || from.y > high && point.y > from.y + 0.01f) return false;
                if (from.y >= low && from.y <= high && (point.y < low || point.y > high)) return false;
                Vector3 line = Vector3.ProjectOnPlane(link.endPos - link.startPos, Vector3.up);
                Vector3 offset = Vector3.ProjectOnPlane(point - link.startPos, Vector3.up);
                float t = line.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(offset, line) / line.sqrMagnitude) : 0f;
                return (offset - line * t).sqrMagnitude <= 2.8f * 2.8f;
            };
            if (linkRoute.Count == 0)
            {
                if (Time.time < nextLinkRouteAttempt) return false;
                nextLinkRouteAttempt = Time.time + 0.6f;
                Vector3 routeStart = Vector3.Distance(from, link.startPos) > 0.2f ? from : link.startPos;
                if (!SupportedLinkDetour.TryBuild(transform, routeStart, link.endPos,
                        PhysicalRadius, PhysicalHeight, settings.agentClimb, settings.agentSlope,
                        ignoreNavigatedActors, allowedGround, linkRoute)
                    && (routeStart == link.startPos || !SupportedLinkDetour.TryBuild(transform, link.startPos, link.endPos,
                        PhysicalRadius, PhysicalHeight, settings.agentClimb, settings.agentSlope,
                        ignoreNavigatedActors, allowedGround, linkRoute))) return false;
                linkRouteIndex = 0;
            }
            while (linkRouteIndex < linkRoute.Count - 1
                && Vector3.Distance(from, linkRoute[linkRouteIndex]) <= 0.13f
                && TerrainNavigationProbe.TryGetSupportedSegment(transform, from, linkRoute[linkRouteIndex + 1],
                    PhysicalRadius, PhysicalHeight, settings.agentClimb, settings.agentSlope,
                    out _, ignoreNavigatedActors, allowedGround, true)) linkRouteIndex++;
            Vector3 routeTarget = linkRoute[linkRouteIndex];
            delta = routeTarget - from;
            gap = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
            if (linkRouteIndex == linkRoute.Count - 1 && gap <= 0.15f && Mathf.Abs(delta.y) <= 0.08f
                && TerrainNavigationProbe.TryGetLinkGroundPosition(transform, link.endPos,
                    settings.agentClimb, settings.agentSlope, PhysicalRadius, out Vector3 endpoint, ignoreAuthorizedBarrier)
                && Vector3.Distance(from, endpoint) <= 0.16f
                && TerrainNavigationProbe.TryGetSupportedSegment(transform, from, endpoint,
                    PhysicalRadius, PhysicalHeight, settings.agentClimb, settings.agentSlope,
                    out _, ignoreAuthorizedBarrier, allowedGround, true))
            {
                transform.position = endpoint;
                navigation.nextPosition = endpoint;
                navigation.CompleteOffMeshLink();
                navigation.nextPosition = endpoint;
                RestoreLinkPosition();
                return true;
            }
            float probe = Mathf.Clamp(gap, 0.08f, 0.65f);
            if (!TerrainNavigationProbe.TryResolveDirection(transform, delta, probe,
                    PhysicalRadius, PhysicalHeight, settings.agentClimb, settings.agentSlope,
                    filter, out Vector3 terrainDirection, ignoreNavigatedActors, false, allowedGround, true)) return false;
            NpcPassageYield.Request(transform, terrainDirection, PhysicalRadius, 1.8f);
            if (!TerrainNavigationProbe.TryResolveDirection(transform, terrainDirection, probe,
                    PhysicalRadius, PhysicalHeight, settings.agentClimb, settings.agentSlope,
                    filter, out Vector3 direction, ignoreAuthorizedBarrier, false, allowedGround, true)) return false;
            float stride = Mathf.Min(gap, navigation.speed * Time.deltaTime);
            if (!TerrainNavigationProbe.TryGetLinkGroundPosition(transform, from + direction * stride,
                    settings.agentClimb, settings.agentSlope, PhysicalRadius, out Vector3 ground, ignoreAuthorizedBarrier)) return false;
            // Manual link traversal uses the same small physical steps as ordinary
            // movement. Native interpolation cannot follow a detour around a railing.
            transform.position = ground;
            navigation.nextPosition = ground;
            return true;
        }

        private void RestoreLinkPosition()
        {
            if (!traversingLink) return;
            traversingLink = false;
            linkRoute.Clear();
            if (navigation.enabled && navigation.isOnNavMesh) navigation.nextPosition = transform.position;
            navigation.updatePosition = linkUpdatePosition;
        }

        private void StopNavigationSafely()
        {
            RestoreLinkPosition();
            if (navigation == null || !navigation.enabled || !navigation.isOnNavMesh) return;
            navigation.isStopped = true;
            navigation.velocity = Vector3.zero;
            if (!navigation.isOnOffMeshLink) navigation.ResetPath();
        }

        internal void ResumeAfterYield(Vector3 target)
        {
            if (storyMoveRequested)
            {
                resolvedStoryDestination = target;
                replaceStoryPathAfterLink = true;
            }
            else
            {
                resolvedActivityDestination = target;
                resumeActivityAfterLink = true;
            }
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
            StopNavigationSafely();
            resumeActivityAfterLink = navigation != null && navigation.enabled
                && navigation.isOnNavMesh && navigation.isOnOffMeshLink;
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
            if (other == null || CurrentState == NpcState.PlayerConversation || storySeated
                || storyHeld && !storyMoveRequested) return;
            SetAmbientSeated(false);
            if (CurrentState == NpcState.Conversation) StopConversation();
            if (passageYield != null) passageYield.Cancel();
            resumeState = CurrentState;
            CurrentState = NpcState.Conversation;
            conversationPartner = other;
            conversationUntil = Time.time + duration;
            PauseForConversation();
            Conversations++;
            if (label != null) label.Say(speech, duration);
        }

        public void BeginPlayerInteraction(Transform player)
        {
            SetAmbientSeated(false);
            if (passageYield != null) passageYield.Cancel();
            if (CurrentState == NpcState.PlayerConversation)
            {
                playerConversationPartner = player;
                return;
            }
            if (CurrentState == NpcState.Conversation) StopConversation();
            resumeState = CurrentState;
            CurrentState = NpcState.PlayerConversation;
            playerConversationPartner = player;
            PauseForConversation();
        }

        public void EndPlayerInteraction()
        {
            if (CurrentState != NpcState.PlayerConversation) return;
            playerConversationPartner = null;
            CurrentState = resumeState;
            ReleaseConversationPosition();
            ResumeAfterConversation();
        }

        private void StopConversation()
        {
            if (CurrentState != NpcState.Conversation) return;
            OfficeNpcAgent previousPartner = conversationPartner;
            conversationPartner = null;
            CurrentState = resumeState;
            nextConversationTime = Time.time + UnityEngine.Random.Range(25f, 50f);
            if (label != null) label.ClearSpeech();
            ReleaseConversationPosition();
            ResumeAfterConversation();
            // Interrupt both sides of ambient chat, with pointers cleared before
            // notifying the partner so it cannot recurse back into this actor.
            if (previousPartner != null && previousPartner.conversationPartner == this)
                previousPartner.StopConversation();
        }

        private void PauseForConversation()
        {
            StopSteering();
            if (navigation == null || !navigation.enabled || !navigation.isOnNavMesh || storySeated) return;
            if (!conversationOwnsPosition)
            {
                RestoreLinkPosition();
                conversationUpdatePosition = navigation.updatePosition;
                conversationOwnsPosition = true;
                navigation.updatePosition = false;
            }
            navigation.nextPosition = transform.position;
        }

        private void ReleaseConversationPosition()
        {
            if (!conversationOwnsPosition) return;
            conversationOwnsPosition = false;
            if (navigation == null) return;
            if (navigation.enabled && navigation.isOnNavMesh) navigation.nextPosition = transform.position;
            navigation.updatePosition = conversationUpdatePosition;
        }

        private void ResumeAfterConversation()
        {
            manualSteering = false;
            progressPosition = transform.position;
            stuckSince = Time.time;
            if (!isActiveAndEnabled || navigation == null || !navigation.enabled || !navigation.isOnNavMesh)
            {
                StopSteering();
                return;
            }
            if (storySeated || storyHeld && !storyMoveRequested)
            {
                CurrentState = NpcState.StoryHeld;
                StopSteering();
                return;
            }
            if (storyMoveRequested)
            {
                if (!TryStartStoryPath())
                {
                    CurrentState = NpcState.StoryHeld;
                    nextStoryRetry = Time.time + 0.2f;
                    StopSteering();
                }
                return;
            }
            if (CurrentState == NpcState.Moving || CurrentState == NpcState.Activity && destination != null
                && !IsAtDestination(resolvedActivityDestination) || resumeActivityAfterLink)
            {
                if (navigation.isOnOffMeshLink)
                {
                    resumeActivityAfterLink = true;
                    navigation.isStopped = false;
                }
                else if (destination != null)
                {
                    resumeActivityAfterLink = false;
                    if (!TryBeginJourney(destination)) ScheduleNext(0.1f, 0.3f);
                }
                else ScheduleNext(0.1f, 0.3f);
                return;
            }
            StopSteering();
        }
    }
}
