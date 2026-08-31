namespace Elemental.Presentation.Animation
{
    /// <summary>
    /// Allocation-free per-frame evidence written by <see cref="EarthAnimationGraph"/>.
    /// Gate-1 capture code can copy the fixed ring into a caller-owned buffer.
    /// </summary>
    public readonly struct EarthAnimationGraphCaptureSample
    {
        public EarthAnimationGraphCaptureSample(
            int frame,
            float unscaledTime,
            in EarthAnimationGraphDiagnostics diagnostics)
        {
            Frame = frame;
            UnscaledTime = unscaledTime;
            GraphValid = diagnostics.GraphValid;
            TopologyValid = diagnostics.TopologyValid;
            RigLayersAppended = diagnostics.RigLayersAppended;
            RigOutputCount = diagnostics.RigOutputCount;
            RigOutputsUsePreviousInputs = diagnostics.RigOutputsUsePreviousInputs;
            LegacyFallbackActive = diagnostics.LegacyFallbackActive;
            FallbackReason = diagnostics.FallbackReason;
            InertiaActive = diagnostics.InertiaActive;
            MaximumPositionOffset = diagnostics.MaximumPositionOffset;
            MaximumRotationOffsetRadians = diagnostics.MaximumRotationOffsetRadians;
            RuntimeEnablePending = diagnostics.RuntimeEnablePending;
            RuntimeDisablePending = diagnostics.RuntimeDisablePending;
            PoseDisablePending = diagnostics.PoseDisablePending;
            StateHandoffCount = diagnostics.StateHandoffCount;
        }

        public int Frame { get; }
        public float UnscaledTime { get; }
        public bool GraphValid { get; }
        public bool TopologyValid { get; }
        public bool RigLayersAppended { get; }
        public int RigOutputCount { get; }
        public bool RigOutputsUsePreviousInputs { get; }
        public bool LegacyFallbackActive { get; }
        public EarthAnimationGraphFallbackReason FallbackReason { get; }
        public bool InertiaActive { get; }
        public float MaximumPositionOffset { get; }
        public float MaximumRotationOffsetRadians { get; }
        public bool RuntimeEnablePending { get; }
        public bool RuntimeDisablePending { get; }
        public bool PoseDisablePending { get; }
        public uint StateHandoffCount { get; }
    }

    public readonly struct EarthAnimationGraphCaptureSummary
    {
        public EarthAnimationGraphCaptureSummary(
            int sampleCount,
            int graphActiveFrames,
            int topologyFailureFrames,
            int legacyFallbackFrames,
            int inertiaActiveFrames,
            int pendingHandoffFrames,
            float maximumPositionOffset,
            float maximumRotationOffsetRadians,
            uint finalStateHandoffCount)
        {
            SampleCount = sampleCount;
            GraphActiveFrames = graphActiveFrames;
            TopologyFailureFrames = topologyFailureFrames;
            LegacyFallbackFrames = legacyFallbackFrames;
            InertiaActiveFrames = inertiaActiveFrames;
            PendingHandoffFrames = pendingHandoffFrames;
            MaximumPositionOffset = maximumPositionOffset;
            MaximumRotationOffsetRadians = maximumRotationOffsetRadians;
            FinalStateHandoffCount = finalStateHandoffCount;
        }

        public int SampleCount { get; }
        public int GraphActiveFrames { get; }
        public int TopologyFailureFrames { get; }
        public int LegacyFallbackFrames { get; }
        public int InertiaActiveFrames { get; }
        public int PendingHandoffFrames { get; }
        public float MaximumPositionOffset { get; }
        public float MaximumRotationOffsetRadians { get; }
        public uint FinalStateHandoffCount { get; }
    }
}
