using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SaksiTerakhir.EditorTools;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public class VehicleWheelMeshSplitterTests
    {
        private Mesh source;
        private readonly List<Mesh> generated = new List<Mesh>();

        [SetUp]
        public void SetUp()
        {
            source = CreateFourWheelMesh();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(source);
            foreach (var mesh in generated)
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Split_CreatesFourWheelMeshesAtTheirOwnCenters()
        {
            var parts = VehicleWheelMeshSplitter.Split(source, Matrix4x4.identity);
            generated.AddRange(parts.Select(part => part.mesh));

            Assert.That(parts.Length, Is.EqualTo(4));
            Assert.That(parts.Select(part => part.center.x).OrderBy(x => x),
                Is.EqualTo(new[] { -0.9f, -0.9f, 0.9f, 0.9f }).Within(0.02f));
            Assert.That(parts.Select(part => part.center.z).OrderBy(z => z),
                Is.EqualTo(new[] { -1.2f, -1.2f, 1.2f, 1.2f }).Within(0.02f));
            Assert.That(parts.All(part => Mathf.Abs(part.center.y - 0.55f) < 0.02f), Is.True);
            Assert.That(parts.All(part => Mathf.Abs(part.radius - 0.45f) < 0.02f), Is.True);
            Assert.That(parts.Sum(part => part.mesh.triangles.Length),
                Is.EqualTo(source.triangles.Length));
        }

        private static Mesh CreateFourWheelMesh()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var centers = new[]
            {
                new Vector3(-0.9f, 0.55f, 1.2f),
                new Vector3(0.9f, 0.55f, 1.2f),
                new Vector3(-0.9f, 0.55f, -1.2f),
                new Vector3(0.9f, 0.55f, -1.2f),
            };

            foreach (var center in centers)
            {
                var start = vertices.Count;
                const int segments = 16;
                const float halfWidth = 0.14f;
                const float radius = 0.45f;
                for (var side = -1; side <= 1; side += 2)
                {
                    for (var segment = 0; segment < segments; segment++)
                    {
                        var angle = 2f * Mathf.PI * segment / segments;
                        vertices.Add(center + new Vector3(
                            side * halfWidth,
                            radius * Mathf.Cos(angle),
                            radius * Mathf.Sin(angle)));
                    }
                }

                for (var segment = 0; segment < segments; segment++)
                {
                    var next = (segment + 1) % segments;
                    var left = start + segment;
                    var leftNext = start + next;
                    var right = start + segments + segment;
                    var rightNext = start + segments + next;
                    triangles.AddRange(new[] { left, right, leftNext, leftNext, right, rightNext });
                }
            }

            var mesh = new Mesh { name = "FourWheelTestMesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
