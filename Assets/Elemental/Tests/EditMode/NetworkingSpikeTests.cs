using System.Collections.Generic;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Networking;
using Elemental.Simulation.Voxel;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class NetworkingSpikeTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void TwoToFourClientsConvergeUnderLatencyJitterAndLoss(int clients)
        {
            var profile = new TransportProfile(6, 2, 0.08f, 256);
            var harness = new OnlineSpikeHarness(clients, in profile, 1234u);
            for (uint tick = 0; tick < 1800; tick++) harness.Tick(tick);
            for (uint tick = 1800; tick < 1830; tick++) harness.Tick(tick);
            Assert.That(harness.ClientCount, Is.EqualTo(clients));
            Assert.That(harness.SubmittedCount, Is.GreaterThan(100));
            Assert.That(harness.Authority.AcceptedCount, Is.GreaterThan(90));
            Assert.That(harness.Authority.DecisionCount, Is.EqualTo(harness.Authority.AcceptedCount));
            Assert.That(harness.QueueDebt, Is.LessThan(16));
            Assert.That(harness.DroppedCount, Is.GreaterThan(0));
            for (int index = 0; index < harness.Authority.DecisionCount; index++)
                Assert.That(harness.Authority.GetDecision(index).Kind, Is.Not.EqualTo(CommandDecisionKind.Rejected));
        }

        [Test]
        public void AuthorityRejectsSpoofedOwnershipAndUnboundedTime()
        {
            var authority = new CommandAuthority();
            MagicCommand spoofed = Command(10u, 2u);
            CommandDecision ownership = authority.Submit(new NetworkPeerId(1), in spoofed, 10u);
            Assert.That(ownership.Kind, Is.EqualTo(CommandDecisionKind.Rejected));
            MagicCommand old = Command(1u, 1u);
            CommandDecision time = authority.Submit(new NetworkPeerId(1), in old, 500u);
            Assert.That(time.Kind, Is.EqualTo(CommandDecisionKind.Rejected));
        }

        [Test]
        public void TerrainEditsRequireMonotonicAuthorityOrderAndCompactPayload()
        {
            var authority = new CommandAuthority();
            SdfEdit first = new SdfEdit(1u, SdfEditKind.AddSphere, float3.zero, float3.zero, 1f, new VoxelMaterialId(1));
            SdfEdit duplicate = new SdfEdit(1u, SdfEditKind.SubtractSphere, float3.zero, float3.zero, 1f, new VoxelMaterialId(1));
            Assert.That(authority.ReplicateTerrain(10u, in first, 2u, 123ul), Is.True);
            Assert.That(authority.ReplicateTerrain(11u, in duplicate, 3u, 456ul), Is.False);
            Assert.That(authority.TerrainEditCount, Is.EqualTo(1));
            TerrainEditReplication payload = authority.GetTerrainEdit(0);
            Assert.That(payload.Edit.Sequence, Is.EqualTo(1u));
            Assert.That(payload.ChunkHash, Is.EqualTo(123ul));
        }

        [Test]
        public void PredictionCorrectionIsSoftThenSnapsAtBound()
        {
            var authority = new RigidbodySnapshot(
                1u, 10u, new float3(1f, 0f, 0f), quaternion.identity,
                new float3(2f, 0f, 0f), float3.zero, 200);
            CorrectionResult soft = PredictionReconciler.Reconcile(float3.zero, float3.zero, in authority);
            Assert.That(soft.Snapped, Is.False);
            Assert.That(soft.Position.x, Is.GreaterThan(0f).And.LessThan(1f));
            var far = new RigidbodySnapshot(
                1u, 11u, new float3(8f, 0f, 0f), quaternion.identity,
                new float3(2f, 0f, 0f), float3.zero, 200);
            CorrectionResult snap = PredictionReconciler.Reconcile(float3.zero, float3.zero, in far);
            Assert.That(snap.Snapped, Is.True);
            Assert.That(snap.Position, Is.EqualTo(far.Position));
        }

        [Test]
        public void RelevanceIncludesPlanetSideObjectiveAndImpendingCollision()
        {
            var distantThreat = new RelevanceFacts(150f, false, false, false, true);
            var nearbyCosmetic = new RelevanceFacts(10f, true, true, false, false);
            Assert.That(RelevanceScorer.Score(in distantThreat), Is.GreaterThan(RelevanceScorer.Score(in nearbyCosmetic)));
        }

        private static MagicCommand Command(uint tick, uint caster)
        {
            return new MagicCommand(
                tick, caster, ElementId.Earth, new AbilityId(1), float3.zero,
                new float3(1f, 0f, 0f), new List<float3> { float3.zero, new float3(1f, 0f, 0f) },
                0.5f, 0u, 1u);
        }
    }
}
