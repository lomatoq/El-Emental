using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Matter;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthChoreographyTests
    {
        [Test]
        public void HeavyWallCommitUsesRootedDialectAndBoundedPoseHold()
        {
            var request = new BendingPoseRequest(
                EarthTechniqueId.RaiseWall,
                EarthCastPhase.Strike,
                new float3(1f, 0.2f, 0.5f),
                new float3(0f, 1f, 0f),
                640f,
                0.92f,
                1f,
                0.35f,
                false,
                new EarthMatterId(7u, 1));

            EarthChoreographySample sample = EarthChoreographySolver.Solve(in request);

            Assert.That(sample.Dialect, Is.EqualTo(EarthBendingDialect.RootedPower));
            Assert.That(sample.StanceWidth01, Is.GreaterThan(0.7f));
            Assert.That(sample.PelvisCompression01, Is.GreaterThan(0.7f));
            Assert.That(sample.PoseHoldSeconds, Is.InRange(0.025f, 0.08f));
            Assert.That(math.abs(math.dot(request.ActionAxis, request.LocalUp)), Is.LessThan(0.0001f));
        }

        [Test]
        public void PrecisionGripUsesCompactDialectWithoutNonCommitHold()
        {
            var request = new BendingPoseRequest(
                EarthTechniqueId.PullStone,
                EarthCastPhase.Sustain,
                new float3(0f, 0f, 1f),
                new float3(0f, 1f, 0f),
                16f,
                0.62f,
                1f,
                0.95f,
                true,
                default);

            EarthChoreographySample sample = EarthChoreographySolver.Solve(in request);

            Assert.That(sample.Dialect, Is.EqualTo(EarthBendingDialect.CompactTactile));
            Assert.That(sample.PoseHoldSeconds, Is.Zero);
            Assert.That(sample.UpperBodyWeight01, Is.GreaterThan(0.5f));
        }
    }
}
