using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public readonly struct EarthDecorRockDamageResult
    {
        public EarthDecorRockDamageResult(float integrity, bool detach, bool shatter)
        {
            Integrity = math.max(0f, integrity);
            Detach = detach;
            Shatter = shatter;
        }

        public float Integrity { get; }
        public bool Detach { get; }
        public bool Shatter { get; }
    }

    public static class EarthDecorRockDamageSolver
    {
        public static EarthDecorRockDamageResult Resolve(
            float integrity,
            float impulse,
            bool anchored,
            float detachImpulse,
            float shatterImpulse)
        {
            float safeImpulse = math.max(0f, impulse);
            float remaining = math.max(0f, integrity - safeImpulse);
            bool shatter = safeImpulse >= math.max(1f, shatterImpulse) || remaining <= 0f;
            bool detach = anchored && !shatter && safeImpulse >= math.max(1f, detachImpulse);
            return new EarthDecorRockDamageResult(remaining, detach, shatter);
        }
    }

    public readonly struct EarthSecondaryFractureSample
    {
        public EarthSecondaryFractureSample(int pieceCount, float spreadSpeed, float intensity01)
        {
            PieceCount = math.max(1, pieceCount);
            SpreadSpeed = math.max(0f, spreadSpeed);
            Intensity01 = math.saturate(intensity01);
        }

        public int PieceCount { get; }
        public float SpreadSpeed { get; }
        public float Intensity01 { get; }
    }

    public static class EarthSecondaryFractureSolver
    {
        public static EarthSecondaryFractureSample Evaluate(
            int basePieceCount,
            int maximumPieceCount,
            float baseSpreadSpeed,
            float highEnergySpreadMultiplier,
            float radius,
            float largeRadius,
            float speed,
            float highSpeed)
        {
            float safeLargeRadius = math.max(0.05f, largeRadius);
            float safeHighSpeed = math.max(0.1f, highSpeed);
            float radius01 = math.saturate((math.max(0f, radius) - safeLargeRadius * 0.55f) /
                                           (safeLargeRadius * 1.25f));
            float speed01 = math.saturate((math.max(0f, speed) - safeHighSpeed * 0.55f) /
                                          (safeHighSpeed * 1.15f));
            float intensity = math.max(radius01, speed01);
            int minimumPieces = math.max(1, basePieceCount);
            int maximumPieces = math.max(minimumPieces, maximumPieceCount);
            int pieces = (int)math.round(math.lerp(minimumPieces, maximumPieces, intensity));
            float spread = math.max(0f, baseSpreadSpeed) *
                           math.lerp(1f, math.max(1f, highEnergySpreadMultiplier), intensity);
            return new EarthSecondaryFractureSample(pieces, spread, intensity);
        }
    }
}
