using Elemental.Runtime.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthArenaMeshPickingTests
    {
        [Test]
        public void ClosedCellContainsInteriorAndMeasuresExteriorWithoutCollider()
        {
            Mesh mesh = Cube(Vector3.zero);
            try
            {
                var query = new EarthArenaMeshPicking(mesh);
                Assert.That(query.SquaredDistance(Vector3.zero, Matrix4x4.identity, Matrix4x4.identity, out _), Is.Zero);
                Assert.That(query.SquaredDistance(new Vector3(2, 0, 0), Matrix4x4.identity, Matrix4x4.identity, out _), Is.EqualTo(1).Within(1e-5));
                Assert.That(query.SquaredDistance(new Vector3(1, .4f, .2f), Matrix4x4.identity, Matrix4x4.identity, out _), Is.Zero);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void DistinctGeometryAtSharedOriginRemainsDistinctUnderNonuniformReflectedPose()
        {
            Mesh first = Cube(Vector3.zero), second = Cube(new Vector3(3, 0, 0));
            try
            {
                var firstQuery = new EarthArenaMeshPicking(first);
                var secondQuery = new EarthArenaMeshPicking(second);
                Matrix4x4 pose = Matrix4x4.TRS(new Vector3(17, 23, -8), Quaternion.Euler(28, 56, 14), new Vector3(-2, 3, .5f));
                Vector3 point = pose.MultiplyPoint3x4(new Vector3(3, 0, 0));
                Assert.That(firstQuery.SquaredDistance(point, pose, pose.inverse, out _), Is.EqualTo(16).Within(.001));
                Assert.That(secondQuery.SquaredDistance(point, pose, pose.inverse, out float center), Is.Zero);
                Assert.That(center, Is.LessThan(1e-8f));
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }

        [Test]
        public void QueriesAllocateNothingAfterConstruction()
        {
            Mesh mesh = Cube(Vector3.zero);
            try
            {
                var query = new EarthArenaMeshPicking(mesh);
                query.SquaredDistance(Vector3.zero, Matrix4x4.identity, Matrix4x4.identity, out _);
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 100; i++)
                    query.SquaredDistance(new Vector3(.2f, .1f, .3f), Matrix4x4.identity, Matrix4x4.identity, out _);
                long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        private static Mesh Cube(Vector3 offset)
        {
            var vertices = new[] { new Vector3(-1,-1,-1), new Vector3(1,-1,-1),
                new Vector3(1,1,-1), new Vector3(-1,1,-1), new Vector3(-1,-1,1),
                new Vector3(1,-1,1), new Vector3(1,1,1), new Vector3(-1,1,1) };
            for (int i = 0; i < vertices.Length; i++) vertices[i] += offset;
            var mesh = new Mesh { vertices = vertices, triangles = new[] {
                0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4,
                3,7,6, 3,6,2, 0,4,7, 0,7,3, 1,2,6, 1,6,5 } };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
