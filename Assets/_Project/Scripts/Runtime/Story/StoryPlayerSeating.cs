using SaksiTerakhir.Player;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    /// <summary>
    /// Places the player at a named story seat and keeps input locked while seated.
    /// Animator support is optional: it only writes the configured Bool if the controller
    /// already exposes it, so this helper does not require an animation-controller edit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryPlayerSeating : MonoBehaviour
    {
        [SerializeField] private PlayerController playerMovement;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerCameraController playerCamera;
        [SerializeField] private Animator animator;
        [SerializeField] private string seatedBoolParameter = "IsSeated";

        private bool ownsCinematicLock;

        public bool IsSeated { get; private set; }
        public Transform CurrentSeat { get; private set; }
        public bool HasDependencies => playerMovement != null && characterController != null;

        private void Awake()
        {
            ResolveDependencies();
        }

        private void OnDisable()
        {
            StandUp();
        }

        public bool Configure(PlayerController player, CharacterController controller, Animator playerAnimator)
        {
            StandUp();
            playerMovement = player;
            characterController = controller;
            animator = playerAnimator;
            return HasDependencies;
        }

        public bool SeatAt(Transform seat)
        {
            ResolveDependencies();
            if (seat == null || !HasDependencies) return false;

            StoryPlayerCinematicMover mover = GetComponent<StoryPlayerCinematicMover>();
            mover?.StopCinematicMotion();
            PlaceAt(seat);
            playerMovement.SetCinematicMovementLocked(true);
            playerMovement.StopCinematicMotion();
            ownsCinematicLock = true;
            CurrentSeat = seat;
            IsSeated = true;
            playerCamera?.SetStorySeated(true);
            SetAnimatorSeated(true);
            return true;
        }

        public void StandUp()
        {
            if (!IsSeated && !ownsCinematicLock) return;
            SetAnimatorSeated(false);
            playerCamera?.SetStorySeated(false);
            IsSeated = false;
            CurrentSeat = null;
            if (!ownsCinematicLock) return;
            ownsCinematicLock = false;
            playerMovement?.SetCinematicMovementLocked(false);
        }

        private void PlaceAt(Transform seat)
        {
            bool controllerWasEnabled = characterController.enabled;
            if (controllerWasEnabled) characterController.enabled = false;
            transform.SetPositionAndRotation(seat.position, seat.rotation);
            if (controllerWasEnabled) characterController.enabled = true;
        }

        private void ResolveDependencies()
        {
            if (playerMovement == null) playerMovement = GetComponent<PlayerController>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (playerCamera == null) playerCamera = GetComponent<PlayerCameraController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void SetAnimatorSeated(bool seated)
        {
            if (animator == null || string.IsNullOrWhiteSpace(seatedBoolParameter)
                || !HasParameter(seatedBoolParameter)) return;
            animator.SetBool(seatedBoolParameter, seated);
        }

        private bool HasParameter(string parameter)
        {
            foreach (AnimatorControllerParameter candidate in animator.parameters)
            {
                if (candidate.type == AnimatorControllerParameterType.Bool && candidate.name == parameter)
                    return true;
            }

            return false;
        }
    }
}
