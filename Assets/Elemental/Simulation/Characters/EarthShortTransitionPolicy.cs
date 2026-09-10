using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthShortTransition : byte { None, StartWalk, CrouchExit, StepDown }

    public struct EarthShortTransitionInput
    {
        public bool Grounded, Crouched, ProtectedOwner, DeliberateJump;
        public bool HasLandingCandidate;
        public float TangentSpeed, ForwardSpeed, VerticalSpeed, FloorDistance;
        public float DesiredSpeed, MaximumStartWalkSpeed;
    }

    public struct EarthShortTransitionState
    {
        public bool Initialized, WasGrounded, WasCrouched, StepDownConsumed, HasSeenSupport, StartArmed;
        public float IdleSeconds, AirSeconds, Elapsed;
        public EarthShortTransition Active;
    }

    public readonly struct EarthShortTransitionSample
    {
        public readonly EarthShortTransition Kind;
        public readonly bool Changed;
        public readonly float Elapsed;
        public EarthShortTransitionSample(EarthShortTransition kind, bool changed, float elapsed)
        { Kind = kind; Changed = changed; Elapsed = elapsed; }
    }

    /// <summary>Short optional bridge poses; never delay motor input or physical contact.</summary>
    public static class EarthShortTransitionPolicy
    {
        public static string StateName(EarthShortTransition kind) => kind switch
        {
            EarthShortTransition.StartWalk => "Start Walk Transition",
            EarthShortTransition.CrouchExit => "Crouch Exit Transition",
            EarthShortTransition.StepDown => "Step Down Transition",
            _ => string.Empty
        };
        public static float Duration(EarthShortTransition kind) => kind switch
        {
            EarthShortTransition.StartWalk => .32f,
            EarthShortTransition.CrouchExit => .38f,
            EarthShortTransition.StepDown => .36f,
            _ => 0f
        };

        public static EarthShortTransitionSample Step(ref EarthShortTransitionState state,
            in EarthShortTransitionInput input, float deltaSeconds)
        {
            float delta = math.isfinite(deltaSeconds) ? math.max(0f, deltaSeconds) : 0f;
            EarthShortTransition previous = state.Active;
            if (!state.Initialized)
            {
                state.Initialized = true;
                state.WasGrounded = input.Grounded;
                state.WasCrouched = input.Crouched;
            }
            if (input.Grounded) { state.AirSeconds = 0f; state.StepDownConsumed = false; state.HasSeenSupport = true; }
            else state.AirSeconds += delta;

            bool blocked = input.ProtectedOwner || input.DeliberateJump;
            float walkLimit = input.MaximumStartWalkSpeed > .12f ? input.MaximumStartWalkSpeed : 2f;
            bool walkCompatible = math.max(input.TangentSpeed, input.DesiredSpeed) <= walkLimit;
            if (blocked || !input.Grounded || input.Crouched) state.StartArmed = false;
            else if (state.IdleSeconds >= .18f) state.StartArmed = true;
            if (state.Active != EarthShortTransition.None)
            {
                state.Elapsed += delta;
                bool invalid = state.Active switch
                {
                    EarthShortTransition.StartWalk => !input.Grounded || input.Crouched ||
                        input.ForwardSpeed < .15f || !walkCompatible,
                    EarthShortTransition.CrouchExit => !input.Grounded || input.Crouched ||
                        input.TangentSpeed > .6f,
                    EarthShortTransition.StepDown => input.Grounded || input.VerticalSpeed > .5f ||
                        input.VerticalSpeed < -4.5f || input.HasLandingCandidate && input.FloorDistance > .9f,
                    _ => true
                };
                if (blocked || invalid || state.Elapsed >= Duration(state.Active))
                { state.Active = EarthShortTransition.None; state.Elapsed = 0f; }
            }
            // A completed/cancelled bridge cannot restart itself on the same frame.
            if (state.Active == EarthShortTransition.None && previous == EarthShortTransition.None && !blocked)
            {
                if (state.WasCrouched && !input.Crouched && input.Grounded && input.TangentSpeed <= .6f)
                    state.Active = EarthShortTransition.CrouchExit;
                else if (!input.Grounded && state.HasSeenSupport && !state.StepDownConsumed && state.AirSeconds <= .18f &&
                    input.HasLandingCandidate && input.FloorDistance >= .08f && input.FloorDistance <= .85f &&
                    input.VerticalSpeed <= .1f && input.VerticalSpeed >= -3.5f && input.TangentSpeed >= .15f)
                { state.Active = EarthShortTransition.StepDown; state.StepDownConsumed = true; }
                else if (input.Grounded && !input.Crouched && state.StartArmed &&
                    input.ForwardSpeed >= .35f && walkCompatible)
                    state.Active = EarthShortTransition.StartWalk;
            }
            // Keep the rest-to-start latch through the .12..35m/s acceleration
            // interval. Otherwise a gradual analog start loses its idle history
            // before reaching the entry threshold. Any established travel consumes it.
            if (input.TangentSpeed >= .35f) state.StartArmed = false;
            state.IdleSeconds = input.Grounded && !input.Crouched && !blocked && input.TangentSpeed < .12f
                ? state.IdleSeconds + delta : 0f;
            state.WasGrounded = input.Grounded;
            state.WasCrouched = input.Crouched;
            return new EarthShortTransitionSample(state.Active, state.Active != previous, state.Elapsed);
        }
    }
}
