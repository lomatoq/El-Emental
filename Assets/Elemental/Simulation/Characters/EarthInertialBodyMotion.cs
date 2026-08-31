using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public struct EarthInertialBodyState
    {
        public float3 AnglesDegrees;
        public float3 AngularVelocity;
        public float3 ImpactOffsetDegrees;
        public float3 ImpactAngularVelocityDegrees;
    }

    public readonly struct EarthInertialBodySample
    {
        public EarthInertialBodySample(
            EarthInertialBodyState state,
            float3 locomotionAnglesDegrees,
            float3 impactAnglesDegrees,
            float3 anglesDegrees)
        {
            State = state;
            LocomotionAnglesDegrees = locomotionAnglesDegrees;
            ImpactAnglesDegrees = impactAnglesDegrees;
            AnglesDegrees = anglesDegrees;
        }

        public EarthInertialBodyState State { get; }
        public float3 LocomotionAnglesDegrees { get; }
        public float3 ImpactAnglesDegrees { get; }
        public float3 AnglesDegrees { get; }
    }

    /// <summary>
    /// Upper-body-only critically damped presentation. It has no sine/rebound
    /// term and never writes hips, knees, feet, movement or gameplay state.
    /// </summary>
    public static class EarthInertialBodyMotionSolver
    {
        public const float MaximumImpactAngleDegrees = 9f;
        public const float MaximumImpactAngularVelocityDegrees = 200f;
        private const float ImpactAngularFrequency = 30f;
        private const float ImpactDampingRatio = 0.72f;
        private const float MaximumImpactSubstep = 1f / 240f;

        public static EarthInertialBodySample Step(
            in EarthInertialBodyState input,
            float3 localAcceleration,
            float yawRateDegrees,
            float moveTurn,
            float3 impactKickDegrees,
            bool grounded,
            bool ragdoll,
            float deltaTime)
        {
            EarthInertialBodyState state = input;
            float dt = math.clamp(deltaTime, 0f, 0.05f);
            if (ragdoll)
            {
                state = default;
                return new EarthInertialBodySample(
                    state,
                    float3.zero,
                    float3.zero,
                    float3.zero);
            }

            float groundedWeight = grounded ? 1f : 0.36f;
            float3 target = new float3(
                math.clamp(-localAcceleration.z * 0.24f, -7f, 7f),
                math.clamp(moveTurn * 2.6f, -3.5f, 3.5f),
                math.clamp(-localAcceleration.x * 0.22f - yawRateDegrees * 0.018f, -8f, 8f)) *
                groundedWeight;
            target = math.select(float3.zero, target, math.isfinite(target));
            float3 velocity = state.AngularVelocity;
            state.AnglesDegrees = SmoothDamp(
                state.AnglesDegrees,
                target,
                ref velocity,
                0.11f,
                dt);
            state.AngularVelocity = velocity;

            state.ImpactAngularVelocityDegrees = math.clamp(
                state.ImpactAngularVelocityDegrees + SelectFinite(impactKickDegrees),
                new float3(-MaximumImpactAngularVelocityDegrees),
                new float3(MaximumImpactAngularVelocityDegrees));
            StepImpactSpring(ref state, dt);
            float3 result = math.clamp(
                state.AnglesDegrees + state.ImpactOffsetDegrees,
                new float3(-10f),
                new float3(10f));
            return new EarthInertialBodySample(
                state,
                state.AnglesDegrees,
                state.ImpactOffsetDegrees,
                result);
        }

        private static void StepImpactSpring(ref EarthInertialBodyState state, float deltaTime)
        {
            float remaining = math.max(0f, deltaTime);
            float stiffness = ImpactAngularFrequency * ImpactAngularFrequency;
            float damping = 2f * ImpactDampingRatio * ImpactAngularFrequency;
            while (remaining > 0.000001f)
            {
                float step = math.min(MaximumImpactSubstep, remaining);
                float3 acceleration = -stiffness * state.ImpactOffsetDegrees -
                                      damping * state.ImpactAngularVelocityDegrees;
                state.ImpactAngularVelocityDegrees = math.clamp(
                    state.ImpactAngularVelocityDegrees + acceleration * step,
                    new float3(-MaximumImpactAngularVelocityDegrees),
                    new float3(MaximumImpactAngularVelocityDegrees));
                float3 next = state.ImpactOffsetDegrees +
                              state.ImpactAngularVelocityDegrees * step;
                float3 clamped = math.clamp(
                    next,
                    new float3(-MaximumImpactAngleDegrees),
                    new float3(MaximumImpactAngleDegrees));
                bool3 hitLimit = math.abs(next - clamped) > 0.000001f;
                state.ImpactOffsetDegrees = clamped;
                state.ImpactAngularVelocityDegrees = math.select(
                    state.ImpactAngularVelocityDegrees,
                    float3.zero,
                    hitLimit & (math.sign(state.ImpactAngularVelocityDegrees) == math.sign(next)));
                remaining -= step;
            }

            if (math.cmax(math.abs(state.ImpactOffsetDegrees)) < 0.001f &&
                math.cmax(math.abs(state.ImpactAngularVelocityDegrees)) < 0.02f)
            {
                state.ImpactOffsetDegrees = float3.zero;
                state.ImpactAngularVelocityDegrees = float3.zero;
            }
        }

        private static float3 SmoothDamp(
            float3 current,
            float3 target,
            ref float3 velocity,
            float smoothTime,
            float deltaTime)
        {
            smoothTime = math.max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * math.max(0.0001f, deltaTime);
            float decay = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float3 change = current - target;
            float3 temporary = (velocity + omega * change) * deltaTime;
            velocity = (velocity - omega * temporary) * decay;
            return target + (change + temporary) * decay;
        }

        public static float3 ResolveDirectionalAngularVelocity(
            float3 localDirection,
            float severity,
            float transferWeight = 1f,
            float angularVelocityCap = MaximumImpactAngularVelocityDegrees)
        {
            float3 direction = math.normalizesafe(localDirection, new float3(0f, 0f, 1f));
            float cap = math.clamp(
                math.isfinite(angularVelocityCap)
                    ? angularVelocityCap
                    : MaximumImpactAngularVelocityDegrees,
                20f,
                MaximumImpactAngularVelocityDegrees);
            float magnitude = math.clamp(severity * 34f, 28f, cap) *
                              math.saturate(math.isfinite(transferWeight) ? transferWeight : 1f);
            return new float3(
                math.clamp(-direction.z * magnitude, -cap, cap),
                math.clamp(direction.x * magnitude * 0.35f, -cap * 0.45f, cap * 0.45f),
                math.clamp(-direction.x * magnitude, -cap, cap));
        }

        private static float3 SelectFinite(float3 value) =>
            math.select(float3.zero, value, math.isfinite(value));
    }
}
