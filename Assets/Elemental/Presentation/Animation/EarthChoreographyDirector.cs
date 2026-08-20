using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    /// <summary>
    /// Presentation-only bridge from physical bending intent to the active Humanoid.
    /// It never commits gameplay results and never changes global time scale.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthChoreographyDirector : MonoBehaviour
    {
        private static readonly int EffortHash = Animator.StringToHash("EarthEffort");
        private static readonly int BraceHash = Animator.StringToHash("EarthBrace");
        private static readonly int GroundingHash = Animator.StringToHash("EarthGrounding");
        private static readonly int PrecisionHash = Animator.StringToHash("EarthPrecision");
        private static readonly int PhaseHash = Animator.StringToHash("EarthPhase");
        private static readonly int DialectHash = Animator.StringToHash("EarthDialect");

        [SerializeField] private Animator animator;
        [SerializeField] private EarthCharacterPoseController poseSource;

        private EarthCastPhase _previousPhase;
        private float _holdUntil;
        private bool _hasEffort;
        private bool _hasBrace;
        private bool _hasGrounding;
        private bool _hasPrecision;
        private bool _hasPhase;
        private bool _hasDialect;

        public BendingPoseRequest CurrentRequest { get; private set; }
        public EarthChoreographySample CurrentSample { get; private set; }
        public bool PoseHoldActive => Time.unscaledTime < _holdUntil;

        public void Configure(Animator configuredAnimator, EarthCharacterPoseController configuredSource)
        {
            animator = configuredAnimator;
            poseSource = configuredSource;
            CacheParameters();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (poseSource == null) poseSource = GetComponent<EarthCharacterPoseController>();
            CacheParameters();
        }

        private void OnDisable()
        {
            if (animator != null) animator.speed = 1f;
            _holdUntil = 0f;
        }

        private void LateUpdate()
        {
            if (poseSource == null || animator == null) return;
            CurrentRequest = poseSource.CurrentRequest;
            BendingPoseRequest request = CurrentRequest;
            CurrentSample = EarthChoreographySolver.Solve(in request);
            if (CurrentRequest.Phase == EarthCastPhase.Strike && _previousPhase != EarthCastPhase.Strike &&
                CurrentSample.PoseHoldSeconds > 0f)
                _holdUntil = Time.unscaledTime + CurrentSample.PoseHoldSeconds;
            _previousPhase = CurrentRequest.Phase;

            float delta = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            if (_hasEffort) animator.SetFloat(EffortHash, CurrentRequest.Effort01, 0.06f, delta);
            if (_hasBrace) animator.SetFloat(BraceHash, CurrentSample.StanceWidth01, 0.07f, delta);
            if (_hasGrounding) animator.SetFloat(GroundingHash, CurrentRequest.Grounding01, 0.08f, delta);
            if (_hasPrecision) animator.SetFloat(PrecisionHash, CurrentRequest.Precision01, 0.06f, delta);
            if (_hasPhase) animator.SetInteger(PhaseHash, (int)CurrentRequest.Phase);
            if (_hasDialect) animator.SetInteger(DialectHash, (int)CurrentSample.Dialect);

            // Never freeze the whole Animator for a casting accent. The base
            // locomotion/surf layer must keep advancing while the upper-body cast
            // layer holds its authored silhouette; globally slowing the Animator was
            // the direct cause of stuck legs and platform T-poses after casting.
            animator.speed = 1f;
        }

        private void CacheParameters()
        {
            _hasEffort = _hasBrace = _hasGrounding = _hasPrecision = _hasPhase = _hasDialect = false;
            if (animator == null) return;
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                int hash = parameters[index].nameHash;
                if (hash == EffortHash) _hasEffort = true;
                else if (hash == BraceHash) _hasBrace = true;
                else if (hash == GroundingHash) _hasGrounding = true;
                else if (hash == PrecisionHash) _hasPrecision = true;
                else if (hash == PhaseHash) _hasPhase = true;
                else if (hash == DialectHash) _hasDialect = true;
            }
        }
    }
}
