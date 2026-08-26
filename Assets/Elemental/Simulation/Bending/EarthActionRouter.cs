namespace Elemental.Simulation.Bending
{
    public enum EarthActionOwner : byte
    {
        None = 0,
        ShiftSpaceChord = 1,
        Wave = 2,
        Resonance = 3,
        Armor = 4,
        Surf = 5,
        Gravity = 6,
        Primary = 7,
        VectorField = 8,
        Pillar = 9,
        LandingCushion = 10,
        DualMouseEarth = 11
    }

    public enum EarthActionRoutePhase : byte
    {
        None = 0,
        Begin = 1,
        Continue = 2,
        Commit = 3,
        Cancel = 4
    }

    /// <summary>
    /// Read-only snapshot of the router's current timing window. Presentation and
    /// diagnostics can inspect chord progress without duplicating input ownership.
    /// </summary>
    public readonly struct EarthInputChordState
    {
        public EarthInputChordState(
            EarthActionOwner owner,
            float startedAt,
            float windowSeconds)
        {
            Owner = owner;
            StartedAt = startedAt;
            WindowSeconds = windowSeconds < 0.0001f ? 0.0001f : windowSeconds;
        }

        public EarthActionOwner Owner { get; }
        public float StartedAt { get; }
        public float WindowSeconds { get; }
        public bool IsPending => Owner == EarthActionOwner.ShiftSpaceChord;

        public float Elapsed(float now) => now > StartedAt ? now - StartedAt : 0f;

        public float Window01(float now)
        {
            if (!IsPending) return 0f;
            float value = Elapsed(now) / WindowSeconds;
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }

    public readonly struct EarthActionRouterFrame
    {
        public EarthActionRouterFrame(
            float time,
            bool cancelPressed = false,
            bool grounded = false,
            bool stableSupport = false,
            bool descending = false,
            float moveForward = 0f,
            bool modifierHeld = false,
            bool jumpPressed = false,
            bool jumpHeld = false,
            bool jumpReleased = false,
            bool primaryPressed = false,
            bool primaryHeld = false,
            bool primaryReleased = false,
            bool forcePressed = false,
            bool forceHeld = false,
            bool forceReleased = false,
            bool fieldPressed = false,
            bool fieldHeld = false,
            bool fieldReleased = false,
            bool hasRepairTarget = false,
            bool hasPrimedQuickStone = false,
            bool resonanceVolleyActive = false)
        {
            Time = time;
            CancelPressed = cancelPressed;
            Grounded = grounded;
            StableSupport = stableSupport;
            Descending = descending;
            MoveForward = moveForward;
            ModifierHeld = modifierHeld;
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            JumpReleased = jumpReleased;
            PrimaryPressed = primaryPressed;
            PrimaryHeld = primaryHeld;
            PrimaryReleased = primaryReleased;
            ForcePressed = forcePressed;
            ForceHeld = forceHeld;
            ForceReleased = forceReleased;
            FieldPressed = fieldPressed;
            FieldHeld = fieldHeld;
            FieldReleased = fieldReleased;
            HasRepairTarget = hasRepairTarget;
            HasPrimedQuickStone = hasPrimedQuickStone;
            ResonanceVolleyActive = resonanceVolleyActive;
        }

        public float Time { get; }
        public bool CancelPressed { get; }
        public bool Grounded { get; }
        public bool StableSupport { get; }
        public bool Descending { get; }
        public float MoveForward { get; }
        public bool ModifierHeld { get; }
        public bool JumpPressed { get; }
        public bool JumpHeld { get; }
        public bool JumpReleased { get; }
        public bool PrimaryPressed { get; }
        public bool PrimaryHeld { get; }
        public bool PrimaryReleased { get; }
        public bool ForcePressed { get; }
        public bool ForceHeld { get; }
        public bool ForceReleased { get; }
        public bool FieldPressed { get; }
        public bool FieldHeld { get; }
        public bool FieldReleased { get; }
        public bool HasRepairTarget { get; }
        public bool HasPrimedQuickStone { get; }
        public bool ResonanceVolleyActive { get; }
        public bool AnyMouseHeld => PrimaryHeld || ForceHeld || FieldHeld;
    }

    public readonly struct EarthActionRoute
    {
        public EarthActionRoute(
            EarthActionOwner owner,
            EarthActionRoutePhase phase,
            EarthActionIntentKind intent,
            EarthInputConsumption consumption,
            float charge01 = 0f)
        {
            Owner = owner;
            Phase = phase;
            Intent = intent;
            Consumption = consumption;
            Charge01 = charge01 < 0f ? 0f : charge01 > 1f ? 1f : charge01;
        }

        public EarthActionOwner Owner { get; }
        public EarthActionRoutePhase Phase { get; }
        public EarthActionIntentKind Intent { get; }
        public EarthInputConsumption Consumption { get; }
        public float Charge01 { get; }
        public bool HasOwner => Owner != EarthActionOwner.None;
        public bool Consumes(EarthInputConsumption input) => (Consumption & input) == input;
    }

    /// <summary>
    /// Canonical owner for overlapping Earth controls. This class does not execute
    /// Unity objects; it produces one route which the thin runtime behaviour executes.
    /// </summary>
    public sealed class EarthActionRouter
    {
        public const float DefaultChordWindowSeconds = 0.15f;
        public const float DefaultSurfForwardThreshold = 0.18f;

        private readonly float _chordWindowSeconds;
        private readonly float _surfForwardThreshold;
        private EarthActionOwner _owner;
        private float _startedAt;

        public EarthActionRouter(
            float chordWindowSeconds = DefaultChordWindowSeconds,
            float surfForwardThreshold = DefaultSurfForwardThreshold)
        {
            _chordWindowSeconds = chordWindowSeconds < 0.02f ? 0.02f : chordWindowSeconds;
            _surfForwardThreshold = surfForwardThreshold < 0f ? 0f : surfForwardThreshold;
        }

        public EarthActionOwner Owner => _owner;
        public bool HasActiveSession => _owner != EarthActionOwner.None;
        public EarthInputChordState ChordState =>
            new EarthInputChordState(_owner, _startedAt, _chordWindowSeconds);

        public EarthActionRoute Step(in EarthActionRouterFrame frame)
        {
            if (frame.CancelPressed)
            {
                EarthActionOwner canceled = _owner;
                Reset();
                return Route(
                    canceled,
                    EarthActionRoutePhase.Cancel,
                    EarthActionIntentKind.Cancel,
                    EarthInputConsumption.Cancel);
            }

            if (_owner != EarthActionOwner.None) return StepActive(in frame);

            if (frame.ResonanceVolleyActive)
                return Route(
                    EarthActionOwner.Resonance,
                    EarthActionRoutePhase.Continue,
                    EarthActionIntentKind.ResonanceVolley,
                    EarthInputConsumption.Primary | EarthInputConsumption.Force);

            if (frame.ModifierHeld && frame.FieldPressed)
                return Begin(
                    EarthActionOwner.Armor,
                    EarthActionIntentKind.ArmorHold,
                    EarthInputConsumption.Modifier | EarthInputConsumption.Field |
                    EarthInputConsumption.Parameter,
                    frame.Time);

            if (frame.ModifierHeld && frame.JumpPressed)
                return Begin(
                    EarthActionOwner.ShiftSpaceChord,
                    EarthActionIntentKind.WaveCharge,
                    EarthInputConsumption.Modifier | EarthInputConsumption.Jump |
                    EarthInputConsumption.Primary,
                    frame.Time);

            if (frame.ModifierHeld && frame.StableSupport &&
                frame.MoveForward >= _surfForwardThreshold && !frame.AnyMouseHeld)
                return Begin(
                    EarthActionOwner.Surf,
                    EarthActionIntentKind.Surf,
                    EarthInputConsumption.Modifier | EarthInputConsumption.Move,
                    frame.Time);

            if (frame.FieldPressed)
                return Begin(
                    EarthActionOwner.Gravity,
                    frame.HasRepairTarget ? EarthActionIntentKind.Repair : EarthActionIntentKind.GravityField,
                    EarthInputConsumption.Field,
                    frame.Time);

            if (frame.PrimaryPressed)
            {
                if (frame.PrimaryReleased || !frame.PrimaryHeld)
                    return Route(
                        EarthActionOwner.Primary,
                        EarthActionRoutePhase.Commit,
                        frame.HasPrimedQuickStone
                            ? EarthActionIntentKind.QuickFire
                            : EarthActionIntentKind.FullBend,
                        EarthInputConsumption.Primary);
                return Begin(
                    EarthActionOwner.Primary,
                    frame.HasPrimedQuickStone ? EarthActionIntentKind.QuickFire : EarthActionIntentKind.FullBend,
                    EarthInputConsumption.Primary,
                    frame.Time);
            }

            if (frame.ForcePressed)
            {
                if (frame.ForceReleased || !frame.ForceHeld)
                    return Route(
                        EarthActionOwner.VectorField,
                        EarthActionRoutePhase.Commit,
                        EarthActionIntentKind.VectorFieldPush,
                        EarthInputConsumption.Force);
                return Begin(
                    EarthActionOwner.VectorField,
                    EarthActionIntentKind.VectorFieldPush,
                    EarthInputConsumption.Force,
                    frame.Time);
            }

            if (frame.JumpPressed)
                return Begin(
                    frame.Descending ? EarthActionOwner.LandingCushion : EarthActionOwner.Pillar,
                    frame.Descending ? EarthActionIntentKind.LandingWave : EarthActionIntentKind.PillarCharge,
                    EarthInputConsumption.Jump,
                    frame.Time);

            return default;
        }

        public void Reset()
        {
            _owner = EarthActionOwner.None;
            _startedAt = 0f;
        }

        private EarthActionRoute StepActive(in EarthActionRouterFrame frame)
        {
            float elapsed = ChordState.Elapsed(frame.Time);
            switch (_owner)
            {
                case EarthActionOwner.ShiftSpaceChord:
                    if (frame.PrimaryPressed || frame.PrimaryHeld)
                    {
                        _owner = EarthActionOwner.Resonance;
                        _startedAt = frame.Time;
                        return Route(
                            _owner,
                            EarthActionRoutePhase.Begin,
                            EarthActionIntentKind.ResonanceCharge,
                            EarthInputConsumption.Modifier | EarthInputConsumption.Jump |
                            EarthInputConsumption.Primary);
                    }
                    if (frame.JumpReleased || !frame.ModifierHeld)
                    {
                        Reset();
                        return Route(
                            EarthActionOwner.Wave,
                            EarthActionRoutePhase.Commit,
                            EarthActionIntentKind.SelfRadialWave,
                            EarthInputConsumption.Modifier | EarthInputConsumption.Jump);
                    }
                    if (elapsed >= _chordWindowSeconds)
                    {
                        _owner = EarthActionOwner.Wave;
                        return Route(
                            _owner,
                            EarthActionRoutePhase.Begin,
                            EarthActionIntentKind.WaveCharge,
                            EarthInputConsumption.Modifier | EarthInputConsumption.Jump,
                            Charge(elapsed - _chordWindowSeconds, 1.1f));
                    }
                    return Route(
                        _owner,
                        EarthActionRoutePhase.Continue,
                        EarthActionIntentKind.WaveCharge,
                        EarthInputConsumption.Modifier | EarthInputConsumption.Jump |
                        EarthInputConsumption.Primary,
                        elapsed / _chordWindowSeconds);

                case EarthActionOwner.Wave:
                    if (frame.JumpReleased || !frame.ModifierHeld)
                        return CommitAndReset(
                            EarthActionOwner.Wave,
                            EarthActionIntentKind.SelfRadialWave,
                            EarthInputConsumption.Modifier | EarthInputConsumption.Jump,
                            Charge(elapsed - _chordWindowSeconds, 1.1f));
                    return Route(
                        _owner,
                        EarthActionRoutePhase.Continue,
                        EarthActionIntentKind.WaveCharge,
                        EarthInputConsumption.Modifier | EarthInputConsumption.Jump,
                        Charge(elapsed - _chordWindowSeconds, 1.1f));

                case EarthActionOwner.Resonance:
                    if (frame.JumpReleased || !frame.ModifierHeld)
                        return CommitAndReset(
                            EarthActionOwner.Resonance,
                            EarthActionIntentKind.ResonanceCharge,
                            EarthInputConsumption.Modifier | EarthInputConsumption.Jump |
                            EarthInputConsumption.Primary,
                            Charge(elapsed, 2.6f));
                    return Route(
                        _owner,
                        EarthActionRoutePhase.Continue,
                        EarthActionIntentKind.ResonanceCharge,
                        EarthInputConsumption.Modifier | EarthInputConsumption.Jump |
                        EarthInputConsumption.Primary,
                        Charge(elapsed, 2.6f));

                case EarthActionOwner.Armor:
                    if (frame.FieldReleased || !frame.ModifierHeld)
                        return CommitAndReset(
                            _owner,
                            EarthActionIntentKind.ArmorHold,
                            EarthInputConsumption.Modifier | EarthInputConsumption.Field |
                            EarthInputConsumption.Parameter | EarthInputConsumption.Primary |
                            EarthInputConsumption.Force);
                    return Route(
                        _owner,
                        EarthActionRoutePhase.Continue,
                        EarthActionIntentKind.ArmorHold,
                        EarthInputConsumption.Modifier | EarthInputConsumption.Field |
                        EarthInputConsumption.Parameter | EarthInputConsumption.Primary |
                        EarthInputConsumption.Force);

                case EarthActionOwner.Surf:
                    if (!frame.ModifierHeld || !frame.StableSupport ||
                        frame.MoveForward < _surfForwardThreshold || frame.AnyMouseHeld)
                        return CommitAndReset(
                            _owner,
                            EarthActionIntentKind.Surf,
                            EarthInputConsumption.Modifier | EarthInputConsumption.Move);
                    return Route(
                        _owner,
                        EarthActionRoutePhase.Continue,
                        EarthActionIntentKind.Surf,
                        EarthInputConsumption.Modifier | EarthInputConsumption.Move,
                        Charge(elapsed, 1.2f));

                case EarthActionOwner.Gravity:
                    if (frame.FieldReleased)
                        return CommitAndReset(_owner, EarthActionIntentKind.GravityField, EarthInputConsumption.Field);
                    return Route(_owner, EarthActionRoutePhase.Continue,
                        frame.HasRepairTarget ? EarthActionIntentKind.Repair : EarthActionIntentKind.GravityField,
                        EarthInputConsumption.Field);

                case EarthActionOwner.Primary:
                    if (frame.PrimaryReleased || !frame.PrimaryHeld)
                        return CommitAndReset(_owner, EarthActionIntentKind.FullBend, EarthInputConsumption.Primary);
                    return Route(_owner, EarthActionRoutePhase.Continue,
                        frame.HasPrimedQuickStone ? EarthActionIntentKind.QuickFire : EarthActionIntentKind.FullBend,
                        EarthInputConsumption.Primary);

                case EarthActionOwner.VectorField:
                    if (frame.ForceReleased || !frame.ForceHeld)
                        return CommitAndReset(_owner, EarthActionIntentKind.VectorFieldPush, EarthInputConsumption.Force);
                    return Route(_owner, EarthActionRoutePhase.Continue,
                        EarthActionIntentKind.VectorFieldPush, EarthInputConsumption.Force);

                case EarthActionOwner.Pillar:
                    if (frame.JumpReleased)
                        return CommitAndReset(_owner, EarthActionIntentKind.PillarCharge, EarthInputConsumption.Jump,
                            Charge(elapsed, 1.45f));
                    return Route(_owner, EarthActionRoutePhase.Continue,
                        EarthActionIntentKind.PillarCharge, EarthInputConsumption.Jump, Charge(elapsed, 1.45f));

                case EarthActionOwner.LandingCushion:
                    if (frame.JumpReleased)
                        return CommitAndReset(_owner, EarthActionIntentKind.LandingWave, EarthInputConsumption.Jump);
                    return Route(_owner, EarthActionRoutePhase.Continue,
                        EarthActionIntentKind.LandingWave, EarthInputConsumption.Jump);
                default:
                    Reset();
                    return default;
            }
        }

        private EarthActionRoute Begin(
            EarthActionOwner owner,
            EarthActionIntentKind intent,
            EarthInputConsumption consumption,
            float time)
        {
            _owner = owner;
            _startedAt = time;
            return Route(owner, EarthActionRoutePhase.Begin, intent, consumption);
        }

        private EarthActionRoute CommitAndReset(
            EarthActionOwner owner,
            EarthActionIntentKind intent,
            EarthInputConsumption consumption,
            float charge01 = 0f)
        {
            Reset();
            return Route(owner, EarthActionRoutePhase.Commit, intent, consumption, charge01);
        }

        private static EarthActionRoute Route(
            EarthActionOwner owner,
            EarthActionRoutePhase phase,
            EarthActionIntentKind intent,
            EarthInputConsumption consumption,
            float charge01 = 0f) =>
            new EarthActionRoute(owner, phase, intent, consumption, charge01);

        private static float Charge(float seconds, float fullSeconds)
        {
            float linear = seconds <= 0f ? 0f : seconds / fullSeconds;
            if (linear >= 1f) return 1f;
            return linear * linear * (3f - 2f * linear);
        }
    }
}
