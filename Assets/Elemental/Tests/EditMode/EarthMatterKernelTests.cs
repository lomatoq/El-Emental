using System;
using Elemental.Simulation.Matter;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMatterKernelTests
    {
        [Test]
        public void RegisterAndLegalLifecycle_PreserveIdentityVolumeAndMass()
        {
            var registry = new EarthMatterRegistry(64);
            EarthMatterRecord authored = Record(0.75f, 1800f * 0.75f);

            Assert.That(registry.TryRegister(authored, out EarthMatterId id), Is.True);
            Assert.That(registry.TryTransition(id, EarthMatterPhase.Controlled), Is.True);
            Assert.That(registry.TryTransition(id, EarthMatterPhase.CapturedForReturn), Is.True);
            Assert.That(registry.TryTransition(id, EarthMatterPhase.Returning), Is.True);
            Assert.That(registry.TryTransition(id, EarthMatterPhase.Reintegrating), Is.True);
            Assert.That(registry.TryTransition(id, EarthMatterPhase.Consumed), Is.True);
            Assert.That(registry.TryGet(id, out EarthMatterRecord final), Is.True);
            Assert.That(final.Volume, Is.EqualTo(0.75f));
            Assert.That(final.Mass, Is.EqualTo(1350f));
            Assert.That(final.Representation, Is.EqualTo(EarthRepresentationTier.DormantRecord));
        }

        [Test]
        public void IllegalPhaseJump_IsRejectedWithoutMutation()
        {
            var registry = new EarthMatterRegistry(64);
            Assert.That(registry.TryRegister(Record(1f, 1800f), out EarthMatterId id), Is.True);

            Assert.That(registry.TryTransition(id, EarthMatterPhase.Reintegrating), Is.False);
            Assert.That(registry.LastFailure, Is.EqualTo(EarthMatterRegistryFailure.IllegalPhaseTransition));
            Assert.That(registry.TryGet(id, out EarthMatterRecord record), Is.True);
            Assert.That(record.Phase, Is.EqualTo(EarthMatterPhase.Forming));
        }

        [Test]
        public void RecycledPoolHandle_InvalidatesPreviousGeneration()
        {
            var registry = new EarthMatterRegistry(64);
            Assert.That(registry.TryRegister(Record(1f, 1800f), out EarthMatterId oldId), Is.True);
            Assert.That(registry.TryTransition(oldId, EarthMatterPhase.Returning), Is.True);
            Assert.That(registry.TryTransition(oldId, EarthMatterPhase.Reintegrating), Is.True);
            Assert.That(registry.TryTransition(oldId, EarthMatterPhase.Consumed), Is.True);

            Assert.That(registry.TryRecycleConsumed(oldId, Record(0.3f, 540f), out EarthMatterId nextId), Is.True);

            Assert.That(nextId.StableId, Is.EqualTo(oldId.StableId));
            Assert.That(nextId.Generation, Is.EqualTo(oldId.Generation + 1));
            Assert.That(registry.TryGet(oldId, out _), Is.False);
            Assert.That(registry.TryGet(nextId, out EarthMatterRecord replacement), Is.True);
            Assert.That(replacement.Volume, Is.EqualTo(0.3f));
        }

        [Test]
        public void Register_ReusesDormantSlotsWithoutExhaustingTheBoundedRegistry()
        {
            var registry = new EarthMatterRegistry(32);
            EarthMatterId previous = default;
            for (int cycle = 0; cycle < 256; cycle++)
            {
                Assert.That(registry.TryRegister(Record(0.2f, 360f), out EarthMatterId current), Is.True,
                    $"cycle {cycle}: {registry.LastFailure}");
                if (previous.IsValid)
                    Assert.That(registry.TryGet(previous, out _), Is.False,
                        "A recycled slot must invalidate the previous generation handle.");
                Assert.That(registry.TryTransition(current, EarthMatterPhase.Returning), Is.True);
                Assert.That(registry.TryTransition(current, EarthMatterPhase.Reintegrating), Is.True);
                Assert.That(registry.TryTransition(current, EarthMatterPhase.Consumed), Is.True);
                previous = current;
            }

            Assert.That(registry.ActiveCount, Is.EqualTo(1),
                "Dormant records are storage slots, not an unbounded append-only allocation.");
        }

        [Test]
        public void LedgerDetectsThreePercentReturnGate()
        {
            EarthMatterRecord first = Record(1f, 1800f);
            first.Id = new EarthMatterId(1u, 1);
            first.Phase = EarthMatterPhase.Reintegrating;
            EarthMatterRecord second = Record(0.5f, 900f);
            second.Id = new EarthMatterId(2u, 1);
            second.Phase = EarthMatterPhase.Controlled;
            var records = new[] { first, second };

            EarthMatterLedgerSnapshot ledger = EarthMatterVolumeLedger.Calculate(records, records.Length);

            Assert.That(ledger.LiveVolume, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(ledger.ReintegratingVolume, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(EarthMatterVolumeLedger.RelativeVolumeError(1f, 0.971f), Is.LessThan(0.03f));
            Assert.That(EarthMatterVolumeLedger.RelativeVolumeError(1f, 0.969f), Is.GreaterThan(0.03f));
        }

        [Test]
        public void SteadyStateLookupAndKinematics_AreAllocationFree()
        {
            var registry = new EarthMatterRegistry(64);
            Assert.That(registry.TryRegister(Record(1f, 1800f), out EarthMatterId id), Is.True);
            var pose = new EarthMatterPose(new float3(1f, 2f, 3f), quaternion.identity);
            registry.TryGet(id, out _);
            registry.TrySetKinematics(id, pose, new float3(2f, 0f, 0f), float3.zero);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1000; index++)
            {
                registry.TryGet(id, out _);
                registry.TrySetKinematics(id, pose, new float3(2f, 0f, 0f), float3.zero);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(after - before, Is.Zero);
        }

        [Test]
        public void Split_IsAtomicAndConservesParentVolumeAndMass()
        {
            var registry = new EarthMatterRegistry(64);
            Assert.That(registry.TryRegister(Record(1f, 1800f), out EarthMatterId parent), Is.True);
            EarthMatterRecord left = Record(0.63f, 1134f);
            left.Phase = EarthMatterPhase.FreeDynamic;
            left.Source = new EarthSourceProvenance(
                EarthSourceKind.StructureCell, parent.StableId, parent.Generation, 0, 1u,
                float3.zero, 0.63f, EarthProvenanceFlags.VolumeReserved);
            EarthMatterRecord right = Record(0.37f, 666f);
            right.Phase = EarthMatterPhase.Sleeping;
            right.Source = new EarthSourceProvenance(
                EarthSourceKind.StructureCell, parent.StableId, parent.Generation, 1, 1u,
                float3.zero, 0.37f, EarthProvenanceFlags.VolumeReserved);
            var children = new[] { left, right };
            var ids = new EarthMatterId[2];

            Assert.That(registry.TrySplit(parent, children, 2, ids), Is.True);
            Assert.That(registry.TryGet(parent, out EarthMatterRecord parentRecord), Is.True);
            Assert.That(parentRecord.Phase, Is.EqualTo(EarthMatterPhase.Consumed));
            Assert.That(registry.TryGet(ids[0], out EarthMatterRecord first), Is.True);
            Assert.That(registry.TryGet(ids[1], out EarthMatterRecord second), Is.True);
            Assert.That(first.Volume + second.Volume, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(first.Mass + second.Mass, Is.EqualTo(1800f).Within(0.001f));
        }

        [Test]
        public void Merge_ConsumesChildrenAndRestoresParentWithNewGeneration()
        {
            var registry = new EarthMatterRegistry(64);
            Assert.That(registry.TryRegister(Record(1f, 1800f), out EarthMatterId parent), Is.True);
            EarthMatterRecord left = Record(0.4f, 720f);
            left.Phase = EarthMatterPhase.FreeDynamic;
            EarthMatterRecord right = Record(0.6f, 1080f);
            right.Phase = EarthMatterPhase.Sleeping;
            var children = new[] { left, right };
            var childIds = new EarthMatterId[2];
            Assert.That(registry.TrySplit(parent, children, 2, childIds), Is.True);
            EarthMatterRecord restored = Record(1f, 1800f);
            restored.Phase = EarthMatterPhase.Sleeping;

            Assert.That(registry.TryMerge(parent, childIds, 2, in restored, out EarthMatterId next), Is.True);
            Assert.That(next.StableId, Is.EqualTo(parent.StableId));
            Assert.That(next.Generation, Is.EqualTo(parent.Generation + 1));
            Assert.That(registry.TryGet(childIds[0], out EarthMatterRecord first), Is.True);
            Assert.That(registry.TryGet(childIds[1], out EarthMatterRecord second), Is.True);
            Assert.That(first.Phase, Is.EqualTo(EarthMatterPhase.Consumed));
            Assert.That(second.Phase, Is.EqualTo(EarthMatterPhase.Consumed));
            Assert.That(registry.TryGet(next, out EarthMatterRecord parentRestored), Is.True);
            Assert.That(parentRestored.Volume, Is.EqualTo(1f).Within(0.0001f));
        }

        private static EarthMatterRecord Record(float volume, float mass) => new EarthMatterRecord
        {
            Phase = EarthMatterPhase.Forming,
            Representation = EarthRepresentationTier.HeroPhysical,
            Material = EarthMaterialKind.Stone,
            Volume = volume,
            Mass = mass,
            Integrity = 1f,
            Source = new EarthSourceProvenance(
                EarthSourceKind.TerrainEdit,
                12u,
                1,
                -1,
                4u,
                float3.zero,
                volume,
                EarthProvenanceFlags.ExactReturnSupported |
                EarthProvenanceFlags.SourceCavityValid |
                EarthProvenanceFlags.VolumeReserved),
            Shape = EarthShapeSemantic.NaturalRock,
            RestPose = EarthMatterPose.Identity,
            CurrentPose = EarthMatterPose.Identity,
            LinearVelocity = float3.zero,
            AngularVelocity = float3.zero
        };
    }
}
