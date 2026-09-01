using Elemental.Simulation.Characters;

namespace Elemental.Presentation.Animation
{
    public enum EarthTransitionProfileResolution : byte
    {
        LegacyPolicy = 0,
        AuthoredPair = 1,
        GenericFallback = 2,
        Queued = 3,
        QueueRejected = 4
    }

    public readonly struct EarthTransitionDirectorDiagnostics
    {
        public EarthTransitionDirectorDiagnostics(
            bool profileEnabled,
            bool queueEnabled,
            int queuedRequestCount,
            EarthTransitionProfileResolution lastResolution,
            int lastPairIndex,
            in EarthTransitionRule lastRule,
            uint authoredPairExecutionCount,
            uint genericFallbackExecutionCount,
            uint queuedRequestCountTotal,
            uint dequeuedExecutionCount,
            uint queueRejectionCount,
            bool motionCatalogConfigured,
            int runtimeLayerCount,
            int verifiedRuntimeLayerCount,
            int inactiveRuntimeLayerCount,
            int unresolvedRuntimeLayerCount,
            in EarthMotionStateResolution baseLayerMotion,
            uint motionResolutionCount,
            uint motionResolutionMissCount,
            bool lastAuthoredPairProfilesVerified,
            int lastPairSourceProfileIndex,
            int lastPairDestinationProfileIndex)
        {
            ProfileEnabled = profileEnabled;
            QueueEnabled = queueEnabled;
            QueuedRequestCount = queuedRequestCount;
            LastResolution = lastResolution;
            LastPairIndex = lastPairIndex;
            LastRule = lastRule;
            AuthoredPairExecutionCount = authoredPairExecutionCount;
            GenericFallbackExecutionCount = genericFallbackExecutionCount;
            QueuedRequestCountTotal = queuedRequestCountTotal;
            DequeuedExecutionCount = dequeuedExecutionCount;
            QueueRejectionCount = queueRejectionCount;
            MotionCatalogConfigured = motionCatalogConfigured;
            RuntimeLayerCount = runtimeLayerCount;
            VerifiedRuntimeLayerCount = verifiedRuntimeLayerCount;
            InactiveRuntimeLayerCount = inactiveRuntimeLayerCount;
            UnresolvedRuntimeLayerCount = unresolvedRuntimeLayerCount;
            BaseLayerMotion = baseLayerMotion;
            MotionResolutionCount = motionResolutionCount;
            MotionResolutionMissCount = motionResolutionMissCount;
            LastAuthoredPairProfilesVerified = lastAuthoredPairProfilesVerified;
            LastPairSourceProfileIndex = lastPairSourceProfileIndex;
            LastPairDestinationProfileIndex = lastPairDestinationProfileIndex;
        }

        public bool ProfileEnabled { get; }
        public bool QueueEnabled { get; }
        public int QueuedRequestCount { get; }
        public EarthTransitionProfileResolution LastResolution { get; }
        public int LastPairIndex { get; }
        public EarthTransitionRule LastRule { get; }
        public uint AuthoredPairExecutionCount { get; }
        public uint GenericFallbackExecutionCount { get; }
        public uint QueuedRequestCountTotal { get; }
        public uint DequeuedExecutionCount { get; }
        public uint QueueRejectionCount { get; }
        public bool MotionCatalogConfigured { get; }
        public int RuntimeLayerCount { get; }
        public int VerifiedRuntimeLayerCount { get; }
        public int InactiveRuntimeLayerCount { get; }
        public int UnresolvedRuntimeLayerCount { get; }
        public EarthMotionStateResolution BaseLayerMotion { get; }
        public uint MotionResolutionCount { get; }
        public uint MotionResolutionMissCount { get; }
        public bool LastAuthoredPairProfilesVerified { get; }
        public int LastPairSourceProfileIndex { get; }
        public int LastPairDestinationProfileIndex { get; }
    }
}
