using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Presentation.MotionMatching;
using Elemental.Simulation.Characters;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class EarthFootContactController : MonoBehaviour
    {
        private const int FootHitCapacity = 8;
        private const float KneeHintResponseSeconds = 0.085f;
        private const float MaximumKneeHintStepDegreesAt60Hz = 6f;
        private static readonly ProfilerMarker ContactMarker =
            new ProfilerMarker("Elemental.Character.FootContact");
        private static readonly int LeftFootContactHash = Animator.StringToHash(
            EarthAnimationClipMetadata.LeftFootContact);
        private static readonly int RightFootContactHash = Animator.StringToHash(
            EarthAnimationClipMetadata.RightFootContact);
        private static readonly int LeftFootPhaseHash = Animator.StringToHash(
            EarthAnimationClipMetadata.LeftFootPhase);
        private static readonly int RightFootPhaseHash = Animator.StringToHash(
            EarthAnimationClipMetadata.RightFootPhase);

        [SerializeField] private Animator animator;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private Rigidbody rootBody;
        [SerializeField] private EarthSurfController surfController;
        [SerializeField] private EarthCharacterPoseController poseIntentSource;
        [SerializeField, Min(0.01f)] private float footProbeLift = 0.42f;
        [SerializeField, Min(0.05f)] private float footProbeDistance = 0.95f;
        [SerializeField, Min(0f)] private float soleOffset = 0.035f;
        [SerializeField, Min(0f)] private float maximumPelvisDrop = 0.22f;
        [SerializeField, Range(0f, 0.08f)] private float supportSwapHysteresis = 0.035f;
        [SerializeField, Range(0.02f, 0.4f)] private float contactCaptureSeconds = 0.10f;
        private EarthAnimationDriver _animationDriver;
        private EAMMBasePoseBridge _basePoseBridge;
        private int _lastContactFrame = -1;
        private Vector3 _leftHintWorld, _rightHintWorld;
        public int LastContactEvaluationFrame => _lastContactFrame;

        private readonly RaycastHit[] _leftHits = new RaycastHit[FootHitCapacity];
        private readonly RaycastHit[] _rightHits = new RaycastHit[FootHitCapacity];
        private readonly CharacterSupportCandidate[] _leftSupportCandidates =
            new CharacterSupportCandidate[FootHitCapacity];
        private readonly CharacterSupportCandidate[] _rightSupportCandidates =
            new CharacterSupportCandidate[FootHitCapacity];
        private readonly int[] _leftSupportHitIndices = new int[FootHitCapacity];
        private readonly int[] _rightSupportHitIndices = new int[FootHitCapacity];
        private Transform _leftFoot;
        private Transform _rightFoot;
        private Transform _leftUpperLeg;
        private Transform _rightUpperLeg;
        private EarthFootContactState _leftState;
        private EarthFootContactState _rightState;
        private EarthFootContactDecision _leftDecision;
        private EarthFootContactDecision _rightDecision;
        private float _leftAppliedWeight;
        private float _rightAppliedWeight;
        private float3 _leftKneeDirection;
        private float3 _rightKneeDirection;
        private float _pelvisOffset;
        private float _pelvisVelocity;
        private Vector3 _previousPelvisBaseWorld;
        private bool _hasPreviousPelvisBaseWorld;
        private float _pelvisResponseSeconds = 0.085f;
        private float _pelvisMaximumSpeed = 0.8f;
        private Vector3 _previousLeftAnimated;
        private Vector3 _previousRightAnimated;
        private bool _hasPreviousAnimatedFeet;
        private Vector3 _leftTargetWorld;
        private Vector3 _rightTargetWorld;
        private Vector3 _leftNormalWorld = Vector3.up;
        private Vector3 _rightNormalWorld = Vector3.up;
        private SupportFrameSnapshot _leftContactSupport;
        private SupportFrameSnapshot _rightContactSupport;
        private Collider _leftSupportCollider;
        private Collider _rightSupportCollider;
        private CharacterSupportSelection _leftSupportSelection;
        private CharacterSupportSelection _rightSupportSelection;
        private bool _poseLocked;
        private EarthAuthoredFootPolicy _authoredFootPolicy;
        private float _turnIntent;
        private Transform _leftLowerLeg;
        private Transform _rightLowerLeg;
        private Vector3 _leftRawContactWorld;
        private Vector3 _rightRawContactWorld;
        private Vector3 _leftRawNormalWorld = Vector3.up;
        private Vector3 _rightRawNormalWorld = Vector3.up;
        private Vector3 _leftActualWorld;
        private Vector3 _rightActualWorld;
        private Quaternion _leftActualRotation = Quaternion.identity;
        private Quaternion _rightActualRotation = Quaternion.identity;
        private Vector3 _leftActualSupportLocal;
        private Vector3 _rightActualSupportLocal;
        private float _gaitPhase;
        private float _leftGaitPhase;
        private float _rightGaitPhase = 0.5f;
        private bool _hasClipContactMetadata;
        private bool _hasBasePoseContactMetadata;
        private float _basePoseLeftPhase;
        private float _basePoseRightPhase = 0.5f;
        private float _basePoseLeftContact;
        private float _basePoseRightContact;
        private bool _locomoting;
        private bool _pivotingInPlace;
        private bool _surfing;

        public float FootIkWeight => (_leftAppliedWeight + _rightAppliedWeight) * 0.5f;
        public float LeftFootIkWeight => _leftAppliedWeight;
        public float RightFootIkWeight => _rightAppliedWeight;
        public bool FeetLocked => _poseLocked && _leftDecision.Locked && _rightDecision.Locked;
        public bool LeftFootLocked => _leftDecision.Locked;
        public bool RightFootLocked => _rightDecision.Locked;
        public EarthFootContactReason LeftReason => _leftDecision.Reason;
        public EarthFootContactReason RightReason => _rightDecision.Reason;
        public EarthFootPlantState LeftPlantState => _leftDecision.PlantState;
        public EarthFootPlantState RightPlantState => _rightDecision.PlantState;
        public CharacterSupportKind LeftSupportKind => _leftSupportSelection.HasSupport
            ? _leftSupportSelection.Candidate.Kind
            : CharacterSupportKind.Unknown;
        public CharacterSupportKind RightSupportKind => _rightSupportSelection.HasSupport
            ? _rightSupportSelection.Candidate.Kind
            : CharacterSupportKind.Unknown;
        public float LeftAnchorErrorMeters { get; private set; }
        public float RightAnchorErrorMeters { get; private set; }
        public float LeftSoleClearance { get; private set; }
        public float RightSoleClearance { get; private set; }
        public float PelvisCorrectionMeters => _pelvisOffset;
        public float PelvisCorrectionVelocity => _pelvisVelocity;
        public Vector3 LeftTargetWorld => _leftTargetWorld;
        public Vector3 RightTargetWorld => _rightTargetWorld;
        public uint LeftSupportId => _leftContactSupport.SurfaceId;
        public uint RightSupportId => _rightContactSupport.SurfaceId;
        public uint LeftSupportGeneration => _leftContactSupport.Generation;
        public uint RightSupportGeneration => _rightContactSupport.Generation;
        public float LeftReleaseCooldownSeconds => _leftDecision.ReleaseCooldownSeconds;
        public float RightReleaseCooldownSeconds => _rightDecision.ReleaseCooldownSeconds;
        public float LeftGaitPhase01 => _leftGaitPhase;
        public float RightGaitPhase01 => _rightGaitPhase;
        public Vector3 LeftRawContactPointWorld => _leftRawContactWorld;
        public Vector3 RightRawContactPointWorld => _rightRawContactWorld;
        public Vector3 LeftRawContactNormalWorld => _leftRawNormalWorld;
        public Vector3 RightRawContactNormalWorld => _rightRawNormalWorld;
        public Vector3 LeftFilteredContactPointWorld => _leftTargetWorld;
        public Vector3 RightFilteredContactPointWorld => _rightTargetWorld;
        public Vector3 LeftFilteredContactNormalWorld => _leftNormalWorld;
        public Vector3 RightFilteredContactNormalWorld => _rightNormalWorld;
        public Vector3 LeftSupportLocalAnchor => ToVector3(_leftDecision.TargetLocal);
        public Vector3 RightSupportLocalAnchor => ToVector3(_rightDecision.TargetLocal);
        public Vector3 LeftActualSupportLocal => _leftActualSupportLocal;
        public Vector3 RightActualSupportLocal => _rightActualSupportLocal;
        public Vector3 LeftActualFootWorld => _leftActualWorld;
        public Vector3 RightActualFootWorld => _rightActualWorld;
        public Quaternion LeftActualFootRotation => _leftActualRotation;
        public Quaternion RightActualFootRotation => _rightActualRotation;
        public bool LeftHasContact => _leftSupportCollider != null;
        public bool RightHasContact => _rightSupportCollider != null;
        public float PelvisOffsetMeters => _pelvisOffset;
        public float LeftPelvisRequestMeters { get; private set; }
        public float RightPelvisRequestMeters { get; private set; }
        public float PelvisTargetMeters { get; private set; }
        public bool IsLocomoting => _locomoting;
        public bool IsPivotingInPlace => _pivotingInPlace;
        public bool IsSurfing => _surfing;
        public EarthAuthoredFootPolicy CurrentFootPolicy => _authoredFootPolicy;
        public float LeftKneeAngleDegrees => ResolveJointAngle(
            _leftUpperLeg,
            _leftLowerLeg,
            _leftFoot);
        public float RightKneeAngleDegrees => ResolveJointAngle(
            _rightUpperLeg,
            _rightLowerLeg,
            _rightFoot);
        public float LeftAnkleAngleDegrees => ResolveAnkleAngle(_leftFoot);
        public float RightAnkleAngleDegrees => ResolveAnkleAngle(_rightFoot);
        public Vector3 LeftKneeDirectionWorld => ResolveKneeDirection(
            _leftUpperLeg,
            _leftLowerLeg);
        public Vector3 RightKneeDirectionWorld => ResolveKneeDirection(
            _rightUpperLeg,
            _rightLowerLeg);
        public Vector3 LeftKneeHintDirectionWorld => ToVector3(_leftKneeDirection);
        public Vector3 RightKneeHintDirectionWorld => ToVector3(_rightKneeDirection);

        public void Configure(
            Animator configuredAnimator,
            PlanetMotor configuredMotor,
            Rigidbody configuredRootBody,
            EarthCharacterPoseController configuredPoseIntentSource = null)
        {
            animator = configuredAnimator;
            motor = configuredMotor;
            rootBody = configuredRootBody;
            poseIntentSource = configuredPoseIntentSource;
            if (surfController == null) surfController = GetComponentInParent<EarthSurfController>();
            ResolveBones();
            ResolveMetadataAvailability();
        }

        public void SetPoseIntentSource(EarthCharacterPoseController source) =>
            poseIntentSource = source;

        public void SetAuthoredFootPolicy(EarthAuthoredFootPolicy policy) =>
            _authoredFootPolicy = policy;

        public void SetTurnIntent(float turn) =>
            _turnIntent = Mathf.Clamp(turn, -1f, 1f);

        public void SetBasePoseContactMetadata(
            float leftPhase,
            float rightPhase,
            bool leftContact,
            bool rightContact)
        {
            _hasBasePoseContactMetadata = true;
            _basePoseLeftPhase = Mathf.Repeat(leftPhase, 1f);
            _basePoseRightPhase = Mathf.Repeat(rightPhase, 1f);
            _basePoseLeftContact = leftContact ? 1f : 0f;
            _basePoseRightContact = rightContact ? 1f : 0f;
        }

        public void ClearBasePoseContactMetadata() =>
            _hasBasePoseContactMetadata = false;

        public void ConfigureAnimationRescue(float pelvisResponseSeconds, float pelvisMaximumSpeed)
        {
            _pelvisResponseSeconds = Mathf.Clamp(pelvisResponseSeconds, 0.02f, 0.25f);
            _pelvisMaximumSpeed = Mathf.Clamp(pelvisMaximumSpeed, 0.2f, 1.5f);
        }

        public void InvalidateBasePose()
        {
            _lastContactFrame = -1;
            _hasBasePoseContactMetadata = false;
            _leftState = default;
            _rightState = default;
            _leftDecision = default;
            _rightDecision = default;
            _leftAppliedWeight = 0f;
            _rightAppliedWeight = 0f;
            _pelvisOffset = 0f;
            _pelvisVelocity = 0f;
            _hasPreviousPelvisBaseWorld = false;
            LeftPelvisRequestMeters = 0f;
            RightPelvisRequestMeters = 0f;
            PelvisTargetMeters = 0f;
            _poseLocked = false;
            _hasPreviousAnimatedFeet = false;
            _leftSupportCollider = null;
            _rightSupportCollider = null;
            _leftSupportSelection = CharacterSupportSelection.None;
            _rightSupportSelection = CharacterSupportSelection.None;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (motor == null) motor = GetComponentInParent<PlanetMotor>();
            if (rootBody == null) rootBody = GetComponentInParent<Rigidbody>();
            if (surfController == null) surfController = GetComponentInParent<EarthSurfController>();
            ResolveBones();
            ResolveMetadataAvailability();
        }

        private void OnDisable()
        {
            _lastContactFrame = -1;
            _hasBasePoseContactMetadata = false;
            _leftState = default;
            _rightState = default;
            _leftDecision = default;
            _rightDecision = default;
            _leftAppliedWeight = 0f;
            _rightAppliedWeight = 0f;
            _pelvisOffset = 0f;
            _pelvisVelocity = 0f;
            _hasPreviousPelvisBaseWorld = false;
            _hasPreviousAnimatedFeet = false;
            _leftSupportCollider = null;
            _rightSupportCollider = null;
            _leftSupportSelection = CharacterSupportSelection.None;
            _rightSupportSelection = CharacterSupportSelection.None;
            _authoredFootPolicy = EarthAuthoredFootPolicy.DefaultContact;
            _turnIntent = 0f;
            _gaitPhase = 0f;
            _leftGaitPhase = 0f;
            _rightGaitPhase = 0.5f;
            _locomoting = false;
            _pivotingInPlace = false;
            _surfing = false;
        }

        private void LateUpdate()
        {
            if (animator == null || !animator.enabled || !animator.isHuman ||
                _leftFoot == null || _rightFoot == null)
            {
                return;
            }
            // Observation only. Moving or rotating an ankle after Humanoid IK
            // disconnects it from the already-solved knee and stretches the shin.
            // Smooth contact targets/weights before IK, never the resulting bones.
            _leftActualWorld = _leftFoot.position;
            _rightActualWorld = _rightFoot.position;
            _leftActualRotation = _leftFoot.rotation;
            _rightActualRotation = _rightFoot.rotation;
            // These metrics describe the final rendered chain, after the graph
            // and final OnAnimatorIK contact pass, rather than the pre-IK pose.
            LeftAnchorErrorMeters = _leftDecision.Locked
                ? Vector3.Distance(_leftTargetWorld, _leftActualWorld) : 0f;
            RightAnchorErrorMeters = _rightDecision.Locked
                ? Vector3.Distance(_rightTargetWorld, _rightActualWorld) : 0f;
            _leftActualSupportLocal = ToVector3(EarthSupportFootLockSolver.CaptureLocal(
                ToFloat3(_leftActualWorld),
                in _leftContactSupport));
            _rightActualSupportLocal = ToVector3(EarthSupportFootLockSolver.CaptureLocal(
                ToFloat3(_rightActualWorld),
                in _rightContactSupport));
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || animator == null || motor == null ||
                _leftFoot == null || _rightFoot == null) return;
            if (_animationDriver == null) _animationDriver = GetComponent<EarthAnimationDriver>();
            if (_basePoseBridge == null) _basePoseBridge = GetComponent<EAMMBasePoseBridge>();
            // The landing mixer has two controller inputs. A second IK callback
            // must not advance contact ramps and anchor history twice this frame.
            if (_lastContactFrame == Time.frameCount)
            {
                ApplyFoot(AvatarIKGoal.LeftFoot, _leftTargetWorld, _leftNormalWorld, _leftAppliedWeight);
                ApplyFoot(AvatarIKGoal.RightFoot, _rightTargetWorld, _rightNormalWorld, _rightAppliedWeight);
                // Reuse the computed hints. A second mixer input must not step
                // the knee filter, pelvis spring or contact state a second time.
                ApplyCachedKneeHints();
                return;
            }
            _lastContactFrame = Time.frameCount;
            using (ContactMarker.Auto()) EvaluateFootContacts();
            if (Mathf.Max(_leftAppliedWeight, _rightAppliedWeight) > .001f)
                _animationDriver?.RecordFinalContactPass();
        }

        private void EvaluateFootContacts()
        {
            float deltaTime = Mathf.Max(0.0001f, Time.deltaTime);
            Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f
                ? motor.LocalUp.normalized
                : transform.up;
            bool supported = motor.HasStableSupport;
            bool surfLock = surfController != null && surfController.IsActive;
            bool authoredFlight = _authoredFootPolicy == EarthAuthoredFootPolicy.FlightIkOff;
            bool authoredContact = _authoredFootPolicy == EarthAuthoredFootPolicy.AuthoredContact;
            bool authoredBrace = _authoredFootPolicy == EarthAuthoredFootPolicy.BraceBoth;
            bool contactSupported = supported && !authoredFlight && !authoredContact;
            float3 supportVelocity = motor.CurrentSupportFrame.IsValid
                ? motor.CurrentSupportFrame.ContactPointVelocity : float3.zero;
            float tangentSpeed = rootBody != null
                ? Vector3.ProjectOnPlane(rootBody.linearVelocity - ToVector3(supportVelocity), up).magnitude
                : 0f;
            float2 moveInput = motor.LastCommand.Move;
            bool pivotingInPlace = supported && !surfLock &&
                                   (Mathf.Abs(moveInput.x) >= 0.05f ||
                                    Mathf.Abs(_turnIntent) >= 0.20f) &&
                                   Mathf.Abs(moveInput.y) < 0.12f &&
                                   tangentSpeed < 0.45f;
            EarthPoseIntent poseIntent = poseIntentSource != null
                ? poseIntentSource.CurrentIntent
                : default;
            bool requestLock = EarthFootPlantMotionGate.ShouldLock(
                contactSupported,
                surfLock,
                poseIntent.LocksFeet,
                poseIntent.Brace01,
                tangentSpeed,
                moveInput);
            if (authoredBrace && supported) requestLock = true;
            // A released bot/player command can leave real support-relative
            // velocity for several frames. Keep alternating stance ownership
            // during that coast instead of dropping into the no-lock idle path.
            bool locomoting = EarthFootPlantMotionGate.IsLocomoting(
                moveInput,
                tangentSpeed) || pivotingInPlace;
            locomoting = locomoting && !surfLock &&
                _authoredFootPolicy == EarthAuthoredFootPolicy.DefaultContact;
            _poseLocked = requestLock;
            _locomoting = locomoting;
            _pivotingInPlace = pivotingInPlace;
            _surfing = surfLock;

            FootProbe leftProbe = ProbeFoot(
                _leftFoot,
                _leftHits,
                _leftSupportCandidates,
                _leftSupportHitIndices,
                -1f,
                up,
                in _leftState,
                in _leftContactSupport,
                in _leftSupportSelection);
            FootProbe rightProbe = ProbeFoot(
                _rightFoot,
                _rightHits,
                _rightSupportCandidates,
                _rightSupportHitIndices,
                1f,
                up,
                in _rightState,
                in _rightContactSupport,
                in _rightSupportSelection);
            _leftSupportCollider = leftProbe.SupportCollider;
            _rightSupportCollider = rightProbe.SupportCollider;
            _leftSupportSelection = leftProbe.SupportSelection;
            _rightSupportSelection = rightProbe.SupportSelection;
            _leftContactSupport = ResolvePresentationSupport(in leftProbe, up);
            _rightContactSupport = ResolvePresentationSupport(in rightProbe, up);
            _leftRawContactWorld = leftProbe.ContactPoint;
            _rightRawContactWorld = rightProbe.ContactPoint;
            _leftRawNormalWorld = leftProbe.Normal;
            _rightRawNormalWorld = rightProbe.Normal;
            LeftSoleClearance = leftProbe.Clearance;
            RightSoleClearance = rightProbe.Clearance;

            float leftVerticalVelocity = 0f;
            float rightVerticalVelocity = 0f;
            if (_hasPreviousAnimatedFeet)
            {
                leftVerticalVelocity = Vector3.Dot(
                    leftProbe.AnimatedPosition - _previousLeftAnimated,
                    up) / deltaTime;
                rightVerticalVelocity = Vector3.Dot(
                    rightProbe.AnimatedPosition - _previousRightAnimated,
                    up) / deltaTime;
            }
            _previousLeftAnimated = leftProbe.AnimatedPosition;
            _previousRightAnimated = rightProbe.AnimatedPosition;
            _hasPreviousAnimatedFeet = true;

            ResolveClipContactMetadata(
                out float leftPhase,
                out float rightPhase,
                out float leftContact,
                out float rightContact);
            _gaitPhase = leftPhase;
            _leftGaitPhase = leftPhase;
            _rightGaitPhase = rightPhase;
            float rightSolverPhase = float.IsFinite(rightContact)
                ? rightPhase
                : leftPhase;
            EarthFootContactInput leftInput = BuildInput(
                true,
                in leftProbe,
                leftVerticalVelocity,
                leftPhase,
                leftContact,
                contactSupported,
                locomoting,
                pivotingInPlace,
                requestLock,
                in _leftContactSupport,
                up,
                deltaTime);
            EarthFootContactInput rightInput = BuildInput(
                false,
                in rightProbe,
                rightVerticalVelocity,
                rightSolverPhase,
                rightContact,
                contactSupported,
                locomoting,
                pivotingInPlace,
                requestLock,
                in _rightContactSupport,
                up,
                deltaTime);
            EarthFootContactPairDecision pair = EarthFootContactSolver.ResolvePair(
                ref _leftState,
                ref _rightState,
                in leftInput,
                in rightInput);
            _leftDecision = pair.Left;
            _rightDecision = pair.Right;

            ResolveWorldTarget(
                in _leftDecision,
                in _leftContactSupport,
                up,
                out _leftTargetWorld,
                out _leftNormalWorld);
            ResolveWorldTarget(
                in _rightDecision,
                in _rightContactSupport,
                up,
                out _rightTargetWorld,
                out _rightNormalWorld);
            // Final IK follows the selected base pose. A regular stance capture
            // takes four percent per 60 Hz frame and cannot consume more than
            // twelve percent in a hitch frame; otherwise the last 0.84 -> 1.0
            // step can visibly straighten a Humanoid knee. Pivot capture stays
            // quicker, while swing release remains immediate.
            float responseSeconds = contactCaptureSeconds;
            _leftAppliedWeight = EarthFootIkWeightBlend.StepContact(
                _leftAppliedWeight,
                _leftDecision.TargetWeight,
                deltaTime,
                pivotingInPlace
                    ? 0.06f
                    : Mathf.Max(responseSeconds, EarthFootIkWeightBlend.StanceCaptureResponseSeconds),
                0.02f,
                pivotingInPlace
                    ? EarthFootIkWeightBlend.MaximumPivotCaptureFrameStep
                    : EarthFootIkWeightBlend.MaximumStanceCaptureFrameStep);
            _rightAppliedWeight = EarthFootIkWeightBlend.StepContact(
                _rightAppliedWeight,
                _rightDecision.TargetWeight,
                deltaTime,
                pivotingInPlace
                    ? 0.06f
                    : Mathf.Max(responseSeconds, EarthFootIkWeightBlend.StanceCaptureResponseSeconds),
                0.02f,
                pivotingInPlace
                    ? EarthFootIkWeightBlend.MaximumPivotCaptureFrameStep
                    : EarthFootIkWeightBlend.MaximumStanceCaptureFrameStep);
            _leftAppliedWeight = EarthFootIkWeightBlend.EnforceSwingMaximum(
                _leftAppliedWeight,
                _leftDecision.Locked,
                _leftDecision.Reason);
            _rightAppliedWeight = EarthFootIkWeightBlend.EnforceSwingMaximum(
                _rightAppliedWeight,
                _rightDecision.Locked,
                _rightDecision.Reason);

            // Establish final body reach before setting goals and hints.
            ApplyPelvis(up, in poseIntent, requestLock, deltaTime);
            ApplyFoot(
                AvatarIKGoal.LeftFoot,
                _leftTargetWorld,
                _leftNormalWorld,
                _leftAppliedWeight);
            ApplyFoot(
                AvatarIKGoal.RightFoot,
                _rightTargetWorld,
                _rightNormalWorld,
                _rightAppliedWeight);
            ApplyKneeHints(up, _leftAppliedWeight, _rightAppliedWeight);
        }

        private EarthFootContactInput BuildInput(
            bool left,
            in FootProbe probe,
            float verticalVelocity,
            float gaitPhase,
            float authoredContact,
            bool supported,
            bool locomoting,
            bool pivotingInPlace,
            bool poseLock,
            in SupportFrameSnapshot support,
            Vector3 up,
            float deltaTime)
        {
            float3 contactLocal = EarthSupportFootLockSolver.CaptureLocal(
                ToFloat3(probe.TargetPosition),
                in support);
            float3 fallbackLocal = EarthSupportFootLockSolver.CaptureLocal(
                ToFloat3(probe.AnimatedPosition),
                in support);
            float3 normalLocal = support.IsValid
                ? math.rotate(math.inverse(support.Rotation), ToFloat3(probe.Normal))
                : ToFloat3(probe.Normal);
            float3 upLocal = support.IsValid
                ? math.rotate(math.inverse(support.Rotation), ToFloat3(up))
                : ToFloat3(up);
            float phaseBias = math.cos(gaitPhase * math.PI * 2f);
            float capturePriority = -Mathf.Abs(probe.Clearance) +
                                    Mathf.Max(0f, -verticalVelocity) * 0.02f +
                                    phaseBias * 0.01f +
                                    (float.IsFinite(authoredContact)
                                        ? (authoredContact - 0.5f) * 0.035f
                                        : 0f);
            return new EarthFootContactInput(
                left,
                supported,
                locomoting,
                pivotingInPlace,
                poseLock,
                probe.HasContact,
                probe.Clearance,
                verticalVelocity,
                capturePriority,
                gaitPhase,
                contactLocal,
                normalLocal,
                fallbackLocal,
                upLocal,
                support.IsValid ? support.SurfaceId : 0u,
                support.IsValid ? support.Generation : 0u,
                deltaTime,
                authoredContact);
        }

        private FootProbe ProbeFoot(
            Transform foot,
            RaycastHit[] hits,
            CharacterSupportCandidate[] candidates,
            int[] candidateHitIndices,
            float side,
            Vector3 up,
            in EarthFootContactState previousFootState,
            in SupportFrameSnapshot previousSupportFrame,
            in CharacterSupportSelection previousSupport)
        {
            // Transform positions can still contain last frame's solved stance.
            // Probe the current controller goal, or the already-validated EAMM
            // candidate, rather than feeding last frame's IK back into itself.
            bool left = side < 0f;
            // Animator IK goals retain their last submitted world position while
            // an authored flight/mantle lane keeps their weight at zero. After an
            // explicit invalidation, seed from the freshly evaluated bone once;
            // otherwise a stale high mantle goal can make grounded idle look
            // 0.9 m out of reach forever.
            float previousWeight = left ? _leftAppliedWeight : _rightAppliedWeight;
            bool needsFreshIdlePose = !_locomoting && previousWeight <= 0.001f;
            Vector3 animated = !_hasPreviousAnimatedFeet || needsFreshIdlePose
                ? foot.position
                : animator.GetIKPosition(left ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightFoot);
            if (_basePoseBridge != null && _basePoseBridge.TryGetBaseFootPosition(left, out Vector3 baseFoot))
                animated = Vector3.Lerp(animated, baseFoot, _basePoseBridge.AppliedEammMasterWeight);
            EarthPoseIntent poseIntent = poseIntentSource != null
                ? poseIntentSource.CurrentIntent
                : default;
            Vector3 stanceOffset = transform.right * side * poseIntent.StanceWidth01 * 0.11f;
            Vector3 probeBase = animated + stanceOffset;
            bool stableLockedAnchor = previousFootState.Locked &&
                                      previousSupportFrame.IsValid &&
                                      previousFootState.SupportId == previousSupportFrame.SurfaceId &&
                                      previousFootState.SupportGeneration == previousSupportFrame.Generation;
            if (stableLockedAnchor)
            {
                probeBase = ToVector3(EarthSupportFootLockSolver.ResolveWorld(
                    previousFootState.AnchorLocal,
                    in previousSupportFrame));
            }
            Vector3 origin = probeBase + up * footProbeLift;
            int count = UnityEngine.Physics.RaycastNonAlloc(
                origin,
                -up,
                hits,
                footProbeLift + footProbeDistance,
                motor.GroundMask,
                QueryTriggerInteraction.Ignore);
            int candidateCount = 0;
            int safeCount = Mathf.Min(count, hits.Length);
            float minimumWalkableUpDot = Mathf.Cos(
                motor.MaximumSlopeAngle * Mathf.Deg2Rad);
            for (int index = 0; index < safeCount; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.collider == null) continue;
                if (rootBody != null && hit.collider.transform.IsChildOf(rootBody.transform))
                    continue;
                if (hit.collider.GetComponentInParent<PlanetMotor>() != null) continue;
                float upDot = Vector3.Dot(hit.normal, up);
                candidates[candidateCount] = CharacterSupportRuntimeAdapter.Classify(
                    hit.collider,
                    hit.distance,
                    upDot);
                candidateHitIndices[candidateCount] = index;
                candidateCount++;
            }

            CharacterSupportSelection selection = CharacterSupportAuthority.Select(
                candidates,
                candidateCount,
                in previousSupport,
                minimumWalkableUpDot,
                supportSwapHysteresis);
            RaycastHit selected = default;
            if (selection.HasSupport)
            {
                CharacterSupportCandidate selectedCandidate = selection.Candidate;
                for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
                {
                    CharacterSupportCandidate candidate = candidates[candidateIndex];
                    if (!CharacterSupportAuthority.Matches(
                            in candidate,
                            in selectedCandidate))
                        continue;
                    selected = hits[candidateHitIndices[candidateIndex]];
                    break;
                }
            }

            bool hasContact = selected.collider != null;
            if (!hasContact) selection = CharacterSupportSelection.None;
            Vector3 normal = hasContact ? selected.normal.normalized : up;
            Vector3 target = hasContact
                ? selected.point + normal * soleOffset
                : animated + stanceOffset;
            float clearance = hasContact
                ? Vector3.Dot(animated + stanceOffset - target, up)
                : float.PositiveInfinity;
            return new FootProbe(
                animated + stanceOffset,
                hasContact ? selected.point : animated + stanceOffset,
                target,
                normal,
                clearance,
                hasContact,
                selected.collider,
                in selection);
        }

        private void ApplyFoot(
            AvatarIKGoal goal,
            Vector3 target,
            Vector3 normal,
            float weight)
        {
            // This callback runs after graph evaluation; it is the supported final
            // Humanoid goal owner for both authored and EAMM-composed base poses.
            // Humanoid IK goals and skeleton bones have different orientation
            // bases. Capture the authored goal before writing IK: feeding the
            // bone rotation back as a goal tipped Linebreaker toes ~90 degrees
            // upwards as idle contact weight approached one.
            Quaternion authoredGoalRotation = animator.GetIKRotation(goal);
            float applied = EarthFootIkWeightBlend.ResolveSubmittedGoalWeight(weight);
            animator.SetIKPositionWeight(goal, applied);
            animator.SetIKRotationWeight(goal, applied);
            if (applied <= 0.001f) return;
            // Animator IK already blends the target by its position weight.
            // Lerping the target here as well applied weight twice (w^2), which
            // left a pivot foot sliding 5.1 cm while the reported IK weight was
            // already 0.68. Fast release prevents a stale swing anchor.
            animator.SetIKPosition(goal, target);
            Vector3 characterUp = motor != null && motor.LocalUp.sqrMagnitude > 0.5f
                ? motor.LocalUp.normalized
                : transform.up;
            // Preserve authored heel/toe roll and yaw. Rebuilding a foot with
            // LookRotation flattened the whole ankle every time a stance lock
            // captured (70.9 degrees in the live audit). Surface adaptation is
            // only the bounded slope delta from character-up to contact normal.
            Quaternion slopeAlignment = Quaternion.FromToRotation(characterUp, normal);
            animator.SetIKRotation(goal, slopeAlignment * authoredGoalRotation);
        }

        private void ApplyKneeHints(Vector3 up, float leftWeight, float rightWeight)
        {
            float leftApplied = Mathf.Clamp01(leftWeight) * 0.28f;
            float rightApplied = Mathf.Clamp01(rightWeight) * 0.28f;
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, leftApplied);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, rightApplied);
            if (Mathf.Max(leftApplied, rightApplied) <= 0.001f ||
                _leftUpperLeg == null || _rightUpperLeg == null) return;

            Vector3 characterForward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            if (characterForward.sqrMagnitude < 0.1f)
                characterForward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            Vector3 characterRight = Vector3.Cross(up, characterForward).normalized;
            float3 forward = ToFloat3(characterForward);
            float3 right = ToFloat3(characterRight);
            float3 localUp = ToFloat3(up);
            float3 leftHint = EarthStableKneeHintSolver.Solve(
                ToFloat3(_leftUpperLeg.position),
                forward,
                right,
                localUp,
                -1f,
                _leftKneeDirection);
            float3 rightHint = EarthStableKneeHintSolver.Solve(
                ToFloat3(_rightUpperLeg.position),
                forward,
                right,
                localUp,
                1f,
                _rightKneeDirection);
            float deltaTime = Mathf.Max(0.0001f, Time.deltaTime);
            float response = 1f - Mathf.Exp(-deltaTime / KneeHintResponseSeconds);
            float3 leftDesired = math.normalizesafe(
                leftHint - ToFloat3(_leftUpperLeg.position),
                forward);
            float3 rightDesired = math.normalizesafe(
                rightHint - ToFloat3(_rightUpperLeg.position),
                forward);
            _leftKneeDirection = StepKneeDirection(
                _leftKneeDirection,
                leftDesired,
                response,
                deltaTime);
            _rightKneeDirection = StepKneeDirection(
                _rightKneeDirection,
                rightDesired,
                response,
                deltaTime);
            _leftHintWorld = _leftUpperLeg.position + ToVector3(_leftKneeDirection) * 0.60f;
            _rightHintWorld = _rightUpperLeg.position + ToVector3(_rightKneeDirection) * 0.60f;
            ApplyCachedKneeHints();
        }

        private void ApplyCachedKneeHints()
        {
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, _leftAppliedWeight * 0.28f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, _rightAppliedWeight * 0.28f);
            animator.SetIKHintPosition(AvatarIKHint.LeftKnee, _leftHintWorld);
            animator.SetIKHintPosition(AvatarIKHint.RightKnee, _rightHintWorld);
        }

        private static float3 StepKneeDirection(
            float3 current,
            float3 desired,
            float response,
            float deltaTime)
        {
            float3 safeDesired = math.normalizesafe(desired, new float3(0f, 0f, 1f));
            float3 previous = math.normalizesafe(current, safeDesired);
            float3 filtered = math.normalizesafe(
                math.lerp(previous, safeDesired, math.saturate(response)),
                safeDesired);
            float maximumRadians = Mathf.Deg2Rad * MaximumKneeHintStepDegreesAt60Hz *
                                   Mathf.Max(0.0001f, deltaTime) * 60f;
            Vector3 next = Vector3.RotateTowards(
                ToVector3(previous),
                ToVector3(filtered),
                maximumRadians,
                0f);
            return math.normalizesafe(ToFloat3(next), safeDesired);
        }

        private void ApplyPelvis(
            Vector3 up,
            in EarthPoseIntent poseIntent,
            bool requestLock,
            float deltaTime)
        {
            float leftError = _leftAppliedWeight > 0.05f
                ? Vector3.Dot(_leftTargetWorld - _leftFoot.position, up)
                : 0f;
            float rightError = _rightAppliedWeight > 0.05f
                ? Vector3.Dot(_rightTargetWorld - _rightFoot.position, up)
                : 0f;
            bool finalContactOwned = Mathf.Max(
                _leftAppliedWeight,
                _rightAppliedWeight) >= 0.999f;
            bool stanceCaptureOwned =
                (_leftDecision.TargetWeight > 0.05f &&
                 (_leftDecision.Reason is EarthFootContactReason.Capture or EarthFootContactReason.Stance)) ||
                (_rightDecision.TargetWeight > 0.05f &&
                 (_rightDecision.Reason is EarthFootContactReason.Capture or EarthFootContactReason.Stance));
            float allowedDrop = requestLock || stanceCaptureOwned
                ? maximumPelvisDrop
                : Mathf.Min(0.08f, maximumPelvisDrop);
            float target = EarthPelvisCompensation.Solve(
                leftError,
                rightError,
                requestLock ? poseIntent.PelvisCompression01 : 0f,
                allowedDrop);
            LeftPelvisRequestMeters = leftError;
            RightPelvisRequestMeters = rightError;
            PelvisTargetMeters = target;
            float previousOffset = _pelvisOffset;
            Vector3 pelvisBaseWorld = animator.bodyPosition;
            float baseRiseAlongUp = _hasPreviousPelvisBaseWorld
                ? Vector3.Dot(pelvisBaseWorld - _previousPelvisBaseWorld, up)
                : 0f;
            _previousPelvisBaseWorld = pelvisBaseWorld;
            _hasPreviousPelvisBaseWorld = true;
            float smoothed = Mathf.SmoothDamp(
                _pelvisOffset,
                target,
                ref _pelvisVelocity,
                _pelvisResponseSeconds,
                _pelvisMaximumSpeed,
                deltaTime);
            _pelvisOffset = EarthPelvisCompensation.SelectAppliedOffset(
                previousOffset,
                target,
                smoothed,
                finalContactOwned,
                baseRiseAlongUp);
            if (finalContactOwned && target < previousOffset)
                _pelvisVelocity = 0f;
            animator.bodyPosition += up * _pelvisOffset;
            LeftAnchorErrorMeters = _leftDecision.Locked
                ? Vector3.Distance(_leftTargetWorld, _leftFoot.position)
                : 0f;
            RightAnchorErrorMeters = _rightDecision.Locked
                ? Vector3.Distance(_rightTargetWorld, _rightFoot.position)
                : 0f;
        }

        private SupportFrameSnapshot ResolvePresentationSupport(
            in FootProbe probe,
            Vector3 up)
        {
            if (surfController != null && surfController.IsActive)
                return surfController.PresentationSupportFrame;
            SupportFrameSnapshot fixedSupport = motor.CurrentSupportFrame;
            float renderLead = Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime);
            CharacterSupportCandidate selectedCandidate = probe.SupportSelection.Candidate;
            if (fixedSupport.IsValid && probe.SupportSelection.HasSupport &&
                fixedSupport.SurfaceId == selectedCandidate.SurfaceId &&
                fixedSupport.Generation == selectedCandidate.Generation)
                return EarthPresentationSupportSolver.Extrapolate(in fixedSupport, renderLead);

            Collider supportCollider = probe.SupportCollider;
            if (supportCollider == null) return default;
            IMovingSurface movingSurface = supportCollider.GetComponentInParent(
                typeof(IMovingSurface)) as IMovingSurface;
            if (movingSurface != null)
            {
                SupportFrameSnapshot movingFrame = movingSurface.SupportFrame;
                if (movingFrame.IsValid && probe.SupportSelection.HasSupport &&
                    movingFrame.SurfaceId == selectedCandidate.SurfaceId &&
                    movingFrame.Generation == selectedCandidate.Generation)
                    return EarthPresentationSupportSolver.Extrapolate(in movingFrame, renderLead);
            }

            uint surfaceId = probe.SupportSelection.HasSupport
                ? selectedCandidate.SurfaceId
                : 0u;
            uint generation = probe.SupportSelection.HasSupport
                ? selectedCandidate.Generation
                : 0u;
            if (surfaceId == 0u || generation == 0u) return default;

            Rigidbody supportBody = supportCollider.attachedRigidbody;
            Transform frame = supportBody != null
                ? supportBody.transform
                : supportCollider.transform;
            Vector3 linearVelocity = supportBody != null
                ? supportBody.linearVelocity
                : Vector3.zero;
            Vector3 angularVelocity = supportBody != null
                ? supportBody.angularVelocity
                : Vector3.zero;
            Vector3 pointVelocity = supportBody != null
                ? supportBody.GetPointVelocity(probe.TargetPosition)
                : Vector3.zero;
            Quaternion rotation = frame.rotation;
            return new SupportFrameSnapshot(
                surfaceId,
                generation,
                ToFloat3(frame.position),
                new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                ToFloat3(linearVelocity),
                ToFloat3(angularVelocity),
                ToFloat3(pointVelocity),
                ToFloat3(up),
                false);
        }

        private float ResolveGaitPhase()
        {
            if (animator == null || !animator.enabled) return 0f;
            AnimatorStateInfo state = _animationDriver != null
                ? _animationDriver.GetCurrentAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);
            return Mathf.Repeat(state.normalizedTime, 1f);
        }

        private void ResolveClipContactMetadata(
            out float leftPhase,
            out float rightPhase,
            out float leftContact,
            out float rightContact)
        {
            if (_hasBasePoseContactMetadata)
            {
                leftPhase = _basePoseLeftPhase;
                rightPhase = _basePoseRightPhase;
                leftContact = _basePoseLeftContact;
                rightContact = _basePoseRightContact;
                return;
            }
            float fallback = ResolveGaitPhase();
            if (!_hasClipContactMetadata || animator == null || !animator.enabled)
            {
                leftPhase = fallback;
                rightPhase = Mathf.Repeat(fallback + 0.5f, 1f);
                leftContact = float.NaN;
                rightContact = float.NaN;
                return;
            }
            leftPhase = Mathf.Repeat(ReadParameter(LeftFootPhaseHash), 1f);
            rightPhase = Mathf.Repeat(ReadParameter(RightFootPhaseHash), 1f);
            leftContact = Mathf.Clamp01(ReadParameter(LeftFootContactHash));
            rightContact = Mathf.Clamp01(ReadParameter(RightFootContactHash));
        }

        private float ReadParameter(int hash) => _animationDriver != null
            ? _animationDriver.GetFloat(hash) : animator.GetFloat(hash);

        private void ResolveMetadataAvailability()
        {
            _hasClipContactMetadata = false;
            if (animator == null) return;
            bool leftContact = false;
            bool rightContact = false;
            bool leftPhase = false;
            bool rightPhase = false;
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                int hash = parameters[index].nameHash;
                leftContact |= hash == LeftFootContactHash;
                rightContact |= hash == RightFootContactHash;
                leftPhase |= hash == LeftFootPhaseHash;
                rightPhase |= hash == RightFootPhaseHash;
            }
            _hasClipContactMetadata = leftContact && rightContact && leftPhase && rightPhase;
        }

        private static void ResolveWorldTarget(
            in EarthFootContactDecision decision,
            in SupportFrameSnapshot support,
            Vector3 fallbackUp,
            out Vector3 position,
            out Vector3 normal)
        {
            position = ToVector3(EarthSupportFootLockSolver.ResolveWorld(
                decision.TargetLocal,
                in support));
            float3 worldNormal = support.IsValid
                ? math.rotate(support.Rotation, decision.NormalLocal)
                : decision.NormalLocal;
            normal = ToVector3(math.normalizesafe(worldNormal, ToFloat3(fallbackUp)));
        }

        private void ResolveBones()
        {
            if (animator == null || !animator.isHuman) return;
            _leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        }

        private float ResolveAnkleAngle(Transform foot)
        {
            if (foot == null) return 0f;
            Vector3 up = motor != null && motor.LocalUp.sqrMagnitude > 0.5f
                ? motor.LocalUp.normalized
                : transform.up;
            return Vector3.Angle(foot.up, up);
        }

        private static float ResolveJointAngle(
            Transform upper,
            Transform lower,
            Transform end)
        {
            if (upper == null || lower == null || end == null) return 0f;
            Vector3 toUpper = upper.position - lower.position;
            Vector3 toEnd = end.position - lower.position;
            if (toUpper.sqrMagnitude < 0.000001f || toEnd.sqrMagnitude < 0.000001f)
                return 0f;
            return Vector3.Angle(toUpper, toEnd);
        }

        private static Vector3 ResolveKneeDirection(Transform upper, Transform lower)
        {
            if (upper == null || lower == null) return Vector3.down;
            Vector3 direction = lower.position - upper.position;
            return direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector3.down;
        }

        private static float3 ToFloat3(Vector3 value) =>
            new float3(value.x, value.y, value.z);

        private static Vector3 ToVector3(float3 value) =>
            new Vector3(value.x, value.y, value.z);

        private readonly struct FootProbe
        {
            public FootProbe(
                Vector3 animatedPosition,
                Vector3 contactPoint,
                Vector3 targetPosition,
                Vector3 normal,
                float clearance,
                bool hasContact,
                Collider supportCollider,
                in CharacterSupportSelection supportSelection)
            {
                AnimatedPosition = animatedPosition;
                ContactPoint = contactPoint;
                TargetPosition = targetPosition;
                Normal = normal;
                Clearance = clearance;
                HasContact = hasContact;
                SupportCollider = supportCollider;
                SupportSelection = supportSelection;
            }

            public Vector3 AnimatedPosition { get; }
            public Vector3 ContactPoint { get; }
            public Vector3 TargetPosition { get; }
            public Vector3 Normal { get; }
            public float Clearance { get; }
            public bool HasContact { get; }
            public Collider SupportCollider { get; }
            public CharacterSupportSelection SupportSelection { get; }
        }
    }
}
