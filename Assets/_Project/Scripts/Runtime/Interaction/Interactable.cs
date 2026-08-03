using UnityEngine;

namespace SaksiTerakhir.Interaction
{
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] private string promptKey = "interact.generic";

        public string PromptKey => ResolvePromptKey();

        public virtual bool CanInteract(Transform actor)
        {
            return isActiveAndEnabled;
        }

        public abstract void Interact(Transform actor);

        protected virtual string ResolvePromptKey()
        {
            return promptKey;
        }
    }
}
