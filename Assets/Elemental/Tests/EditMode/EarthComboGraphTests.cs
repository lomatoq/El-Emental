using Elemental.Simulation.Bending;
using Elemental.Simulation.Matter;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthComboGraphTests
    {
        [TestCase(EarthTechniqueId.RaiseWall, EarthTechniqueId.WallSlide, EarthTechniqueId.FractureFan)]
        [TestCase(EarthTechniqueId.PillarJump, EarthTechniqueId.SpearMorph, EarthTechniqueId.MeteorFinish)]
        [TestCase(EarthTechniqueId.WebWave, EarthTechniqueId.CrestPluck, EarthTechniqueId.SurfConversion)]
        [TestCase(EarthTechniqueId.Armor, EarthTechniqueId.ArmorBarrage, EarthTechniqueId.ArmorRepack)]
        [TestCase(EarthTechniqueId.Repair, EarthTechniqueId.PartialRepairFeint, EarthTechniqueId.TerrainStitch)]
        public void HeroMoveHasAtLeastTwoAuthoredFollowups(
            EarthTechniqueId root,
            EarthTechniqueId expectedA,
            EarthTechniqueId expectedB)
        {
            var history = new EarthMoveHistory();
            var matter = new EarthMatterId(19u, 2);
            var record = new EarthMoveRecord(
                root, matter, EarthEventTag.Formed, 100u, 112u, 0.8f, new float3(0f, 0f, 1f));
            history.Add(in record);
            var output = new EarthComboOpportunity[8];

            int count = EarthComboResolver.ResolveNonAlloc(history, 120u, matter, output);

            Assert.That(count, Is.GreaterThanOrEqualTo(2));
            Assert.That(new[] { output[0].Technique, output[1].Technique }, Does.Contain(expectedA));
            Assert.That(new[] { output[0].Technique, output[1].Technique }, Does.Contain(expectedB));
            Assert.That(output[0].Matter, Is.EqualTo(matter));
        }

        [Test]
        public void FractureAndReintegrationBothBecomeFollowupInputs()
        {
            var history = new EarthMoveHistory();
            var matter = new EarthMatterId(7u, 1);
            var fracture = new EarthMoveRecord(
                EarthTechniqueId.FractureFan, matter, EarthEventTag.Fractured,
                10u, 20u, 1f, new float3(1f, 0f, 0f));
            history.Add(in fracture);
            var output = new EarthComboOpportunity[8];

            int count = EarthComboResolver.ResolveNonAlloc(history, 21u, matter, output);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(output[0].RequiredResult | output[1].RequiredResult,
                Is.EqualTo(EarthEventTag.Fractured | EarthEventTag.Reintegrated));
        }

        [Test]
        public void FollowupWindowExpiresWithoutAllocatingNewHistory()
        {
            var history = new EarthMoveHistory(4);
            var record = new EarthMoveRecord(
                EarthTechniqueId.RaiseWall, default, EarthEventTag.Formed,
                0u, 1u, 0.5f, new float3(0f, 0f, 1f));
            history.Add(in record);
            var output = new EarthComboOpportunity[4];

            Assert.That(EarthComboResolver.ResolveNonAlloc(history, 1000u, default, output), Is.Zero);
        }
    }
}
