using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public readonly struct EarthStructureId : IEquatable<EarthStructureId>
    {
        public EarthStructureId(uint value) => Value = value;
        public uint Value { get; }
        public bool IsValid => Value != 0u;
        public bool Equals(EarthStructureId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EarthStructureId other && Equals(other);
        public override int GetHashCode() => unchecked((int)Value);
        public override string ToString() => Value.ToString();
        public static bool operator ==(EarthStructureId left, EarthStructureId right) => left.Equals(right);
        public static bool operator !=(EarthStructureId left, EarthStructureId right) => !left.Equals(right);
    }

    public readonly struct EarthPieceId : IEquatable<EarthPieceId>
    {
        public EarthPieceId(ushort value) => Value = value;
        public ushort Value { get; }
        public bool IsValid => Value != 0;
        public bool Equals(EarthPieceId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EarthPieceId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
        public static bool operator ==(EarthPieceId left, EarthPieceId right) => left.Equals(right);
        public static bool operator !=(EarthPieceId left, EarthPieceId right) => !left.Equals(right);
    }

    public readonly struct EarthBondId : IEquatable<EarthBondId>
    {
        public EarthBondId(ushort value) => Value = value;
        public ushort Value { get; }
        public bool IsValid => Value != 0;
        public bool Equals(EarthBondId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EarthBondId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
        public static bool operator ==(EarthBondId left, EarthBondId right) => left.Equals(right);
        public static bool operator !=(EarthBondId left, EarthBondId right) => !left.Equals(right);
    }

    [Flags]
    public enum EarthPieceFlags : byte
    {
        None = 0,
        Structural = 1 << 0,
        Repairable = 1 << 1,
        Foundation = 1 << 2,
        HeroPiece = 1 << 3
    }

    [Flags]
    public enum EarthBondFlags : byte
    {
        None = 0,
        Foundation = 1 << 0,
        Repairable = 1 << 1,
        Unbreakable = 1 << 2
    }

    public enum EarthPiecePhase : byte
    {
        Intact,
        Cracked,
        Dynamic,
        Captured,
        Staging,
        Aligning,
        WeldCandidate,
        Welded,
        Missing
    }

    public enum EarthBondPhase : byte
    {
        Healthy,
        Damaged,
        Broken,
        Reforming,
        Repaired
    }

    public enum EarthStructurePhase : byte
    {
        Intact,
        Damaged,
        Fractured,
        Repairing,
        Rebuilt,
        Reabsorbing
    }

    public struct EarthStructureFrame
    {
        public float3 PlanetLocalOrigin;
        public float3 Tangent;
        public float3 Up;
        public float3 Forward;
    }

    public struct EarthPieceDefinition
    {
        public EarthPieceId Id;
        public short ParentPieceIndex;
        public byte HierarchyLevel;
        public EarthPieceFlags Flags;

        public float3 RestLocalPosition;
        public quaternion RestLocalRotation;
        public float3 RestLocalScale;

        public float Mass;
        public float Volume;
        public float3 LocalCenterOfMass;
        public byte MaterialId;
    }

    public struct EarthBondDefinition
    {
        public EarthBondId Id;
        public short PieceA;
        public short PieceB;
        public EarthBondFlags Flags;

        public float3 LocalCentroid;
        public float3 LocalNormalA;
        public float ContactArea;

        public float TensileStrength;
        public float ShearStrength;
        public float CompressionStrength;
    }

    public struct EarthPieceState
    {
        public EarthPiecePhase Phase;
        public short IslandIndex;
        public uint LastChangedTick;

        public static EarthPieceState Intact => new EarthPieceState
        {
            Phase = EarthPiecePhase.Intact,
            IslandIndex = -1
        };
    }

    public struct EarthBondState
    {
        public EarthBondPhase Phase;
        public float AccumulatedDamage;
        public uint LastChangedTick;

        public static EarthBondState Healthy => new EarthBondState
        {
            Phase = EarthBondPhase.Healthy,
            AccumulatedDamage = 0f
        };
    }

    public struct EarthStructureState
    {
        public EarthStructureId Id;
        public EarthStructurePhase Phase;
        public EarthStructureFrame Frame;
        public ushort PieceCount;
        public ushort BondCount;
        public ushort IslandCount;
        public ushort SupportedIslandCount;
        public uint Revision;
        public uint LastChangedTick;
    }
}
