using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Animation;
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
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
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
        private static readonly int MotionTimeAHash = Animator.StringToHash("EarthMotionTimeA");
        private static readonly int MotionTimeBHash = Animator.StringToHash("EarthMotionTimeB");
        private static readonly int LocomotionStateHash = Animator.StringToHash("Base Layer.Locomotion");
        private static readonly int JumpStateHash = Animator.StringToHash("Base Layer.Jump");
        private static readonly int FallStateHash = Animator.StringToHash("Base Layer.Fall");
        private static readonly int LandStateHash = Animator.StringToHash("Base Layer.Land");
        private static readonly int MovingLandStateHash = Animator.StringToHash("Base Layer.Moving Land");
        private static readonly int MovingLandBackStateHash = Animator.StringToHash("Base Layer.Moving Land Back");
        private static readonly int HardLandStateHash = Animator.StringToHash("Base Layer.Hard Land");
        private static readonly int KnockdownRecoveryStateHash =
            Animator.StringToHash("Base Layer.Knockdown Recovery");
        private static readonly int KnockdownRecoveryBackStateHash =
            Animator.StringToHash("Base Layer.Knockdown Recovery Back");
        private static readonly int DodgeStateHash = Animator.StringToHash("Base Layer.Dodge");
        private static readonly int TurnInPlaceStateHash =
            Animator.StringToHash("Base Layer.Turn In Place");
        private static readonly int EarthCastStateHash =
            Animator.StringToHash("Earth Magic Upper Body.Earth Cast");
        private static readonly int EarthCastBStateHash =
            Animator.StringToHash("Earth Magic Upper Body.Earth Cast B");
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
        private static readonly int[] EarthPoseAWeightHashes = CreateMagicBufferHashes("EarthPoseA");
        private static readonly int[] EarthPoseBWeightHashes = CreateMagicBufferHashes("EarthPoseB");
        private const float MagicBufferCrossFadeSeconds = 0.08f;
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
        [SerializeField] private EarthAnimationDriver animationDriver;
        [SerializeField] private bool driveMagicPresentation = true;
        [SerializeField] private EarthMagicMotionProfile magicMotionProfile;
        private EarthMagicClipClock _magicClipClock;
        private int _activeMagicBuffer = -1;
        private int _outgoingMagicBuffer = -1;
        private int _activeMagicBufferCastKind;
        private uint _activeMagicBufferSequence;
        private float _activeMagicBufferVisibleAt;
        private float _outgoingMagicBufferClearAt;
        private bool _hasPendingRenderedMagicSample;
        private uint _pendingRenderedMagicSequence;
        private float _pendingRenderedMagicTime;
        private float _pendingRenderedMagicContact;
        private float _pendingRenderedMagicRecovery;
        private EarthMagicMotionEntry _activeMagicMotion;
        private bool _wasMantling;
        private bool _mantleAwaitingGroundedExit;
        private uint _mantleSequence;
        private float _mantleHandWeight;
        private static readonly int MantleStateHash = Animator.StringToHash("Base Layer.Mantle");
        private static readonly int MantleTimeHash = Animator.StringToHash("MantleTime");
        public float MagicClipTime => _magicClipClock.NormalizedTime;
        public void ConfigureMagicMotionProfile(EarthMagicMotionProfile value) => magicMotionProfile = value;

        private float _castWeight;
        private float _magicHandConstraintWeight;
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
        private EarthLocomotionBlendState _locomotionBlend;
        private EarthScalarPresentationState _gaitRateFilter;
        private EarthScalarPresentationState _turnFilter;
        private Vector3 _previousFacing;
        private bool _hasPreviousFacing;
        private int _activeBaseStateHash;
        private EarthResponsiveHandTargetState _responsiveHandTargetState;
        private HandIkState _handIkState;
        private float _gaitPhase01;
        private float _locomotionCycleSeconds = 1f;
        private EarthMotionStateId _activeMotionState;
        private float _dodgeUntil;
        private bool _dodgeWasActive;
        private bool _hasObservedLandingSupport;
        private bool _wasLandingSupported;
        private bool _hasLandingPosition;
        private Vector3 _previousLandingPosition;
        private float _airborneHeight;
        private float _airbornePeakHeight;
        private float _landingImpactSpeed;
        private float _landingPoseBlend = 1f;
        private float _jumpIntentUntil;
        private bool _deliberateJump;
        private bool _ordinaryJumpMagicCleared;
        private float _signedTakeoffSpeed;
        private float _lastAirForwardSpeed;
        private float _externalAirDeltaSpeed;
        private Vector3 _previousEvidenceVelocity;
        private float _previousEvidenceFixedTime = -1f;
        private bool _landingBackwards;

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
        public EarthAuthoredActionId CurrentAuthoredAction { get; private set; }
        public EarthAuthoredFootPolicy CurrentFootPolicy { get; private set; }
        public EarthDirectionalDodgeDecision LastDodgeDecision { get; private set; }
        public bool IsDirectionalDodgeActive => Time.time < _dodgeUntil;
        public ImpactMotionLane LastImpactMotionLane { get; private set; }
        public float LandingDropHeight { get; private set; }
        public float LandingAirborneSeconds { get; private set; }
        public float LandingPoseStrength { get; private set; }
        public bool LandingRollAllowed { get; private set; }
        public bool LandingBackwards => _landingBackwards;
        public float LandingExternalDeltaSpeed => _externalAirDeltaSpeed;

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
            ConfigureTransitionDirector();
        }

        private void PrepareAnimator()
        {
            if (animator == null) return;
            EnsureAnimationDriver(true);
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
            if (Application.isPlaying && animator.isActiveAndEnabled)
            {
                _magicLayerIndex = animator.GetLayerIndex(MagicLayerName);
                _impactLayerIndex = animator.GetLayerIndex(ImpactLayerName);
                animationDriver.SetLayerWeight(0, 1f);
                if (_magicLayerIndex >= 0) animationDriver.SetLayerWeight(_magicLayerIndex, 0f);
                if (_impactLayerIndex >= 0) animationDriver.SetLayerWeight(_impactLayerIndex, 0f);
                animationDriver.SetBool(GroundedHash, true);
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

        private void OnEnable()
        {
            // No-domain-reload sessions and round object reactivation must not
            // resume a previous fall/roll/dodge lane. Initialize semantic state as
            // supported; the motor may promote it to a real fall after its normal
            // unsupported grace window.
            ResetTransientAnimationState();
            poseController?.SetPresentationSuppressed(false);
            SubscribeRagdoll();
        }

        private void OnDisable()
        {
            poseController?.SetPresentationSuppressed(true);
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
                visibleRagdoll.RagdollBegan += HandleVisibleRagdollBegan;
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
                {
                    visibleRagdoll.AuthoredRecoveryBegan -= HandleAuthoredRecoveryBegan;
                    visibleRagdoll.RagdollBegan -= HandleVisibleRagdollBegan;
                }
                _visibleRagdollSubscribed = false;
            }
        }

        private void ResetTransientAnimationState()
        {
            _physicalMode = CharacterPhysicalMode.AnimatedMotor;
            _animationGrounded = true;
            _unsupportedSeconds = 0f;
            _rescueState = new EarthAnimationRescueState
            {
                Phase = EarthAnimationPhase.GroundedIdle
            };
            _speedFilter = default;
            _locomotionBlend = default;
            _gaitRateFilter = default;
            _turnFilter = default;
            _hasPreviousFacing = false;
            _activeBaseStateHash = LocomotionStateHash;
            _activeMotionState = EarthMotionStateId.Locomotion;
            _dodgeUntil = 0f;
            _dodgeWasActive = false;
            _impactUntil = 0f;
            _impactWeight = 0f;
            _wasMantling = false;
            _mantleAwaitingGroundedExit = false;
            _mantleSequence = 0u;
            _mantleHandWeight = 0f;
            CurrentAuthoredAction = EarthAuthoredActionId.None;
            CurrentFootPolicy = default;
            LastDodgeDecision = default;
            LastImpactMotionLane = default;
            _hasObservedLandingSupport = false;
            _wasLandingSupported = false;
            _hasLandingPosition = false;
            _airborneHeight = 0f;
            _airbornePeakHeight = 0f;
            _landingImpactSpeed = 0f;
            LandingDropHeight = 0f;
            LandingAirborneSeconds = 0f;
            LandingPoseStrength = 0f;
            _landingPoseBlend = 1f;
            _jumpIntentUntil = -1f;
            _deliberateJump = false;
            _ordinaryJumpMagicCleared = false;
            _signedTakeoffSpeed = 0f;
            _lastAirForwardSpeed = 0f;
            _externalAirDeltaSpeed = 0f;
            _previousEvidenceFixedTime = -1f;
            _landingBackwards = false;
            LandingRollAllowed = false;

            if (!Application.isPlaying || !EnsureAnimationDriver(false)) return;
            animationDriver.SetLandingPoseWeight(1f);
            animationDriver.ResetTrigger(DodgeHash);
            animationDriver.ResetTrigger(ImpactHash);
            animationDriver.SetBool(GroundedHash, true);
            animationDriver.SetBool(HardLandingHash, false);
            animationDriver.SetBool(SurfingHash, false);
            animationDriver.SetFloat(VerticalSpeedHash, 0f);
            animationDriver.Play(LocomotionStateHash, 0, 0f);
            transitionDirector?.SynchronizeState(
                EarthMotionStateId.Locomotion,
                LocomotionStateHash,
                EarthAnimationTransitionPriority.Idle);
        }

        private void Update()
        {
            using (PresentationMarker.Auto()) UpdatePresentation();
        }

        private void UpdatePresentation()
        {
            if (animator == null || rootBody == null || motor == null) return;
            bool protectedAnimationOwner = motor.IsMantling || _mantleAwaitingGroundedExit ||
                                            _physicalMode == CharacterPhysicalMode.FullRagdoll ||
                                            (visibleRagdoll != null &&
                                             (visibleRagdoll.IsRagdollActive ||
                                              visibleRagdoll.IsRecoveringToAnimation));
            poseController?.SetPresentationSuppressed(protectedAnimationOwner);
            if (!EnsureAnimationDriver(true)) return;
            if (PresentMantle()) return;
            if (_mantleAwaitingGroundedExit)
            {
                // The fixed-clock path can finish one rendered frame before the
                // destination support probe confirms grounding. Keep the protected
                // mantle pose and feet-off policy across that physical boundary;
                // ordinary airborne selection would flash a fall pose here.
                if (!motor.HasStableSupport) return;
                CompleteMantleGroundedExit();
            }
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

            // Locomotion blend weight is driven by actual support-relative travel,
            // not by the facing projection. Aim/facing is allowed to differ from
            // travel (strafing, camera-relative input and AI chase), and using the
            // dot product made a physically moving character report Speed = 0.
            // Direction remains available through MoveX/MoveY below.
            float measuredSpeed = surfing ? 0f : tangentVelocity.magnitude;
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

            UpdateLandingEvidence(verticalSpeed, Vector3.Dot(tangentVelocity, facing));
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
                Time.deltaTime,
                LandingRollAllowed);

            if (motor.HasStableSupport)
            {
                _ordinaryJumpMagicCleared = false;
            }
            else if (EarthPersistentAnimationPolicy.ShouldClearMagicForOrdinaryJump(
                         false,
                         _deliberateJump,
                         _ordinaryJumpMagicCleared,
                         rescue.Phase))
            {
                // The base Jump/Fall lane owns an ordinary Space takeoff. Clear a
                // stale upper-body cast exactly once at confirmed support departure;
                // later airborne casts remain possible and pillar launches are not
                // classified as deliberate motor jumps.
                poseController?.CancelPresentationForAnimationOwnership();
                ResetMagicIK();
                _ordinaryJumpMagicCleared = true;
            }

            LandingPoseStrength = _hasObservedLandingSupport
                ? EarthLandingPoseStrength.Resolve(
                    LandingDropHeight,
                    Mathf.Max(_landingImpactSpeed, candidate.IsValid ? candidate.ImpactSpeed : 0f),
                    LandingAirborneSeconds)
                : 0f;
            if (LandingRollAllowed) LandingPoseStrength = 1f;
            bool landingPose = rescue.Phase is EarthAnimationPhase.PreLanding or
                EarthAnimationPhase.LandingContact or EarthAnimationPhase.LandingRecovery ||
                TryResolveActiveBaseLayerLanding(out _, out _);
            float landingTarget = landingPose ? LandingPoseStrength : 1f;
            // Attenuate immediately so a short hop never flashes one full-strength
            // hard-landing frame; only increasing strength/recovery is smoothed.
            _landingPoseBlend = landingTarget < _landingPoseBlend
                ? landingTarget
                : Mathf.MoveTowards(_landingPoseBlend, landingTarget, Time.deltaTime / 0.06f);
            animationDriver.SetLandingPoseWeight(_landingPoseBlend);

            animationDriver.SetFloat(SpeedHash, presentationSpeed);
            float2 blendVelocity = EarthLocomotionBlend.Step(
                ref _locomotionBlend,
                new float3(tangentVelocity.x, tangentVelocity.y, tangentVelocity.z),
                new float3(motor.LocalUp.x, motor.LocalUp.y, motor.LocalUp.z),
                new float3(facing.x, facing.y, facing.z),
                !surfing && targetSpeed > 0f,
                Time.deltaTime);
            animationDriver.SetFloat(MoveXHash, blendVelocity.x);
            animationDriver.SetFloat(MoveYHash, blendVelocity.y);
            animationDriver.SetFloat(GaitRateHash, gaitRate);
            animationDriver.SetFloat(TurnHash, turn.Value);
            footContactController?.SetTurnIntent(turn.Value);
            animationDriver.SetBool(SurfingHash, surfing);
            animationDriver.SetBool(GroundedHash, _animationGrounded);
            animationDriver.SetFloat(VerticalSpeedHash, verticalSpeed);
            animationDriver.SetBool(HardLandingHash, rescue.LandingStyle == EarthLandingStyle.Hard &&
                                                    (rescue.Phase == EarthAnimationPhase.PreLanding ||
                                                     rescue.Phase == EarthAnimationPhase.LandingContact ||
                                                     rescue.Phase == EarthAnimationPhase.LandingRecovery));
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
            bool externallyAuthoredCast = !driveMagicPresentation && animationDriver.GetBool(CastHash);
            UpdateAuthoredAction(in rescue, externallyAuthoredCast, directionalDodge);
            UpdateImpactPresentation();
            if (!driveMagicPresentation)
            {
                // The bot presenter owns the cast layer. Clearing its parameters
                // here every Update erased the telegraph authored the frame before.
                _castWeight = 0f;
                _handIkState = HandIkState.Inactive;
                animationRigBridge?.ResetMagicIk();
                _wasCasting = false;
                return;
            }
            int castKind = ResolveCastKind();
            bool casting = castKind > 0 && !authoredKnockdownRecovery &&
                           _physicalMode != CharacterPhysicalMode.FullRagdoll &&
                           ((poseController != null && poseController.CurrentRequest.IsActive) ||
                            (executor != null &&
                             (executor.HeldBody != null || executor.IsGravityWellActive ||
                              executor.IsVectorFieldActive)));
            UpdateAuthoredAction(in rescue, casting, directionalDodge);
            float targetWeight = casting && _physicalMode != CharacterPhysicalMode.FullRagdoll
                ? (profile != null ? profile.HandIkWeight : 0.92f)
                : 0f;
            float castingResponse = targetWeight > _castWeight
                ? CastingBlendSeconds
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
            animationDriver.SetBool(CastHash, casting);
            animationDriver.SetInteger(CastKindHash, castKind);
            animationDriver.SetFloat(EarthPoseHash, castKind, 0.055f, Time.deltaTime);
            uint magicPresentationGeneration = poseController != null
                ? poseController.AuthoritativePresentationGeneration
                : 0u;
            if (casting && (!_wasCasting || _activeMagicBuffer < 0 ||
                            magicPresentationGeneration != _activeMagicBufferSequence ||
                            castKind != _activeMagicBufferCastKind))
                BeginMagicBuffer(magicPresentationGeneration, castKind);
            ClearOutgoingMagicBufferWhenHidden();
            _activeMagicMotion = magicMotionProfile != null ? magicMotionProfile.Find(castKind) : null;
            EarthMagicClipTiming clipTiming = _activeMagicMotion != null
                ? _activeMagicMotion.timing : EarthMagicClipTiming.Default;
            if (casting && poseController != null)
                poseController.EnsureRenderedContactBudget(
                    clipTiming.Contact,
                    EarthMagicClipClock.MaximumSpeedForSlot(castKind),
                    MagicBufferCrossFadeSeconds + .30f);
            float motionTime = _magicClipClock.Step(castKind,
                magicPresentationGeneration,
                poseController != null ? poseController.CurrentRequest.Phase : EarthCastPhase.Sustain,
                casting, in clipTiming, Time.deltaTime,
                poseController != null && poseController.AuthoritativeStartsAtContact);
            // The clip clock already interpolates continuously. Smoothing it a
            // second time delays authored contact and never reaches markers.
            animationDriver.SetFloat(MotionTimeHash, motionTime);
            if (_activeMagicBuffer >= 0)
                animationDriver.SetFloat(
                    _activeMagicBuffer == 0 ? MotionTimeAHash : MotionTimeBHash,
                    motionTime);
            for (int index = 0; index < EarthPoseWeightHashes.Length; index++)
            {
                float targetPoseWeight = casting && castKind == index + 1 ? 1f : 0f;
                animationDriver.SetFloat(
                    EarthPoseWeightHashes[index],
                    targetPoseWeight,
                    targetPoseWeight > animationDriver.GetFloat(EarthPoseWeightHashes[index])
                        ? 0.055f
                        : 0.10f,
                    Time.deltaTime);
            }
            if (_magicLayerIndex >= 0) animationDriver.SetLayerWeight(_magicLayerIndex, _castWeight);
            _hasPendingRenderedMagicSample = poseController != null && casting;
            _pendingRenderedMagicSequence = poseController != null
                ? poseController.LastAuthoritativeTick
                : 0u;
            _pendingRenderedMagicTime = motionTime;
            _pendingRenderedMagicContact = clipTiming.Contact;
            _pendingRenderedMagicRecovery = clipTiming.RecoverEnd;
            // One-shot actions retain complete authored arm ownership. Only a
            // real held field/body may blend toward a persistent aim, and only
            // after that action's contact pose was actually rendered.
            float handConstraintTarget = ResolveMagicHandConstraintTarget();
            _magicHandConstraintWeight = EarthAnimationDriver.DampParameter(
                _magicHandConstraintWeight,
                handConstraintTarget,
                handConstraintTarget > _magicHandConstraintWeight ? .10f : .08f,
                Time.deltaTime);
            animationRigBridge?.SetMagicWeight(HandConstraintWeight);
            UpdateHandTargets();
            animationRigBridge?.PrepareForEvaluation();
            _wasCasting = casting;
        }

        private void LateUpdate()
        {
            if (!_hasPendingRenderedMagicSample || poseController == null) return;
            _hasPendingRenderedMagicSample = false;
            float renderedSemanticWeight = IsActiveMagicBufferRendered() ? 1f : 0f;
            float renderedLayerWeight = _magicLayerIndex >= 0
                ? animationDriver.GetLayerWeight(_magicLayerIndex)
                : 0f;
            poseController.NotifyRenderedMagicSample(
                _pendingRenderedMagicSequence,
                _pendingRenderedMagicTime,
                _pendingRenderedMagicContact,
                _pendingRenderedMagicRecovery,
                renderedSemanticWeight,
                renderedLayerWeight);
        }

        private void BeginMagicBuffer(uint sequence, int castKind)
        {
            int nextBuffer = _activeMagicBuffer < 0 ? 0 : 1 - _activeMagicBuffer;
            int previousBuffer = _activeMagicBuffer;
            int[] nextWeights = nextBuffer == 0
                ? EarthPoseAWeightHashes
                : EarthPoseBWeightHashes;
            for (int index = 0; index < nextWeights.Length; index++)
                animationDriver.SetFloat(nextWeights[index], castKind == index + 1 ? 1f : 0f);
            animationDriver.SetFloat(nextBuffer == 0 ? MotionTimeAHash : MotionTimeBHash, 0f);

            int nextState = nextBuffer == 0 ? EarthCastStateHash : EarthCastBStateHash;
            bool hasVisibleOutgoing = previousBuffer >= 0 && _castWeight > .05f;
            if (_magicLayerIndex >= 0)
            {
                if (hasVisibleOutgoing)
                    animationDriver.CrossFadeInFixedTime(
                        nextState, MagicBufferCrossFadeSeconds, _magicLayerIndex, 0f);
                else
                    animationDriver.Play(nextState, _magicLayerIndex, 0f);
            }

            _magicClipClock = default;
            _activeMagicBuffer = nextBuffer;
            _activeMagicBufferSequence = sequence;
            _activeMagicBufferCastKind = castKind;
            _activeMagicBufferVisibleAt = Time.unscaledTime +
                (hasVisibleOutgoing ? MagicBufferCrossFadeSeconds : 0f);
            _outgoingMagicBuffer = hasVisibleOutgoing ? previousBuffer : -1;
            _outgoingMagicBufferClearAt = Time.unscaledTime + MagicBufferCrossFadeSeconds + .02f;
        }

        private void ClearOutgoingMagicBufferWhenHidden()
        {
            if (_outgoingMagicBuffer < 0 ||
                Time.unscaledTime < _outgoingMagicBufferClearAt) return;
            if (_magicLayerIndex >= 0 && animationDriver.IsInTransition(_magicLayerIndex)) return;
            int expected = _activeMagicBuffer == 0
                ? EarthCastStateHash
                : EarthCastBStateHash;
            if (_magicLayerIndex >= 0 &&
                animationDriver.GetCurrentAnimatorStateInfo(_magicLayerIndex).fullPathHash != expected)
                return;
            int[] outgoingWeights = _outgoingMagicBuffer == 0
                ? EarthPoseAWeightHashes
                : EarthPoseBWeightHashes;
            for (int index = 0; index < outgoingWeights.Length; index++)
                animationDriver.SetFloat(outgoingWeights[index], 0f);
            _outgoingMagicBuffer = -1;
        }

        private bool IsActiveMagicBufferRendered()
        {
            if (_activeMagicBuffer < 0 || _magicLayerIndex < 0 ||
                Time.unscaledTime + .0001f < _activeMagicBufferVisibleAt ||
                animationDriver.IsInTransition(_magicLayerIndex)) return false;
            int expected = _activeMagicBuffer == 0
                ? EarthCastStateHash
                : EarthCastBStateHash;
            return animationDriver.GetCurrentAnimatorStateInfo(_magicLayerIndex).fullPathHash == expected;
        }

        private static int[] CreateMagicBufferHashes(string prefix)
        {
            var hashes = new int[11];
            for (int index = 0; index < hashes.Length; index++)
                hashes[index] = Animator.StringToHash($"{prefix}{index + 1:00}");
            return hashes;
        }

        private void UpdateLandingEvidence(float verticalSpeed, float signedForwardSpeed)
        {
            Vector3 position = rootBody.position;
            float heightDelta = _hasLandingPosition
                ? Vector3.Dot(position - _previousLandingPosition, motor.LocalUp)
                : 0f;
            _previousLandingPosition = position;
            _hasLandingPosition = true;
            bool supported = motor.HasStableSupport;
            if (motor.LastCommand.JumpPressed) _jumpIntentUntil = Time.time + 0.20f;
            if (!supported)
            {
                if (_wasLandingSupported)
                {
                    _airborneHeight = 0f;
                    _airbornePeakHeight = 0f;
                    _landingImpactSpeed = 0f;
                    LandingAirborneSeconds = 0f;
                    _deliberateJump = verticalSpeed > 0.75f && Time.time <= _jumpIntentUntil;
                    _signedTakeoffSpeed = signedForwardSpeed;
                    _lastAirForwardSpeed = signedForwardSpeed;
                    _externalAirDeltaSpeed = 0f;
                    _landingBackwards = false;
                }
                if (Mathf.Abs(signedForwardSpeed) > 0.25f)
                    _lastAirForwardSpeed = signedForwardSpeed;
                // Observe velocity only once per physics sample. Remove ordinary
                // gravity and ignore upward jump impulse / tangential braking.
                // Ground snap and collision deceleration at contact are never sampled.
                float fixedDelta = Time.fixedTime - _previousEvidenceFixedTime;
                if (_hasObservedLandingSupport && !_wasLandingSupported && _previousEvidenceFixedTime >= 0f &&
                    fixedDelta > 0f && fixedDelta <= 0.10f)
                {
                    Vector3 extra = rootBody.linearVelocity - _previousEvidenceVelocity -
                                    motor.GravityAcceleration * fixedDelta;
                    Vector3 tangent = Vector3.ProjectOnPlane(rootBody.linearVelocity, motor.LocalUp);
                    float tangentialGain = tangent.sqrMagnitude > 0.01f
                        ? Mathf.Max(0f, Vector3.Dot(extra, tangent.normalized)) : 0f;
                    float downwardGain = Mathf.Max(0f, -Vector3.Dot(extra, motor.LocalUp));
                    _externalAirDeltaSpeed = Mathf.Max(_externalAirDeltaSpeed,
                        Mathf.Max(tangentialGain, downwardGain));
                }
                LandingAirborneSeconds += Time.deltaTime;
                _airborneHeight += heightDelta;
                _airbornePeakHeight = Mathf.Max(_airbornePeakHeight, _airborneHeight);
                LandingDropHeight = Mathf.Max(0f, _airbornePeakHeight - _airborneHeight);
                _landingImpactSpeed = Mathf.Max(_landingImpactSpeed, -verticalSpeed);
            }
            else if (!_hasObservedLandingSupport)
            {
                // Loading a level is not a jump. Do not play a landing one-shot
                // when the motor acquires its first real support contact.
                LandingAirborneSeconds = 0f;
                LandingDropHeight = 0f;
                _landingImpactSpeed = 0f;
                LandingRollAllowed = false;
                _landingBackwards = false;
                _rescueState = new EarthAnimationRescueState
                {
                    Phase = EarthAnimationPhase.GroundedIdle
                };
                animationDriver.Play(LocomotionStateHash, 0, 0f);
                _activeBaseStateHash = LocomotionStateHash;
                _activeMotionState = EarthMotionStateId.Locomotion;
            }
            else if (!_wasLandingSupported)
            {
                _airborneHeight += heightDelta;
                LandingDropHeight = Mathf.Max(0f, _airbornePeakHeight - _airborneHeight);
            }
            if (_previousEvidenceFixedTime != Time.fixedTime)
            {
                _previousEvidenceVelocity = rootBody.linearVelocity;
                _previousEvidenceFixedTime = Time.fixedTime;
            }
            LandingRollAllowed = EarthLandingRollPolicy.AllowsRoll(
                _hasObservedLandingSupport, LandingAirborneSeconds, LandingDropHeight,
                _deliberateJump, _signedTakeoffSpeed, _externalAirDeltaSpeed) &&
                // The reversed forward-roll asset failed visual validation.
                // Backward landings use the ordinary landing/brace until a real
                // authored backward roll is supplied; never twist through that asset.
                _lastAirForwardSpeed >= -0.25f;
            // Prediction is visual only; confirmed roll eligibility/travel belongs
            // to the fixed-clock motor, not to a rendered frame or animation event.
            if (supported) LandingRollAllowed = motor.LastLandingWasRoll;
            if (supported) _hasObservedLandingSupport = true;
            _wasLandingSupported = supported;
        }

        private bool PresentMantle()
        {
            if (!motor.IsMantling)
            {
                if (_wasMantling)
                {
                    _wasMantling = false; _mantleHandWeight = 0f;
                    _mantleAwaitingGroundedExit = true;
                    poseController?.SetPresentationSuppressed(true);
                    animationRigBridge?.ResetMagicIk();
                    _activeBaseStateHash = 0;
                    _hasPreviousFacing = false;
                    footContactController?.InvalidateBasePose();
                }
                return false;
            }
            if (!_wasMantling || _mantleSequence != motor.MantleSequence)
            {
                poseController?.CancelPresentationForAnimationOwnership();
                ResetMagicIK();
                _impactUntil = 0f; _impactWeight = 0f;
                animationDriver.ResetTrigger(ImpactHash);
                if (_impactLayerIndex >= 0) animationDriver.SetLayerWeight(_impactLayerIndex, 0f);
                _wasMantling = true; _mantleSequence = motor.MantleSequence;
                animationDriver.CrossFadeInFixedTime(MantleStateHash, 0.10f, 0, 0f);
                transitionDirector?.SynchronizeState(EarthMotionStateId.Mantle, MantleStateHash,
                    EarthAnimationTransitionPriority.DefensiveCancel);
                footContactController?.InvalidateBasePose();
            }
            _activeBaseStateHash = MantleStateHash;
            _activeMotionState = EarthMotionStateId.Mantle;
            CurrentAuthoredAction = EarthAuthoredActionId.Mantle;
            // Motor support remains authoritative, including during Settle. The
            // feet replant after actual destination grounding, not a clock label.
            bool mantleContact = motor.MantlePhase == EarthMantlePhase.Settle && motor.HasStableSupport;
            CurrentFootPolicy = mantleContact
                ? EarthAuthoredFootPolicy.DefaultContact : EarthAuthoredFootPolicy.FlightIkOff;
            footContactController?.SetAuthoredFootPolicy(CurrentFootPolicy);
            animationDriver.SetLandingPoseWeight(1f);
            animationDriver.SetFloat(MantleTimeHash, motor.MantleProgress);
            animationDriver.SetBool(GroundedHash, mantleContact);
            animationDriver.SetBool(CastHash, false);
            if (_magicLayerIndex >= 0) animationDriver.SetLayerWeight(_magicLayerIndex, 0f);
            Vector3 up = motor.LocalUp;
            Vector3 facing = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            Vector3 right = Vector3.Cross(up, facing).normalized;
            float progress = motor.MantleProgress;
            _mantleHandWeight = Mathf.SmoothStep(0f, 1f, progress / 0.15f) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.60f, 0.85f, progress)));
            if (leftHandTarget != null) leftHandTarget.SetPositionAndRotation(
                motor.MantleLedgePoint - right * 0.18f + up * 0.025f, Quaternion.LookRotation(facing, up));
            if (rightHandTarget != null) rightHandTarget.SetPositionAndRotation(
                motor.MantleLedgePoint + right * 0.18f + up * 0.025f, Quaternion.LookRotation(facing, up));
            // Mantle contacts are authored against a world-space physical lip.
            // Mecanim's Humanoid IK reaches that contact more consistently than
            // the additive magic rig, whose arm hints are tuned for casting.
            // Give the mantle one arm owner and restore the optional rig after it.
            animationRigBridge?.ResetMagicIk();
            UpdateImpactPresentation();
            return true;
        }

        private void CompleteMantleGroundedExit()
        {
            _mantleAwaitingGroundedExit = false;
            // The motor can finish its path one render frame before support is
            // reacquired. Leave the protected mantle pose only at that real
            // support boundary, then give idle a reachable leg chain to replant.
            animationDriver.CrossFadeInFixedTime(LocomotionStateHash, 0.12f, 0, 0f);
            _activeBaseStateHash = LocomotionStateHash;
            _activeMotionState = EarthMotionStateId.Locomotion;
            transitionDirector?.SynchronizeState(
                EarthMotionStateId.Locomotion,
                LocomotionStateHash,
                EarthAnimationTransitionPriority.Idle);
            footContactController?.InvalidateBasePose();
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
            // A near-floor spawn gets a short presentation-only initialization
            // window. Elevated spawns still become visibly airborne after 0.2 s
            // or 0.25 m of actual descent; no motor force is suppressed.
            if (!_hasObservedLandingSupport && _unsupportedSeconds < 0.20f &&
                LandingDropHeight < 0.25f && verticalSpeed <= 0.75f)
                return;
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
                    if (_activeMotionState != EarthMotionStateId.MovingLanding &&
                        rescue.LandingStyle == EarthLandingStyle.Moving)
                        _landingBackwards = _lastAirForwardSpeed < -0.25f;
                    // This reversed clip has no airborne lead. Start it only at
                    // confirmed contact, not at the forward clip's predicted lead.
                    if (_landingBackwards && rescue.Phase == EarthAnimationPhase.PreLanding)
                        return;
                    stateHash = rescue.LandingStyle switch
                    {
                        EarthLandingStyle.Hard => HardLandStateHash,
                        EarthLandingStyle.Moving => _landingBackwards
                            ? MovingLandBackStateHash : MovingLandStateHash,
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
            // The semantic recovery clock is intentionally short, but must not
            // cut an eligible authored roll off after only 0.08 seconds. Its
            // positive-clock controller exit owns completion. Real jumps/surf,
            // impacts and gameplay remain free to interrupt.
            if (destinationCategory == EarthMotionCategory.Locomotion ||
                destinationCategory == EarthMotionCategory.Turn)
            {
                AnimatorStateInfo rolling = animationDriver.GetCurrentAnimatorStateInfo(0);
                AnimatorStateInfo next = animationDriver.GetNextAnimatorStateInfo(0);
                // The controller owns the complete roll AND its outgoing blend.
                // Looking only at next state discarded this guard at blend start
                // and issued another CrossFade over the one already in progress.
                if (rolling.fullPathHash == MovingLandStateHash || rolling.fullPathHash == MovingLandBackStateHash ||
                    (animationDriver.IsInTransition(0) &&
                     (next.fullPathHash == MovingLandStateHash || next.fullPathHash == MovingLandBackStateHash)))
                    return;
                if (!animationDriver.IsInTransition(0) && rolling.fullPathHash == stateHash)
                {
                    _activeBaseStateHash = stateHash;
                    _activeMotionState = destinationState;
                    transitionDirector?.SynchronizeState(destinationState, stateHash, PriorityFor(destinationState));
                    return;
                }
            }
            AnimatorStateInfo current = animationDriver.GetCurrentAnimatorStateInfo(0);
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
                destinationState == EarthMotionStateId.Locomotion);
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
            if (animator == null || animationDriver.IsInTransition(0)) return;
            AnimatorStateInfo state = animationDriver.GetCurrentAnimatorStateInfo(0);
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
            if (style == EarthLandingStyle.Moving && _landingBackwards) return 0f;
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
            // The reversed clip contains only the grounded roll segment, so it
            // owns foot contact from its first frame through its outgoing blend.
            if (CurrentAuthoredAction == EarthAuthoredActionId.MovingLandingRoll && _landingBackwards)
                CurrentFootPolicy = motor.HasStableSupport
                    ? EarthAuthoredFootPolicy.AuthoredContact : EarthAuthoredFootPolicy.FlightIkOff;
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
            AnimatorStateInfo current = animationDriver.GetCurrentAnimatorStateInfo(0);
            if (TryMapLandingState(current, out action))
            {
                normalizedTime = Mathf.Clamp01(current.normalizedTime);
                return true;
            }
            if (!animationDriver.IsInTransition(0)) return false;
            AnimatorStateInfo next = animationDriver.GetNextAnimatorStateInfo(0);
            if (!TryMapLandingState(next, out action)) return false;
            normalizedTime = Mathf.Clamp01(next.normalizedTime);
            return true;
        }

        private static bool TryMapLandingState(
            AnimatorStateInfo state,
            out EarthAuthoredActionId action)
        {
            if (state.fullPathHash == MovingLandStateHash || state.fullPathHash == MovingLandBackStateHash)
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
            AnimatorStateInfo state = animationDriver.IsInTransition(0)
                ? animationDriver.GetNextAnimatorStateInfo(0)
                : animationDriver.GetCurrentAnimatorStateInfo(0);
            return Mathf.Clamp01(state.normalizedTime);
        }

        public void NotifyImpactResponse(EarthCharacterImpactResponse response)
        {
            if (response != EarthCharacterImpactResponse.Flinch &&
                response != EarthCharacterImpactResponse.Stagger) return;
            float severity = response == EarthCharacterImpactResponse.Stagger ? 0.52f : 0.18f;
            var impactContext = new ImpactMotionContext(
                severity,
                new float3(0f, 0f, -1f),
                motor != null && motor.HasStableSupport,
                false);
            LastImpactMotionLane = ImpactMotionSelector.Select(in impactContext);
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
            if (!HasProceduralImpactOwner && animator != null && animator.enabled)
                animationDriver.SetTrigger(ImpactHash);
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
                           (animationDriver.GetBool(CastHash) || _castWeight > 0.05f);
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
            animationDriver.SetFloat(DodgeXHash, LastDodgeDecision.BlendDirection.x);
            animationDriver.SetFloat(DodgeYHash, LastDodgeDecision.BlendDirection.y);
            animationDriver.ResetTrigger(DodgeHash);
            animationDriver.SetTrigger(DodgeHash);
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
                AnimatorStateInfo current = animationDriver.GetCurrentAnimatorStateInfo(0);
                int recoveryStateHash = visibleRagdoll.LastRecoverySide == EarthRagdollRecoverySide.Back
                    ? KnockdownRecoveryBackStateHash
                    : KnockdownRecoveryStateHash;
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
                transitionDirector.RequestTransition(recoveryStateHash, in context);
                _activeBaseStateHash = recoveryStateHash;
            }
            else
            {
                _activeBaseStateHash = visibleRagdoll != null &&
                                       visibleRagdoll.LastRecoverySide == EarthRagdollRecoverySide.Back
                    ? KnockdownRecoveryBackStateHash
                    : KnockdownRecoveryStateHash;
            }
            _activeMotionState = EarthMotionStateId.KnockdownRecovery;
            CurrentAuthoredAction = EarthAuthoredActionId.RecoverableKnockdownRecovery;
            CurrentFootPolicy = EarthAuthoredActionCatalog.Resolve(CurrentAuthoredAction)
                .FootPolicyAt(0.18f);
            footContactController?.SetAuthoredFootPolicy(CurrentFootPolicy);
            poseController?.SetPresentationSuppressed(true);
            poseController?.CancelPresentationForAnimationOwnership();
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
            if (_impactLayerIndex >= 0) animationDriver.SetLayerWeight(_impactLayerIndex, _impactWeight);
        }

        private bool HasProceduralImpactOwner =>
            proceduralBodyResponse != null && proceduralBodyResponse.isActiveAndEnabled;

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;
            // Animation Rigging owns casting arms when available. Mantle disables
            // that rig in PresentMantle and deliberately uses Humanoid IK because
            // its targets are physical ledge contacts on the base layer.
            if (animationRigBridge != null && animationRigBridge.IsBuilt && !_wasMantling) return;
            int handLayer = _wasMantling ? 0 : _magicLayerIndex;
            if (handLayer >= 0 && layerIndex != handLayer) return;
            float handWeight = HandConstraintWeight;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, handWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, handWeight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, handWeight);
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
            if (_handIkState == HandIkState.Inactive || leftHandTarget == null || rightHandTarget == null)
            {
                EarthResponsiveHandTargetSolver.Reset(ref _responsiveHandTargetState);
                return;
            }

            bool hasLiveFocus = false;
            Vector3 focus = default;
            if (executor != null && executor.IsGravityWellActive)
            {
                focus = executor.GravityWellFocus;
                hasLiveFocus = true;
            }
            else if (executor != null && executor.IsVectorFieldActive)
            {
                focus = executor.VectorFieldPoint;
                hasLiveFocus = true;
            }
            else if (executor != null && executor.HeldBody != null)
            {
                focus = executor.HeldBody.worldCenterOfMass;
                hasLiveFocus = true;
            }
            // A one-shot retains the authored arm pose. During sustained release,
            // keep the last body-relative target while the rig weight fades.
            if (!hasLiveFocus && !_responsiveHandTargetState.IsInitialized) return;

            // Telekinesis points can be many metres away. They define gaze/aim,
            // not a literal wrist destination. Feeding the distant point directly
            // into TwoBoneIK fully stretched both arms and flipped the elbows.
            Transform chest = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Chest)
                : null;
            Vector3 shoulderCenter = chest != null
                ? chest.position
                : transform.position + motor.LocalUp * 0.66f;
            EarthResponsiveHandTargetSample target;
            if (hasLiveFocus)
            {
                Vector3 desiredAim = focus - shoulderCenter;
                if (desiredAim.sqrMagnitude < 0.001f) desiredAim = transform.forward;
                EarthCastPhase castPhase = poseController != null
                    ? poseController.CurrentRequest.Phase
                    : EarthCastPhase.Sustain;
                float effort = poseController != null
                    ? poseController.CurrentRequest.Effort01
                    : _castWeight;
                Vector3 localAim = transform.InverseTransformDirection(desiredAim);
                EarthMagicReachSample reach = EarthMagicReachSolver.Resolve(
                    new float3(localAim.x, localAim.y, localAim.z),
                    castPhase,
                    effort);
                target = EarthResponsiveHandTargetSolver.Step(
                    ref _responsiveHandTargetState,
                    reach.LocalAim,
                    reach.ReachMeters,
                    reach.HandSpreadMeters,
                    true,
                    Time.deltaTime);
            }
            else
            {
                target = EarthResponsiveHandTargetSolver.Step(
                    ref _responsiveHandTargetState, default, 0f, 0f, false, Time.deltaTime);
            }

            Vector3 aimDirection = transform.TransformDirection(new Vector3(
                target.LocalAim.x, target.LocalAim.y, target.LocalAim.z)).normalized;
            Vector3 reachableFocus = shoulderCenter + aimDirection * target.ReachMeters;
            Vector3 across = Vector3.Cross(motor.LocalUp, aimDirection).normalized;
            if (across.sqrMagnitude < 0.1f) across = transform.right;
            leftHandTarget.position = reachableFocus - across * target.HandSpreadMeters;
            rightHandTarget.position = reachableFocus + across * target.HandSpreadMeters;
            Quaternion rotation = Quaternion.LookRotation(aimDirection, motor.LocalUp);
            leftHandTarget.rotation = rotation;
            rightHandTarget.rotation = rotation;
        }

        public float HandConstraintWeight
        {
            get
            {
                if (_wasMantling) return _mantleHandWeight;
                return _magicHandConstraintWeight;
            }
        }

        public bool HasResponsiveSustainedAim =>
            _responsiveHandTargetState.IsInitialized && _magicHandConstraintWeight > 0.001f;
        public float3 ResponsiveSustainedLocalAim => _responsiveHandTargetState.LocalAim;
        public float ResponsiveSustainedAimWeight => _magicHandConstraintWeight;

        private float ResolveMagicHandConstraintTarget()
        {
            bool sustainedAim = executor != null &&
                                (executor.HeldBody != null ||
                                 executor.IsGravityWellActive ||
                                 executor.IsVectorFieldActive);
            // One-shot events must pass the pose arbiter's readable-contact
            // barrier. A live held field has no authoritative event sequence,
            // so its own rendered A/B buffer contact is the authority instead.
            bool renderedContact = poseController == null ||
                                   !poseController.HasAuthoritativePresentation ||
                                   poseController.RenderedContactReached;
            if (_activeMagicMotion != null)
                renderedContact &= _magicClipClock.NormalizedTime + .0005f >=
                                   _activeMagicMotion.timing.Contact &&
                                   IsActiveMagicBufferRendered();
            float influence = _activeMagicMotion != null
                ? _activeMagicMotion.sustainedHandInfluence
                : .48f;
            return ResolveHandConstraintTarget(
                sustainedAim, renderedContact, _castWeight, influence);
        }

        public static float ResolveHandConstraintTarget(
            bool sustainedAim,
            bool renderedContact,
            float layerWeight,
            float sustainedInfluence) => sustainedAim && renderedContact
            ? Mathf.Clamp01(layerWeight) * Mathf.Clamp01(sustainedInfluence)
            : 0f;

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

        private void HandleVisibleRagdollBegan()
        {
            // Handoff can happen after Update. Reject late magic in this same
            // frame even though the ragdoll has already disabled the Animator.
            poseController?.SetPresentationSuppressed(true);
            ResetMagicIK();
        }

        private void HandlePhysicalState(CharacterPhysicalState state)
        {
            _physicalMode = state.Mode;
            if (state.Mode == CharacterPhysicalMode.FullRagdoll)
            {
                _dodgeUntil = 0f;
                _dodgeWasActive = false;
                poseController?.SetPresentationSuppressed(true);
                poseController?.CancelPresentationForAnimationOwnership();
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
            _magicClipClock = default;
            _activeMagicBuffer = -1;
            _outgoingMagicBuffer = -1;
            _activeMagicBufferCastKind = 0;
            _activeMagicBufferSequence = 0u;
            _activeMagicBufferVisibleAt = 0f;
            _outgoingMagicBufferClearAt = 0f;
            _hasPendingRenderedMagicSample = false;
            _pendingRenderedMagicSequence = 0u;
            _pendingRenderedMagicTime = 0f;
            _pendingRenderedMagicContact = 0f;
            _pendingRenderedMagicRecovery = 0f;
            _castWeight = 0f;
            _magicHandConstraintWeight = 0f;
            EarthResponsiveHandTargetSolver.Reset(ref _responsiveHandTargetState);
            _handIkState = HandIkState.Inactive;
            animationRigBridge?.ResetMagicIk();
            if (animator == null) return;
            if (EnsureAnimationDriver(false))
            {
                animationDriver.SetBool(CastHash, false);
                animationDriver.SetInteger(CastKindHash, 0);
                animationDriver.SetFloat(EarthPoseHash, 0f);
                if (_magicLayerIndex >= 0) animationDriver.SetLayerWeight(_magicLayerIndex, 0f);
                for (int index = 0; index < EarthPoseWeightHashes.Length; index++)
                {
                    animationDriver.SetFloat(EarthPoseWeightHashes[index], 0f);
                    animationDriver.SetFloat(EarthPoseAWeightHashes[index], 0f);
                    animationDriver.SetFloat(EarthPoseBWeightHashes[index], 0f);
                }
                animationDriver.SetFloat(MotionTimeAHash, 0f);
                animationDriver.SetFloat(MotionTimeBHash, 0f);
            }
            Transform left = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            Transform right = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (left != null && leftHandTarget != null)
                leftHandTarget.SetPositionAndRotation(left.position, left.rotation);
            if (right != null && rightHandTarget != null)
                rightHandTarget.SetPositionAndRotation(right.position, right.rotation);
        }

        private bool EnsureAnimationDriver(bool allowCreate)
        {
            if (animator == null) return false;
            if (animationDriver == null) animationDriver = GetComponent<EarthAnimationDriver>();
            if (animationDriver == null && allowCreate)
                animationDriver = gameObject.AddComponent<EarthAnimationDriver>();
            if (animationDriver == null) return false;
            if (animationDriver.Animator != animator) animationDriver.Configure(animator);
            return animationDriver.IsUsable;
        }
    }
}
