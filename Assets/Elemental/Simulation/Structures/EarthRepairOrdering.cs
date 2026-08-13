using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public static class EarthRepairOrdering
    {
        public static bool IsSelectable(
            EarthStructureId requestedStructure,
            EarthStructureId candidateStructure,
            in EarthPieceDefinition definition,
            in EarthPieceState state,
            bool hasConflictingOwner,
            float selectedMass,
            float massLimit)
        {
            if (!requestedStructure.IsValid || requestedStructure != candidateStructure || hasConflictingOwner)
                return false;
            if ((definition.Flags & EarthPieceFlags.Repairable) == 0 || state.Phase == EarthPiecePhase.Missing)
                return false;
            float nextMass = selectedMass + math.max(0f, definition.Mass);
            return math.isfinite(nextMass) && nextMass <= math.max(0f, massLimit);
        }

        public static EarthRepairOrderResult Build(
            EarthPieceDefinition[] pieces,
            EarthPieceState[] pieceStates,
            int pieceCount,
            EarthBondDefinition[] bonds,
            int bondCount,
            bool[] available,
            EarthRepairAnchorMode anchorMode,
            int[] outputOrder,
            int[] graphDepth,
            bool[] visited)
        {
            if (pieces == null || pieceStates == null || bonds == null || available == null ||
                outputOrder == null || graphDepth == null || visited == null)
            {
                return new EarthRepairOrderResult(
                    EarthRepairOrderStatus.InvalidStorage, -1, 0, 0, 0f);
            }
            if (pieceCount < 0 || bondCount < 0 ||
                pieceCount > EarthBondGraph.MaxPieceCount || bondCount > EarthBondGraph.MaxBondCount ||
                pieceCount > pieces.Length || pieceCount > pieceStates.Length ||
                pieceCount > available.Length || pieceCount > outputOrder.Length ||
                pieceCount > graphDepth.Length || pieceCount > visited.Length || bondCount > bonds.Length)
            {
                return new EarthRepairOrderResult(
                    EarthRepairOrderStatus.CapacityExceeded, -1, 0, 0, 0f);
            }

            int missing = 0;
            float selectedMass = 0f;
            for (int index = 0; index < pieceCount; index++)
            {
                outputOrder[index] = -1;
                graphDepth[index] = -1;
                visited[index] = false;
                if (!available[index] || pieceStates[index].Phase == EarthPiecePhase.Missing)
                    missing++;
                else
                    selectedMass += math.max(0f, pieces[index].Mass);
            }

            int anchor = SelectAnchor(pieces, pieceStates, pieceCount, available, anchorMode);
            if (anchor < 0)
            {
                return new EarthRepairOrderResult(
                    EarthRepairOrderStatus.NoRepairablePieces, -1, 0, missing, selectedMass);
            }

            int written = 0;
            Write(anchor, 0, outputOrder, graphDepth, visited, ref written);
            while (written < pieceCount - missing)
            {
                int best = -1;
                int bestDepth = int.MaxValue;
                int bestRepairedNeighbors = -1;
                float bestContact = -1f;
                for (int candidate = 0; candidate < pieceCount; candidate++)
                {
                    if (visited[candidate] || !IsAvailableRepairable(candidate, pieces, pieceStates, available))
                        continue;

                    int candidateDepth = int.MaxValue;
                    int repairedNeighbors = 0;
                    float contact = 0f;
                    for (int bondIndex = 0; bondIndex < bondCount; bondIndex++)
                    {
                        EarthBondDefinition bond = bonds[bondIndex];
                        if ((bond.Flags & EarthBondFlags.Repairable) == 0) continue;
                        int neighbor = NeighborOf(in bond, candidate);
                        if (neighbor == EarthBondGraph.WorldPieceIndex &&
                            (bond.Flags & EarthBondFlags.Foundation) != 0)
                        {
                            candidateDepth = math.min(candidateDepth, 0);
                            repairedNeighbors++;
                            contact += bond.ContactArea;
                        }
                        else if (neighbor >= 0 && neighbor < pieceCount && visited[neighbor])
                        {
                            candidateDepth = math.min(candidateDepth, graphDepth[neighbor] + 1);
                            repairedNeighbors++;
                            contact += bond.ContactArea;
                        }
                    }
                    if (candidateDepth == int.MaxValue) continue;
                    if (IsBetter(
                            candidate, candidateDepth, repairedNeighbors, contact,
                            best, bestDepth, bestRepairedNeighbors, bestContact, pieces))
                    {
                        best = candidate;
                        bestDepth = candidateDepth;
                        bestRepairedNeighbors = repairedNeighbors;
                        bestContact = contact;
                    }
                }

                if (best < 0)
                {
                    // Missing pieces can split the repair graph. Preserve mass and
                    // continue from a deterministic secondary root instead of inventing a bridge.
                    for (int candidate = 0; candidate < pieceCount; candidate++)
                    {
                        if (visited[candidate] || !IsAvailableRepairable(candidate, pieces, pieceStates, available))
                            continue;
                        if (best < 0 || StablePieceLess(candidate, best, pieces)) best = candidate;
                    }
                    if (best < 0) break;
                    bestDepth = 0;
                }
                Write(best, bestDepth, outputOrder, graphDepth, visited, ref written);
            }

            return new EarthRepairOrderResult(
                EarthRepairOrderStatus.Success, anchor, written, missing, selectedMass);
        }

        private static int SelectAnchor(
            EarthPieceDefinition[] pieces,
            EarthPieceState[] states,
            int count,
            bool[] available,
            EarthRepairAnchorMode mode)
        {
            int best = -1;
            if (mode == EarthRepairAnchorMode.LargestSurvivingIsland)
            {
                int bestIsland = -1;
                int bestCount = 0;
                for (int candidate = 0; candidate < count; candidate++)
                {
                    if (!IsAvailableRepairable(candidate, pieces, states, available)) continue;
                    int island = states[candidate].IslandIndex;
                    int islandCount = 0;
                    for (int other = 0; other < count; other++)
                        if (IsAvailableRepairable(other, pieces, states, available) &&
                            states[other].IslandIndex == island) islandCount++;
                    if (islandCount > bestCount || (islandCount == bestCount && island < bestIsland))
                    {
                        bestIsland = island;
                        bestCount = islandCount;
                    }
                }
                for (int candidate = 0; candidate < count; candidate++)
                {
                    if (!IsAvailableRepairable(candidate, pieces, states, available) ||
                        states[candidate].IslandIndex != bestIsland) continue;
                    if (best < 0 || StablePieceLess(candidate, best, pieces)) best = candidate;
                }
                if (best >= 0) return best;
            }

            for (int candidate = 0; candidate < count; candidate++)
            {
                if (!IsAvailableRepairable(candidate, pieces, states, available) ||
                    (pieces[candidate].Flags & EarthPieceFlags.Foundation) == 0) continue;
                if (best < 0 || StablePieceLess(candidate, best, pieces)) best = candidate;
            }
            if (best >= 0) return best;
            for (int candidate = 0; candidate < count; candidate++)
            {
                if (!IsAvailableRepairable(candidate, pieces, states, available)) continue;
                if (best < 0 || StablePieceLess(candidate, best, pieces)) best = candidate;
            }
            return best;
        }

        private static bool IsBetter(
            int candidate,
            int candidateDepth,
            int candidateNeighbors,
            float candidateContact,
            int best,
            int bestDepth,
            int bestNeighbors,
            float bestContact,
            EarthPieceDefinition[] pieces)
        {
            if (best < 0 || candidateDepth < bestDepth) return true;
            if (candidateDepth > bestDepth) return false;
            if (candidateNeighbors != bestNeighbors) return candidateNeighbors > bestNeighbors;
            float candidateHeight = pieces[candidate].RestLocalPosition.y;
            float bestHeight = pieces[best].RestLocalPosition.y;
            if (math.abs(candidateHeight - bestHeight) > 0.00001f) return candidateHeight < bestHeight;
            if (math.abs(candidateContact - bestContact) > 0.00001f) return candidateContact > bestContact;
            if (math.abs(pieces[candidate].Volume - pieces[best].Volume) > 0.00001f)
                return pieces[candidate].Volume > pieces[best].Volume;
            return pieces[candidate].Id.Value < pieces[best].Id.Value;
        }

        private static bool StablePieceLess(int left, int right, EarthPieceDefinition[] pieces)
        {
            float leftHeight = pieces[left].RestLocalPosition.y;
            float rightHeight = pieces[right].RestLocalPosition.y;
            if (math.abs(leftHeight - rightHeight) > 0.00001f) return leftHeight < rightHeight;
            if (math.abs(pieces[left].Volume - pieces[right].Volume) > 0.00001f)
                return pieces[left].Volume > pieces[right].Volume;
            return pieces[left].Id.Value < pieces[right].Id.Value;
        }

        private static int NeighborOf(in EarthBondDefinition bond, int piece)
        {
            if (bond.PieceA == piece) return bond.PieceB;
            if (bond.PieceB == piece) return bond.PieceA;
            return int.MinValue;
        }

        private static bool IsAvailableRepairable(
            int index,
            EarthPieceDefinition[] pieces,
            EarthPieceState[] states,
            bool[] available)
        {
            return available[index] && states[index].Phase != EarthPiecePhase.Missing &&
                   (pieces[index].Flags & EarthPieceFlags.Repairable) != 0;
        }

        private static void Write(
            int piece,
            int depth,
            int[] outputOrder,
            int[] graphDepth,
            bool[] visited,
            ref int written)
        {
            visited[piece] = true;
            graphDepth[piece] = depth;
            outputOrder[written++] = piece;
        }
    }
}
