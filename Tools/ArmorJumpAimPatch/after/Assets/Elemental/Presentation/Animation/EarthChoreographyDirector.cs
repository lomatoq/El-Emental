using Elemental.Simulation.Characters;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    /// <summary>
    /// Presentation-only bridge from physical bending intent to the active Humanoid.
    /// It never commits gameplay results and never changes global time scale.
    /// </summary>
    [DefaultExecutionOrder(800)]
    [DisallowMultipleComponent]
    public sealed class EarthChoreographyDirector : MonoBehaviour
    {
        private static readonly ProfilerMarker VisualPoseMarker =
            new ProfilerMarker("Elemental.Animation.ChoreographyPose");
        private static readonly int EffortHash = Animator.StringToHash("EarthEffort");
        private static readonly int BraceHash = Animator.StringToHash("EarthBrace");
        private static readonly int GroundingHash = Animator.StringToHash("EarthGrounding");
        private static readonly int PrecisionHash = Animator.StringToHash("EarthPrecision");
        private static readonly int PhaseHash = Animator.StringToHash("EarthPhase");
        private static readonly int DialectHash = Animator.StringToHash("EarthDialect");

        [SerializeField] private Animator animator;
        [SerializeField] private EarthCharacterPoseController poseSource;
        [SerializeField] private EarthAnimationDriver animationDriver;
        [SerializeField] private HumanoidCharacterPresentation presentation;

        private EarthCastPhase _previousPhase;
        private float _holdUntil;
        private bool _hasEffort;
        private bool _hasBrace;
        private bool _hasGrounding;
        private bool _hasPrecision;
        private bool _hasPhase;
        private bool _hasDialect;
        private Transform _chest;
        private Transform _head;
        private Transform _leftShoulder;
        private Transform _rightShoulder;
        private EarthChoreographyPoseOffset _appliedVisualPose;

        public BendingPoseRequest CurrentRequest { get; private set; }
        public EarthChoreographySample CurrentSample { get; private set; }
        public EarthChoreographyPoseOffset AppliedVisualPose => _appliedVisualPose;
        public bool PoseHoldActive => Time.unscaledTime < _holdUntil;

        public void Configure(Animator configuredAnimator, EarthCharacterPoseController configuredSource)
        {
            animator = configuredAnimator;
            poseSource = configuredSource;
            if (presentation == null) presentation = GetComponent<HumanoidCharacterPresentation>();
            ResolveAnimationDriver();
            CacheParameters();
            ResolveBones();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (poseSource == null) poseSource = GetComponent<EarthCharacterPoseController>();
            if (presentation == null) presentation = GetComponent<HumanoidCharacterPresentation>();
            ResolveAnimationDriver();
            CacheParameters();
            ResolveBones();
        }

        private void OnDisable()
        {
            if (animator != null) animator.speed = 1f;
            _holdUntil = 0f;
            _appliedVisualPose = default;
        }

        private void LateUpdate()
        {
            if (poseSource == null || animator == null) return;
            // Both production animation backends evaluate on scaled GameTime
            // (AnimatorUpdateMode.Normal / DirectorUpdateMode.GameTime). When a
            // capture or pause sets timeScale to zero their base pose is not
            // rewritten, so multiplying the same additive offset again would
            // accumulate it once per rendered frame. Hold the already evaluated
            // pose until the owning animation backend advances again.
            if (Time.deltaTime <= 0f)
            {
                animator.speed = 1f;
                return;
            }
            CurrentRequest = poseSource.CurrentRequest;
            BendingPoseRequest request = CurrentRequest;
            CurrentSample = EarthChoreographySolver.Solve(in request);
            if (CurrentRequest.Phase == EarthCastPhase.Strike && _previousPhase != EarthCastPhase.Strike &&
                CurrentSample.PoseHoldSeconds > 0f)
                _holdUntil = Time.unscaledTime + CurrentSample.PoseHoldSeconds;
            _previousPhase = CurrentRequest.Phase;

            float delta = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            if (_hasEffort) animationDriver.SetFloat(EffortHash, CurrentRequest.Effort01, 0.06f, delta);
            if (_hasBrace) animationDriver.SetFloat(BraceHash, CurrentSample.StanceWidth01, 0.07f, delta);
            if (_hasGrounding) animationDriver.SetFloat(GroundingHash, CurrentRequest.Grounding01, 0.08f, delta);
            if (_hasPrecision) animationDriver.SetFloat(PrecisionHash, CurrentRequest.Precision01, 0.06f, delta);
            if (_hasPhase) animationDriver.SetInteger(PhaseHash, (int)CurrentRequest.Phase);
            if (_hasDialect) animationDriver.SetInteger(DialectHash, (int)CurrentSample.Dialect);

            using (VisualPoseMarker.Auto()) ApplyVisualPose(delta);

            // Never freeze the whole Animator for a casting accent. The base
            // locomotion/surf layer must keep advancing while the upper-body cast
            // layer holds its authored silhouette; globally slowing the Animator was
            // the direct cause of stuck legs and platform T-poses after casting.
            animator.speed = 1f;
        }

        private void ApplyVisualPose(float deltaTime)
        {
            bool protectedMantle = presentation != null &&
                presentation.CurrentAuthoredAction == EarthAuthoredActionId.Mantle;
            if (protectedMantle)
            {
                // Mantle hand contacts are solved by Humanoid IK before this pass.
                // Rotating their chest/shoulder ancestors here would move both
                // wrists off the physical ledge. Drop residual cast state as well,
                // so it cannot reappear on the first frame after mantle release.
                _appliedVisualPose = default;
                _holdUntil = 0f;
                return;
            }
            if (!animator.enabled || !animator.isHuman)
            {
                _appliedVisualPose = default;
                return;
            }
            if (_chest == null || _head == null || _leftShoulder == null || _rightShoulder == null)
                ResolveBones();

            EarthChoreographyPoseOffset target = EarthChoreographyVisualSolver.Solve(
                CurrentRequest.Technique,
                CurrentRequest.Phase,
                CurrentSample.Dialect,
                CurrentRequest.Effort01,
                CurrentSample.StanceWidth01,
                CurrentRequest.Grounding01,
                CurrentRequest.Precision01,
                CurrentRequest.LeftDominant);
            // The flavor solver intentionally has a dominant side for silhouette,
            // but a centered action axis used to choose the right side and add
            // chest/head yaw even when the pointer was straight ahead. Keep arm
            // authorship while making lateral torso motion follow actual aim.
            if (poseSource != null)
                target = EarthChoreographyVisualSolver.AlignLateralBodyToAim(
                    in target,
                    poseSource.CurrentIntent.LocalDirection);
            if (presentation != null && presentation.HasResponsiveSustainedAim)
            {
                // Chest already belongs to this late choreography pass. Consume
                // the hand solver's body-local aim here rather than adding another
                // transform writer or rotating the gameplay root/head.
                float3 chest = target.ChestEuler;
                chest.y = math.clamp(
                    chest.y + EarthResponsiveHandTargetSolver.ResolveTorsoYawDegrees(
                        presentation.ResponsiveSustainedLocalAim,
                        presentation.ResponsiveSustainedAimWeight),
                    -EarthChoreographyVisualSolver.MaximumChestDegrees,
                    EarthChoreographyVisualSolver.MaximumChestDegrees);
                target = new EarthChoreographyPoseOffset(
                    chest,
                    target.HeadEuler,
                    target.LeftShoulderEuler,
                    target.RightShoulderEuler);
            }
            float responseSeconds = CurrentRequest.IsActive ? 0.065f : 0.10f;
            float blend = 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / responseSeconds);
            _appliedVisualPose = EarthChoreographyPoseOffset.Lerp(
                in _appliedVisualPose, in target, blend);
            if (_appliedVisualPose.MaximumAbsDegrees < 0.002f) return;

            ApplyLocalOffset(_chest, _appliedVisualPose.ChestEuler);
            ApplyLocalOffset(_head, _appliedVisualPose.HeadEuler);
            ApplyLocalOffset(_leftShoulder, _appliedVisualPose.LeftShoulderEuler);
            ApplyLocalOffset(_rightShoulder, _appliedVisualPose.RightShoulderEuler);
        }

        private void ResolveBones()
        {
            if (animator == null || !animator.isHuman) return;
            _chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                     animator.GetBoneTransform(HumanBodyBones.Chest);
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
            _leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            _rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
        }

        private static void ApplyLocalOffset(Transform bone, Unity.Mathematics.float3 euler)
        {
            if (bone == null) return;
            bone.localRotation *= Quaternion.Euler(euler.x, euler.y, euler.z);
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

        private void ResolveAnimationDriver()
        {
            if (animator == null) return;
            if (animationDriver == null) animationDriver = GetComponent<EarthAnimationDriver>();
            if (animationDriver == null) animationDriver = gameObject.AddComponent<EarthAnimationDriver>();
            if (animationDriver.Animator != animator) animationDriver.Configure(animator);
        }
    }
}
