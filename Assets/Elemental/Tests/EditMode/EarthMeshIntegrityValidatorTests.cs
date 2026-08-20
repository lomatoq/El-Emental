using Elemental.Runtime.Geometry;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMeshIntegrityValidatorTests
    {
        [Test]
        public void ClosedHardEdgeBox_PassesAfterPositionalWeld()
        {
            Mesh mesh = EarthSafeMeshFactory.CreateBox("IntegrityBox", new Bounds(Vector3.zero, new Vector3(2f, 3f, 4f)));
            try
            {
                EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(
                    mesh,
                    EarthMeshIntegrityPolicy.ConvexCollider);

                Assert.That(report.IsValid, Is.True, report.ToString());
                Assert.That(report.ComponentCount, Is.EqualTo(1));
                Assert.That(report.OpenEdgeCount, Is.Zero);
                Assert.That(report.SignedVolume, Is.GreaterThan(23.99d));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void OpenTriangle_IsRejectedForClosedPolicy()
        {
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                triangles = new[] { 0, 1, 2 }
            };
            mesh.RecalculateBounds();
            try
            {
                EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(
                    mesh,
                    EarthMeshIntegrityPolicy.ClosedHero);

                Assert.That(report.Issues & EarthMeshIntegrityIssue.OpenBoundary, Is.Not.Zero);
                Assert.That(report.OpenEdgeCount, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void NegativeTransformDeterminant_IsAlwaysRejected()
        {
            Mesh mesh = EarthSafeMeshFactory.CreateBox("Mirrored", new Bounds(Vector3.zero, Vector3.one));
            try
            {
                EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(
                    mesh,
                    EarthMeshIntegrityPolicy.ClosedHero,
                    Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)));

                Assert.That(report.Issues & EarthMeshIntegrityIssue.NegativeTransformDeterminant, Is.Not.Zero);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void FullyInvertedClosedMesh_IsSafelyRepairable()
        {
            Mesh mesh = EarthSafeMeshFactory.CreateBox("Inverted", new Bounds(Vector3.zero, Vector3.one));
            int[] indices = mesh.triangles;
            for (int index = 0; index < indices.Length; index += 3)
                (indices[index + 1], indices[index + 2]) = (indices[index + 2], indices[index + 1]);
            mesh.triangles = indices;
            Vector3[] normals = mesh.normals;
            for (int index = 0; index < normals.Length; index++) normals[index] = -normals[index];
            mesh.normals = normals;

            try
            {
                bool repaired = EarthMeshIntegrityValidator.TryRepairFullyInvertedClosedMesh(
                    mesh,
                    out EarthMeshIntegrityReport report);

                Assert.That(repaired, Is.True, report.ToString());
                Assert.That(report.IsValid, Is.True, report.ToString());
                Assert.That(report.SignedVolume, Is.GreaterThan(0d));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void MixedWinding_IsRejectedAndNotBlindlyRecalculated()
        {
            Mesh mesh = EarthSafeMeshFactory.CreateBox("Mixed", new Bounds(Vector3.zero, Vector3.one));
            int[] indices = mesh.triangles;
            (indices[1], indices[2]) = (indices[2], indices[1]);
            mesh.triangles = indices;
            try
            {
                bool repaired = EarthMeshIntegrityValidator.TryRepairFullyInvertedClosedMesh(
                    mesh,
                    out EarthMeshIntegrityReport report);

                Assert.That(repaired, Is.False);
                Assert.That(report.Issues & EarthMeshIntegrityIssue.InconsistentWinding, Is.Not.Zero);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ProceduralArmorAndWave_TenThousandSeedCourtHasNoFallback()
        {
            EarthGeometrySeedSweepReport report = EarthGeometrySeedSweep.Run(10000);

            Assert.That(report.Passed, Is.True, report.ToString());
            Assert.That(report.MeshCount, Is.EqualTo(20000));
            Assert.That(report.MaximumTriangleCount, Is.LessThanOrEqualTo(255));
        }
    }
}
