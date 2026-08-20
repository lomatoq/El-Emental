using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthArmorFormationSolverTests
    {
        [Test]
        public void DirectedDomeUsesThreeDepthLayersAndFacesThreat()
        {
            const int count = 48;
            bool[] layers = new bool[3];
            float3 average = float3.zero;
            float minimumRadius = float.PositiveInfinity;
            float maximumRadius = float.NegativeInfinity;
            for (int index = 0; index < count; index++)
            {
                EarthArmorFormationSample sample = EarthArmorFormationSolver.DirectedDome(
                    index, count, new float3(0f, 0f, 1f), new float3(0f, 1f, 0f), 91u);
                layers[sample.Layer] = true;
                average += sample.Direction;
                minimumRadius = math.min(minimumRadius, sample.RadiusMultiplier);
                maximumRadius = math.max(maximumRadius, sample.RadiusMultiplier);
            }
            Assert.That(layers, Is.All.True);
            Assert.That(math.normalize(average).z, Is.GreaterThan(0.82f),
                "The dome must read as a directional shield, not a uniform sphere.");
            Assert.That(maximumRadius - minimumRadius, Is.GreaterThan(0.12f));
        }

        [Test]
        public void OrbitUsesFiveCounterRotatingNonUniformBands()
        {
            const int count = 50;
            bool[] bands = new bool[5];
            float minimumRadius = float.PositiveInfinity;
            float maximumRadius = float.NegativeInfinity;
            float3 before = EarthArmorFormationSolver.BrokenOrbit(
                1, count, 0f, new float3(0f, 0f, 1f), new float3(0f, 1f, 0f), 13u).Direction;
            float3 after = EarthArmorFormationSolver.BrokenOrbit(
                1, count, 1f, new float3(0f, 0f, 1f), new float3(0f, 1f, 0f), 13u).Direction;
            for (int index = 0; index < count; index++)
            {
                EarthArmorFormationSample sample = EarthArmorFormationSolver.BrokenOrbit(
                    index, count, 0.5f, new float3(0f, 0f, 1f), new float3(0f, 1f, 0f), 13u);
                bands[sample.Layer] = true;
                minimumRadius = math.min(minimumRadius, sample.RadiusMultiplier);
                maximumRadius = math.max(maximumRadius, sample.RadiusMultiplier);
            }
            Assert.That(bands, Is.All.True);
            Assert.That(maximumRadius - minimumRadius, Is.GreaterThan(0.20f));
            Assert.That(math.distance(before, after), Is.GreaterThan(0.15f));
        }

        [Test]
        public void OneHundredSeededFormationsRemainFiniteAndDeterministic()
        {
            for (uint seed = 0u; seed < 100u; seed++)
            {
                for (int index = 0; index < 48; index++)
                {
                    EarthArmorFormationSample first = EarthArmorFormationSolver.DirectedDome(
                        index, 48, new float3(0.3f, 0f, 1f), new float3(0f, 1f, 0f), seed);
                    EarthArmorFormationSample second = EarthArmorFormationSolver.DirectedDome(
                        index, 48, new float3(0.3f, 0f, 1f), new float3(0f, 1f, 0f), seed);
                    Assert.That(math.all(math.isfinite(first.Direction)), Is.True);
                    Assert.That(math.distance(first.Direction, second.Direction), Is.LessThan(0.000001f));
                    Assert.That(first.RadiusMultiplier, Is.EqualTo(second.RadiusMultiplier).Within(0.000001f));
                }
            }
        }
    }
}
