using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public struct EarthTurnStepState
    {
        public bool Active, ClockEntered;
        public float Direction, CycleEnd, UncommandedYaw;
    }

    /// <summary>One real authored step finishes even when a short turn command ends.
    /// The controller's sampled clip phase owns completion; gameplay never waits.</summary>
    public static class EarthTurnStepSequence
    {
        public static void Step(ref EarthTurnStepState state, float turnInput, float measuredYawDelta,
            bool eligible, bool turnClockVisible, float normalizedTime)
        {
            if (!eligible || !math.isfinite(turnInput) || !math.isfinite(measuredYawDelta))
            { state = default; return; }
            float request = math.abs(turnInput) >= .05f ? math.sign(turnInput) : 0f;
            if (request == 0f && math.abs(measuredYawDelta) >= .5f)
            {
                state.UncommandedYaw = math.sign(state.UncommandedYaw) == math.sign(measuredYawDelta)
                    ? state.UncommandedYaw + measuredYawDelta : measuredYawDelta;
                if (math.abs(state.UncommandedYaw) >= 5f) request = math.sign(state.UncommandedYaw);
            }
            else state.UncommandedYaw = 0f;
            if (!state.Active)
            {
                if (request == 0f) return;
                state.Active = true; state.Direction = request; state.ClockEntered = false;
                state.UncommandedYaw = 0f;
            }
            if (!turnClockVisible || !math.isfinite(normalizedTime)) return;
            if (!state.ClockEntered)
            {
                state.ClockEntered = true;
                state.CycleEnd = math.floor(math.max(0f, normalizedTime)) + 1f;
                return;
            }
            if (normalizedTime < state.CycleEnd) return;
            if (request == 0f) { state = default; return; }
            state.Direction = request;
            state.CycleEnd = math.floor(normalizedTime) + 1f;
        }
    }
}
