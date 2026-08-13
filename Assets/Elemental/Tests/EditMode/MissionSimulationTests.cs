using Elemental.Simulation.Missions;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class MissionSimulationTests
    {
        [TestCase(MissionStrategyKind.EarthFortify)]
        [TestCase(MissionStrategyKind.AirEvacuate)]
        [TestCase(MissionStrategyKind.WaterCool)]
        public void VolcanoVillageIsSolvableByThreeMateriallyDifferentStrategies(MissionStrategyKind strategy)
        {
            MissionDefinition definition = MissionSimulation.VolcanoVillage(12345u);
            MissionStrategyProfile profile = Profile(strategy);
            var simulation = new MissionSimulation(in definition, in profile);
            for (int tick = 0; tick < 1000 && simulation.Outcome == MissionOutcome.Running; tick++) simulation.Tick(0.1f);
            Assert.That(simulation.Outcome, Is.EqualTo(MissionOutcome.Win));
            Assert.That(simulation.RescuedCount, Is.GreaterThanOrEqualTo(definition.RequiredRescues));
            Assert.That(simulation.BuildScore().Total, Is.GreaterThan(0));
            Assert.That(simulation.Director.ActiveCount, Is.LessThanOrEqualTo(definition.CrisisBudget));
        }

        [Test]
        public void FixedSeedReproducesExactCrisisTimeline()
        {
            MissionDefinition definition = MissionSimulation.VolcanoVillage(777u);
            MissionStrategyProfile profile = MissionStrategyProfile.Air;
            var first = new MissionSimulation(in definition, in profile);
            var second = new MissionSimulation(in definition, in profile);
            for (int tick = 0; tick < 600; tick++)
            {
                first.Tick(0.1f); second.Tick(0.1f);
            }
            Assert.That(second.Director.TimelineCount, Is.EqualTo(first.Director.TimelineCount));
            for (int index = 0; index < first.Director.TimelineCount; index++)
            {
                CrisisEvent a = first.Director.GetTimeline(index);
                CrisisEvent b = second.Director.GetTimeline(index);
                Assert.That(b.Tick, Is.EqualTo(a.Tick));
                Assert.That(b.Kind, Is.EqualTo(a.Kind));
                Assert.That(b.Severity, Is.EqualTo(a.Severity));
                Assert.That(b.Position, Is.EqualTo(a.Position));
            }
        }

        [Test]
        public void DestructionCanHelpRouteAndHurtStructures()
        {
            MissionDefinition definition = MissionSimulation.VolcanoVillage();
            MissionStrategyProfile profile = MissionStrategyProfile.Earth;
            var simulation = new MissionSimulation(in definition, in profile);
            simulation.ApplyTerrainChange(true, false);
            Assert.That(simulation.Objectives.GetState(2), Is.EqualTo(ObjectiveState.Completed));
            float before = simulation.StructureIntegrity;
            simulation.ApplyTerrainChange(false, true);
            Assert.That(simulation.StructureIntegrity, Is.LessThan(before));
        }

        [Test]
        public void EscalationRemainsInsideCrisisBudget()
        {
            MissionDefinition definition = MissionSimulation.VolcanoVillage(99u);
            var director = new CrisisDirector(in definition);
            int maximum = 0;
            for (uint tick = 0; tick < 2000; tick++)
            {
                director.Tick(tick, tick * 0.1f, 0.1f, 1);
                maximum = System.Math.Max(maximum, director.ActiveCount);
                Assert.That(director.ActiveCount, Is.LessThanOrEqualTo(definition.CrisisBudget));
                Assert.That(director.DeferredSpawnCount, Is.GreaterThanOrEqualTo(0));
            }
            Assert.That(maximum, Is.GreaterThan(0));
        }

        private static MissionStrategyProfile Profile(MissionStrategyKind strategy)
        {
            switch (strategy)
            {
                case MissionStrategyKind.EarthFortify: return MissionStrategyProfile.Earth;
                case MissionStrategyKind.AirEvacuate: return MissionStrategyProfile.Air;
                default: return MissionStrategyProfile.Water;
            }
        }
    }
}
