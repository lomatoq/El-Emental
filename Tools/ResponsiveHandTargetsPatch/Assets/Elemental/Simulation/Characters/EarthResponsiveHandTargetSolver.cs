using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public struct EarthResponsiveHandTargetState
    {
        public bool IsInitialized;
        public float3 LocalAim;
        public float ReachMeters;
        public float HandSpreadMeters;
    }

    public readonly struct EarthResponsiveHandTargetSample
    {
        public EarthResponsiveHandTargetSample(float3 localAim, float reachMeters, float handSpreadMeters)
        {
            LocalAim = localAim;
            ReachMeters = reachMeters;
            HandSpreadMeters = handSpreadMeters;
        }

        public float3 LocalAim { get; }
        public float ReachMeters { get; }
        public float HandSpreadMeters { get; }
    }

    /// <summary>
    /// Filters persistent magic hand targets in character space. World motion of
    /// the character therefore moves the targets immediately, while changes in
    /// the controlled body's direction and reach remain bounded.
    /// </summary>
    public static class EarthResponsiveHandTargetSolver
    {
        public const float MaximumYawDegrees = 70f;
        public const float MinimumPitchDegrees = -18f;
        public const float MaximumPitchDegrees = 38f;
        public const float MaximumAimDegreesPerSecond = 300f;
        public const float MaximumReachMetersPerSecond = 1.20f;
        public const float MaximumSpreadMetersPerSecond = 0.60f;
        public const float MaximumTorsoYawDegrees = 4.5f;
        public const float MinimumReachMeters = 0.25f;
        public const float MaximumReachMeters = 0.68f;
        public const float MinimumSpreadMeters = 0.08f;
        public const float MaximumSpreadMeters = 0.24f;
        private const float MaximumDeltaTime = 0.05f;

        public static EarthResponsiveHandTargetSample Step(
            ref EarthResponsiveHandTargetState state,
            float3 desiredLocalAim,
            float desiredReachMeters,
            float desiredHandSpreadMeters,
            bool hasLiveFocus,
            float deltaTime)
        {
            if (!hasLiveFocus)
                return SampleOrFallback(in state);

            float3 targetAim = ConstrainAim(desiredLocalAim);
            float targetReach = ClampFinite(
                desiredReachMeters, MinimumReachMeters, MaximumReachMeters, 0.48f);
            float targetSpread = ClampFinite(
                desiredHandSpreadMeters, MinimumSpreadMeters, MaximumSpreadMeters, 0.15f);

            if (!state.IsInitialized)
            {
                state.IsInitialized = true;
                state.LocalAim = targetAim;
                state.ReachMeters = targetReach;
                state.HandSpreadMeters = targetSpread;
                return ToSample(in state);
            }

            float dt = math.clamp(math.isfinite(deltaTime) ? deltaTime : 0f, 0f, MaximumDeltaTime);
            state.LocalAim = RotateTowards(
                state.LocalAim,
                targetAim,
                math.radians(MaximumAimDegreesPerSecond) * dt);
            state.ReachMeters = MoveTowards(
                state.ReachMeters,
                targetReach,
                MaximumReachMetersPerSecond * dt);
            state.HandSpreadMeters = MoveTowards(
                state.HandSpreadMeters,
                targetSpread,
                MaximumSpreadMetersPerSecond * dt);
            return ToSample(in state);
        }

        public static void Reset(ref EarthResponsiveHandTargetState state) => state = default;

        public static float ResolveTorsoYawDegrees(float3 filteredLocalAim, float handConstraintWeight)
        {
            float3 aim = ConstrainAim(filteredLocalAim);
            float yawDegrees = math.degrees(math.atan2(aim.x, math.max(0.001f, aim.z)));
            float weight = math.saturate(
                (math.isfinite(handConstraintWeight) ? handConstraintWeight : 0f) / 0.25f);
            return math.clamp(
                yawDegrees * 0.10f,
                -MaximumTorsoYawDegrees,
                MaximumTorsoYawDegrees) * weight;
        }

        public static float3 ConstrainAim(float3 desiredLocalAim)
        {
            float3 direction = math.normalizesafe(
                math.select(new float3(0f, 0f, 1f), desiredLocalAim, math.isfinite(desiredLocalAim)),
                new float3(0f, 0f, 1f));

            // Ignore the rearward component when selecting yaw. A target directly
            // behind the torso resolves forward; a target behind either shoulder
            // resolves to the matching edge of the reachable front cone.
            float yaw = math.atan2(direction.x, math.max(0.001f, direction.z));
            yaw = math.clamp(yaw, -math.radians(MaximumYawDegrees), math.radians(MaximumYawDegrees));
            float horizontalLength = math.length(direction.xz);
            float pitch = math.atan2(direction.y, math.max(0.001f, horizontalLength));
            pitch = math.clamp(
                pitch,
                math.radians(MinimumPitchDegrees),
                math.radians(MaximumPitchDegrees));
            float cosPitch = math.cos(pitch);
            return new float3(
                math.sin(yaw) * cosPitch,
                math.sin(pitch),
                math.cos(yaw) * cosPitch);
        }

        private static EarthResponsiveHandTargetSample SampleOrFallback(
            in EarthResponsiveHandTargetState state) => state.IsInitialized
            ? ToSample(in state)
            : new EarthResponsiveHandTargetSample(new float3(0f, 0f, 1f), 0.48f, 0.15f);

        private static EarthResponsiveHandTargetSample ToSample(
            in EarthResponsiveHandTargetState state) => new EarthResponsiveHandTargetSample(
            state.LocalAim,
            state.ReachMeters,
            state.HandSpreadMeters);

        private static float3 RotateTowards(float3 current, float3 target, float maximumRadians)
        {
            current = math.normalizesafe(current, new float3(0f, 0f, 1f));
            target = math.normalizesafe(target, new float3(0f, 0f, 1f));
            float dot = math.clamp(math.dot(current, target), -1f, 1f);
            float angle = math.acos(dot);
            if (angle <= 0.00001f || maximumRadians >= angle) return target;
            if (maximumRadians <= 0f) return current;

            float t = maximumRadians / angle;
            float sinAngle = math.sin(angle);
            if (math.abs(sinAngle) <= 0.00001f)
                return math.normalizesafe(math.lerp(current, target, t), target);
            float3 result = (math.sin((1f - t) * angle) / sinAngle) * current +
                            (math.sin(t * angle) / sinAngle) * target;
            return math.normalizesafe(result, target);
        }

        private static float MoveTowards(float current, float target, float maximumDelta)
        {
            current = math.isfinite(current) ? current : target;
            float delta = target - current;
            if (math.abs(delta) <= maximumDelta) return target;
            return current + math.sign(delta) * maximumDelta;
        }

        private static float ClampFinite(float value, float minimum, float maximum, float fallback) =>
            math.clamp(math.isfinite(value) ? value : fallback, minimum, maximum);
    }
}
