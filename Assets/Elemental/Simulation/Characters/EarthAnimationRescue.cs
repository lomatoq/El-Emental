using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthAnimationPhase : byte
    {
        GroundedIdle = 0,
        TurnStart = 1,
        TurnSustain = 2,
        TurnSettle = 3,
        LocomotionStart = 4,
        LocomotionLoop = 5,
        LocomotionStop = 6,
        JumpAnticipation = 7,
        Rising = 8,
        Apex = 9,
        Falling = 10,
        PreLanding = 11,
        LandingContact = 12,
        LandingRecovery = 13,
        SurfEnter = 14,
        SurfLoop = 15,
        SurfExit = 16,
        CastAnticipation = 17,
        CastCommit = 18,
        CastFollowThrough = 19,
        CastRecovery = 20,
        Stagger = 21,
        Ragdoll = 22,
        GetUp = 23
    }

    public enum EarthLandingStyle : byte
    {
        None = 0,
        Soft = 1,
        Moving = 2,
        Hard = 3
    }

    public readonly struct EarthLandingCandidateSnapshot
    {
        public EarthLandingCandidateSnapshot(
            bool isValid,
            float timeToContact,
            float impactSpeed,
            float planarSpeed,
            float3 point,
            float3 normal,
            float3 surfacePointVelocity,
            uint surfaceId,
            uint generation,
            bool movingSupport)
        {
            IsValid = isValid && math.isfinite(timeToContact) && math.isfinite(impactSpeed) &&
                      math.all(math.isfinite(point)) && math.all(math.isfinite(normal)) &&
                      math.all(math.isfinite(surfacePointVelocity));
            TimeToContact = math.max(0f, timeToContact);
            ImpactSpeed = math.max(0f, impactSpeed);
            PlanarSpeed = math.max(0f, planarSpeed);
            Point = point;
            Normal = math.normalizesafe(normal, new float3(0f, 1f, 0f));
            SurfacePointVelocity = surfacePointVelocity;
            SurfaceId = surfaceId;
            Generation = generation;
            MovingSupport = movingSupport;
        }

        public bool IsValid { get; }
        public float TimeToContact { get; }
        public float ImpactSpeed { get; }
        public float PlanarSpeed { get; }
        public float3 Point { get; }
        public float3 Normal { get; }
        public float3 SurfacePointVelocity { get; }
        public uint SurfaceId { get; }
        public uint Generation { get; }
        public bool MovingSupport { get; }
    }

    public readonly struct EarthAnimationRescueTuning
    {
        public EarthAnimationRescueTuning(
            float minimumAnticipationSeconds,
            float maximumAnticipationSeconds,
            float candidateLossGraceSeconds,
            float softImpactSpeed,
            float hardImpactSpeed,
            float movingPlanarSpeed,
            float movingRecoverySeconds,
            float softRecoverySeconds,
            float hardRecoverySeconds)
        {
            MinimumAnticipationSeconds = math.max(0.01f, minimumAnticipationSeconds);
            MaximumAnticipationSeconds = math.max(MinimumAnticipationSeconds, maximumAnticipationSeconds);
            CandidateLossGraceSeconds = math.clamp(candidateLossGraceSeconds, 0f, 0.25f);
            SoftImpactSpeed = math.max(0.1f, softImpactSpeed);
            HardImpactSpeed = math.max(SoftImpactSpeed + 0.1f, hardImpactSpeed);
            MovingPlanarSpeed = math.max(0.1f, movingPlanarSpeed);
            MovingRecoverySeconds = math.max(0.01f, movingRecoverySeconds);
            SoftRecoverySeconds = math.max(0.01f, softRecoverySeconds);
            HardRecoverySeconds = math.max(0.01f, hardRecoverySeconds);
        }

        public float MinimumAnticipationSeconds { get; }
        public float MaximumAnticipationSeconds { get; }
        public float CandidateLossGraceSeconds { get; }
        public float SoftImpactSpeed { get; }
        public float HardImpactSpeed { get; }
        public float MovingPlanarSpeed { get; }
        public float MovingRecoverySeconds { get; }
        public float SoftRecoverySeconds { get; }
        public float HardRecoverySeconds { get; }

        public float AnticipationFor(float impactSpeed)
        {
            float range = math.max(0.001f, HardImpactSpeed - SoftImpactSpeed);
            float speed01 = math.saturate((impactSpeed - SoftImpactSpeed) / range);
            return math.lerp(MinimumAnticipationSeconds, MaximumAnticipationSeconds, speed01);
        }

        public EarthLandingStyle StyleFor(float impactSpeed, float planarSpeed)
        {
            if (impactSpeed >= HardImpactSpeed) return EarthLandingStyle.Hard;
            return planarSpeed >= MovingPlanarSpeed ? EarthLandingStyle.Moving : EarthLandingStyle.Soft;
        }

        public float RecoveryFor(EarthLandingStyle style) => style switch
        {
            EarthLandingStyle.Hard => HardRecoverySeconds,
            EarthLandingStyle.Moving => MovingRecoverySeconds,
            _ => SoftRecoverySeconds
        };

        public static EarthAnimationRescueTuning Default => new EarthAnimationRescueTuning(
            0.06f, 0.18f, 0.12f, 4.5f, 7.5f, 1.2f, 0.08f, 0.16f, 0.34f);
    }

    public struct EarthAnimationRescueState
    {
        public EarthAnimationPhase Phase;
        public EarthLandingStyle LandingStyle;
        public float PhaseSeconds;
        public float CandidateLostSeconds;
        public float MinimumAirVerticalSpeed;
        public float LastPredictedImpactSpeed;
        public float LastPredictedPlanarSpeed;
        public uint CandidateSurfaceId;
        public uint CandidateGeneration;
    }

    public readonly struct EarthAnimationRescueSample
    {
        public EarthAnimationRescueSample(
            EarthAnimationPhase phase,
            EarthLandingStyle landingStyle,
            bool phaseChanged,
            float recoverySeconds)
        {
            Phase = phase;
            LandingStyle = landingStyle;
            PhaseChanged = phaseChanged;
            RecoverySeconds = recoverySeconds;
        }

        public EarthAnimationPhase Phase { get; }
        public EarthLandingStyle LandingStyle { get; }
        public bool PhaseChanged { get; }
        public float RecoverySeconds { get; }
    }

    public static class EarthLandingClipPhaseAlignment
    {
        public static float ResolveStartSeconds(
            float contactTimeSeconds,
            float predictedTimeToContact,
            bool hasPrediction)
        {
            float contact = math.max(0f, contactTimeSeconds);
            float lead = hasPrediction ? math.max(0f, predictedTimeToContact) : 0f;
            return math.max(0f, contact - lead);
        }
    }

    public static class EarthAnimationStateResolver
    {
        public static EarthAnimationRescueSample Step(
            ref EarthAnimationRescueState state,
            in EarthAnimationRescueTuning tuning,
            in EarthLandingCandidateSnapshot candidate,
            bool grounded,
            bool surfing,
            bool ragdoll,
            float verticalSpeed,
            float supportRelativePlanarSpeed,
            float deltaTime)
        {
            deltaTime = math.max(0.0001f, deltaTime);
            EarthAnimationPhase previous = state.Phase;
            state.PhaseSeconds += deltaTime;
            if (!grounded) state.MinimumAirVerticalSpeed = math.min(state.MinimumAirVerticalSpeed, verticalSpeed);

            if (ragdoll)
            {
                Enter(ref state, EarthAnimationPhase.Ragdoll);
                state.LandingStyle = EarthLandingStyle.None;
            }
            else if (surfing)
            {
                Enter(ref state, EarthAnimationPhase.SurfLoop);
                state.LandingStyle = EarthLandingStyle.None;
            }
            else if (grounded)
            {
                if (IsAirborne(previous))
                {
                    float observedImpact = math.max(state.LastPredictedImpactSpeed, -state.MinimumAirVerticalSpeed);
                    state.LandingStyle = tuning.StyleFor(
                        observedImpact,
                        math.max(state.LastPredictedPlanarSpeed, supportRelativePlanarSpeed));
                    Enter(ref state, EarthAnimationPhase.LandingContact);
                }
                else if (previous == EarthAnimationPhase.LandingContact)
                {
                    // Severity is fixed at confirmed contact. MinimumAirVerticalSpeed
                    // is reset after that tick, so classifying again here could turn a
                    // moving/hard landing into soft during its own recovery.
                    if (state.PhaseSeconds >= 0.02f)
                        Enter(ref state, EarthAnimationPhase.LandingRecovery);
                }
                else if (previous == EarthAnimationPhase.LandingRecovery)
                {
                    if (state.PhaseSeconds >= tuning.RecoveryFor(state.LandingStyle))
                        Enter(ref state, supportRelativePlanarSpeed > 0.12f
                            ? EarthAnimationPhase.LocomotionLoop
                            : EarthAnimationPhase.GroundedIdle);
                }
                else
                {
                    Enter(ref state, supportRelativePlanarSpeed > 0.12f
                        ? EarthAnimationPhase.LocomotionLoop
                        : EarthAnimationPhase.GroundedIdle);
                    state.LandingStyle = EarthLandingStyle.None;
                    state.LastPredictedImpactSpeed = 0f;
                    state.LastPredictedPlanarSpeed = 0f;
                }

                state.CandidateLostSeconds = 0f;
                state.MinimumAirVerticalSpeed = 0f;
            }
            else if (verticalSpeed > 0.35f)
            {
                if (!IsAirborne(previous))
                {
                    state.LastPredictedImpactSpeed = 0f;
                    state.LastPredictedPlanarSpeed = 0f;
                    state.CandidateSurfaceId = 0u;
                    state.CandidateGeneration = 0u;
                }
                Enter(ref state, EarthAnimationPhase.Rising);
                state.CandidateLostSeconds = 0f;
                state.LandingStyle = EarthLandingStyle.None;
            }
            else
            {
                bool sameCandidate = candidate.IsValid &&
                                     (state.CandidateSurfaceId == 0u ||
                                      (state.CandidateSurfaceId == candidate.SurfaceId &&
                                       state.CandidateGeneration == candidate.Generation));
                if (candidate.IsValid && candidate.TimeToContact <= tuning.AnticipationFor(candidate.ImpactSpeed))
                {
                    // A one-frame candidate loss must not erase the strongest
                    // evidence collected during this airborne phase. Otherwise a
                    // fast moving/hard landing can downgrade to soft immediately
                    // before contact when the curved support sweep flickers.
                    state.LastPredictedImpactSpeed = math.max(
                        state.LastPredictedImpactSpeed,
                        candidate.ImpactSpeed);
                    state.LastPredictedPlanarSpeed = math.max(
                        state.LastPredictedPlanarSpeed,
                        candidate.PlanarSpeed);
                    state.CandidateSurfaceId = candidate.SurfaceId;
                    state.CandidateGeneration = candidate.Generation;
                    state.CandidateLostSeconds = 0f;
                    state.LandingStyle = tuning.StyleFor(
                        state.LastPredictedImpactSpeed,
                        state.LastPredictedPlanarSpeed);
                    Enter(ref state, EarthAnimationPhase.PreLanding);
                }
                else if (previous == EarthAnimationPhase.PreLanding && !sameCandidate)
                {
                    state.CandidateLostSeconds += deltaTime;
                    if (state.CandidateLostSeconds > tuning.CandidateLossGraceSeconds)
                    {
                        Enter(ref state, EarthAnimationPhase.Falling);
                        state.LandingStyle = EarthLandingStyle.None;
                        state.CandidateSurfaceId = 0u;
                        state.CandidateGeneration = 0u;
                    }
                }
                else
                {
                    Enter(ref state, math.abs(verticalSpeed) < 0.3f
                        ? EarthAnimationPhase.Apex
                        : EarthAnimationPhase.Falling);
                    state.LandingStyle = EarthLandingStyle.None;
                }
            }

            return new EarthAnimationRescueSample(
                state.Phase,
                state.LandingStyle,
                state.Phase != previous,
                tuning.RecoveryFor(state.LandingStyle));
        }

        private static bool IsAirborne(EarthAnimationPhase phase) =>
            phase == EarthAnimationPhase.Rising || phase == EarthAnimationPhase.Apex ||
            phase == EarthAnimationPhase.Falling || phase == EarthAnimationPhase.PreLanding;

        private static void Enter(ref EarthAnimationRescueState state, EarthAnimationPhase phase)
        {
            if (state.Phase == phase) return;
            state.Phase = phase;
            state.PhaseSeconds = 0f;
        }
    }

    public struct EarthScalarPresentationState
    {
        public float Value;
        public float Velocity;
    }

    public readonly struct EarthTurnPresentationSample
    {
        public EarthTurnPresentationSample(float value, bool pivotActive)
        {
            Value = value;
            PivotActive = pivotActive;
        }

        public float Value { get; }
        public bool PivotActive { get; }
    }

    public static class EarthAnimationParameterFilter
    {
        public static EarthTurnPresentationSample StepTurn(
            ref EarthScalarPresentationState state,
            float measuredYawRateDegrees,
            float rawTurnInput,
            float referenceYawRateDegrees,
            float measuredFallbackThresholdDegrees,
            float deadZone,
            float enterSeconds,
            float releaseSeconds,
            float deltaTime)
        {
            float reference = math.max(1f, referenceYawRateDegrees);
            float target = math.abs(measuredYawRateDegrees) >= math.max(0f, measuredFallbackThresholdDegrees)
                ? math.clamp(measuredYawRateDegrees / reference, -1f, 1f)
                : math.clamp(rawTurnInput, -1f, 1f);
            if (math.abs(target) < math.max(0f, deadZone)) target = 0f;
            float smoothTime = math.abs(target) > math.abs(state.Value)
                ? math.max(0.01f, enterSeconds)
                : math.max(0.12f, releaseSeconds);
            float previous = state.Value;
            state.Value = SmoothDamp(state.Value, target, ref state.Velocity, smoothTime, deltaTime);
            if (previous > 0f && state.Value < 0f) state.Value = 0f;
            else if (previous < 0f && state.Value > 0f) state.Value = 0f;
            if (target == 0f && math.abs(state.Value) < deadZone * 0.25f)
            {
                state.Value = 0f;
                state.Velocity = 0f;
            }
            return new EarthTurnPresentationSample(state.Value, math.abs(state.Value) >= deadZone);
        }

        public static float StepSpeed(
            ref EarthScalarPresentationState state,
            float targetSpeed,
            float accelerationSeconds,
            float decelerationSeconds,
            float deltaTime)
        {
            float response = math.abs(targetSpeed) > math.abs(state.Value)
                ? math.max(0.01f, accelerationSeconds)
                : math.max(0.01f, decelerationSeconds);
            state.Value = SmoothDamp(state.Value, targetSpeed, ref state.Velocity, response, deltaTime);
            if (math.abs(targetSpeed) < 0.001f && math.abs(state.Value) < 0.005f)
            {
                state.Value = 0f;
                state.Velocity = 0f;
            }
            return state.Value;
        }

        private static float SmoothDamp(float current, float target, ref float velocity, float smoothTime, float deltaTime)
        {
            smoothTime = math.max(0.0001f, smoothTime);
            deltaTime = math.max(0.0001f, deltaTime);
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float decay = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float temporary = (velocity + omega * change) * deltaTime;
            velocity = (velocity - omega * temporary) * decay;
            return target + (change + temporary) * decay;
        }
    }

    public static class EarthPresentationSupportSolver
    {
        public static SupportFrameSnapshot Extrapolate(in SupportFrameSnapshot support, float seconds)
        {
            if (!support.IsValid || seconds <= 0f) return support;
            float dt = math.min(0.05f, seconds);
            float3 position = support.Position + support.LinearVelocity * dt;
            float angularSpeed = math.length(support.AngularVelocity);
            quaternion rotation = support.Rotation;
            if (angularSpeed > 0.0001f)
                rotation = math.mul(quaternion.AxisAngle(support.AngularVelocity / angularSpeed, angularSpeed * dt), rotation);
            return new SupportFrameSnapshot(
                support.SurfaceId,
                support.Generation,
                position,
                rotation,
                support.LinearVelocity,
                support.AngularVelocity,
                support.ContactPointVelocity,
                support.Up,
                support.Emerging,
                support.Discontinuous);
        }
    }
}
