using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public static class EarthRepairPoseSolver
    {
        private const float MinimumTime = 0.02f;

        public static EarthRepairPoseControlSample Solve(
            in EarthRepairPoseInput input,
            in EarthReassemblyTuning tuning,
            bool capturePhase)
        {
            if (!IsFinite(in input))
                return new EarthRepairPoseControlSample(default, default, 0f, 0f, false, false, false);

            float settleTime = math.max(
                MinimumTime,
                capturePhase ? tuning.CaptureSettleTime : tuning.AlignmentSettleTime);
            float omega = 2f / settleTime;
            float3 positionErrorVector = input.TargetPosition - input.Position;
            float3 velocityError = input.TargetVelocity - input.Velocity;
            float3 acceleration = (omega * omega * positionErrorVector) +
                                  (2f * math.max(0f, tuning.DampingRatio) * omega * velocityError);
            float maximumByForce = math.max(0f, tuning.MaximumForce) /
                                   math.max(0.01f, input.Mass);
            float maximumAcceleration = math.max(0.01f, tuning.MaximumAcceleration);
            if (maximumByForce > 0f) maximumAcceleration = math.min(maximumAcceleration, maximumByForce);
            bool accelerationLimited = ClampMagnitude(ref acceleration, maximumAcceleration);

            quaternion current = SafeNormalize(input.Rotation);
            quaternion target = SafeNormalize(input.TargetRotation);
            quaternion error = math.mul(target, math.inverse(current));
            float4 errorValue = error.value;
            if (errorValue.w < 0f) errorValue = -errorValue;
            errorValue = math.normalize(errorValue);
            float angle = 2f * math.acos(math.clamp(errorValue.w, -1f, 1f));
            float sinHalf = math.sqrt(math.max(0f, 1f - (errorValue.w * errorValue.w)));
            float3 axis = sinHalf > 0.0001f
                ? errorValue.xyz / sinHalf
                : float3.zero;
            float3 angularVelocityError = input.TargetAngularVelocity - input.AngularVelocity;
            float3 angularAcceleration = axis * angle * math.max(0f, tuning.RotationStiffness) +
                                         angularVelocityError * math.max(0f, tuning.RotationDamping);
            bool angularLimited = ClampMagnitude(
                ref angularAcceleration,
                math.max(0.01f, tuning.MaximumAngularAcceleration));
            bool finite = math.all(math.isfinite(acceleration)) &&
                          math.all(math.isfinite(angularAcceleration)) && math.isfinite(angle);
            if (!finite) return new EarthRepairPoseControlSample(default, default, 0f, 0f, false, false, false);
            return new EarthRepairPoseControlSample(
                acceleration,
                angularAcceleration,
                math.length(positionErrorVector),
                angle,
                accelerationLimited,
                angularLimited,
                true);
        }

        public static bool UpdateSettle(
            in EarthRepairPoseControlSample pose,
            float relativeSpeed,
            float relativeAngularSpeed,
            float deltaTime,
            in EarthReassemblyTuning tuning,
            ref EarthRepairSettleState state)
        {
            bool inside = pose.IsFinite &&
                          pose.PositionError <= math.max(0f, tuning.PositionTolerance) &&
                          pose.AngleErrorRadians <= math.max(0f, tuning.AngleToleranceRadians) &&
                          relativeSpeed <= math.max(0f, tuning.MaximumRelativeSpeed) &&
                          relativeAngularSpeed <= math.max(0f, tuning.MaximumRelativeAngularSpeed);
            state.StableSeconds = inside
                ? state.StableSeconds + math.max(0f, deltaTime)
                : 0f;
            return inside && state.StableSeconds >= math.max(0f, tuning.SettleDuration);
        }

        public static EarthRepairProgressSample UpdateProgress(
            float error,
            float deltaTime,
            in EarthReassemblyTuning tuning,
            ref EarthRepairProgressState state)
        {
            float dt = math.max(0f, deltaTime);
            if (state.RetryDelayRemaining > 0f)
            {
                state.RetryDelayRemaining = math.max(0f, state.RetryDelayRemaining - dt);
                return new EarthRepairProgressSample(false, state.RetryDelayRemaining > 0f);
            }
            if (!math.isfinite(error))
            {
                state.RetryCount++;
                state.RetryDelayRemaining = math.max(0f, tuning.RetryDelay);
                state.SecondsWithoutProgress = 0f;
                state.BestError = float.MaxValue;
                return new EarthRepairProgressSample(true, false);
            }
            if (state.BestError <= 0f || error + math.max(0f, tuning.JamProgressEpsilon) < state.BestError)
            {
                state.BestError = error;
                state.SecondsWithoutProgress = 0f;
                return new EarthRepairProgressSample(false, false);
            }
            state.SecondsWithoutProgress += dt;
            if (state.SecondsWithoutProgress < math.max(MinimumTime, tuning.JamDuration))
                return new EarthRepairProgressSample(false, false);
            state.RetryCount++;
            state.RetryDelayRemaining = math.max(0f, tuning.RetryDelay);
            state.SecondsWithoutProgress = 0f;
            state.BestError = float.MaxValue;
            return new EarthRepairProgressSample(true, false);
        }

        public static float3 StagingOffset(
            EarthPieceId pieceId,
            int graphDepth,
            float stagingDistance,
            float3 outward,
            float3 tangent,
            byte retryCount)
        {
            uint hash = (uint)pieceId.Value * 0x9E3779B9u + 0x7F4A7C15u;
            float shell = math.max(0f, stagingDistance) *
                          (1f + math.max(0, graphDepth) * 0.16f + retryCount * 0.28f);
            float3 outwardAxis = math.normalizesafe(outward, new float3(0f, 0f, 1f));
            float3 tangentAxis = math.normalizesafe(tangent, new float3(1f, 0f, 0f));
            float3 verticalAxis = math.normalizesafe(
                math.cross(outwardAxis, tangentAxis),
                new float3(0f, 1f, 0f));
            float angle = ((hash >> 8) & 0xFFFFu) * (math.PI * 2f / 65535f);
            float ring = math.lerp(0.55f, 1.35f, ((hash >> 24) & 0xFFu) / 255f);
            // Bias the vertical part upward so the staging cloud never pushes
            // foundation pieces deeper into the planet collision shell.
            float vertical = math.abs(math.sin(angle)) * 0.85f + 0.12f;
            return outwardAxis * shell +
                   tangentAxis * math.cos(angle) * shell * ring +
                   verticalAxis * vertical * shell * ring;
        }

        private static bool ClampMagnitude(ref float3 value, float maximum)
        {
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= maximum * maximum) return false;
            value = math.normalizesafe(value) * maximum;
            return true;
        }

        private static bool IsFinite(in EarthRepairPoseInput input)
        {
            return math.all(math.isfinite(input.Position)) &&
                   math.all(math.isfinite(input.Rotation.value)) &&
                   math.all(math.isfinite(input.Velocity)) &&
                   math.all(math.isfinite(input.AngularVelocity)) &&
                   math.all(math.isfinite(input.TargetPosition)) &&
                   math.all(math.isfinite(input.TargetRotation.value)) &&
                   math.all(math.isfinite(input.TargetVelocity)) &&
                   math.all(math.isfinite(input.TargetAngularVelocity)) &&
                   math.isfinite(input.Mass) && input.Mass > 0f;
        }

        private static quaternion SafeNormalize(quaternion value)
        {
            float lengthSq = math.lengthsq(value.value);
            return lengthSq > 0.000001f
                ? new quaternion(value.value * math.rsqrt(lengthSq))
                : quaternion.identity;
        }
    }
}
