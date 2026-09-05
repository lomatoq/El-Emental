using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>Final gaze envelope; authored motion remains untouched inside it.</summary>
    public static class EarthHeadPitchStabilizer
    {
        public const float MinimumPitchDegrees = -25f;
        public const float MaximumPitchDegrees = 28f;

        public static float CorrectionDegrees(float measuredPitchDegrees)
        {
            if (!math.isfinite(measuredPitchDegrees)) return 0f;
            return math.clamp(
                       measuredPitchDegrees,
                       MinimumPitchDegrees,
                       MaximumPitchDegrees) - measuredPitchDegrees;
        }
    }
}
