using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public enum EarthRepairAnchorMode : byte
    {
        OriginalStructureFrame,
        LargestSurvivingIsland,
        CursorDefinedFrame
    }

    public enum EarthRepairOrderStatus : byte
    {
        Success,
        InvalidStorage,
        CapacityExceeded,
        NoRepairablePieces
    }

    public enum EarthRepairRejectReason : byte
    {
        None,
        StructureNotFractured,
        DifferentStructure,
        NoRepairablePieces,
        ConflictingOwner,
        MassLimitExceeded,
        InvalidGraph
    }

    public enum EarthRepairInterruptReason : byte
    {
        Released,
        TargetInvalidated,
        GenerationChanged,
        SolverRejected,
        ExplicitCancel
    }

    public readonly struct EarthRepairOrderResult
    {
        public EarthRepairOrderResult(
            EarthRepairOrderStatus status,
            int anchorPieceIndex,
            int orderedPieceCount,
            int missingPieceCount,
            float selectedMass)
        {
            Status = status;
            AnchorPieceIndex = anchorPieceIndex;
            OrderedPieceCount = orderedPieceCount;
            MissingPieceCount = missingPieceCount;
            SelectedMass = selectedMass;
        }

        public EarthRepairOrderStatus Status { get; }
        public int AnchorPieceIndex { get; }
        public int OrderedPieceCount { get; }
        public int MissingPieceCount { get; }
        public float SelectedMass { get; }
        public bool IsSuccess => Status == EarthRepairOrderStatus.Success;
        public bool IsPartial => MissingPieceCount > 0;
    }

    public struct EarthReassemblyTuning
    {
        public float CaptureSettleTime;
        public float AlignmentSettleTime;
        public float DampingRatio;
        public float MaximumAcceleration;
        public float MaximumForce;
        public float MaximumAngularAcceleration;
        public float RotationStiffness;
        public float RotationDamping;
        public float PositionTolerance;
        public float AngleToleranceRadians;
        public float MaximumRelativeSpeed;
        public float MaximumRelativeAngularSpeed;
        public float SettleDuration;
        public float JamDuration;
        public float JamProgressEpsilon;
        public float RetryDelay;
    }

    public readonly struct EarthRepairPoseInput
    {
        public EarthRepairPoseInput(
            float3 position,
            quaternion rotation,
            float3 velocity,
            float3 angularVelocity,
            float3 targetPosition,
            quaternion targetRotation,
            float3 targetVelocity,
            float3 targetAngularVelocity,
            float mass)
        {
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            AngularVelocity = angularVelocity;
            TargetPosition = targetPosition;
            TargetRotation = targetRotation;
            TargetVelocity = targetVelocity;
            TargetAngularVelocity = targetAngularVelocity;
            Mass = mass;
        }

        public float3 Position { get; }
        public quaternion Rotation { get; }
        public float3 Velocity { get; }
        public float3 AngularVelocity { get; }
        public float3 TargetPosition { get; }
        public quaternion TargetRotation { get; }
        public float3 TargetVelocity { get; }
        public float3 TargetAngularVelocity { get; }
        public float Mass { get; }
    }

    public readonly struct EarthRepairPoseControlSample
    {
        public EarthRepairPoseControlSample(
            float3 acceleration,
            float3 angularAcceleration,
            float positionError,
            float angleErrorRadians,
            bool accelerationLimited,
            bool angularAccelerationLimited,
            bool isFinite)
        {
            Acceleration = acceleration;
            AngularAcceleration = angularAcceleration;
            PositionError = positionError;
            AngleErrorRadians = angleErrorRadians;
            AccelerationLimited = accelerationLimited;
            AngularAccelerationLimited = angularAccelerationLimited;
            IsFinite = isFinite;
        }

        public float3 Acceleration { get; }
        public float3 AngularAcceleration { get; }
        public float PositionError { get; }
        public float AngleErrorRadians { get; }
        public bool AccelerationLimited { get; }
        public bool AngularAccelerationLimited { get; }
        public bool IsFinite { get; }
    }

    public struct EarthRepairSettleState
    {
        public float StableSeconds;
    }

    public struct EarthRepairProgressState
    {
        public float BestError;
        public float SecondsWithoutProgress;
        public float RetryDelayRemaining;
        public byte RetryCount;
    }

    public readonly struct EarthRepairProgressSample
    {
        public EarthRepairProgressSample(bool retryRequested, bool retryWaiting)
        {
            RetryRequested = retryRequested;
            RetryWaiting = retryWaiting;
        }

        public bool RetryRequested { get; }
        public bool RetryWaiting { get; }
    }
}
