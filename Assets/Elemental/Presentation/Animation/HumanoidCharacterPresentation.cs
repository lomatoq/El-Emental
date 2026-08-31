using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Combat;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [DisallowMultipleComponent]
    public sealed class HumanoidCharacterPresentation : MonoBehaviour
    {
        private static readonly ProfilerMarker PresentationMarker =
            new ProfilerMarker("Elemental.Character.Presentation");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int TurnHash = Animator.StringToHash("Turn");
        private static readonly int SurfingHash = Animator.StringToHash("Surfing");
        private static readonly int HardLandingHash = Animator.StringToHash("HardLanding");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int GaitRateHash = Animator.StringToHash("GaitRate");
        private static readonly int CastHash = Animator.StringToHash("Cast");
        private static readonly int CastKindHash = Animator.StringToHash("CastKind");
        private static readonly int EarthPoseHash = Animator.StringToHash("EarthPose");
        private static readonly int ImpactHash = Animator.StringToHash("Impact");
        private static readonly int DodgeHash = Animator.StringToHash("Dodge");
        private static readonly int DodgeXHash = Animator.StringToHash("DodgeX");
        private static readonly int DodgeYHash = Animator.StringToHash("DodgeY");
        private static readonly int MotionTimeHash = Animator.StringToHash("EarthMotionTime");
        private static readonly int LocomotionStateHash = Animator.StringToHash("Base Layer.Locomotion");
        private static readonly int JumpStateHash = Animator.StringToHash("Base Layer.Jump");
        private static readonly int FallStateHash = Animator.StringToHash("Base Layer.Fall");
        private static readonly int LandStateHash = Animator.StringToHash("Base Layer.Land");
        private static readonly int MovingLandStateHash = Animator.StringToHash("Base Layer.Moving Land");
        private static readonly int HardLandStateHash = Animator.StringToHash("Base Layer.Hard Land");
        private static readonly int KnockdownRecoveryStateHash =
            Animator.StringToHash("Base Layer.Knockdown Recovery");
        private static readonly int DodgeStateHash = Animator.StringToHash("Base Layer.Dodge");
        private static readonly int TurnInPlaceStateHash =
            Animator.StringToHash("Base Layer.Turn In Place");
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
        [SerializeField] private EarthFootContactController footContactController;
        [SerializeField] private EarthChoreographyDirector choreographyDirector;
        [SerializeField] private EarthSurfController surfController;
        [SerializeField] private EarthAnimationRigBridge animationRigBridge;
        [SerializeField] private EarthAnimationContactPredictor contactPredictor;
        [SerializeField] private HumanoidRagdollRig visibleRagdoll;
        [SerializeField] private HumanoidProceduralBodyResponse proceduralBodyResponse;
        [SerializeField] private EarthTransitionDirector transitionDirector;
        [SerializeField] private EarthAnimationGraphProfile animationGraphProfile;
        [SerializeField] private EarthAnimationGraph animationGraph;
        [SerializeField] private bool driveMagicPresentation = true;

        private float _castWeight;
        private CharacterPhysicalMode _physicalMode;
        private int _magicLayerIndex = -1;
        private int _impactLayerIndex = -1;
        private float _impactWeight;
        private bool _wasCasting;
        private float _impactUntil;
        private bool _ragdollSubscribed;
        private bool _visibleRagdollSubscribed;
        private bool _animationGrounded = true;
        private float _unsupportedSeconds;
        private EarthAnimationRescueState _rescueState;
        private EarthScalarPresentationState _speedFilter;
        private EarthScalarPresentationState _gaitRateFilter;
        private EarthScalarPresentationState _turnFilter;
        private Vector3 _previousFacing;
        private bool _hasPreviousFacing;
        private int _activeBaseStateHash;
        private HandIkState _handIkState;
        private float _gaitPhase01;
        private float _locomotionCycleSeconds = 1f;
        private EarthMotionStateId _activeMotionState;
        private float _dodgeUntil;
        private bool _dodgeWasActive;
        private float _previousInertializationSpeed;
        private float _previousInertializationTurn;
        private bool _hasInertializationLocomotionSample;
        private bool _staggerInertializationPending;

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
        public EarthFootContactController FootContactController => footContactController;
        public HumanoidProceduralBodyResponse ProceduralBodyResponse => proceduralBodyResponse;
        public EarthTransitionDirector TransitionDirector => transitionDirector;
        public EarthAnimationGraph AnimationGraph => animationGraph;
        public EarthAuthoredActionId CurrentAuthoredAction { get; private set; }
        public EarthAuthoredFootPolicy CurrentFootPolicy { get; private set; }
        public EarthDirectionalDodgeDecision LastDodgeDecision { get; private set; }
        public bool IsDirectionalDodgeActive => Time.time < _dodgeUntil;

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
            EarthPillarMobility configuredPillarMobility = null,
            HumanoidRagdollRig configuredVisibleRagdoll = null,
            bool configuredDriveMagicPresentation = true)
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
            visibleRagdoll = configuredVisibleRagdoll;
            driveMagicPresentation = configuredDriveMagicPresentation;
            PrepareAnimator();
            ConfigureFootContactController();
            ConfigurePoseController();
            ConfigureProceduralBodyResponse();
            ConfigureAnimationGraph();
            ConfigureTransitionDirector();
            if (isActiveAndEnabled) SubscribeRagdoll();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (motor == null) motor = GetComponentInParent<PlanetMotor>();
            if (rootBody == null) rootBody = GetComponentInParent<Rigidbody>();
            if (pillarMobility == null) pillarMobility = GetComponentInParent<EarthPillarMobility>();
            if (surfController == null) surfController = GetComponentInParent<EarthSurfController>();
            if (visibleRagdoll == null) visibleRagdoll = GetComponent<HumanoidRagdollRig>();
            PrepareAnimator();
            ConfigureFootContactController();
            ConfigurePoseController();
            ConfigureProceduralBodyResponse();
            ConfigureAnimationGraph();
            ConfigureTransitionDirector();
        }

        private void PrepareAnimator()
        {
            if (animator == null) return;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // EarthFootContactController is the single base-layer foot owner.
            // Mecanim's automatic stabilizer evaluates a second solution from a
            // different clock and fights persistent surf/casting anchors.
            animator.stabilizeFeet = false;
            animator.feetPivotActive = 1f;
            _magicLayerIndex = -1;
            _impactLayerIndex = -1;
            _animationGrounded = true;
            _unsupportedSeconds = 0f;
            _activeBaseStateHash = 0;
            _activeMotionState = EarthMotionStateId.None;
            _hasInertializationLocomotionSample = false;
            _staggerInertializationPending = false;
            if (Application.isPlaying && animator.isActiveAndEnabled)
            {
                _magicLayerIndex = animator.GetLayerIndex(MagicLayerName);
                _impactLayerIndex = animator.GetLayerIndex(ImpactLayerName);
                animator.SetLayerWeight(0, 1f);
                if (_magicLayerIndex >= 0) animator.SetLayerWeight(_magicLayerIndex, 0f);
                if (_impactLayerIndex >= 0) animator.SetLayerWeight(_impactLayerIndex, 0f);
                animator.SetBool(GroundedHash, true);
            }
            if (contactPredictor == null) contactPredictor = GetComponent<EarthAnimationContactPredictor>();
            if (contactPredictor == null) contactPredictor = gameObject.AddComponent<EarthAnimationContactPredictor>();
            contactPredictor.Configure(motor);
        }

        private void ConfigureFootContactController()
        {
            if (animator == null || motor == null || rootBody == null) return;
            if (footContactController == null)
                footContactController = GetComponent<EarthFootContactController>();
            if (footContactController == null)
                footContactController = gameObject.AddComponent<EarthFootContactController>();
            footContactController.Configure(animator, motor, rootBody, poseController);
            footContactController.ConfigureAnimationRescue(
                profile != null ? profile.SurfPelvisResponseSeconds : 0.085f,
                profile != null ? profile.SurfPelvisMaximumSpeed : 0.8f);
        }

        private void ConfigurePoseController()
        {
            if (!driveMagicPresentation || animator == null || motor == null || rootBody == null)
            {
                footContactController?.SetPoseIntentSource(null);
                return;
            }
            if (poseController == null) poseController = GetComponent<EarthCharacterPoseController>();
            if (poseController == null) poseController = gameObject.AddComponent<EarthCharacterPoseController>();
            poseController.Configure(
                animator, magicInput, executor, motor, rootBody, pillarMobility, techniqueProfile);
            poseController.ConfigureAnimationRescue(
                profile != null ? profile.SurfPelvisResponseSeconds : 0.085f,
                profile != null ? profile.SurfPelvisMaximumSpeed : 0.8f);
            poseController.SetFootContactController(footContactController);
            footContactController?.SetPoseIntentSource(poseController);
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

        private void ConfigureProceduralBodyResponse()
        {
            if (animator == null || motor == null || rootBody == null) return;
            if (proceduralBodyResponse == null)
                proceduralBodyResponse = GetComponent<HumanoidProceduralBodyResponse>();
            if (proceduralBodyResponse == null)
                proceduralBodyResponse = gameObject.AddComponent<HumanoidProceduralBodyResponse>();
            proceduralBodyResponse.Configure(
                animator,
                motor,
                rootBody,
                visibleRagdoll,
                this);
        }

        private void ConfigureTransitionDirector()
        {
            if (animator == null) return;
            if (transitionDirector == null)
                transitionDirector = GetComponent<EarthTransitionDirector>();
            if (transitionDirector == null)
                transitionDirector = gameObject.AddComponent<EarthTransitionDirector>();
            transitionDirector.Configure(animator, profile);
        }

        public void SetAnimationGraphProfile(EarthAnimationGraphProfile configuredProfile)
        {
            animationGraphProfile = configuredProfile;
            ConfigureAnimationGraph();
            ConfigureTransitionDirector();
        }

        private void ConfigureAnimationGraph()
        {
            if (animator == null) return;
            if (animationGraph == null) animationGraph = GetComponent<EarthAnimationGraph>();
            EarthAnimationGraphProfile resolvedProfile = animationGraphProfile != null
                ? animationGraphProfile
                : animationGraph != null ? animationGraph.Profile : null;
            if (resolvedProfile == null) return;
            if (animationGraph == null) animationGraph = gameObject.AddComponent<EarthAnimationGraph>();
            animationGraph.Configure(
                animator,
                resolvedProfile,
                footContactController,
                visibleRagdoll);
        }

        private void OnEnable()
        {
            SubscribeRagdoll();
        }

        private void OnDisable()
        {
            ResetMagicIK();
            UnsubscribeRagdoll();
        }

        private void SubscribeRagdoll()
        {
            if (!_ragdollSubscribed && ragdoll != null)
            {
                ragdoll.StateChanged += HandlePhysicalState;
                _ragdollSubscribed = true;
            }
            if (!_visibleRagdollSubscribed && visibleRagdoll != null)
            {
                visibleRagdoll.AuthoredRecoveryBegan += HandleAuthoredRecoveryBegan;
                _visibleRagdollSubscribed = true;
            }
        }

        private void UnsubscribeRagdoll()
        {
            if (_ragdollSubscribed)
            {
                if (ragdoll != null) ragdoll.StateChanged -= HandlePhysicalState;
                _ragdollSubscribed = false;
            }
            if (_visibleRagdollSubscribed)
            {
                if (visibleRagdoll != null)
                    visibleRagdoll.AuthoredRecoveryBegan -= HandleAuthoredRecoveryBegan;
                _visibleRagdollSubscribed = false;
            }
        }

        private void Update()
        {
            using (PresentationMarker.Auto()) UpdatePresentation();
        }

        private void UpdatePresentation()
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
                // The zero-speed child is a neutralized first frame of the same
                // Mixamo walk. Blending from it into the live cycle in 75 ms can
                // rotate a knee by 10+ degrees in one normalized frame, especially
                // on the bot's stop/start planner. Give the authored legs enough
                // time to enter the cycle coherently.
                profile != null
                    ? Mathf.Max(0.14f, profile.SpeedAccelerationSeconds)
                    : 0.14f,
                profile != null ? profile.SpeedDecelerationSeconds : 0.24f,
                Time.deltaTime);
            float gaitRate = EarthAnimationParameterFilter.StepGaitRate(
                ref _gaitRateFilter,
                tangentVelocity.magnitude,
                Time.deltaTime,
                0.12f);
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
            animator.SetFloat(GaitRateHash, gaitRate);
            animator.SetFloat(TurnHash, turn.Value);
            footContactController?.SetTurnIntent(turn.Value);
            animator.SetBool(SurfingHash, surfing);
            animator.SetBool(GroundedHash, _animationGrounded);
            animator.SetFloat(VerticalSpeedHash, verticalSpeed);
            animator.SetBool(HardLandingHash, rescue.LandingStyle == EarthLandingStyle.Hard &&
                                                    (rescue.Phase == EarthAnimationPhase.PreLanding ||
                                                     rescue.Phase == EarthAnimationPhase.LandingContact ||
                                                     rescue.Phase == EarthAnimationPhase.LandingRecovery));
            DriveContinuousLocomotionInertialization(presentationSpeed, turn.Value);
            CaptureGaitPhase();
            bool landingStyleChanged = rescue.LandingStyle != previousLandingStyle &&
                                       (rescue.Phase == EarthAnimationPhase.PreLanding ||
                                        rescue.Phase == EarthAnimationPhase.LandingContact);
            bool directionalDodge = IsDirectionalDodgeActive;
            bool authoredKnockdownRecovery = visibleRagdoll != null &&
                                               visibleRagdoll.IsRecoveringToAnimation;
            int desiredGroundedState = ResolveGroundedStateHash(in rescue);
            bool groundedLaneChanged = desiredGroundedState != 0 &&
                                       desiredGroundedState != _activeBaseStateHash;
            if (!directionalDodge && !authoredKnockdownRecovery && _dodgeWasActive)
            {
                _activeBaseStateHash = 0;
                DriveRescueTransition(in rescue);
            }
            else if (!directionalDodge && !authoredKnockdownRecovery &&
                     (rescue.PhaseChanged || landingStyleChanged || groundedLaneChanged))
            {
                DriveRescueTransition(in rescue);
            }
            _dodgeWasActive = directionalDodge;
            // The bot's telegraph presenter owns its magic layer, while this shared
            // component still owns action/contact policy. Read the already-authored
            // Cast parameter so player and bot report the same semantic graph state.
            bool externallyAuthoredCast = !driveMagicPresentation && animator.GetBool(CastHash);
            UpdateAuthoredAction(in rescue, externallyAuthoredCast, directionalDodge);
            UpdateImpactPresentation();
            if (!driveMagicPresentation)
            {
                ResetMagicIK();
                _wasCasting = false;
                UpdateAnimationGraphOwnership();
                return;
            }
            int castKind = ResolveCastKind();
            bool casting = castKind > 0 && _physicalMode != CharacterPhysicalMode.FullRagdoll &&
                           ((poseController != null && poseController.CurrentRequest.IsActive) ||
                            (executor != null &&
                             (executor.HeldBody != null || executor.IsGravityWellActive ||
                              executor.IsVectorFieldActive)));
            bool movementInterruptsRecovery = poseController != null &&
                                              EarthHumanoidMotionResolver.ShouldInterruptRecovery(
                                                  poseController.CurrentRequest.Phase,
                                                  Mathf.Sqrt(
                                                      motor.LastCommand.Move.x * motor.LastCommand.Move.x +
                                                      motor.LastCommand.Move.y * motor.LastCommand.Move.y));
            if (movementInterruptsRecovery) casting = false;
            if (_wasCasting && !casting)
                transitionDirector?.RequestPoseInertialization(
                    EarthAnimationInertializationReason.CastToLocomotion,
                    profile != null ? profile.AuthoredActionTransitionSeconds : 0.12f);
            UpdateAuthoredAction(in rescue, casting, directionalDodge);
            float targetWeight = casting && _physicalMode != CharacterPhysicalMode.FullRagdoll
                ? (profile != null ? profile.HandIkWeight : 0.92f)
                : 0f;
            float castingResponse = targetWeight > _castWeight
                ? CastingBlendSeconds
                : movementInterruptsRecovery
                    ? (profile != null ? profile.LocomotionBlendSeconds : 0.08f)
                    : CastingRecoverySeconds;
            HandIkSample ikSample = HandIkSolver.Step(
                _handIkState,
                _castWeight,
                targetWeight,
                Time.deltaTime,
                castingResponse,
                castingResponse);
            _handIkState = ikSample.State;
            _castWeight = ikSample.Weight;
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
            animationRigBridge?.SetMagicWeight(_castWeight);
            UpdateHandTargets();
            _wasCasting = casting;
            UpdateAnimationGraphOwnership();
        }

        private void UpdateAnimationGraphOwnership()
        {
            bool handContact = _handIkState != HandIkState.Inactive && _castWeight > 0.001f;
            animationGraph?.SetHandContactOwnership(handContact, handContact);
        }

        private void DriveContinuousLocomotionInertialization(float speed, float turn)
        {
            if (!_hasInertializationLocomotionSample)
            {
                _previousInertializationSpeed = speed;
                _previousInertializationTurn = turn;
                _hasInertializationLocomotionSample = true;
                return;
            }

            float duration = profile != null ? profile.LocomotionTransitionSeconds : 0.14f;
            if (Mathf.Abs(_previousInertializationSpeed) > 0.55f && Mathf.Abs(speed) < 0.12f)
                transitionDirector?.RequestPoseInertialization(
                    EarthAnimationInertializationReason.RunToStop,
                    duration);
            else if (Mathf.Abs(_previousInertializationSpeed) > 0.25f &&
                     Mathf.Abs(speed) > 0.25f &&
                     Mathf.Sign(_previousInertializationSpeed) != Mathf.Sign(speed))
                transitionDirector?.RequestPoseInertialization(
                    EarthAnimationInertializationReason.DirectionReverse,
                    duration);
            if (Mathf.Abs(_previousInertializationTurn) > 0.28f && Mathf.Abs(turn) < 0.08f)
                transitionDirector?.RequestPoseInertialization(
                    EarthAnimationInertializationReason.TurnToSettle,
                    profile != null ? profile.TurnTransitionSeconds : 0.12f);

            _previousInertializationSpeed = speed;
            _previousInertializationTurn = turn;
        }

        private AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer) =>
            animationGraph != null && animationGraph.IsActive
                ? animationGraph.GetCurrentAnimatorStateInfo(layer)
                : animator.GetCurrentAnimatorStateInfo(layer);

        private AnimatorStateInfo GetNextAnimatorStateInfo(int layer) =>
            animationGraph != null && animationGraph.IsActive
                ? animationGraph.GetNextAnimatorStateInfo(layer)
                : animator.GetNextAnimatorStateInfo(layer);

        private bool IsAnimatorInTransition(int layer) =>
            animationGraph != null && animationGraph.IsActive
                ? animationGraph.IsInTransition(layer)
                : animator.IsInTransition(layer);

        private void SetAnimatorTrigger(int parameterHash)
        {
            if (animationGraph != null && animationGraph.IsActive)
                animationGraph.SetTrigger(parameterHash);
            else
                animator.SetTrigger(parameterHash);
        }

        private void ResetAnimatorTrigger(int parameterHash)
        {
            if (animationGraph != null && animationGraph.IsActive)
                animationGraph.ResetTrigger(parameterHash);
            else
                animator.ResetTrigger(parameterHash);
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
            EarthMotionStateId destinationState = EarthMotionStateId.None;
            EarthMotionCategory destinationCategory = EarthMotionCategory.None;
            switch (rescue.Phase)
            {
                case EarthAnimationPhase.GroundedIdle:
                case EarthAnimationPhase.LocomotionLoop:
                    stateHash = ResolveGroundedStateHash(in rescue);
                    destinationState = stateHash == TurnInPlaceStateHash
                        ? EarthMotionStateId.TurnInPlace
                        : EarthMotionStateId.Locomotion;
                    destinationCategory = stateHash == TurnInPlaceStateHash
                        ? EarthMotionCategory.Turn
                        : EarthMotionCategory.Locomotion;
                    break;
                case EarthAnimationPhase.Rising:
                    stateHash = JumpStateHash;
                    destinationState = EarthMotionStateId.Jump;
                    destinationCategory = EarthMotionCategory.Airborne;
                    break;
                case EarthAnimationPhase.Apex:
                case EarthAnimationPhase.Falling:
                    stateHash = FallStateHash;
                    destinationState = EarthMotionStateId.Fall;
                    destinationCategory = EarthMotionCategory.Airborne;
                    break;
                case EarthAnimationPhase.PreLanding:
                case EarthAnimationPhase.LandingContact:
                    stateHash = rescue.LandingStyle switch
                    {
                        EarthLandingStyle.Hard => HardLandStateHash,
                        EarthLandingStyle.Moving => MovingLandStateHash,
                        _ => LandStateHash
                    };
                    destinationState = rescue.LandingStyle switch
                    {
                        EarthLandingStyle.Hard => EarthMotionStateId.HardLanding,
                        EarthLandingStyle.Moving => EarthMotionStateId.MovingLanding,
                        _ => EarthMotionStateId.SoftLanding
                    };
                    destinationCategory = EarthMotionCategory.Landing;
                    break;
                case EarthAnimationPhase.SurfLoop:
                    stateHash = SurfStateHash;
                    destinationState = EarthMotionStateId.Surf;
                    destinationCategory = EarthMotionCategory.Surf;
                    break;
            }
            if (stateHash == 0) return;
            AnimatorStateInfo current = GetCurrentAnimatorStateInfo(0);
            EarthLandingCandidateSnapshot candidate = LandingCandidate;
            bool canInterrupt = CurrentAuthoredAction == EarthAuthoredActionId.None ||
                                EarthAuthoredActionCatalog.CanInterrupt(
                                    CurrentAuthoredAction,
                                    ResolveCurrentActionNormalizedTime(),
                                    ResolveRequestedAction(destinationState));
            var context = new EarthAnimationTransitionContext(
                _activeMotionState,
                destinationState,
                CategoryFor(_activeMotionState),
                destinationCategory,
                PriorityFor(destinationState),
                transitionDirector != null && transitionDirector.TransitionWeight < 1f
                    ? PriorityFor(_activeMotionState)
                    : EarthAnimationTransitionPriority.Idle,
                Mathf.Repeat(current.normalizedTime, 1f),
                _gaitPhase01,
                _locomotionCycleSeconds,
                ResolveLandingContactSeconds(rescue.LandingStyle),
                candidate.TimeToContact,
                rescue.Phase == EarthAnimationPhase.PreLanding && candidate.IsValid,
                canInterrupt,
                false,
                destinationState == EarthMotionStateId.Locomotion ||
                destinationCategory == EarthMotionCategory.Landing);
            if (transitionDirector != null &&
                transitionDirector.RequestTransition(stateHash, in context))
            {
                _activeBaseStateHash = stateHash;
                _activeMotionState = destinationState;
            }
        }

        private int ResolveGroundedStateHash(in EarthAnimationRescueSample rescue)
        {
            if (rescue.Phase != EarthAnimationPhase.GroundedIdle &&
                rescue.Phase != EarthAnimationPhase.LocomotionLoop)
                return 0;
            bool turningInPlace = Mathf.Abs(_speedFilter.Value) < 0.35f &&
                                  Mathf.Abs(_turnFilter.Value) >= 0.20f;
            return turningInPlace ? TurnInPlaceStateHash : LocomotionStateHash;
        }

        private void CaptureGaitPhase()
        {
            if (animator == null || IsAnimatorInTransition(0)) return;
            AnimatorStateInfo state = GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != LocomotionStateHash) return;
            _gaitPhase01 = Mathf.Repeat(state.normalizedTime, 1f);
            _locomotionCycleSeconds = Mathf.Max(0.01f, state.length);
        }

        private static EarthMotionCategory CategoryFor(EarthMotionStateId state) => state switch
        {
            EarthMotionStateId.Locomotion => EarthMotionCategory.Locomotion,
            EarthMotionStateId.TurnInPlace => EarthMotionCategory.Turn,
            EarthMotionStateId.Jump or EarthMotionStateId.Fall => EarthMotionCategory.Airborne,
            EarthMotionStateId.SoftLanding or EarthMotionStateId.MovingLanding or
                EarthMotionStateId.HardLanding => EarthMotionCategory.Landing,
            EarthMotionStateId.Surf => EarthMotionCategory.Surf,
            EarthMotionStateId.DirectionalDodge => EarthMotionCategory.AuthoredAction,
            EarthMotionStateId.KnockdownRecovery => EarthMotionCategory.RagdollRecovery,
            EarthMotionStateId.ImpactOverlay => EarthMotionCategory.Impact,
            _ => EarthMotionCategory.None
        };

        private static EarthAnimationTransitionPriority PriorityFor(EarthMotionStateId state) =>
            state switch
            {
                EarthMotionStateId.KnockdownRecovery => EarthAnimationTransitionPriority.HeavyImpact,
                EarthMotionStateId.DirectionalDodge => EarthAnimationTransitionPriority.DefensiveCancel,
                EarthMotionStateId.ImpactOverlay => EarthAnimationTransitionPriority.MediumStagger,
                EarthMotionStateId.SoftLanding or EarthMotionStateId.MovingLanding or
                    EarthMotionStateId.HardLanding => EarthAnimationTransitionPriority.LandingContact,
                EarthMotionStateId.Locomotion or EarthMotionStateId.TurnInPlace =>
                    EarthAnimationTransitionPriority.Locomotion,
                _ => EarthAnimationTransitionPriority.Idle
            };

        private static EarthAuthoredActionId ResolveRequestedAction(EarthMotionStateId state) =>
            state switch
            {
                EarthMotionStateId.SoftLanding => EarthAuthoredActionId.SoftLanding,
                EarthMotionStateId.MovingLanding => EarthAuthoredActionId.MovingLandingRoll,
                EarthMotionStateId.HardLanding => EarthAuthoredActionId.HardLandingBrace,
                EarthMotionStateId.DirectionalDodge => EarthAuthoredActionId.DirectionalDodge,
                EarthMotionStateId.KnockdownRecovery =>
                    EarthAuthoredActionId.RecoverableKnockdownRecovery,
                EarthMotionStateId.ImpactOverlay => EarthAuthoredActionId.HitRecoil,
                _ => EarthAuthoredActionId.None
            };

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

        private void UpdateAuthoredAction(
            in EarthAnimationRescueSample rescue,
            bool casting,
            bool directionalDodge)
        {
            bool recoverableRecovery = visibleRagdoll != null &&
                                       visibleRagdoll.IsRecoveringToAnimation;
            bool impactReaction = Time.time < _impactUntil &&
                                  _physicalMode != CharacterPhysicalMode.FullRagdoll;
            EarthAuthoredActionId resolvedAction = EarthAuthoredActionResolver.Resolve(
                rescue.Phase,
                rescue.LandingStyle,
                recoverableRecovery,
                casting,
                impactReaction,
                directionalDodge);
            float normalizedTime = ResolveCurrentActionNormalizedTime();
            if (TryResolveActiveBaseLayerLanding(
                    out EarthAuthoredActionId activeLanding,
                    out float activeLandingNormalizedTime))
            {
                resolvedAction = EarthAuthoredActionResolver.ResolveBaseLayerContactOwnership(
                    resolvedAction,
                    activeLanding);
                if (resolvedAction == activeLanding)
                    normalizedTime = activeLandingNormalizedTime;
            }
            CurrentAuthoredAction = resolvedAction;
            EarthAuthoredActionDefinition definition =
                EarthAuthoredActionCatalog.Resolve(CurrentAuthoredAction);
            CurrentFootPolicy = definition.FootPolicyAt(normalizedTime);
            footContactController?.SetAuthoredFootPolicy(CurrentFootPolicy);
        }

        private bool TryResolveActiveBaseLayerLanding(
            out EarthAuthoredActionId action,
            out float normalizedTime)
        {
            action = EarthAuthoredActionId.None;
            normalizedTime = 0f;
            if (animator == null || !animator.enabled) return false;

            // Prefer the current landing while it blends out. On entry, where
            // the current state is still Fall, use the incoming landing state.
            AnimatorStateInfo current = GetCurrentAnimatorStateInfo(0);
            if (TryMapLandingState(current, out action))
            {
                normalizedTime = Mathf.Clamp01(current.normalizedTime);
                return true;
            }
            if (!IsAnimatorInTransition(0)) return false;
            AnimatorStateInfo next = GetNextAnimatorStateInfo(0);
            if (!TryMapLandingState(next, out action)) return false;
            normalizedTime = Mathf.Clamp01(next.normalizedTime);
            return true;
        }

        private static bool TryMapLandingState(
            AnimatorStateInfo state,
            out EarthAuthoredActionId action)
        {
            if (state.fullPathHash == MovingLandStateHash)
            {
                action = EarthAuthoredActionId.MovingLandingRoll;
                return true;
            }
            if (state.fullPathHash == HardLandStateHash)
            {
                action = EarthAuthoredActionId.HardLandingBrace;
                return true;
            }
            if (state.fullPathHash == LandStateHash)
            {
                action = EarthAuthoredActionId.SoftLanding;
                return true;
            }
            action = EarthAuthoredActionId.None;
            return false;
        }

        private float ResolveCurrentActionNormalizedTime()
        {
            if (animator == null || !animator.enabled) return 0f;
            // During an authored cross-fade the outgoing state can already be near
            // its end while the incoming jump/land/recovery clip is at frame zero.
            // Contact windows must follow the incoming action, otherwise IK can be
            // re-enabled for one frame exactly at take-off or landing contact.
            AnimatorStateInfo state = IsAnimatorInTransition(0)
                ? GetNextAnimatorStateInfo(0)
                : GetCurrentAnimatorStateInfo(0);
            return Mathf.Clamp01(state.normalizedTime);
        }

        public void NotifyImpactResponse(EarthCharacterImpactResponse response)
        {
            if (response != EarthCharacterImpactResponse.Flinch &&
                response != EarthCharacterImpactResponse.Stagger) return;
            if (CurrentAuthoredAction == EarthAuthoredActionId.DirectionalDodge)
            {
                float normalizedTime = ResolveCurrentActionNormalizedTime();
                if (!EarthAuthoredActionCatalog.CanInterrupt(
                        CurrentAuthoredAction,
                        normalizedTime,
                        EarthAuthoredActionId.HitRecoil)) return;
                _dodgeUntil = 0f;
                _dodgeWasActive = true;
            }
            float duration = response == EarthCharacterImpactResponse.Stagger ? 0.46f : 0.24f;
            _impactUntil = Mathf.Max(_impactUntil, Time.time + duration);
            transitionDirector?.RequestPoseInertialization(
                EarthAnimationInertializationReason.LocomotionToFlinch,
                profile != null ? profile.AuthoredActionTransitionSeconds : 0.12f);
            if (response == EarthCharacterImpactResponse.Stagger)
                _staggerInertializationPending = true;
            if (!HasProceduralImpactOwner && animator != null && animator.enabled)
                SetAnimatorTrigger(ImpactHash);
        }

        /// <summary>
        /// Presentation-side authored dodge request. The gameplay motor keeps all
        /// displacement/collision authority; this selects one of four real KayKit
        /// clips and publishes its deterministic foot-contact window.
        /// </summary>
        public bool TryPlayDirectionalDodge(Vector2 localDirection)
        {
            float normalizedTime = ResolveCurrentActionNormalizedTime();
            bool recovering = _physicalMode == CharacterPhysicalMode.FullRagdoll ||
                              (visibleRagdoll != null &&
                               (visibleRagdoll.IsRagdollActive ||
                                visibleRagdoll.IsRecoveringToAnimation));
            bool casting = animator != null && animator.enabled &&
                           (animator.GetBool(CastHash) || _castWeight > 0.05f);
            var input = new EarthDirectionalDodgeInput(
                new float2(localDirection.x, localDirection.y),
                motor != null && motor.HasStableSupport && _animationGrounded,
                surfController != null && surfController.IsActive,
                casting,
                recovering,
                CurrentAuthoredAction,
                normalizedTime);
            LastDodgeDecision = EarthDirectionalDodgeGate.Resolve(in input);
            if (!LastDodgeDecision.Accepted || animator == null || !animator.enabled)
                return false;

            _dodgeUntil = Time.time + 0.48f;
            _dodgeWasActive = true;
            animator.SetFloat(DodgeXHash, LastDodgeDecision.BlendDirection.x);
            animator.SetFloat(DodgeYHash, LastDodgeDecision.BlendDirection.y);
            ResetAnimatorTrigger(DodgeHash);
            SetAnimatorTrigger(DodgeHash);
            _activeBaseStateHash = DodgeStateHash;
            _activeMotionState = EarthMotionStateId.DirectionalDodge;
            transitionDirector?.SynchronizeState(
                _activeMotionState,
                _activeBaseStateHash,
                EarthAnimationTransitionPriority.DefensiveCancel);
            CurrentAuthoredAction = EarthAuthoredActionId.DirectionalDodge;
            CurrentFootPolicy = EarthAuthoredActionCatalog.Resolve(CurrentAuthoredAction)
                .FootPolicyAt(0f);
            footContactController?.SetAuthoredFootPolicy(CurrentFootPolicy);
            return true;
        }

        private void HandleAuthoredRecoveryBegan()
        {
            _dodgeUntil = 0f;
            _dodgeWasActive = false;
            if (animator != null && animator.enabled && transitionDirector != null)
            {
                AnimatorStateInfo current = GetCurrentAnimatorStateInfo(0);
                var context = new EarthAnimationTransitionContext(
                    _activeMotionState,
                    EarthMotionStateId.KnockdownRecovery,
                    CategoryFor(_activeMotionState),
                    EarthMotionCategory.RagdollRecovery,
                    EarthAnimationTransitionPriority.HeavyImpact,
                    PriorityFor(_activeMotionState),
                    Mathf.Repeat(current.normalizedTime, 1f),
                    _gaitPhase01,
                    Mathf.Max(0.01f, current.length),
                    0f,
                    0f,
                    false,
                    true,
                    false,
                    false);
                transitionDirector.RequestTransition(
                    KnockdownRecoveryStateHash,
                    in context);
            }
            _activeBaseStateHash = KnockdownRecoveryStateHash;
            _activeMotionState = EarthMotionStateId.KnockdownRecovery;
            CurrentAuthoredAction = EarthAuthoredActionId.RecoverableKnockdownRecovery;
            CurrentFootPolicy = EarthAuthoredActionCatalog.Resolve(CurrentAuthoredAction)
                .FootPolicyAt(0.18f);
            footContactController?.SetAuthoredFootPolicy(CurrentFootPolicy);
            ResetMagicIK();
        }

        private void UpdateImpactPresentation()
        {
            if (animator == null) return;
            float impactTarget = Time.time < _impactUntil &&
                                 _physicalMode != CharacterPhysicalMode.FullRagdoll &&
                                 !HasProceduralImpactOwner
                ? 0.56f
                : 0f;
            _impactWeight = Mathf.MoveTowards(
                _impactWeight,
                impactTarget,
                Time.deltaTime / (impactTarget > 0f ? 0.045f : 0.18f));
            if (_staggerInertializationPending && Time.time >= _impactUntil &&
                _impactWeight <= 0.001f)
            {
                transitionDirector?.RequestPoseInertialization(
                    EarthAnimationInertializationReason.StaggerToLocomotion,
                    profile != null ? profile.AuthoredActionTransitionSeconds : 0.12f);
                _staggerInertializationPending = false;
            }
            if (_impactLayerIndex >= 0) animator.SetLayerWeight(_impactLayerIndex, _impactWeight);
        }

        private bool HasProceduralImpactOwner =>
            proceduralBodyResponse != null && proceduralBodyResponse.isActiveAndEnabled;

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
            if (_handIkState == HandIkState.Inactive || leftHandTarget == null || rightHandTarget == null) return;
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
            if (state.Mode == CharacterPhysicalMode.FullRagdoll)
            {
                _dodgeUntil = 0f;
                _dodgeWasActive = false;
                ResetMagicIK();
            }
            if (animator == null) return;
            bool animatorEnabled = state.Mode != CharacterPhysicalMode.FullRagdoll;
            if (visibleRagdoll == null && animator.enabled != animatorEnabled)
            {
                animator.enabled = animatorEnabled;
                if (animatorEnabled)
                {
                    // The visible rigid-part hierarchy is intentionally parented
                    // below Humanoid bones. Animator.Rebind after that runtime
                    // parenting can leave the state clock running while the bone
                    // transforms stay frozen, so resume without rebuilding bindings.
                    transitionDirector?.ForcePlayImmediate(
                        EarthMotionStateId.Locomotion,
                        LocomotionStateHash,
                        0f);
                    animator.Update(0f);
                    _animationGrounded = true;
                    _unsupportedSeconds = 0f;
                }
            }
            if (state.Mode == CharacterPhysicalMode.Stagger)
                NotifyImpactResponse(EarthCharacterImpactResponse.Stagger);
        }

        private float CastingBlendSeconds => profile != null ? profile.CastingBlendSeconds : 0.1f;
        private float CastingRecoverySeconds => profile != null ? profile.CastingRecoverySeconds : 0.22f;

        public void ResetMagicIK()
        {
            _castWeight = 0f;
            _handIkState = HandIkState.Inactive;
            animationRigBridge?.ResetMagicIk();
            if (animator == null) return;
            animator.SetBool(CastHash, false);
            animator.SetInteger(CastKindHash, 0);
            animator.SetFloat(EarthPoseHash, 0f);
            if (_magicLayerIndex >= 0) animator.SetLayerWeight(_magicLayerIndex, 0f);
            for (int index = 0; index < EarthPoseWeightHashes.Length; index++)
                animator.SetFloat(EarthPoseWeightHashes[index], 0f);
            Transform left = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            Transform right = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (left != null && leftHandTarget != null)
                leftHandTarget.SetPositionAndRotation(left.position, left.rotation);
            if (right != null && rightHandTarget != null)
                rightHandTarget.SetPositionAndRotation(right.position, right.rotation);
        }
    }
}
