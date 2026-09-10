using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class RadialKneeFrameTests
    {
        [TestCase(90f)]
        [TestCase(180f)]
        [TestCase(270f)]
        public void KneeBendSurvivesPlanetFrameRotationWithoutAntiFlipDeadlock(float degrees)
        {
            float3 hip = new float3(0f, 1f, 0f);
            float3 original = EarthStableKneeHintSolver.Solve(hip, math.forward(), math.right(), math.up(), -1f, float3.zero) - hip;
            quaternion frame = quaternion.AxisAngle(math.right(), math.radians(degrees));
            float3 transported = EarthStableKneeHintSolver.TransportHistory(original, quaternion.identity, frame);
            float3 rotatedHip = math.rotate(frame, hip);
            float3 result = EarthStableKneeHintSolver.Solve(rotatedHip, math.rotate(frame, math.forward()),
                math.rotate(frame, math.right()), math.rotate(frame, math.up()), -1f, transported) - rotatedHip;
            Assert.That(math.distance(result, math.rotate(frame, original)), Is.LessThan(1e-5f));
        }

        [Test]
        public void TransportRoundTripRetainsMagnitudeAndZeroRemainsUnseeded()
        {
            quaternion a = quaternion.EulerXYZ(.3f, 1.2f, -.7f), b = quaternion.EulerXYZ(2.8f, -.3f, .9f);
            float3 direction = new float3(.2f, -.1f, .8f);
            float3 moved = EarthStableKneeHintSolver.TransportHistory(direction, a, b);
            Assert.That(math.distance(direction, EarthStableKneeHintSolver.TransportHistory(moved, b, a)), Is.LessThan(1e-5f));
            Assert.That(math.length(EarthStableKneeHintSolver.TransportHistory(float3.zero, a, b)), Is.Zero);
            Assert.That(math.length(EarthStableKneeHintSolver.TransportHistory(direction, default, b)), Is.Zero);
        }
    }
}
