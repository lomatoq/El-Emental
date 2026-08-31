using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    /// <summary>
    /// One deterministic mass scale for authored arena pieces, loose rocks and
    /// reconstructed earth matter. Physical density preserves size ordering;
    /// a sub-linear compression keeps gameplay impulses controllable without
    /// inventing a different density for every content family.
    /// </summary>
    public readonly struct EarthMatterMassProfile
    {
        public EarthMatterMassProfile(
            float densityKilogramsPerCubicMetre,
            float referencePhysicalMassKilograms,
            float referenceGameplayMassKilograms,
            float compressionExponent,
            float minimumGameplayMassKilograms,
            float maximumGameplayMassKilograms)
        {
            DensityKilogramsPerCubicMetre = math.max(0.001f, densityKilogramsPerCubicMetre);
            ReferencePhysicalMassKilograms = math.max(0.001f, referencePhysicalMassKilograms);
            ReferenceGameplayMassKilograms = math.max(0.001f, referenceGameplayMassKilograms);
            CompressionExponent = math.clamp(compressionExponent, 0.25f, 1f);
            MinimumGameplayMassKilograms = math.max(0.001f, minimumGameplayMassKilograms);
            MaximumGameplayMassKilograms = math.max(
                MinimumGameplayMassKilograms,
                maximumGameplayMassKilograms);
        }

        public float DensityKilogramsPerCubicMetre { get; }
        public float ReferencePhysicalMassKilograms { get; }
        public float ReferenceGameplayMassKilograms { get; }
        public float CompressionExponent { get; }
        public float MinimumGameplayMassKilograms { get; }
        public float MaximumGameplayMassKilograms { get; }

        public static EarthMatterMassProfile ArenaStone => new EarthMatterMassProfile(
            densityKilogramsPerCubicMetre: 2300f,
            referencePhysicalMassKilograms: 230f,
            referenceGameplayMassKilograms: 120f,
            compressionExponent: 0.68f,
            minimumGameplayMassKilograms: 12f,
            maximumGameplayMassKilograms: 1800f);
    }

    public static class EarthMatterMassPolicy
    {
        public static float ResolveGameplayMass(
            float volumeCubicMetres,
            in EarthMatterMassProfile profile)
        {
            float volume = math.max(0f, Sanitize(volumeCubicMetres));
            return ResolveGameplayMassFromPhysicalMass(
                volume * profile.DensityKilogramsPerCubicMetre,
                in profile);
        }

        public static float ResolveGameplayMassFromPhysicalMass(
            float physicalMassKilograms,
            in EarthMatterMassProfile profile)
        {
            float physicalMass = math.max(0f, Sanitize(physicalMassKilograms));
            if (physicalMass <= 0f)
                return profile.MinimumGameplayMassKilograms;

            float normalized = math.max(
                0.000001f,
                physicalMass / profile.ReferencePhysicalMassKilograms);
            float gameplayMass = profile.ReferenceGameplayMassKilograms *
                                 math.pow(normalized, profile.CompressionExponent);
            return math.clamp(
                Sanitize(gameplayMass),
                profile.MinimumGameplayMassKilograms,
                profile.MaximumGameplayMassKilograms);
        }

        public static float EstimateBoxVolume(float3 size, float solidFillRatio = 0.62f)
        {
            float3 safe = math.max(math.select(float3.zero, size, math.isfinite(size)), float3.zero);
            return safe.x * safe.y * safe.z * math.saturate(Sanitize(solidFillRatio));
        }

        private static float Sanitize(float value) =>
            math.isfinite(value) ? value : 0f;
    }
}
