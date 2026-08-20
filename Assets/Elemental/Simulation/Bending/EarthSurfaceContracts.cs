using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum EarthSurfaceKind : byte
    {
        Invalid = 0,
        Planet = 1,
        Platform = 2,
        WallTop = 3,
        WallSide = 4,
        PlatformSide = 5
    }

    public enum EarthSurfaceMaterial : byte
    {
        Unknown = 0,
        PlanetStone = 1,
        RaisedEarth = 2,
        ConstructedEarth = 3
    }

    public enum EarthSurfaceProvenance : byte
    {
        Unknown = 0,
        VoxelPlanet = 1,
        RaisedPlatform = 2,
        RaisedWall = 3
    }

    [Flags]
    public enum EarthSurfaceCapabilities : ushort
    {
        None = 0,
        Support = 1 << 0,
        Pillar = 1 << 1,
        LandingCushion = 1 << 2,
        Draw = 1 << 3,
        Destructible = 1 << 4,
        Moving = 1 << 5
    }

    public readonly struct EarthSurfaceHandle : IEquatable<EarthSurfaceHandle>
    {
        public EarthSurfaceHandle(EarthSurfaceKind kind, uint stableId, uint generation, byte faceId = 0)
        {
            Kind = kind;
            StableId = stableId;
            Generation = generation;
            FaceId = faceId;
        }

        public EarthSurfaceKind Kind { get; }
        public uint StableId { get; }
        public uint Generation { get; }
        public byte FaceId { get; }
        public bool IsValid => Kind != EarthSurfaceKind.Invalid && StableId != 0u && Generation != 0u;

        public bool Equals(EarthSurfaceHandle other) =>
            Kind == other.Kind && StableId == other.StableId && Generation == other.Generation &&
            FaceId == other.FaceId;

        public override bool Equals(object obj) => obj is EarthSurfaceHandle other && Equals(other);
        public override int GetHashCode() =>
            (((int)Kind * 397) ^ ((int)StableId * 31) ^ (int)Generation) * 31 ^ FaceId;
        public static bool operator ==(EarthSurfaceHandle left, EarthSurfaceHandle right) => left.Equals(right);
        public static bool operator !=(EarthSurfaceHandle left, EarthSurfaceHandle right) => !left.Equals(right);
    }

    public readonly struct EarthSurfaceQuery
    {
        public EarthSurfaceQuery(
            float3 origin,
            float3 direction,
            float maximumDistance,
            EarthSurfaceCapabilities requiredCapabilities,
            float castRadius = 0f)
        {
            Origin = origin;
            Direction = math.normalizesafe(direction, new float3(0f, -1f, 0f));
            MaximumDistance = math.max(0f, maximumDistance);
            RequiredCapabilities = requiredCapabilities;
            CastRadius = math.max(0f, castRadius);
        }

        public float3 Origin { get; }
        public float3 Direction { get; }
        public float MaximumDistance { get; }
        public EarthSurfaceCapabilities RequiredCapabilities { get; }
        public float CastRadius { get; }
        public bool IsValid => MaximumDistance > 0.0001f && math.lengthsq(Direction) > 0.5f;
    }

    public readonly struct EarthSurfaceSample
    {
        public EarthSurfaceSample(
            EarthSurfaceHandle handle,
            float3 point,
            float3 normal,
            float3 tangent,
            float3 velocity,
            float distance,
            EarthSurfaceMaterial material,
            EarthSurfaceProvenance provenance,
            EarthSurfaceCapabilities capabilities)
        {
            Handle = handle;
            Point = point;
            Normal = math.normalizesafe(normal, new float3(0f, 1f, 0f));
            float3 projectedTangent = tangent - Normal * math.dot(tangent, Normal);
            Tangent = math.normalizesafe(projectedTangent, OrthonormalTangent(Normal));
            Bitangent = math.normalizesafe(math.cross(Normal, Tangent), new float3(0f, 0f, 1f));
            Velocity = velocity;
            Distance = math.max(0f, distance);
            Material = material;
            Provenance = provenance;
            Capabilities = capabilities;
        }

        public EarthSurfaceHandle Handle { get; }
        public float3 Point { get; }
        public float3 Normal { get; }
        public float3 Tangent { get; }
        public float3 Bitangent { get; }
        public float3 Velocity { get; }
        public float Distance { get; }
        public EarthSurfaceMaterial Material { get; }
        public EarthSurfaceProvenance Provenance { get; }
        public EarthSurfaceCapabilities Capabilities { get; }
        public bool IsValid => Handle.IsValid && math.all(math.isfinite(Point)) &&
                               math.all(math.isfinite(Normal)) && math.isfinite(Distance);

        public bool Supports(EarthSurfaceCapabilities required) =>
            (Capabilities & required) == required;

        private static float3 OrthonormalTangent(float3 normal)
        {
            float3 reference = math.abs(normal.y) < 0.92f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            return math.normalizesafe(math.cross(reference, normal), new float3(1f, 0f, 0f));
        }
    }

    public static class EarthSurfaceSelection
    {
        public static bool IsBetter(
            in EarthSurfaceSample candidate,
            in EarthSurfaceSample current,
            EarthSurfaceCapabilities required)
        {
            if (!candidate.IsValid || !candidate.Supports(required)) return false;
            if (!current.IsValid) return true;
            if (candidate.Distance < current.Distance - 0.0001f) return true;
            if (math.abs(candidate.Distance - current.Distance) > 0.0001f) return false;
            // Stable deterministic tie-break: constructed support before planet,
            // then by kind/id/generation.
            if (candidate.Handle.Kind != current.Handle.Kind)
                return candidate.Handle.Kind > current.Handle.Kind;
            if (candidate.Handle.StableId != current.Handle.StableId)
                return candidate.Handle.StableId < current.Handle.StableId;
            return candidate.Handle.Generation < current.Handle.Generation;
        }
    }
}
