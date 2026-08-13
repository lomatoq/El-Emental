using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [DisallowMultipleComponent]
    public sealed class HumanoidCharacterPresentation : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int CastHash = Animator.StringToHash("Cast");
        private static readonly int CastKindHash = Animator.StringToHash("CastKind");
        private static readonly int ImpactHash = Animator.StringToHash("Impact");

        [SerializeField] private CharacterPresentationProfile profile;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private Rigidbody rootBody;
        [SerializeField] private ActiveRagdollPuppet ragdoll;
        [SerializeField] private MagicInputController magicInput;
        [SerializeField] private MagicExecutor executor;

        private float _castWeight;
        private CharacterPhysicalMode _physicalMode;

        public Animator Animator => animator;
        public CharacterPresentationProfile Profile => profile;

        public void Configure(
            CharacterPresentationProfile configuredProfile,
            Animator configuredAnimator,
            Transform leftTarget,
            Transform rightTarget,
            PlanetMotor configuredMotor,
            Rigidbody configuredRoot,
            ActiveRagdollPuppet configuredRagdoll,
            MagicInputController configuredInput,
            MagicExecutor configuredExecutor)
        {
            profile = configuredProfile;
            animator = configuredAnimator;
            leftHandTarget = leftTarget;
            rightHandTarget = rightTarget;
            motor = configuredMotor;
            rootBody = configuredRoot;
            ragdoll = configuredRagdoll;
            magicInput = configuredInput;
            executor = configuredExecutor;
            if (animator != null) animator.applyRootMotion = false;
        }

        private void OnEnable()
        {
            if (ragdoll != null) ragdoll.StateChanged += HandlePhysicalState;
        }

        private void OnDisable()
        {
            if (ragdoll != null) ragdoll.StateChanged -= HandlePhysicalState;
        }

        private void Update()
        {
            if (animator == null || rootBody == null || motor == null) return;
            Vector3 tangentVelocity = Vector3.ProjectOnPlane(rootBody.linearVelocity, motor.LocalUp);
            animator.SetFloat(SpeedHash, tangentVelocity.magnitude, ProfileBlendSeconds, Time.deltaTime);
            animator.SetBool(GroundedHash, motor.IsGrounded);
            animator.SetFloat(VerticalSpeedHash, Vector3.Dot(rootBody.linearVelocity, motor.LocalUp));
            bool casting = executor != null &&
                           (executor.HeldBody != null || executor.IsGravityWellActive || executor.IsVectorFieldActive);
            float targetWeight = casting && _physicalMode != CharacterPhysicalMode.FullRagdoll
                ? (profile != null ? profile.HandIkWeight : 0.92f)
                : 0f;
            _castWeight = Mathf.MoveTowards(_castWeight, targetWeight, Time.deltaTime / Mathf.Max(0.01f, CastingBlendSeconds));
            animator.SetBool(CastHash, casting);
            animator.SetInteger(CastKindHash, ResolveCastKind());
            UpdateHandTargets();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _castWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _castWeight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _castWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, _castWeight);
            if (leftHandTarget != null)
            {
                animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget.position);
                animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandTarget.rotation);
            }
            if (rightHandTarget != null)
            {
                animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget.position);
                animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandTarget.rotation);
            }
        }

        private void UpdateHandTargets()
        {
            if (_castWeight <= 0f || leftHandTarget == null || rightHandTarget == null) return;
            Vector3 focus;
            if (executor != null && executor.IsGravityWellActive) focus = executor.GravityWellFocus;
            else if (executor != null && executor.IsVectorFieldActive) focus = executor.VectorFieldPoint;
            else if (executor != null && executor.HeldBody != null) focus = executor.HeldBody.worldCenterOfMass;
            else focus = transform.position + transform.forward * 1.4f + motor.LocalUp * 0.8f;
            Vector3 across = Vector3.Cross(motor.LocalUp, focus - transform.position).normalized;
            if (across.sqrMagnitude < 0.1f) across = transform.right;
            leftHandTarget.position = focus - across * 0.16f;
            rightHandTarget.position = focus + across * 0.16f;
            Quaternion rotation = Quaternion.LookRotation(focus - transform.position, motor.LocalUp);
            leftHandTarget.rotation = rotation;
            rightHandTarget.rotation = rotation;
        }

        private int ResolveCastKind()
        {
            if (executor == null) return 0;
            if (executor.IsGravityWellActive) return 4;
            if (executor.IsVectorFieldActive) return 3;
            if (executor.HeldBody != null) return 2;
            return 1;
        }

        private void HandlePhysicalState(CharacterPhysicalState state)
        {
            _physicalMode = state.Mode;
            if (animator == null) return;
            bool animatorEnabled = state.Mode != CharacterPhysicalMode.FullRagdoll;
            if (animator.enabled != animatorEnabled) animator.enabled = animatorEnabled;
            if (state.Mode == CharacterPhysicalMode.Stagger) animator.SetTrigger(ImpactHash);
        }

        private float ProfileBlendSeconds => profile != null ? profile.LocomotionBlendSeconds : 0.12f;
        private float CastingBlendSeconds => profile != null ? profile.CastingBlendSeconds : 0.1f;
    }
}
