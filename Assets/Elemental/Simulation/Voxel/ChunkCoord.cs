using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Voxel
{
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public ChunkCoord(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public float3 GetPlanetLocalMin(float chunkWorldSize)
        {
            return new float3(X * chunkWorldSize, Y * chunkWorldSize, Z * chunkWorldSize);
        }

        public static ChunkCoord FromPlanetLocal(float3 position, float chunkWorldSize)
        {
            return new ChunkCoord(
                (int)math.floor(position.x / chunkWorldSize),
                (int)math.floor(position.y / chunkWorldSize),
                (int)math.floor(position.z / chunkWorldSize));
        }

        public bool Equals(ChunkCoord other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X * 73856093;
                hash ^= Y * 19349663;
                hash ^= Z * 83492791;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }

        public static bool operator ==(ChunkCoord left, ChunkCoord right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChunkCoord left, ChunkCoord right)
        {
            return !left.Equals(right);
        }
    }
}
