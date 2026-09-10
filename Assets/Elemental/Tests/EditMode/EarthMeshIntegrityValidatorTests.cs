using Elemental.Runtime.Geometry;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMeshIntegrityValidatorTests
    {
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void DistanceWeld_ClosesSharedCornerAcrossCellBoundaryWithoutChangingVertices(float boundary)
        {
            Mesh mesh = EarthSafeMeshFactory.CreateBox("CellBoundary", new Bounds(Vector3.one * 0.5f, Vector3.one));
            try
            {
                Vector3[] vertices = mesh.vertices;
                float tolerance = 0.00001f * mesh.bounds.size.magnitude;
                int corner = 0;
                for (int i = 0; i < vertices.Length; i++)
                    if (vertices[i] == Vector3.zero)
                        vertices[i].x = tolerance * (boundary + (corner++ == 0 ? -0.1f : 0.1f));
                Assert.That(corner, Is.EqualTo(3));
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                var report = EarthMeshIntegrityValidator.Validate(mesh, EarthMeshIntegrityPolicy.ClosedHero);
                Assert.That(report.IsValid, Is.True, report.ToString());
                Assert.That(mesh.vertices, Is.EqualTo(vertices), "Diagnostic welding must preserve split render vertices.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StrictFlatPolicy_SmallTrianglesCheckEachNormal(bool cancelingNormals)
        {
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, new Vector3(0.001f, 0, 0), new Vector3(0, 0.001f, 0) },
                normals = cancelingNormals
                    ? new[] { Vector3.forward, new Vector3(0.8660254f, 0, -0.5f), new Vector3(-0.8660254f, 0, -0.5f) }
                    : new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                triangles = new[] { 0, 1, 2 }
            };
            mesh.RecalculateBounds();
            try
            {
                var policy = new EarthMeshIntegrityPolicy(false, true, false, 4096, strictFlatNormals: true);
                var report = EarthMeshIntegrityValidator.Validate(mesh, policy);
                Assert.That(report.IsValid, Is.EqualTo(!cancelingNormals), report.ToString());
                if (cancelingNormals)
                    Assert.That(report.Issues & EarthMeshIntegrityIssue.InvertedNormals, Is.Not.Zero);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void StrictFlatPolicy_SeparatedClosedComponentsKeepTheirOwnVolumeScale()
        {
            Mesh first = EarthSafeMeshFactory.CreateBox("First", new Bounds(Vector3.zero, Vector3.one));
            Mesh second = EarthSafeMeshFactory.CreateBox("Second", new Bounds(new Vector3(3000, 0, 0), Vector3.one));
            var combined = new Mesh();
            try
            {
                combined.CombineMeshes(new[]
                {
                    new CombineInstance { mesh = first, transform = Matrix4x4.identity },
                    new CombineInstance { mesh = second, transform = Matrix4x4.identity }
                });
                var policy = new EarthMeshIntegrityPolicy(true, true, false, 4096, strictFlatNormals: true);
                var report = EarthMeshIntegrityValidator.Validate(combined, policy);
                Assert.That(report.IsValid, Is.True, report.ToString());
                Assert.That(report.ComponentCount, Is.EqualTo(2));
                Assert.That(report.SignedVolume, Is.EqualTo(2).Within(0.00001));
            }
            finally
            {
                Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(combined);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrDuplicateFace_IsRejected(bool duplicate)
        {
            Mesh mesh = EarthSafeMeshFactory.CreateBox("Malformed", new Bounds(Vector3.zero, Vector3.one));
            try
            {
                int[] original = mesh.triangles;
                int[] changed = new int[original.Length + (duplicate ? 3 : -3)];
                System.Array.Copy(original, changed, System.Math.Min(original.Length, changed.Length));
                if (duplicate) System.Array.Copy(original, 0, changed, original.Length, 3);
                mesh.triangles = changed;
                var report = EarthMeshIntegrityValidator.Validate(mesh, EarthMeshIntegrityPolicy.ClosedHero);
                Assert.That(report.IsValid, Is.False);
                Assert.That(report.Issues & (duplicate ? EarthMeshIntegrityIssue.DuplicateTriangle :
                    EarthMeshIntegrityIssue.OpenBoundary), Is.Not.Zero);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void StrictFlatPolicy_RejectsOneWrongNormal()
        {
            Mesh mesh = EarthSafeMeshFactory.CreateBox("WrongNormal", new Bounds(Vector3.zero, Vector3.one));
            try
            {
                Vector3[] normals = mesh.normals;
                normals[0] = -normals[0];
                mesh.normals = normals;
                var policy = new EarthMeshIntegrityPolicy(true, true, false, 4096, strictFlatNormals: true);
                var report = EarthMeshIntegrityValidator.Validate(mesh, policy);
                Assert.That(report.Issues & EarthMeshIntegrityIssue.InvertedNormals, Is.Not.Zero);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void StrictFlatPolicy_RejectsClosedZeroVolumeComponent()
        {
            Vector3[] corners = { Vector3.zero, Vector3.right, Vector3.up, Vector3.right + Vector3.up };
            int[] topology = { 0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3 };
            var vertices = new Vector3[12];
            var normals = new Vector3[12];
            var indices = new int[12];
            for (int i = 0; i < 12; i += 3)
            {
                Vector3 n = Vector3.Cross(corners[topology[i + 1]] - corners[topology[i]],
                    corners[topology[i + 2]] - corners[topology[i]]).normalized;
                for (int j = 0; j < 3; j++)
                { vertices[i + j] = corners[topology[i + j]]; normals[i + j] = n; indices[i + j] = i + j; }
            }
            var mesh = new Mesh { vertices = vertices, normals = normals, triangles = indices };
            mesh.RecalculateBounds();
            try
            {
                var policy = new EarthMeshIntegrityPolicy(true, true, false, 4096, strictFlatNormals: true);
                var report = EarthMeshIntegrityValidator.Validate(mesh, policy);
                Assert.That(report.OpenEdgeCount, Is.Zero);
                Assert.That(report.Issues & EarthMeshIntegrityIssue.DegenerateClosedComponent, Is.Not.Zero);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void OpenComponent_DoesNotHideSeparateInvertedClosedComponent()
        {
            Mesh mesh = EarthSafeMeshFactory.CreateBox("MixedComponents", new Bounds(Vector3.zero, Vector3.one));
            try
            {
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int[] indices = mesh.triangles;
                int count = vertices.Length;
                int indexCount = indices.Length;
                for (int i = 0; i < indexCount; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
                for (int i = 0; i < normals.Length; i++) normals[i] = -normals[i];
                System.Array.Resize(ref vertices, count + 3);
                System.Array.Resize(ref normals, count + 3);
                System.Array.Resize(ref indices, indexCount + 3);
                vertices[count] = new Vector3(3, 0, 0);
                vertices[count + 1] = new Vector3(4, 0, 0);
                vertices[count + 2] = new Vector3(3, 1, 0);
                for (int i = 0; i < 3; i++) { normals[count + i] = Vector3.forward; indices[indexCount + i] = count + i; }
                mesh.vertices = vertices; mesh.normals = normals; mesh.triangles = indices; mesh.RecalculateBounds();
                var report = EarthMeshIntegrityValidator.Validate(mesh, EarthMeshIntegrityPolicy.ClosedHero);
                Assert.That(report.Issues & EarthMeshIntegrityIssue.OpenBoundary, Is.Not.Zero);
                Assert.That(report.Issues & EarthMeshIntegrityIssue.InvertedClosedComponent, Is.Not.Zero);
                Assert.That(report.InvertedComponentCount, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

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
