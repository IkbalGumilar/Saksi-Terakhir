using System;
using System.Collections.Generic;
using System.Linq;
using SaksiTerakhir.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    /// <summary>Connects authored building leaves without changing source FBX files or saving scenes.</summary>
    public static class WorldDoorSetup
    {
        private const int InteractionLayer = 6;

        [Serializable]
        public sealed class Report
        {
            public int total;
            public int office;
            public int warehouse;
            public int businesses;
            public int houses;
            public int background;
            public int safetyLocked;
            public int swing;
            public int rollUp;
            public int slide;
            public string[] skipped;
        }

        private sealed class Leaf
        {
            public Transform Owner;
            public Transform Part;
            public DoorMotion Motion;
        }

        [MenuItem("Saksi Terakhir/Doors/Configure Active Scene")]
        public static void ConfigureFromMenu() => Debug.Log(ConfigureSceneDoors(SceneManager.GetActiveScene()));

        public static string ConfigureSceneDoors(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Building doors require a loaded scene outside Play Mode.");
            Transform[] objects = scene.GetRootGameObjects().Where(root => root.activeInHierarchy)
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform[] frames = objects.Where(IsFrameRoot).ToArray();
            var leaves = new List<Leaf>();
            var configured = new HashSet<Transform>();
            foreach (Transform candidate in objects)
            {
                Leaf leaf = IdentifyLeaf(candidate);
                if (leaf != null && configured.Add(leaf.Part)) leaves.Add(leaf);
            }

            var report = new Report();
            var skipped = new List<string>();
            foreach (Leaf leaf in leaves)
            {
                DoorInteractable existing = leaf.Owner.GetComponent<DoorInteractable>();
                // Repeating setup preserves the established closed pose and authored swing direction.
                bool motionConfigured = existing != null
                    && new SerializedObject(existing).FindProperty("interactionTrigger").objectReferenceValue != null;
                if (!motionConfigured)
                {
                    if (IsOfficeLeaf(leaf.Owner)) CloseOfficeReviewLeaf(leaf.Part, frames);
                    if (leaf.Motion == DoorMotion.RollUp)
                    {
                        Undo.RecordObject(leaf.Part, "Restore closed roller door");
                        leaf.Part.localScale = Vector3.one;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(leaf.Part);
                    }
                }
                Renderer[] renderers = leaf.Part.GetComponentsInChildren<Renderer>(true);
                if (!TryBounds(renderers, out Bounds bounds))
                {
                    skipped.Add(Path(leaf.Owner) + ": no leaf mesh");
                    continue;
                }
                Collider[] solids = leaf.Part.GetComponentsInChildren<Collider>(true)
                    .Where(collider => !collider.isTrigger).ToArray();
                if (solids.Length == 0)
                {
                    BoxCollider body = Undo.AddComponent<BoxCollider>(leaf.Part.gameObject);
                    FitBox(body, bounds);
                    solids = new Collider[] { body };
                }
                foreach (Collider solid in solids)
                {
                    Undo.RecordObject(solid, "Enable building door collider");
                    solid.enabled = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(solid);
                }

                DoorInteractable door = existing;
                if (door == null) door = Undo.AddComponent<DoorInteractable>(leaf.Owner.gameObject);
                BoxCollider prompt = EnsurePromptCollider(leaf.Owner, bounds);
                Vector3 scale = Vector3.one;
                Vector3 offset = Vector3.zero;
                if (leaf.Motion == DoorMotion.RollUp)
                    scale[VerticalLocalAxis(leaf.Part)] = 0.025f;
                else if (leaf.Motion == DoorMotion.Slide)
                    offset = Vector3.right * (leaf.Part.name.Contains("Right") ? 0.98f : -0.98f);

                Undo.RecordObject(door, "Connect building door");
                door.ConfigureMotion(leaf.Part, leaf.Motion, offset, scale, solids, prompt);
                bool locked = leaf.Motion == DoorMotion.Slide || IsSealedBackground(leaf.Owner);
                door.SetOpeningAvailable(!locked, locked ? "interact.door.locked" : null);
                EditorUtility.SetDirty(door);
                PrefabUtility.RecordPrefabInstancePropertyModifications(door);
                MarkMovingObjects(leaf.Part);
                report.total++;
                if (locked) report.safetyLocked++;
                if (leaf.Motion == DoorMotion.RollUp) report.rollUp++;
                else if (leaf.Motion == DoorMotion.Slide) report.slide++;
                else report.swing++;
                CountCategory(leaf.Owner, report);
            }
            report.skipped = skipped.ToArray();
            if (report.total > 0) EditorSceneManager.MarkSceneDirty(scene);
            return JsonUtility.ToJson(report, true);
        }

        private static Leaf IdentifyLeaf(Transform candidate)
        {
            string name = ModelName(candidate.name);
            if (IsOfficeLeaf(candidate) || name == "OAA_Exterior_Gate_2m"
                || name.StartsWith("Office_ArchiveHQ_DoorLeaf_", StringComparison.Ordinal))
            {
                if (candidate.parent != null && IsOfficeLeaf(candidate.parent)) return null;
                return new Leaf { Owner = candidate, Part = candidate, Motion = DoorMotion.Swing };
            }
            if (name.EndsWith("_Hinge", StringComparison.Ordinal)
                && (name.StartsWith("MH_Door_Leaf_", StringComparison.Ordinal)
                    || name.StartsWith("MH_Gate_", StringComparison.Ordinal))
                || name == "StallDoor_HingePivot" || name == "CubicleDoor_HingePivot")
                return new Leaf { Owner = candidate, Part = candidate, Motion = DoorMotion.Swing };
            if (name == "Pivot_Tirai")
                return new Leaf { Owner = candidate.parent != null ? candidate.parent : candidate,
                    Part = candidate, Motion = DoorMotion.RollUp };
            if (name.EndsWith("_SlidePivot", StringComparison.Ordinal)
                && (name.StartsWith("DoorSet", StringComparison.Ordinal) || name.StartsWith("LiftDoor", StringComparison.Ordinal)))
                return new Leaf { Owner = candidate, Part = candidate, Motion = DoorMotion.Slide };
            return null;
        }

        private static bool IsOfficeLeaf(Transform candidate)
        {
            string name = ModelName(candidate.name);
            return name == "OA_DoorLeaf_Single" || name == "OA_DoorLeaf_Double"
                || name == "Daun_Pintu_Tertutup";
        }

        private static bool IsFrameRoot(Transform candidate)
        {
            string name = ModelName(candidate.name);
            bool named = name == "OA_DoorFrame_Single" || name == "OA_DoorFrame_Double"
                || name == "Kusen_Pintu_Asli";
            return named && (candidate.parent == null || !(candidate.parent.name.StartsWith("OA_DoorFrame_", StringComparison.Ordinal)
                || ModelName(candidate.parent.name) == "Kusen_Pintu_Asli"));
        }

        private static string ModelName(string name)
        {
            int suffix = name.LastIndexOf('.');
            if (suffix < 0 || suffix == name.Length - 1) return name;
            for (int i = suffix + 1; i < name.Length; i++)
                if (!char.IsDigit(name[i])) return name;
            return name.Substring(0, suffix);
        }

        private static void CloseOfficeReviewLeaf(Transform part, Transform[] frames)
        {
            Transform nearest = null;
            float best = float.MaxValue;
            float side = 0f;
            foreach (Transform frame in frames)
            {
                Vector3 local = frame.InverseTransformPoint(part.position);
                float width = frame.name.Contains("Double")
                    || frame.GetComponentsInChildren<Transform>(true)
                        .Any(child => child.name == "OA_DoorFrame_Double") ? 2.4f : 1.2f;
                if (Mathf.Abs(local.y) > 0.25f || Mathf.Abs(local.z) > 0.4f
                    || local.x < -0.15f || local.x > width + 0.15f) continue;
                float distance = (part.position - frame.position).sqrMagnitude;
                if (distance >= best) continue;
                nearest = frame;
                best = distance;
                side = local.x > width * 0.5f ? 180f : 0f;
            }
            if (nearest == null) return;
            Undo.RecordObject(part, "Restore closed office door pose");
            part.rotation = Quaternion.AngleAxis(side, Vector3.up) * nearest.rotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(part);
        }

        private static BoxCollider EnsurePromptCollider(Transform owner, Bounds bounds)
        {
            Transform child = owner.Find("Door Interaction");
            if (child == null)
            {
                var holder = new GameObject("Door Interaction");
                Undo.RegisterCreatedObjectUndo(holder, "Create door interaction trigger");
                holder.transform.SetParent(owner, false);
                child = holder.transform;
            }
            BoxCollider collider = child.GetComponent<BoxCollider>();
            if (collider == null) collider = Undo.AddComponent<BoxCollider>(child.gameObject);
            Undo.RecordObject(collider, "Fit door interaction trigger");
            FitBox(collider, bounds);
            collider.isTrigger = true;
            collider.enabled = true;
            Undo.RecordObject(child.gameObject, "Set interaction layer");
            child.gameObject.layer = InteractionLayer;
            PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            return collider;
        }

        private static bool TryBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer.bounds.size.sqrMagnitude < 0.0001f) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private static void FitBox(BoxCollider collider, Bounds world)
        {
            Vector3 first = collider.transform.InverseTransformPoint(world.min);
            Bounds local = new Bounds(first, Vector3.zero);
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                    for (int z = 0; z < 2; z++)
                        local.Encapsulate(collider.transform.InverseTransformPoint(new Vector3(
                            x == 0 ? world.min.x : world.max.x,
                            y == 0 ? world.min.y : world.max.y,
                            z == 0 ? world.min.z : world.max.z)));
            collider.center = local.center;
            Vector3 size = local.size;
            for (int axis = 0; axis < 3; axis++) size[axis] = Mathf.Max(0.06f, size[axis]);
            collider.size = size;
        }

        private static int VerticalLocalAxis(Transform part)
        {
            Vector3 vertical = part.InverseTransformDirection(Vector3.up);
            int axis = Mathf.Abs(vertical.x) > Mathf.Abs(vertical.y) ? 0 : 1;
            return Mathf.Abs(vertical.z) > Mathf.Abs(vertical[axis]) ? 2 : axis;
        }

        private static void MarkMovingObjects(Transform part)
        {
            NavMeshModifier modifier = part.GetComponent<NavMeshModifier>();
            if (modifier == null) modifier = Undo.AddComponent<NavMeshModifier>(part.gameObject);
            Undo.RecordObject(modifier, "Keep dynamic door out of navigation bake");
            modifier.ignoreFromBuild = true;
            EditorUtility.SetDirty(modifier);
            PrefabUtility.RecordPrefabInstancePropertyModifications(modifier);
            foreach (Transform child in part.GetComponentsInChildren<Transform>(true))
            {
                Undo.RecordObject(child.gameObject, "Mark moving door dynamic");
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            }
        }

        private static bool IsSealedBackground(Transform owner)
        {
            for (Transform parent = owner; parent != null; parent = parent.parent)
                if (parent.name.StartsWith("Gedung_Latar_Timur", StringComparison.Ordinal)) return true;
            return false;
        }

        private static void CountCategory(Transform owner, Report report)
        {
            for (Transform parent = owner; parent != null; parent = parent.parent)
            {
                string name = parent.name;
                if (name.StartsWith("Kantor_Arsip_Modular", StringComparison.Ordinal)) { report.office++; return; }
                if (name.StartsWith("Gudang_", StringComparison.Ordinal)) { report.warehouse++; return; }
                if (name.StartsWith("Toko_", StringComparison.Ordinal) || name.StartsWith("Rumah_Makan_", StringComparison.Ordinal)
                    || name.Contains("SPBU")) { report.businesses++; return; }
                if (name.StartsWith("Rumah_", StringComparison.Ordinal)) { report.houses++; return; }
            }
            report.background++;
        }

        private static string Path(Transform current)
        {
            string path = current.name;
            while (current.parent != null) { current = current.parent; path = current.name + "/" + path; }
            return path;
        }
    }
}
