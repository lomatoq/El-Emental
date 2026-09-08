using System;
using Elemental.Simulation.Voxel;

namespace Elemental.Runtime.World
{
    public sealed partial class VoxelPlanetBehaviour
    {
        public bool HasOnlineSimulationAuthority { get; private set; } = true;
        private bool _applyingOnlineCanonical;
        public event Action<EditBatch, bool> OnlineEditBatchApplied;
        public void ConfigureOnlineTerrainAuthority(bool authority) => HasOnlineSimulationAuthority = authority;
        public void ApplyOnlineCanonicalBatch(EditBatch batch, bool transactional)
        {
            if (HasOnlineSimulationAuthority) throw new InvalidOperationException("Authority cannot consume a replica terrain batch.");
            _applyingOnlineCanonical = true;
            try
            {
                if (transactional) ApplyEditBatchTransactional(batch); else ApplyEditBatch(batch);
                if (batch.Count > 0) _nextEditSequence = Math.Max(_nextEditSequence, batch[batch.Count - 1].Sequence + 1);
            }
            finally { _applyingOnlineCanonical = false; }
        }
        private void PublishOnlineBatch(EditBatch batch, bool transactional)
        { if (HasOnlineSimulationAuthority) OnlineEditBatchApplied?.Invoke(batch, transactional); }
    }
}
