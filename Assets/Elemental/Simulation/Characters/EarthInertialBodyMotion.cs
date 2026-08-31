using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public struct EarthInertialBodyState
    {
        public float3 AnglesDegrees;
        public float3 AngularVelocity;
        public float3 ImpactOffsetDegrees;
    }

    public readonly struct EarthInertialBodySample
    {
        public EarthInertialBodySample(EarthInertialBodyState state, float3 anglesDegrees)
        {
            State = state;
            AnglesDegrees = anglesDegrees;
        }

        public EarthInertialBodyState State { get; }
        public float3 AnglesDegrees { get; }
    }

    /// <summary>
    /// Upper-body-only critically damped presentation. It has no sine/rebound
    /// term and never writes hips, knees, feet, movement or gameplay state.
    /// </summary>
    public static class EarthInertialBodyMotionSolver
    {
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
                return new EarthInertialBodySample(state, float3.zero);
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

            state.ImpactOffsetDegrees = math.clamp(
                state.ImpactOffsetDegrees + impactKickDegrees,
                new float3(-9f),
                new float3(9f));
            float impactDecay = math.exp(-dt / 0.115f);
            state.ImpactOffsetDegrees *= impactDecay;
            float3 result = math.clamp(
                state.AnglesDegrees + state.ImpactOffsetDegrees,
                new float3(-10f),
                new float3(10f));
            return new EarthInertialBodySample(state, result);
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

        public static float3 ResolveDirectionalKick(float3 localDirection, float severity)
        {
            float3 direction = math.normalizesafe(localDirection, new float3(0f, 0f, 1f));
            float magnitude = math.clamp(severity * 1.35f, 1.5f, 7.5f);
            return new float3(
                math.clamp(-direction.z * magnitude, -7.5f, 7.5f),
                math.clamp(direction.x * magnitude * 0.35f, -3f, 3f),
                math.clamp(-direction.x * magnitude, -7.5f, 7.5f));
        }
    }
}
