using UnityEngine;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Story;
using SaksiTerakhir.Inventory;

namespace SaksiTerakhir.Interaction
{
    public enum DoorMotion { Swing, Slide, RollUp }

    [DisallowMultipleComponent]
    public sealed class DoorInteractable : Interactable, IInventoryItemUseTarget
    {
        private const float AngleTolerance = 0.01f;

        [Header("Interaction")]
        [SerializeField] private bool interact = true;

        [Header("Door Motion")]
        [Tooltip("Use a negative value to make this door swing in the opposite direction.")]
        [SerializeField, Range(-180f, 180f)] private float openAngle = 92f;
        [SerializeField, Min(1f)] private float swingSpeed = 180f;
        [SerializeField] private DoorMotion motion;
        [SerializeField] private Transform movingPart;
        [SerializeField] private Vector3 slideOffset;
        [SerializeField] private Vector3 openScale = Vector3.one;
        [SerializeField] private Collider[] solidColliders = System.Array.Empty<Collider>();
        [SerializeField] private BoxCollider interactionTrigger;
        [SerializeField] private string unavailablePromptKey;
        [SerializeField] private bool openingAvailable = true;
        [SerializeField] private BossOfficeGate officeAccessGate;

        [Header("Prompt Keys")]
        [SerializeField] private string openPromptKey = "interact.door.open";
        [SerializeField] private string closePromptKey = "interact.door.close";

        private Collider doorCollider;
        private Quaternion closedRotation;
        private Vector3 closedPosition;
        private Vector3 closedScale;
        private bool initialized;
        private Vector3 hingeAxis;
        private float currentAngle;
        private float targetAngle;
        private BossOfficeGate bossGate;
        private float autoCloseRemaining = -1f;
        private float autoCloseDelay;
        private Bounds closedDoorwayBounds;
        private Vector3 closedDoorPosition;
        private readonly Collider[] passageActors = new Collider[64];
        private DoorInteractable pairedLeaf;
        private bool pairedLeafResolved;

        public bool IsOpen => Mathf.Abs(targetAngle) > AngleTolerance;
        private ItemKeyLock KeyLock => GetComponent<ItemKeyLock>();
        public bool IsClosing => !IsOpen && Mathf.Abs(currentAngle) > AngleTolerance;
        public float OpeningTimeRemaining => IsOpen
            ? Mathf.Abs(targetAngle - currentAngle) / Mathf.Max(1f, swingSpeed) : 0f;
        public Bounds ClosedDoorwayBounds => initialized
            ? closedDoorwayBounds : CalculateDoorwayBounds();

        private void Awake()
        {
            if (movingPart == null) movingPart = transform;
            doorCollider = GetComponent<Collider>();
            closedDoorPosition = transform.position;
            closedDoorwayBounds = CalculateDoorwayBounds();
            closedRotation = movingPart.localRotation;
            closedPosition = movingPart.localPosition;
            closedScale = movingPart.localScale;
            hingeAxis = movingPart.parent != null
                ? movingPart.parent.InverseTransformDirection(Vector3.up).normalized
                : Vector3.up;
            initialized = true;
        }

        public void ConfigureMotion(Transform part, DoorMotion mode, Vector3 offset,
            Vector3 scale, Collider[] solids, BoxCollider promptCollider)
        {
            movingPart = part;
            motion = mode;
            slideOffset = offset;
            openScale = scale;
            solidColliders = solids ?? System.Array.Empty<Collider>();
            interactionTrigger = promptCollider;
        }

        public void SetOpeningAvailable(bool available, string prompt = null)
        {
            openingAvailable = available;
            unavailablePromptKey = prompt;
        }

        public void BindAccessGate(BossOfficeGate gate) => officeAccessGate = gate;

        private Bounds CalculateDoorwayBounds()
        {
            if (interactionTrigger != null) return interactionTrigger.bounds;
            Collider collider = GetComponent<Collider>();
            return collider != null ? collider.bounds : new Bounds(transform.position, Vector3.one);
        }

        public override bool CanInteract(Transform actor)
        {
            return interact && isActiveAndEnabled;
        }

