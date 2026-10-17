using System.IO;
using SaksiTerakhir.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    /// <summary>
    /// Editor tool to verify and set up Office BGM in Regional Archive Office.
    /// Automatically pairs BGM_Sunny_Morning_Office and BGM_Sunny_Morning_Office-2.
    /// </summary>
    [InitializeOnLoad]
    public static class OfficeAudioSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string MenuMusicPath = "Assets/_Project/Audio/Ambience/BGM_Sunny_Morning_Office.wav";
        private const string OfficeMusicPath = "Assets/_Project/Audio/Ambience/BGM_Sunny_Morning_Office-2.wav";

        static OfficeAudioSetup()
        {
            EditorApplication.delayCall += CheckAndSetupAudio;
        }

        private static void CheckAndSetupAudio()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) return;
            SetupInScene(scene);
        }

        [MenuItem("Saksi Terakhir/Audio/Setup Office Audio")]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath);
            }
            SetupInScene(scene);
        }

        public static void SetupInScene(Scene scene)
        {
            AudioClip menuClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MenuMusicPath);
            AudioClip officeClip = AssetDatabase.LoadAssetAtPath<AudioClip>(OfficeMusicPath);

            OfficeBgmController bgm = Object.FindFirstObjectByType<OfficeBgmController>();
            if (bgm == null)
            {
                GameObject chapterObj = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren<SaksiTerakhir.Story.ChapterOneDirector>(true) != null)
                    {
                        chapterObj = root;
                        break;
                    }
                }

                GameObject targetObj = chapterObj != null ? chapterObj : new GameObject("[Office Audio]");
                bgm = targetObj.AddComponent<OfficeBgmController>();
                Undo.RegisterCreatedObjectUndo(bgm, "Add OfficeBgmController");
            }

            SerializedObject so = new SerializedObject(bgm);
            so.Update();
            if (menuClip != null)
            {
                so.FindProperty("menuMusicClip").objectReferenceValue = menuClip;
            }
            if (officeClip != null)
            {
                so.FindProperty("officeGameplayMusicClip").objectReferenceValue = officeClip;
            }
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(bgm);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[OfficeAudioSetup] Office BGM successfully configured with Menu music and Office gameplay music.");
        }
    }
}
