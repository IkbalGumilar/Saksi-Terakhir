using NUnit.Framework;
using SaksiTerakhir.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.Tests
{
    public class DoorInteractionLayerTests
    {
        private const string InteractionLayerName = "Interactable";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string OfficePrefabPath = "Assets/_Project/Prefabs/Office_ArchiveHQ.prefab";
        private const string OfficeScenePath = "Assets/_Project/Scenes/Regional Archive Office.unity";

        [Test]
        public void PlayerProbeAndEveryOfficePrefabDoorUseTheInteractionLayer()
        {
            int interactionLayer = LayerMask.NameToLayer(InteractionLayerName);
            Assert.That(interactionLayer, Is.GreaterThanOrEqualTo(0),
                $"The '{InteractionLayerName}' layer must exist.");
            if (interactionLayer < 0)
            {
                return;
            }

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(playerPrefab, Is.Not.Null, "The player prefab must exist.");
            PlayerInteractor interactor = playerPrefab.GetComponent<PlayerInteractor>();
            Assert.That(interactor, Is.Not.Null, "The player prefab must have an interactor.");
            var serializedInteractor = new SerializedObject(interactor);
            SerializedProperty probeLayers = serializedInteractor.FindProperty("probeLayers");
            Assert.That(probeLayers, Is.Not.Null);
            Assert.That(probeLayers.intValue, Is.EqualTo(1 << interactionLayer),
                "The crosshair probe must only search the interaction layer.");

            GameObject officePrefab = PrefabUtility.LoadPrefabContents(OfficePrefabPath);
            try
            {
                DoorInteractable[] doors = officePrefab.GetComponentsInChildren<DoorInteractable>(true);
                Assert.That(doors, Is.Not.Empty, "The office prefab must contain its doors.");

                foreach (DoorInteractable door in doors)
                {
                    Assert.That(door.gameObject.layer, Is.EqualTo(interactionLayer),
                        $"Door '{door.name}' must be on the interaction layer.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(officePrefab);
            }

            Scene officeScene = EditorSceneManager.OpenScene(OfficeScenePath,
                OpenSceneMode.Additive);
            try
            {
                DoorInteractable[] sceneDoors = FindSceneDoors(officeScene);
                Assert.That(sceneDoors, Is.Not.Empty, "The office scene must contain its doors.");

                foreach (DoorInteractable door in sceneDoors)
                {
                    Assert.That(door.gameObject.layer, Is.EqualTo(interactionLayer),
                        $"Scene door '{door.name}' must be on the interaction layer.");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(officeScene, true);
            }
        }

        private static DoorInteractable[] FindSceneDoors(Scene scene)
        {
            var doors = new System.Collections.Generic.List<DoorInteractable>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                doors.AddRange(root.GetComponentsInChildren<DoorInteractable>(true));
            }

            return doors.ToArray();
        }
    }
}
