using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>
    /// Shared, frame-rate-normalized acceptance rules for the production
    /// Humanoid contact telemetry. Keeping the limits outside the PlayMode
    /// writer prevents a report from silently redefining what "passed" means.
    /// </summary>
    public static class EarthAnimationContactAcceptance
    {
        public const float MinimumReleaseRecaptureSeconds = 0.12f;
        public const float MaximumSwingIkWeight = 0.15f;
        public const float MaximumPlantedDriftMeters = 0.015f;
        public const float MinimumPlantedGapMeters = -0.010f;
        public const float MaximumPlantedGapMeters = 0.015f;
        public const float MaximumSupportLocalTargetStepAt60Hz = 0.025f;
        public const float MaximumJointStepDegreesAt60Hz = 8f;
        public const float MaximumPelvisStepAt60Hz = 0.020f;
        public const float MaximumCrossFpsDelta01 = 0.10f;

        public static float NormalizeTo60Hz(float step, float deltaTime)
        {
            float safeDelta = math.clamp(
                math.isfinite(deltaTime) ? deltaTime : 0f,
                0.0001f,
                0.1f);
            return math.abs(math.isfinite(step) ? step : 0f) *
                   math.clamp((1f / 60f) / safeDelta, 0.25f, 4f);
        }

        public static bool IsPlantedGapAccepted(float gapMeters) =>
            math.isfinite(gapMeters) &&
            gapMeters >= MinimumPlantedGapMeters - 0.00001f &&
            gapMeters <= MaximumPlantedGapMeters + 0.00001f;

        public static float RelativeDelta(float baseline, float candidate, float epsilon = 0.001f)
        {
            if (!math.isfinite(baseline) || !math.isfinite(candidate))
                return float.PositiveInfinity;
            float denominator = math.max(
                math.max(math.abs(baseline), math.abs(candidate)),
                math.max(0.000001f, epsilon));
            return math.abs(candidate - baseline) / denominator;
        }

        public static bool IsCrossFpsAccepted(float baseline, float candidate, float epsilon = 0.001f) =>
            RelativeDelta(baseline, candidate, epsilon) <= MaximumCrossFpsDelta01 + 0.00001f;

        public static bool IsUnallowedDiscontinuity(
            float footStepMeters,
            float kneeStepDegrees,
            float ankleStepDegrees,
            float pelvisStepMeters,
            bool allowedTransition)
        {
            if (allowedTransition) return false;
            return footStepMeters > 0.085f ||
                   kneeStepDegrees > MaximumJointStepDegreesAt60Hz ||
                   ankleStepDegrees > MaximumJointStepDegreesAt60Hz ||
                   pelvisStepMeters > MaximumPelvisStepAt60Hz;
        }
    }
}
