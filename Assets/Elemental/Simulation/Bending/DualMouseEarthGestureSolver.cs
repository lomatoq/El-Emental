using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum DualMouseEarthResultKind : byte
    {
        None = 0,
        Pending = 1,
        Tracking = 2,
        FallbackPrimary = 3,
        FallbackForce = 4,
        StompStone = 5,
        PillarCrest = 6,
        Cancel = 7
    }

    public readonly struct DualMouseEarthGestureFrame
    {
        public DualMouseEarthGestureFrame(
            float time,
            bool primaryPressed,
            bool primaryHeld,
            bool primaryReleased,
            bool forcePressed,
            bool forceHeld,
            bool forceReleased,
            float2 pointerViewport,
            bool cancel = false)
        {
            Time = time;
            PrimaryPressed = primaryPressed;
            PrimaryHeld = primaryHeld;
            PrimaryReleased = primaryReleased;
            ForcePressed = forcePressed;
            ForceHeld = forceHeld;
            ForceReleased = forceReleased;
            PointerViewport = pointerViewport;
            Cancel = cancel;
        }

        public float Time { get; }
        public bool PrimaryPressed { get; }
        public bool PrimaryHeld { get; }
        public bool PrimaryReleased { get; }
        public bool ForcePressed { get; }
        public bool ForceHeld { get; }
        public bool ForceReleased { get; }
        public float2 PointerViewport { get; }
        public bool Cancel { get; }
    }

    public readonly struct DualMouseEarthGestureResult
    {
        public DualMouseEarthGestureResult(
            DualMouseEarthResultKind kind,
            bool ownsInput,
            int crestCount = 0,
            float2 deltaViewport = default,
            float2 startPointer = default,
            float2 endPointer = default)
        {
            Kind = kind;
            OwnsInput = ownsInput;
            CrestCount = crestCount;
            DeltaViewport = deltaViewport;
            StartPointer = startPointer;
            EndPointer = endPointer;
            Travel = math.length(deltaViewport);
            Direction = Travel > 0.00001f ? deltaViewport / Travel : float2.zero;
        }

        public DualMouseEarthResultKind Kind { get; }
        public bool OwnsInput { get; }
        public int CrestCount { get; }
        public float2 DeltaViewport { get; }
        public float2 StartPointer { get; }
        public float2 EndPointer { get; }
        public float2 Direction { get; }
        public float Travel { get; }
    }

    public sealed class DualMouseEarthGestureSolver
    {
        public const float ChordWindowSeconds = 0.08f;
        public const float TapMaximumSeconds = 0.20f;
        public const float TapMaximumTravelViewport = 0.035f;
        public const float HoldMinimumSeconds = 0.20f;
        public const float CrestMinimumTravelViewport = 0.045f;

        private State _state;
        private float _startedAt;
        private float2 _startPointer;
        private float _maximumTravel;
        private float2 _crestDelta;

        private enum State : byte
        {
            Idle,
            PendingPrimary,
            PendingForce,
            Chord
        }

        public bool OwnsInput => _state != State.Idle;

        public DualMouseEarthGestureResult Step(in DualMouseEarthGestureFrame frame)
        {
            if (frame.Cancel)
            {
                bool owned = OwnsInput;
                Reset();
                return owned
                    ? new DualMouseEarthGestureResult(DualMouseEarthResultKind.Cancel, false)
                    : default;
            }

            if (_state == State.Idle)
            {
                if (frame.PrimaryPressed && frame.ForcePressed)
                {
                    Begin(State.Chord, frame.Time, frame.PointerViewport);
                    return new DualMouseEarthGestureResult(DualMouseEarthResultKind.Tracking, true);
                }
                if (frame.PrimaryPressed)
                {
                    Begin(State.PendingPrimary, frame.Time, frame.PointerViewport);
                    return new DualMouseEarthGestureResult(DualMouseEarthResultKind.Pending, true);
                }
                if (frame.ForcePressed)
                {
                    Begin(State.PendingForce, frame.Time, frame.PointerViewport);
                    return new DualMouseEarthGestureResult(DualMouseEarthResultKind.Pending, true);
                }
                return default;
            }

            float elapsed = math.max(0f, frame.Time - _startedAt);
            float2 delta = frame.PointerViewport - _startPointer;
            _maximumTravel = math.max(_maximumTravel, math.length(delta));
            if (math.lengthsq(delta) > math.lengthsq(_crestDelta)) _crestDelta = delta;
            if (_state == State.PendingPrimary)
            {
                if (elapsed <= ChordWindowSeconds && frame.PrimaryHeld &&
                    (frame.ForcePressed || frame.ForceHeld))
                {
                    _state = State.Chord;
                    return new DualMouseEarthGestureResult(DualMouseEarthResultKind.Tracking, true);
                }
                if (frame.PrimaryReleased || elapsed >= ChordWindowSeconds)
                {
                    Reset();
                    return new DualMouseEarthGestureResult(DualMouseEarthResultKind.FallbackPrimary, false);
                }
                return new DualMouseEarthGestureResult(DualMouseEarthResultKind.Pending, true);
            }
            if (_state == State.PendingForce)
            {
                if (elapsed <= ChordWindowSeconds && frame.ForceHeld &&
                    (frame.PrimaryPressed || frame.PrimaryHeld))
                {
                    _state = State.Chord;
                    return new DualMouseEarthGestureResult(DualMouseEarthResultKind.Tracking, true);
                }
                if (frame.ForceReleased || elapsed >= ChordWindowSeconds)
                {
                    Reset();
                    return new DualMouseEarthGestureResult(DualMouseEarthResultKind.FallbackForce, false);
                }
                return new DualMouseEarthGestureResult(DualMouseEarthResultKind.Pending, true);
            }

            if (frame.PrimaryHeld || frame.ForceHeld)
                return new DualMouseEarthGestureResult(
                    DualMouseEarthResultKind.Tracking,
                    true,
                    0,
                    delta,
                    _startPointer,
                    frame.PointerViewport);

            DualMouseEarthGestureResult result;
            if (elapsed <= TapMaximumSeconds && _maximumTravel < TapMaximumTravelViewport)
            {
                result = new DualMouseEarthGestureResult(
                    DualMouseEarthResultKind.StompStone,
                    false,
                    0,
                    delta,
                    _startPointer,
                    frame.PointerViewport);
            }
            else if (elapsed >= HoldMinimumSeconds &&
                     math.length(_crestDelta) >= CrestMinimumTravelViewport)
            {
                float travel = math.length(_crestDelta);
                int count = travel < 0.09f ? 1 : travel < 0.15f ? 3 :
                    travel < 0.23f ? 5 : 7;
                result = new DualMouseEarthGestureResult(
                    DualMouseEarthResultKind.PillarCrest,
                    false,
                    count,
                    _crestDelta,
                    _startPointer,
                    _startPointer + _crestDelta);
            }
            else
            {
                result = new DualMouseEarthGestureResult(
                    DualMouseEarthResultKind.Cancel,
                    false,
                    0,
                    delta,
                    _startPointer,
                    frame.PointerViewport);
            }
            Reset();
            return result;
        }

        public void Reset()
        {
            _state = State.Idle;
            _startedAt = 0f;
            _startPointer = default;
            _maximumTravel = 0f;
            _crestDelta = default;
        }

        private void Begin(State state, float time, float2 pointer)
        {
            _state = state;
            _startedAt = time;
            _startPointer = pointer;
            _maximumTravel = 0f;
            _crestDelta = default;
        }
    }
}
