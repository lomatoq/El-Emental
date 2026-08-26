using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthOrganicIdleSolverTests
    {
        [Test]
        public void ZeroWeightProducesNoSecondaryMotion()
        {
            EarthOrganicIdlePose pose = EarthOrganicIdleSolver.Evaluate(12f, 0.42f, 0f);
            Assert.That(pose.Breath, Is.EqualTo(0f));
            Assert.That(pose.WeightShift, Is.EqualTo(0f));
            Assert.That(pose.CounterMotion, Is.EqualTo(0f));
        }

        [Test]
        public void SameTimeAndPhaseProduceDeterministicPose()
        {
            EarthOrganicIdlePose a = EarthOrganicIdleSolver.Evaluate(3.25f, 0.17f, 1f);
            EarthOrganicIdlePose b = EarthOrganicIdleSolver.Evaluate(3.25f, 0.17f, 1f);
            Assert.That(b.Breath, Is.EqualTo(a.Breath));
            Assert.That(b.WeightShift, Is.EqualTo(a.WeightShift));
            Assert.That(b.CounterMotion, Is.EqualTo(a.CounterMotion));
        }

        [Test]
        public void SurfSecondaryMotionLeansIntoBankAndKeepsHeadCounterBalanced()
        {
            EarthOrganicSurfPose pose = EarthOrganicIdleSolver.EvaluateSurf(2f, 1f, 1f, 14f, 1f);

            Assert.That(pose.Pitch, Is.LessThan(-5f));
            Assert.That(pose.Roll, Is.LessThan(0f));
            Assert.That(pose.HeadCounterRoll, Is.GreaterThan(0f));
        }

        [Test]
        public void SurfSecondaryMotionHasZeroOutputAtZeroWeight()
        {
            EarthOrganicSurfPose pose = EarthOrganicIdleSolver.EvaluateSurf(2f, 1f, 1f, 14f, 0f);

            Assert.That(pose.Pitch, Is.Zero);
            Assert.That(pose.Yaw, Is.Zero);
            Assert.That(pose.Roll, Is.Zero);
            Assert.That(pose.HeadCounterRoll, Is.Zero);
        }
    }
}
