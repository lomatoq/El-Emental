using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum SupportFrameContinuity : byte
    {
        Invalid = 0,
        NewSupport = 1,
        Stable = 2,
        NewGeneration = 3,
        Discontinuous = 4
    }

    public readonly struct SupportFrameSnapshot
    {
        public SupportFrameSnapshot(
            uint surfaceId,
            uint generation,
            float3 position,
            quaternion rotation,
            float3 linearVelocity,
            float3 angularVelocity,
            float3 contactPointVelocity,
            float3 up,
            bool emerging,
            bool discontinuous = false)
        {
            SurfaceId = surfaceId;
            Generation = generation;
            Position = position;
            Rotation = math.normalizesafe(rotation, quaternion.identity);
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
            ContactPointVelocity = contactPointVelocity;
            Up = math.normalizesafe(up, new float3(0f, 1f, 0f));
            Emerging = emerging;
            Discontinuous = discontinuous;
        }

        public uint SurfaceId { get; }
        public uint Generation { get; }
        public float3 Position { get; }
        public quaternion Rotation { get; }
        public float3 LinearVelocity { get; }
        public float3 AngularVelocity { get; }
        public float3 ContactPointVelocity { get; }
        public float3 Up { get; }
        public bool Emerging { get; }
        public bool Discontinuous { get; }
        public bool IsValid => SurfaceId != 0u && Generation != 0u &&
                               math.all(math.isfinite(Position)) &&
                               math.all(math.isfinite(LinearVelocity)) &&
                               math.all(math.isfinite(AngularVelocity)) &&
                               math.all(math.isfinite(ContactPointVelocity)) &&
                               math.all(math.isfinite(Up));

        public float3 VelocityAt(float3 worldPoint) =>
            LinearVelocity + math.cross(AngularVelocity, worldPoint - Position);

        public SupportFrameSnapshot WithContactPoint(float3 worldPoint) =>
            new SupportFrameSnapshot(
                SurfaceId,
                Generation,
                Position,
                Rotation,
                LinearVelocity,
                AngularVelocity,
                VelocityAt(worldPoint),
                Up,
                Emerging,
                Discontinuous);
    }

    /// <summary>
    /// V3 compatibility view. New moving surfaces and the motor use
    /// SupportFrameSnapshot so rotation and pooled generations cannot be lost.
    /// </summary>
    public readonly struct MovingSupportSnapshot
    {
        public MovingSupportSnapshot(uint surfaceId, float3 pointVelocity, float3 up, bool emerging)
        {
            Frame = new SupportFrameSnapshot(
                surfaceId,
                1u,
                float3.zero,
                quaternion.identity,
                pointVelocity,
                float3.zero,
                pointVelocity,
                up,
                emerging);
        }

        public MovingSupportSnapshot(in SupportFrameSnapshot frame) => Frame = frame;

        public SupportFrameSnapshot Frame { get; }
        public uint SurfaceId => Frame.SurfaceId;
        public float3 PointVelocity => Frame.ContactPointVelocity;
        public float3 Up => Frame.Up;
        public bool Emerging => Frame.Emerging;
        public bool IsValid => Frame.IsValid;
    }

    public static class MovingSurfaceSolver
    {
        public static SupportFrameContinuity ClassifyContinuity(
            in SupportFrameSnapshot previous,
            in SupportFrameSnapshot current,
            float maximumPositionDelta,
            float maximumRotationDeltaRadians)
        {
            if (!current.IsValid) return SupportFrameContinuity.Invalid;
            if (!previous.IsValid || previous.SurfaceId != current.SurfaceId)
                return SupportFrameContinuity.NewSupport;
            if (previous.Generation != current.Generation)
                return SupportFrameContinuity.NewGeneration;
            if (current.Discontinuous ||
                math.distance(previous.Position, current.Position) > math.max(0.001f, maximumPositionDelta))
                return SupportFrameContinuity.Discontinuous;
            quaternion delta = math.mul(current.Rotation, math.inverse(previous.Rotation));
            float angle = 2f * math.acos(math.clamp(math.abs(delta.value.w), 0f, 1f));
            return angle > math.max(0.001f, maximumRotationDeltaRadians)
                ? SupportFrameContinuity.Discontinuous
                : SupportFrameContinuity.Stable;
        }

        public static float3 AngularVelocity(quaternion previous, quaternion current, float deltaTime)
        {
            quaternion delta = math.normalize(math.mul(current, math.inverse(previous)));
            if (delta.value.w < 0f) delta.value = -delta.value;
            float halfSin = math.length(delta.value.xyz);
            if (halfSin < 0.000001f || deltaTime <= 0f) return float3.zero;
            float angle = 2f * math.atan2(halfSin, math.clamp(delta.value.w, -1f, 1f));
            return delta.value.xyz / halfSin * (angle / math.max(0.0001f, deltaTime));
        }

        public static float3 RelativeVelocity(float3 worldVelocity, in SupportFrameSnapshot support) =>
            support.IsValid ? worldVelocity - support.ContactPointVelocity : worldVelocity;

        public static float3 TangentCarryVelocityChange(
            float3 previousSupportVelocity,
            float3 currentSupportVelocity,
            float3 localUp,
            bool sameSupport,
            float maximumAcceleration,
            float deltaTime)
        {
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 supportDelta = sameSupport
                ? currentSupportVelocity - previousSupportVelocity
                : currentSupportVelocity;
            float3 tangentDelta = supportDelta - (up * math.dot(supportDelta, up));
            float maximumDelta = math.max(0.1f, maximumAcceleration) *
                                 math.max(0.0001f, deltaTime);
            return math.normalizesafe(tangentDelta) * math.min(math.length(tangentDelta), maximumDelta);
        }

        public static float3 CarryAcceleration(
            float3 riderVelocity,
            float3 supportVelocity,
            float3 localUp,
            float verticalError,
            float maximumSpeed,
            float maximumAcceleration,
            float deltaTime)
        {
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float desiredUpSpeed = math.clamp(
                math.dot(supportVelocity, up) + verticalError / math.max(0.0001f, deltaTime),
                -math.max(0.1f, maximumSpeed),
                math.max(0.1f, maximumSpeed));
            float currentUpSpeed = math.dot(riderVelocity, up);
            float required = (desiredUpSpeed - currentUpSpeed) / math.max(0.0001f, deltaTime);
            return up * math.clamp(required, -math.max(0.1f, maximumAcceleration), math.max(0.1f, maximumAcceleration));
        }

        public static float3 AnchorCorrectionVelocityChange(
            float3 riderPosition,
            float3 desiredAnchorPosition,
            float3 localUp,
            float stiffness,
            float maximumAcceleration,
            float deltaTime)
        {
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 error = desiredAnchorPosition - riderPosition;
            float3 tangentError = error - up * math.dot(error, up);
            float distance = math.length(tangentError);
            if (distance <= 0.005f || distance > 1.25f) return float3.zero;
            float acceleration = math.min(
                distance * math.max(0f, stiffness),
                math.max(0.1f, maximumAcceleration));
            float maximumDelta = math.max(0.1f, maximumAcceleration) * math.max(0.0001f, deltaTime);
            float requestedDelta = acceleration * math.max(0.0001f, deltaTime);
            return math.normalizesafe(tangentError) * math.min(requestedDelta, maximumDelta);
        }
    }
}
