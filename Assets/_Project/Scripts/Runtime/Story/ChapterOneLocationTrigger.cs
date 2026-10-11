using UnityEngine;
using SaksiTerakhir.Player;

namespace SaksiTerakhir.Story
{
    public enum ChapterOneLocation { OfficeEntrance, RooftopExit }

    [RequireComponent(typeof(BoxCollider))]
    public sealed class ChapterOneLocationTrigger : MonoBehaviour
    {
        [SerializeField] private ChapterOneDirector chapter;
        [SerializeField] private ChapterOneLocation location;

        public void Configure(ChapterOneDirector director, ChapterOneLocation kind)
        {
            chapter = director;
            location = kind;
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other) => TryVisit(other);
        private void OnTriggerStay(Collider other) => TryVisit(other);

        private void TryVisit(Collider other)
        {
            if (chapter == null || chapter.GameplaySuspended
                || other.GetComponentInParent<PlayerController>() == null) return;
            if (location == ChapterOneLocation.OfficeEntrance) chapter.TryEnterOffice();
            else chapter.TryTriggerBossCall();
        }
    }
}
