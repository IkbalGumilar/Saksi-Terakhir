using System.Linq;
using System.Text;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    public static class PlayerRigFixer
    {
        private const string FallbackScenePath =
            "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string ControllerPath = "Assets/_Project/Animation/MC_Locomotion.controller";

        private const float NearClip = 0.05f;
        private const float FarClip = 300f;
        private const float SkinWidth = 0.02f;

        private static readonly float Diagonal = Mathf.Sqrt(0.5f);

        private static readonly string[] HiddenInFirstPerson = { "_Head", "_Glasses" };

        private static readonly (string token, Vector2 position)[] ClipDirections =
        {
            ("forward-left", new Vector2(-1f, 1f)),
            ("forward-right", new Vector2(1f, 1f)),
            ("backward-left", new Vector2(-1f, -1f)),
            ("backward-right", new Vector2(1f, -1f)),
            ("forward", new Vector2(0f, 1f)),
            ("backward", new Vector2(0f, -1f)),
            ("left", new Vector2(-1f, 0f)),
            ("right", new Vector2(1f, 0f)),
            ("idle", Vector2.zero),
        };

        [MenuItem("Saksi Terakhir/Align Locomotion Blend Tree")]
        public static void AlignBlendTree()
        {
            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("=== BLEND TREE ALIGNMENT ===");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            BlendTree tree = controller == null
                ? null
                : AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>()
                    .FirstOrDefault();
            if (tree == null)
            {
                report.AppendLine($"  no blend tree found in {ControllerPath}");
                report.AppendLine("=== END BLEND TREE ALIGNMENT ===");
                Debug.Log(report.ToString());
                return;
            }

            ChildMotion[] children = tree.children;
            for (var index = 0; index < children.Length; index++)
            {
                Motion motion = children[index].motion;
                if (motion == null)
                {
                    report.AppendLine($"  slot {index} is still empty - left at "
                                      + $"{children[index].position}");
                    continue;
                }

                Vector2? direction = DirectionFor(motion.name);
                if (direction == null)
                {
                    report.AppendLine($"  {motion.name,-42} no direction in the name - left at "
                                      + $"{children[index].position}");
                    continue;
                }

                Vector2 position = direction.Value;
                if (Mathf.Abs(position.x) > 0.5f && Mathf.Abs(position.y) > 0.5f)
                {
                    position *= Diagonal;
                }

                report.AppendLine($"  {motion.name,-42} {children[index].position} -> {position}");
                children[index].position = position;
            }

            tree.children = children;
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();

            report.AppendLine("=== END BLEND TREE ALIGNMENT ===");
            Debug.Log(report.ToString());
        }

        [MenuItem("Saksi Terakhir/Fix Player Rig")]
        public static void FixPlayerRig()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.rootCount == 0)
            {
                scene = EditorSceneManager.OpenScene(FallbackScenePath, OpenSceneMode.Single);
            }

            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("=== PLAYER RIG FIX ===");

            var input = Object.FindObjectsByType<PlayerInputReader>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (input == null)
            {
                report.AppendLine("  no PlayerInputReader in the open scene");
                report.AppendLine("=== END PLAYER RIG FIX ===");
                Debug.Log(report.ToString());
                return;
            }

            GameObject player = input.gameObject;
            report.AppendLine($"  player object: {player.name}");

            if (!player.activeSelf)
            {
                player.SetActive(true);
                report.AppendLine("  activated the player object");
            }

            var body = player.GetComponent<CharacterController>();
            if (body != null && !Mathf.Approximately(body.skinWidth, SkinWidth))
            {
                report.AppendLine($"  skinWidth {body.skinWidth:F3} -> {SkinWidth:F3}");
                body.skinWidth = SkinWidth;
            }

            var look = player.GetComponent<PlayerCameraController>();
            if (look != null && Assign(look, "body", body))
            {
                report.AppendLine("  PlayerCameraController.body assigned");
            }

            var animator = player.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.applyRootMotion)
            {
                animator.applyRootMotion = false;
                report.AppendLine("  applyRootMotion turned off");
            }

            var camera = player.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                report.AppendLine($"  camera clip {camera.nearClipPlane:F2}/{camera.farClipPlane:F0}"
                                  + $" -> {NearClip:F2}/{FarClip:F0}");
                camera.nearClipPlane = NearClip;
                camera.farClipPlane = FarClip;
            }

            var driver = player.GetComponent<PlayerAnimatorDriver>();
            if (driver == null)
            {
                driver = player.AddComponent<PlayerAnimatorDriver>();
                report.AppendLine("  added PlayerAnimatorDriver");
            }

            Assign(driver, "input", input);
            Assign(driver, "body", player.GetComponent<PlayerController>());
            Assign(driver, "animator", animator);

            var firstPerson = player.GetComponent<FirstPersonBody>();
            if (firstPerson != null)
            {
                Object.DestroyImmediate(firstPerson, true);
                report.AppendLine("  removed FirstPersonBody (head-bone scaling shrank the shadow too)");
            }

            foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                bool hidden = HiddenInFirstPerson.Any(token => renderer.name.Contains(token));
                var mode = hidden
                    ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                    : UnityEngine.Rendering.ShadowCastingMode.On;
                if (renderer.shadowCastingMode == mode)
                {
                    continue;
                }

                renderer.shadowCastingMode = mode;
                report.AppendLine($"  {renderer.name,-36} cast shadows -> {mode}");
            }

            var interactor = player.GetComponent<PlayerInteractor>();
            var prompt = Object.FindObjectsByType<InteractionPromptView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (prompt != null && interactor != null && Assign(prompt, "interactor", interactor))
            {
                report.AppendLine("  InteractionPromptView.interactor assigned");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine($"  saved {scene.path}");
            report.AppendLine("=== END PLAYER RIG FIX ===");
            Debug.Log(report.ToString());
        }

        private static bool Assign(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"Field '{field}' not found on {target.GetType().Name}.");
                return false;
            }

            bool changed = property.objectReferenceValue != value;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static Vector2? DirectionFor(string clipName)
        {
            string lowered = clipName.ToLowerInvariant();
            foreach (var (token, position) in ClipDirections)
            {
                if (lowered.Contains(token))
                {
                    return position;
                }
            }

            return null;
        }
    }
}
