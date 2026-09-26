using UnityEngine;

namespace SaksiTerakhir.Story
{
    [DisallowMultipleComponent]
    public sealed class VehicleArrivalZone : MonoBehaviour
    {
        [SerializeField] private ChapterOneDirector chapter;
        [SerializeField] private Transform vehicle;
        [SerializeField] private Transform player;
        [SerializeField, Min(0.5f)] private float radius = 2f;

        public void Configure(ChapterOneDirector director, Transform vehicleTransform,
            Transform playerTransform, float interactionRadius = 2f)
        {
            chapter = director;
            vehicle = vehicleTransform;
            player = playerTransform;
            radius = Mathf.Max(0.5f, interactionRadius);
        }

        private void Update()
        {
            if (chapter == null || vehicle == null || player == null
                || chapter.Progress == null || chapter.Progress.Stage != ChapterOneStage.ReachVehicle)
                return;
            Vector3 difference = player.position - vehicle.position;
            difference.y = 0f;
            if (difference.sqrMagnitude <= radius * radius)
                chapter.TryReachVehicle(player);
        }
    }
}
