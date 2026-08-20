using System;

namespace Elemental.Simulation.Voxel
{
    /// <summary>
    /// Stable acknowledgement token for an ordered SDF edit. A valid receipt only
    /// becomes committed after every affected render mesh and collider has reached
    /// at least the version captured when the edit was applied.
    /// </summary>
    public readonly struct VoxelEditReceipt : IEquatable<VoxelEditReceipt>
    {
        public VoxelEditReceipt(uint transactionId, uint firstSequence, uint lastSequence)
        {
            TransactionId = transactionId;
            FirstSequence = firstSequence;
            LastSequence = lastSequence;
        }

        public uint TransactionId { get; }
        public uint FirstSequence { get; }
        public uint LastSequence { get; }
        public bool IsValid => TransactionId != 0u && FirstSequence != 0u && LastSequence >= FirstSequence;

        public bool Equals(VoxelEditReceipt other) =>
            TransactionId == other.TransactionId && FirstSequence == other.FirstSequence &&
            LastSequence == other.LastSequence;

        public override bool Equals(object obj) => obj is VoxelEditReceipt other && Equals(other);
        public override int GetHashCode() => unchecked(((int)TransactionId * 397) ^ (int)LastSequence);
        public override string ToString() => IsValid
            ? $"VoxelEdit<{TransactionId}:{FirstSequence}-{LastSequence}>"
            : "VoxelEdit<invalid>";
    }
}
