using Elemental.Simulation.Combat;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class CharacterOutcomeResolverTests
    {
        [TestCase(0.2f, 2f, CharacterOutcome.Stumble)]
        [TestCase(3.2f, 8f, CharacterOutcome.RecoverableRagdoll)]
        [TestCase(5.5f, 10.9f, CharacterOutcome.RecoverableRagdoll)]
        [TestCase(5.49f, 14f, CharacterOutcome.RecoverableRagdoll)]
        [TestCase(5.5f, 11f, CharacterOutcome.Knockout)]
        public void FallRequiresBothCatastrophicThresholdsForKnockout(
            float distance,
            float speed,
            CharacterOutcome expected)
        {
            var input = new CharacterOutcomeInput(
                EarthCharacterImpactSourceKind.FallLanding,
                distance,
                speed,
                1f);
            Assert.That(CharacterOutcomeResolver.Resolve(in input), Is.EqualTo(expected));
        }

        [Test]
        public void GenericPhysicsNeverChoosesCombatKnockout()
        {
            var input = new CharacterOutcomeInput(
                EarthCharacterImpactSourceKind.Physics,
                0f,
                0f,
                12f);
            Assert.That(
                CharacterOutcomeResolver.Resolve(in input),
                Is.EqualTo(CharacterOutcome.RecoverableRagdoll));
        }
    }
}
