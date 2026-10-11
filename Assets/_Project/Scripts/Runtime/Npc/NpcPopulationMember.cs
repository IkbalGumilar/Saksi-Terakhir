using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.Npc
{
    /// <summary>Lifetime protection is independent of renderer visibility and seated work.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(OfficeNpcAgent))]
    public sealed class NpcPopulationMember : MonoBehaviour
    {
        private readonly HashSet<string> missionPins = new HashSet<string>(StringComparer.Ordinal);
        private OfficeNpcAgent actor;
        private Renderer[] renderers = Array.Empty<Renderer>();
        public OfficeNpcAgent Actor => actor != null ? actor : actor = GetComponent<OfficeNpcAgent>();
        public NpcSpawnPoint SpawnPoint { get; private set; }
        public bool IsManaged { get; private set; }
        public bool IsMissionPinned => missionPins.Count > 0;
        public bool LifetimeProtected => IsMissionPinned || Actor.HasStoryOrder || Actor.IsDialogueBusy;
        public bool RenderSuppressed { get; private set; }
        public float ActivatedAt { get; private set; }

        public void AcquireMissionPin(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A mission pin needs a stable reason.", nameof(reason));
            missionPins.Add(reason);
        }

        public void ReleaseMissionPin(string reason) { if (reason != null) missionPins.Remove(reason); }
        public string[] CaptureMissionPins()
        {
            var result = new string[missionPins.Count]; missionPins.CopyTo(result); Array.Sort(result, StringComparer.Ordinal); return result;
        }

        internal void Assign(NpcSpawnPoint point)
        {
            SpawnPoint = point; IsManaged = true; missionPins.Clear(); ActivatedAt = Time.time;
            RefreshRenderers(); SetRenderSuppressed(false);
        }

        public void RefreshRenderers()
        {
            foreach (Renderer renderer in renderers) if (renderer != null) renderer.forceRenderingOff = false;
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        internal void SetRenderSuppressed(bool value)
        {
            RenderSuppressed = value;
            // Preserve authored enabled flags, TMP visibility, animations, and props.
            foreach (Renderer renderer in renderers) if (renderer != null) renderer.forceRenderingOff = value;
        }

        internal bool InFrustum(Plane[] planes)
        {
            foreach (Renderer renderer in renderers)
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy
                    && GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) return true;
            return false;
        }

        internal void ClearAssignment()
        {
            SpawnPoint?.Release(this); SpawnPoint = null; missionPins.Clear(); SetRenderSuppressed(false);
        }

        private void Awake() => RefreshRenderers();
        private void OnDestroy() => SpawnPoint?.Release(this);
    }
}
