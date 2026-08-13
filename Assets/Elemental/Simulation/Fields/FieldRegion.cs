using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Fields
{
    public readonly struct FieldRegionId : IEquatable<FieldRegionId>
    {
        public FieldRegionId(uint value) => Value = value;
        public uint Value { get; }
        public bool IsValid => Value != 0u;
        public bool Equals(FieldRegionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is FieldRegionId other && Equals(other);
        public override int GetHashCode() => unchecked((int)Value);
    }

    public enum AirFieldKind : byte
    {
        GustCorridor = 1,
        Vortex = 2,
        LiftColumn = 3,
        AirBrake = 4
    }

    public readonly struct FieldRegion
    {
        public FieldRegion(
            FieldRegionId id,
            uint owner,
            AirFieldKind kind,
            float3 center,
            float3 axis,
            float radius,
            float length,
            float strength,
            float pressure,
            float lifetime,
            byte priority)
        {
            if (!id.IsValid || !math.all(math.isfinite(center)) || !math.all(math.isfinite(axis)) ||
                !float.IsFinite(radius) || radius <= 0f || !float.IsFinite(length) || length < 0f ||
                !float.IsFinite(strength) || strength < 0f || !float.IsFinite(pressure) ||
                !float.IsFinite(lifetime) || lifetime <= 0f)
            {
                throw new ArgumentException("Field region values must be finite and bounded.");
            }

            Id = id;
            Owner = owner;
            Kind = kind;
            Center = center;
            Axis = math.normalizesafe(axis, new float3(0f, 1f, 0f));
            Radius = radius;
            Length = length;
            Strength = strength;
            Pressure = pressure;
            RemainingLifetime = lifetime;
            Priority = priority;
        }

        private FieldRegion(FieldRegion source, float remainingLifetime)
        {
            Id = source.Id;
            Owner = source.Owner;
            Kind = source.Kind;
            Center = source.Center;
            Axis = source.Axis;
            Radius = source.Radius;
            Length = source.Length;
            Strength = source.Strength;
            Pressure = source.Pressure;
            RemainingLifetime = remainingLifetime;
            Priority = source.Priority;
        }

        public FieldRegionId Id { get; }
        public uint Owner { get; }
        public AirFieldKind Kind { get; }
        public float3 Center { get; }
        public float3 Axis { get; }
        public float Radius { get; }
        public float Length { get; }
        public float Strength { get; }
        public float Pressure { get; }
        public float RemainingLifetime { get; }
        public byte Priority { get; }
        public bool IsExpired => RemainingLifetime <= 0f;

        public FieldRegion StepLifetime(float deltaTime)
        {
            return new FieldRegion(this, math.max(0f, RemainingLifetime - deltaTime));
        }

        public bool TrySample(float3 worldPosition, out FieldContribution contribution)
        {
            switch (Kind)
            {
                case AirFieldKind.GustCorridor:
                    return TrySampleGust(worldPosition, out contribution);
                case AirFieldKind.Vortex:
                    return TrySampleVortex(worldPosition, out contribution);
                case AirFieldKind.LiftColumn:
                    return TrySampleLift(worldPosition, out contribution);
                case AirFieldKind.AirBrake:
                    return TrySampleBrake(worldPosition, out contribution);
                default:
                    contribution = default;
                    return false;
            }
        }

        private bool TrySampleGust(float3 position, out FieldContribution contribution)
        {
            float3 end = Center + (Axis * Length);
            float3 segment = end - Center;
            float denominator = math.max(math.lengthsq(segment), 0.0001f);
            float t = math.saturate(math.dot(position - Center, segment) / denominator);
            float distance = math.distance(position, Center + (segment * t));
            if (distance > Radius)
            {
                contribution = default;
                return false;
            }

            float falloff = 1f - math.saturate(distance / Radius);
            contribution = new FieldContribution(Axis * Strength * falloff, Pressure * falloff, 1f, falloff);
            return true;
        }

        private bool TrySampleVortex(float3 position, out FieldContribution contribution)
        {
            float3 offset = position - Center;
            float axial = math.dot(offset, Axis);
            float3 planar = offset - (Axis * axial);
            float radialDistance = math.length(planar);
            if (math.abs(axial) > Length * 0.5f || radialDistance > Radius)
            {
                contribution = default;
                return false;
            }

            float falloff = 1f - math.saturate(radialDistance / Radius);
            float3 radial = math.normalizesafe(planar, new float3(1f, 0f, 0f));
            float3 tangent = math.normalizesafe(math.cross(Axis, radial), new float3(0f, 0f, 1f));
            float3 velocity = (tangent * Strength * falloff) - (radial * Strength * 0.2f * falloff);
            contribution = new FieldContribution(velocity, Pressure * falloff, 1f, falloff);
            return true;
        }

        private bool TrySampleLift(float3 position, out FieldContribution contribution)
        {
            float3 offset = position - Center;
            float axial = math.dot(offset, Axis);
            float3 radial = offset - (Axis * axial);
            float radialDistance = math.length(radial);
            if (axial < 0f || axial > Length || radialDistance > Radius)
            {
                contribution = default;
                return false;
            }

            float falloff = 1f - math.saturate(radialDistance / Radius);
            contribution = new FieldContribution(Axis * Strength * falloff, Pressure * falloff, 1f, falloff);
            return true;
        }

        private bool TrySampleBrake(float3 position, out FieldContribution contribution)
        {
            float distance = math.distance(position, Center);
            if (distance > Radius)
            {
                contribution = default;
                return false;
            }

            float falloff = 1f - math.saturate(distance / Radius);
            contribution = new FieldContribution(float3.zero, Pressure * falloff, 1f + (Strength * falloff), falloff);
            return true;
        }
    }

    public readonly struct FieldContribution
    {
        public FieldContribution(float3 velocity, float pressure, float dragMultiplier, float weight)
        {
            Velocity = velocity;
            Pressure = pressure;
            DragMultiplier = dragMultiplier;
            Weight = weight;
        }

        public float3 Velocity { get; }
        public float Pressure { get; }
        public float DragMultiplier { get; }
        public float Weight { get; }
    }

    public readonly struct FieldSample
    {
        public FieldSample(float3 velocity, float pressure, float dragMultiplier, int activeRegions, int regionChecks)
        {
            Velocity = velocity;
            Pressure = pressure;
            DragMultiplier = dragMultiplier;
            ActiveRegions = activeRegions;
            RegionChecks = regionChecks;
        }

        public float3 Velocity { get; }
        public float Pressure { get; }
        public float DragMultiplier { get; }
        public int ActiveRegions { get; }
        public int RegionChecks { get; }
    }
}
