using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public static class PlanetOrientationSolver
    {
        public static quaternion Step(
            quaternion current,
            quaternion desired,
            float response,
            float maximumDegreesPerSecond,
            float deltaSeconds)
        {
            if (!math.all(math.isfinite(current.value)) || !math.all(math.isfinite(desired.value)))
                return quaternion.identity;
            float dt = math.max(0f, deltaSeconds);
            if (dt <= 0f) return math.normalize(current);
            quaternion from = math.normalize(current);
            quaternion to = math.normalize(desired);
            float dot = math.clamp(math.abs(math.dot(from.value, to.value)), 0f, 1f);
            float angle = 2f * math.acos(dot);
            if (angle <= 0.00001f) return to;
            float exponential = 1f - math.exp(-math.max(0.01f, response) * dt);
            float maximumStep = math.radians(math.max(1f, maximumDegreesPerSecond)) * dt;
            float t = math.min(exponential, maximumStep / angle);
            return math.normalize(math.slerp(from, to, math.saturate(t)));
        }
    }
}
