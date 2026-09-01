using Elemental.Presentation.Rendering;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthArenaLargeFragmentRenderingTests
    {
        [Test]
        public void AdmissionFailsClosedForIntactTinyAndInvalidPieces()
        {
            AssertRejected(false, true, 1u, 2f,
                EarthArenaFragmentShadowRejection.InactiveStructure);
            AssertRejected(true, false, 1u, 2f,
                EarthArenaFragmentShadowRejection.NotReleased);
            AssertRejected(true, true, 0u, 2f,
                EarthArenaFragmentShadowRejection.InvalidIdentity);
            AssertRejected(true, true, 1u, float.NaN,
                EarthArenaFragmentShadowRejection.InvalidDiameter);
            AssertRejected(
                true,
                true,
                1u,
                EarthArenaLargeFragmentCapsuleShadowPolicy.MinimumWorldDiameter - 0.001f,
                EarthArenaFragmentShadowRejection.TinyDebris);
            Assert.That(EarthArenaLargeFragmentCapsuleShadowPolicy.TryAdmit(
                true,
                true,
                0xF1234567u,
                EarthArenaLargeFragmentCapsuleShadowPolicy.MinimumWorldDiameter,
                out EarthArenaFragmentShadowRejection admitted), Is.True);
            Assert.That(admitted, Is.EqualTo(EarthArenaFragmentShadowRejection.None));
        }

        [Test]
        public void CohortBudgetAndOrderingAreDeterministicAndBounded()
        {
            Assert.That(
                EarthArenaLargeFragmentCapsuleShadowPolicy.MaximumActiveFragments,
                Is.EqualTo(4));
            Assert.That(
                EarthArenaLargeFragmentCapsuleShadowPolicy.MaximumTrackedStructures,
                Is.EqualTo(8));
            Assert.That(
                EarthArenaLargeFragmentCapsuleShadowPolicy.StableCohortGroupId,
                Is.Not.EqualTo(0u));
            Assert.That(EarthArenaLargeFragmentCapsuleShadowPolicy.ComesBefore(
                2f, 9u, 1f, 1u), Is.True);
            Assert.That(EarthArenaLargeFragmentCapsuleShadowPolicy.ComesBefore(
                1f, 2u, 1f, 3u), Is.True);
            Assert.That(EarthArenaLargeFragmentCapsuleShadowPolicy.ComesBefore(
                1f, 3u, 1f, 2u), Is.False);
        }

        private static void AssertRejected(
            bool active,
            bool released,
            uint stableId,
            float diameter,
            EarthArenaFragmentShadowRejection expected)
        {
            Assert.That(EarthArenaLargeFragmentCapsuleShadowPolicy.TryAdmit(
                active,
                released,
                stableId,
                diameter,
                out EarthArenaFragmentShadowRejection rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(expected));
        }
    }
}
