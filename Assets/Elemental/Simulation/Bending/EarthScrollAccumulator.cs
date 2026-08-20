using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum EarthScrollDeviceProfile : byte
    {
        DetentWheel = 0,
        SmoothTrackpad = 1
    }

    public readonly struct EarthScrollState
    {
        public EarthScrollState(
            float rawDelta,
            float normalizedDelta,
            float continuous,
            int quantizedSteps,
            float velocity,
            float directionHoldTime,
            int reversalCount,
            bool fastFlick,
            bool directionReversal,
            bool overscrollConfirmed)
        {
            RawDelta = rawDelta;
            NormalizedDelta = normalizedDelta;
            Continuous = continuous;
            QuantizedSteps = quantizedSteps;
            Velocity = velocity;
            DirectionHoldTime = directionHoldTime;
            ReversalCount = reversalCount;
            FastFlick = fastFlick;
            DirectionReversal = directionReversal;
            OverscrollConfirmed = overscrollConfirmed;
        }

        public float RawDelta { get; }
        public float NormalizedDelta { get; }
        public float Continuous { get; }
        public int QuantizedSteps { get; }
        public float Velocity { get; }
        public float DirectionHoldTime { get; }
        public int ReversalCount { get; }
        public bool FastFlick { get; }
        public bool DirectionReversal { get; }
        public bool OverscrollConfirmed { get; }
    }

    /// <summary>Frame-rate independent mouse-wheel/trackpad semantic normalizer.</summary>
    public sealed class EarthScrollAccumulator
    {
        private readonly EarthScrollDeviceProfile _profile;
        private float _continuous;
        private float _fractional;
        private float _velocity;
        private float _directionHold;
        private float _lastPulseAt = -100f;
        private float _boundaryEnteredAt = -100f;
        private int _direction;
        private int _reversalCount;
        private int _overscrollPulses;

        public EarthScrollAccumulator(EarthScrollDeviceProfile profile = EarthScrollDeviceProfile.DetentWheel)
        {
            _profile = profile;
        }

        public void Reset(float continuous = 0f)
        {
            _continuous = continuous;
            _fractional = 0f;
            _velocity = 0f;
            _directionHold = 0f;
            _lastPulseAt = -100f;
            _boundaryEnteredAt = -100f;
            _direction = 0;
            _reversalCount = 0;
            _overscrollPulses = 0;
        }

        public EarthScrollState Step(
            float rawDelta,
            float unscaledDeltaTime,
            float unscaledTime,
            float minimum = float.NegativeInfinity,
            float maximum = float.PositiveInfinity)
        {
            float dt = math.max(0.0001f, unscaledDeltaTime);
            float normalized = Normalize(rawDelta, _profile);
            int direction = normalized > 0.0001f ? 1 : normalized < -0.0001f ? -1 : 0;
            bool reversal = direction != 0 && _direction != 0 && direction != _direction &&
                            unscaledTime - _lastPulseAt <= 0.28f;
            if (reversal)
            {
                _reversalCount++;
                _overscrollPulses = 0;
            }
            if (direction != 0)
            {
                _directionHold = direction == _direction ? _directionHold + dt : dt;
                _direction = direction;
                _lastPulseAt = unscaledTime;
            }
            else
            {
                _directionHold = math.max(0f, _directionHold - dt * 1.6f);
            }

            float instantaneousVelocity = normalized / dt;
            float response = 1f - math.exp(-18f * dt);
            _velocity = math.lerp(_velocity, instantaneousVelocity, response);
            _fractional += normalized;
            int steps = 0;
            const float stepHysteresis = 0.82f;
            while (_fractional >= stepHysteresis)
            {
                steps++;
                _fractional -= 1f;
            }
            while (_fractional <= -stepHysteresis)
            {
                steps--;
                _fractional += 1f;
            }

            float before = _continuous;
            _continuous = math.clamp(_continuous + normalized, minimum, maximum);
            bool atUpper = math.isfinite(maximum) && _continuous >= maximum - 0.0001f && direction > 0;
            bool atLower = math.isfinite(minimum) && _continuous <= minimum + 0.0001f && direction < 0;
            bool pressingBoundary = (atUpper || atLower) && math.abs(before - _continuous) <= math.abs(normalized) + 0.0001f;
            bool overscroll = false;
            if (pressingBoundary && direction != 0)
            {
                if (unscaledTime - _boundaryEnteredAt > 0.40f) _overscrollPulses = 0;
                _boundaryEnteredAt = unscaledTime;
                if (math.abs(normalized) >= 0.55f) _overscrollPulses++;
                if (_overscrollPulses >= 2)
                {
                    overscroll = true;
                    _overscrollPulses = 0;
                }
            }
            else if (direction != 0)
            {
                _overscrollPulses = 0;
            }

            bool flick = math.abs(_velocity) >= (_profile == EarthScrollDeviceProfile.DetentWheel ? 7.5f : 4.5f) &&
                         math.abs(normalized) >= (_profile == EarthScrollDeviceProfile.DetentWheel ? 0.55f : 0.08f);
            return new EarthScrollState(
                rawDelta, normalized, _continuous, steps, _velocity, _directionHold,
                _reversalCount, flick, reversal, overscroll);
        }

        public static float Normalize(float rawDelta, EarthScrollDeviceProfile profile)
        {
            if (!math.isfinite(rawDelta) || math.abs(rawDelta) < 0.0001f) return 0f;
            if (profile == EarthScrollDeviceProfile.DetentWheel)
                return math.abs(rawDelta) >= 2f ? rawDelta / 120f : rawDelta;
            return math.clamp(rawDelta * 0.10f, -1.5f, 1.5f);
        }
    }
}
