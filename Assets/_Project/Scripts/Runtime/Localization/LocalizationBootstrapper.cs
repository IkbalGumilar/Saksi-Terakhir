using UnityEngine;

namespace SaksiTerakhir.Localization
{
    public sealed class LocalizationBootstrapper : MonoBehaviour
    {
        [SerializeField] private LocalizationTable table;

        private void Awake()
        {
            // The static manager retains localization state across scene loads.
            // Keep this host scene-owned, since it can also carry the EventSystem.
            LocalizationManager.Initialize(table);
        }
    }
}
