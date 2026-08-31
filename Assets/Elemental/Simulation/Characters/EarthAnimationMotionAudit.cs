using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public readonly struct EarthAnimationMotionSample
    {
        public EarthAnimationMotionSample(
            float deltaTime,
            float3 leftFootLocal,
            float3 rightFootLocal,
            float3 leftKneeDirection,
            float3 rightKneeDirection,
            float footIkWeight,
            float leftFootIkWeight,
            float rightFootIkWeight,
            float pelvisCorrection,
            bool leftFootLocked,
            bool rightFootLocked,
            bool supported,
            uint supportId,
            uint supportGeneration)
        {
            DeltaTime = deltaTime;
            LeftFootLocal = leftFootLocal;
            RightFootLocal = rightFootLocal;
            LeftKneeDirection = math.normalizesafe(leftKneeDirection, new float3(0f, -1f, 0f));
            RightKneeDirection = math.normalizesafe(rightKneeDirection, new float3(0f, -1f, 0f));
            FootIkWeight = math.saturate(math.isfinite(footIkWeight) ? footIkWeight : 0f);
            LeftFootIkWeight = math.saturate(
                math.isfinite(leftFootIkWeight) ? leftFootIkWeight : 0f);
            RightFootIkWeight = math.saturate(
                math.isfinite(rightFootIkWeight) ? rightFootIkWeight : 0f);
            PelvisCorrection = math.isfinite(pelvisCorrection) ? pelvisCorrection : 0f;
            LeftFootLocked = leftFootLocked;
            RightFootLocked = rightFootLocked;
            Supported = supported;
            SupportId = supportId;
            SupportGeneration = supportGeneration;
        }

        public float DeltaTime { get; }
        public float3 LeftFootLocal { get; }
        public float3 RightFootLocal { get; }
        public float3 LeftKneeDirection { get; }
        public float3 RightKneeDirection { get; }
        public float FootIkWeight { get; }
        public float LeftFootIkWeight { get; }
        public float RightFootIkWeight { get; }
        public float PelvisCorrection { get; }
        public bool LeftFootLocked { get; }
        public bool RightFootLocked { get; }
        public bool Supported { get; }
        public uint SupportId { get; }
        public uint SupportGeneration { get; }
    }

    public struct EarthAnimationMotionAuditState
    {
        internal bool HasPrevious;
        internal EarthAnimationMotionSample Previous;
        internal float3 PreviousLeftVelocity;
        internal float3 PreviousRightVelocity;
        internal float ElapsedSeconds;
        internal int SampleCount;
        internal int LeftLockTransitions;
        internal int RightLockTransitions;
        internal int BothLockedFrames;
        internal int UnsupportedFrames;
        internal int SupportTransitions;
        internal int DiscontinuityFrames;
        internal float MaximumLeftFootStep;
        internal float MaximumRightFootStep;
        internal float MaximumFootSpeed;
        internal float MaximumFootAcceleration;
        internal float MaximumKneeAngleStep;
        internal float MaximumPelvisStep;
        internal float MaximumIkWeightStep;
    }

    public readonly struct EarthAnimationMotionAuditSummary
    {
        internal EarthAnimationMotionAuditSummary(in EarthAnimationMotionAuditState state)
        {
            ElapsedSeconds = state.ElapsedSeconds;
            SampleCount = state.SampleCount;
            LeftLockTransitions = state.LeftLockTransitions;
            RightLockTransitions = state.RightLockTransitions;
            BothLockedFrames = state.BothLockedFrames;
            UnsupportedFrames = state.UnsupportedFrames;
            SupportTransitions = state.SupportTransitions;
            DiscontinuityFrames = state.DiscontinuityFrames;
            MaximumLeftFootStep = state.MaximumLeftFootStep;
            MaximumRightFootStep = state.MaximumRightFootStep;
            MaximumFootSpeed = state.MaximumFootSpeed;
            MaximumFootAcceleration = state.MaximumFootAcceleration;
            MaximumKneeAngleStep = state.MaximumKneeAngleStep;
            MaximumPelvisStep = state.MaximumPelvisStep;
            MaximumIkWeightStep = state.MaximumIkWeightStep;
        }

        public float ElapsedSeconds { get; }
        public int SampleCount { get; }
        public int LeftLockTransitions { get; }
        public int RightLockTransitions { get; }
        public int TotalLockTransitions => LeftLockTransitions + RightLockTransitions;
        public int BothLockedFrames { get; }
        public int UnsupportedFrames { get; }
        public int SupportTransitions { get; }
        public int DiscontinuityFrames { get; }
        public float MaximumLeftFootStep { get; }
        public float MaximumRightFootStep { get; }
        public float MaximumFootStep => math.max(MaximumLeftFootStep, MaximumRightFootStep);
        public float MaximumFootSpeed { get; }
        public float MaximumFootAcceleration { get; }
        public float MaximumKneeAngleStep { get; }
        public float MaximumPelvisStep { get; }
        public float MaximumIkWeightStep { get; }
    }

    public static class EarthAnimationMotionAudit
    {
        private const float DiscontinuousFootStep = 0.085f;
        private const float DiscontinuousKneeStepDegrees = 14f;
        private const float DiscontinuousPelvisStep = 0.022f;

        public static EarthAnimationMotionAuditSummary Step(
            ref EarthAnimationMotionAuditState state,
            in EarthAnimationMotionSample sample)
        {
            float deltaTime = math.clamp(
                math.isfinite(sample.DeltaTime) ? sample.DeltaTime : 0f,
                0.0001f,
                0.1f);
            state.ElapsedSeconds += deltaTime;
            state.SampleCount++;
            if (!sample.Supported) state.UnsupportedFrames++;
            if (sample.LeftFootLocked && sample.RightFootLocked) state.BothLockedFrames++;

            if (state.HasPrevious)
            {
                if (sample.LeftFootLocked != state.Previous.LeftFootLocked)
                    state.LeftLockTransitions++;
                if (sample.RightFootLocked != state.Previous.RightFootLocked)
                    state.RightLockTransitions++;
                if (sample.SupportId != state.Previous.SupportId ||
                    sample.SupportGeneration != state.Previous.SupportGeneration)
                    state.SupportTransitions++;

                // Animation gates compare an equivalent 60 Hz motion step, not
                // an arbitrary Editor render interval. Without normalization a
                // valid 30 ms sample is judged as two teleports while a 7 ms
                // sample receives an undeservedly loose threshold.
                float normalizedFrameScale = math.clamp(
                    (1f / 60f) / deltaTime,
                    0.25f,
                    4f);
                float rawLeftStep = math.distance(
                    sample.LeftFootLocal,
                    state.Previous.LeftFootLocal);
                float rawRightStep = math.distance(
                    sample.RightFootLocal,
                    state.Previous.RightFootLocal);
                float leftStep = rawLeftStep * normalizedFrameScale;
                float rightStep = rawRightStep * normalizedFrameScale;
                state.MaximumLeftFootStep = math.max(state.MaximumLeftFootStep, leftStep);
                state.MaximumRightFootStep = math.max(state.MaximumRightFootStep, rightStep);
                float3 leftVelocity = (sample.LeftFootLocal - state.Previous.LeftFootLocal) / deltaTime;
                float3 rightVelocity = (sample.RightFootLocal - state.Previous.RightFootLocal) / deltaTime;
                state.MaximumFootSpeed = math.max(
                    state.MaximumFootSpeed,
                    math.max(math.length(leftVelocity), math.length(rightVelocity)));
                if (state.SampleCount > 2)
                {
                    state.MaximumFootAcceleration = math.max(
                        state.MaximumFootAcceleration,
                        math.max(
                            math.distance(leftVelocity, state.PreviousLeftVelocity) / deltaTime,
                            math.distance(rightVelocity, state.PreviousRightVelocity) / deltaTime));
                }
                state.PreviousLeftVelocity = leftVelocity;
                state.PreviousRightVelocity = rightVelocity;

                float leftKneeStep = AngleDegrees(
                    state.Previous.LeftKneeDirection,
                    sample.LeftKneeDirection);
                float rightKneeStep = AngleDegrees(
                    state.Previous.RightKneeDirection,
                    sample.RightKneeDirection);
                float kneeStep = math.max(leftKneeStep, rightKneeStep) * normalizedFrameScale;
                float pelvisStep = math.abs(
                    sample.PelvisCorrection - state.Previous.PelvisCorrection) *
                    normalizedFrameScale;
                state.MaximumKneeAngleStep = math.max(state.MaximumKneeAngleStep, kneeStep);
                state.MaximumPelvisStep = math.max(state.MaximumPelvisStep, pelvisStep);
                state.MaximumIkWeightStep = math.max(
                    state.MaximumIkWeightStep,
                    math.max(
                        math.abs(sample.FootIkWeight - state.Previous.FootIkWeight),
                        math.max(
                            math.abs(sample.LeftFootIkWeight - state.Previous.LeftFootIkWeight),
                            math.abs(sample.RightFootIkWeight - state.Previous.RightFootIkWeight))) *
                    normalizedFrameScale);
                if (math.max(leftStep, rightStep) > DiscontinuousFootStep ||
                    kneeStep > DiscontinuousKneeStepDegrees ||
                    pelvisStep > DiscontinuousPelvisStep)
                    state.DiscontinuityFrames++;
            }

            state.Previous = sample;
            state.HasPrevious = true;
            return new EarthAnimationMotionAuditSummary(in state);
        }

        public static EarthAnimationMotionAuditSummary Snapshot(
            in EarthAnimationMotionAuditState state) =>
            new EarthAnimationMotionAuditSummary(in state);

        private static float AngleDegrees(float3 from, float3 to)
        {
            float cosine = math.clamp(math.dot(from, to), -1f, 1f);
            return math.degrees(math.acos(cosine));
        }
    }
}
