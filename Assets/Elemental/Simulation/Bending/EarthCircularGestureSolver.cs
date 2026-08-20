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
            float2 radial = pointer - state.Center;
            float radius = math.length(radial);
            if (radius < math.max(0.0001f, minimumRadiusViewport))
                return Evaluate(in state, recognitionDegrees, fullPhaseDegrees);

            float2 direction = radial / radius;
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
            if (math.abs(degrees) <= math.max(1f, maximumSampleDegrees))
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
