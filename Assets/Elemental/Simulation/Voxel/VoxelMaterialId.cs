using System;

namespace Elemental.Simulation.Voxel
{
    public readonly struct VoxelMaterialId : IEquatable<VoxelMaterialId>
    {
        public VoxelMaterialId(byte value)
        {
            Value = value;
        }

        public byte Value { get; }
        public bool IsEmpty => Value == 0;

        public bool Equals(VoxelMaterialId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is VoxelMaterialId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(VoxelMaterialId left, VoxelMaterialId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VoxelMaterialId left, VoxelMaterialId right)
        {
            return !left.Equals(right);
        }
    }
}
