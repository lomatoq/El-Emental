using Elemental.Runtime.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthHardSurfaceMeshUtilityTests
    {
        [Test]
        public void FractureCopyPreservesAuthoredExteriorAndHardensOnlyFreshInterior()
        {
            Mesh intact = new Mesh { name = "Authored intact" };
            intact.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f)
            };
            Vector3 n0 = Vector3.forward;
            Vector3 n1 = new Vector3(0.6f, 0f, 0.8f).normalized;
            Vector3 n2 = new Vector3(0f, 0.6f, 0.8f).normalized;
            intact.normals = new[] { n0, n1, n2 };
            intact.triangles = new[] { 0, 1, 2 };

            Mesh piece = new Mesh { name = "Fracture piece", subMeshCount = 2 };
            piece.vertices = new[]
            {
                new Vector3(0.25f, 0.25f, 0f),
                new Vector3(0.50f, 0.25f, 0f),
                new Vector3(0.25f, 0.50f, 0f),
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, 0f, 1f)
            };
            piece.normals = new[]
            {
                Vector3.forward, Vector3.forward, Vector3.forward,
                Vector3.up, Vector3.up, Vector3.up
            };
            piece.colors32 = new[]
            {
                new Color32(1, 2, 3, 4), new Color32(5, 6, 7, 8),
                new Color32(9, 10, 11, 12), new Color32(13, 14, 15, 16),
                new Color32(17, 18, 19, 20), new Color32(21, 22, 23, 24)
            };
            piece.uv = new[]
            {
                Vector2.zero, Vector2.right, Vector2.up,
                Vector2.zero, Vector2.right, Vector2.up
            };
            piece.SetTriangles(new[] { 0, 1, 2 }, 0);
            piece.SetTriangles(new[] { 3, 4, 5 }, 1);

            Mesh result = EarthHardSurfaceMeshUtility.CreateFractureShadedCopy(
                piece, intact, Matrix4x4.identity, 0, 1, "Preserved fracture");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.subMeshCount, Is.EqualTo(2));
            Assert.That(result.vertexCount, Is.EqualTo(6));
            Vector3 expectedExterior = (n0 * 0.5f + n1 * 0.25f + n2 * 0.25f).normalized;
            Assert.That(Vector3.Angle(result.normals[0], expectedExterior), Is.LessThan(0.05f));
            Vector3 expectedInterior = Vector3.Cross(
                piece.vertices[4] - piece.vertices[3],
                piece.vertices[5] - piece.vertices[3]).normalized;
            Assert.That(Vector3.Angle(result.normals[3], expectedInterior), Is.LessThan(0.01f));
            Assert.That(Vector3.Angle(result.normals[4], expectedInterior), Is.LessThan(0.01f));
            Assert.That(Vector3.Angle(result.normals[5], expectedInterior), Is.LessThan(0.01f));
            Assert.That(result.colors32[0], Is.EqualTo(piece.colors32[0]));
            Assert.That(result.uv[1], Is.EqualTo(piece.uv[1]));

            Object.DestroyImmediate(result);
            Object.DestroyImmediate(piece);
            Object.DestroyImmediate(intact);
        }
    }
}
