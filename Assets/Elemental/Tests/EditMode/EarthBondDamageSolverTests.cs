using System;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthBondDamageSolverTests
    {
        [Test]
        public void ImpulseDecompositionDistinguishesTensionShearAndCompression()
        {
            float tension = ApplyAndReadDamage(new float3(2f, 0f, 0f));
            float shear = ApplyAndReadDamage(new float3(0f, 2f, 0f));
            float compression = ApplyAndReadDamage(new float3(-2f, 0f, 0f));

            Assert.That(tension, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(shear, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(compression, Is.EqualTo(0.05f).Within(0.0001f));
        }

        [Test]
        public void RadialFalloffAndContactAreaChangeDamageDeterministically()
        {
            EarthBondDefinition center = CreateBond(1, float3.zero);
            EarthBondDefinition edge = CreateBond(2, new float3(0.75f, 0f, 0f));
            EarthBondDefinition broad = CreateBond(3, float3.zero);
            broad.ContactArea = 4f;
            EarthBondDefinition[] definitions = { center, edge, broad };
            EarthBondState[] states = HealthyStates(3);
            var impact = new EarthBondImpact(float3.zero, new float3(2f, 0f, 0f), 1f, 1f, 12);

            EarthBondDamageSolver.ApplyImpact(in impact, definitions, states, 3, null);

            Assert.That(states[0].AccumulatedDamage, Is.GreaterThan(states[1].AccumulatedDamage));
            Assert.That(states[0].AccumulatedDamage, Is.GreaterThan(states[2].AccumulatedDamage));
            Assert.That(states[1].AccumulatedDamage, Is.GreaterThan(0f));
        }

        [Test]
        public void BatchBreaksBondsInStableDefinitionOrderAndReportsOverflow()
        {
            EarthBondDefinition[] definitions =
            {
                CreateBond(7, float3.zero, strength: 1f),
                CreateBond(3, float3.zero, strength: 1f),
                CreateBond(9, float3.zero, strength: 1f)
            };
            EarthBondState[] states = HealthyStates(3);
            EarthBondImpact[] impacts =
            {
                new EarthBondImpact(float3.zero, new float3(2f, 0f, 0f), 1f, 1f, 40)
            };
            var broken = new EarthBondId[2];

            EarthBondDamageResult result = EarthBondDamageSolver.ApplyBatch(
                impacts, 1, definitions, states, 3, broken);

            Assert.That(result.Status, Is.EqualTo(EarthBondDamageStatus.Success));
            Assert.That(result.NewlyBrokenBondCount, Is.EqualTo(3));
            Assert.That(result.WrittenBrokenBondCount, Is.EqualTo(2));
            Assert.That(result.OutputOverflowed, Is.True);
            Assert.That(broken[0], Is.EqualTo(new EarthBondId(7)));
            Assert.That(broken[1], Is.EqualTo(new EarthBondId(3)));
            Assert.That(states[2].Phase, Is.EqualTo(EarthBondPhase.Broken));
            Assert.That(states[2].LastChangedTick, Is.EqualTo(40u));
        }

        [Test]
        public void BrokenAndUnbreakableBondsIgnoreLaterDamage()
        {
            EarthBondDefinition brokenDefinition = CreateBond(1, float3.zero, strength: 1f);
            EarthBondDefinition unbreakableDefinition = CreateBond(2, float3.zero, strength: 1f);
            unbreakableDefinition.Flags |= EarthBondFlags.Unbreakable;
            EarthBondDefinition[] definitions = { brokenDefinition, unbreakableDefinition };
            EarthBondState[] states = HealthyStates(2);
            states[0].Phase = EarthBondPhase.Broken;
            states[0].AccumulatedDamage = 1f;
            var impact = new EarthBondImpact(float3.zero, new float3(100f, 0f, 0f), 2f, 1f, 9);

            EarthBondDamageResult result = EarthBondDamageSolver.ApplyImpact(
                in impact, definitions, states, 2, new EarthBondId[2]);

            Assert.That(result.NewlyBrokenBondCount, Is.Zero);
            Assert.That(states[0].AccumulatedDamage, Is.EqualTo(1f));
            Assert.That(states[1].AccumulatedDamage, Is.Zero);
            Assert.That(states[1].Phase, Is.EqualTo(EarthBondPhase.Healthy));
        }

        [Test]
        public void InvalidImpactIsSkippedWithoutContaminatingState()
        {
            EarthBondDefinition[] definitions = { CreateBond(1, float3.zero) };
            EarthBondState[] states = HealthyStates(1);
            EarthBondImpact[] impacts =
            {
                new EarthBondImpact(float3.zero, new float3(float.NaN, 0f, 0f), 1f, 1f, 1),
                new EarthBondImpact(float3.zero, new float3(1f, 0f, 0f), 0f, 1f, 2)
            };

            EarthBondDamageResult result = EarthBondDamageSolver.ApplyBatch(
                impacts, impacts.Length, definitions, states, 1, null);

            Assert.That(result.ProcessedImpactCount, Is.Zero);
            Assert.That(result.InvalidImpactCount, Is.EqualTo(2));
            Assert.That(states[0].AccumulatedDamage, Is.Zero);
            Assert.That(math.isfinite(states[0].AccumulatedDamage), Is.True);
        }

        [Test]
        public void CapacityErrorsDoNotMutateState()
        {
            EarthBondDefinition[] definitions = { CreateBond(1, float3.zero) };
            EarthBondState[] states = HealthyStates(1);
            EarthBondImpact[] impacts =
            {
                new EarthBondImpact(float3.zero, new float3(2f, 0f, 0f), 1f, 1f, 1)
            };

            EarthBondDamageResult result = EarthBondDamageSolver.ApplyBatch(
                impacts, 2, definitions, states, 1, null);

            Assert.That(result.Status, Is.EqualTo(EarthBondDamageStatus.CapacityExceeded));
            Assert.That(states[0].AccumulatedDamage, Is.Zero);
        }

        [Test]
        public void DamageAndIslandHotLoopsAllocateNoManagedMemoryAfterWarmup()
        {
            EarthPieceDefinition[] pieces = EarthBondGraphTests.CreatePieces(2);
            EarthPieceState[] pieceStates = { EarthPieceState.Intact, EarthPieceState.Intact };
            EarthBondDefinition[] definitions = { CreateBond(1, float3.zero) };
            EarthBondState[] bondStates = HealthyStates(1);
            var impact = new EarthBondImpact(
                float3.zero, new float3(0.1f, 0f, 0f), 1f, 1f, 3);
            var broken = new EarthBondId[1];
            var islandByPiece = new int[2];
            var supported = new bool[2];
            var counts = new int[2];
            var queue = new int[2];

            for (int index = 0; index < 32; index++)
            {
                bondStates[0] = EarthBondState.Healthy;
                EarthBondDamageSolver.ApplyImpact(
                    in impact, definitions, bondStates, 1, broken);
                EarthIslandSolver.Solve(
                    pieces, pieceStates, 2, definitions, bondStates, 1,
                    islandByPiece, supported, counts, queue);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1000; index++)
            {
                bondStates[0] = EarthBondState.Healthy;
                EarthBondDamageSolver.ApplyImpact(
                    in impact, definitions, bondStates, 1, broken);
                EarthIslandSolver.Solve(
                    pieces, pieceStates, 2, definitions, bondStates, 1,
                    islandByPiece, supported, counts, queue);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        private static float ApplyAndReadDamage(float3 impulse)
        {
            EarthBondDefinition[] definitions = { CreateBond(1, float3.zero) };
            EarthBondState[] states = HealthyStates(1);
            var impact = new EarthBondImpact(float3.zero, impulse, 1f, 1f, 4);
            EarthBondDamageSolver.ApplyImpact(in impact, definitions, states, 1, null);
            return states[0].AccumulatedDamage;
        }

        private static EarthBondDefinition CreateBond(ushort id, float3 centroid, float strength = 10f)
        {
            EarthBondDefinition definition = EarthBondGraphTests.CreateBond(id, 0, 1);
            definition.LocalCentroid = centroid;
            definition.TensileStrength = strength;
            definition.ShearStrength = strength;
            definition.CompressionStrength = strength * 4f;
            return definition;
        }

        private static EarthBondState[] HealthyStates(int count)
        {
            var states = new EarthBondState[count];
            for (int index = 0; index < count; index++)
                states[index] = EarthBondState.Healthy;
            return states;
        }
    }
}
