namespace Elemental.Simulation.Characters
{
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
    /// Pure ownership adapter for the existing animation/active-ragdoll stack.
    /// CharacterPhysicalController remains the sole mode authority; every mutation
    /// is accepted only when its canonical CharacterPhysicalMode agrees.
    /// </summary>
    public sealed class EarthPhysicalAnimationCoordinator
    {
        private uint _activeHandoffSequence;
        private uint _activeRecoverySequence;
        private EarthRecoveryMarkerProfile _markers;
        private bool _ragdollOwnershipActive;
        private bool _recoveryOwnershipActive;
        private bool _poseMatchedRecoveryActive;
        private EarthPhysicalAnimationOwnership _ownership = AnimatedOwnership();

        public int RagdollHandoffCount { get; private set; }
        public int RecoveryHandoffCount { get; private set; }
        public EarthPhysicalAnimationOwnership Ownership => _ownership;
        public bool IsRagdollOwnershipActive => _ragdollOwnershipActive;
        public bool IsRecoveryOwnershipActive => _recoveryOwnershipActive;
        public bool IsPoseMatchedRecoveryActive => _poseMatchedRecoveryActive;

        public bool TryBeginFullRagdoll(
            CharacterPhysicalMode canonicalMode,
            uint handoffSequence)
        {
            if (canonicalMode != CharacterPhysicalMode.FullRagdoll ||
                handoffSequence == 0u ||
                handoffSequence == _activeHandoffSequence ||
                _ragdollOwnershipActive)
                return false;

            _activeHandoffSequence = handoffSequence;
            _activeRecoverySequence = 0u;
            _markers = default;
            _ragdollOwnershipActive = true;
            _recoveryOwnershipActive = false;
            _poseMatchedRecoveryActive = false;
            _ownership = RagdollOwnership();
            RagdollHandoffCount++;
            return true;
        }

        public bool TryBeginLegacyRecovery(CharacterPhysicalMode canonicalMode)
        {
            if (canonicalMode != CharacterPhysicalMode.Recovery ||
                !_ragdollOwnershipActive ||
                _recoveryOwnershipActive)
                return false;

            _ragdollOwnershipActive = false;
            _recoveryOwnershipActive = true;
            _poseMatchedRecoveryActive = false;
            _markers = default;
            _ownership = AnimatedOwnership();
            return true;
        }

        public bool TryBeginPoseMatchedRecovery(
            CharacterPhysicalMode canonicalMode,
            uint recoverySequence,
            in EarthRecoveryResult result)
        {
            if (canonicalMode != CharacterPhysicalMode.Recovery ||
                recoverySequence == 0u ||
                recoverySequence == _activeRecoverySequence ||
                !_ragdollOwnershipActive ||
                _recoveryOwnershipActive ||
                !result.IsValid ||
                !result.Markers.IsValid)
                return false;

            _activeRecoverySequence = recoverySequence;
            _markers = result.Markers;
            _ragdollOwnershipActive = false;
            _recoveryOwnershipActive = true;
            _poseMatchedRecoveryActive = true;
            _ownership = new EarthPhysicalAnimationOwnership(true, false, false, false, false);
            RecoveryHandoffCount++;
            return true;
        }

        public bool TryAdvancePoseMatchedRecovery(
            CharacterPhysicalMode canonicalMode,
            float normalizedPhase,
            bool rootAndSupportValid,
            out EarthPhysicalAnimationOwnership ownership)
        {
            ownership = _ownership;
            if (canonicalMode != CharacterPhysicalMode.Recovery ||
                !_recoveryOwnershipActive ||
                !_poseMatchedRecoveryActive ||
                !float.IsFinite(normalizedPhase) ||
                normalizedPhase < 0f)
                return false;

            float phase = normalizedPhase > 1f ? 1f : normalizedPhase;
            bool feet = rootAndSupportValid && phase >= _markers.FeetEnablePhase;
            bool controls = rootAndSupportValid && phase >= _markers.ControlsEnablePhase;
            bool exit = rootAndSupportValid && phase >= _markers.ExitPhase;
            _ownership = new EarthPhysicalAnimationOwnership(
                true,
                exit,
                feet,
                controls,
                exit);
            ownership = _ownership;
            return true;
        }

        public bool TryCompleteRecovery(CharacterPhysicalMode canonicalMode)
        {
            if (canonicalMode != CharacterPhysicalMode.AnimatedMotor ||
                !_recoveryOwnershipActive ||
                (_poseMatchedRecoveryActive && !_ownership.RecoveryExitReady))
                return false;

            ClearToAnimated();
            return true;
        }

        public bool TryResetAnimated(CharacterPhysicalMode canonicalMode)
        {
            if (canonicalMode != CharacterPhysicalMode.AnimatedMotor)
                return false;

            ClearToAnimated();
            return true;
        }

        public bool IsConsistentWith(CharacterPhysicalMode canonicalMode)
        {
            switch (canonicalMode)
            {
                case CharacterPhysicalMode.AnimatedMotor:
                case CharacterPhysicalMode.PhysicalAssist:
                case CharacterPhysicalMode.Stagger:
                    return !_ragdollOwnershipActive &&
                           !_recoveryOwnershipActive &&
                           _ownership.AnimationOwnerEnabled &&
                           _ownership.ProceduralOwnersEnabled &&
                           _ownership.FeetEnabled &&
                           _ownership.ControlsEnabled;
                case CharacterPhysicalMode.FullRagdoll:
                    return _ragdollOwnershipActive &&
                           !_recoveryOwnershipActive &&
                           !_ownership.AnimationOwnerEnabled &&
                           !_ownership.ProceduralOwnersEnabled &&
                           !_ownership.FeetEnabled &&
                           !_ownership.ControlsEnabled;
                case CharacterPhysicalMode.Recovery:
                    return !_ragdollOwnershipActive &&
                           _recoveryOwnershipActive &&
                           _ownership.AnimationOwnerEnabled;
                default:
                    return false;
            }
        }

        private void ClearToAnimated()
        {
            _activeHandoffSequence = 0u;
            _activeRecoverySequence = 0u;
            _markers = default;
            _ragdollOwnershipActive = false;
            _recoveryOwnershipActive = false;
            _poseMatchedRecoveryActive = false;
            _ownership = AnimatedOwnership();
        }

        private static EarthPhysicalAnimationOwnership AnimatedOwnership() =>
            new EarthPhysicalAnimationOwnership(true, true, true, true, true);

        private static EarthPhysicalAnimationOwnership RagdollOwnership() =>
            new EarthPhysicalAnimationOwnership(false, false, false, false, false);
    }
}
