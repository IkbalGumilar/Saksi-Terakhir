using System;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Npc;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    [DisallowMultipleComponent, RequireComponent(typeof(DoorInteractable))]
    public sealed class BossOfficeGate : MonoBehaviour
    {
        [SerializeField] private Collider roomZone;
        [SerializeField] private Transform corridorExit;
        [SerializeField] private Transform player;
        [SerializeField] private OfficeNpcDirector npcDirector;
        [SerializeField] private OfficeNpcAgent boss;
        [SerializeField] private OfficeNpcAgent colleagueA;
        [SerializeField] private OfficeNpcAgent colleagueB;

        private ChapterOneProgress progress;
        private DoorInteractable door;
        private bool playerInside;
        private Transform lastBlockedActor;

        public event Action<Transform> Blocked;

        public bool PlayerInside => playerInside;
        public bool PlayerBlocked => progress != null && !CanOpenFor(player);
        public string BlockedPromptKey
        {
            get
            {
                if (progress == null) return "interact.story.wait_boss";
                if (progress.Stage == ChapterOneStage.FindColleagues
                    || progress.Stage == ChapterOneStage.EscortLastColleague)
                {
                    if (!progress.RakaMet && !progress.SintaMet) return "interact.story.find_both";
                    if (!progress.RakaMet) return "interact.story.find_raka";
                    if (!progress.SintaMet) return "interact.story.find_sinta";
                    return "interact.story.wait_colleagues";
                }
                return "interact.story.wait_boss";
            }
        }

        public void Configure(ChapterOneProgress chapterProgress, DoorInteractable officeDoor)
        {
            progress = chapterProgress;
            door = officeDoor;
        }

        public void ConfigureScene(Collider zone, Transform exit, Transform playerTransform,
            OfficeNpcDirector director, OfficeNpcAgent bossActor,
            OfficeNpcAgent raka, OfficeNpcAgent sinta)
        {
            roomZone = zone;
            corridorExit = exit;
            player = playerTransform;
            npcDirector = director;
            boss = bossActor;
            colleagueA = raka;
            colleagueB = sinta;
        }

        public bool CanOpenFor(Transform actor)
        {
            if (actor == null || progress == null) return false;
            OfficeNpcAgent npc = actor.GetComponentInParent<OfficeNpcAgent>();
            if (npc != null)
            {
                if (npc == boss) return true;
                if (roomZone != null && roomZone.bounds.Contains(actor.position)) return true;
                string id = npc.Profile != null ? npc.Profile.Id : string.Empty;
                return id == "NPC-003" ? progress.RakaMet
                    : id == "NPC-004" && progress.SintaMet;
            }
            if (playerInside) return true;
            return progress.Stage == ChapterOneStage.MeetBoss
                || progress.Stage >= ChapterOneStage.FinalBossBriefing;
        }

        public void SetPlayerInside(bool inside)
        {
            bool wasInside = playerInside;
            playerInside = inside;
            if (wasInside && !inside && progress != null
                && (progress.Stage == ChapterOneStage.FindColleagues
                    || progress.Stage == ChapterOneStage.EscortLastColleague))
                door?.ForceClose();
        }

        public void OnBlocked(Transform actor)
        {
            if (actor == null || actor == lastBlockedActor) return;
            lastBlockedActor = actor;
            Blocked?.Invoke(actor);
        }

        public void ClearBackgroundActors()
        {
            if (npcDirector == null || roomZone == null || corridorExit == null)
            {
                Debug.LogError("Boss office gate needs its room zone, corridor exit and NPC director.", this);
                return;
            }
            npcDirector.RestrictExecutiveRoom(roomZone, corridorExit,
                boss, colleagueA, colleagueB);
        }

        private void Update()
        {
            if (player != null && roomZone != null)
                SetPlayerInside(roomZone.bounds.Contains(player.position));
            if (lastBlockedActor != null
                && Vector3.Distance(lastBlockedActor.position, transform.position) > 2.5f)
                lastBlockedActor = null;
        }
    }
}
