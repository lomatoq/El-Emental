namespace Elemental.Simulation.Structures
{
    public enum EarthIslandSolveStatus : byte
    {
        Success,
        InvalidStorage,
        CapacityExceeded
    }

    public readonly struct EarthIslandSolveResult
    {
        public EarthIslandSolveResult(
            EarthIslandSolveStatus status,
            int islandCount,
            int supportedIslandCount,
            int dynamicIslandCount,
            int missingPieceCount,
            int invalidBondCount)
        {
            Status = status;
            IslandCount = islandCount;
            SupportedIslandCount = supportedIslandCount;
            DynamicIslandCount = dynamicIslandCount;
            MissingPieceCount = missingPieceCount;
            InvalidBondCount = invalidBondCount;
        }

        public EarthIslandSolveStatus Status { get; }
        public int IslandCount { get; }
        public int SupportedIslandCount { get; }
        public int DynamicIslandCount { get; }
        public int MissingPieceCount { get; }
        public int InvalidBondCount { get; }
    }

    public static class EarthIslandSolver
    {
        public static EarthIslandSolveResult Solve(
            EarthPieceDefinition[] pieceDefinitions,
            EarthPieceState[] pieceStates,
            int pieceCount,
            EarthBondDefinition[] bondDefinitions,
            EarthBondState[] bondStates,
            int bondCount,
            int[] islandByPiece,
            bool[] islandSupported,
            int[] islandPieceCounts,
            int[] traversalQueue)
        {
            EarthIslandSolveStatus status = ValidateStorage(
                pieceDefinitions,
                pieceStates,
                pieceCount,
                bondDefinitions,
                bondStates,
                bondCount,
                islandByPiece,
                islandSupported,
                islandPieceCounts,
                traversalQueue);
            if (status != EarthIslandSolveStatus.Success)
                return new EarthIslandSolveResult(status, 0, 0, 0, 0, 0);

            int missingPieceCount = 0;
            for (int pieceIndex = 0; pieceIndex < pieceCount; pieceIndex++)
            {
                islandByPiece[pieceIndex] = -1;
                EarthPieceState state = pieceStates[pieceIndex];
                state.IslandIndex = -1;
                pieceStates[pieceIndex] = state;
                if (state.Phase == EarthPiecePhase.Missing)
                    missingPieceCount++;
                islandSupported[pieceIndex] = false;
                islandPieceCounts[pieceIndex] = 0;
            }

            int invalidBondCount = CountInvalidBonds(pieceCount, bondDefinitions, bondCount);
            int islandCount = 0;
            for (int seedPiece = 0; seedPiece < pieceCount; seedPiece++)
            {
                if (pieceStates[seedPiece].Phase == EarthPiecePhase.Missing ||
                    islandByPiece[seedPiece] >= 0)
                {
                    continue;
                }

                int islandIndex = islandCount++;
                int read = 0;
                int write = 0;
                traversalQueue[write++] = seedPiece;
                islandByPiece[seedPiece] = islandIndex;

                while (read < write)
                {
                    int pieceIndex = traversalQueue[read++];
                    islandPieceCounts[islandIndex]++;
                    if ((pieceDefinitions[pieceIndex].Flags & EarthPieceFlags.Foundation) != 0)
                        islandSupported[islandIndex] = true;

                    for (int bondIndex = 0; bondIndex < bondCount; bondIndex++)
                    {
                        EarthBondDefinition bond = bondDefinitions[bondIndex];
                        if (!IsEndpointRangeValid(bond, pieceCount) ||
                            !EarthBondGraph.IsStructuralConnection(bondStates[bondIndex].Phase))
                        {
                            continue;
                        }

                        if (bond.PieceA == pieceIndex && bond.PieceB == EarthBondGraph.WorldPieceIndex)
                        {
                            islandSupported[islandIndex] = true;
                            continue;
                        }

                        int neighbor = -1;
                        if (bond.PieceA == pieceIndex)
                            neighbor = bond.PieceB;
                        else if (bond.PieceB == pieceIndex)
                            neighbor = bond.PieceA;

                        if (neighbor < 0 || pieceStates[neighbor].Phase == EarthPiecePhase.Missing ||
                            islandByPiece[neighbor] >= 0)
                        {
                            continue;
                        }

                        islandByPiece[neighbor] = islandIndex;
                        traversalQueue[write++] = neighbor;
                    }
                }
            }

            int supportedCount = 0;
            for (int islandIndex = 0; islandIndex < islandCount; islandIndex++)
            {
                if (islandSupported[islandIndex])
                    supportedCount++;
            }

            for (int pieceIndex = 0; pieceIndex < pieceCount; pieceIndex++)
            {
                EarthPieceState state = pieceStates[pieceIndex];
                state.IslandIndex = (short)islandByPiece[pieceIndex];
                pieceStates[pieceIndex] = state;
            }

            return new EarthIslandSolveResult(
                EarthIslandSolveStatus.Success,
                islandCount,
                supportedCount,
                islandCount - supportedCount,
                missingPieceCount,
                invalidBondCount);
        }

        private static EarthIslandSolveStatus ValidateStorage(
            EarthPieceDefinition[] pieceDefinitions,
            EarthPieceState[] pieceStates,
            int pieceCount,
            EarthBondDefinition[] bondDefinitions,
            EarthBondState[] bondStates,
            int bondCount,
            int[] islandByPiece,
            bool[] islandSupported,
            int[] islandPieceCounts,
            int[] traversalQueue)
        {
            if (pieceDefinitions == null || pieceStates == null || bondDefinitions == null || bondStates == null ||
                islandByPiece == null || islandSupported == null || islandPieceCounts == null || traversalQueue == null)
            {
                return EarthIslandSolveStatus.InvalidStorage;
            }

            if (pieceCount < 0 || bondCount < 0 ||
                pieceCount > EarthBondGraph.MaxPieceCount || bondCount > EarthBondGraph.MaxBondCount ||
                pieceCount > pieceDefinitions.Length || pieceCount > pieceStates.Length ||
                bondCount > bondDefinitions.Length || bondCount > bondStates.Length ||
                pieceCount > islandByPiece.Length || pieceCount > islandSupported.Length ||
                pieceCount > islandPieceCounts.Length || pieceCount > traversalQueue.Length)
            {
                return EarthIslandSolveStatus.CapacityExceeded;
            }

            return EarthIslandSolveStatus.Success;
        }

        private static int CountInvalidBonds(
            int pieceCount,
            EarthBondDefinition[] bondDefinitions,
            int bondCount)
        {
            int invalid = 0;
            for (int bondIndex = 0; bondIndex < bondCount; bondIndex++)
            {
                if (!IsEndpointRangeValid(bondDefinitions[bondIndex], pieceCount))
                    invalid++;
            }
            return invalid;
        }

        private static bool IsEndpointRangeValid(in EarthBondDefinition bond, int pieceCount)
        {
            return bond.PieceA >= 0 && bond.PieceA < pieceCount &&
                   bond.PieceB >= EarthBondGraph.WorldPieceIndex && bond.PieceB < pieceCount &&
                   bond.PieceA != bond.PieceB;
        }
    }
}
