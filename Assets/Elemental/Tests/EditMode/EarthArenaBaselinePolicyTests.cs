using Elemental.Simulation.Matter;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthArenaBaselinePolicyTests
    {
        [TestCase(true, EarthMatterPhase.TerrainAttached, EarthRepresentationTier.CanonicalTerrain, true)]
        [TestCase(true, EarthMatterPhase.FreeDynamic, EarthRepresentationTier.HeroPhysical, true)]
        [TestCase(true, EarthMatterPhase.Sleeping, EarthRepresentationTier.SecondaryPhysical, true)]
        [TestCase(true, EarthMatterPhase.Consumed, EarthRepresentationTier.HeroPhysical, false)]
        [TestCase(true, EarthMatterPhase.Sleeping, EarthRepresentationTier.DormantRecord, false)]
        [TestCase(false, EarthMatterPhase.FreeDynamic, EarthRepresentationTier.HeroPhysical, false)]
        public void OnlyLiveAuthoredRepresentationsReturn(bool authored, EarthMatterPhase phase, EarthRepresentationTier representation, bool expected)
        { Assert.That(EarthArenaBaselinePolicy.RestoresRepresentation(authored, phase, representation), Is.EqualTo(expected)); }
    }
}
