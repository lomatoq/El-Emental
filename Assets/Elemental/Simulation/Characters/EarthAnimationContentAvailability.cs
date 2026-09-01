namespace Elemental.Simulation.Characters
{
    public enum EarthAnimationContentFamily : byte
    {
        DirectionalStart = 0,
        DirectionalStop = 1,
        PivotLeft = 2,
        PivotRight = 3,
        MagicGather = 4,
        MagicPull = 5,
        MagicPush = 6,
        MagicLift = 7,
        MagicSlam = 8,
        MagicSustain = 9,
        MagicRelease = 10,
        RecoveryFront = 11,
        RecoveryBack = 12,
        AuthoredFlip = 13
    }

    public enum EarthAnimationContentQuality : byte
    {
        Missing = 0,
        GenericFallback = 1,
        CompatibleAuthored = 2,
        ExactAuthored = 3,
        MirroredAuthored = 4
    }

    public enum EarthAnimationContentBlocker : byte
    {
        None = 0,
        MissingSourceClip = 1,
        MissingCatalogProfile = 2,
        MissingControllerBinding = 3,
        GenericFallbackOnly = 4
    }

    public readonly struct EarthAnimationContentAvailability
    {
        public EarthAnimationContentAvailability(
            EarthAnimationContentFamily family,
            EarthAnimationContentQuality quality,
            bool hasSourceClip,
            bool hasCatalogProfile,
            bool hasControllerBinding)
        {
            Family = family;
            Quality = quality;
            HasSourceClip = hasSourceClip;
            HasCatalogProfile = hasCatalogProfile;
            HasControllerBinding = hasControllerBinding;
            Blocker = ResolveBlocker(
                quality,
                hasSourceClip,
                hasCatalogProfile,
                hasControllerBinding);
        }

        public EarthAnimationContentFamily Family { get; }
        public EarthAnimationContentQuality Quality { get; }
        public bool HasSourceClip { get; }
        public bool HasCatalogProfile { get; }
        public bool HasControllerBinding { get; }
        public EarthAnimationContentBlocker Blocker { get; }
        public bool IsRuntimePlayable =>
            HasSourceClip && HasCatalogProfile && HasControllerBinding;
        public bool IsAuthoredCoverage =>
            IsRuntimePlayable &&
            (Quality == EarthAnimationContentQuality.CompatibleAuthored ||
             Quality == EarthAnimationContentQuality.ExactAuthored ||
             Quality == EarthAnimationContentQuality.MirroredAuthored);

        private static EarthAnimationContentBlocker ResolveBlocker(
            EarthAnimationContentQuality quality,
            bool hasSourceClip,
            bool hasCatalogProfile,
            bool hasControllerBinding)
        {
            if (!hasSourceClip || quality == EarthAnimationContentQuality.Missing)
                return EarthAnimationContentBlocker.MissingSourceClip;
            if (!hasCatalogProfile)
                return EarthAnimationContentBlocker.MissingCatalogProfile;
            if (!hasControllerBinding)
                return EarthAnimationContentBlocker.MissingControllerBinding;
            return quality == EarthAnimationContentQuality.GenericFallback
                ? EarthAnimationContentBlocker.GenericFallbackOnly
                : EarthAnimationContentBlocker.None;
        }
    }
}
