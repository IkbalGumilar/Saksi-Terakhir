using System.Collections.Generic;
using System.Linq;
using System.Text;
using SaksiTerakhir.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    public static class InteractionProbe
    {
        private const string FallbackScenePath =
            "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const float EyeHeight = 1.6f;
        private const float StandOff = 0.9f;
        private const float ProbeRadius = 0.12f;
        private const float ProbeRange = 2.6f;

        [MenuItem("Saksi Terakhir/Probe Interaction")]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.rootCount == 0)
            {
                scene = EditorSceneManager.OpenScene(FallbackScenePath, OpenSceneMode.Single);
            }

            var doors = Object.FindObjectsByType<DoorInteractable>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(door => door.name)
                .ToArray();

            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("=== INTERACTION PROBE ===");
            report.AppendLine($"  scene {scene.name}  doors={doors.Length}");
            if (doors.Length == 0)
            {
                report.AppendLine("=== END INTERACTION PROBE ===");
                Debug.Log(report.ToString());
                return;
            }

            Physics.SyncTransforms();
            var reached = 0;
            var blocked = 0;
            var missed = 0;
            foreach (DoorInteractable door in doors)
            {
                var filter = door.GetComponent<MeshFilter>();
                var renderer = door.GetComponent<Renderer>();
                if (filter == null || filter.sharedMesh == null || renderer == null)
                {
                    report.AppendLine($"  {door.name,-30} NO MESH");
                    missed++;
                    continue;
                }

                Vector3 normal = door.transform.TransformDirection(
                    ThinAxis(filter.sharedMesh.bounds.extents)).normalized;
                Vector3 target = renderer.bounds.center;
                Vector3 eye = target + normal * StandOff;
                eye.y = renderer.bounds.min.y + EyeHeight;

                var ray = new Ray(eye, (target - eye).normalized);
                var hits = Physics.SphereCastAll(ray, ProbeRadius, ProbeRange, ~0,
                        QueryTriggerInteraction.Collide)
                    .OrderBy(hit => hit.distance)
                    .ToArray();

                if (hits.Length == 0)
                {
                    report.AppendLine($"  {door.name,-30} nothing hit");
                    missed++;
                    continue;
                }

                RaycastHit first = hits[0];
                Interactable found = first.collider.GetComponentInParent<Interactable>();
                if (found == door)
                {
                    reached++;
                    continue;
                }

                blocked++;
                report.AppendLine($"  {door.name,-30} first hit {first.collider.name} "
                                  + $"at {first.distance:F2} m "
                                  + $"(interactable={(found == null ? "none" : found.name)})");
            }

            report.AppendLine($"  reached={reached}  blocked={blocked}  missed={missed}");
            report.AppendLine("  --- swing clearance (same box the door uses at runtime) ---");
            var stuck = 0;
            foreach (DoorInteractable door in doors)
            {
                var filter = door.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                BuildProbe(filter.sharedMesh.bounds, door.transform.lossyScale,
                    out Vector3 centre, out Vector3 extents);
                var touching = new HashSet<Collider>(Overlaps(door, centre, extents, 0f));
                string positive = SweepReport(door, centre, extents, 92f, touching);
                string negative = SweepReport(door, centre, extents, -92f, touching);
                if (positive != null && negative != null)
                {
                    stuck++;
                    report.AppendLine($"  {door.name,-30} STUCK  +{positive}  -{negative}");
                }
                else if (positive != null || negative != null)
                {
                    report.AppendLine($"  {door.name,-30} one way only "
                                      + $"({(positive != null ? "+" : "-")} hits "
                                      + $"{positive ?? negative})");
                }
            }

            report.AppendLine($"  doors stuck in both directions: {stuck}");
            report.AppendLine("=== END INTERACTION PROBE ===");
            Debug.Log(report.ToString());
        }

        private static void BuildProbe(Bounds local, Vector3 scale, out Vector3 centre,
            out Vector3 extents)
        {
            centre = local.center;
            extents = local.extents;
            int along = MiddleAxis(extents);
            float far = centre[along] + Mathf.Sign(centre[along]) * extents[along];
            float near = far * 0.25f;
            centre[along] = (near + far) * 0.5f;
            extents[along] = Mathf.Abs(far - near) * 0.5f;
            extents = new Vector3(extents.x * Mathf.Abs(scale.x),
                extents.y * Mathf.Abs(scale.y), extents.z * Mathf.Abs(scale.z));
            extents = Vector3.Max(extents - Vector3.one * 0.02f, Vector3.one * 0.01f);
        }

        private static int MiddleAxis(Vector3 extents)
        {
            int largest = extents.x >= extents.y
                ? (extents.x >= extents.z ? 0 : 2)
                : (extents.y >= extents.z ? 1 : 2);
            int smallest = extents.x <= extents.y
                ? (extents.x <= extents.z ? 0 : 2)
                : (extents.y <= extents.z ? 1 : 2);
            return 3 - largest - smallest;
        }

        private static Collider[] Overlaps(DoorInteractable door, Vector3 centre,
            Vector3 extents, float angle)
        {
            Transform leaf = door.transform;
            Transform parent = leaf.parent;
            Vector3 axis = parent != null
                ? parent.InverseTransformDirection(Vector3.up).normalized
                : Vector3.up;
            Quaternion local = Quaternion.AngleAxis(angle, axis) * leaf.localRotation;
            Quaternion world = parent != null ? parent.rotation * local : local;
            Vector3 boxCentre = leaf.position + world * Vector3.Scale(centre, leaf.lossyScale);
            return Physics.OverlapBox(boxCentre, extents, world, ~0,
                    QueryTriggerInteraction.Ignore)
                .Where(hit => hit.transform != leaf)
                .ToArray();
        }

        private static string SweepReport(DoorInteractable door, Vector3 centre,
            Vector3 extents, float angle, HashSet<Collider> touching)
        {
            foreach (float fraction in new[] { 0.5f, 1f })
            {
                Collider other = Overlaps(door, centre, extents, angle * fraction)
                    .FirstOrDefault(hit => !touching.Contains(hit));
                if (other != null)
                {
                    return $"{other.name}@{angle * fraction:F0}deg";
                }
            }

            return null;
        }

        private static Vector3 ThinAxis(Vector3 extents)
        {
            if (extents.x <= extents.y && extents.x <= extents.z)
            {
                return Vector3.right;
            }

            return extents.y <= extents.z ? Vector3.up : Vector3.forward;
        }
    }
}
