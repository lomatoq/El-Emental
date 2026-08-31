using Unity.Mathematics;

namespace Elemental.Simulation.Rendering
{
    /// <summary>
    /// CPU mirror of the cinematic DOF shader's signed circle-of-confusion and
    /// foreground-safe composite policy. Keeping the math pure makes the most
    /// important depth/occlusion invariants testable without a GPU.
    /// </summary>
    public static class EarthCinematicDepthOfFieldSolver
    {
        public static float SignedCircleOfConfusion(
            float eyeDepth,
            float sharpNearDistance,
            float sharpFarDistance,
            float nearTransition,
            float farTransition)
        {
            eyeDepth = math.max(0.0001f, eyeDepth);
            sharpNearDistance = math.max(0.0001f, sharpNearDistance);
            sharpFarDistance = math.max(sharpNearDistance, sharpFarDistance);
            nearTransition = math.max(0.0001f, nearTransition);
            farTransition = math.max(0.0001f, farTransition);

            if (eyeDepth < sharpNearDistance)
                return -math.saturate(
                    (sharpNearDistance - eyeDepth) / nearTransition);
            if (eyeDepth > sharpFarDistance)
                return math.saturate(
                    (eyeDepth - sharpFarDistance) / farTransition);
            return 0f;
        }

        public static EarthCinematicDepthOfFieldEnvelope ResolveSharpEnvelope(
            float primarySubjectDepth,
            float secondarySubjectDepth,
            float silhouettePadding,
            float minimumDepth = 1.25f,
            float maximumDepth = 36f)
        {
            return ResolveSharpEnvelopeFromRanges(
                primarySubjectDepth,
                primarySubjectDepth,
                secondarySubjectDepth,
                secondarySubjectDepth,
                silhouettePadding,
                minimumDepth,
                maximumDepth);
        }

        public static EarthCinematicDepthOfFieldEnvelope ResolveSharpEnvelopeFromRanges(
            float primaryNearDepth,
            float primaryFarDepth,
            float secondaryNearDepth,
            float secondaryFarDepth,
            float silhouettePadding,
            float minimumDepth = 1.25f,
            float maximumDepth = 36f)
        {
            minimumDepth = math.max(0.0001f, minimumDepth);
            maximumDepth = math.max(minimumDepth, maximumDepth);
            silhouettePadding = math.max(0f, silhouettePadding);

            bool primaryValid = IsValidRange(primaryNearDepth, primaryFarDepth);
            bool secondaryValid = IsValidRange(secondaryNearDepth, secondaryFarDepth);
            float primaryNear = primaryValid
                ? math.min(primaryNearDepth, primaryFarDepth)
                : secondaryValid
                    ? math.min(secondaryNearDepth, secondaryFarDepth)
                    : minimumDepth;
            float primaryFar = primaryValid
                ? math.max(primaryNearDepth, primaryFarDepth)
                : secondaryValid
                    ? math.max(secondaryNearDepth, secondaryFarDepth)
                    : minimumDepth;
            float secondaryNear = secondaryValid
                ? math.min(secondaryNearDepth, secondaryFarDepth)
                : primaryNear;
            float secondaryFar = secondaryValid
                ? math.max(secondaryNearDepth, secondaryFarDepth)
                : primaryFar;
            float near = math.clamp(
                math.min(primaryNear, secondaryNear) - silhouettePadding,
                minimumDepth,
                maximumDepth);
            float far = math.clamp(
                math.max(primaryFar, secondaryFar) + silhouettePadding,
                near,
                maximumDepth);
            return new EarthCinematicDepthOfFieldEnvelope(near, far);
        }

        /// <summary>
        /// Expands immediately so a newly separated subject is never blurred.
        /// Contraction is deliberately rate-limited to suppress focus pumping
        /// when actors cross, dodge or briefly lose their renderer bounds.
        /// </summary>
        public static EarthCinematicDepthOfFieldEnvelope StepSharpEnvelope(
            in EarthCinematicDepthOfFieldEnvelope current,
            in EarthCinematicDepthOfFieldEnvelope target,
            float contractionSpeed,
            float deltaTime)
        {
            contractionSpeed = math.max(0f, contractionSpeed);
            deltaTime = math.max(0f, deltaTime);
            float maximumContraction = contractionSpeed * deltaTime;
            float near = target.Near < current.Near
                ? target.Near
                : MoveTowards(current.Near, target.Near, maximumContraction);
            float far = target.Far > current.Far
                ? target.Far
                : MoveTowards(current.Far, target.Far, maximumContraction);
            return new EarthCinematicDepthOfFieldEnvelope(
                math.min(near, far),
                math.max(near, far));
        }

        public static EarthCinematicDepthOfFieldCompositeWeights ResolveCompositeWeights(
            float centerSignedCoc,
            float nearCoverage,
            float farCoverage)
        {
            // The near gather is deliberately dilated to cover foreground edges,
            // but it may never replace a pixel whose own full-resolution depth is
            // inside the sharp envelope. Without this guard a half-resolution
            // foreground tap can visibly soften an otherwise focused silhouette.
            bool centerIsSharp = math.abs(centerSignedCoc) <= 0.001f;
            float near = centerIsSharp ? 0f : math.saturate(nearCoverage);
            // A far gather may never paint across a pixel classified as foreground
            // or in-focus. The dilated near layer is composed last.
            float far = centerSignedCoc > 0f
                ? math.saturate(farCoverage * centerSignedCoc) * (1f - near)
                : 0f;
            return new EarthCinematicDepthOfFieldCompositeWeights(
                far,
                near,
                (1f - far) * (1f - near));
        }

        private static float MoveTowards(float current, float target, float maximumDelta)
        {
            float delta = target - current;
            if (math.abs(delta) <= maximumDelta) return target;
            return current + math.sign(delta) * maximumDelta;
        }

        private static bool IsValidRange(float near, float far)
        {
            return math.isfinite(near) && math.isfinite(far) &&
                   math.max(near, far) > 0f;
        }
    }

    public readonly struct EarthCinematicDepthOfFieldEnvelope
    {
        public EarthCinematicDepthOfFieldEnvelope(float near, float far)
        {
            Near = near;
            Far = math.max(near, far);
        }

        public float Near { get; }
        public float Far { get; }
        public float Midpoint => (Near + Far) * 0.5f;
        public float Width => Far - Near;
    }

    public readonly struct EarthCinematicDepthOfFieldCompositeWeights
    {
        public EarthCinematicDepthOfFieldCompositeWeights(
            float far,
            float near,
            float sharp)
        {
            Far = far;
            Near = near;
            Sharp = sharp;
        }

        public float Far { get; }
        public float Near { get; }
        public float Sharp { get; }
    }
}
