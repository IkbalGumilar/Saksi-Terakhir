using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SaksiTerakhir.EditorTools
{
    public static class MotorcycleRidingPreviewSetup
    {
        private const string CharacterModelPath =
            "Assets/_Project/Art/Models/MC_ArchiveOfficer_Textured.fbx";
        private const string MotorcycleModelPath =
            "Assets/_Project/Art/Models/GSX_S_125.fbx";
        private const string RidingMotionPath =
            "Assets/_Project/Animations/Vehicle/MC_ArchiveOfficer_Armature.fbx";
        private const string ControllerPath =
            "Assets/_Project/Animation/Vehicle/MC_GSX125_RidingPreview.controller";
        private const string PreviewPrefabPath =
            "Assets/_Project/Prefabs/MC_GSX125_RidingPreview.prefab";
        private const string RidingClipName = "MC_GSX125_RidingLoop";

        [MenuItem("Saksi Terakhir/Build Motorcycle Riding Preview")]
        public static void BuildPreviewAssets()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureFolder("Assets/_Project/Animations", "Vehicle");
            EnsureFolder("Assets/_Project/Animation", "Vehicle");

            GameObject characterAsset = RequireAsset<GameObject>(CharacterModelPath);
            GameObject motorcycleAsset = RequireAsset<GameObject>(MotorcycleModelPath);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(CharacterModelPath)
                .OfType<Avatar>()
                .FirstOrDefault(candidate => candidate != null && candidate.isHuman && candidate.isValid);

            if (avatar == null)
            {
                throw new InvalidOperationException(
                    $"No valid Humanoid Avatar was found in '{CharacterModelPath}'.");
            }

            AnimationClip ridingClip = ConfigureRidingMotion(avatar);
            AnimatorController controller = ConfigureController(ridingClip);
            BuildPreviewPrefab(
                characterAsset,
                motorcycleAsset,
                avatar,
                controller);

            Debug.Log(
                $"Motorcycle riding preview ready: clip='{ridingClip.name}', "
                + $"length={ridingClip.length:F2}s, humanMotion={ridingClip.humanMotion}, "
                + $"loop={ridingClip.isLooping}, prefab='{PreviewPrefabPath}'.");
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip ConfigureRidingMotion(Avatar sourceAvatar)
        {
            var importer = AssetImporter.GetAtPath(RidingMotionPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"The riding motion FBX was not imported at '{RidingMotionPath}'.");
            }

            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
            {
                throw new InvalidOperationException(
                    $"The riding motion FBX contains no animation clips: '{RidingMotionPath}'.");
            }

            ModelImporterClipAnimation riding = clips[0];
            riding.name = RidingClipName;
            riding.firstFrame = 1f;
            riding.lastFrame = 61f;
            riding.loopTime = true;
            riding.loopPose = true;
            riding.wrapMode = WrapMode.Loop;
            importer.clipAnimations = new[] { riding };
            importer.SaveAndReimport();

            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(RidingMotionPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => candidate != null && candidate.name == RidingClipName);

            if (clip == null)
            {
                throw new InvalidOperationException(
                    $"Unity did not extract '{RidingClipName}' from '{RidingMotionPath}'.");
            }

            if (!clip.humanMotion)
            {
                Debug.LogWarning(
                    $"'{RidingClipName}' was imported, but Unity did not mark it as Humanoid motion.",
                    clip);
            }

            return clip;
        }

        private static AnimatorController ConfigureController(AnimationClip ridingClip)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState ridingState = stateMachine.states
                .Select(entry => entry.state)
                .FirstOrDefault(state => state.name == "Riding Loop");

            if (ridingState == null)
            {
                ridingState = stateMachine.AddState("Riding Loop");
            }

            ridingState.motion = ridingClip;
            ridingState.writeDefaultValues = true;
            stateMachine.defaultState = ridingState;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static GameObject BuildPreviewPrefab(
            GameObject characterAsset,
            GameObject motorcycleAsset,
            Avatar avatar,
            AnimatorController controller)
        {
            var root = new GameObject("Pratinjau MC Berkendara");
            try
            {
                GameObject motorcycle =
                    PrefabUtility.InstantiatePrefab(motorcycleAsset) as GameObject;
                GameObject character =
                    PrefabUtility.InstantiatePrefab(characterAsset) as GameObject;

                if (motorcycle == null || character == null)
                {
                    throw new InvalidOperationException(
                        "Could not instantiate the character or motorcycle model.");
                }

                motorcycle.name = "Motor GSX S 125";
                motorcycle.transform.SetParent(root.transform, false);
                motorcycle.transform.localPosition = Vector3.zero;
                motorcycle.transform.localRotation = Quaternion.identity;

                // This imported mesh is a large placeholder cube in the model source.
                // Hide it only in the review prefab; the motorcycle source asset is untouched.
                Transform placeholder = FindChild(motorcycle.transform, "Cube");
                if (placeholder != null)
                {
                    placeholder.gameObject.SetActive(false);
                }

                character.name = "MC - Riding Loop";
                character.transform.SetParent(root.transform, false);
                character.transform.localPosition = Vector3.zero;
                character.transform.localRotation = Quaternion.identity;

                Transform villageOutfit = FindChild(character.transform, "Outfit_Village");
                if (villageOutfit != null)
                {
                    villageOutfit.gameObject.SetActive(false);
                }

                Animator animator = character.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = character.AddComponent<Animator>();
                }

                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                character.transform.rotation = Quaternion.LookRotation(
                    motorcycle.transform.forward,
                    Vector3.up);
                animator.Rebind();
                animator.Update(0f);

                Transform seat = FindChild(motorcycle.transform, "Jok Utama");
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (seat == null || hips == null)
                {
                    throw new InvalidOperationException(
                        "Could not align the rider: the motorcycle seat or Humanoid hips bone is missing.");
                }

                Vector3 desiredHips = seat.position + Vector3.up * 0.12f;
                character.transform.position += desiredHips - hips.position;
                float hipAlignmentError = Vector3.Distance(hips.position, desiredHips);
                if (hipAlignmentError > 0.025f)
                {
                    throw new InvalidOperationException(
                        $"Rider hips are too far from the motorcycle seat (error={hipAlignmentError:F3} m).");
                }
                Debug.Log($"Rider hips aligned to motorcycle seat within {hipAlignmentError:F3} m.");

                // Save only this standalone preview. The chapter scene and player prefab stay unchanged.
                PrefabUtility.SaveAsPrefabAsset(root, PreviewPrefabPath);
                return root;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Transform FindChild(Transform parent, string namePart)
        {
            return parent.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform =>
                    transform.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"Required asset is missing: '{path}'.");
            }

            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
