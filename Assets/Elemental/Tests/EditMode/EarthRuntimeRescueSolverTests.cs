using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthRuntimeRescueSolverTests
    {
        [Test]
        public void CentralProbe_IsNarrowEnoughToNotBridgeCharacterSizedHole()
        {
            Assert.That(EarthCentralSupportMath.ProbeRadius(0.5f), Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(EarthCentralSupportMath.ProbeRadius(1.2f), Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(EarthCentralSupportMath.ProbeRadius(0.05f), Is.EqualTo(0.045f).Within(0.0001f));
        }

        [Test]
        public void CentralProbe_UsesItsOwnBottomInsteadOfBroadCapsuleRadius()
        {
            float centerToBottom = EarthCentralSupportMath.CenterToProbeBottom(0.92f, 0.15f);
            Assert.That(centerToBottom, Is.EqualTo(0.77f).Within(0.0001f));
        }

        [Test]
        public void CentralSupport_RejectsWallAndAcceptsWalkableSlope()
        {
            float3 up = new float3(0f, 1f, 0f);
            Assert.That(EarthCentralSupportMath.IsWalkable(up, up, 55f), Is.True);
            Assert.That(EarthCentralSupportMath.IsWalkable(
                math.normalize(new float3(0.7f, 0.7f, 0f)), up, 55f), Is.True);
            Assert.That(EarthCentralSupportMath.IsWalkable(new float3(1f, 0f, 0f), up, 55f), Is.False);
        }

        [Test]
        public void HardLandingSeverity_SeparatesStepStaggerAndRagdollBands()
        {
            Assert.That(EarthHardLandingMath.ImpactSeverity(5.9f), Is.Zero);
            Assert.That(EarthHardLandingMath.ImpactSeverity(7.5f), Is.InRange(2.1f, 4.9f));
            Assert.That(EarthHardLandingMath.ImpactSeverity(12f), Is.GreaterThanOrEqualTo(5.35f));
        }

        [Test]
        public void OrdinaryLocomotion_KeepsSurfaceContactWithoutWorldLockingFoot()
        {
            float contact = EarthFootPlantMotionGate.TargetContactWeight(
                true,
                false,
                false,
                3.5f,
                new float2(0f, 1f));
            Assert.That(contact, Is.GreaterThan(0.4f));
            Assert.That(contact, Is.LessThan(1f));
            Assert.That(EarthFootPlantMotionGate.ShouldLock(
                true,
                false,
                true,
                1f,
                0.1f,
                new float2(0f, 1f)), Is.False);
        }
    }
}
