using SaksiTerakhir.Player;
using UnityEngine;

namespace SaksiTerakhir.Npc
{
    /// <summary>Proximity greeting owned by a stable workplace ID, rather than a pooled object instance.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(NpcInteractable))]
    public sealed class NpcGreetingTrigger : MonoBehaviour
    {
        [SerializeField] private string greetingId;
        [SerializeField, Min(2f)] private float radius = 4f;
        private static readonly RaycastHit[] SightHits = new RaycastHit[16];
        private Transform player;
        private NpcInteractable npc;
        private float nextCheck;

        public string GreetingId => greetingId;

        public void Configure(string stableGreetingId, Transform playerTransform, float greetingRadius = 4f)
        {
            greetingId = stableGreetingId;
            player = playerTransform;
            radius = Mathf.Max(2f, greetingRadius);
            npc = GetComponent<NpcInteractable>();
            nextCheck = Time.time + 0.5f;
        }

        private void Awake() => npc = GetComponent<NpcInteractable>();

        private void Update()
        {
            if (Time.timeScale <= 0f || Time.time < nextCheck) return;
            nextCheck = Time.time + 0.5f;
            if (NpcGreetingMemory.Contains(greetingId) || string.IsNullOrWhiteSpace(greetingId)) return;
            if (player == null)
            {
                PlayerController controller = FindFirstObjectByType<PlayerController>();
                if (controller != null) player = controller.transform;
            }
            if (player == null || npc == null) return;
            Vector3 delta = player.position - transform.position;
            if (Mathf.Abs(delta.y) > 1.5f || delta.sqrMagnitude > radius * radius || !HasClearSight()) return;
            npc.TryOneWayGreeting(player, greetingId);
        }

        private bool HasClearSight()
        {
            Vector3 start = transform.position + Vector3.up * 1.35f;
            Vector3 delta = player.position + Vector3.up * 1.35f - start;
            if (delta.sqrMagnitude < 0.01f) return true;
            int count = Physics.RaycastNonAlloc(start, delta.normalized, SightHits, delta.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == SightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Transform hit = SightHits[i].transform;
                if (hit != null && !hit.IsChildOf(transform) && !hit.IsChildOf(player)) return false;
            }
            return true;
        }
    }
}
