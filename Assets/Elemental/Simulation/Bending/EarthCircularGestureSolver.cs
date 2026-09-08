using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum EarthCircularGestureDirection : sbyte
    {
        None = 0,
        Clockwise = -1,
        CounterClockwise = 1
    }

    public enum EarthGravityStructureIntent : byte
    {
        Neutral = 0,
        Repair = 1,
        Disassemble = 2
    }

    public struct EarthCircularGestureState
    {
        public float2 Center;
        public float2 PreviousDirection;
        public float2 PreviousPointer;
        public float2 BoundsMin;
        public float2 BoundsMax;
        public float Travel;
        public float AccumulatedDegrees;
        public bool HasPreviousDirection;
    }

    public readonly struct EarthCircularGestureSample
    {
        public EarthCircularGestureSample(
            EarthCircularGestureDirection direction,
            float phase01,
            float accumulatedDegrees,
            bool recognized)
        {
            Direction = direction;
            Phase01 = math.saturate(phase01);
            AccumulatedDegrees = accumulatedDegrees;
            Recognized = recognized;
        }

        public EarthCircularGestureDirection Direction { get; }
        public float Phase01 { get; }
        public float AccumulatedDegrees { get; }
        public bool Recognized { get; }
    }

    public static class EarthCircularGestureSolver
    {
        public static EarthCircularGestureState Begin(float2 center) =>
            new EarthCircularGestureState
            {
                Center = center,
                PreviousPointer = center,
                BoundsMin = center,
                BoundsMax = center,
                PreviousDirection = float2.zero,
                AccumulatedDegrees = 0f,
                HasPreviousDirection = false
            };

        public static EarthCircularGestureSample Step(
            ref EarthCircularGestureState state,
            float2 pointer,
            float minimumRadiusViewport = 0.028f,
            float recognitionDegrees = 28f,
            float fullPhaseDegrees = 300f,
            float maximumSampleDegrees = 72f)
        {
            state.BoundsMin = math.min(state.BoundsMin, pointer);
            state.BoundsMax = math.max(state.BoundsMax, pointer);
            // Integrate the turn of the drawn path, not its angle about mouse-down.
            // Mouse-down is usually on the rim of a player's circle; treating it
            // as the centre caps a full circle at roughly half the intended phase.
            float2 movement = pointer - state.PreviousPointer;
            float distance = math.length(movement);
            if (distance < math.max(0.0001f, minimumRadiusViewport * .04f))
                return Evaluate(in state, recognitionDegrees, fullPhaseDegrees);
            state.PreviousPointer = pointer;
            state.Travel += distance;
            float2 direction = movement / distance;
            if (!state.HasPreviousDirection)
            {
                state.PreviousDirection = direction;
                state.HasPreviousDirection = true;
                return Evaluate(in state, recognitionDegrees, fullPhaseDegrees);
            }

            float cross = state.PreviousDirection.x * direction.y -
                          state.PreviousDirection.y * direction.x;
            float dot = math.clamp(math.dot(state.PreviousDirection, direction), -1f, 1f);
            float degrees = math.degrees(math.atan2(cross, dot));
            // Travel alone is insufficient: repeatedly circling inside the old
            // deadzone must never arm a spell. Require an observed diameter.
            float diameter = math.cmax(state.BoundsMax - state.BoundsMin);
            if (diameter >= math.max(.0002f, minimumRadiusViewport * 2f) &&
                math.abs(degrees) <= math.max(1f, maximumSampleDegrees))
                state.AccumulatedDegrees += degrees;
            state.PreviousDirection = direction;
            return Evaluate(in state, recognitionDegrees, fullPhaseDegrees);
        }

        private static EarthCircularGestureSample Evaluate(
            in EarthCircularGestureState state,
            float recognitionDegrees,
            float fullPhaseDegrees)
        {
            float magnitude = math.abs(state.AccumulatedDegrees);
            float recognition = math.max(1f, recognitionDegrees);
            bool recognized = magnitude >= recognition;
            EarthCircularGestureDirection direction = !recognized
                ? EarthCircularGestureDirection.None
                : state.AccumulatedDegrees < 0f
                    ? EarthCircularGestureDirection.Clockwise
                    : EarthCircularGestureDirection.CounterClockwise;
            float phase = recognized
                ? math.unlerp(recognition, math.max(recognition + 1f, fullPhaseDegrees), magnitude)
                : 0f;
            return new EarthCircularGestureSample(
                direction,
                math.saturate(phase),
                state.AccumulatedDegrees,
                recognized);
        }
    }
}
