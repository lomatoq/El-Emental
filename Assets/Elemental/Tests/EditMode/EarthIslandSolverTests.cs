using Elemental.Simulation.Structures;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthIslandSolverTests
    {
        [Test]
        public void BrokenBondCreatesSupportedAndDynamicIslands()
        {
            EarthPieceDefinition[] pieces = EarthBondGraphTests.CreatePieces(4);
            EarthPieceState[] pieceStates = IntactStates(4);
            EarthBondDefinition[] bonds =
            {
                EarthBondGraphTests.CreateBond(1, 0, EarthBondGraph.WorldPieceIndex, EarthBondFlags.Foundation),
                EarthBondGraphTests.CreateBond(2, 0, 1),
                EarthBondGraphTests.CreateBond(3, 1, 2),
                EarthBondGraphTests.CreateBond(4, 2, 3)
            };
            EarthBondState[] bondStates = HealthyStates(4);
            bondStates[2].Phase = EarthBondPhase.Broken;

            SolveBuffers buffers = new SolveBuffers(4);
            EarthIslandSolveResult result = EarthIslandSolver.Solve(
                pieces, pieceStates, 4, bonds, bondStates, 4,
                buffers.IslandByPiece, buffers.Supported, buffers.Counts, buffers.Queue);

            Assert.That(result.Status, Is.EqualTo(EarthIslandSolveStatus.Success));
            Assert.That(result.IslandCount, Is.EqualTo(2));
            Assert.That(result.SupportedIslandCount, Is.EqualTo(1));
            Assert.That(result.DynamicIslandCount, Is.EqualTo(1));
            Assert.That(buffers.IslandByPiece, Is.EqualTo(new[] { 0, 0, 1, 1 }));
            Assert.That(buffers.Supported[0], Is.True);
            Assert.That(buffers.Supported[1], Is.False);
            Assert.That(buffers.Counts[0], Is.EqualTo(2));
            Assert.That(buffers.Counts[1], Is.EqualTo(2));
            Assert.That(pieceStates[3].IslandIndex, Is.EqualTo(1));
        }

        [Test]
        public void ComponentIdsRemainStableWhenBondStorageOrderChanges()
        {
            EarthPieceDefinition[] pieces = EarthBondGraphTests.CreatePieces(5);
            EarthBondDefinition a = EarthBondGraphTests.CreateBond(1, 0, 1);
            EarthBondDefinition b = EarthBondGraphTests.CreateBond(2, 1, 2);
            EarthBondDefinition c = EarthBondGraphTests.CreateBond(3, 3, 4);
            EarthBondDefinition[] forward = { a, b, c };
            EarthBondDefinition[] reverse = { c, b, a };

            int[] first = SolveIslands(pieces, forward);
            int[] second = SolveIslands(pieces, reverse);

            Assert.That(first, Is.EqualTo(new[] { 0, 0, 0, 1, 1 }));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void MissingPiecesAreExcludedAndCannotBridgeComponents()
        {
            EarthPieceDefinition[] pieces = EarthBondGraphTests.CreatePieces(3);
            EarthPieceState[] states = IntactStates(3);
            states[1].Phase = EarthPiecePhase.Missing;
            EarthBondDefinition[] bonds =
            {
                EarthBondGraphTests.CreateBond(1, 0, EarthBondGraph.WorldPieceIndex, EarthBondFlags.Foundation),
                EarthBondGraphTests.CreateBond(2, 0, 1),
                EarthBondGraphTests.CreateBond(3, 1, 2)
            };
            SolveBuffers buffers = new SolveBuffers(3);

            EarthIslandSolveResult result = EarthIslandSolver.Solve(
                pieces, states, 3, bonds, HealthyStates(3), 3,
                buffers.IslandByPiece, buffers.Supported, buffers.Counts, buffers.Queue);

            Assert.That(result.MissingPieceCount, Is.EqualTo(1));
            Assert.That(result.IslandCount, Is.EqualTo(2));
            Assert.That(buffers.IslandByPiece, Is.EqualTo(new[] { 0, -1, 1 }));
            Assert.That(states[1].IslandIndex, Is.EqualTo(-1));
        }

        [Test]
        public void FoundationFlagSupportsAnIslandWithoutWorldBond()
        {
            EarthPieceDefinition[] pieces = EarthBondGraphTests.CreatePieces(2);
            pieces[1].Flags |= EarthPieceFlags.Foundation;
            EarthBondDefinition[] bonds = { EarthBondGraphTests.CreateBond(1, 0, 1) };
            SolveBuffers buffers = new SolveBuffers(2);

            EarthIslandSolveResult result = EarthIslandSolver.Solve(
                pieces, IntactStates(2), 2, bonds, HealthyStates(1), 1,
                buffers.IslandByPiece, buffers.Supported, buffers.Counts, buffers.Queue);

            Assert.That(result.SupportedIslandCount, Is.EqualTo(1));
            Assert.That(buffers.Supported[0], Is.True);
        }

        [TestCase(EarthBondPhase.Broken)]
        [TestCase(EarthBondPhase.Reforming)]
        public void NonStructuralBondPhasesDoNotConnectPieces(EarthBondPhase phase)
        {
            EarthPieceDefinition[] pieces = EarthBondGraphTests.CreatePieces(2);
            EarthBondDefinition[] bonds = { EarthBondGraphTests.CreateBond(1, 0, 1) };
            EarthBondState[] states = HealthyStates(1);
            states[0].Phase = phase;
            SolveBuffers buffers = new SolveBuffers(2);

            EarthIslandSolveResult result = EarthIslandSolver.Solve(
                pieces, IntactStates(2), 2, bonds, states, 1,
                buffers.IslandByPiece, buffers.Supported, buffers.Counts, buffers.Queue);

            Assert.That(result.IslandCount, Is.EqualTo(2));
        }

        [Test]
        public void InvalidBondIsCountedAndIgnoredWithoutBreakingTheSolve()
        {
            EarthPieceDefinition[] pieces = EarthBondGraphTests.CreatePieces(2);
            EarthBondDefinition[] bonds = { EarthBondGraphTests.CreateBond(1, 0, 9) };
            SolveBuffers buffers = new SolveBuffers(2);

            EarthIslandSolveResult result = EarthIslandSolver.Solve(
                pieces, IntactStates(2), 2, bonds, HealthyStates(1), 1,
                buffers.IslandByPiece, buffers.Supported, buffers.Counts, buffers.Queue);

            Assert.That(result.Status, Is.EqualTo(EarthIslandSolveStatus.Success));
            Assert.That(result.InvalidBondCount, Is.EqualTo(1));
            Assert.That(result.IslandCount, Is.EqualTo(2));
        }

        [Test]
        public void EmptyGraphAndUndersizedBuffersReturnBoundedResults()
        {
            EarthIslandSolveResult empty = EarthIslandSolver.Solve(
                new EarthPieceDefinition[0], new EarthPieceState[0], 0,
                new EarthBondDefinition[0], new EarthBondState[0], 0,
                new int[0], new bool[0], new int[0], new int[0]);
            EarthIslandSolveResult undersized = EarthIslandSolver.Solve(
                EarthBondGraphTests.CreatePieces(2), IntactStates(2), 2,
                new EarthBondDefinition[0], new EarthBondState[0], 0,
                new int[1], new bool[2], new int[2], new int[2]);

            Assert.That(empty.Status, Is.EqualTo(EarthIslandSolveStatus.Success));
            Assert.That(empty.IslandCount, Is.Zero);
            Assert.That(undersized.Status, Is.EqualTo(EarthIslandSolveStatus.CapacityExceeded));
        }

        private static int[] SolveIslands(EarthPieceDefinition[] pieces, EarthBondDefinition[] bonds)
        {
            SolveBuffers buffers = new SolveBuffers(pieces.Length);
            EarthIslandSolver.Solve(
                pieces, IntactStates(pieces.Length), pieces.Length,
                bonds, HealthyStates(bonds.Length), bonds.Length,
                buffers.IslandByPiece, buffers.Supported, buffers.Counts, buffers.Queue);
            return buffers.IslandByPiece;
        }

        private static EarthPieceState[] IntactStates(int count)
        {
            var states = new EarthPieceState[count];
            for (int index = 0; index < count; index++)
                states[index] = EarthPieceState.Intact;
            return states;
        }

        private static EarthBondState[] HealthyStates(int count)
        {
            var states = new EarthBondState[count];
            for (int index = 0; index < count; index++)
                states[index] = EarthBondState.Healthy;
            return states;
        }

        private sealed class SolveBuffers
        {
            public SolveBuffers(int count)
            {
                IslandByPiece = new int[count];
                Supported = new bool[count];
                Counts = new int[count];
                Queue = new int[count];
            }

            public int[] IslandByPiece { get; }
            public bool[] Supported { get; }
            public int[] Counts { get; }
            public int[] Queue { get; }
        }
    }
}
