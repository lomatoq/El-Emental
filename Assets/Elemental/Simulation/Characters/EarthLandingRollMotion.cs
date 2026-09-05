using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>Fixed-clock landing evidence and bounded roll travel; no animation/root-motion authority.</summary>
    public struct EarthLandingRollMotion
    {
        public const float DefaultDurationSeconds = 1.4f;
        private bool _observedSupport, _wasSupported, _deliberateJump;
        private float _airSeconds, _height, _peakHeight, _takeoffSpeed, _lastForwardSpeed;
        private float _externalDeltaSpeed, _elapsed, _duration, _initialSpeed;
        public bool Active { get; private set; }
        public bool LastLandingWasRoll { get; private set; }
        public uint Sequence { get; private set; }
        public float Speed { get; private set; }
        public void Cancel() { Active = false; LastLandingWasRoll = false; Speed = 0f; }

        public void Step(bool supported, bool interrupted, bool jump, float heightDelta,
            float verticalSpeed, float forwardSpeed, float externalDeltaSpeed,
            float duration, float minimumSpeed, float maximumSpeed, float deltaTime)
        {
            float dt = math.max(0f, deltaTime);
            if (!supported)
            {
                if (_wasSupported)
                {
                    _airSeconds = _height = _peakHeight = _externalDeltaSpeed = 0f;
                    _takeoffSpeed = forwardSpeed;
                    _deliberateJump = jump && verticalSpeed > 0.75f;
                    _lastForwardSpeed = forwardSpeed;
                    LastLandingWasRoll = false;
                }
                _airSeconds += dt;
                _height += heightDelta;
                _peakHeight = math.max(_peakHeight, _height);
                _externalDeltaSpeed = math.max(_externalDeltaSpeed, externalDeltaSpeed);
                if (math.abs(forwardSpeed) > 0.25f) _lastForwardSpeed = forwardSpeed;
                Active = false;
            }
            else if (!_wasSupported)
            {
                _height += heightDelta;
                LastLandingWasRoll = !interrupted && !jump && _lastForwardSpeed >= -0.25f &&
                    EarthLandingRollPolicy.AllowsRoll(_observedSupport, _airSeconds,
                        math.max(0f, _peakHeight - _height), _deliberateJump,
                        _takeoffSpeed, _externalDeltaSpeed);
                if (LastLandingWasRoll)
                {
                    _duration = math.max(0.05f, duration);
                    _initialSpeed = math.clamp(math.max(0f, _lastForwardSpeed),
                        math.max(0f, minimumSpeed), math.max(minimumSpeed, maximumSpeed));
                    _elapsed = 0f;
                    Active = true;
                    Sequence++;
                }
            }
            if (interrupted || jump) Active = false;
            Speed = Active ? AverageSpeed(_initialSpeed, _duration, _elapsed, dt) : 0f;
            if (Active)
            {
                _elapsed += dt;
                if (_elapsed >= _duration) Active = false;
            }
            _observedSupport |= supported;
            _wasSupported = supported;
        }

        // Integrate v(t) = v0 * (1-t/T)^2 over the physics step. The travel budget
        // is v0*T/3 at any tick rate, with zero speed and zero slope at completion.
        public static float AverageSpeed(float initialSpeed, float duration, float elapsed, float dt)
        {
            if (duration <= 0f || dt <= 0f) return 0f;
            float a = math.saturate(1f - elapsed / duration);
            float b = math.saturate(1f - (elapsed + dt) / duration);
            return math.max(0f, initialSpeed) * duration * (a * a * a - b * b * b) / (3f * dt);
        }
    }
}
