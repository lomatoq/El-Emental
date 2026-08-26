using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Voxel;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class TerrainExtractionTransactionTests
    {
        [Test]
        public void TransactionRequiresMatchingVisualCommitBeforePhysicalCommit()
        {
            GameObject fragmentObject = new GameObject("Reserved extraction test");
            fragmentObject.AddComponent<Rigidbody>();
            EarthFragment fragment = fragmentObject.AddComponent<EarthFragment>();
            var receipt = new VoxelEditReceipt(17u, 3u, 3u);
            var transaction = new TerrainExtractionTransaction(
                receipt,
                fragment,
                44u,
                EarthAbilityIds.PullRock,
                Vector3.up * 8f,
                Vector3.up * 8.4f,
                Vector3.up,
                Vector3.up * 8.6f,
                0.45f,
                22f);

            Assert.That(transaction.State, Is.EqualTo(TerrainExtractionTransactionState.Preparing));
            Assert.That(transaction.MarkVisualReady(new VoxelEditReceipt(18u, 3u, 3u)), Is.False);
            Assert.That(transaction.MarkCommitted(), Is.False);
            Assert.That(transaction.MarkVisualReady(receipt), Is.True);
            Assert.That(transaction.State, Is.EqualTo(TerrainExtractionTransactionState.VisualReady));
            Assert.That(transaction.MarkCommitted(), Is.True);
            Assert.That(transaction.State, Is.EqualTo(TerrainExtractionTransactionState.Committed));

            Object.DestroyImmediate(fragmentObject);
        }

        [Test]
        public void MissingReservationFailsBeforeTerrainCanCommit()
        {
            var transaction = new TerrainExtractionTransaction(
                new VoxelEditReceipt(9u, 1u, 1u),
                null,
                1u,
                EarthAbilityIds.PullRock,
                Vector3.zero,
                Vector3.up,
                Vector3.up,
                Vector3.up,
                0.4f,
                10f);

            Assert.That(transaction.State, Is.EqualTo(TerrainExtractionTransactionState.Failed));
            Assert.That(transaction.IsTerminal, Is.True);
        }
    }
}
