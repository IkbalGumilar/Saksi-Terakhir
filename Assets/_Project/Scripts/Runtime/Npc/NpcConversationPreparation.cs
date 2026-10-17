using System;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Navigation;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using UnityEngine;
using UnityEngine.AI;

namespace SaksiTerakhir.Npc
{
    [DisallowMultipleComponent]
    public sealed class NpcConversationPreparation : MonoBehaviour
    {
        private static NavMeshPath retreatPath;
        private static NavMeshPath RetreatPath => retreatPath ??= new NavMeshPath();
        private static readonly Vector3[] PathCorners = new Vector3[64];
        private static readonly RaycastHit[] SharedHits = new RaycastHit[32];
        private static readonly Collider[] SharedColliders = new Collider[32];

        private NpcInteractable speaker;
        private OfficeNpcAgent agent;
        private PlayerController movement;
        private PlayerCameraController cameraController;
        private StoryPlayerCinematicMover mover;
        private Action startDialogue;
        private float deadline;
        private const float SpeakingDistance = 2f;
        private const float DistanceTolerance = 0.15f;
        public bool IsPreparing => speaker != null;

        public bool Begin(NpcInteractable npc, OfficeNpcAgent npcAgent, Action onReady)
        {
            if (IsPreparing || npc == null || npcAgent == null || onReady == null) return false;
            movement = GetComponent<PlayerController>();
            CharacterController body = GetComponent<CharacterController>();
            if (movement == null || body == null) { onReady(); return true; }
            StoryPlayerSeating seating = GetComponent<StoryPlayerSeating>();
            if (seating != null && seating.IsSeated) { onReady(); return true; }
            if (npcAgent.StorySeated || npcAgent.IsAmbientSeated) { onReady(); return true; }
            if (movement.StoryMovementLocked || movement.UiMovementLocked) return false;
            if (AtSpeakingDistance(npc.transform))
            { onReady(); return true; }
            mover = GetComponent<StoryPlayerCinematicMover>();
            if (mover == null)
            {
                mover = gameObject.AddComponent<StoryPlayerCinematicMover>();
                mover.Configure(movement, body);
            }
            if (mover.IsActive || !body.enabled) return false;
            speaker = npc;
            agent = npcAgent;
            startDialogue = onReady;
            agent.BeginPlayerInteraction(transform);
            movement.SetStoryMovementLocked(true);
            cameraController = GetComponent<PlayerCameraController>();
            cameraController?.SetCinematicLookLocked(true);
            cameraController?.SetStoryFocus(npc.transform);
            Vector3 away = Vector3.ProjectOnPlane(transform.position - npc.transform.position, Vector3.up).normalized;
            if (away.sqrMagnitude < 0.001f) away = -npc.transform.forward;
            var filter = new NavMeshQueryFilter { agentTypeID = mover.NavMeshAgentTypeId, areaMask = NavMesh.AllAreas };
            // Ground may rise over the two metre retreat on a stair or slope.
            // Project locally onto its physical support, then compare the bake
            // to that ground rather than to the player's original height.
            float projectionAllowance = Mathf.Min(2.4f, body.stepOffset
                + SpeakingDistance * Mathf.Tan(Mathf.Min(50f, body.slopeLimit) * Mathf.Deg2Rad));
            float[] angles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };
            foreach (float angle in angles)
            {
                Vector3 candidate = npc.transform.position + Quaternion.AngleAxis(angle, Vector3.up) * away * SpeakingDistance;
                if (!TerrainNavigationProbe.TryGetGroundPosition(transform, candidate, projectionAllowance,
                        body.slopeLimit, out Vector3 ground)
                    || !NavMesh.SamplePosition(ground, out NavMeshHit hit, 0.15f, filter)
                    || Mathf.Abs(hit.position.y - ground.y) > 0.15f
                    || !ClearAt(hit.position, body, npc.transform)
                    || !HasBoundedRetreatPath(hit.position, body, filter)) continue;
                mover.SetRetreatSpeaker(npc.transform);
                if (!mover.BeginMoveTo(hit.position, 1.6f, 0.06f, Arrived)) continue;
                deadline = Time.time + 8f;
                return true;
            }
            FailToMakeSpace();
            return false;
        }

