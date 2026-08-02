using UnityEngine;

namespace SaksiTerakhir.Localization
{
    public sealed class LocalizationBootstrapper : MonoBehaviour
    {
        [SerializeField] private LocalizationTable table;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            LocalizationManager.Initialize(table);
        }
    }
}
