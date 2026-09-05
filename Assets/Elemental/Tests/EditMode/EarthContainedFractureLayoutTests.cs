using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Elemental.Runtime.Geometry;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthContainedFractureLayoutTests
    {
        [TestCase(3)]
        [TestCase(4)]
        public void ChildCellsStayBetweenConvexChordEndpointsWithoutOverlap(int count)
        {
            float3 first = new float3(-2f, 3f, 4f), last = new float3(7f, -1f, 6f);
            float previousEnd = -1f;
            float length = math.distance(first, last);
            for (int i = 0; i < count; i++)
            {
                Assert.That(EarthContainedFractureLayout.TryGetCell(first, last, i, count, out var cell), Is.True);
                float along = math.dot(cell.Center - first, cell.Axis);
                Assert.That(along - cell.HalfWidth, Is.GreaterThan(previousEnd));
                Assert.That(along + cell.HalfWidth, Is.LessThan(length));
                Assert.That(cell.Contains(cell.Center), Is.True);
                Assert.That(cell.Contains(cell.Center + cell.Axis * (cell.HalfWidth * 1.01f)), Is.False);
                previousEnd = along + cell.HalfWidth;
            }
        }

        [Test]
        public void InvalidAndDegenerateInputsAreRejected()
        {
            Assert.That(EarthContainedFractureLayout.TryGetCell(float3.zero, float3.zero, 0, 3, out _), Is.False);
            Assert.That(EarthContainedFractureLayout.TryGetCell(float3.zero, new float3(1), 3, 3, out _), Is.False);
            Assert.That(EarthContainedFractureLayout.TryGetCell(float3.zero, new float3(float.NaN), 0, 3, out _), Is.False);
        }

        [Test]
        public void NaturalWallStoneFitsTrueTetrahedronInsteadOfItsBoundingBox()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var hull = new Mesh { name = "Natural wall tetrahedron" };
            Mesh visual = null;
            try
            {
                hull.vertices = new[] { Vector3.zero, new Vector3(4,0,0), new Vector3(0,.4f,0), new Vector3(0,0,1) };
                hull.triangles = new[] { 0,2,1, 0,1,3, 0,3,2, 1,2,3 };
                hull.RecalculateBounds();
                visual = EarthNaturalFractureVisual.Create(template.GetComponent<MeshFilter>().sharedMesh, hull);
                Assert.That(visual.subMeshCount, Is.EqualTo(1));
                foreach (var point in visual.vertices)
                {
                    Assert.That(point.x, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(point.y, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(point.z, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(point.x / 4f + point.y / .4f + point.z, Is.LessThanOrEqualTo(1.00001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(template); Object.DestroyImmediate(hull);
                if (visual != null) Object.DestroyImmediate(visual);
            }
        }
    }
}
