using UnityEngine;

namespace SaksiTerakhir.Npc
{
    /// <summary>Optional work poses and carried equipment; never owns a story order or teleports its worker.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(OfficeNpcAgent))]
    public sealed class NpcWorkRoutine : MonoBehaviour
    {
        [SerializeField] private NpcDuty duty = NpcDuty.Wander;
        [SerializeField] private Transform seatAnchor;
        [SerializeField] private GameObject equipmentPrefab;

        private OfficeNpcAgent actor;
        private Transform visual;
        private GameObject equipmentInstance;
        private GameObject instantiatedEquipmentPrefab;
        private Transform equipmentGrip;
        private Vector3 baseVisualPosition, appliedVisualPosition;
        private Quaternion baseVisualRotation, appliedVisualRotation;
        private bool ownsVisualMotion;
        private float phase;

        public NpcDuty Duty => duty;
        public GameObject EquipmentInstance => equipmentInstance;

        public void Configure(NpcDuty assignedDuty, Transform assignedSeat, GameObject assignedEquipment)
        {
            RestoreVisualMotion();
            FindActor();
            if (actor != null && actor.IsAmbientSeated) actor.SetAmbientSeated(false);
            duty = assignedDuty;
            seatAnchor = assignedSeat;
            equipmentPrefab = assignedEquipment;
            phase = Random.Range(0f, Mathf.PI * 2f);
            RefreshEquipment();
        }

        private void Awake()
        {
            FindActor();
            phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Start() => RefreshEquipment();

        private void FindActor()
        {
            if (actor == null) actor = GetComponent<OfficeNpcAgent>();
            if (visual == null) visual = transform.Find("Capsule Visual");
        }

        private void LateUpdate()
        {
            RestoreVisualMotion();
            if (Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
            FindActor();
            if (actor == null || !actor.isActiveAndEnabled) return;

            bool storyBusy = actor.HasStoryOrder;
            bool dutyAvailable = !storyBusy && !actor.IsDialogueBusy;
            bool atWork = actor.CurrentState == NpcState.Activity && (actor.IsAmbientSeated || IsAtHome());
            bool seatedDuty = duty == NpcDuty.SeatedWork || duty == NpcDuty.Typing;
            bool shouldSit = dutyAvailable && atWork && seatedDuty && seatAnchor != null;
            if (actor.IsAmbientSeated != shouldSit)
            {
                actor.SetAmbientSeated(shouldSit);
                if (shouldSit)
                {
                    actor.SetAmbientSeatPosition(seatAnchor.position - Vector3.up * 0.46f);
                    transform.rotation = Quaternion.Euler(0f, seatAnchor.eulerAngles.y, 0f);
                }
            }

            // Carried tools remain visible during ordinary conversation, but story scenes
            // decide their own props. No equipment objects are created in the frame loop.
            if (equipmentInstance != null)
            {
                bool equipmentVisible = !storyBusy && UsesEquipment();
                if (equipmentInstance.activeSelf != equipmentVisible)
                    equipmentInstance.SetActive(equipmentVisible);
            }
            if (!dutyAvailable || !atWork || visual == null) return;

            baseVisualPosition = visual.localPosition;
            baseVisualRotation = visual.localRotation;
            float breath = Mathf.Sin(Time.time * 1.65f + phase) * 0.0025f;
            float typing = duty == NpcDuty.Typing && actor.IsAmbientSeated
                ? Mathf.Sin(Time.time * 5.7f + phase) * 0.65f : 0f;
            appliedVisualPosition = baseVisualPosition + Vector3.up * breath;
            appliedVisualRotation = baseVisualRotation * Quaternion.Euler(typing, 0f, 0f);
            visual.localPosition = appliedVisualPosition;
            visual.localRotation = appliedVisualRotation;
            ownsVisualMotion = true;
        }

        private bool IsAtHome()
        {
            if (actor.Home == null) return false;
            Vector3 separation = actor.transform.position - actor.Home.transform.position;
            return Mathf.Abs(separation.y) <= 0.45f
                && Vector3.ProjectOnPlane(separation, Vector3.up).sqrMagnitude <= 0.65f * 0.65f;
        }

        private bool UsesEquipment() => duty == NpcDuty.Cleaning || duty == NpcDuty.CarryDocuments;

        private void RefreshEquipment()
        {
            if (equipmentInstance != null && instantiatedEquipmentPrefab != equipmentPrefab)
            {
                // A change of assigned prop is rare; returning the same worker to a pool
                // simply hides this child and keeps it for its next activation.
                equipmentInstance.SetActive(false);
                if (Application.isPlaying) Destroy(equipmentInstance);
                else DestroyImmediate(equipmentInstance);
                equipmentInstance = null;
            }
            if (equipmentPrefab == null || !UsesEquipment())
            {
                if (equipmentInstance != null) equipmentInstance.SetActive(false);
                return;
            }
            if (equipmentGrip == null)
            {
                equipmentGrip = new GameObject("Equipment Grip").transform;
                equipmentGrip.SetParent(transform, false);
            }
            Transform hand = FindRightHand();
            equipmentGrip.SetParent(hand != null ? hand : transform, false);
            equipmentGrip.localPosition = hand != null ? Vector3.zero
                : duty == NpcDuty.Cleaning ? new Vector3(0.36f, 0.7f, 0.18f)
                : new Vector3(0.36f, 1.05f, 0.18f);
            equipmentGrip.localRotation = Quaternion.identity;
            if (equipmentInstance == null)
            {
                equipmentGrip.gameObject.SetActive(false);
                equipmentInstance = Instantiate(equipmentPrefab, equipmentGrip, false);
                equipmentInstance.name = "Work Equipment";
                equipmentInstance.transform.localPosition = Vector3.zero;
                equipmentInstance.transform.localRotation = Quaternion.identity;
                foreach (Collider collider in equipmentInstance.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                foreach (Rigidbody body in equipmentInstance.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    body.detectCollisions = false;
                    body.useGravity = false;
                    body.isKinematic = true;
                }
                foreach (Behaviour behaviour in equipmentInstance.GetComponentsInChildren<Behaviour>(true))
                    behaviour.enabled = false;
                instantiatedEquipmentPrefab = equipmentPrefab;
                equipmentGrip.gameObject.SetActive(true);
            }
            equipmentInstance.SetActive(actor != null && !actor.HasStoryOrder);
        }

        private Transform FindRightHand()
        {
            foreach (Animator animator in GetComponentsInChildren<Animator>(true))
                if (animator.avatar != null && animator.isHuman
                    && (equipmentGrip == null || !animator.transform.IsChildOf(equipmentGrip)))
                {
                    Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    if (hand != null) return hand;
                }
            return null;
        }

        private void RestoreVisualMotion()
        {
            if (!ownsVisualMotion || visual == null) { ownsVisualMotion = false; return; }
            // A story/animation may have authored a new pose since our last frame.
            // Restore only the exact components still owned by this routine.
            if ((visual.localPosition - appliedVisualPosition).sqrMagnitude < 0.00000001f)
                visual.localPosition = baseVisualPosition;
            if (Quaternion.Angle(visual.localRotation, appliedVisualRotation) < 0.01f)
                visual.localRotation = baseVisualRotation;
            ownsVisualMotion = false;
        }

        private void OnDisable()
        {
            RestoreVisualMotion();
            if (actor != null && actor.IsAmbientSeated) actor.SetAmbientSeated(false);
            if (equipmentInstance != null) equipmentInstance.SetActive(false);
        }
    }
}
