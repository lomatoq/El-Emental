using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMatterMassPolicyTests
    {
        [Test]
        public void SameVolumeAlwaysResolvesToSameStoneMass()
        {
            EarthMatterMassProfile profile = EarthMatterMassProfile.ArenaStone;
            float arenaPiece = EarthMatterMassPolicy.ResolveGameplayMass(0.2f, in profile);
            float looseRock = EarthMatterMassPolicy.ResolveGameplayMass(0.2f, in profile);

            Assert.That(looseRock, Is.EqualTo(arenaPiece).Within(0.0001f));
        }

        [Test]
        public void LargerStoneCannotBeLighterThanSmallerStone()
        {
            EarthMatterMassProfile profile = EarthMatterMassProfile.ArenaStone;
            float small = EarthMatterMassPolicy.ResolveGameplayMass(0.03f, in profile);
            float large = EarthMatterMassPolicy.ResolveGameplayMass(0.8f, in profile);

            Assert.That(large, Is.GreaterThan(small));
        }

        [Test]
        public void InvalidGeometryReturnsFiniteMinimumMass()
        {
            EarthMatterMassProfile profile = EarthMatterMassProfile.ArenaStone;
            float mass = EarthMatterMassPolicy.ResolveGameplayMass(float.NaN, in profile);
            float volume = EarthMatterMassPolicy.EstimateBoxVolume(
                new float3(float.NaN, 2f, 3f));

            Assert.That(mass, Is.EqualTo(profile.MinimumGameplayMassKilograms));
            Assert.That(volume, Is.EqualTo(0f));
        }
    }
}
