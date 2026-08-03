using System;
using SaksiTerakhir.Player;
using UnityEngine;

namespace SaksiTerakhir.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private const int HitBufferSize = 12;

        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Transform viewpoint;

        [Header("Probe")]
        [SerializeField] private float range = 2.6f;
        [SerializeField] private float probeRadius = 0.12f;
        [SerializeField] private LayerMask probeLayers = ~0;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[HitBufferSize];

        private Interactable current;

        public event Action<Interactable> TargetChanged;

        public Interactable Current => current;

        private void Awake()
        {
            if (input == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerInteractor)} on '{gameObject.name}' has no input reader assigned.",
                    this);
                enabled = false;
                return;
            }

            if (viewpoint == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerInteractor)} on '{gameObject.name}' has no viewpoint assigned.",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            input.InteractPressed += OnInteractPressed;
        }

        private void OnDisable()
        {
            input.InteractPressed -= OnInteractPressed;
            SetCurrent(null);
        }

        private void Update()
        {
            SetCurrent(Probe());
        }

        private Interactable Probe()
        {
            var ray = new Ray(viewpoint.position, viewpoint.forward);
            int count = Physics.SphereCastNonAlloc(ray, probeRadius, hitBuffer, range,
                probeLayers, QueryTriggerInteraction.Collide);

            Collider nearest = null;
            float nearestDistance = float.MaxValue;
            for (var index = 0; index < count; index++)
            {
                RaycastHit hit = hitBuffer[index];
                if (hit.distance >= nearestDistance || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                nearest = hit.collider;
                nearestDistance = hit.distance;
            }

            if (nearest == null)
            {
                return null;
            }

            Interactable candidate = nearest.GetComponentInParent<Interactable>();
            return candidate != null && candidate.CanInteract(transform) ? candidate : null;
        }

        private void SetCurrent(Interactable candidate)
        {
            if (candidate == current)
            {
                return;
            }

            current = candidate;
            TargetChanged?.Invoke(current);
        }

        private void OnInteractPressed()
        {
            if (current == null || !current.CanInteract(transform))
            {
                return;
            }

            current.Interact(transform);
            TargetChanged?.Invoke(current);
        }
    }
}