        public override void Interact(Transform actor)
        {
            if (!CanInteract(actor))
            {
                return;
            }

            if (!openingAvailable) return;

            BossOfficeGate gate = GetBossGate();
            if (gate != null && !gate.CanOpenFor(actor))
            {
                gate.OnBlocked(actor);
                return;
            }

            ItemKeyLock keyLock = KeyLock;
            if (keyLock != null && keyLock.IsLocked)
            {
                keyLock.RequestUnlock(this, actor);
                return;
            }

            bool opening = !IsOpen;
            if (opening) ForceOpen(actor);
            else ForceClose();
        }

        public void ForceOpen() => ForceOpen(null);

        public bool AcceptsItem(string itemId) => openingAvailable && isActiveAndEnabled
            && KeyLock != null && KeyLock.AcceptsItem(itemId);

        public bool TryUseItem(InventoryEntry entry, Transform actor)
        {
            if (!CanInteract(actor) || actor == null || entry == null || !AcceptsItem(entry.Id)) return false;
            InventoryPanelController inventory = actor.GetComponentInParent<InventoryPanelController>();
            if (inventory == null || inventory.State == null || inventory.State.GetSlotIndex(entry) < 0) return false;
            if ((ClosedDoorwayBounds.ClosestPoint(actor.position) - actor.position).sqrMagnitude > 4f) return false;
            BossOfficeGate gate = GetBossGate();
            if (gate != null && !gate.CanOpenFor(actor)) return false;
            if (!KeyLock.TryUnlock(entry)) return false;
            ForceOpen(actor);
            return IsOpen;
        }

        public void ForceOpen(Transform actor) => ForceOpenInternal(actor, true);

        private void ForceOpenInternal(Transform actor, bool syncPairedLeaf)
        {
            if (!openingAvailable) return;
            ItemKeyLock keyLock = KeyLock;
            if (keyLock != null && keyLock.IsLocked) return;
            targetAngle = openAngle;
            StoryPlayerCinematicMover mover = actor != null
                ? actor.GetComponentInParent<StoryPlayerCinematicMover>(true) : null;
            bool automatic = actor != null && (actor.GetComponentInParent<OfficeNpcAgent>(true) != null
                || (mover != null && mover.IsActive));
            autoCloseRemaining = autoCloseDelay = automatic ? 1f : Random.Range(4f, 7f);
            SetDoorColliderEnabled(false);
            if (syncPairedLeaf) GetPairedLeaf()?.ForceOpenInternal(actor, false);
        }

        public void ForceClose() => ForceCloseInternal(true);

        private void ForceCloseInternal(bool syncPairedLeaf)
        {
            if (PassageOccupied())
            {
                RequestAutoCloseInternal(1f, syncPairedLeaf);
                return;
            }
            targetAngle = 0f;
            autoCloseRemaining = -1f;
            if (Mathf.Abs(currentAngle) < AngleTolerance) SetDoorColliderEnabled(true);
            if (syncPairedLeaf) GetPairedLeaf()?.ForceCloseInternal(false);
        }

        // Story gates schedule closure through the same traffic checks as every other door.
        public void RequestAutoClose(float delay = 1f) => RequestAutoCloseInternal(delay, true);

        private void RequestAutoCloseInternal(float delay, bool syncPairedLeaf)
        {
            if (IsOpen && autoCloseRemaining < 0f)
                autoCloseRemaining = autoCloseDelay = Mathf.Max(0.1f, delay);
            if (syncPairedLeaf) GetPairedLeaf()?.RequestAutoCloseInternal(delay, false);
        }

        public bool PassageOccupied()
            => PassageOccupiedSelf() || (GetPairedLeaf() is DoorInteractable pair && pair.PassageOccupiedSelf());

        private bool PassageOccupiedSelf()
        {
            Bounds bounds = closedDoorwayBounds;
            bounds.center += transform.position - closedDoorPosition;
            Vector3 size = bounds.size;
            size.x += 0.16f;
            size.z += 0.16f;
            if (size.x < size.z) size.x = Mathf.Max(size.x, 0.9f);
            else size.z = Mathf.Max(size.z, 0.9f);
            int count = Physics.OverlapBoxNonAlloc(bounds.center, size * 0.5f, passageActors,
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count == passageActors.Length) return true;
            for (int i = 0; i < count; i++)
            {
                Collider other = passageActors[i];
                if (other == null || other == doorCollider || other.transform.IsChildOf(transform)
                    || movingPart != null && other.transform.IsChildOf(movingPart)) continue;
                if (other is CharacterController || other.GetComponentInParent<OfficeNpcAgent>() != null) return true;
            }
            return false;
        }

