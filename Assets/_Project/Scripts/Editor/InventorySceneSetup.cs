using SaksiTerakhir.Inventory;
using SaksiTerakhir.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.Editor
{
    public static class InventorySceneSetup
    {
        [MenuItem("Saksi Terakhir/Inventory/Hubungkan UI Inventory")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            GameObject panel = InventorySceneBootstrap.FindPanel(scene);
            if (!InventoryView.CanBind(panel))
            {
                Debug.LogWarning("Panel Inventory belum lengkap. Dibutuhkan Slot 1 di Inventory dan Inventory Dock.");
                return;
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                PlayerInputReader player = root.GetComponentInChildren<PlayerInputReader>(true);
                if (player == null) continue;
                InventoryPanelController controller = player.GetComponent<InventoryPanelController>();
                if (controller == null) controller = Undo.AddComponent<InventoryPanelController>(player.gameObject);
                Undo.RecordObject(controller, "Hubungkan inventory");
                controller.BindPanel(panel);
                EditorUtility.SetDirty(controller);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = player.gameObject;
                Debug.Log("Inventory terhubung. Atur Columns/Rows di Inventory Panel Controller. Scene belum disimpan otomatis.", controller);
                return;
            }
            Debug.LogWarning("PlayerInputReader tidak ditemukan pada scene aktif.");
        }
    }
}
