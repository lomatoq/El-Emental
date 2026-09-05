using Unity.Mathematics;

namespace Elemental.Simulation.Gravity
{
    public struct EarthBodyRestState
    {
        private float _quietSeconds;
        public bool Step(bool supported, float3 velocity, float3 angularVelocity, float deltaTime)
        {
            if (!supported || !math.all(math.isfinite(velocity)) || !math.all(math.isfinite(angularVelocity)) ||
                math.lengthsq(velocity) > .07f*.07f || math.lengthsq(angularVelocity) > .12f*.12f)
            { _quietSeconds=0f; return false; }
            _quietSeconds += math.max(0f,deltaTime);
            if (_quietSeconds < .6f) return false;
            _quietSeconds=0f;
            return true;
        }
    }
}
