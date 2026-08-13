using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum EarthTechniqueKind : byte
    {
        None = 0,
        Grip = 1,
        Wall = 2,
        Platform = 3,
        Pillar = 4,
        GroundWave = 5,
        Repair = 6
    }

    public enum EarthTechniqueStage : byte
    {
        Idle = 0,
        Intent = 1,
        Anticipation = 2,
        Release = 3,
        Impact = 4,
        Settle = 5,
        Complete = 6
    }

    public enum EarthTechniqueRejectReason : byte
    {
        None = 0,
        InvalidSource = 1,
        NotGrounded = 2,
        OverMass = 3,
        Obstructed = 4,
        InvalidGesture = 5,
        AmbiguousGesture = 6,
        PoolExhausted = 7,
        MissingProvenance = 8,
        RuntimeUnavailable = 9
    }

    public enum EarthTechniqueGesture : byte
    {
        None = 0,
        Tap = 1,
        Line = 2,
        ClosedRegion = 3,
        Sweep = 4,
        Flick = 5
    }

    [Flags]
    public enum EarthTechniqueModifierFlags : byte
    {
        None = 0,
        Primary = 1 << 0,
        Force = 1 << 1,
        Field = 1 << 2,
        Modifier = 1 << 3,
        Jump = 1 << 4,
        Release = 1 << 5
    }

    public readonly struct EarthTechniqueContext
    {
        public EarthTechniqueContext(
            EarthTechniqueGesture gesture,
            EarthTechniqueModifierFlags modifiers,
            bool terrain,
            bool physicalTarget,
            bool brokenStructure,
            bool grounded,
            bool overMass = false,
            bool obstructed = false,
            bool hasProvenance = true)
        {
            Gesture = gesture;
            Modifiers = modifiers;
            Terrain = terrain;
            PhysicalTarget = physicalTarget;
            BrokenStructure = brokenStructure;
            Grounded = grounded;
            OverMass = overMass;
            Obstructed = obstructed;
            HasProvenance = hasProvenance;
        }

        public EarthTechniqueGesture Gesture { get; }
        public EarthTechniqueModifierFlags Modifiers { get; }
        public bool Terrain { get; }
        public bool PhysicalTarget { get; }
        public bool BrokenStructure { get; }
        public bool Grounded { get; }
        public bool OverMass { get; }
        public bool Obstructed { get; }
        public bool HasProvenance { get; }
        public bool Has(EarthTechniqueModifierFlags flag) => (Modifiers & flag) != 0;
    }

    public readonly struct EarthTechniqueResolution
    {
        public EarthTechniqueResolution(EarthTechniqueKind technique, EarthTechniqueRejectReason rejection)
        {
            Technique = technique;
            Rejection = rejection;
        }

        public EarthTechniqueKind Technique { get; }
        public EarthTechniqueRejectReason Rejection { get; }
        public bool Accepted => Technique != EarthTechniqueKind.None && Rejection == EarthTechniqueRejectReason.None;
    }

    /// <summary>Pure contextual grammar used by live input, replay and tests.</summary>
    public static class EarthTechniqueRouter
    {
        public static EarthTechniqueResolution Resolve(in EarthTechniqueContext context)
        {
            if (context.OverMass) return Reject(EarthTechniqueRejectReason.OverMass);
            if (context.Obstructed) return Reject(EarthTechniqueRejectReason.Obstructed);

            if (context.Has(EarthTechniqueModifierFlags.Field))
            {
                if (!context.BrokenStructure) return Reject(EarthTechniqueRejectReason.InvalidSource);
                return context.HasProvenance
                    ? Accept(EarthTechniqueKind.Repair)
                    : Reject(EarthTechniqueRejectReason.MissingProvenance);
            }

            if (context.Has(EarthTechniqueModifierFlags.Jump) &&
                context.Has(EarthTechniqueModifierFlags.Force))
                return context.Grounded
                    ? Accept(EarthTechniqueKind.Pillar)
                    : Reject(EarthTechniqueRejectReason.NotGrounded);

            if (context.Has(EarthTechniqueModifierFlags.Primary) && context.Has(EarthTechniqueModifierFlags.Force))
            {
                if (!context.Grounded) return Reject(EarthTechniqueRejectReason.NotGrounded);
                return context.Terrain &&
                       (context.Gesture == EarthTechniqueGesture.Line || context.Gesture == EarthTechniqueGesture.Sweep)
                    ? Accept(EarthTechniqueKind.GroundWave)
                    : Reject(EarthTechniqueRejectReason.InvalidGesture);
            }

            if (!context.Has(EarthTechniqueModifierFlags.Primary))
                return Reject(EarthTechniqueRejectReason.InvalidGesture);
            if (context.PhysicalTarget) return Accept(EarthTechniqueKind.Grip);
            if (!context.Terrain) return Reject(EarthTechniqueRejectReason.InvalidSource);
            if (context.Gesture == EarthTechniqueGesture.Line) return Accept(EarthTechniqueKind.Wall);
            if (context.Gesture == EarthTechniqueGesture.ClosedRegion) return Accept(EarthTechniqueKind.Platform);
            return Reject(EarthTechniqueRejectReason.AmbiguousGesture);
        }

        private static EarthTechniqueResolution Accept(EarthTechniqueKind kind) =>
            new EarthTechniqueResolution(kind, EarthTechniqueRejectReason.None);

        private static EarthTechniqueResolution Reject(EarthTechniqueRejectReason reason) =>
            new EarthTechniqueResolution(EarthTechniqueKind.None, reason);
    }

    [Serializable]
    public readonly struct EarthTechniqueCommand
    {
        public EarthTechniqueCommand(
            uint tick,
            uint casterId,
            EarthTechniqueKind technique,
            uint sourceStableId,
            ushort sourceGeneration,
            float3 origin,
            float3 direction,
            float primary01,
            float secondary01,
            EarthTechniqueModifierFlags modifiers,
            uint seed,
            uint geometryDigest)
        {
            Tick = tick;
            CasterId = casterId;
            Technique = technique;
            SourceStableId = sourceStableId;
            SourceGeneration = sourceGeneration;
            Origin = math.select(float3.zero, origin, math.isfinite(origin));
            Direction = math.normalizesafe(direction, new float3(0f, 1f, 0f));
            PrimaryQ = EarthTechniqueParameterCodec.Quantize01(primary01);
            SecondaryQ = EarthTechniqueParameterCodec.Quantize01(secondary01);
            Modifiers = modifiers;
            Seed = seed;
            GeometryDigest = geometryDigest;
        }

        public uint Tick { get; }
        public uint CasterId { get; }
        public EarthTechniqueKind Technique { get; }
        public uint SourceStableId { get; }
        public ushort SourceGeneration { get; }
        public float3 Origin { get; }
        public float3 Direction { get; }
        public ushort PrimaryQ { get; }
        public ushort SecondaryQ { get; }
        public EarthTechniqueModifierFlags Modifiers { get; }
        public uint Seed { get; }
        public uint GeometryDigest { get; }
        public float Primary01 => EarthTechniqueParameterCodec.Dequantize01(PrimaryQ);
        public float Secondary01 => EarthTechniqueParameterCodec.Dequantize01(SecondaryQ);
    }

    public static class EarthTechniqueParameterCodec
    {
        public static ushort Quantize01(float value) => (ushort)math.round(math.saturate(value) * ushort.MaxValue);
        public static float Dequantize01(ushort value) => value / (float)ushort.MaxValue;
        public static uint Pack(float primary01, float secondary01) =>
            Quantize01(primary01) | ((uint)Quantize01(secondary01) << 16);
        public static float UnpackPrimary(uint packed) => Dequantize01((ushort)(packed & 0xffffu));
        public static float UnpackSecondary(uint packed) => Dequantize01((ushort)(packed >> 16));
    }

    public readonly struct EarthTechniqueTiming
    {
        public EarthTechniqueTiming(float anticipation, float release, float impact, float settle)
        {
            Anticipation = math.max(0f, anticipation);
            Release = math.max(0f, release);
            Impact = math.max(0f, impact);
            Settle = math.max(0f, settle);
        }

        public float Anticipation { get; }
        public float Release { get; }
        public float Impact { get; }
        public float Settle { get; }
        public float Duration => Anticipation + Release + Impact + Settle;

        public EarthTechniqueStage Evaluate(float elapsed)
        {
            if (elapsed < 0f) return EarthTechniqueStage.Intent;
            if (elapsed < Anticipation) return EarthTechniqueStage.Anticipation;
            elapsed -= Anticipation;
            if (elapsed < Release) return EarthTechniqueStage.Release;
            elapsed -= Release;
            if (elapsed < Impact) return EarthTechniqueStage.Impact;
            elapsed -= Impact;
            if (elapsed < Settle) return EarthTechniqueStage.Settle;
            return EarthTechniqueStage.Complete;
        }
    }
}
