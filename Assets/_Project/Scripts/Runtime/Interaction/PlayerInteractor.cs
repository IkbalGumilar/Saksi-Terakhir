using System;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using UnityEngine;
using UnityEngine.Serialization;

namespace SaksiTerakhir.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private const int InteractableLayerIndex = 6;
        private const int HitBufferSize = 12;
        private const int OverlapBufferSize = 32;
        private const float DefaultMaximumAimDistance = 30f;

        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorPropertyId = Shader.PropertyToID("_Color");

        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Transform viewpoint;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private StoryDialogueController storyDialogue;

        [Header("Crosshair Probe")]
        [Tooltip("Legacy ray limit. The effective distance is also capped by Maximum Aim Distance.")]
        [SerializeField] private float aimDistance = 999f;
        [Tooltip("Caps aim detection at a practical distance, in meters.")]
        [SerializeField, Min(1f)] private float maximumAimDistance = DefaultMaximumAimDistance;
        [FormerlySerializedAs("range")]
        [SerializeField] private float interactionRange = 2f;
        [Tooltip("Probe diameter as a fraction of camera viewport height. 0.0125 is 1.25%.")]
        [SerializeField, Range(0.002f, 0.05f)] private float probeViewportHeightFraction = 0.0125f;
        [Tooltip("Visible sphere placed at the aim ray's hit point.")]
        [SerializeField] private Transform debugTransform;
        [SerializeField] private LayerMask aimColliderLayers = ~0;
        [SerializeField] private LayerMask probeLayers = 1 << InteractableLayerIndex;

        [Header("Aim Sphere")]
        [SerializeField] private Color probeInRangeColor = Color.yellow;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[HitBufferSize];
        private readonly Collider[] overlapBuffer = new Collider[OverlapBufferSize];

        private Interactable current;
        private Renderer[] probeRenderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock[] originalProbeBlocks = Array.Empty<MaterialPropertyBlock>();
        private Transform probeRendererRoot;
        private bool lastProbeInRange;
        private bool hasLastProbeState;

        public event Action<Interactable> TargetChanged;

        public Interactable Current => current;
        public bool IsInteract => current != null;
        public Vector3 ProbePosition { get; private set; }
        public float ProbeRadius { get; private set; }

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
                return;
            }

            if (aimCamera == null)
            {
                aimCamera = viewpoint.GetComponent<Camera>();
                if (aimCamera == null)
                {
                    aimCamera = Camera.main;
                }
            }

            if (debugTransform != null)
            {
                CacheProbeRenderers();

                Collider[] colliders = debugTransform.GetComponentsInChildren<Collider>(true);
                foreach (Collider collider in colliders)
                {
                    collider.enabled = false;
                }
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
            SetProbe(Vector3.zero, 0f);
            SetProbeVisualColor(false);
        }

        private void Update()
        {
            SetCurrent(Probe());
            SetProbeVisualColor(current != null);
        }

        private Interactable Probe()
        {
            Ray ray = CreateCenterScreenRay();
            float effectiveAimDistance = Mathf.Min(aimDistance, MaximumAimDistance);
            int count = Physics.RaycastNonAlloc(ray, hitBuffer, effectiveAimDistance,
                aimColliderLayers, QueryTriggerInteraction.Collide);

            bool hasHit = false;
            RaycastHit nearestHit = default;
            float nearestDistance = float.MaxValue;
            for (var index = 0; index < count; index++)
            {
                RaycastHit hit = hitBuffer[index];
                if (hit.collider == null
                    || hit.distance >= nearestDistance
                    || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                nearestHit = hit;
                nearestDistance = hit.distance;
                hasHit = true;
            }

            if (!hasHit)
            {
                SetProbe(Vector3.zero, 0f);
                return null;
            }

            float radius = CalculateProbeRadiusAtDistance(nearestHit.distance);
            SetProbe(nearestHit.point, radius);

            if (radius <= 0f || nearestHit.distance - radius > interactionRange)
            {
                return null;
            }

            int overlapCount = Physics.OverlapSphereNonAlloc(nearestHit.point, radius,
                overlapBuffer, probeLayers, QueryTriggerInteraction.Collide);

            Interactable nearestInteractable = null;
            float nearestInteractableDistance = float.MaxValue;
            float interactionRangeSquared = interactionRange * interactionRange;
            for (var index = 0; index < overlapCount; index++)
            {
                Collider collider = overlapBuffer[index];
                if (collider == null || collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                Interactable candidate = collider.GetComponentInParent<Interactable>();
                if (candidate == null || !candidate.CanInteract(transform))
                {
                    continue;
                }

                Vector3 closestPointToPlayer = collider.ClosestPoint(ray.origin);
                if ((closestPointToPlayer - ray.origin).sqrMagnitude > interactionRangeSquared)
                {
                    continue;
                }

                Vector3 closestPoint = collider.ClosestPoint(nearestHit.point);
                float distanceToProbe = (closestPoint - nearestHit.point).sqrMagnitude;
                if (distanceToProbe >= nearestInteractableDistance)
                {
                    continue;
                }

                nearestInteractable = candidate;
                nearestInteractableDistance = distanceToProbe;
            }

            return nearestInteractable;
        }

        public static float CalculateProbeRadius(float distance, float verticalFieldOfView,
            float viewportHeightFraction, float maximumDistance)
        {
            if (distance <= 0f || verticalFieldOfView <= 0f || verticalFieldOfView >= 180f
                || viewportHeightFraction <= 0f || maximumDistance <= 0f)
            {
                return 0f;
            }

            float boundedDistance = Mathf.Min(distance, maximumDistance);
            return boundedDistance
                * Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad)
                * viewportHeightFraction;
        }

        private Ray CreateCenterScreenRay()
        {
            if (aimCamera == null)
            {
                return new Ray(viewpoint.position, viewpoint.forward);
            }

            Rect pixelRect = aimCamera.pixelRect;
            var screenCenter = new Vector3(
                pixelRect.xMin + pixelRect.width * 0.5f,
                pixelRect.yMin + pixelRect.height * 0.5f,
                0f);
            return aimCamera.ScreenPointToRay(screenCenter);
        }

        private void SetProbe(Vector3 position, float radius)
        {
            ProbePosition = position;
            ProbeRadius = radius;

            if (debugTransform == null)
            {
                return;
            }

            debugTransform.position = position;
            float diameter = radius * 2f;
            Transform parent = debugTransform.parent;
            Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
            debugTransform.localScale = new Vector3(
                SafeDivide(diameter, parentScale.x),
                SafeDivide(diameter, parentScale.y),
                SafeDivide(diameter, parentScale.z));
        }

        private float GetCameraFieldOfView()
        {
            return aimCamera != null ? aimCamera.fieldOfView : 60f;
        }

        private float MaximumAimDistance => Mathf.Max(1f, maximumAimDistance);

        private float CalculateProbeRadiusAtDistance(float distance)
        {
            if (aimCamera != null && aimCamera.orthographic)
            {
                return distance > 0f
                    ? aimCamera.orthographicSize * probeViewportHeightFraction
                    : 0f;
            }

            return CalculateProbeRadius(distance, GetCameraFieldOfView(),
                probeViewportHeightFraction, MaximumAimDistance);
        }

        private void CacheProbeRenderers()
        {
            if (debugTransform == null || debugTransform == probeRendererRoot)
            {
                return;
            }

            probeRendererRoot = debugTransform;
            probeRenderers = debugTransform.GetComponentsInChildren<Renderer>(true);
            originalProbeBlocks = new MaterialPropertyBlock[probeRenderers.Length];
            hasLastProbeState = false;
            for (int index = 0; index < probeRenderers.Length; index++)
            {
                Renderer renderer = probeRenderers[index];
                if (renderer != null)
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    originalProbeBlocks[index] = block;
                    renderer.enabled = true;
                }
            }
        }

        private void SetProbeVisualColor(bool withinInteractionRange)
        {
            if (debugTransform == null)
            {
                return;
            }

            CacheProbeRenderers();
            if (hasLastProbeState && lastProbeInRange == withinInteractionRange)
            {
                return;
            }

            for (int index = 0; index < probeRenderers.Length; index++)
            {
                Renderer renderer = probeRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (!withinInteractionRange)
                {
                    renderer.SetPropertyBlock(originalProbeBlocks[index]);
                    continue;
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                SetMaterialColor(block, probeInRangeColor);
                renderer.SetPropertyBlock(block);
            }

            lastProbeInRange = withinInteractionRange;
            hasLastProbeState = true;
        }

        private static void SetMaterialColor(MaterialPropertyBlock block, Color color)
        {
            block.SetColor(BaseColorPropertyId, color);
            block.SetColor(LegacyColorPropertyId, color);
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) > Mathf.Epsilon ? value / divisor : value;
        }

        private void SetCurrent(Interactable candidate)
        {
            if (candidate == current)
            {
                return;
            }

            if (current is NpcInteractable previousNpc) previousNpc.CloseDialogue();
            current = candidate;
            TargetChanged?.Invoke(current);
        }

        private void OnInteractPressed()
        {
            if (storyDialogue != null && storyDialogue.TryConsumeInteract())
            {
                return;
            }

            if (current == null || !current.CanInteract(transform))
            {
                return;
            }

            current.Interact(transform);
            TargetChanged?.Invoke(current);
        }
    }
}
