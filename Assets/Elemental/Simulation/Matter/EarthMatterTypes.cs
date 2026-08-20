using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Matter
{
    public enum EarthMatterPhase : byte
    {
        TerrainAttached = 0,
        Forming = 1,
        Controlled = 2,
        FreeDynamic = 3,
        Sleeping = 4,
        CapturedForReturn = 5,
        Returning = 6,
        Reintegrating = 7,
        Consumed = 8
    }

    public enum EarthRepresentationTier : byte
    {
        CanonicalTerrain = 0,
        HeroPhysical = 1,
        SecondaryPhysical = 2,
        VisualOnlyGpu = 3,
        DormantRecord = 4
    }

    public enum EarthMaterialKind : byte
    {
        Unknown = 0,
        Stone = 1,
        Soil = 2,
        Clay = 3,
        Sand = 4,
        MetalOre = 5,
        Crystal = 6
    }

    public enum EarthSourceKind : byte
    {
        None = 0,
        TerrainEdit = 1,
        StructureCell = 2,
        Fragment = 3,
        Mixed = 4
    }

    public enum EarthShapeSemantic : byte
    {
        Unspecified = 0,
        NaturalRock = 1,
        Slab = 2,
        WallCell = 3,
        PlatformCell = 4,
        Pillar = 5,
        ArmorPlate = 6,
        Spear = 7,
        Drill = 8,
        Debris = 9,
        Wedge = 10
    }

    [Flags]
    public enum EarthProvenanceFlags : byte
    {
        None = 0,
        ExactReturnSupported = 1 << 0,
        SourceCavityValid = 1 << 1,
        SourceStructureAlive = 1 << 2,
        VolumeReserved = 1 << 3
    }

    public readonly struct EarthMatterId : IEquatable<EarthMatterId>
    {
        public EarthMatterId(uint stableId, ushort generation)
        {
            StableId = stableId;
            Generation = generation;
        }

        public uint StableId { get; }
        public ushort Generation { get; }
        public bool IsValid => StableId != 0u && Generation != 0;
        public bool Equals(EarthMatterId other) => StableId == other.StableId && Generation == other.Generation;
        public override bool Equals(object obj) => obj is EarthMatterId other && Equals(other);
        public override int GetHashCode() => unchecked(((int)StableId * 397) ^ Generation);
        public override string ToString() => IsValid ? $"M{StableId}:{Generation}" : "Matter<invalid>";
        public static bool operator ==(EarthMatterId left, EarthMatterId right) => left.Equals(right);
        public static bool operator !=(EarthMatterId left, EarthMatterId right) => !left.Equals(right);
    }

    public readonly struct EarthOwnerId : IEquatable<EarthOwnerId>
    {
        public EarthOwnerId(uint stableId, ushort generation)
        {
            StableId = stableId;
            Generation = generation;
        }
        public uint StableId { get; }
        public ushort Generation { get; }
        public bool IsValid => StableId != 0u && Generation != 0;
        public bool Equals(EarthOwnerId other) => StableId == other.StableId && Generation == other.Generation;
        public override bool Equals(object obj) => obj is EarthOwnerId other && Equals(other);
        public override int GetHashCode() => unchecked(((int)StableId * 397) ^ Generation);
    }

    public readonly struct EarthMatterPose
    {
        public EarthMatterPose(float3 position, quaternion rotation)
        {
            Position = position;
            Rotation = math.normalizesafe(rotation, quaternion.identity);
        }
        public float3 Position { get; }
        public quaternion Rotation { get; }
        public bool IsFinite => math.all(math.isfinite(Position)) && math.all(math.isfinite(Rotation.value));
        public static EarthMatterPose Identity => new EarthMatterPose(float3.zero, quaternion.identity);
    }

    public readonly struct EarthSourceProvenance
    {
        public EarthSourceProvenance(
            EarthSourceKind kind,
            uint sourceStableId,
            ushort sourceGeneration,
            int sourceCellIndex,
            uint sourceRevision,
            float3 sourceLocalPoint,
            float reservedVolume,
            EarthProvenanceFlags flags)
        {
            Kind = kind;
            SourceStableId = sourceStableId;
            SourceGeneration = sourceGeneration;
            SourceCellIndex = sourceCellIndex;
            SourceRevision = sourceRevision;
            SourceLocalPoint = sourceLocalPoint;
            ReservedVolume = math.max(0f, reservedVolume);
            Flags = flags;
        }

        public EarthSourceKind Kind { get; }
        public uint SourceStableId { get; }
        public ushort SourceGeneration { get; }
        public int SourceCellIndex { get; }
        public uint SourceRevision { get; }
        public float3 SourceLocalPoint { get; }
        public float ReservedVolume { get; }
        public EarthProvenanceFlags Flags { get; }
        public bool CanReturnExactly =>
            (Flags & (EarthProvenanceFlags.ExactReturnSupported | EarthProvenanceFlags.SourceCavityValid)) ==
            (EarthProvenanceFlags.ExactReturnSupported | EarthProvenanceFlags.SourceCavityValid);

        public EarthSourceProvenance WithFlags(EarthProvenanceFlags flags) =>
            new EarthSourceProvenance(
                Kind, SourceStableId, SourceGeneration, SourceCellIndex, SourceRevision,
                SourceLocalPoint, ReservedVolume, flags);
    }

    public struct EarthMatterRecord
    {
        public EarthMatterId Id;
        public EarthMatterPhase Phase;
        public EarthRepresentationTier Representation;
        public EarthMaterialKind Material;
        public float Volume;
        public float Mass;
        public float Integrity;
        public EarthSourceProvenance Source;
        public EarthOwnerId Owner;
        public EarthShapeSemantic Shape;
        public EarthMatterPose RestPose;
        public EarthMatterPose CurrentPose;
        public float3 LinearVelocity;
        public float3 AngularVelocity;

        public bool IsFiniteAndPhysical => Id.IsValid && Volume >= 0f && Mass >= 0f &&
                                          math.isfinite(Volume) && math.isfinite(Mass) &&
                                          math.isfinite(Integrity) && RestPose.IsFinite && CurrentPose.IsFinite &&
                                          math.all(math.isfinite(LinearVelocity)) &&
                                          math.all(math.isfinite(AngularVelocity));
    }
}
