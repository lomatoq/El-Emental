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
        private static readonly int LocomotionStateHash = Animator.StringToHash("Base Layer.Locomotion");
        private static readonly int JumpStateHash = Animator.StringToHash("Base Layer.Jump");
        private static readonly int FallStateHash = Animator.StringToHash("Base Layer.Fall");
        private static readonly int LandStateHash = Animator.StringToHash("Base Layer.Land");
        private static readonly int MovingLandStateHash = Animator.StringToHash("Base Layer.Moving Land");
        private static readonly int HardLandStateHash = Animator.StringToHash("Base Layer.Hard Land");
        private static readonly int SurfStateHash = Animator.StringToHash("Base Layer.Surf Crouch");
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
        [SerializeField] private EarthAnimationContactPredictor contactPredictor;

        private float _castWeight;
        private CharacterPhysicalMode _physicalMode;
        private int _magicLayerIndex = -1;
        private int _impactLayerIndex = -1;
        private float _impactWeight;
        private bool _wasCasting;
        private float _impactUntil;
        private bool _ragdollSubscribed;
        private bool _animationGrounded = true;
        private float _unsupportedSeconds;
        private EarthAnimationRescueState _rescueState;
        private EarthScalarPresentationState _speedFilter;
        private EarthScalarPresentationState _turnFilter;
        private Vector3 _previousFacing;
        private bool _hasPreviousFacing;
        private int _activeBaseStateHash;

        public Animator Animator => animator;
        public CharacterPresentationProfile Profile => profile;
        public EarthAnimationPhase MotionPhase => _rescueState.Phase;
        public float MotionPhaseSeconds => _rescueState.PhaseSeconds;
        public EarthLandingStyle LandingStyle => _rescueState.LandingStyle;
        public EarthLandingCandidateSnapshot LandingCandidate =>
            contactPredictor != null ? contactPredictor.Latest : default;
        public float FilteredSpeed => _speedFilter.Value;
        public float FilteredTurn => _turnFilter.Value;
        public float MeasuredYawRateDegrees { get; private set; }
        public EarthCharacterPoseController PoseController => poseController;

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
            // EarthCharacterPoseController is the single foot-contact owner.
            // Mecanim's automatic stabilizer evaluates a second solution from a
            // different clock and fights persistent surf/casting anchors.
            animator.stabilizeFeet = false;
            animator.feetPivotActive = 1f;
            _magicLayerIndex = animator.GetLayerIndex(MagicLayerName);
            _impactLayerIndex = animator.GetLayerIndex(ImpactLayerName);
            animator.SetLayerWeight(0, 1f);
            if (_magicLayerIndex >= 0) animator.SetLayerWeight(_magicLayerIndex, 0f);
            if (_impactLayerIndex >= 0) animator.SetLayerWeight(_impactLayerIndex, 0f);
            _animationGrounded = true;
            _unsupportedSeconds = 0f;
            _activeBaseStateHash = 0;
            animator.SetBool(GroundedHash, true);
            if (contactPredictor == null) contactPredictor = GetComponent<EarthAnimationContactPredictor>();
            if (contactPredictor == null) contactPredictor = gameObject.AddComponent<EarthAnimationContactPredictor>();
            contactPredictor.Configure(motor);
        }

        private void ConfigurePoseController()
        {
            if (animator == null || motor == null || rootBody == null) return;
            if (poseController == null) poseController = GetComponent<EarthCharacterPoseController>();
            if (poseController == null) poseController = gameObject.AddComponent<EarthCharacterPoseController>();
            poseController.Configure(
                animator, magicInput, executor, motor, rootBody, pillarMobility, techniqueProfile);
            poseController.ConfigureAnimationRescue(
                profile != null ? profile.SurfPelvisResponseSeconds : 0.085f,
                profile != null ? profile.SurfPelvisMaximumSpeed : 0.8f);
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
            bool surfing = surfController != null && surfController.IsActive;
            Vector3 facing = Vector3.ProjectOnPlane(motor.FacingForward, motor.LocalUp);
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.ProjectOnPlane(transform.forward, motor.LocalUp);
            facing.Normalize();
            float measuredYaw = 0f;
            if (_hasPreviousFacing && Time.deltaTime > 0.0001f)
                measuredYaw = Vector3.SignedAngle(_previousFacing, facing, motor.LocalUp) / Time.deltaTime;
            _previousFacing = facing;
            _hasPreviousFacing = true;
            MeasuredYawRateDegrees = measuredYaw;

            float measuredSpeed = surfing ? 0f : Vector3.Dot(tangentVelocity, facing);
            float targetSpeed = EarthAnimationParameterFilter.ResolveLocomotionTargetSpeed(
                measuredSpeed,
                motor.LastCommand.Move.y,
                profile != null ? profile.PassiveLocomotionDriftDeadZone : 0.14f);
            float presentationSpeed = EarthAnimationParameterFilter.StepSpeed(
                ref _speedFilter,
                targetSpeed,
                profile != null ? profile.SpeedAccelerationSeconds : 0.075f,
                profile != null ? profile.SpeedDecelerationSeconds : 0.11f,
                Time.deltaTime);
            EarthTurnPresentationSample turn = EarthAnimationParameterFilter.StepTurn(
                ref _turnFilter,
                measuredYaw,
                motor.LastCommand.Move.x,
                profile != null ? profile.ReferenceYawRateDegrees : 145f,
                profile != null ? profile.MeasuredYawFallbackThreshold : 7f,
                profile != null ? profile.TurnDeadZone : 0.055f,
                profile != null ? profile.TurnEnterSeconds : 0.065f,
                profile != null ? profile.TurnReleaseSeconds : 0.16f,
                Time.deltaTime);
            animator.feetPivotActive = Mathf.MoveTowards(
                animator.feetPivotActive,
                turn.PivotActive ? 0.18f : 1f,
                Time.deltaTime * 5.5f);

            UpdateAnimationGrounded(verticalSpeed);
            EarthLandingCandidateSnapshot candidate = !_animationGrounded && contactPredictor != null
                ? contactPredictor.Predict(
                    profile != null ? profile.LandingPredictionHorizon : 0.65f,
                    profile != null ? profile.LandingPredictionSteps : 6,
                    profile != null ? profile.LandingCandidateGrace : 0.12f,
                    Time.deltaTime)
                : default;
            EarthAnimationRescueTuning rescueTuning = ResolveRescueTuning();
            EarthLandingStyle previousLandingStyle = _rescueState.LandingStyle;
            EarthAnimationRescueSample rescue = EarthAnimationStateResolver.Step(
                ref _rescueState,
                in rescueTuning,
                in candidate,
                _animationGrounded,
                surfing,
                _physicalMode == CharacterPhysicalMode.FullRagdoll,
                verticalSpeed,
                tangentVelocity.magnitude,
                Time.deltaTime);

            animator.SetFloat(SpeedHash, presentationSpeed);
            animator.SetFloat(TurnHash, turn.Value);
            animator.SetBool(SurfingHash, surfing);
            animator.SetBool(GroundedHash, _animationGrounded);
            animator.SetFloat(VerticalSpeedHash, verticalSpeed);
            animator.SetBool(HardLandingHash, rescue.LandingStyle == EarthLandingStyle.Hard &&
                                                    (rescue.Phase == EarthAnimationPhase.PreLanding ||
                                                     rescue.Phase == EarthAnimationPhase.LandingContact ||
                                                     rescue.Phase == EarthAnimationPhase.LandingRecovery));
            bool landingStyleChanged = rescue.LandingStyle != previousLandingStyle &&
                                       (rescue.Phase == EarthAnimationPhase.PreLanding ||
                                        rescue.Phase == EarthAnimationPhase.LandingContact);
            if (rescue.PhaseChanged || landingStyleChanged) DriveRescueTransition(in rescue);
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
                _unsupportedSeconds = 0f;
                _animationGrounded = true;
                return;
            }

            _unsupportedSeconds += Time.deltaTime;
            // Curved-ground probes can miss for one rendered frame at chunk or
            // structure seams. Do not let that false negative enter a one-shot
            // jump state. A real launch is accepted immediately from velocity;
            // an ordinary fall is accepted after a short unsupported window.
            if (verticalSpeed > 0.75f || _unsupportedSeconds >= 0.11f)
                _animationGrounded = false;
        }

        private EarthAnimationRescueTuning ResolveRescueTuning() => profile != null
            ? new EarthAnimationRescueTuning(
                profile.MinimumLandingAnticipation,
                profile.MaximumLandingAnticipation,
                profile.LandingCandidateGrace,
                profile.SoftLandingImpactSpeed,
                profile.HardLandingImpactSpeed,
                profile.MovingLandingPlanarSpeed,
                profile.MovingLandingRecovery,
                profile.SoftLandingRecovery,
                profile.HardLandingRecovery)
            : EarthAnimationRescueTuning.Default;

        private void DriveRescueTransition(in EarthAnimationRescueSample rescue)
        {
            if (animator == null || !animator.enabled) return;
            int stateHash = 0;
            switch (rescue.Phase)
            {
                case EarthAnimationPhase.GroundedIdle:
                case EarthAnimationPhase.LocomotionLoop:
                    stateHash = LocomotionStateHash;
                    break;
                case EarthAnimationPhase.Rising:
                    stateHash = JumpStateHash;
                    break;
                case EarthAnimationPhase.Apex:
                case EarthAnimationPhase.Falling:
                    stateHash = FallStateHash;
                    break;
                case EarthAnimationPhase.PreLanding:
                case EarthAnimationPhase.LandingContact:
                    stateHash = rescue.LandingStyle switch
                    {
                        EarthLandingStyle.Hard => HardLandStateHash,
                        EarthLandingStyle.Moving => MovingLandStateHash,
                        _ => LandStateHash
                    };
                    break;
                case EarthAnimationPhase.SurfLoop:
                    stateHash = SurfStateHash;
                    break;
            }
            if (stateHash == 0) return;
            // PreLanding and LandingContact intentionally resolve to the same
            // authored clip. Re-entering it on physical contact restarted the
            // motion at time zero and caused a visible snap at touchdown.
            if (_activeBaseStateHash == stateHash) return;
            _activeBaseStateHash = stateHash;
            float startSeconds = 0f;
            if (rescue.Phase == EarthAnimationPhase.PreLanding ||
                rescue.Phase == EarthAnimationPhase.LandingContact)
            {
                float contactSeconds = ResolveLandingContactSeconds(rescue.LandingStyle);
                EarthLandingCandidateSnapshot candidate = LandingCandidate;
                startSeconds = EarthLandingClipPhaseAlignment.ResolveStartSeconds(
                    contactSeconds,
                    candidate.TimeToContact,
                    rescue.Phase == EarthAnimationPhase.PreLanding && candidate.IsValid);
            }
            animator.CrossFadeInFixedTime(
                stateHash,
                profile != null ? profile.FixedTransitionSeconds : 0.065f,
                0,
                startSeconds);
        }

        private float ResolveLandingContactSeconds(EarthLandingStyle style)
        {
            if (profile == null) return style == EarthLandingStyle.Moving ? 0.533f : 0.625f;
            return style switch
            {
                EarthLandingStyle.Moving => profile.MovingLandingContactSeconds,
                EarthLandingStyle.Hard => profile.HardLandingContactSeconds,
                _ => profile.SoftLandingContactSeconds
            };
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

        private float CastingBlendSeconds => profile != null ? profile.CastingBlendSeconds : 0.1f;
    }
}
