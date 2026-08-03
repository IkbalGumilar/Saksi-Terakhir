using SaksiTerakhir.Localization;
using TMPro;
using UnityEngine;

namespace SaksiTerakhir.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private string keyGlyph = "E";

        private void Awake()
        {
            if (interactor == null || promptRoot == null || actionLabel == null)
            {
                Debug.LogError(
                    $"{nameof(InteractionPromptView)} on '{gameObject.name}' is missing a reference "
                    + "(interactor, prompt root or action label).",
                    this);
                enabled = false;
                return;
            }

            promptRoot.SetActive(false);
        }

        private void OnEnable()
        {
            interactor.TargetChanged += OnTargetChanged;
            LocalizationManager.LanguageChanged += Refresh;
            OnTargetChanged(interactor.Current);
        }

        private void OnDisable()
        {
            interactor.TargetChanged -= OnTargetChanged;
            LocalizationManager.LanguageChanged -= Refresh;
        }

        private void OnTargetChanged(Interactable target)
        {
            promptRoot.SetActive(target != null);
            Refresh();
        }

        private void Refresh()
        {
            Interactable target = interactor.Current;
            if (target == null)
            {
                return;
            }

            if (keyLabel != null)
            {
                keyLabel.text = keyGlyph;
            }

            actionLabel.text = LocalizationManager.Get(target.PromptKey);
        }
    }
}
