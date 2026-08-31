using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    public enum EarthRecoverableKnockdownPhase : byte
    {
        Inactive = 0,
        Physical = 1,
        AuthoredRecovery = 2
    }

    public readonly struct EarthRecoverableKnockdownState
    {
        public EarthRecoverableKnockdownState(
            EarthRecoverableKnockdownPhase phase,
            float elapsedSeconds,
            float physicalSeconds,
            float recoverySeconds)
        {
            Phase = phase;
            ElapsedSeconds = math.max(0f, elapsedSeconds);
            PhysicalSeconds = math.max(0.05f, physicalSeconds);
            RecoverySeconds = math.max(0.05f, recoverySeconds);
        }

        public EarthRecoverableKnockdownPhase Phase { get; }
        public float ElapsedSeconds { get; }
        public float PhysicalSeconds { get; }
        public float RecoverySeconds { get; }
        public bool IsActive => Phase != EarthRecoverableKnockdownPhase.Inactive;

        public static EarthRecoverableKnockdownState Begin(
            float physicalSeconds = 0.72f,
            float recoverySeconds = 0.72f) =>
            new EarthRecoverableKnockdownState(
                EarthRecoverableKnockdownPhase.Physical,
                0f,
                physicalSeconds,
                recoverySeconds);
    }

    public readonly struct EarthRecoverableKnockdownStep
    {
        public EarthRecoverableKnockdownStep(
            EarthRecoverableKnockdownState state,
            bool beginAuthoredRecovery,
            bool completed)
        {
            State = state;
            BeginAuthoredRecovery = beginAuthoredRecovery;
            Completed = completed;
        }

        public EarthRecoverableKnockdownState State { get; }
        public bool BeginAuthoredRecovery { get; }
        public bool Completed { get; }
    }

    /// <summary>
    /// Deterministic two-stage non-lethal knockdown. Physical motion gets one
    /// bounded window, followed by one authored recovery. There is no rebound,
    /// retry loop or animation callback in the authoritative decision.
    /// </summary>
    public static class EarthRecoverableKnockdownSolver
    {
        public static EarthRecoverableKnockdownStep Step(
            in EarthRecoverableKnockdownState state,
            float deltaTime)
        {
            if (!state.IsActive)
                return new EarthRecoverableKnockdownStep(state, false, false);

            float elapsed = state.ElapsedSeconds + math.max(0f, deltaTime);
            if (state.Phase == EarthRecoverableKnockdownPhase.Physical)
            {
                if (elapsed < state.PhysicalSeconds)
                    return new EarthRecoverableKnockdownStep(
                        new EarthRecoverableKnockdownState(
                            state.Phase,
                            elapsed,
                            state.PhysicalSeconds,
                            state.RecoverySeconds),
                        false,
                        false);
                float overflow = elapsed - state.PhysicalSeconds;
                return new EarthRecoverableKnockdownStep(
                    new EarthRecoverableKnockdownState(
                        EarthRecoverableKnockdownPhase.AuthoredRecovery,
                        overflow,
                        state.PhysicalSeconds,
                        state.RecoverySeconds),
                    true,
                    false);
            }

            if (elapsed < state.RecoverySeconds)
                return new EarthRecoverableKnockdownStep(
                    new EarthRecoverableKnockdownState(
                        state.Phase,
                        elapsed,
                        state.PhysicalSeconds,
                        state.RecoverySeconds),
                    false,
                    false);

            return new EarthRecoverableKnockdownStep(default, false, true);
        }
    }
}
