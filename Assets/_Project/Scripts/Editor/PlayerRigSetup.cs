using System.Text;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SaksiTerakhir.EditorTools
{
    public static class PlayerRigSetup
    {
        private const string AnimationFolder = "Assets/_Project/Animation";
        private const string ControllerPath = AnimationFolder + "/MC_Locomotion.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string ModelPath =
            "Assets/_Project/Art/Models/MC_ArchiveOfficer_Textured.fbx";
        private const string InputAssetPath = "Assets/InputSystem_Actions.inputactions";

        private const float StandingHeight = 1.75f;
        private const float BodyRadius = 0.3f;

        private static readonly (string name, Vector2 position)[] BlendSlots =
        {
            ("Idle", new Vector2(0f, 0f)),
            ("Walk Forward", new Vector2(0f, 0.6f)),
            ("Walk Back", new Vector2(0f, -0.6f)),
            ("Walk Left", new Vector2(-0.6f, 0f)),
            ("Walk Right", new Vector2(0.6f, 0f)),
            ("Run Forward", new Vector2(0f, 1f)),
            ("Run Back", new Vector2(0f, -1f)),
            ("Run Left", new Vector2(-1f, 0f)),
            ("Run Right", new Vector2(1f, 0f)),
        };

        [MenuItem("Saksi Terakhir/Create Player Animator")]
        public static void CreateAnimator()
        {
            if (!AssetDatabase.IsValidFolder(AnimationFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Animation");
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("=== PLAYER ANIMATOR ===");
            if (controller != null)
            {
                report.AppendLine($"  {ControllerPath} already exists - left untouched");
                report.AppendLine("=== END PLAYER ANIMATOR ===");
                Debug.Log(report.ToString());
                return;
            }

            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(PlayerAnimatorDriver.MoveXParameter,
                AnimatorControllerParameterType.Float);
            controller.AddParameter(PlayerAnimatorDriver.MoveYParameter,
                AnimatorControllerParameterType.Float);
            controller.AddParameter(PlayerAnimatorDriver.SpeedParameter,
                AnimatorControllerParameterType.Float);
            controller.AddParameter(PlayerAnimatorDriver.CrouchParameter,
                AnimatorControllerParameterType.Bool);

            AnimatorState state = controller.CreateBlendTreeInController(
                "Locomotion", out BlendTree tree);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = PlayerAnimatorDriver.MoveXParameter;
            tree.blendParameterY = PlayerAnimatorDriver.MoveYParameter;

            foreach (var (_, position) in BlendSlots)
            {
                tree.AddChild(null, position);
            }

            controller.layers[0].stateMachine.defaultState = state;

            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            report.AppendLine($"  created {ControllerPath}");
            report.AppendLine($"  blend tree {tree.blendType} on "
                              + $"{tree.blendParameter}/{tree.blendParameterY}");
            foreach (var (name, position) in BlendSlots)
            {
                report.AppendLine($"    slot ({position.x,5:F2},{position.y,5:F2})  {name}");
            }

            report.AppendLine("=== END PLAYER ANIMATOR ===");
            Debug.Log(report.ToString());
        }

        [MenuItem("Saksi Terakhir/Create Player Prefab")]
        public static void CreatePlayerPrefab()
        {
            CreateAnimator();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var actions = AssetDatabase.LoadAssetAtPath<Object>(InputAssetPath);

            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("=== PLAYER PREFAB ===");
            if (model == null || controller == null || actions == null)
            {
                report.AppendLine($"  missing input: model={model != null} "
                                  + $"controller={controller != null} actions={actions != null}");
                report.AppendLine("=== END PLAYER PREFAB ===");
                Debug.Log(report.ToString());
                return;
            }

            var root = new GameObject("Player");
            var controllerBody = root.AddComponent<CharacterController>();
            controllerBody.height = StandingHeight;
            controllerBody.radius = BodyRadius;
            controllerBody.center = new Vector3(0f, StandingHeight * 0.5f, 0f);
            controllerBody.slopeLimit = 45f;
            controllerBody.stepOffset = 0.3f;
            controllerBody.skinWidth = 0.02f;
            controllerBody.minMoveDistance = 0f;

            var cameraRoot = new GameObject("Camera Root");
            cameraRoot.transform.SetParent(root.transform, false);
            cameraRoot.transform.localPosition = new Vector3(0f, StandingHeight * 0.92f, 0f);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraRoot.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 120f;
            camera.fieldOfView = 65f;
            cameraObject.AddComponent<AudioListener>();

            var body = (GameObject)PrefabUtility.InstantiatePrefab(model);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            var animator = body.GetComponent<Animator>();
            if (animator == null)
            {
                animator = body.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var input = root.AddComponent<PlayerInputReader>();
            var move = root.AddComponent<PlayerController>();
            var look = root.AddComponent<PlayerCameraController>();
            var interactor = root.AddComponent<PlayerInteractor>();
            var driver = root.AddComponent<PlayerAnimatorDriver>();

            Assign(input, "inputActions", actions);
            Assign(move, "input", input);
            Assign(look, "input", input);
            Assign(look, "body", controllerBody);
            Assign(look, "yawTransform", root.transform);
            Assign(look, "cameraTransform", cameraRoot.transform);
            Assign(interactor, "input", input);
            Assign(interactor, "viewpoint", cameraObject.transform);
            Assign(driver, "input", input);
            Assign(driver, "body", move);
            Assign(driver, "animator", animator);

            string bodyLine = $"  controller height={controllerBody.height:F2} "
                              + $"radius={controllerBody.radius:F2} "
                              + $"step={controllerBody.stepOffset:F2} "
                              + $"skin={controllerBody.skinWidth:F3}";
            string cameraLine = $"  camera root y={cameraRoot.transform.localPosition.y:F2}  "
                                + $"fov={camera.fieldOfView:F0} near={camera.nearClipPlane:F2}  "
                                + $"animator={animator.runtimeAnimatorController.name}";

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            report.AppendLine($"  {PrefabPath} {(success ? "ok" : "FAILED")}");
            report.AppendLine(bodyLine);
            report.AppendLine(cameraLine);
            report.AppendLine("=== END PLAYER PREFAB ===");
            Debug.Log(report.ToString());
        }

        private static void Assign(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"Field '{field}' not found on {target.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
