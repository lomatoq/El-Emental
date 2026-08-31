namespace Elemental.Simulation.Characters
{
    public enum EarthPhysicalAnimationMode : byte
    {
        Animated = 0,
        PhysicalAssist = 1,
        Stagger = 2,
        BalanceRecovery = 3,
        Brace = 4,
        FallProtect = 5,
        FullRagdoll = 6,
        GetUp = 7
    }

    public readonly struct EarthPhysicalAnimationOwnership
    {
        public EarthPhysicalAnimationOwnership(
            bool animationOwnerEnabled,
            bool proceduralOwnersEnabled,
            bool feetEnabled,
            bool controlsEnabled,
            bool recoveryExitReady)
        {
            AnimationOwnerEnabled = animationOwnerEnabled;
            ProceduralOwnersEnabled = proceduralOwnersEnabled;
            FeetEnabled = feetEnabled;
            ControlsEnabled = controlsEnabled;
            RecoveryExitReady = recoveryExitReady;
        }

        public bool AnimationOwnerEnabled { get; }
        public bool ProceduralOwnersEnabled { get; }
        public bool FeetEnabled { get; }
        public bool ControlsEnabled { get; }
        public bool RecoveryExitReady { get; }
    }

    /// <summary>
    /// Pure ownership protocol for the existing animation/active-ragdoll stack.
    /// It does not implement P2 muscle behaviours; it only makes handoff and
    /// recovery marker decisions single-owner and idempotent.
    /// </summary>
    public sealed class EarthPhysicalAnimationCoordinator
    {
        private uint _activeHandoffSequence;
        private uint _activeRecoverySequence;
        private EarthRecoveryMarkerProfile _markers;
        private EarthPhysicalAnimationOwnership _ownership =
            new EarthPhysicalAnimationOwnership(true, true, true, true, true);

        public EarthPhysicalAnimationMode Mode { get; private set; } =
            EarthPhysicalAnimationMode.Animated;
        public int RagdollHandoffCount { get; private set; }
        public int RecoveryHandoffCount { get; private set; }
        public EarthPhysicalAnimationOwnership Ownership => _ownership;

        public bool TryBeginFullRagdoll(uint handoffSequence)
        {
            if (handoffSequence == 0u ||
                Mode == EarthPhysicalAnimationMode.FullRagdoll ||
                Mode == EarthPhysicalAnimationMode.GetUp ||
                handoffSequence == _activeHandoffSequence)
                return false;

            _activeHandoffSequence = handoffSequence;
            _activeRecoverySequence = 0u;
            Mode = EarthPhysicalAnimationMode.FullRagdoll;
            _ownership = new EarthPhysicalAnimationOwnership(false, false, false, false, false);
            RagdollHandoffCount++;
            return true;
        }

        public bool TryBeginGetUp(
            uint recoverySequence,
            in EarthRecoveryResult result)
        {
            if (recoverySequence == 0u ||
                Mode != EarthPhysicalAnimationMode.FullRagdoll ||
                recoverySequence == _activeRecoverySequence ||
                !result.IsValid)
                return false;

            _activeRecoverySequence = recoverySequence;
            _markers = result.Markers;
            Mode = EarthPhysicalAnimationMode.GetUp;
            _ownership = new EarthPhysicalAnimationOwnership(true, false, false, false, false);
            RecoveryHandoffCount++;
            return true;
        }

        public EarthPhysicalAnimationOwnership AdvanceGetUp(
            float normalizedPhase,
            bool rootAndSupportValid)
        {
            if (Mode != EarthPhysicalAnimationMode.GetUp)
                return _ownership;

            float phase = normalizedPhase;
            if (!float.IsFinite(phase)) phase = 0f;
            if (phase < 0f) phase = 0f;
            if (phase > 1f) phase = 1f;
            bool feet = rootAndSupportValid && phase >= _markers.FeetEnablePhase;
            bool controls = rootAndSupportValid && phase >= _markers.ControlsEnablePhase;
            bool exit = rootAndSupportValid && phase >= _markers.ExitPhase;
            _ownership = new EarthPhysicalAnimationOwnership(
                true,
                exit,
                feet,
                controls,
                exit);
            return _ownership;
        }

        public void CompleteGetUp()
        {
            if (Mode != EarthPhysicalAnimationMode.GetUp || !_ownership.RecoveryExitReady)
                return;
            Mode = EarthPhysicalAnimationMode.Animated;
            _ownership = new EarthPhysicalAnimationOwnership(true, true, true, true, true);
        }

        public void ResetAnimated()
        {
            Mode = EarthPhysicalAnimationMode.Animated;
            _activeHandoffSequence = 0u;
            _activeRecoverySequence = 0u;
            _ownership = new EarthPhysicalAnimationOwnership(true, true, true, true, true);
        }
    }
}
