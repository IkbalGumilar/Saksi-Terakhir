using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.EditorTools
{
    public readonly struct VehicleWheelMeshPart
    {
        public readonly string name;
        public readonly Mesh mesh;
        public readonly Vector3 center;
        public readonly float radius;

        public VehicleWheelMeshPart(string name, Mesh mesh, Vector3 center, float radius)
        {
            this.name = name;
            this.mesh = mesh;
            this.center = center;
            this.radius = radius;
        }
    }

    public static class VehicleWheelMeshSplitter
    {
        private static readonly string[] WheelNames = { "RL", "RR", "FL", "FR" };

        public static VehicleWheelMeshPart[] Split(Mesh source, Matrix4x4 sourceToVehicleRoot)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var sourceVertices = source.vertices;
            if (sourceVertices.Length < 12 || source.subMeshCount == 0)
            {
                throw new InvalidOperationException("The combined wheel mesh has no readable triangle data.");
            }

            var rootVertices = new Vector3[sourceVertices.Length];
            var bounds = new Bounds(sourceToVehicleRoot.MultiplyPoint3x4(sourceVertices[0]), Vector3.zero);
            for (var i = 0; i < sourceVertices.Length; i++)
            {
                rootVertices[i] = sourceToVehicleRoot.MultiplyPoint3x4(sourceVertices[i]);
                bounds.Encapsulate(rootVertices[i]);
            }

            var splitX = bounds.center.x;
            var splitZ = bounds.center.z;
            var triangleIndices = new List<int>[4, source.subMeshCount];
            var referencedVertices = new HashSet<int>[4];
            for (var wheel = 0; wheel < 4; wheel++)
            {
                referencedVertices[wheel] = new HashSet<int>();
                for (var submesh = 0; submesh < source.subMeshCount; submesh++)
                {
                    triangleIndices[wheel, submesh] = new List<int>();
                }
            }

            for (var submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                var indices = source.GetTriangles(submesh);
                if (indices.Length % 3 != 0)
                {
                    throw new InvalidOperationException($"Submesh {submesh} is not made of triangles.");
                }

                for (var triangle = 0; triangle < indices.Length; triangle += 3)
                {
                    var a = indices[triangle];
                    var b = indices[triangle + 1];
                    var c = indices[triangle + 2];
                    var centroid = (rootVertices[a] + rootVertices[b] + rootVertices[c]) / 3f;
                    var wheel = (centroid.x >= splitX ? 1 : 0) | (centroid.z >= splitZ ? 2 : 0);
                    triangleIndices[wheel, submesh].Add(a);
                    triangleIndices[wheel, submesh].Add(b);
                    triangleIndices[wheel, submesh].Add(c);
                    referencedVertices[wheel].Add(a);
                    referencedVertices[wheel].Add(b);
                    referencedVertices[wheel].Add(c);
                }
            }

            var result = new VehicleWheelMeshPart[4];
            for (var wheel = 0; wheel < 4; wheel++)
            {
                if (referencedVertices[wheel].Count < 6)
                {
                    DestroyMeshes(result, wheel);
                    throw new InvalidOperationException(
                        $"Wheel quadrant {WheelNames[wheel]} contains too little mesh data.");
                }

                var wheelBounds = BoundsFor(rootVertices, referencedVertices[wheel]);
                var center = wheelBounds.center;
                var radius = (wheelBounds.size.y + wheelBounds.size.z) * 0.25f;
                if (radius <= 0.05f || wheelBounds.size.x <= 0.05f)
                {
                    DestroyMeshes(result, wheel);
                    throw new InvalidOperationException(
                        $"Wheel quadrant {WheelNames[wheel]} has invalid dimensions {wheelBounds.size}.");
                }

                result[wheel] = new VehicleWheelMeshPart(
                    WheelNames[wheel], BuildPartMesh(source, rootVertices, center, referencedVertices[wheel],
                        triangleIndices, wheel, sourceToVehicleRoot), center, radius);
            }

            return result;
        }

        private static Bounds BoundsFor(Vector3[] vertices, HashSet<int> indices)
        {
            using (var enumerator = indices.GetEnumerator())
            {
                enumerator.MoveNext();
                var bounds = new Bounds(vertices[enumerator.Current], Vector3.zero);
                while (enumerator.MoveNext())
                {
                    bounds.Encapsulate(vertices[enumerator.Current]);
                }

                return bounds;
            }
        }

        private static Mesh BuildPartMesh(Mesh source, Vector3[] rootVertices, Vector3 center,
            HashSet<int> sourceIndices, List<int>[,] triangleIndices, int wheel,
            Matrix4x4 sourceToVehicleRoot)
        {
            var sourceNormals = source.normals;
            var normalMatrix = sourceToVehicleRoot.inverse.transpose;
            var sourceUvs = source.uv;
            var sourceUv2 = source.uv2;
            var sourceColors = source.colors32;
            var map = new Dictionary<int, int>(sourceIndices.Count);
            var vertices = new List<Vector3>(sourceIndices.Count);
            var normals = new List<Vector3>(sourceIndices.Count);
            var uvs = new List<Vector2>(sourceIndices.Count);
            var uv2 = new List<Vector2>(sourceIndices.Count);
            var colors = new List<Color32>(sourceIndices.Count);

            foreach (var oldIndex in sourceIndices)
            {
                map.Add(oldIndex, vertices.Count);
                vertices.Add(rootVertices[oldIndex] - center);
                if (sourceNormals.Length == rootVertices.Length)
                {
                    normals.Add(normalMatrix.MultiplyVector(sourceNormals[oldIndex]).normalized);
                }
                if (sourceUvs.Length == rootVertices.Length) uvs.Add(sourceUvs[oldIndex]);
                if (sourceUv2.Length == rootVertices.Length) uv2.Add(sourceUv2[oldIndex]);
                if (sourceColors.Length == rootVertices.Length) colors.Add(sourceColors[oldIndex]);
            }

            var mesh = new Mesh { name = $"{source.name}_Wheel_{WheelNames[wheel]}" };
            if (vertices.Count > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.subMeshCount = source.subMeshCount;
            for (var submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                var sourceTriangles = triangleIndices[wheel, submesh];
                var partTriangles = new int[sourceTriangles.Count];
                for (var i = 0; i < sourceTriangles.Count; i++)
                {
                    partTriangles[i] = map[sourceTriangles[i]];
                }
                mesh.SetTriangles(partTriangles, submesh, false);
            }

            if (uvs.Count == vertices.Count) mesh.SetUVs(0, uvs);
            if (uv2.Count == vertices.Count) mesh.SetUVs(1, uv2);
            if (colors.Count == vertices.Count) mesh.SetColors(colors);
            if (normals.Count == vertices.Count) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void DestroyMeshes(VehicleWheelMeshPart[] parts, int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (parts[i].mesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(parts[i].mesh);
                }
            }
        }
    }
}
