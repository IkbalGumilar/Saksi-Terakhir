using SaksiTerakhir.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.Inventory
{
    public static class InventorySceneBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallLoadedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) Install(SceneManager.GetSceneAt(i));
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install(scene);

        private static void Install(Scene scene)
        {
            GameObject panel = FindPanel(scene);
            if (!InventoryView.CanBind(panel)) return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                PlayerInputReader player = root.GetComponentInChildren<PlayerInputReader>();
                if (player == null) continue;
                if (player.GetComponent<InventoryPanelController>() == null)
                    player.gameObject.AddComponent<InventoryPanelController>();
                return;
            }
        }

        public static GameObject FindPanel(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Canvas") continue;
                Transform panel = root.transform.Find("Save Area/Inventory Panel");
                if (panel != null) return panel.gameObject;
            }
            return null;
        }
    }
}
