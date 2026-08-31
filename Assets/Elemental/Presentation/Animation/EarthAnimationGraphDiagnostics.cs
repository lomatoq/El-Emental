namespace Elemental.Presentation.Animation
{
    /// <summary>
    /// Derived telemetry for the first A1 seams. It does not select states; the
    /// existing transition director remains the only semantic state owner.
    /// </summary>
    public enum EarthAnimationInertializationReason : byte
    {
        None = 0,
        RunToStop = 1,
        DirectionReverse = 2,
        TurnToSettle = 3,
        CastToLocomotion = 4,
        LocomotionToFlinch = 5,
        StaggerToLocomotion = 6,
        RecoveryToLocomotion = 7,
        FallToLanding = 8
    }

    public enum EarthAnimationGraphFallbackReason : byte
    {
        None = 0,
        FeatureDisabled = 1,
        MissingAnimator = 2,
        MissingController = 3,
        GraphBuildFailed = 4,
        ComponentDisabled = 5,
        InvalidTopology = 6
    }

    public readonly struct EarthAnimationGraphDiagnostics
    {
        public EarthAnimationGraphDiagnostics(
            bool graphValid,
            bool controllerPlayableValid,
            bool animationScriptPlayableValid,
            bool topologyValid,
            bool rigLayersAppended,
            bool legacyFallbackActive,
            EarthAnimationGraphFallbackReason fallbackReason,
            int trackedBoneCount,
            uint transitionRequestCount,
            uint interruptedTransitionCount,
            bool inertiaActive,
            float inertiaElapsedSeconds,
            float maximumPositionOffset,
            float maximumRotationOffsetRadians)
        {
            GraphValid = graphValid;
            ControllerPlayableValid = controllerPlayableValid;
            AnimationScriptPlayableValid = animationScriptPlayableValid;
            TopologyValid = topologyValid;
            RigLayersAppended = rigLayersAppended;
            LegacyFallbackActive = legacyFallbackActive;
            FallbackReason = fallbackReason;
            TrackedBoneCount = trackedBoneCount;
            TransitionRequestCount = transitionRequestCount;
            InterruptedTransitionCount = interruptedTransitionCount;
            InertiaActive = inertiaActive;
            InertiaElapsedSeconds = inertiaElapsedSeconds;
            MaximumPositionOffset = maximumPositionOffset;
            MaximumRotationOffsetRadians = maximumRotationOffsetRadians;
        }

        public bool GraphValid { get; }
        public bool ControllerPlayableValid { get; }
        public bool AnimationScriptPlayableValid { get; }
        public bool TopologyValid { get; }
        public bool RigLayersAppended { get; }
        public bool LegacyFallbackActive { get; }
        public EarthAnimationGraphFallbackReason FallbackReason { get; }
        public int TrackedBoneCount { get; }
        public uint TransitionRequestCount { get; }
        public uint InterruptedTransitionCount { get; }
        public bool InertiaActive { get; }
        public float InertiaElapsedSeconds { get; }
        public float MaximumPositionOffset { get; }
        public float MaximumRotationOffsetRadians { get; }
    }

    public struct EarthAnimationGraphControl
    {
        public byte UsePoseInertialization;
        public EarthAnimationBoneOwnership ActiveOwnership;
        public uint RequestSequence;
        public float PositionHalfLifeSeconds;
        public float RotationHalfLifeSeconds;
        public float MaximumDurationSeconds;
        public float MaximumPositionOffset;
        public float MaximumRotationOffsetRadians;
        public float MaximumLinearVelocity;
        public float MaximumAngularVelocity;
    }

    public struct EarthAnimationJobDiagnostics
    {
        public uint AppliedRequestSequence;
        public uint TransitionRequestCount;
        public uint InterruptedTransitionCount;
        public byte InertiaActive;
        public float ElapsedSeconds;
        public float MaximumPositionOffset;
        public float MaximumRotationOffsetRadians;
    }
}
