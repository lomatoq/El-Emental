using Elemental.Simulation.Animation;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode.MotionMatching
{
    public sealed class GravityMotionFrameTests
    {
        [Test]
        public void ArbitraryUp_RoundTripsPointAndDirection()
        {
            GravityMotionFrame frame = GravityMotionFrame.Create(
                new float3(8f, -3f, 2f),
                math.normalize(new float3(1f, 2f, -0.5f)),
                new float3(-2f, 1f, 4f));
            float3 point = new float3(3f, 6f, -7f);
            float3 direction = math.normalize(new float3(-4f, 2f, 3f));

            Assert.That(math.distance(frame.LocalPointToWorld(frame.WorldPointToLocal(point)), point), Is.LessThan(1e-4f));
            Assert.That(math.distance(frame.LocalDirectionToWorld(frame.WorldDirectionToLocal(direction)), direction), Is.LessThan(1e-4f));
            Assert.That(math.abs(math.dot(frame.Up, frame.Forward)), Is.LessThan(1e-5f));
        }

        [Test]
        public void Clock_IsRenderRateIndependentAtThirtyHertz()
        {
            var thirty = new DeterministicMotionClock(30f);
            var oneTwenty = new DeterministicMotionClock(30f);
            for (int i = 0; i < 30; i++) thirty.Advance(1f / 30f);
            for (int i = 0; i < 120; i++) oneTwenty.Advance(1f / 120f);

            Assert.That(thirty.ContinuousFrame, Is.EqualTo(30d).Within(1e-4d));
            Assert.That(oneTwenty.ContinuousFrame, Is.EqualTo(30d).Within(1e-4d));
        }

        [TestCase(0.0f, false, ImpactMotionLane.None)]
        [TestCase(0.2f, true, ImpactMotionLane.LightAdditive)]
        [TestCase(0.5f, true, ImpactMotionLane.MediumStagger)]
        [TestCase(0.9f, true, ImpactMotionLane.HeavyRagdoll)]
        public void ImpactLane_PreservesExistingAuthorityThresholds(
            float severity,
            bool grounded,
            ImpactMotionLane expected)
        {
            var context = new ImpactMotionContext(severity, new float3(0f, 0f, -1f), grounded, false);
            Assert.That(ImpactMotionSelector.Select(in context), Is.EqualTo(expected));
        }
    }
}
