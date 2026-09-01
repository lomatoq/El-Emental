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
            uint queueRejectionCount)
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
    }
}
