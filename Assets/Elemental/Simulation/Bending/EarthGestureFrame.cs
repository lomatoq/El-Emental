namespace Elemental.Simulation.Bending
{
    /// <summary>
    /// One render-frame snapshot of earth controls plus the gameplay context
    /// required to resolve them. Distances are normalized viewport units, so
    /// tap/drag classification is independent of display resolution.
    /// </summary>
    public readonly struct EarthGestureFrame
    {
        public EarthGestureFrame(
            bool cancelPressed = false,
            bool invalidated = false,
            bool grounded = false,
            bool descending = false,
            float moveMagnitude = 0f,
            bool modifierHeld = false,
            bool jumpPressed = false,
            bool landingWaveArmed = false,
            bool primaryPressed = false,
            bool primaryHeld = false,
            bool primaryReleased = false,
            float primaryHeldSeconds = 0f,
            float pointerTravelViewport = 0f,
            bool forceHeld = false,
            bool fieldHeld = false,
            bool pointerOverEarthTarget = false,
            bool hasControlledTarget = false,
            bool hasPrimedQuickStone = false,
            bool hasRepairTarget = false)
        {
            CancelPressed = cancelPressed;
            Invalidated = invalidated;
            Grounded = grounded;
            Descending = descending;
            MoveMagnitude = moveMagnitude < 0f ? 0f : moveMagnitude;
            ModifierHeld = modifierHeld;
            JumpPressed = jumpPressed;
            LandingWaveArmed = landingWaveArmed;
            PrimaryPressed = primaryPressed;
            PrimaryHeld = primaryHeld;
            PrimaryReleased = primaryReleased;
            PrimaryHeldSeconds = primaryHeldSeconds < 0f ? 0f : primaryHeldSeconds;
            PointerTravelViewport = pointerTravelViewport < 0f ? 0f : pointerTravelViewport;
            ForceHeld = forceHeld;
            FieldHeld = fieldHeld;
            PointerOverEarthTarget = pointerOverEarthTarget;
            HasControlledTarget = hasControlledTarget;
            HasPrimedQuickStone = hasPrimedQuickStone;
            HasRepairTarget = hasRepairTarget;
        }

        public bool CancelPressed { get; }
        public bool Invalidated { get; }
        public bool Grounded { get; }
        public bool Descending { get; }
        public float MoveMagnitude { get; }
        public bool ModifierHeld { get; }
        public bool JumpPressed { get; }
        public bool LandingWaveArmed { get; }
        public bool PrimaryPressed { get; }
        public bool PrimaryHeld { get; }
        public bool PrimaryReleased { get; }
        public float PrimaryHeldSeconds { get; }
        public float PointerTravelViewport { get; }
        public bool ForceHeld { get; }
        public bool FieldHeld { get; }
        public bool PointerOverEarthTarget { get; }
        public bool HasControlledTarget { get; }
        public bool HasPrimedQuickStone { get; }
        public bool HasRepairTarget { get; }
    }
}
