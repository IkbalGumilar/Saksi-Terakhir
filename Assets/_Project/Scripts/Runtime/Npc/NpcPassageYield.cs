using System.Collections.Generic;
using SaksiTerakhir.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace SaksiTerakhir.Npc
{
    /// <summary>NPC traffic only: sidestep, or backtrack to a safe passing place, then resume the same order.</summary>
    [DisallowMultipleComponent]
    public sealed class NpcPassageYield : MonoBehaviour
    {
        private static readonly List<NpcPassageYield> Workers = new List<NpcPassageYield>();
        private static readonly NpcTrafficDecision Decisions = new NpcTrafficDecision();
        private static readonly RaycastHit[] Hits = new RaycastHit[48];
        private static readonly RaycastHit[] SightHits = new RaycastHit[48];
        private static readonly Collider[] Overlaps = new Collider[48];
        private static readonly List<LinkTurn> LinkTurns = new List<LinkTurn>();
        private sealed class LinkTurn
        {
            public Vector3 Start, End;
            public NpcPassageYield Holder;
            public float Until;
        }

        private OfficeNpcAgent owner;
        private NavMeshAgent navigation;
        private CapsuleCollider capsule;
        private NavMeshPath path;
        private Transform traveller;
        private NpcActivityPoint originalDestination;
        private NpcState originalState;
        private Vector3 originalPosition, pocketPosition, originalNavigationTarget, passageDirection, blockedDirection;
        private float originalSpeed, originalStoppingDistance, nextAttempt, holdUntil, lastRequest, cooldown;
        private bool originalStopped;
        private Vector3 progressPosition;
        private float progressTime;
        private float trafficWaitUntil;
        private int pocketReplans;
        private readonly List<Vector3> trail = new List<Vector3>();
        private readonly HashSet<Collider> initialActors = new HashSet<Collider>();
        // 1: walk to passing place, 2: wait for traffic, 3: return to an idle work spot.
        private int phase;
        public bool IsActive => phase != 0;
        public bool MustWaitForTraffic => Time.time < trafficWaitUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetWorkers() { Workers.Clear(); LinkTurns.Clear(); Decisions.Clear(); }
        private void Awake()
        {
            owner = GetComponent<OfficeNpcAgent>();
            navigation = GetComponent<NavMeshAgent>();
            capsule = GetComponent<CapsuleCollider>();
            path = new NavMeshPath();
        }
        private void OnEnable() { if (!Workers.Contains(this)) Workers.Add(this); }
        private void OnDisable()
        {
            Cancel();
            if (owner != null)
                foreach (NpcPassageYield worker in Workers)
                    if (worker != null && worker != this && worker.owner != null)
                        Decisions.Release(owner.GetInstanceID(), worker.owner.GetInstanceID());
            Workers.Remove(this);
            trail.Clear(); initialActors.Clear(); traveller = null;
            cooldown = trafficWaitUntil = lastRequest = nextAttempt = holdUntil = 0f;
            pocketReplans = 0;
            for (int i = LinkTurns.Count - 1; i >= 0; i--)
                if (LinkTurns[i].Holder == this) LinkTurns.RemoveAt(i);
        }

        public static void Request(Transform requester, Vector3 direction, float radius, float lookAhead)
        {
            direction.y = 0f;
            if (requester == null || direction.sqrMagnitude < 0.0001f) return;
            direction.Normalize();
            OfficeNpcAgent actor = requester.GetComponentInParent<OfficeNpcAgent>();
            NpcPassageYield source = actor != null ? actor.GetComponent<NpcPassageYield>() : null;
            if (actor != null && source == null) return;
            if (source != null && (source.IsActive || !source.CanYield()
                && (source.navigation == null || !source.navigation.isOnNavMesh
                    || !source.navigation.isOnOffMeshLink || actor.MovementIntent.sqrMagnitude < 0.01f))) return;
            Decisions.Expire(Time.time);
            Vector3 origin = requester.position + Vector3.up * 0.95f;
            float reach = Mathf.Max(0.5f, lookAhead);
            float width = Mathf.Max(0.05f, radius) + 0.12f;
            int hitCount = Physics.SphereCastNonAlloc(origin, width, direction, Hits, reach,
                ~0, QueryTriggerInteraction.Ignore);
            if (hitCount == Hits.Length) return;
            // A wall is a wall even when a worker exists behind it.
            float wallDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = Hits[i].collider;
                if (collider == null || collider.transform == requester || collider.transform.IsChildOf(requester)) continue;
                if (collider.GetComponentInParent<OfficeNpcAgent>() == null)
                    wallDistance = Mathf.Min(wallDistance, Hits[i].distance);
            }
            NpcPassageYield nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = Workers.Count - 1; i >= 0; i--)
            {
                NpcPassageYield worker = Workers[i];
                if (worker == null || worker == source || !worker.CanYield()) continue;
                if (requester.GetComponentInParent<SaksiTerakhir.Story.StoryPlayerCinematicMover>() is var follower
                    && follower != null && follower.IsFollowing && follower.FollowTarget == worker.transform) continue;
                Vector3 offset = worker.transform.position - requester.position;
                if (Mathf.Abs(offset.y) > worker.Height * 0.7f) continue;
                offset.y = 0f;
                float along = Vector3.Dot(offset, direction);
                float passageWidth = Mathf.Max(0.05f, radius) + worker.Radius + 0.15f;
                if (along < -worker.Radius || along > reach + worker.Radius
                    || (offset - direction * along).sqrMagnitude > passageWidth * passageWidth) continue;
                bool visible = false;
                for (int j = 0; j < hitCount; j++)
                    if (Hits[j].collider != null && Hits[j].collider.GetComponentInParent<OfficeNpcAgent>() == worker.owner
                        && Hits[j].distance <= wallDistance + 0.01f) { visible = true; break; }
                // Sphere casts omit bodies containing the starting point. Allow only
                // nearby body overlaps here, with an independent sight line through walls.
                if (!visible && offset.sqrMagnitude < passageWidth * passageWidth)
                    visible = HasSight(requester, worker, origin);
                if (!visible || along >= nearestDistance) continue;
                nearest = worker;
                nearestDistance = along;
            }
            if (nearest == null) return;
            if (nearest.IsActive)
            {
                nearest.lastRequest = Time.time;
                nearest.blockedDirection = direction;
                if (source != null) source.trafficWaitUntil = Time.time + 0.15f;
                return;
            }
            if (actor == null || source.navigation.isOnOffMeshLink)
            {
                // Player movement has no traffic priority. A person already on
                // a link also has to finish crossing before it can yield.
                nearest.BeginOrRefresh(requester, direction, Mathf.Max(0.05f, radius));
                return;
            }
            Vector3 theirDirection = Vector3.ProjectOnPlane(nearest.owner.MovementIntent, Vector3.up);
            bool theirsMoving = theirDirection.sqrMagnitude > 0.01f && !nearest.owner.IsCurrentlyStalled;
            bool coDirectional = theirsMoving && Vector3.Dot(direction, theirDirection.normalized) > 0.5f;
            if (coDirectional)
            {
                if (actor.TrafficPriority > nearest.owner.TrafficPriority && !nearest.owner.HasStoryOrder)
                {
                    nearest.BeginOrRefresh(requester, direction, radius);
                    return;
                }
                return; // Same-direction traffic follows normally.
            }
            if (!theirsMoving)
            {
                // An idle, non-dialogue worker clears a walking actor's route.
                nearest.BeginOrRefresh(requester, direction, radius);
                source.trafficWaitUntil = Time.time + 0.15f;
                return;
            }
            int loser = Decisions.ChooseYielding(actor.GetInstanceID(), actor.TrafficPriority,
                nearest.owner.GetInstanceID(), nearest.owner.TrafficPriority, Time.time, () => Random.value < 0.5f);
            if (loser == actor.GetInstanceID())
            {
                source.BeginOrRefresh(nearest.transform, theirDirection.normalized, nearest.Radius);
                if (!source.IsActive) source.trafficWaitUntil = Time.time + 0.15f;
            }
            else
            {
                nearest.BeginOrRefresh(requester, direction, radius);
                source.trafficWaitUntil = Time.time + 0.15f;
            }
        }

        private static bool HasSight(Transform requester, NpcPassageYield worker, Vector3 origin)
        {
            Vector3 delta = worker.transform.position + Vector3.up * 0.95f - origin;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, SightHits, delta.magnitude,
                ~0, QueryTriggerInteraction.Ignore);
            if (count == SightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider c = SightHits[i].collider;
                if (c != null && c.transform != requester && !c.transform.IsChildOf(requester)
                    && c.GetComponentInParent<OfficeNpcAgent>() == null) return false;
            }
            return true;
        }

        private bool CanYield() => owner != null && owner.isActiveAndEnabled && !owner.StorySeated && !owner.IsAmbientSeated
            && (owner.CurrentState == NpcState.Waiting || owner.CurrentState == NpcState.Activity
                || owner.CurrentState == NpcState.Moving || owner.CurrentState == NpcState.StoryMoving
                || owner.CurrentState == NpcState.StoryHeld && owner.StoryMoveRequested)
            && navigation != null && navigation.enabled && navigation.isOnNavMesh && !navigation.isOnOffMeshLink;
        public static bool IsMovableBody(Collider other)
        {
            NpcPassageYield worker = other != null ? other.GetComponentInParent<NpcPassageYield>() : null;
            return worker != null && worker.CanYield();
        }

        private void BeginOrRefresh(Transform requester, Vector3 direction, float requesterRadius)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (IsActive)
            {
                if (traveller == requester) lastRequest = Time.time;
                return;
            }
            if (!CanYield() || Time.time < cooldown) return;
            CaptureInitialActors();
            if (!FindPocket(direction, requesterRadius + Radius + 0.2f, out Vector3 pocket))
            {
                cooldown = Time.time + 0.4f;
                return; // Stop through the ordinary physical guard; never walk through walls/holes.
            }
            originalPosition = transform.position;
            originalDestination = owner.Destination;
            originalState = owner.CurrentState;
            originalSpeed = navigation.speed;
            originalStoppingDistance = navigation.stoppingDistance;
            originalStopped = navigation.isStopped;
            originalNavigationTarget = navigation.hasPath ? navigation.destination : originalPosition;
            traveller = requester;
            passageDirection = direction;
            blockedDirection = direction;
            pocketPosition = pocket;
            pocketReplans = 0;
            lastRequest = Time.time;
            navigation.speed = Mathf.Clamp(originalSpeed, 0.5f, 2f);
            navigation.stoppingDistance = 0.07f;
            phase = 1;
            StartLeg();
        }

        private bool FindPocket(Vector3 direction, float clearance, out Vector3 pocket)
        {
            Vector3 origin = transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            float lateral = Mathf.Clamp(clearance + 0.15f, 0.95f, 1.5f);
            // First move aside. If stairs have no side space, turn back along the
            // already walked route and find a pocket on its wider landing.
            for (int i = 0; i < 6; i++)
            {
                float along = i < 2 ? 0f : i < 4 ? 0.6f : -0.6f;
                Vector3 requested = origin + direction * along + side * (i % 2 == 0 ? lateral : -lateral);
                if (SafePath(origin, requested, out pocket)) return true;
            }
            foreach (float extraLateral in new[] { Mathf.Max(0.75f, clearance), lateral + 0.45f })
            {
                for (int i = 0; i < 6; i++)
                {
                    float along = i < 2 ? 0f : i < 4 ? -0.75f : 0.75f;
                    Vector3 requested = origin + direction * along + side * (i % 2 == 0 ? extraLateral : -extraLateral);
                    if (SafePath(origin, requested, out pocket)) return true;
                }
            }
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                Vector3 back = trail[i];
                if (Vector3.Distance(back, origin) < 0.65f || Vector3.Distance(back, origin) > 6f) continue;
                for (int sign = -1; sign <= 1; sign += 2)
                    if (SafePath(origin, back + side * lateral * sign, out pocket)) return true;
            }
            // Newly spawned workers may have no breadcrumbs yet.
            foreach (float retreat in new[] { 1.2f, 2f, 3f, 4.5f })
                for (int sign = -1; sign <= 1; sign += 2)
                    if (SafePath(origin, origin + direction * retreat + side * lateral * sign, out pocket)) return true;
            pocket = origin;
            return false;
        }

        public bool TryHandleMovement()
        {
            if (!IsActive)
            {
                if (CanYield() && (owner.CurrentState == NpcState.Moving || owner.StoryMoveRequested)
                    && (trail.Count == 0 || Vector3.Distance(trail[trail.Count - 1], transform.position) > 0.45f))
                {
                    trail.Add(transform.position);
                    if (trail.Count > 32) trail.RemoveAt(0);
                }
                return false;
            }
            if (!CanYield() || owner.CurrentState != originalState || owner.Destination != originalDestination)
            { Cancel(); return false; }
            OfficeNpcAgent other = traveller != null ? traveller.GetComponentInParent<OfficeNpcAgent>() : null;
            bool nearby = traveller != null && Vector3.Distance(traveller.position, transform.position) < 2.8f;
            bool recent = Time.time - lastRequest < 0.7f;
            bool beforePass = traveller != null && Vector3.Dot(traveller.position - originalPosition,
                passageDirection) < Radius + 0.8f && Vector3.Distance(traveller.position, originalPosition) < 7f;
            bool traffic = recent || (nearby || beforePass) && (other == null || other.MovementIntent.sqrMagnitude > 0.01f);
            if (other != null && traffic) Decisions.Retain(owner.GetInstanceID(), other.GetInstanceID(), Time.time);
            if (phase == 2)
            {
                if (recent && Time.time > holdUntil + 1f)
                {
                    // A later terrain detour can pass through the first pocket.
                    // Find another spot, rather than refreshing an eternal wait.
                    CaptureInitialActors();
                    float otherRadius = other != null && other.Navigation != null ? other.Navigation.radius : 0.3f;
                    if (pocketReplans++ < 3 && FindPocket(blockedDirection, Radius + otherRadius + 0.2f, out Vector3 next))
                    { pocketPosition = next; phase = 1; StartLeg(); }
                    else { Cancel(); cooldown = Time.time + 0.8f; }
                    return true;
                }
                if (traffic || Time.time < holdUntil) return true;
                if (other != null) Decisions.Release(owner.GetInstanceID(), other.GetInstanceID());
                if (originalState == NpcState.Moving || owner.StoryMoveRequested) { Cancel(); return true; }
                phase = 3;
                CaptureInitialActors();
                StartLeg();
                return true;
            }
            Vector3 target = phase == 3 ? originalPosition : pocketPosition;
            if (!navigation.pathPending && Vector3.Distance(transform.position, target) < 0.13f)
            {
                navigation.ResetPath();
                navigation.isStopped = true;
                navigation.velocity = Vector3.zero;
                if (phase == 3) Cancel();
                else { phase = 2; holdUntil = Time.time + 0.65f; }
                return true;
            }
            if (!navigation.hasPath && Time.time >= nextAttempt) StartLeg();
            if (navigation.hasPath && !TryStepOutOfInitialOverlap()) owner.ApplyPhysicalSteering();
            if (Vector3.Distance(progressPosition, transform.position) > 0.06f)
            { progressPosition = transform.position; progressTime = Time.time; }
            else if (Time.time - progressTime > 2f)
            {
                // A failed detour must not permanently disable ambient conversation.
                Cancel();
                cooldown = Time.time + 0.8f;
            }
            return true;
        }

        private void StartLeg()
        {
            nextAttempt = Time.time + 0.4f;
            CaptureInitialActors();
            Vector3 target = phase == 3 ? originalPosition : pocketPosition;
            if (SafePath(transform.position, target, out _) && navigation.SetPath(path))
            {
                navigation.isStopped = false;
                progressPosition = transform.position;
                progressTime = Time.time;
                TerrainNavigationProbe.ClearDetour(transform);
            }
            else { navigation.isStopped = true; navigation.velocity = Vector3.zero; }
        }

        public void Cancel()
        {
            if (!IsActive) return;
            phase = 0;
            traveller = null;
            initialActors.Clear();
            cooldown = Time.time + 0.5f;
            TerrainNavigationProbe.ClearDetour(transform);
            if (navigation == null) return;
            navigation.speed = originalSpeed;
            navigation.stoppingDistance = originalStoppingDistance;
            if (!navigation.enabled || !navigation.isOnNavMesh) return;
            bool onLink = navigation.isOnOffMeshLink;
            if (!onLink) navigation.ResetPath(); // ResetPath on a live link would snap to its end.
            navigation.velocity = Vector3.zero;
            navigation.isStopped = originalStopped;
            if (owner == null || owner.CurrentState != originalState || owner.Destination != originalDestination) return;
            if (onLink) { owner.ResumeAfterYield(originalNavigationTarget); return; }
            if (originalState == NpcState.Moving || originalState == NpcState.StoryMoving || owner.StoryMoveRequested)
            {
                navigation.SetDestination(originalNavigationTarget);
                navigation.isStopped = false;
            }
            else if (originalState == NpcState.Activity && originalDestination != null
                && Vector3.Distance(transform.position, originalPosition) > 0.2f)
                owner.ResumeAfterYield(originalPosition);
        }

        private bool SafePath(Vector3 from, Vector3 requested, out Vector3 end)
        {
            end = requested;
            var filter = new NavMeshQueryFilter { agentTypeID = navigation.agentTypeID, areaMask = navigation.areaMask };
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(navigation.agentTypeID);
            float distance = Vector3.ProjectOnPlane(requested - from, Vector3.up).magnitude;
            float groundRange = settings.agentClimb + distance * Mathf.Tan(settings.agentSlope * Mathf.Deg2Rad);
            if (!TerrainNavigationProbe.TryGetGroundPosition(transform, requested, groundRange, settings.agentSlope,
                    out Vector3 projected, IsMovableBody)
                || !NavMesh.SamplePosition(projected, out NavMeshHit hit, 0.25f, filter)
                || Mathf.Abs(hit.position.y - projected.y) > 0.15f || !ClearCapsule(hit.position)
                || !PocketAvailable(hit.position) || !NavMesh.CalculatePath(from, hit.position, filter, path)
                || path.status != NavMeshPathStatus.PathComplete) return false;
            NpcPopulationMember population = GetComponent<NpcPopulationMember>();
            if (population != null && population.IsManaged && !owner.HasStoryOrder
                && !population.IsMissionPinned && population.SpawnPoint != null
                && !population.SpawnPoint.AllowsPath(path.corners)) return false;
            Vector3 previous = from;
            float length = 0f;
            foreach (Vector3 corner in path.corners)
            {
                Vector3 segment = corner - previous;
                if (segment.sqrMagnitude < 0.00001f) continue;
                // Traffic detours never own an authored link or jump storeys.
                if (NavMesh.Raycast(previous, corner, out _, filter)) return false;
                length += segment.magnitude;
                if (!TerrainNavigationProbe.TryGetSupportedSegment(transform, previous, corner,
                    Radius, Height, settings.agentClimb, settings.agentSlope, out Vector3 grounded,
                    IgnoreExistingOverlap, null)) return false;
                previous = grounded;
            }
            end = hit.position;
            return path.corners.Length > 0 && length <= distance + 3f;
        }

        private bool ClearCapsule(Vector3 feet)
        {
            float lower = Mathf.Min(Height - Radius,
                Radius + NavMesh.GetSettingsByID(navigation.agentTypeID).agentClimb) + 0.035f;
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * lower,
                feet + Vector3.up * Mathf.Max(lower, Height - Radius), Radius + 0.04f,
                Overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == Overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (!OwnCollider(Overlaps[i])) return false;
            return true;
        }
        private bool PocketAvailable(Vector3 point)
        {
            foreach (NpcPassageYield worker in Workers)
            {
                if (worker == null || worker == this || !worker.IsActive || worker.phase == 3) continue;
                if (Vector3.Distance(point, worker.pocketPosition) < Radius + worker.Radius + 0.15f) return false;
            }
            return true;
        }
        private void CaptureInitialActors()
        {
            initialActors.Clear();
            int count = Physics.OverlapCapsuleNonAlloc(transform.position + Vector3.up * (Radius + 0.05f),
                transform.position + Vector3.up * Mathf.Max(Radius + 0.05f, Height - Radius), Radius + 0.04f,
                Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!OwnCollider(Overlaps[i]) && (Overlaps[i] is CharacterController
                    || Overlaps[i].GetComponentInParent<OfficeNpcAgent>() != null)) initialActors.Add(Overlaps[i]);
        }
        private bool IgnoreExistingOverlap(Collider other) => initialActors.Contains(other);

        private bool TryStepOutOfInitialOverlap()
        {
            if (capsule == null) return false;
            CaptureInitialActors();
            float before = OverlapDepth(transform.position);
            if (before < 0.005f) return false;
            Vector3 from = transform.position;
            Vector3 desired = Vector3.ProjectOnPlane(navigation.steeringTarget - from, Vector3.up).normalized;
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(navigation.agentTypeID);
            var filter = new NavMeshQueryFilter { agentTypeID = navigation.agentTypeID, areaMask = navigation.areaMask };
            navigation.isStopped = true;
            navigation.velocity = Vector3.zero;
            foreach (float angle in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f })
            {
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * desired;
                Vector3 requested = from + direction * Mathf.Min(0.055f, navigation.speed * Time.deltaTime);
                if (!TerrainNavigationProbe.TryGetSupportedSegment(transform, from, requested,
                    Radius, Height, settings.agentClimb, settings.agentSlope, out Vector3 ground,
                    IgnoreExistingOverlap, null)
                    || !NavMesh.SamplePosition(from, out NavMeshHit start, 0.35f, filter)
                    || !NavMesh.SamplePosition(ground, out NavMeshHit end, 0.35f, filter)
                    || Mathf.Abs(end.position.y - ground.y) > 0.35f
                    || NavMesh.Raycast(start.position, end.position, out _, filter)
                    || OverlapDepth(ground) >= before - 0.0001f || !AllOverlapsDecrease(from, ground)) continue;
                navigation.Move(ground - from);
                break;
            }
            return true;
        }

        private float OverlapDepth(Vector3 feet)
        {
            float total = 0f;
            foreach (Collider other in initialActors)
                if (other != null && Physics.ComputePenetration(capsule, feet, transform.rotation,
                    other, other.transform.position, other.transform.rotation, out _, out float depth)) total += depth;
            return total;
        }

        private bool AllOverlapsDecrease(Vector3 from, Vector3 to)
        {
            foreach (Collider other in initialActors)
            {
                if (other == null) continue;
                Physics.ComputePenetration(capsule, from, transform.rotation,
                    other, other.transform.position, other.transform.rotation, out _, out float before);
                Physics.ComputePenetration(capsule, to, transform.rotation,
                    other, other.transform.position, other.transform.rotation, out _, out float after);
                if (after > before + 0.0001f) return false;
            }
            return true;
        }
        private bool OwnCollider(Collider c) => c == null || c.transform == transform || c.transform.IsChildOf(transform);
        private float Radius => capsule != null ? capsule.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.z)) : navigation.radius;
        private float Height => capsule != null ? Mathf.Max(Radius * 2f, capsule.height * Mathf.Abs(transform.lossyScale.y))
            : navigation.height;

        internal bool WaitForLinkTurn()
        {
            if (navigation == null || !navigation.isOnNavMesh || !navigation.hasPath) return false;
            OffMeshLinkData link = navigation.isOnOffMeshLink ? navigation.currentOffMeshLinkData : navigation.nextOffMeshLinkData;
            if (!link.valid || !navigation.isOnOffMeshLink && Vector3.Distance(transform.position, link.startPos) > 1.6f) return false;
            LinkTurn turn = null;
            for (int i = LinkTurns.Count - 1; i >= 0; i--)
            {
                LinkTurn item = LinkTurns[i];
                if (item.Holder == null || !item.Holder.isActiveAndEnabled
                    || item.Holder.navigation == null || !item.Holder.navigation.isOnNavMesh
                    || item.Until < Time.time && !item.Holder.navigation.isOnOffMeshLink)
                { LinkTurns.RemoveAt(i); continue; }
                if (SameLink(item.Start, item.End, link.startPos, link.endPos)) turn = item;
            }
            if (turn == null)
            {
                LinkTurns.Add(new LinkTurn { Start = link.startPos, End = link.endPos, Holder = this, Until = Time.time + 0.8f });
                return false;
            }
            if (turn.Holder == this) { turn.Until = Time.time + 0.8f; return false; }
            // A person already crossing has to finish. Before either enters,
            // use the same priority/lottery as ordinary head-on encounters.
            NpcPassageYield holder = turn.Holder;
            bool sameEntry = (turn.Start - link.startPos).sqrMagnitude < 0.01f;
            if (!sameEntry && !holder.navigation.isOnOffMeshLink && !navigation.isOnOffMeshLink)
            {
                int loser = Decisions.ChooseYielding(owner.GetInstanceID(), owner.TrafficPriority,
                    holder.owner.GetInstanceID(), holder.owner.TrafficPriority, Time.time, () => Random.value < 0.5f);
                if (loser == holder.owner.GetInstanceID())
                {
                    holder.BeginOrRefresh(transform, (link.endPos - link.startPos).normalized, Radius);
                    if (!holder.IsActive)
                    {
                        holder.trafficWaitUntil = Time.time + 0.2f;
                        holder.navigation.isStopped = true;
                        holder.navigation.velocity = Vector3.zero;
                    }
                    turn.Holder = this;
                    turn.Start = link.startPos;
                    turn.End = link.endPos;
                    turn.Until = Time.time + 0.8f;
                    return false;
                }
            }
            // Same-entry traffic queues behind the first person; higher priority
            // cannot strand that person across its own route to the entrance.
            if (!sameEntry && !IsActive && CanYield())
                BeginOrRefresh(holder.transform, (link.startPos - link.endPos).normalized, holder.Radius);
            if (!IsActive) { navigation.isStopped = true; navigation.velocity = Vector3.zero; }
            return true;
        }
        private static bool SameLink(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            => (a - c).sqrMagnitude < 0.01f && (b - d).sqrMagnitude < 0.01f
                || (a - d).sqrMagnitude < 0.01f && (b - c).sqrMagnitude < 0.01f;
    }
}
