using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public readonly struct EarthProjectileRedirectResult
    {
        public EarthProjectileRedirectResult(float3 velocity, float retainedEnergy01, bool valid)
        {
            Velocity = velocity;
            RetainedEnergy01 = math.saturate(retainedEnergy01);
            Valid = valid;
        }
        public float3 Velocity { get; }
        public float RetainedEnergy01 { get; }
        public bool Valid { get; }
    }

    public static class EarthProjectileRedirectSolver
    {
        public static EarthProjectileRedirectResult Solve(
            float3 incomingVelocity,
            float3 requestedDirection,
            float control01,
            float maximumSpeed)
        {
            float speed = math.length(incomingVelocity);
            float3 direction = math.normalizesafe(requestedDirection);
            if (speed <= 0.05f || math.lengthsq(direction) <= 0.5f)
                return new EarthProjectileRedirectResult(incomingVelocity, 1f, false);
            float control = math.saturate(control01);
            float3 incoming = incomingVelocity / speed;
            float3 redirected = math.normalizesafe(math.lerp(incoming, direction, control), direction);
            float angularLoss = math.lerp(1f, 0.78f, control * (1f - math.saturate(math.dot(incoming, direction))));
            float retainedSpeed = math.min(math.max(0.1f, maximumSpeed), speed * angularLoss);
            return new EarthProjectileRedirectResult(
                redirected * retainedSpeed, angularLoss * angularLoss, true);
        }
    }

    public readonly struct EarthSeismicCounterResult
    {
        public EarthSeismicCounterResult(bool triggered, float radius, float impulse, float followUpMatter01)
        {
            Triggered = triggered;
            Radius = math.max(0f, radius);
            Impulse = math.max(0f, impulse);
            FollowUpMatter01 = math.saturate(followUpMatter01);
        }
        public bool Triggered { get; }
        public float Radius { get; }
        public float Impulse { get; }
        public float FollowUpMatter01 { get; }
    }

    public static class EarthSeismicCounterSolver
    {
        public static EarthSeismicCounterResult Evaluate(
            bool braced,
            float incomingImpulse,
            float kineticEnergy,
            float minimumImpulse = 120f,
            float maximumStoredImpulse = 920f)
        {
            if (!braced || incomingImpulse < math.max(1f, minimumImpulse)) return default;
            float stored = math.saturate(
                (incomingImpulse - minimumImpulse) /
                math.max(1f, maximumStoredImpulse - minimumImpulse));
            float energy = 1f - math.exp(-math.max(0f, kineticEnergy) / 6000f);
            float charge = math.saturate(stored * 0.72f + energy * 0.28f);
            return new EarthSeismicCounterResult(
                true,
                math.lerp(2.2f, 5.4f, charge),
                math.lerp(90f, 520f, charge),
                math.lerp(0.25f, 1f, charge));
        }
    }

    public enum EarthTrapState : byte
    {
        Dormant = 0,
        Armed = 1,
        Captured = 2,
        Spent = 3
    }

    public readonly struct EarthTrapSample
    {
        public EarthTrapSample(EarthTrapState state, float strength01, bool release)
        {
            State = state;
            Strength01 = math.saturate(strength01);
            Release = release;
        }
        public EarthTrapState State { get; }
        public float Strength01 { get; }
        public bool Release { get; }
    }

    public static class EarthTrapSolver
    {
        public static EarthTrapSample Step(
            EarthTrapState state,
            float capturedSeconds,
            float holdSeconds,
            float escapeImpulse,
            float breakImpulse)
        {
            if (state != EarthTrapState.Captured)
                return new EarthTrapSample(state, state == EarthTrapState.Armed ? 1f : 0f, false);
            float hold = math.max(0.1f, holdSeconds);
            float strength = math.saturate(1f - capturedSeconds / hold);
            bool release = capturedSeconds >= hold || escapeImpulse >= math.max(1f, breakImpulse);
            return new EarthTrapSample(release ? EarthTrapState.Spent : state, strength, release);
        }
    }
}
