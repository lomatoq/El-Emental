using Elemental.Simulation.Materials;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class ThermalWaterTests
    {
        [Test]
        public void SameWaterFreezesMeltsVaporizesAndCondensesWithoutMassLoss()
        {
            MaterialDefinition water = MaterialDefinition.Water;
            var initial = new PhaseState(water.Id, PhaseKind.Liquid, 20f, 2f);
            PhaseTransitionResult frozen = PhaseTransitionMath.ApplyEnergy(in initial, in water, -900f);
            Assert.That(frozen.State.Phase, Is.EqualTo(PhaseKind.Solid));
            PhaseState frozenState = frozen.State;
            PhaseTransitionResult melted = PhaseTransitionMath.ApplyEnergy(in frozenState, in water, 900f);
            Assert.That(melted.State.Phase, Is.EqualTo(PhaseKind.Liquid));
            Assert.That(melted.State.Temperature, Is.EqualTo(20f).Within(0.02f));
            PhaseState meltedState = melted.State;
            PhaseTransitionResult steam = PhaseTransitionMath.ApplyEnergy(in meltedState, in water, 5200f);
            Assert.That(steam.State.Phase, Is.EqualTo(PhaseKind.Gas));
            PhaseState steamState = steam.State;
            PhaseTransitionResult condensed = PhaseTransitionMath.ApplyEnergy(in steamState, in water, -5200f);
            Assert.That(condensed.State.Phase, Is.EqualTo(PhaseKind.Liquid));
            Assert.That(condensed.State.Mass, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void PhaseHysteresisPreventsBoundaryOscillation()
        {
            MaterialDefinition water = MaterialDefinition.Water;
            PhaseState state = new PhaseState(water.Id, PhaseKind.Liquid, 1f, 1f);
            for (int index = 0; index < 100; index++)
            {
                float energy = index % 2 == 0 ? -2f : 2f;
                PhaseTransitionResult result = PhaseTransitionMath.ApplyEnergy(in state, in water, energy, 2f);
                state = result.State;
                Assert.That(state.Phase, Is.EqualTo(PhaseKind.Liquid));
            }
        }

        [Test]
        public void TransferMassIsConservativeAcrossTenThousandOperations()
        {
            MaterialDefinition water = MaterialDefinition.Water;
            var world = new WaterWorld(4);
            WaterVolume a = new WaterVolume(
                new WaterVolumeId(1), 1u, float3.zero, float3.zero, 1f,
                new PhaseState(water.Id, PhaseKind.Liquid, 20f, 10f));
            WaterVolume b = new WaterVolume(
                new WaterVolumeId(2), 1u, new float3(1f, 0f, 0f), float3.zero, 1f,
                new PhaseState(water.Id, PhaseKind.Liquid, 20f, 5f));
            world.Register(in a);
            world.Register(in b);
            for (int index = 0; index < 10000; index++)
            {
                Assert.That(world.TransferMass(index % 2, 1 - (index % 2), 0.001f), Is.True);
            }
            Assert.That(world.Telemetry.CurrentMass, Is.EqualTo(15f).Within(0.001f));
            Assert.That(world.Telemetry.MassError, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void EnergyTelemetryReportsOnlyExplicitClampError()
        {
            MaterialDefinition water = MaterialDefinition.Water;
            var world = new WaterWorld(2);
            WaterVolume volume = new WaterVolume(
                new WaterVolumeId(1), 1u, float3.zero, float3.zero, 1f,
                new PhaseState(water.Id, PhaseKind.Liquid, 20f, 1f));
            world.Register(in volume);
            for (int index = 0; index < 100; index++)
            {
                world.ApplyEnergy(0, in water, index % 2 == 0 ? 50f : -50f);
            }
            Assert.That(world.Telemetry.EnergyError, Is.EqualTo(0f).Within(0.001f));
            Assert.That(world.GetVolume(0).State.Mass, Is.EqualTo(1f));
        }

        [Test]
        public void ThermalWorldUpdatesAndQueriesStayWithinBudgets()
        {
            var world = new ThermalWorld(64, 16);
            for (uint index = 1; index <= 64; index++)
            {
                ThermalRegion region = new ThermalRegion(
                    new ThermalRegionId(index), 1u, float3.zero, 10f,
                    index % 2 == 0 ? 100f : -50f, 1f, 5f, MaterialTags.None, 100);
                Assert.That(world.Register(in region), Is.True);
            }
            Assert.That(world.Tick(0.1f, 8), Is.EqualTo(8));
            Assert.That(world.DeferredUpdateCount, Is.EqualTo(56));
            ThermalSample sample = world.Sample(float3.zero);
            Assert.That(sample.RegionChecks, Is.EqualTo(16));
            Assert.That(world.LastQueryDebt, Is.EqualTo(48));
            Assert.That(float.IsFinite(sample.TemperatureDelta), Is.True);
        }

        [Test]
        public void ReactionsAreDrivenByStateThresholds()
        {
            var resolver = new ReactionResolver();
            MaterialDefinition water = MaterialDefinition.Water;
            PhaseState liquid = new PhaseState(water.Id, PhaseKind.Liquid, 20f, 1f);
            var freezeContext = new ReactionContext(liquid, water, -30f, 30f, 0f, 1f);
            Assert.That(resolver.Resolve(in freezeContext).Kind, Is.EqualTo(ReactionKind.Freeze));
            var boilContext = new ReactionContext(liquid, water, 120f, 0f, 0f, 1f);
            Assert.That(resolver.Resolve(in boilContext).Kind, Is.EqualTo(ReactionKind.Vaporize));
            PhaseState steam = new PhaseState(water.Id, PhaseKind.Gas, 110f, 1f);
            var airSteam = new ReactionContext(steam, water, 0f, 0f, 12f, 1f);
            Assert.That(resolver.Resolve(in airSteam).Kind, Is.EqualTo(ReactionKind.SteamDispersal));

            MaterialDefinition rock = MaterialDefinition.BrittleRock;
            PhaseState hotRock = new PhaseState(rock.Id, PhaseKind.Solid, 400f, 5f);
            var shock = new ReactionContext(hotRock, rock, -100f, 120f, 0f, 1f);
            Assert.That(resolver.Resolve(in shock).Kind, Is.EqualTo(ReactionKind.ThermalShock));

            MaterialDefinition fuel = MaterialDefinition.Fuel;
            PhaseState fuelState = new PhaseState(fuel.Id, PhaseKind.Solid, 220f, 1f);
            var ignition = new ReactionContext(fuelState, fuel, 20f, 0f, 0f, 0.5f);
            Assert.That(resolver.Resolve(in ignition).Kind, Is.EqualTo(ReactionKind.Ignition));
        }
    }
}
