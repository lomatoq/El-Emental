using Unity.Profiling;

namespace Elemental.Simulation.Bending
{
    public static class EarthActionIntentResolver
    {
        private static readonly ProfilerMarker ResolveMarker =
            new ProfilerMarker("Elemental.Earth.ActionIntent.Resolve");

        public const float DefaultMoveThreshold = 0.18f;
        public const float DefaultTapSeconds = 0.19f;
        public const float DefaultHoldSeconds = 0.20f;
        public const float DefaultTapTravelViewport = 0.018f;

        public static EarthActionIntent Resolve(
            in EarthGestureFrame frame,
            float moveThreshold = DefaultMoveThreshold,
            float tapSeconds = DefaultTapSeconds,
            float holdSeconds = DefaultHoldSeconds,
            float tapTravelViewport = DefaultTapTravelViewport)
        {
            using (ResolveMarker.Auto())
            {
                // Priority is deliberately explicit. One frame yields one owner.
                if (frame.CancelPressed || frame.Invalidated)
                    return Intent(EarthActionIntentKind.Cancel, EarthInputConsumption.Cancel);

                if (!frame.Grounded && frame.Descending && frame.LandingWaveArmed)
                    return Intent(EarthActionIntentKind.LandingWave,
                        EarthInputConsumption.Modifier | EarthInputConsumption.Jump);

                float safeMoveThreshold = moveThreshold < 0f ? 0f : moveThreshold;
                if (frame.Grounded && frame.ModifierHeld && frame.MoveMagnitude >= safeMoveThreshold)
                    return Intent(EarthActionIntentKind.Surf,
                        EarthInputConsumption.Modifier | EarthInputConsumption.Move);

                if (frame.Grounded && frame.ModifierHeld && frame.JumpPressed)
                    return Intent(EarthActionIntentKind.SelfRadialWave,
                        EarthInputConsumption.Modifier | EarthInputConsumption.Jump);

                if (frame.HasControlledTarget)
                    return Intent(EarthActionIntentKind.ManipulateTarget,
                        EarthInputConsumption.Primary | EarthInputConsumption.Force);

                if (frame.HasPrimedQuickStone && frame.ForceHeld)
                    return Intent(EarthActionIntentKind.QuickFire, EarthInputConsumption.Force);

                float safeTapSeconds = tapSeconds < 0f ? 0f : tapSeconds;
                float safeTapTravel = tapTravelViewport < 0f ? 0f : tapTravelViewport;
                bool emptyTap = frame.PrimaryReleased && !frame.PointerOverEarthTarget &&
                                frame.PrimaryHeldSeconds <= safeTapSeconds &&
                                frame.PointerTravelViewport <= safeTapTravel;
                if (emptyTap)
                    return Intent(EarthActionIntentKind.QuickPrime, EarthInputConsumption.Primary);

                float safeHoldSeconds = holdSeconds < 0f ? 0f : holdSeconds;
                bool fullBend = (frame.PrimaryHeld &&
                                 (frame.PrimaryHeldSeconds >= safeHoldSeconds ||
                                  frame.PointerTravelViewport > safeTapTravel)) ||
                                (frame.PrimaryReleased && !emptyTap);
                if (fullBend)
                    return Intent(EarthActionIntentKind.FullBend, EarthInputConsumption.Primary,
                        safeHoldSeconds <= 0f ? 1f : frame.PrimaryHeldSeconds / safeHoldSeconds);

                if (frame.FieldHeld)
                    return Intent(frame.HasRepairTarget
                            ? EarthActionIntentKind.Repair
                            : EarthActionIntentKind.GravityField,
                        EarthInputConsumption.Field);

                if (frame.JumpPressed)
                    return Intent(EarthActionIntentKind.PillarJump, EarthInputConsumption.Jump);

                return default;
            }
        }

        private static EarthActionIntent Intent(
            EarthActionIntentKind kind,
            EarthInputConsumption consumption,
            float charge01 = 0f) =>
            new EarthActionIntent(kind, consumption, charge01);
    }
}
