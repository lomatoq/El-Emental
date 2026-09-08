using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthStoneLethalityTests
    {
        [Test]
        public void VerifiedPinnedBodyGetsSlowDamageBelowHeavyCrushGateAndStopsAfterRelease()
        {
            float elapsed = 0f;
            for (int i = 0; i < 60; i++) elapsed = EarthSustainedCrush.StepPinned(elapsed, 588f, 42f, true, true, .02f);
            Assert.That(EarthSustainedCrush.PinnedDamage(elapsed, 588f, 42f, .02f), Is.Zero, "Pin requires1.25s, not one impact.");
            for (int i = 0; i < 4; i++) elapsed = EarthSustainedCrush.StepPinned(elapsed, 588f, 42f, true, true, .02f);
            Assert.That(EarthSustainedCrush.PinnedDamage(elapsed, 588f, 42f, 1f), Is.EqualTo(12f).Within(.001f));
            Assert.That(EarthSustainedCrush.StepPinned(elapsed, 0f, 42f, true, true, .02f), Is.Zero);
            Assert.That(EarthSustainedCrush.StepPinned(elapsed, 588f, 42f, true, false, .02f), Is.Zero);
        }

        [Test]
        public void StandingLightAndNearbyUnloadedObstructionsCannotCausePinDamage()
        {
            Assert.That(EarthSustainedCrush.StepPinned(1.2f, 588f, 42f, false, true, .02f), Is.Zero);
            Assert.That(EarthSustainedCrush.StepPinned(1.2f, 200f, 42f, true, true, .02f), Is.Zero);
            Assert.That(EarthSustainedCrush.StepPinned(1.2f, 0f, 42f, true, true, .02f), Is.Zero);
            Assert.That(EarthSustainedCrush.PinnedDamage(1.25f, 200f, 42f, .02f), Is.Zero);
        }

        [Test]
        public void WeakRealStoneContactHasChipDamageWithoutPromotingItsPhysicalResponse()
        {
            float impulse = EarthCharacterImpactSolver.StoneImpulse(20f, 42f, 2f);
            float responseVelocity = impulse / 42f;
            var outcome = new CharacterOutcomeInput(EarthCharacterImpactSourceKind.LooseStone, 0f, 0f, responseVelocity);
            Assert.That(CharacterOutcomeResolver.Resolve(outcome), Is.EqualTo(CharacterOutcome.Ignore));
            Assert.That(EarthCharacterImpactSolver.ResolvesHealthDamage(EarthCharacterImpactSourceKind.LooseStone,
                EarthCharacterImpactResponse.Ignore), Is.True);
            Assert.That(EarthCharacterImpactSolver.StoneDamage(8f, responseVelocity), Is.GreaterThan(0f));
            Assert.That(EarthCharacterImpactSolver.ResolvesHealthDamage(EarthCharacterImpactSourceKind.Physics,
                EarthCharacterImpactResponse.Ignore), Is.False);
        }

        [TestCase(-2f)] [TestCase(0f)] [TestCase(.1f)] [TestCase(.75f)]
        public void NonClosingAndGrazingContactsRemainBelowDamageAdmission(float normalClosingSpeed)
        {
            Assert.That(EarthCharacterImpactSolver.StoneImpulse(600f, 42f, normalClosingSpeed), Is.Zero);
        }

        [Test]
        public void ObliqueContactPreservesMeasuredVerticalPileWeight()
        {
            float3 normal = math.normalize(new float3(.9165f, .4f, 0f));
            Assert.That(EarthSustainedCrush.OverheadContactForce(new float3(70f, 35f, 0f), normal,
                math.up(), .4f, .02f), Is.EqualTo(1750f).Within(.01f));
            Assert.That(EarthSustainedCrush.OverheadContactForce(new float3(70f, 35f, 0f), math.right(),
                math.up(), .4f, .02f), Is.Zero, "Sideways wedging without an upward contact is not overhead load.");
            Assert.That(EarthSustainedCrush.OverheadContactForce(new float3(0f, 35f, 0f), math.up(),
                math.up(), -.4f, .02f), Is.Zero, "Ground beneath the actor cannot become overhead weight.");
        }
    }
}
