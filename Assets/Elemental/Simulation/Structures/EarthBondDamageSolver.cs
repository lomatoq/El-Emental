using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public readonly struct EarthBondImpact
    {
        public EarthBondImpact(
            float3 localPoint,
            float3 localImpulse,
            float radius,
            float materialResponse,
            uint tick)
        {
            LocalPoint = localPoint;
            LocalImpulse = localImpulse;
            Radius = radius;
            MaterialResponse = materialResponse;
            Tick = tick;
        }

        public float3 LocalPoint { get; }
        public float3 LocalImpulse { get; }
        public float Radius { get; }
        public float MaterialResponse { get; }
        public uint Tick { get; }
    }

    public enum EarthBondDamageStatus : byte
    {
        Success,
        InvalidStorage,
        CapacityExceeded
    }

    public readonly struct EarthBondDamageResult
    {
        public EarthBondDamageResult(
            EarthBondDamageStatus status,
            int processedImpactCount,
            int invalidImpactCount,
            int newlyBrokenBondCount,
            int writtenBrokenBondCount,
            float accumulatedDamage,
            bool outputOverflowed)
        {
            Status = status;
            ProcessedImpactCount = processedImpactCount;
            InvalidImpactCount = invalidImpactCount;
            NewlyBrokenBondCount = newlyBrokenBondCount;
            WrittenBrokenBondCount = writtenBrokenBondCount;
            AccumulatedDamage = accumulatedDamage;
            OutputOverflowed = outputOverflowed;
        }

        public EarthBondDamageStatus Status { get; }
        public int ProcessedImpactCount { get; }
        public int InvalidImpactCount { get; }
        public int NewlyBrokenBondCount { get; }
        public int WrittenBrokenBondCount { get; }
        public float AccumulatedDamage { get; }
        public bool OutputOverflowed { get; }
    }

    public static class EarthBondDamageSolver
    {
        private const float MinimumArea = 0.04f;
        private const float MinimumStrength = 0.0001f;

        public static EarthBondDamageResult ApplyImpact(
            in EarthBondImpact impact,
            EarthBondDefinition[] definitions,
            EarthBondState[] states,
            int bondCount,
            EarthBondId[] brokenBondOutput)
        {
            EarthBondDamageStatus status = ValidateStorage(
                definitions, states, bondCount, 1, 1);
            if (status != EarthBondDamageStatus.Success)
                return InvalidResult(status);

            int written = 0;
            int broken = 0;
            float damage = 0f;
            int invalid = 0;
            if (IsValid(impact))
            {
                ApplyOne(
                    in impact,
                    definitions,
                    states,
                    bondCount,
                    brokenBondOutput,
                    ref written,
                    ref broken,
                    ref damage);
            }
            else
            {
                invalid = 1;
            }

            return new EarthBondDamageResult(
                EarthBondDamageStatus.Success,
                1 - invalid,
                invalid,
                broken,
                written,
                damage,
                broken > written);
        }

        public static EarthBondDamageResult ApplyBatch(
            EarthBondImpact[] impacts,
            int impactCount,
            EarthBondDefinition[] definitions,
            EarthBondState[] states,
            int bondCount,
            EarthBondId[] brokenBondOutput)
        {
            EarthBondDamageStatus status = ValidateStorage(
                definitions,
                states,
                bondCount,
                impacts == null ? -1 : impacts.Length,
                impactCount);
            if (status != EarthBondDamageStatus.Success)
                return InvalidResult(status);

            int processed = 0;
            int invalid = 0;
            int written = 0;
            int broken = 0;
            float damage = 0f;
            for (int impactIndex = 0; impactIndex < impactCount; impactIndex++)
            {
                EarthBondImpact impact = impacts[impactIndex];
                if (!IsValid(impact))
                {
                    invalid++;
                    continue;
                }

                processed++;
                ApplyOne(
                    in impact,
                    definitions,
                    states,
                    bondCount,
                    brokenBondOutput,
                    ref written,
                    ref broken,
                    ref damage);
            }

            return new EarthBondDamageResult(
                EarthBondDamageStatus.Success,
                processed,
                invalid,
                broken,
                written,
                damage,
                broken > written);
        }

        private static void ApplyOne(
            in EarthBondImpact impact,
            EarthBondDefinition[] definitions,
            EarthBondState[] states,
            int bondCount,
            EarthBondId[] brokenBondOutput,
            ref int written,
            ref int newlyBroken,
            ref float accumulatedDamage)
        {
            float radiusSq = impact.Radius * impact.Radius;
            for (int bondIndex = 0; bondIndex < bondCount; bondIndex++)
            {
                EarthBondState state = states[bondIndex];
                EarthBondDefinition definition = definitions[bondIndex];
                if (state.Phase == EarthBondPhase.Broken ||
                    (definition.Flags & EarthBondFlags.Unbreakable) != 0)
                {
                    continue;
                }

                float3 toBond = definition.LocalCentroid - impact.LocalPoint;
                float distanceSq = math.lengthsq(toBond);
                if (distanceSq >= radiusSq)
                    continue;

                float3 normal = math.normalizesafe(definition.LocalNormalA);
                if (math.lengthsq(normal) <= 0f)
                    continue;

                float normalImpulse = math.dot(impact.LocalImpulse, normal);
                float tension = math.max(0f, normalImpulse);
                float compression = math.max(0f, -normalImpulse);
                float3 shearVector = impact.LocalImpulse - (normal * normalImpulse);
                float shear = math.length(shearVector);

                float directionalDamage =
                    (tension / math.max(MinimumStrength, definition.TensileStrength)) +
                    (shear / math.max(MinimumStrength, definition.ShearStrength)) +
                    (compression / math.max(MinimumStrength, definition.CompressionStrength));
                if (!math.isfinite(directionalDamage) || directionalDamage <= 0f)
                    continue;

                float distance01 = math.sqrt(distanceSq) / impact.Radius;
                float radial = 1f - math.saturate(distance01);
                float radialWeight = radial * radial * (3f - (2f * radial));
                float areaWeight = math.clamp(
                    math.rsqrt(math.max(MinimumArea, definition.ContactArea)),
                    0.35f,
                    2.5f);
                float appliedDamage = radialWeight * areaWeight *
                                      impact.MaterialResponse * directionalDamage;
                if (!math.isfinite(appliedDamage) || appliedDamage <= 0f)
                    continue;

                float previousDamage = math.saturate(state.AccumulatedDamage);
                state.AccumulatedDamage = math.saturate(previousDamage + appliedDamage);
                state.LastChangedTick = impact.Tick;
                accumulatedDamage += state.AccumulatedDamage - previousDamage;
                if (state.AccumulatedDamage >= 1f)
                {
                    state.Phase = EarthBondPhase.Broken;
                    newlyBroken++;
                    if (brokenBondOutput != null && written < brokenBondOutput.Length)
                        brokenBondOutput[written++] = definition.Id;
                }
                else
                {
                    state.Phase = EarthBondPhase.Damaged;
                }
                states[bondIndex] = state;
            }
        }

        private static EarthBondDamageStatus ValidateStorage(
            EarthBondDefinition[] definitions,
            EarthBondState[] states,
            int bondCount,
            int inputCapacity,
            int inputCount)
        {
            if (definitions == null || states == null || inputCapacity < 0)
                return EarthBondDamageStatus.InvalidStorage;
            if (bondCount < 0 || inputCount < 0 ||
                bondCount > definitions.Length || bondCount > states.Length ||
                bondCount > EarthBondGraph.MaxBondCount || inputCount > inputCapacity)
            {
                return EarthBondDamageStatus.CapacityExceeded;
            }
            return EarthBondDamageStatus.Success;
        }

        private static bool IsValid(in EarthBondImpact impact)
        {
            return math.all(math.isfinite(impact.LocalPoint)) &&
                   math.all(math.isfinite(impact.LocalImpulse)) &&
                   math.isfinite(impact.Radius) && impact.Radius > 0f &&
                   math.isfinite(impact.MaterialResponse) && impact.MaterialResponse > 0f;
        }

        private static EarthBondDamageResult InvalidResult(EarthBondDamageStatus status)
        {
            return new EarthBondDamageResult(status, 0, 0, 0, 0, 0f, false);
        }
    }
}
