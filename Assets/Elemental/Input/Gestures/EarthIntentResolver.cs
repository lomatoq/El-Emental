using Unity.Profiling;

namespace Elemental.Input.Gestures
{
    public enum EarthSourceKind : byte
    {
        Invalid = 0,
        Terrain = 1,
        Rock = 2,
        IntactStructure = 3,
        BrokenStructure = 4
    }

    public enum EarthReticleState : byte
    {
        Invalid = 0,
        Terrain = 1,
        Rock = 2,
        Intact = 3,
        Broken = 4,
        Overmass = 5,
        Obstructed = 6,
        Ambiguous = 7,
        Valid = 8
    }

    public enum EarthIntentKind : byte
    {
        None = 0,
        Acquire = 1,
        Manipulate = 2,
        RaiseWall = 3,
        RaisePlatform = 4,
        Throw = 5,
        VectorFieldPush = 6,
        GravityGrip = 7,
        Repair = 8,
        Pillar = 9,
        GroundWave = 10
    }

    public readonly struct EarthInputContext
    {
        public EarthInputContext(
            EarthSourceKind source,
            bool activeSession,
            bool primary,
            bool force,
            bool field,
            bool modifier,
            bool overmass = false,
            bool obstructed = false)
        {
            Source = source;
            ActiveSession = activeSession;
            Primary = primary;
            Force = force;
            Field = field;
            Modifier = modifier;
            Overmass = overmass;
            Obstructed = obstructed;
        }

        public EarthSourceKind Source { get; }
        public bool ActiveSession { get; }
        public bool Primary { get; }
        public bool Force { get; }
        public bool Field { get; }
        public bool Modifier { get; }
        public bool Overmass { get; }
        public bool Obstructed { get; }
    }

    public readonly struct EarthResolvedIntent
    {
        public EarthResolvedIntent(
            EarthIntentKind kind,
            EarthReticleState reticle,
            bool accepted,
            EarthGestureResult gesture)
        {
            Kind = kind;
            Reticle = reticle;
            Accepted = accepted;
            Gesture = gesture;
        }

        public EarthIntentKind Kind { get; }
        public EarthReticleState Reticle { get; }
        public bool Accepted { get; }
        public EarthGestureResult Gesture { get; }
    }

    public static class EarthIntentResolver
    {
        private static readonly ProfilerMarker ResolveMarker =
            new ProfilerMarker("Elemental.Earth.Intent.Resolve");

        public static bool NeedsGestureRecognition(in EarthInputContext context)
        {
            if (context.Source == EarthSourceKind.Invalid || context.Overmass || context.Obstructed)
                return false;
            if (context.ActiveSession || context.Field) return false;
            return context.Primary && context.Source == EarthSourceKind.Terrain;
        }

        public static EarthGestureTemplateMask RelevantTemplates(in EarthInputContext context)
        {
            if (!NeedsGestureRecognition(in context)) return EarthGestureTemplateMask.None;
            return context.Force
                ? EarthGestureTemplateMask.Line | EarthGestureTemplateMask.Flick
                : EarthGestureTemplateMask.Structures;
        }

        public static EarthResolvedIntent Resolve(
            in EarthInputContext context,
            in EarthGestureResult gesture)
        {
            using (ResolveMarker.Auto())
            {
                if (context.Source == EarthSourceKind.Invalid)
                    return Reject(EarthReticleState.Invalid, gesture);
                if (context.Overmass) return Reject(EarthReticleState.Overmass, gesture);
                if (context.Obstructed) return Reject(EarthReticleState.Obstructed, gesture);

                if (context.ActiveSession)
                    return Accept(EarthIntentKind.Manipulate, gesture);
                if (context.Field)
                {
                    EarthIntentKind fieldIntent = context.Source == EarthSourceKind.BrokenStructure
                        ? EarthIntentKind.Repair
                        : EarthIntentKind.GravityGrip;
                    return Accept(fieldIntent, gesture);
                }
                if (context.Force)
                {
                    if (context.Primary && context.Source == EarthSourceKind.Terrain)
                    {
                        if (!gesture.Accepted)
                            return Reject(gesture.Best == EarthGestureKind.Invalid
                                ? EarthReticleState.Invalid
                                : EarthReticleState.Ambiguous, gesture);
                        if (gesture.Best == EarthGestureKind.Line || gesture.Best == EarthGestureKind.Flick)
                            return Accept(EarthIntentKind.GroundWave, gesture);
                        return Reject(EarthReticleState.Invalid, gesture);
                    }
                    return Accept(EarthIntentKind.VectorFieldPush, gesture);
                }
                if (!context.Primary) return Reject(SourceReticle(context.Source), gesture);
                if (context.Source == EarthSourceKind.Rock ||
                    context.Source == EarthSourceKind.IntactStructure ||
                    context.Source == EarthSourceKind.BrokenStructure)
                    return Accept(EarthIntentKind.Acquire, gesture);

                if (!gesture.Accepted)
                    return Reject(gesture.Best == EarthGestureKind.Invalid
                        ? EarthReticleState.Invalid
                        : EarthReticleState.Ambiguous, gesture);
                if (gesture.Best == EarthGestureKind.Line)
                    return Accept(EarthIntentKind.RaiseWall, gesture);
                if (gesture.Best == EarthGestureKind.Arc ||
                    gesture.Best == EarthGestureKind.ClosedContour)
                    return Accept(EarthIntentKind.RaisePlatform, gesture);
                return Reject(EarthReticleState.Invalid, gesture);
            }
        }

        private static EarthResolvedIntent Accept(EarthIntentKind kind, EarthGestureResult gesture) =>
            new EarthResolvedIntent(kind, EarthReticleState.Valid, true, gesture);

        private static EarthResolvedIntent Reject(EarthReticleState state, EarthGestureResult gesture) =>
            new EarthResolvedIntent(EarthIntentKind.None, state, false, gesture);

        private static EarthReticleState SourceReticle(EarthSourceKind source) => source switch
        {
            EarthSourceKind.Terrain => EarthReticleState.Terrain,
            EarthSourceKind.Rock => EarthReticleState.Rock,
            EarthSourceKind.IntactStructure => EarthReticleState.Intact,
            EarthSourceKind.BrokenStructure => EarthReticleState.Broken,
            _ => EarthReticleState.Invalid
        };
    }
}
