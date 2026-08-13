using System;

namespace Elemental.Simulation.Voxel
{
    public readonly struct MeshBuildRequest
    {
        public MeshBuildRequest(ChunkCoord coord, uint expectedVersion, int priority = 0)
        {
            Coord = coord;
            ExpectedVersion = expectedVersion;
            Priority = priority;
        }

        public ChunkCoord Coord { get; }
        public uint ExpectedVersion { get; }
        public int Priority { get; }
    }

    public readonly struct ColliderDebt
    {
        public ColliderDebt(
            ChunkCoord coord,
            uint visualVersion,
            uint colliderVersion,
            float ageSeconds,
            float playerProximity)
        {
            if (!float.IsFinite(ageSeconds) || ageSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(ageSeconds));
            }

            Coord = coord;
            VisualVersion = visualVersion;
            ColliderVersion = colliderVersion;
            AgeSeconds = ageSeconds;
            PlayerProximity = playerProximity;
        }

        public ChunkCoord Coord { get; }
        public uint VisualVersion { get; }
        public uint ColliderVersion { get; }
        public uint VersionDebt => VisualVersion >= ColliderVersion
            ? VisualVersion - ColliderVersion
            : 0u;
        public float AgeSeconds { get; }
        public float PlayerProximity { get; }
        public bool IsOutstanding => VersionDebt > 0u;
        public float RiskScore => IsOutstanding
            ? (VersionDebt * 10f) + AgeSeconds + (1f / Math.Max(PlayerProximity, 0.25f))
            : 0f;

        public bool IsWithin(uint maximumVersionDebt, float maximumAgeSeconds)
        {
            return VersionDebt <= maximumVersionDebt && AgeSeconds <= maximumAgeSeconds;
        }
    }
}
