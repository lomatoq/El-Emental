using Elemental.Runtime.Physics;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Voxel;
using UnityEngine;

namespace Elemental.Runtime.World
{
    public enum TerrainExtractionTransactionState : byte
    {
        Preparing = 0,
        VisualReady = 1,
        Committed = 2,
        Failed = 3
    }

    /// <summary>
    /// Couples a reserved physical fragment to the atomic terrain edit that owns it.
    /// The fragment remains non-rendering and non-physical until every affected render
    /// and collider chunk has been staged and committed by VoxelPlanetBehaviour.
    /// </summary>
    public sealed class TerrainExtractionTransaction
    {
        public TerrainExtractionTransaction(
            VoxelEditReceipt receipt,
            EarthFragment fragment,
            uint tick,
            AbilityId ability,
            Vector3 editCenter,
            Vector3 surfacePoint,
            Vector3 localUp,
            Vector3 emergencePosition,
            float radius,
            float mass)
        {
            Receipt = receipt;
            Fragment = fragment;
            Tick = tick;
            Ability = ability;
            EditCenter = editCenter;
            SurfacePoint = surfacePoint;
            LocalUp = localUp.sqrMagnitude > 0.0001f ? localUp.normalized : Vector3.up;
            EmergencePosition = emergencePosition;
            Radius = radius;
            Mass = mass;
            State = receipt.IsValid && fragment != null
                ? TerrainExtractionTransactionState.Preparing
                : TerrainExtractionTransactionState.Failed;
        }

        public VoxelEditReceipt Receipt { get; }
        public EarthFragment Fragment { get; }
        public uint Tick { get; }
        public AbilityId Ability { get; }
        public Vector3 EditCenter { get; }
        public Vector3 SurfacePoint { get; }
        public Vector3 LocalUp { get; }
        public Vector3 EmergencePosition { get; }
        public float Radius { get; }
        public float Mass { get; }
        public TerrainExtractionTransactionState State { get; private set; }
        public bool IsTerminal => State == TerrainExtractionTransactionState.Committed ||
                                  State == TerrainExtractionTransactionState.Failed;

        public bool MarkVisualReady(VoxelEditReceipt receipt)
        {
            if (State != TerrainExtractionTransactionState.Preparing ||
                !Receipt.Equals(receipt)) return false;
            State = TerrainExtractionTransactionState.VisualReady;
            return true;
        }

        public bool MarkCommitted()
        {
            if (State != TerrainExtractionTransactionState.VisualReady) return false;
            State = TerrainExtractionTransactionState.Committed;
            return true;
        }

        public void MarkFailed()
        {
            if (!IsTerminal) State = TerrainExtractionTransactionState.Failed;
        }
    }
}
