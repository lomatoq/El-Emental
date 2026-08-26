using Elemental.Simulation.Bending;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthSurfSessionTests
    {
        [Test]
        public void Speed_RisesNonlinearlyFromFourToThirteen()
        {
            EarthSurfProfileData data = EarthSurfProfileData.Default;
            var session = new EarthSurfSession(in data);
            Assert.That(session.Begin(2f), Is.True);
            EarthSurfSample start = session.Sample(2f);
            EarthSurfSample middle = session.Sample(2.6f);
            EarthSurfSample full = session.Sample(3.2f);
            Assert.That(start.Speed, Is.EqualTo(4f).Within(0.001f));
            Assert.That(middle.Speed, Is.GreaterThan(4f).And.LessThan(8.5f));
            Assert.That(full.Speed, Is.EqualTo(13f).Within(0.001f));
            Assert.That(middle.Speed - start.Speed, Is.LessThan(full.Speed - middle.Speed));
        }

        [Test]
        public void Release_RetractsOverPointFourFiveSeconds()
        {
            EarthSurfProfileData data = EarthSurfProfileData.Default;
            var session = new EarthSurfSession(in data);
            session.Begin(0f);
            session.Release(1f);
            Assert.That(session.Sample(1.44f).Complete, Is.False);
            Assert.That(session.Sample(1.45f).Complete, Is.True);
            Assert.That(session.Active, Is.False);
        }

        [Test]
        public void FamilySelection_IsDeterministicAndNeverImmediatelyRepeats()
        {
            EarthSurfSilhouetteFamily first = EarthSurfControlSolver.SelectFamily(
                77u, EarthSurfSilhouetteFamily.BrokenWedge);
            EarthSurfSilhouetteFamily repeated = EarthSurfControlSolver.SelectFamily(
                77u, EarthSurfSilhouetteFamily.BrokenWedge);
            EarthSurfSilhouetteFamily next = EarthSurfControlSolver.SelectFamily(78u, first);
            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(next, Is.Not.EqualTo(first));
        }

        [Test]
        public void WheelAndSteer_ProduceBoundedVisualBankRampAndBrake()
        {
            EarthSurfControlSample ramp = EarthSurfControlSolver.Solve(1f, 1f, 0f, 0f, 0.2f);
            EarthSurfControlSample brake = EarthSurfControlSolver.Solve(-1f, -1f, 0f, 0f, 0.2f);
            Assert.That(ramp.BankDegrees, Is.EqualTo(11f));
            Assert.That(ramp.Ramp01, Is.EqualTo(1f));
            Assert.That(brake.BankDegrees, Is.EqualTo(-11f));
            Assert.That(brake.SpeedMultiplier, Is.LessThan(0.5f));
        }

        [Test]
        public void AllFourHeroSurfFamilies_AreClosedAndVisiblyDifferent()
        {
            int previousVertexCount = -1;
            for (int index = 0; index < 4; index++)
            {
                Mesh mesh = EarthSurfController.BuildHeroMesh(
                    (EarthSurfSilhouetteFamily)index, 2.35f, 3.9f, 0.82f, (uint)(100 + index));
                EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(
                    mesh, EarthMeshIntegrityPolicy.ClosedHero);
                Assert.That(report.IsValid, Is.True, $"family {(EarthSurfSilhouetteFamily)index}: {report.Issues}");
                Assert.That(mesh.bounds.size.x, Is.GreaterThan(2.1f));
                Assert.That(mesh.bounds.size.z, Is.GreaterThan(3.5f));
                Assert.That(HasFaceAndBevelVertexClasses(mesh), Is.True,
                    $"family {(EarthSurfSilhouetteFamily)index} must publish both stone faces and bevels.");
                if (previousVertexCount >= 0)
                    Assert.That(mesh.vertexCount, Is.Not.EqualTo(previousVertexCount),
                        "Adjacent surf families must not be the same wedge with another label.");
                previousVertexCount = mesh.vertexCount;
                Object.DestroyImmediate(mesh);
            }
        }

        private static bool HasFaceAndBevelVertexClasses(Mesh mesh)
        {
            Color[] colors = mesh.colors;
            bool hasFace = false;
            bool hasBevel = false;
            for (int index = 0; index < colors.Length; index++)
            {
                hasFace |= colors[index].a < 0.55f;
                hasBevel |= colors[index].a > 0.62f;
            }
            return colors.Length == mesh.vertexCount && hasFace && hasBevel;
        }
    }
}
