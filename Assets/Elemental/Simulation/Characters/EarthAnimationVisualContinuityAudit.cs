using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public readonly struct EarthAnimationVisualContinuitySample
    {
        public EarthAnimationVisualContinuitySample(
            float deltaTime,
            bool grounded,
            bool locomoting,
            bool turningInPlace,
            float3 leftFootLocal,
            float3 rightFootLocal,
            quaternion leftAnkleLocal,
            quaternion rightAnkleLocal,
            float3 leftFootWorld,
            float3 rightFootWorld,
            float leftIkWeight,
            float rightIkWeight,
            bool leftLocked,
            bool rightLocked)
        {
            DeltaTime = math.clamp(
                math.isfinite(deltaTime) ? deltaTime : 0f,
                0.0001f,
                0.1f);
            Grounded = grounded;
            Locomoting = locomoting;
            TurningInPlace = turningInPlace;
            LeftFootLocal = SelectFinite(leftFootLocal, float3.zero);
            RightFootLocal = SelectFinite(rightFootLocal, float3.zero);
            LeftAnkleLocal = NormalizeSafe(leftAnkleLocal);
            RightAnkleLocal = NormalizeSafe(rightAnkleLocal);
            LeftFootWorld = SelectFinite(leftFootWorld, float3.zero);
            RightFootWorld = SelectFinite(rightFootWorld, float3.zero);
            LeftIkWeight = math.saturate(math.isfinite(leftIkWeight) ? leftIkWeight : 0f);
            RightIkWeight = math.saturate(math.isfinite(rightIkWeight) ? rightIkWeight : 0f);
            LeftLocked = leftLocked;
            RightLocked = rightLocked;
        }

        public float DeltaTime { get; }
        public bool Grounded { get; }
        public bool Locomoting { get; }
        public bool TurningInPlace { get; }
        public float3 LeftFootLocal { get; }
        public float3 RightFootLocal { get; }
        public quaternion LeftAnkleLocal { get; }
        public quaternion RightAnkleLocal { get; }
        public float3 LeftFootWorld { get; }
        public float3 RightFootWorld { get; }
        public float LeftIkWeight { get; }
        public float RightIkWeight { get; }
        public bool LeftLocked { get; }
        public bool RightLocked { get; }

        private static float3 SelectFinite(float3 value, float3 fallback) =>
            math.select(fallback, value, math.isfinite(value));

        private static quaternion NormalizeSafe(quaternion value)
        {
            float lengthSquared = math.lengthsq(value.value);
            return math.isfinite(lengthSquared) && lengthSquared > 0.000001f
                ? math.normalize(value)
                : quaternion.identity;
        }
    }

    public struct EarthAnimationVisualContinuityState
    {
        internal bool HasPrevious;
        internal EarthAnimationVisualContinuitySample Previous;
        internal bool LeftReleaseObserved;
        internal bool RightReleaseObserved;
        internal float LeftReleasedNormalizedFrames;
        internal float RightReleasedNormalizedFrames;
        internal int TransitionFramesRemaining;
        internal int SampleCount;
        internal int SwingResidualViolationFrames;
        internal int PivotWithoutPlantedFootFrames;
        internal float MaximumSwingIkAfterTwoFrames;
        internal float MaximumAnkleStepDegrees;
        internal float MaximumStartStopFootStepMeters;
        internal float MaximumPivotPlantedFootStepMeters;
    }

    public readonly struct EarthAnimationVisualContinuitySummary
    {
        internal EarthAnimationVisualContinuitySummary(
            in EarthAnimationVisualContinuityState state)
        {
            SampleCount = state.SampleCount;
            SwingResidualViolationFrames = state.SwingResidualViolationFrames;
            PivotWithoutPlantedFootFrames = state.PivotWithoutPlantedFootFrames;
            MaximumSwingIkAfterTwoFrames = state.MaximumSwingIkAfterTwoFrames;
            MaximumAnkleStepDegrees = state.MaximumAnkleStepDegrees;
            MaximumStartStopFootStepMeters = state.MaximumStartStopFootStepMeters;
            MaximumPivotPlantedFootStepMeters = state.MaximumPivotPlantedFootStepMeters;
        }

        public int SampleCount { get; }
        public int SwingResidualViolationFrames { get; }
        public int PivotWithoutPlantedFootFrames { get; }
        public float MaximumSwingIkAfterTwoFrames { get; }
        public float MaximumAnkleStepDegrees { get; }
        public float MaximumStartStopFootStepMeters { get; }
        public float MaximumPivotPlantedFootStepMeters { get; }

        public bool HardGatesPassed =>
            SwingResidualViolationFrames == 0 &&
            PivotWithoutPlantedFootFrames == 0 &&
            MaximumSwingIkAfterTwoFrames <= EarthAnimationVisualContinuityAudit.MaximumSwingIk &&
            MaximumAnkleStepDegrees <= EarthAnimationVisualContinuityAudit.MaximumAnkleStepDegrees &&
            MaximumStartStopFootStepMeters <= EarthAnimationVisualContinuityAudit.MaximumStartStopFootStepMeters &&
            MaximumPivotPlantedFootStepMeters <= EarthAnimationVisualContinuityAudit.MaximumPivotFootStepMeters;
    }

    /// <summary>
    /// Pure visual continuity gate for defects that contact ownership alone cannot
    /// see: a released IK chain fighting the swing clip, ankle rotation pops,
    /// start/stop pose jumps and a pivot foot skating across the support.
    /// Measurements are normalized to an equivalent 60 Hz rendered frame.
    /// </summary>
    public static class EarthAnimationVisualContinuityAudit
    {
        public const float MaximumSwingIk = 0.15f;
        public const float MaximumAnkleStepDegrees = 8f;
        public const float MaximumStartStopFootStepMeters = 0.055f;
        public const float MaximumPivotFootStepMeters = 0.020f;
        public const float SwingReleaseFrameBudget = 2f;

        public static EarthAnimationVisualContinuitySummary Step(
            ref EarthAnimationVisualContinuityState state,
            in EarthAnimationVisualContinuitySample sample)
        {
            state.SampleCount++;
            if (!state.HasPrevious)
            {
                state.Previous = sample;
                state.HasPrevious = true;
                return new EarthAnimationVisualContinuitySummary(in state);
            }

            float normalizedScale = math.clamp(
                (1f / 60f) / sample.DeltaTime,
                0.25f,
                4f);
            float normalizedFrames = sample.DeltaTime * 60f;
            UpdateReleaseAge(
                state.Previous.LeftLocked,
                sample.LeftLocked,
                normalizedFrames,
                ref state.LeftReleaseObserved,
                ref state.LeftReleasedNormalizedFrames);
            UpdateReleaseAge(
                state.Previous.RightLocked,
                sample.RightLocked,
                normalizedFrames,
                ref state.RightReleaseObserved,
                ref state.RightReleasedNormalizedFrames);

            if (sample.Locomoting)
            {
                CheckSwingResidual(
                    state.LeftReleaseObserved,
                    state.LeftReleasedNormalizedFrames,
                    sample.LeftIkWeight,
                    ref state);
                CheckSwingResidual(
                    state.RightReleaseObserved,
                    state.RightReleasedNormalizedFrames,
                    sample.RightIkWeight,
                    ref state);
            }

            if (sample.Locomoting || state.Previous.Locomoting ||
                sample.TurningInPlace || state.Previous.TurningInPlace)
            {
                float ankleStep = math.max(
                    AngleDegrees(state.Previous.LeftAnkleLocal, sample.LeftAnkleLocal),
                    AngleDegrees(state.Previous.RightAnkleLocal, sample.RightAnkleLocal)) *
                    normalizedScale;
                state.MaximumAnkleStepDegrees = math.max(
                    state.MaximumAnkleStepDegrees,
                    ankleStep);
            }

            // A jump, dodge flight window or physical knockdown also toggles the
            // locomotion flag, but those are authored action transitions rather
            // than a grounded start/stop. Keep this metric scoped to the actual
            // planted-ground seam it is intended to expose.
            if (sample.Grounded && state.Previous.Grounded &&
                !sample.TurningInPlace && !state.Previous.TurningInPlace &&
                sample.Locomoting != state.Previous.Locomoting)
                state.TransitionFramesRemaining = 3;
            else if (!sample.Grounded || !state.Previous.Grounded ||
                     sample.TurningInPlace || state.Previous.TurningInPlace)
                state.TransitionFramesRemaining = 0;
            if (state.TransitionFramesRemaining > 0)
            {
                float localStep = math.max(
                    math.distance(sample.LeftFootLocal, state.Previous.LeftFootLocal),
                    math.distance(sample.RightFootLocal, state.Previous.RightFootLocal)) *
                    normalizedScale;
                state.MaximumStartStopFootStepMeters = math.max(
                    state.MaximumStartStopFootStepMeters,
                    localStep);
                state.TransitionFramesRemaining--;
            }

            if (sample.TurningInPlace)
            {
                if (!sample.LeftLocked && !sample.RightLocked)
                    state.PivotWithoutPlantedFootFrames++;
                float pivotStep = 0f;
                // The capture frame establishes the stance anchor; drift is the
                // change while the same leg remains planted on the following
                // frames. Counting free-pose -> first lock as drift mislabeled
                // the intended capture correction as a 40 cm slide.
                if (sample.LeftLocked && state.Previous.LeftLocked)
                    pivotStep = math.max(
                        pivotStep,
                        math.distance(sample.LeftFootWorld, state.Previous.LeftFootWorld) *
                        normalizedScale);
                if (sample.RightLocked && state.Previous.RightLocked)
                    pivotStep = math.max(
                        pivotStep,
                        math.distance(sample.RightFootWorld, state.Previous.RightFootWorld) *
                        normalizedScale);
                state.MaximumPivotPlantedFootStepMeters = math.max(
                    state.MaximumPivotPlantedFootStepMeters,
                    pivotStep);
            }

            state.Previous = sample;
            return new EarthAnimationVisualContinuitySummary(in state);
        }

        public static EarthAnimationVisualContinuitySummary Snapshot(
            in EarthAnimationVisualContinuityState state) =>
            new EarthAnimationVisualContinuitySummary(in state);

        private static void UpdateReleaseAge(
            bool wasLocked,
            bool locked,
            float normalizedFrames,
            ref bool releaseObserved,
            ref float releasedFrames)
        {
            if (locked)
            {
                releaseObserved = false;
                releasedFrames = 0f;
                return;
            }
            if (wasLocked)
            {
                releaseObserved = true;
                releasedFrames = 0f;
            }
            if (releaseObserved) releasedFrames += normalizedFrames;
        }

        private static void CheckSwingResidual(
            bool releaseObserved,
            float releasedFrames,
            float weight,
            ref EarthAnimationVisualContinuityState state)
        {
            if (!releaseObserved || releasedFrames < SwingReleaseFrameBudget) return;
            state.MaximumSwingIkAfterTwoFrames = math.max(
                state.MaximumSwingIkAfterTwoFrames,
                weight);
            if (weight > MaximumSwingIk + 0.0001f)
                state.SwingResidualViolationFrames++;
        }

        private static float AngleDegrees(quaternion from, quaternion to)
        {
            quaternion delta = math.mul(math.inverse(from), to);
            float cosine = math.clamp(math.abs(delta.value.w), 0f, 1f);
            return math.degrees(2f * math.acos(cosine));
        }
    }
}
