using Elemental.Simulation.Combat;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthArmorDamageResolverTests
    {
        [TestCase(3f, 20f, 1)]
        [TestCase(10f, 24f, 3)]
        [TestCase(30f, 30f, 12)]
        [TestCase(120f, 50f, 12)]
        public void ProjectileMomentumProducesBoundedLocalDamage(
            float mass,
            float speed,
            int expectedBudget)
        {
            var impact = new EarthArmorImpact(mass, speed, 0);
            EarthArmorDamageResult result = EarthArmorDamageResolver.Resolve(in impact);
            Assert.That(result.DamageBudget, Is.EqualTo(expectedBudget));
            Assert.That(result.DamageBudget, Is.InRange(1, 12));
        }

        [Test]
        public void SmallStoneIsFullyAbsorbedButLargeStoneKeepsOnlyCappedResidual()
        {
            EarthArmorDamageResult small = EarthArmorDamageResolver.Resolve(
                new EarthArmorImpact(2f, 18f, 0));
            EarthArmorDamageResult large = EarthArmorDamageResolver.Resolve(
                new EarthArmorImpact(120f, 40f, 0));
            Assert.That(small.FullyBlocked, Is.True);
            Assert.That(large.ResidualVelocityFraction, Is.InRange(0.20f, 0.55f));
        }
    }
}
