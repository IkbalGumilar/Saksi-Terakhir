using System;
using UnityEngine;

namespace SaksiTerakhir.Vehicles
{
    [DisallowMultipleComponent]
    public sealed class VehicleSignalFeedback : MonoBehaviour
    {
        [Serializable]
        private sealed class IndicatorSurface
        {
            public Renderer renderer;
            public int materialSlot;
            [NonSerialized] public MaterialPropertyBlock original;
            [NonSerialized] public MaterialPropertyBlock illuminated;
        }

        [SerializeField] private IndicatorSurface[] indicators = Array.Empty<IndicatorSurface>();
        [SerializeField] private Light[] indicatorLights = Array.Empty<Light>();
        [SerializeField, Min(1)] private int blinkCount = 2;
        [SerializeField, Min(0.05f)] private float blinkInterval = 0.25f;
        [SerializeField] private Color illuminatedColor = new Color(1f, 0.48f, 0.03f, 1f);
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip remoteClip;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private int pulsesRemaining;
        private float phaseRemaining;
        private bool lit;

        public bool IsFlashing => pulsesRemaining > 0;

        public void SignalRemote()
        {
            Flash();
            if (audioSource != null && remoteClip != null) audioSource.PlayOneShot(remoteClip);
        }

        public void Flash()
        {
            if (!isActiveAndEnabled) return;
            pulsesRemaining = Mathf.Max(1, blinkCount);
            phaseRemaining = Mathf.Max(0.05f, blinkInterval);
            SetLit(true);
        }

        private void Update()
        {
            if (!IsFlashing || Time.timeScale <= 0f) return;
            phaseRemaining -= Time.deltaTime;
            if (phaseRemaining > 0f) return;
            phaseRemaining = Mathf.Max(0.05f, blinkInterval);
            if (lit)
            {
                SetLit(false);
                pulsesRemaining--;
            }
            else SetLit(true);
        }

        private void OnDisable()
        {
            pulsesRemaining = 0;
            SetLit(false);
        }

        private void SetLit(bool enabled)
        {
            lit = enabled;
            foreach (Light light in indicatorLights)
                if (light != null) light.enabled = enabled;
            foreach (IndicatorSurface indicator in indicators)
            {
                if (indicator == null || indicator.renderer == null) continue;
                if (indicator.materialSlot < 0 || indicator.materialSlot >= indicator.renderer.sharedMaterials.Length) continue;
                if (indicator.original == null)
                {
                    indicator.original = new MaterialPropertyBlock();
                    indicator.illuminated = new MaterialPropertyBlock();
                    indicator.renderer.GetPropertyBlock(indicator.original, indicator.materialSlot);
                    indicator.renderer.GetPropertyBlock(indicator.illuminated, indicator.materialSlot);
                    indicator.illuminated.SetColor(BaseColor, illuminatedColor);
                    indicator.illuminated.SetColor(EmissionColor, illuminatedColor * 5f);
                }
                indicator.renderer.SetPropertyBlock(enabled ? indicator.illuminated : indicator.original,
                    indicator.materialSlot);
            }
        }
    }
}
