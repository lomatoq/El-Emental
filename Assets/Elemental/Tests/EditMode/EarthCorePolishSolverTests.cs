using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Rendering;
using Elemental.Simulation.Time;
using Elemental.Simulation.Voxel;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthCorePolishSolverTests
    {
        [Test]
        public void GravityGripCancelsPlanetGravityAndCapsLinearAndAngularMotion()
        {
            EarthGravityGripSample sample = EarthGravityGripSolver.Solve(
                new float3(0f, 24f, 0f),
                new float3(0f, 20f, 0f),
                new float3(7f, -3f, 5f),
                new float3(0f, 28f, 0f),
                new float3(0f, -14f, 0f),
                16f, 5.5f, 6.5f, 62f, 16f, 0.02f);

            Assert.That(math.all(math.isfinite(sample.Acceleration)), Is.True);
            Assert.That(sample.SpeedLimited, Is.True);
            Assert.That(math.dot(sample.AngularAcceleration, new float3(7f, -3f, 5f)), Is.LessThan(0f));
            Assert.That(math.distance(
                EarthGravityGripSolver.SlotOffset(11u, 1.35f, new float3(0f, 1f, 0f)),
                EarthGravityGripSolver.SlotOffset(12u, 1.35f, new float3(0f, 1f, 0f))), Is.GreaterThan(0.01f));
        }

        [Test]
        public void MovingSupportCarryIsFiniteAndAccelerationLimited()
        {
            float3 acceleration = MovingSurfaceSolver.CarryAcceleration(
                new float3(1f, -8f, 0f),
                new float3(0f, 5f, 0f),
                new float3(0f, 1f, 0f),
                0.22f,
                8f,
                55f,
                0.02f);

            Assert.That(math.all(math.isfinite(acceleration)), Is.True);
            Assert.That(math.length(acceleration), Is.LessThanOrEqualTo(55.001f));
            Assert.That(acceleration.y, Is.GreaterThan(0f));
        }

        [Test]
        public void RadiusEightySelectsOnlyBoundedSurfaceShellChunks()
        {
            const float radius = 80f;
            const float size = 16f;
            const float margin = 1.35f;
            int extent = (int)math.ceil((radius + margin) / size) + 1;
            int selected = 0;
            for (int x = -extent; x <= extent; x++)
            for (int y = -extent; y <= extent; y++)
            for (int z = -extent; z <= extent; z++)
                if (PlanetChunkShellSolver.IntersectsSurfaceShell(new int3(x, y, z), size, radius, margin)) selected++;

            Assert.That(selected, Is.GreaterThan(350));
            Assert.That(selected, Is.LessThanOrEqualTo(512));
        }

        [Test]
        public void CelestialDirectionsStayNormalizedAndAdvanceDeterministically()
        {
            CelestialSnapshot dawn = CelestialEphemerisSolver.Evaluate(0d, 480f, 1440f, 240f, 18f, 0.21f);
            CelestialSnapshot later = CelestialEphemerisSolver.Evaluate(120d, 480f, 1440f, 240f, 18f, 0.21f);
            CelestialSnapshot repeat = CelestialEphemerisSolver.Evaluate(120d, 480f, 1440f, 240f, 18f, 0.21f);

            Assert.That(math.length(dawn.SunDirection), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(math.length(later.MoonDirection), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(math.distance(dawn.SunDirection, later.SunDirection), Is.GreaterThan(0.5f));
            Assert.That(math.distance(later.SunDirection, repeat.SunDirection), Is.LessThan(0.000001f));
            Assert.That(later.MoonPhase01, Is.InRange(0f, 1f));
        }

        [Test]
        public void LocalProjectionTexelMovesAndRotatesWithTheStone()
        {
            float3 original = TriplanarProjectionFrameSolver.Project(
                new float3(12f, 5f, -1f),
                new float3(10f, 3f, -4f),
                new float3(1f, 0f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f));
            float3 movedAndRotated = TriplanarProjectionFrameSolver.Project(
                new float3(17f, 0f, 2f),
                new float3(20f, -2f, 0f),
                new float3(0f, 0f, 1f) * 3f,
                new float3(0f, 1f, 0f) * 2f,
                new float3(-1f, 0f, 0f) * 4f);

            Assert.That(math.distance(original, movedAndRotated), Is.LessThan(0.0001f));
        }
    }
}
