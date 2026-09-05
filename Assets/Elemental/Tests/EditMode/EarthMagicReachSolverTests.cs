using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMagicReachSolverTests
    {
        [Test]
        public void DistantVerticalTarget_RemainsInsideShoulderEnvelope()
        {
            EarthMagicReachSample sample = EarthMagicReachSolver.Resolve(
                new float3(0f, 100f, 0.01f),
                EarthCastPhase.Sustain,
                1f);

            Assert.That(sample.LocalAim.y, Is.LessThanOrEqualTo(0.62f));
            Assert.That(sample.ReachMeters, Is.InRange(0.40f, 0.64f));
            Assert.That(math.length(sample.LocalAim), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Strike_ExtendsFurtherThanRecoveryWithoutHyperextension()
        {
            EarthMagicReachSample strike = EarthMagicReachSolver.Resolve(
                new float3(0.2f, 0.1f, 1f), EarthCastPhase.Strike, 0.8f);
            EarthMagicReachSample recovery = EarthMagicReachSolver.Resolve(
                new float3(0.2f, 0.1f, 1f), EarthCastPhase.Recover, 0.8f);

            Assert.That(strike.ReachMeters, Is.GreaterThan(recovery.ReachMeters));
            Assert.That(strike.ReachMeters, Is.LessThanOrEqualTo(0.64f));
        }
    }
}
