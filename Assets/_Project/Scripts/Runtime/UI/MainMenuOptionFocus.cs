using UnityEngine;
using UnityEngine.EventSystems;

namespace SaksiTerakhir.UI
{
    /// <summary>Keeps pointer hover and keyboard selection on the same menu option.</summary>
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    [DisallowMultipleComponent]
    public sealed class MainMenuOptionFocus : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            var button = GetComponent<UnityEngine.UI.Button>();
            if (button.isActiveAndEnabled && button.IsInteractable() && EventSystem.current != null)
                button.Select();
        }
    }
}
