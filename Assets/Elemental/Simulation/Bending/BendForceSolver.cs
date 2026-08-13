using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public static class BendForceSolver
    {
        public static BendForceResult SolvePdForce(
            float3 actualPosition,
            float3 actualVelocity,
            float3 targetPosition,
            float3 targetVelocity,
            float effectiveMass,
            float charge01,
            in BendTuning tuning)
        {
            return SolvePdForce(
                actualPosition,
                actualVelocity,
                targetPosition,
                targetVelocity,
                effectiveMass,
                float3.zero,
                charge01,
                tuning);
        }

        public static BendForceResult SolvePdForce(
            float3 actualPosition,
            float3 actualVelocity,
            float3 targetPosition,
            float3 targetVelocity,
            float effectiveMass,
            float3 externalAcceleration,
            float charge01,
            in BendTuning tuning)
        {
            float3 positionError = targetPosition - actualPosition;
            float3 velocityError = targetVelocity - actualVelocity;
            float mass = math.max(0.01f, effectiveMass);
            float3 requested = mass * ((positionError * tuning.PositionGain) +
                                       (velocityError * tuning.VelocityGain) -
                                       externalAcceleration);
            float maximum = tuning.MaximumControlForce *
                            math.lerp(1f, tuning.ChargedControlMultiplier, math.saturate(charge01));
            float requestedLength = math.length(requested);
            bool clamped = requestedLength > maximum && requestedLength > 0f;
            float3 applied = clamped ? requested * (maximum / requestedLength) : requested;
            return new BendForceResult(positionError, velocityError, applied, clamped);
        }

        public static float3 SolveReleaseVelocity(
            float3 physicalVelocity,
            float3 aimDirection,
            float3 smoothedGestureVelocity,
            float charge01,
            in BendTuning tuning)
        {
            float3 aim = math.normalizesafe(aimDirection);
            float chargeSpeed = math.lerp(tuning.MinimumReleaseSpeed, tuning.MaximumReleaseSpeed,
                math.saturate(charge01));
            float3 velocity = physicalVelocity +
                              (aim * chargeSpeed) +
                              (smoothedGestureVelocity * tuning.GestureVelocityTransfer);
            float speed = math.length(velocity);
            return speed > tuning.MaximumReleaseSpeed && speed > 0f
                ? velocity * (tuning.MaximumReleaseSpeed / speed)
                : velocity;
        }
    }
}