        private bool AtSpeakingDistance(Transform npc)
        {
            float distance = Vector3.ProjectOnPlane(transform.position - npc.position, Vector3.up).magnitude;
            return Mathf.Abs(distance - SpeakingDistance) <= DistanceTolerance;
        }

        private bool HasBoundedRetreatPath(Vector3 target, CharacterController body, NavMeshQueryFilter filter)
        {
            float startAllowance = Mathf.Min(0.35f, body.stepOffset + 0.035f);
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit start, 0.6f, filter)
                || Mathf.Abs(start.position.y - transform.position.y) > startAllowance) return false;
            if (!NavMesh.CalculatePath(start.position, target, filter, RetreatPath)
                || RetreatPath.status != NavMeshPathStatus.PathComplete) return false;
            int cornerCount = RetreatPath.GetCornersNonAlloc(PathCorners);
            float length = 0f;
            Vector3 previous = start.position;
            for (int i = 0; i < cornerCount; i++)
            {
                Vector3 corner = PathCorners[i];
                length += Vector3.Distance(previous, corner);
                previous = corner;
            }
            // A failed local conversation retreat must not send MC around the
            // building, onto another storey, or keep control locked indefinitely.
            return length <= 6f;
        }

        private bool ClearAt(Vector3 feet, CharacterController body, Transform npc)
        {
            int colliderCount = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * (body.radius + 0.04f),
                feet + Vector3.up * (body.height - body.radius), body.radius + 0.035f, SharedColliders, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < colliderCount; i++)
            {
                Collider other = SharedColliders[i];
                if (other != null && !other.transform.IsChildOf(transform)) return false;
            }
            Vector3 start = feet + Vector3.up * (body.height * 0.7f);
            Vector3 end = npc.position + Vector3.up * (body.height * 0.7f);
            Vector3 delta = end - start;
            float dist = delta.magnitude;
            if (dist < 0.001f) return true;
            int hitCount = Physics.RaycastNonAlloc(start, delta / dist, SharedHits,
                dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = SharedHits[i];
                if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) && !hit.collider.transform.IsChildOf(npc)
                    && hit.collider.GetComponentInParent<OfficeNpcAgent>() == null) return false;
            }
            return true;
        }

        private void Arrived()
        {
            if (speaker == null || !speaker.isActiveAndEnabled) { Cancel(); return; }
            if (!AtSpeakingDistance(speaker.transform)) { FailToMakeSpace(); return; }
            Action ready = startDialogue;
            Release();
            ready?.Invoke();
        }

        public void Cancel(NpcInteractable requestedSpeaker = null)
        {
            if (requestedSpeaker != null && speaker != requestedSpeaker) return;
            if (speaker == null && startDialogue == null) return;
            if (mover != null) mover.StopCinematicMotion();
            Release();
        }

        private void FailToMakeSpace()
        {
            OfficeNpcAgent failedAgent = agent;
            Cancel();
            const string key = "interact.npc.make_space";
            StoryNotificationView notification = FindFirstObjectByType<StoryNotificationView>();
            if (notification != null) notification.Show(key, 3f);
            else if (failedAgent != null)
            {
                NpcLabel label = failedAgent.GetComponentInChildren<NpcLabel>(true);
                if (label != null) label.Say(LocalizationManager.Get(key), 3f);
            }
        }

        private void Release()
        {
            if (mover != null) mover.SetRetreatSpeaker(null);
            if (agent != null) agent.EndPlayerInteraction();
            if (movement != null) movement.SetStoryMovementLocked(false);
            if (cameraController != null)
            {
                cameraController.SetCinematicLookLocked(false);
                cameraController.ClearStoryFocus();
            }
            speaker = null;
            agent = null;
            startDialogue = null;
        }

        private void Update()
        {
            if (startDialogue == null || Time.timeScale <= 0f) return;
            if (speaker == null || !speaker.isActiveAndEnabled || mover == null) Cancel();
            else if (!mover.IsActive || Time.time >= deadline) FailToMakeSpace();
        }
        private void OnDisable() => Cancel();
    }
}
