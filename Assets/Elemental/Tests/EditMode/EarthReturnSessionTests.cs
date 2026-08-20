using Elemental.Simulation.Matter;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthReturnSessionTests
    {
        [Test]
        public void DestinationResolver_PrefersExactProvenanceCavity()
        {
            EarthMatterRecord record = Record(new float3(2f, 7f, -1f), exact: true);

            EarthReturnDestination destination = EarthReturnDestinationResolver.Resolve(
                in record, new float3(9f), true, new float3(4f), true);

            Assert.That(destination.Kind, Is.EqualTo(EarthReturnDestinationKind.ProvenanceCavity));
            Assert.That(destination.PlanetLocalPoint, Is.EqualTo(new float3(2f, 7f, -1f)));
        }

        [Test]
        public void DestinationResolver_FallsBackInDocumentedPriorityOrder()
        {
            EarthMatterRecord record = Record(float3.zero, exact: false);
            EarthReturnDestination crater = EarthReturnDestinationResolver.Resolve(
                in record, new float3(1f, 2f, 3f), true, new float3(4f), true);
            EarthReturnDestination surface = EarthReturnDestinationResolver.Resolve(
                in record, float3.zero, false, new float3(5f, 6f, 7f), true);

            Assert.That(crater.Kind, Is.EqualTo(EarthReturnDestinationKind.SelectedCrater));
            Assert.That(surface.Kind, Is.EqualTo(EarthReturnDestinationKind.NearestStableSurface));
        }

        [Test]
        public void StableIdentity_ProducesDeterministicReturnCorridor()
        {
            EarthMatterRecord record = Record(new float3(0f, 8f, 0f), exact: true);
            var id = new EarthMatterId(47u, 3);
            var destination = new EarthReturnDestination(
                EarthReturnDestinationKind.ProvenanceCavity, record.Source.SourceLocalPoint);
            EarthReturnConfiguration configuration = EarthReturnConfiguration.Default;
            var first = new EarthReturnSession();
            var second = new EarthReturnSession();
            Assert.That(first.Begin(id, in record, new float3(4f, 9f, 1f), in destination, in configuration), Is.True);
            Assert.That(second.Begin(id, in record, new float3(4f, 9f, 1f), in destination, in configuration), Is.True);

            for (int tick = 0; tick < 20; tick++)
            {
                EarthReturnFrame a = first.Step(0.02f, new float3(4f, 9f, 1f), float3.zero);
                EarthReturnFrame b = second.Step(0.02f, new float3(4f, 9f, 1f), float3.zero);
                Assert.That(a.Target, Is.EqualTo(b.Target));
                Assert.That(a.Acceleration, Is.EqualTo(b.Acceleration));
            }
        }

        [Test]
        public void ReverseBeforeCommit_CancelsWithoutCanonicalMutation()
        {
            EarthMatterRecord record = Record(new float3(0f, 8f, 0f), exact: true);
            var destination = new EarthReturnDestination(
                EarthReturnDestinationKind.ProvenanceCavity, record.Source.SourceLocalPoint);
            var session = new EarthReturnSession();
            EarthReturnConfiguration configuration = EarthReturnConfiguration.Default;
            session.Begin(new EarthMatterId(3u, 1), in record, new float3(0f, 9f, 0f),
                in destination, in configuration);
            session.Step(0.02f, new float3(0f, 9f, 0f), float3.zero);

            Assert.That(session.ReverseBeforeCommit(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(EarthReturnPhase.Cancelled));
            Assert.That(session.PendingTransactionId, Is.Zero);
        }

        [Test]
        public void CommitReceiptMustMatchBeforeSessionCompletes()
        {
            EarthMatterRecord record = Record(new float3(0f, 8f, 0f), exact: true);
            var destination = new EarthReturnDestination(
                EarthReturnDestinationKind.ProvenanceCavity, record.Source.SourceLocalPoint);
            var session = new EarthReturnSession();
            EarthReturnConfiguration configuration = EarthReturnConfiguration.Default;
            session.Begin(new EarthMatterId(3u, 1), in record, destination.PlanetLocalPoint,
                in destination, in configuration);
            EarthReturnFrame frame = session.Step(0.4f, destination.PlanetLocalPoint, float3.zero);

            Assert.That(frame.RequestCommit, Is.True);
            Assert.That(session.MarkSdfCommitPending(17u), Is.True);
            Assert.That(session.ConfirmCommit(16u), Is.False);
            Assert.That(session.Phase, Is.EqualTo(EarthReturnPhase.SdfCommitPending));
            Assert.That(session.ConfirmCommit(17u), Is.True);
            Assert.That(session.Phase, Is.EqualTo(EarthReturnPhase.Completed));
        }

        [Test]
        public void PartialReturnBrushPreservesOnlyAvailableVolume()
        {
            const float reservedCavityVolume = 0.8f;
            const float availableVolume = 0.23f;
            float partialRadius = EarthReturnGeometry.SphereRadiusForVolume(availableVolume);
            float fullRadius = EarthReturnGeometry.SphereRadiusForVolume(reservedCavityVolume);
            float rematerialized = EarthReturnGeometry.SphereVolume(partialRadius);

            Assert.That(partialRadius, Is.LessThan(fullRadius));
            Assert.That(EarthMatterVolumeLedger.RelativeVolumeError(availableVolume, rematerialized),
                Is.LessThan(0.0001f));
            Assert.That(rematerialized, Is.LessThan(reservedCavityVolume * 0.30f),
                "Missing matter must remain visibly missing instead of being recreated to fill the source cavity.");
        }

        private static EarthMatterRecord Record(float3 source, bool exact)
        {
            EarthProvenanceFlags flags = EarthProvenanceFlags.VolumeReserved;
            if (exact) flags |= EarthProvenanceFlags.ExactReturnSupported | EarthProvenanceFlags.SourceCavityValid;
            return new EarthMatterRecord
            {
                Id = new EarthMatterId(1u, 1),
                Phase = EarthMatterPhase.FreeDynamic,
                Representation = EarthRepresentationTier.HeroPhysical,
                Material = EarthMaterialKind.Stone,
                Volume = 0.8f,
                Mass = 96f,
                Integrity = 1f,
                Source = new EarthSourceProvenance(
                    EarthSourceKind.TerrainEdit, 1u, 1, -1, 1u, source, 0.8f, flags),
                Shape = EarthShapeSemantic.NaturalRock,
                RestPose = EarthMatterPose.Identity,
                CurrentPose = EarthMatterPose.Identity,
                LinearVelocity = float3.zero,
                AngularVelocity = float3.zero
            };
        }
    }
}