        private DoorInteractable GetPairedLeaf()
        {
            if (pairedLeafResolved) return pairedLeaf;
            pairedLeafResolved = true;
            if (transform.parent == null
                || transform.parent.name.IndexOf("Ganda", System.StringComparison.OrdinalIgnoreCase) < 0)
                return null;
            foreach (DoorInteractable candidate in transform.parent.GetComponentsInChildren<DoorInteractable>(true))
            {
                if (candidate != null && candidate != this && candidate.transform.parent == transform.parent)
                {
                    pairedLeaf = candidate;
                    break;
                }
            }
            return pairedLeaf;
        }

        public void RestoreSavedState(bool open)
        {
            open &= openingAvailable;
            currentAngle = targetAngle = open ? openAngle : 0f;
            autoCloseRemaining = open ? (autoCloseDelay = Random.Range(4f, 7f)) : -1f;
            ApplyRotation();
            SetDoorColliderEnabled(!open);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
            if (!IsOpen && Mathf.Abs(currentAngle) < AngleTolerance) return;
            bool occupied = PassageOccupied();
            if (occupied && IsClosing)
            {
                targetAngle = openAngle;
                autoCloseRemaining = Mathf.Max(0.1f, autoCloseDelay);
            }
            // Count only time spent fully open; slow swing animations must still
            // leave NPCs their full second to cross the doorway.
            if (IsOpen && Mathf.Abs(currentAngle - targetAngle) < AngleTolerance
                && autoCloseRemaining >= 0f)
            {
                if (occupied) autoCloseRemaining = Mathf.Max(0.1f, autoCloseDelay);
                else
                {
                    autoCloseRemaining -= Time.deltaTime;
                    if (autoCloseRemaining <= 0f) ForceClose();
                }
            }
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle,
                swingSpeed * Time.deltaTime);

            if (Mathf.Abs(currentAngle - targetAngle) < AngleTolerance)
            {
                currentAngle = targetAngle;
            }

            ApplyRotation();
            SetDoorColliderEnabled(Mathf.Abs(currentAngle) < AngleTolerance
                && Mathf.Abs(targetAngle) < AngleTolerance);
        }

        protected override string ResolvePromptKey()
        {
            if (!openingAvailable && !string.IsNullOrEmpty(unavailablePromptKey))
                return unavailablePromptKey;
            BossOfficeGate gate = GetBossGate();
            if (gate != null && gate.PlayerBlocked) return gate.BlockedPromptKey;
            return IsOpen ? closePromptKey : openPromptKey;
        }

        private BossOfficeGate GetBossGate()
        {
            if (officeAccessGate != null) return officeAccessGate;
            if (bossGate == null) bossGate = GetComponent<BossOfficeGate>();
            return bossGate;
        }

        private void ApplyRotation()
        {
            if (movingPart == null) movingPart = transform;
            float fraction = Mathf.Abs(openAngle) > AngleTolerance ? currentAngle / openAngle : 0f;
            switch (motion)
            {
                case DoorMotion.Slide:
                    movingPart.localPosition = closedPosition + slideOffset * fraction;
                    break;
                case DoorMotion.RollUp:
                    movingPart.localScale = Vector3.Scale(closedScale,
                        Vector3.Lerp(Vector3.one, openScale, fraction));
                    break;
                default:
                    movingPart.localRotation = Quaternion.AngleAxis(currentAngle, hingeAxis)
                        * closedRotation;
                    break;
            }
        }

        private void SetDoorColliderEnabled(bool value)
        {
            if (solidColliders.Length > 0)
            {
                foreach (Collider collider in solidColliders)
                    if (collider != null && collider.enabled != value) collider.enabled = value;
            }
            else if (doorCollider != null && doorCollider != interactionTrigger
                && doorCollider.enabled != value)
            {
                doorCollider.enabled = value;
            }
        }
    }
}
