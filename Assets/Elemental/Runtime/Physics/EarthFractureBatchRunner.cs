using Elemental.Simulation.Structures;
using Unity.Profiling;

namespace Elemental.Runtime.Physics
{
    /// <summary>
    /// Profiled runtime boundary around the engine-independent fracture graph.
    /// Callers retain ownership of every fixed-capacity buffer.
    /// </summary>
    public static class EarthFractureBatchRunner
    {
        private static readonly ProfilerMarker DamageMarker =
            new ProfilerMarker("Elemental.Earth.Fracture.Damage");
        private static readonly ProfilerMarker IslandMarker =
            new ProfilerMarker("Elemental.Earth.Fracture.Islands");

        public static EarthBondDamageResult ApplyImpact(
            in EarthBondImpact impact,
            EarthBondDefinition[] definitions,
            EarthBondState[] states,
            int bondCount,
            EarthBondId[] brokenBondOutput)
        {
            using (DamageMarker.Auto())
            {
                return EarthBondDamageSolver.ApplyImpact(
                    in impact, definitions, states, bondCount, brokenBondOutput);
            }
        }

        public static EarthBondDamageResult ApplyBatch(
            EarthBondImpact[] impacts,
            int impactCount,
            EarthBondDefinition[] definitions,
            EarthBondState[] states,
            int bondCount,
            EarthBondId[] brokenBondOutput)
        {
            using (DamageMarker.Auto())
            {
                return EarthBondDamageSolver.ApplyBatch(
                    impacts, impactCount, definitions, states, bondCount, brokenBondOutput);
            }
        }

        public static EarthIslandSolveResult SolveIslands(
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
            using (IslandMarker.Auto())
            {
                return EarthIslandSolver.Solve(
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
            }
        }
    }
}
