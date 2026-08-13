using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthRepairOrderingTests
    {
        [Test]
        public void FoundationStartsDeterministicBreadthFirstOrder()
        {
            EarthPieceDefinition[] pieces = EarthBondGraphTests.CreatePieces(5);
            pieces[0].Flags |= EarthPieceFlags.Foundation;
            pieces[3].RestLocalPosition = new float3(1f, 2f, 0f);
            pieces[4].RestLocalPosition = new float3(1f, 1f, 0f);
            EarthPieceState[] states = DynamicStates(5);
            EarthBondDefinition[] bonds =
            {
                EarthBondGraphTests.CreateBond(1, 0, EarthBondGraph.WorldPieceIndex,
                    EarthBondFlags.Foundation | EarthBondFlags.Repairable),
                EarthBondGraphTests.CreateBond(2, 0, 1),
                EarthBondGraphTests.CreateBond(3, 0, 2),
                EarthBondGraphTests.CreateBond(4, 1, 3),
                EarthBondGraphTests.CreateBond(5, 1, 4)
            };
            bool[] available = { true, true, true, true, true };
            int[] order = new int[5];
            int[] depth = new int[5];
            bool[] visited = new bool[5];

            EarthRepairOrderResult result = EarthRepairOrdering.Build(
                pieces, states, 5, bonds, bonds.Length, available,
                EarthRepairAnchorMode.OriginalStructureFrame, order, depth, visited);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.AnchorPieceIndex, Is.EqualTo(0));
            Assert.That(order[0], Is.EqualTo(0));
            Assert.That(depth[0], Is.Zero);
            Assert.That(depth[1], Is.EqualTo(1));
            Assert.That(depth[2], Is.EqualTo(1));
            Assert.That(depth[3], Is.EqualTo(2));
            Assert.That(depth[4], Is.EqualTo(2));
            Assert.That(System.Array.IndexOf(order, 4), Is.LessThan(System.Array.IndexOf(order, 3)),
                "At equal graph depth the lower rest target should seat first.");
        }

        [Test]
        public void LargestIslandAnchorAndMissingGapRemainPartialWithoutInventedMass()
        {
            EarthPieceDefinition[] pieces = EarthBondGraphTests.CreatePieces(6);
            EarthPieceState[] states = DynamicStates(6);
            states[0].IslandIndex = 0;
            states[1].IslandIndex = 0;
            states[2].IslandIndex = 1;
            states[3].IslandIndex = 1;
            states[4].IslandIndex = 1;
            states[5].Phase = EarthPiecePhase.Missing;
            states[5].IslandIndex = -1;
            EarthBondDefinition[] bonds =
            {
                EarthBondGraphTests.CreateBond(1, 0, 1),
                EarthBondGraphTests.CreateBond(2, 2, 3),
                EarthBondGraphTests.CreateBond(3, 3, 4),
                EarthBondGraphTests.CreateBond(4, 4, 5)
            };
            bool[] available = { true, true, true, true, true, false };
            int[] order = new int[6];
            int[] depth = new int[6];
            bool[] visited = new bool[6];

            EarthRepairOrderResult result = EarthRepairOrdering.Build(
                pieces, states, 6, bonds, bonds.Length, available,
                EarthRepairAnchorMode.LargestSurvivingIsland, order, depth, visited);

            Assert.That(result.AnchorPieceIndex, Is.EqualTo(2));
            Assert.That(result.OrderedPieceCount, Is.EqualTo(5));
            Assert.That(result.MissingPieceCount, Is.EqualTo(1));
            Assert.That(result.SelectedMass, Is.EqualTo(10f));
            Assert.That(System.Array.IndexOf(order, 5), Is.EqualTo(-1));
        }

        [Test]
        public void SelectionRejectsForeignOwnedAndOverBudgetPieces()
        {
            EarthPieceDefinition piece = EarthBondGraphTests.CreatePieces(1)[0];
            EarthPieceState state = DynamicStates(1)[0];
            EarthStructureId requested = new EarthStructureId(7);

            Assert.That(EarthRepairOrdering.IsSelectable(
                requested, requested, in piece, in state, false, 0f, 10f), Is.True);
            Assert.That(EarthRepairOrdering.IsSelectable(
                requested, new EarthStructureId(8), in piece, in state, false, 0f, 10f), Is.False);
            Assert.That(EarthRepairOrdering.IsSelectable(
                requested, requested, in piece, in state, true, 0f, 10f), Is.False);
            Assert.That(EarthRepairOrdering.IsSelectable(
                requested, requested, in piece, in state, false, 9f, 10f), Is.False);
        }

        private static EarthPieceState[] DynamicStates(int count)
        {
            var states = new EarthPieceState[count];
            for (int index = 0; index < count; index++)
                states[index] = new EarthPieceState { Phase = EarthPiecePhase.Dynamic, IslandIndex = -1 };
            return states;
        }
    }
}
