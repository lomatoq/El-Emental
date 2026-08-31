using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public static class EarthStableKneeHintSolver
    {
        public static float3 Solve(
            float3 hip,
            float3 characterForward,
            float3 characterRight,
            float3 localUp,
            float side,
            float3 previousDirection,
            float forwardOffset = 0.42f,
            float sideOffset = 0.18f)
        {
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 forward = characterForward - up * math.dot(characterForward, up);
            forward = math.normalizesafe(forward, OrthonormalTangent(up));
            float3 right = characterRight - up * math.dot(characterRight, up);
            right = math.normalizesafe(right, math.cross(up, forward));
            float3 desired = math.normalizesafe(
                forward * math.max(0.05f, forwardOffset) +
                right * (math.sign(side) * math.max(0.02f, sideOffset)) -
                up * 0.04f,
                forward);
            float3 previous = math.normalizesafe(previousDirection, desired);
            // Preserve the established bend side near a straight-leg singularity.
            // A sudden 180-degree hint flip is always worse than a short lag.
            if (math.dot(previous, desired) < -0.15f) desired = previous;
            return hip + desired * math.max(0.24f, forwardOffset + sideOffset);
        }

        private static float3 OrthonormalTangent(float3 normal)
        {
            float3 reference = math.abs(normal.y) < 0.92f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            return math.normalizesafe(math.cross(reference, normal), new float3(1f, 0f, 0f));
        }
    }

    public static class EarthSupportFootLockSolver
    {
        public static float3 CaptureLocal(float3 worldPosition, in SupportFrameSnapshot support)
        {
            if (!support.IsValid) return worldPosition;
            return math.rotate(math.inverse(support.Rotation), worldPosition - support.Position);
        }

        public static float3 ResolveWorld(float3 localPosition, in SupportFrameSnapshot support)
        {
            if (!support.IsValid) return localPosition;
            return support.Position + math.rotate(support.Rotation, localPosition);
        }

        public static bool SameSupport(
            uint capturedId,
            uint capturedGeneration,
            in SupportFrameSnapshot support) => support.IsValid
            ? support.SurfaceId == capturedId && support.Generation == capturedGeneration
            : capturedId == 0u && capturedGeneration == 0u;
    }

    public readonly struct EarthFootStanceState
    {
        public EarthFootStanceState(
            bool locked,
            bool armed,
            bool poseOwned,
            bool hasPreviousClearance,
            float previousClearance)
        {
            Locked = locked;
            Armed = armed;
            PoseOwned = poseOwned;
            HasPreviousClearance = hasPreviousClearance;
            PreviousClearance = math.isfinite(previousClearance)
                ? previousClearance
                : 0f;
        }

        public bool Locked { get; }
        public bool Armed { get; }
        public bool PoseOwned { get; }
        public bool HasPreviousClearance { get; }
        public float PreviousClearance { get; }
    }

    public readonly struct EarthFootStanceDecision
    {
        public EarthFootStanceDecision(
            in EarthFootStanceState state,
            bool captured,
            bool maintained)
        {
            State = state;
            Captured = captured;
            Maintained = maintained;
        }

        public EarthFootStanceState State { get; }
        public bool Locked => State.Locked;
        public bool Captured { get; }
        public bool Maintained { get; }
    }

    public static class EarthFootStanceGate
    {
        private const float MinimumCaptureClearance = -0.045f;
        private const float MaximumCaptureClearance = 0.055f;
        private const float ReleaseClearance = 0.135f;
        private const float RearmClearance = 0.11f;
        private const float DescendingTolerance = 0.002f;

        public static EarthFootStanceDecision Step(
            in EarthFootStanceState previous,
            bool supported,
            bool locomoting,
            bool poseLock,
            bool sameSupport,
            bool hasContact,
            float signedSoleClearance,
            bool otherLocomotionFootLocked)
        {
            bool validContact = supported && hasContact && math.isfinite(signedSoleClearance);
            if (!validContact)
                return new EarthFootStanceDecision(default, false, false);

            if (poseLock)
            {
                var poseState = new EarthFootStanceState(
                    true,
                    false,
                    true,
                    true,
                    signedSoleClearance);
                return new EarthFootStanceDecision(
                    in poseState,
                    !previous.Locked || !sameSupport,
                    previous.Locked && sameSupport);
            }

            // A two-foot casting/surf brace must hand ownership back to the gait
            // before ordinary locomotion may capture a stance foot.
            if (previous.PoseOwned)
            {
                var releasedPose = new EarthFootStanceState(
                    false,
                    signedSoleClearance >= RearmClearance,
                    false,
                    true,
                    signedSoleClearance);
                return new EarthFootStanceDecision(in releasedPose, false, false);
            }

            if (!locomoting)
            {
                var idle = new EarthFootStanceState(
                    false,
                    true,
                    false,
                    true,
                    signedSoleClearance);
                return new EarthFootStanceDecision(in idle, false, false);
            }

            if (previous.Locked && sameSupport && signedSoleClearance <= ReleaseClearance)
            {
                var maintained = new EarthFootStanceState(
                    true,
                    false,
                    false,
                    true,
                    signedSoleClearance);
                return new EarthFootStanceDecision(in maintained, false, true);
            }

            if (previous.Locked)
            {
                // Release itself is not a re-arm sample. The authored swing must
                // remain visibly high on a later frame before this foot is allowed
                // to descend into another stance capture.
                var released = new EarthFootStanceState(
                    false,
                    false,
                    false,
                    true,
                    signedSoleClearance);
                return new EarthFootStanceDecision(in released, false, false);
            }

            bool armed = previous.Armed || !previous.HasPreviousClearance ||
                         signedSoleClearance >= RearmClearance;
            bool descending = !previous.HasPreviousClearance ||
                              signedSoleClearance <=
                              previous.PreviousClearance + DescendingTolerance;
            bool capture = armed && !otherLocomotionFootLocked && descending &&
                           signedSoleClearance >= MinimumCaptureClearance &&
                           signedSoleClearance <= MaximumCaptureClearance;
            var next = new EarthFootStanceState(
                capture,
                capture ? false : armed,
                false,
                true,
                signedSoleClearance);
            return new EarthFootStanceDecision(in next, capture, false);
        }

        public static float ContactWeight(bool locomoting, bool locked, float signedSoleClearance)
        {
            if (locked) return 1f;
            if (!math.isfinite(signedSoleClearance)) return 0f;
            if (!locomoting) return 1f;
            return 1f - math.smoothstep(0.035f, 0.16f, signedSoleClearance);
        }

        // Compatibility helpers retained for older focused tests and tooling.
        public static bool ShouldKeepLock(
            bool supported,
            bool locomoting,
            float signedSoleClearance) =>
            supported && locomoting && math.isfinite(signedSoleClearance) &&
            signedSoleClearance <= ReleaseClearance;

        public static bool ShouldBeginLock(
            bool supported,
            bool locomoting,
            float signedSoleClearance) =>
            supported && locomoting && math.isfinite(signedSoleClearance) &&
            signedSoleClearance >= MinimumCaptureClearance &&
            signedSoleClearance <= MaximumCaptureClearance;
    }

    public static class EarthFootIkWeightBlend
    {
        private const float MaximumFrameStep = 0.28f;
        private const float MaximumReleaseFrameStep = 0.90f;

        public static float Step(
            float current,
            float target,
            float deltaTime,
            float responseSeconds)
        {
            current = math.saturate(math.isfinite(current) ? current : 0f);
            target = math.saturate(math.isfinite(target) ? target : 0f);
            float response = math.clamp(
                math.isfinite(responseSeconds) ? responseSeconds : 0.06f,
                0.02f,
                0.4f);
            float frameStep = math.min(
                MaximumFrameStep,
                math.max(0f, math.isfinite(deltaTime) ? deltaTime : 0f) / response);
            return current + math.clamp(target - current, -frameStep, frameStep);
        }

        /// <summary>
        /// Capturing a stance anchor must remain gentle, while releasing a swing
        /// chain must be nearly immediate so the authored gait is not pulled back
        /// toward its previous support-local anchor.
        /// </summary>
        public static float StepContact(
            float current,
            float target,
            float deltaTime,
            float captureResponseSeconds = 0.40f,
            float releaseResponseSeconds = 0.02f,
            float maximumCaptureFrameStep = MaximumFrameStep)
        {
            current = math.saturate(math.isfinite(current) ? current : 0f);
            target = math.saturate(math.isfinite(target) ? target : 0f);
            bool releasing = target < current;
            float response = math.clamp(
                releasing ? releaseResponseSeconds : captureResponseSeconds,
                0.01f,
                0.40f);
            float maximumStep = releasing
                ? MaximumReleaseFrameStep
                : math.clamp(maximumCaptureFrameStep, 0.01f, 1f);
            float frameStep = math.min(
                maximumStep,
                math.max(0f, math.isfinite(deltaTime) ? deltaTime : 0f) / response);
            return current + math.clamp(target - current, -frameStep, frameStep);
        }

        public static float EnforceSwingMaximum(
            float weight,
            bool locked,
            EarthFootContactReason reason,
            float maximumSwingWeight = 0.15f)
        {
            float safeWeight = math.saturate(math.isfinite(weight) ? weight : 0f);
            if (locked || reason != EarthFootContactReason.Swing) return safeWeight;
            return math.min(safeWeight, math.saturate(maximumSwingWeight));
        }
    }

    /// <summary>
    /// Frame-rate independent final ankle inertialization. This bounds the
    /// visible authored/IK seam without taking ownership from the foot contact
    /// controller or changing the planted target itself.
    /// </summary>
    public static class EarthAnkleRotationInertializer
    {
        public const float MaximumDegreesAt60Hz = 7.5f;

        public static quaternion Step(
            quaternion current,
            quaternion target,
            float deltaTime)
        {
            current = NormalizeSafe(current);
            target = NormalizeSafe(target);
            float dot = math.dot(current.value, target.value);
            if (dot < 0f)
            {
                target.value = -target.value;
                dot = -dot;
            }
            float angle = 2f * math.acos(math.clamp(dot, 0f, 1f));
            if (angle <= 0.000001f) return target;
            // A synchronous capture or hitch must not allow a 45-degree visible
            // ankle step. Above 60 Hz the rate remains time based; at/below
            // 60 Hz the per-rendered-pose bound wins.
            float boundedDeltaTime = math.min(
                math.clamp(
                    math.isfinite(deltaTime) ? deltaTime : 0f,
                    0f,
                    0.1f),
                1f / 60f);
            float maximumRadians = math.radians(MaximumDegreesAt60Hz) *
                                   boundedDeltaTime * 60f;
            float t = math.saturate(maximumRadians / angle);
            return math.normalize(math.slerp(current, target, t));
        }

        private static quaternion NormalizeSafe(quaternion value)
        {
            float lengthSquared = math.lengthsq(value.value);
            return math.isfinite(lengthSquared) && lengthSquared > 0.000001f
                ? math.normalize(value)
                : quaternion.identity;
        }
    }
}
