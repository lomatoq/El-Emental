using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [DisallowMultipleComponent]
    public sealed class HumanoidCharacterPresentation : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int TurnHash = Animator.StringToHash("Turn");
        private static readonly int SurfingHash = Animator.StringToHash("Surfing");
        private static readonly int HardLandingHash = Animator.StringToHash("HardLanding");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int CastHash = Animator.StringToHash("Cast");
        private static readonly int CastKindHash = Animator.StringToHash("CastKind");
        private static readonly int EarthPoseHash = Animator.StringToHash("EarthPose");
        private static readonly int ImpactHash = Animator.StringToHash("Impact");
        private static readonly int MotionTimeHash = Animator.StringToHash("EarthMotionTime");
        private static readonly int[] EarthPoseWeightHashes =
        {
            Animator.StringToHash("EarthPose01"),
            Animator.StringToHash("EarthPose02"),
            Animator.StringToHash("EarthPose03"),
            Animator.StringToHash("EarthPose04"),
            Animator.StringToHash("EarthPose05"),
            Animator.StringToHash("EarthPose06"),
            Animator.StringToHash("EarthPose07"),
            Animator.StringToHash("EarthPose08"),
            Animator.StringToHash("EarthPose09"),
            Animator.StringToHash("EarthPose10"),
            Animator.StringToHash("EarthPose11")
        };
        private const string MagicLayerName = "Earth Magic Upper Body";
        private const string ImpactLayerName = "Impact Additive";

        [SerializeField] private CharacterPresentationProfile profile;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private Rigidbody rootBody;
        [SerializeField] private ActiveRagdollPuppet ragdoll;
        [SerializeField] private MagicInputController magicInput;
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private EarthTechniquePresentationProfile techniqueProfile;
        [SerializeField] private EarthPillarMobility pillarMobility;
        [SerializeField] private EarthCharacterPoseController poseController;
        [SerializeField] private EarthChoreographyDirector choreographyDirector;
        [SerializeField] private EarthSurfController surfController;
        [SerializeField] private EarthAnimationRigBridge animationRigBridge;

        private float _castWeight;
        private CharacterPhysicalMode _physicalMode;
        private int _magicLayerIndex = -1;
        private int _impactLayerIndex = -1;
        private float _impactWeight;
        private bool _wasCasting;
        private float _impactUntil;
        private bool _ragdollSubscribed;
        private float _stableGroundedSeconds;
        private bool _animationGrounded = true;
        private float _unsupportedSeconds;
        private float _minimumAirVerticalSpeed;
        private float _hardLandingUntil;

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
            MagicExecutor configuredExecutor,
            EarthTechniquePresentationProfile configuredTechniqueProfile = null,
            EarthPillarMobility configuredPillarMobility = null)
        {
            UnsubscribeRagdoll();
            profile = configuredProfile;
            animator = configuredAnimator;
            leftHandTarget = leftTarget;
            rightHandTarget = rightTarget;
            motor = configuredMotor;
            rootBody = configuredRoot;
            ragdoll = configuredRagdoll;
            magicInput = configuredInput;
            executor = configuredExecutor;
            techniqueProfile = configuredTechniqueProfile;
            pillarMobility = configuredPillarMobility;
            PrepareAnimator();
            ConfigurePoseController();
            if (isActiveAndEnabled) SubscribeRagdoll();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (motor == null) motor = GetComponentInParent<PlanetMotor>();
            if (rootBody == null) rootBody = GetComponentInParent<Rigidbody>();
            if (pillarMobility == null) pillarMobility = GetComponentInParent<EarthPillarMobility>();
            if (surfController == null) surfController = GetComponentInParent<EarthSurfController>();
            PrepareAnimator();
            ConfigurePoseController();
        }

        private void PrepareAnimator()
        {
            if (animator == null) return;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _magicLayerIndex = animator.GetLayerIndex(MagicLayerName);
            _impactLayerIndex = animator.GetLayerIndex(ImpactLayerName);
            animator.SetLayerWeight(0, 1f);
            if (_magicLayerIndex >= 0) animator.SetLayerWeight(_magicLayerIndex, 0f);
            if (_impactLayerIndex >= 0) animator.SetLayerWeight(_impactLayerIndex, 0f);
            _animationGrounded = true;
            _unsupportedSeconds = 0f;
            animator.SetBool(GroundedHash, true);
        }

        private void ConfigurePoseController()
        {
            if (animator == null || motor == null || rootBody == null) return;
            if (poseController == null) poseController = GetComponent<EarthCharacterPoseController>();
            if (poseController == null) poseController = gameObject.AddComponent<EarthCharacterPoseController>();
            poseController.Configure(
                animator, magicInput, executor, motor, rootBody, pillarMobility, techniqueProfile);
            if (choreographyDirector == null)
                choreographyDirector = GetComponent<EarthChoreographyDirector>();
            if (choreographyDirector == null)
                choreographyDirector = gameObject.AddComponent<EarthChoreographyDirector>();
            choreographyDirector.Configure(animator, poseController);
            if (animationRigBridge == null)
                animationRigBridge = GetComponent<EarthAnimationRigBridge>();
            if (animationRigBridge == null)
                animationRigBridge = gameObject.AddComponent<EarthAnimationRigBridge>();
            animationRigBridge.Configure(animator, leftHandTarget, rightHandTarget);
        }

        private void OnEnable()
        {
            SubscribeRagdoll();
        }

        private void OnDisable()
        {
            UnsubscribeRagdoll();
        }

        private void SubscribeRagdoll()
        {
            if (_ragdollSubscribed || ragdoll == null) return;
            ragdoll.StateChanged += HandlePhysicalState;
            _ragdollSubscribed = true;
        }

        private void UnsubscribeRagdoll()
        {
            if (!_ragdollSubscribed) return;
            if (ragdoll != null) ragdoll.StateChanged -= HandlePhysicalState;
            _ragdollSubscribed = false;
        }

        private void Update()
        {
            if (animator == null || rootBody == null || motor == null) return;
            Vector3 supportVelocity = Vector3.zero;
            if (motor.CurrentSupportFrame.IsValid)
            {
                var support = motor.CurrentSupportFrame.ContactPointVelocity;
                supportVelocity = new Vector3(support.x, support.y, support.z);
            }
            Vector3 tangentVelocity = Vector3.ProjectOnPlane(
                rootBody.linearVelocity - supportVelocity,
                motor.LocalUp);
            float verticalSpeed = Vector3.Dot(rootBody.linearVelocity, motor.LocalUp);
            float presentationSpeed = 0f;
            bool surfing = surfController != null && surfController.IsActive;
            if (!surfing)
            {
                Vector3 facing = Vector3.ProjectOnPlane(motor.FacingForward, motor.LocalUp);
                if (facing.sqrMagnitude < 0.001f) facing = transform.forward;
                presentationSpeed = Vector3.Dot(tangentVelocity, facing.normalized);
            }
            animator.SetFloat(SpeedHash, presentationSpeed, ProfileBlendSeconds, Time.deltaTime);
            animator.SetFloat(TurnHash, motor.LastCommand.Move.x, 0.08f, Time.deltaTime);
            animator.SetBool(SurfingHash, surfing);
            UpdateAnimationGrounded(verticalSpeed);
            animator.SetBool(GroundedHash, _animationGrounded);
            animator.SetFloat(VerticalSpeedHash, verticalSpeed);
            RecoverGroundedLocomotionState();
            int castKind = ResolveCastKind();
            bool casting = castKind > 0 && _physicalMode != CharacterPhysicalMode.FullRagdoll &&
                           ((poseController != null && poseController.CurrentRequest.IsActive) ||
                            (executor != null &&
                             (executor.HeldBody != null || executor.IsGravityWellActive ||
                              executor.IsVectorFieldActive)));
            float targetWeight = casting && _physicalMode != CharacterPhysicalMode.FullRagdoll
                ? (profile != null ? profile.HandIkWeight : 0.92f)
                : 0f;
            _castWeight = Mathf.MoveTowards(_castWeight, targetWeight, Time.deltaTime / Mathf.Max(0.01f, CastingBlendSeconds));
            animator.SetBool(CastHash, casting);
            animator.SetInteger(CastKindHash, castKind);
            animator.SetFloat(EarthPoseHash, castKind, 0.055f, Time.deltaTime);
            float motionTime = casting
                ? EarthHumanoidMotionResolver.ResolveMotionTime(
                    poseController != null ? poseController.CurrentRequest.Phase : EarthCastPhase.Sustain)
                : 0f;
            if (casting && !_wasCasting) animator.SetFloat(MotionTimeHash, motionTime);
            else animator.SetFloat(MotionTimeHash, motionTime, 0.075f, Time.deltaTime);
            for (int index = 0; index < EarthPoseWeightHashes.Length; index++)
            {
                float targetPoseWeight = casting && castKind == index + 1 ? 1f : 0f;
                animator.SetFloat(
                    EarthPoseWeightHashes[index],
                    targetPoseWeight,
                    0.10f,
                    Time.deltaTime);
            }
            if (_magicLayerIndex >= 0) animator.SetLayerWeight(_magicLayerIndex, _castWeight);
            float impactTarget = Time.time < _impactUntil &&
                                 _physicalMode != CharacterPhysicalMode.FullRagdoll
                ? 0.56f
                : 0f;
            _impactWeight = Mathf.MoveTowards(
                _impactWeight,
                impactTarget,
                Time.deltaTime / (impactTarget > 0f ? 0.045f : 0.18f));
            if (_impactLayerIndex >= 0) animator.SetLayerWeight(_impactLayerIndex, _impactWeight);
            animationRigBridge?.SetMagicWeight(_castWeight);
            UpdateHandTargets();
            _wasCasting = casting;
        }

        private void UpdateAnimationGrounded(float verticalSpeed)
        {
            if (motor.HasStableSupport)
            {
                bool justLanded = !_animationGrounded;
                _unsupportedSeconds = 0f;
                _animationGrounded = true;
                if (justLanded)
                    _hardLandingUntil = _minimumAirVerticalSpeed <= -7.5f
                        ? Time.time + 0.65f
                        : 0f;
                _minimumAirVerticalSpeed = 0f;
                animator.SetBool(HardLandingHash, Time.time < _hardLandingUntil);
                return;
            }

            _unsupportedSeconds += Time.deltaTime;
            // Curved-ground probes can miss for one rendered frame at chunk or
            // structure seams. Do not let that false negative enter a one-shot
            // jump state. A real launch is accepted immediately from velocity;
            // an ordinary fall is accepted after a short unsupported window.
            if (verticalSpeed > 0.75f || _unsupportedSeconds >= 0.11f)
                _animationGrounded = false;
            if (!_animationGrounded)
                _minimumAirVerticalSpeed = Mathf.Min(_minimumAirVerticalSpeed, verticalSpeed);
            animator.SetBool(HardLandingHash, false);
        }

        private void RecoverGroundedLocomotionState()
        {
            if (!motor.HasStableSupport)
            {
                _stableGroundedSeconds = 0f;
                return;
            }
            _stableGroundedSeconds += Time.deltaTime;
            if (_stableGroundedSeconds < 0.22f || animator.IsInTransition(0)) return;
            if (surfController != null && surfController.IsActive) return;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Locomotion")) return;
            if (state.IsName("Land") && state.normalizedTime < 0.68f) return;
            if (state.IsName("Hard Land") && state.normalizedTime < 0.82f) return;

            // A one-frame support miss on a curved surface can enter Jump before
            // locomotion has settled. If its vertical threshold never crosses,
            // that one-shot state freezes until the player performs a real jump.
            // Stable support is the authoritative escape back to locomotion.
            animator.CrossFade("Locomotion", 0.08f, 0, 0f);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;
            // Animation Rigging owns the arm chain when available. Running Mecanim
            // IK on top of the TwoBoneIK job fights the same joints and produces
            // elbow flips/reach snapping. The built-in path remains the documented
            // rollback for a missing rig package or invalid Humanoid.
            if (animationRigBridge != null && animationRigBridge.IsBuilt) return;
            if (_magicLayerIndex >= 0 && layerIndex != _magicLayerIndex) return;
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

            // Telekinesis points can be many metres away. They define gaze/aim,
            // not a literal wrist destination. Feeding the distant point directly
            // into TwoBoneIK fully stretched both arms and flipped the elbows.
            Transform chest = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Chest)
                : null;
            Vector3 shoulderCenter = chest != null
                ? chest.position
                : transform.position + motor.LocalUp * 0.66f;
            Vector3 aimDirection = focus - shoulderCenter;
            if (aimDirection.sqrMagnitude < 0.001f) aimDirection = transform.forward;
            aimDirection.Normalize();
            float targetReach = Mathf.Lerp(0.43f, 0.72f, Mathf.Clamp01(_castWeight));
            Vector3 reachableFocus = shoulderCenter + aimDirection * targetReach;
            Vector3 across = Vector3.Cross(motor.LocalUp, aimDirection).normalized;
            if (across.sqrMagnitude < 0.1f) across = transform.right;
            leftHandTarget.position = reachableFocus - across * 0.17f;
            rightHandTarget.position = reachableFocus + across * 0.17f;
            Quaternion rotation = Quaternion.LookRotation(aimDirection, motor.LocalUp);
            leftHandTarget.rotation = rotation;
            rightHandTarget.rotation = rotation;
        }

        private int ResolveCastKind()
        {
            EarthTechniqueId technique = poseController != null
                ? poseController.CurrentRequest.Technique
                : EarthTechniqueId.None;
            if (technique != EarthTechniqueId.None)
            {
                return (int)EarthHumanoidMotionResolver.Resolve(technique);
            }
            if (executor == null) return 0;
            if (executor.IsGravityWellActive) return (int)EarthHumanoidPoseSlot.GravityRepair;
            if (executor.IsVectorFieldActive) return (int)EarthHumanoidPoseSlot.VectorPush;
            if (executor.HeldBody != null) return (int)EarthHumanoidPoseSlot.PullStone;
            return 0;
        }

        private void HandlePhysicalState(CharacterPhysicalState state)
        {
            _physicalMode = state.Mode;
            if (animator == null) return;
            bool animatorEnabled = state.Mode != CharacterPhysicalMode.FullRagdoll;
            if (animator.enabled != animatorEnabled)
            {
                animator.enabled = animatorEnabled;
                if (animatorEnabled)
                {
                    // The visible rigid-part hierarchy is intentionally parented
                    // below Humanoid bones. Animator.Rebind after that runtime
                    // parenting can leave the state clock running while the bone
                    // transforms stay frozen, so resume without rebuilding bindings.
                    animator.Play("Locomotion", 0, 0f);
                    animator.Update(0f);
                    _animationGrounded = true;
                    _unsupportedSeconds = 0f;
                }
            }
            if (state.Mode == CharacterPhysicalMode.Stagger)
            {
                _impactUntil = Time.time + 0.46f;
                animator.SetTrigger(ImpactHash);
            }
        }

        private float ProfileBlendSeconds => profile != null ? profile.LocomotionBlendSeconds : 0.12f;
        private float CastingBlendSeconds => profile != null ? profile.CastingBlendSeconds : 0.1f;
    }
}
