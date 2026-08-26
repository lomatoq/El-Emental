using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    public enum EarthDuelFighterPhase : byte
    {
        Active = 0,
        KnockedOut = 1
    }

    public readonly struct EarthDuelFighterState
    {
        public EarthDuelFighterState(EarthDuelFighterPhase phase, float remainingSeconds)
        {
            Phase = phase;
            RemainingSeconds = math.max(0f, remainingSeconds);
        }

        public EarthDuelFighterPhase Phase { get; }
        public float RemainingSeconds { get; }
        public static EarthDuelFighterState Active =>
            new EarthDuelFighterState(EarthDuelFighterPhase.Active, 0f);
    }

    public readonly struct EarthDuelFighterStep
    {
        public EarthDuelFighterStep(
            in EarthDuelFighterState state,
            bool respawnThisTick,
            float stoneFade01)
        {
            State = state;
            RespawnThisTick = respawnThisTick;
            StoneFade01 = math.saturate(stoneFade01);
        }

        public EarthDuelFighterState State { get; }
        public bool RespawnThisTick { get; }
        public float StoneFade01 { get; }
    }

    public static class EarthDuelRespawnSolver
    {
        public static EarthDuelFighterState KnockOut(float respawnSeconds)
        {
            if (!float.IsFinite(respawnSeconds) || respawnSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds));
            return new EarthDuelFighterState(EarthDuelFighterPhase.KnockedOut, respawnSeconds);
        }

        public static EarthDuelFighterStep Step(
            in EarthDuelFighterState state,
            float deltaTime,
            float stoneFadeSeconds = 0.35f)
        {
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!float.IsFinite(stoneFadeSeconds) || stoneFadeSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(stoneFadeSeconds));
            if (state.Phase == EarthDuelFighterPhase.Active)
                return new EarthDuelFighterStep(in state, false, 0f);
            if (state.Phase != EarthDuelFighterPhase.KnockedOut ||
                !float.IsFinite(state.RemainingSeconds))
                return new EarthDuelFighterStep(EarthDuelFighterState.Active, true, 1f);

            float remaining = math.max(0f, state.RemainingSeconds - deltaTime);
            if (remaining > 0f)
            {
                var knockedOut = new EarthDuelFighterState(
                    EarthDuelFighterPhase.KnockedOut,
                    remaining);
                float fade01 = 1f - math.saturate(remaining / stoneFadeSeconds);
                return new EarthDuelFighterStep(in knockedOut, false, fade01);
            }
            EarthDuelFighterState active = EarthDuelFighterState.Active;
            return new EarthDuelFighterStep(in active, true, 1f);
        }
    }
}
