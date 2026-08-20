using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum EarthGestureTokenKind : byte
    {
        Invalid = 0, Tap = 1, DoubleTap = 2, Hold = 3, DragLinear = 4, DragArc = 5,
        ClosedLoop = 6, Flick = 7, PushToward = 8, PullToward = 9, CircleCW = 10,
        CircleCCW = 11, DirectionReversal = 12, ScrollPulseUp = 13, ScrollPulseDown = 14,
        ScrollFlickUp = 15, ScrollFlickDown = 16, ScrollOverscrollConfirm = 17, BraceStillness = 18
    }

    public readonly struct EarthGestureTargetContext
    {
        public EarthGestureTargetContext(uint stableId, uint generation, ushort capabilities)
        {
            StableId = stableId;
            Generation = generation;
            Capabilities = capabilities;
        }
        public uint StableId { get; }
        public uint Generation { get; }
        public ushort Capabilities { get; }
        public bool IsValid => StableId != 0u;
    }

    public readonly struct EarthGestureTokenFeatures
    {
        public EarthGestureTokenFeatures(
            float duration, float pathLength, float straightness, float curvatureIntegral,
            float signedArea, float2 direction, float closureRatio, uint geometryDigest)
        {
            Duration = math.max(0f, duration);
            PathLength = math.max(0f, pathLength);
            Straightness = math.saturate(straightness);
            CurvatureIntegral = math.max(0f, curvatureIntegral);
            SignedArea = signedArea;
            Direction = math.normalizesafe(direction);
            ClosureRatio = math.max(0f, closureRatio);
            GeometryDigest = geometryDigest;
        }
        public float Duration { get; }
        public float PathLength { get; }
        public float Straightness { get; }
        public float CurvatureIntegral { get; }
        public float SignedArea { get; }
        public float2 Direction { get; }
        public float ClosureRatio { get; }
        public uint GeometryDigest { get; }
    }

    public readonly struct EarthGestureToken
    {
        public EarthGestureToken(
            EarthGestureTokenKind kind,
            float confidence,
            in EarthGestureTokenFeatures features,
            float peakSpeed,
            int directionReversals,
            in EarthGestureTargetContext pointerDown,
            in EarthGestureTargetContext commit)
            : this(
                kind,
                confidence,
                in features,
                peakSpeed,
                0f,
                directionReversals,
                in pointerDown,
                in commit,
                default)
        {
        }

        public EarthGestureToken(
            EarthGestureTokenKind kind,
            float confidence,
            in EarthGestureTokenFeatures features,
            float peakSpeed,
            float peakAcceleration,
            int directionReversals,
            in EarthGestureTargetContext pointerDown,
            in EarthGestureTargetContext commit,
            float3 worldProjectedDirection)
        {
            Kind = kind;
            Confidence = math.saturate(confidence);
            Features = features;
            PeakSpeed = math.max(0f, peakSpeed);
            PeakAcceleration = math.max(0f, peakAcceleration);
            DirectionReversals = math.max(0, directionReversals);
            PointerDownTarget = pointerDown;
            CommitTarget = commit;
            WorldProjectedDirection = math.normalizesafe(worldProjectedDirection);
        }
        public EarthGestureTokenKind Kind { get; }
        public float Confidence { get; }
        public EarthGestureTokenFeatures Features { get; }
        public float PeakSpeed { get; }
        public float PeakAcceleration { get; }
        public int DirectionReversals { get; }
        public EarthGestureTargetContext PointerDownTarget { get; }
        public EarthGestureTargetContext CommitTarget { get; }
        public float3 WorldProjectedDirection { get; }
        public bool IsValid => Kind != EarthGestureTokenKind.Invalid;

        public EarthGestureToken WithWorldProjectedDirection(float3 direction)
        {
            EarthGestureTokenFeatures features = Features;
            EarthGestureTargetContext pointerDown = PointerDownTarget;
            EarthGestureTargetContext commit = CommitTarget;
            return new EarthGestureToken(
                Kind,
                Confidence,
                in features,
                PeakSpeed,
                PeakAcceleration,
                DirectionReversals,
                in pointerDown,
                in commit,
                direction);
        }
    }
}
