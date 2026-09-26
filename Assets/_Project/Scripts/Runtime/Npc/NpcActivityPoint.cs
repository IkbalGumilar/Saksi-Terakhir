using System;
using UnityEngine;

namespace SaksiTerakhir.Npc
{
    public enum NpcRole { Security, Receptionist, ColleagueA, ColleagueB, Supervisor, Boss, ArchiveGuard, Worker }
    [Flags] public enum NpcZone { Interior = 1, Yard = 2, Roof = 4 }
    public enum NpcActivityKind { Idle, Work, InspectArchive, Patrol, Socialize }
    public enum NpcState { Waiting, Moving, Activity, Conversation, OffNavMesh, PlayerConversation, StoryHeld, StoryMoving }

    [DisallowMultipleComponent]
    public sealed class NpcActivityPoint : MonoBehaviour
    {
        [SerializeField] private string pointLabel = "Tempat kerja";
        [SerializeField] private NpcActivityKind activity = NpcActivityKind.Work;
        [SerializeField] private NpcZone zone = NpcZone.Interior;
        [SerializeField] private int floor;
        [SerializeField] private Vector2 dwellSeconds = new Vector2(8f, 20f);
        private OfficeNpcAgent occupant;

        public string Label => pointLabel;
        public NpcActivityKind Activity => activity;
        public NpcZone Zone => zone;
        public int Floor => floor;
        public Vector2 DwellSeconds => dwellSeconds;
        public OfficeNpcAgent Occupant => occupant;

        public void Configure(string label, NpcActivityKind kind, NpcZone pointZone, int pointFloor, Vector2 dwell)
        {
            pointLabel = label;
            activity = kind;
            zone = pointZone;
            floor = pointFloor;
            dwellSeconds = new Vector2(Mathf.Max(1f, dwell.x), Mathf.Max(Mathf.Max(1f, dwell.x), dwell.y));
        }

        public bool TryReserve(OfficeNpcAgent actor)
        {
            if (actor == null || !isActiveAndEnabled) return false;
            if (occupant != null && occupant != actor && occupant.isActiveAndEnabled) return false;
            occupant = actor;
            return true;
        }

        public void Release(OfficeNpcAgent actor)
        {
            if (occupant == actor) occupant = null;
        }

        public float ChooseDwellTime() => UnityEngine.Random.Range(dwellSeconds.x, dwellSeconds.y);
        private void OnDisable() => occupant = null;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = activity == NpcActivityKind.InspectArchive ? Color.yellow : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.6f);
        }
    }
}
