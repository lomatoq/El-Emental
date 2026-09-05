using System;
using System.Collections.Generic;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Matter;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [DisallowMultipleComponent]
    public sealed class EarthCharacterPoseController : MonoBehaviour
    {
        private const int FootHitCapacity = 8;
        private static readonly int CastKindHash = Animator.StringToHash("CastKind");

        [SerializeField] private Animator animator;
        [SerializeField] private EarthAnimationDriver animationDriver;
        [SerializeField] private MagicInputController input;
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private Rigidbody rootBody;
        [SerializeField] private EarthPillarMobility pillarMobility;
        [SerializeField] private EarthDualMouseAbilityController dualMouseAbilities;
        [SerializeField] private EarthSurfController surfController;
        [SerializeField] private EarthTechniquePresentationProfile profile;
        [SerializeField] private EarthFootContactController footContactController;
        [SerializeField, Min(0.01f)] private float footProbeLift = 0.42f;
        [SerializeField, Min(0.05f)] private float footProbeDistance = 0.95f;
        [SerializeField, Min(0f)] private float soleOffset = 0.035f;
        [SerializeField, Min(0f)] private float maximumPelvisDrop = 0.22f;

        private readonly RaycastHit[] _leftHits = new RaycastHit[FootHitCapacity];
        private readonly RaycastHit[] _rightHits = new RaycastHit[FootHitCapacity];
        private Transform _leftFoot;
        private Transform _rightFoot;
        private Transform _leftUpperLeg;
        private Transform _rightUpperLeg;
        private EarthFootPlantResult _leftPlant;
        private EarthFootPlantResult _rightPlant;
        private EarthFootStanceState _leftStanceState;
        private EarthFootStanceState _rightStanceState;
        private float _footIkWeight;
        private float _leftAppliedFootIkWeight;
        private float _rightAppliedFootIkWeight;
        private float3 _leftKneeDirection;
        private float3 _rightKneeDirection;
        private bool _feetPoseLocked;
        private float3 _leftSupportLocal;
        private float3 _rightSupportLocal;
        private uint _leftLockedSupportId;
        private uint _leftLockedSupportGeneration;
        private uint _rightLockedSupportId;
        private uint _rightLockedSupportGeneration;
        private float _pelvisOffset;
        private float _pelvisVelocity;
        private float _legacyLeftAnchorErrorMeters;
        private float _legacyRightAnchorErrorMeters;
        private float _pelvisResponseSeconds = 0.085f;
        private float _pelvisMaximumSpeed = 0.8f;
        private uint _presentationTick;
        private uint _castStartTick;
        private uint _castPhaseOffsetTicks;
        private uint _authoritativeTick;
        private uint _authoritativePresentationGeneration;
        private EarthTechniqueKind _technique;
        private EarthTechniqueId _presentationTechnique;
        private EarthCastTiming _timing;
        private float _eventMass;
        private float _eventAcceleration;
        private Vector3 _target;
        private bool _authoritativeTransient;
        private bool _authoritativeStartsAtContact;
        private bool _presentationSuppressed;
        private EarthCastPhase _authoritativePhase;
        private bool _subscribed;
        private int _lastArmorReleaseFrame = -1;
        private QueuedPresentation _pendingPresentation;
        private bool _hasPendingPresentation;
        private int _droppedPresentationRequests;
        private int _supersededPresentationRequests;
        private bool _renderedContactReached;
        private float _lastRenderedMagicTime;
        private float _lastRenderedRecoveryTime;
        private float _lastRenderedSemanticWeight;
        private float _lastRenderedMagicLayerWeight;
        private float _renderContactElapsedSeconds;
        private float _renderContactBudgetSeconds;

        private struct QueuedPresentation
        {
            public EarthTechniqueKind Technique;
            public EarthTechniqueId PresentationTechnique;
            public uint Tick;
            public Vector3 Target;
            public float Mass;
            public float Acceleration;
            public bool EntryAtContact;
        }

        public EarthPoseIntent CurrentIntent { get; private set; }
        public BendingPoseRequest CurrentRequest { get; private set; }
        public uint LastAuthoritativeTick => _authoritativeTick;
        public uint AuthoritativePresentationGeneration => _authoritativePresentationGeneration;
        public uint PresentationTick => _presentationTick;
        public int QueuedPresentationCount => _hasPendingPresentation ? 1 : 0;
        public int DroppedPresentationRequests => _droppedPresentationRequests;
        public int SupersededPresentationRequests => _supersededPresentationRequests;
        public bool RenderedContactReached => _renderedContactReached;
        public bool HasAuthoritativePresentation => _authoritativeTransient;
        public bool AuthoritativeStartsAtContact =>
            _authoritativeTransient && _authoritativeStartsAtContact;
        public bool PresentationSuppressed => _presentationSuppressed;
        public float LastRenderedMagicTime => _lastRenderedMagicTime;
        public float LastRenderedSemanticWeight => _lastRenderedSemanticWeight;
        public float LastRenderedMagicLayerWeight => _lastRenderedMagicLayerWeight;
        public EarthCastPhase AuthoritativePhase => _authoritativePhase;
        public event Action<uint, EarthTechniqueId, EarthCastPhase> PresentationPhaseChanged;
        // Public pose-lock state intentionally excludes the per-foot gait stance
        // anchors below. During locomotion one support foot may be planted while
        // the other swings; that is support IK, not a magic/casting feet lock.
        public bool FeetLocked => footContactController != null && footContactController.FeetLocked;
        public bool LeftFootLocked => footContactController != null && footContactController.LeftFootLocked;
        public bool RightFootLocked => footContactController != null && footContactController.RightFootLocked;
        public float FootIkWeight => footContactController != null ? footContactController.FootIkWeight : 0f;
        public float LeftFootIkWeight => footContactController != null
            ? footContactController.LeftFootIkWeight
            : 0f;
        public float RightFootIkWeight => footContactController != null
            ? footContactController.RightFootIkWeight
            : 0f;
        public float LeftAnchorErrorMeters
        {
            get => footContactController != null
                ? footContactController.LeftAnchorErrorMeters
                : _legacyLeftAnchorErrorMeters;
            private set => _legacyLeftAnchorErrorMeters = value;
        }
        public float RightAnchorErrorMeters
        {
            get => footContactController != null
                ? footContactController.RightAnchorErrorMeters
                : _legacyRightAnchorErrorMeters;
            private set => _legacyRightAnchorErrorMeters = value;
        }
        public float PelvisCorrectionMeters => footContactController != null
            ? footContactController.PelvisCorrectionMeters
            : 0f;
        public float PelvisCorrectionVelocity => footContactController != null
            ? footContactController.PelvisCorrectionVelocity
            : 0f;

        public void Configure(
            Animator configuredAnimator,
            MagicInputController configuredInput,
            MagicExecutor configuredExecutor,
            PlanetMotor configuredMotor,
            Rigidbody configuredRootBody,
            EarthPillarMobility configuredPillar,
            EarthTechniquePresentationProfile configuredProfile)
        {
            if (_subscribed) Unsubscribe();
            animator = configuredAnimator;
            ResolveAnimationDriver();
            input = configuredInput;
            executor = configuredExecutor;
            motor = configuredMotor;
            rootBody = configuredRootBody;
            pillarMobility = configuredPillar;
            ResolvePresentationSources();
            if (surfController == null) surfController = GetComponentInParent<EarthSurfController>();
            profile = configuredProfile;
            ResolveFeet();
            if (isActiveAndEnabled) Subscribe();
        }

        public void ConfigureAnimationRescue(float pelvisResponseSeconds, float pelvisMaximumSpeed)
        {
            _pelvisResponseSeconds = Mathf.Clamp(pelvisResponseSeconds, 0.02f, 0.25f);
            _pelvisMaximumSpeed = Mathf.Clamp(pelvisMaximumSpeed, 0.2f, 1.5f);
            footContactController?.ConfigureAnimationRescue(
                _pelvisResponseSeconds,
                _pelvisMaximumSpeed);
        }

        public void SetFootContactController(EarthFootContactController controller)
        {
            footContactController = controller;
            footContactController?.SetPoseIntentSource(this);
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            ResolveAnimationDriver();
            if (surfController == null) surfController = GetComponentInParent<EarthSurfController>();
            ResolvePresentationSources();
            ResolveFeet();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable()
        {
            Unsubscribe();
            ClearPresentationQueue();
        }

        private void FixedUpdate()
        {
            _presentationTick++;
            if (_authoritativeTransient && !_renderedContactReached)
                _renderContactElapsedSeconds += Mathf.Max(0f, Time.fixedDeltaTime);
            if (_presentationSuppressed)
            {
                CurrentIntent = default;
                CurrentRequest = default;
                return;
            }
            if (input != null && input.LastActionIntent.Kind == EarthActionIntentKind.ArmorRadialRelease &&
                _lastArmorReleaseFrame != Time.frameCount)
            {
                _lastArmorReleaseFrame = Time.frameCount;
                BeginAuthoritative(
                    EarthTechniqueKind.Grip,
                    EarthTechniqueId.ArmorBarrage,
                    _presentationTick,
                    transform.position + transform.forward * 3f,
                    180f,
                    16f,
                    true);
                return;
            }
            UpdatePoseIntent();
        }

        private void UpdatePoseIntent()
        {
            if (motor == null || rootBody == null) return;
            bool sustained = ResolveSustainedState(
                out EarthTechniqueKind sustainedTechnique,
                out EarthTechniqueId sustainedPresentationTechnique,
                out Vector3 focus);
            if (!_authoritativeTransient && sustainedTechnique != EarthTechniqueKind.None)
            {
                _technique = sustainedTechnique;
                _presentationTechnique = sustainedPresentationTechnique;
                _target = focus;
                _eventMass = executor != null && executor.HeldBody != null
                    ? executor.HeldBody.mass
                    : 0f;
                _eventAcceleration = 0f;
                if (sustained && !_authoritativeTransient) _castStartTick = _presentationTick;
            }

            EarthCastPhase phase;
            if (_authoritativeTransient)
            {
                if (_renderedContactReached && _hasPendingPresentation)
                {
                    QueuedPresentation next = _pendingPresentation;
                    _pendingPresentation = default;
                    _hasPendingPresentation = false;
                    StartAuthoritative(in next);
                }
                uint elapsed = _presentationTick - _castStartTick + _castPhaseOffsetTicks;
                // An unrelated held field must not pin a queued one-shot action in
                // Sustain forever. The held field resumes after the accepted burst.
                phase = EarthCastPhaseSolver.Evaluate(elapsed, in _timing, false);
                if (phase == EarthCastPhase.Idle)
                {
                    if (!_renderedContactReached &&
                        _renderContactElapsedSeconds < _renderContactBudgetSeconds)
                    {
                        // Fixed ticks can finish during a long rendered-frame
                        // hitch. Keep the request alive at its authored contact
                        // silhouette until one visible sample is observed.
                        phase = EarthCastPhase.Sustain;
                    }
                    else if (_renderedContactReached && RequiresRenderedRecovery(
                                 _lastRenderedMagicTime,
                                 _lastRenderedRecoveryTime))
                    {
                        // The simulation phase can finish before a deliberately
                        // slower source clip. Keep a real rendered recovery phase
                        // alive until that clip reaches its authored recovery end.
                        // This prevents both a clipped return and visual-QA loops
                        // that never observe Recovery after a late contact.
                        phase = EarthCastPhase.Recover;
                    }
                    else
                    {
                        if (!_renderedContactReached)
                            _droppedPresentationRequests++;
                        if (_hasPendingPresentation)
                        {
                            QueuedPresentation next = _pendingPresentation;
                            _pendingPresentation = default;
                            _hasPendingPresentation = false;
                            StartAuthoritative(in next);
                            phase = EarthCastPhase.Acquire;
                        }
                        else
                        {
                            _authoritativeTransient = false;
                            _authoritativeStartsAtContact = false;
                            _castPhaseOffsetTicks = 0u;
                            _eventMass = 0f;
                            _eventAcceleration = 0f;
                            ReportAuthoritativePhase(EarthCastPhase.Idle);
                        }
                    }
                }
                if (_authoritativeTransient) ReportAuthoritativePhase(phase);
            }
            else phase = ResolveLivePhase(sustained);

            if (_technique == EarthTechniqueKind.None || phase == EarthCastPhase.Idle)
            {
                CurrentIntent = default;
                CurrentRequest = default;
                _presentationTechnique = EarthTechniqueId.None;
                return;
            }

            Vector3 localDirection = transform.InverseTransformDirection(
                Vector3.ProjectOnPlane(_target - rootBody.worldCenterOfMass, motor.LocalUp));
            float charge = input != null ? Mathf.Max(input.BendCharge01, input.BendAmount01) : 0f;
            float authoredEffort = 0.7f;
            float authoredBrace = 0.65f;
            if (profile != null && profile.TryGet(_technique, out EarthTechniquePresentation presentation))
            {
                authoredEffort = presentation.PoseEffort;
                authoredBrace = presentation.BraceAmount;
            }
            CurrentIntent = EarthPoseSolver.Solve(
                _technique,
                phase,
                ToFloat3(localDirection),
                ToFloat3(_target),
                _eventMass > 0f ? _eventMass : executor != null && executor.HeldBody != null
                    ? executor.HeldBody.mass
                    : 0f,
                _eventAcceleration,
                charge,
                motor.IsGrounded,
                authoredEffort,
                authoredBrace);
            float controlledMass = _eventMass > 0f
                ? _eventMass
                : executor != null && executor.HeldBody != null ? executor.HeldBody.mass : 0f;
            Vector3 actionAxis = Vector3.ProjectOnPlane(
                _target - rootBody.worldCenterOfMass, motor.LocalUp);
            EarthMatterId focusMatter = default;
            if (executor != null && executor.HeldBody != null)
            {
                EarthMatterIdentity identity = executor.HeldBody.GetComponent<EarthMatterIdentity>();
                if (identity != null) focusMatter = identity.MatterId;
            }
            CurrentRequest = new BendingPoseRequest(
                _presentationTechnique != EarthTechniqueId.None
                    ? _presentationTechnique
                    : TechniqueId(_technique),
                phase,
                ToFloat3(actionAxis),
                ToFloat3(motor.LocalUp),
                controlledMass,
                CurrentIntent.Effort01,
                motor.HasStableSupport ? 1f : 0f,
                Precision(_technique),
                localDirection.x < 0f,
                focusMatter);
            if (animationDriver != null)
                animationDriver.SetInteger(CastKindHash, (int)CurrentIntent.Family);
        }

        private void ResolveAnimationDriver()
        {
            if (animator == null) return;
            if (animationDriver == null) animationDriver = GetComponent<EarthAnimationDriver>();
            if (animationDriver == null) animationDriver = gameObject.AddComponent<EarthAnimationDriver>();
            if (animationDriver.Animator != animator) animationDriver.Configure(animator);
        }

        private EarthCastPhase ResolveLivePhase(bool sustained)
        {
            if (sustained) return EarthCastPhase.Sustain;
            if (input == null) return EarthCastPhase.Idle;
            return input.CurrentBendPhase switch
            {
                BendPhase.Acquiring => EarthCastPhase.Acquire,
                BendPhase.Forming => EarthCastPhase.Root,
                BendPhase.Holding => EarthCastPhase.Load,
                BendPhase.Charging => EarthCastPhase.Load,
                BendPhase.Committing => EarthCastPhase.Strike,
                BendPhase.Sustaining => EarthCastPhase.Sustain,
                BendPhase.Recovery => EarthCastPhase.Recover,
                _ => EarthCastPhase.Idle
            };
        }

        private bool ResolveSustainedState(
            out EarthTechniqueKind technique,
            out EarthTechniqueId presentationTechnique,
            out Vector3 focus)
        {
            technique = EarthTechniqueKind.None;
            presentationTechnique = EarthTechniqueId.None;
            focus = transform.position + transform.forward * 2f;
            if (input != null)
            {
                EarthActionIntentKind intent = input.LastActionIntent.Kind;
                if (intent == EarthActionIntentKind.ArmorRadialRelease)
                {
                    technique = EarthTechniqueKind.Grip;
                    presentationTechnique = EarthTechniqueId.ArmorBarrage;
                    focus = transform.position + transform.forward * 3f;
                    return true;
                }
                switch (input.ActiveActionOwner)
                {
                    case EarthActionOwner.Armor:
                        technique = EarthTechniqueKind.Grip;
                        presentationTechnique = input.ArmorPhase01 <= 0.30f
                            ? EarthTechniqueId.Armor
                            : input.ArmorPhase01 <= 0.78f
                                ? EarthTechniqueId.ArmorDome
                                : EarthTechniqueId.ArmorOrbit;
                        focus = transform.position + transform.forward * 1.5f;
                        return true;
                    case EarthActionOwner.Wave:
                        technique = EarthTechniqueKind.GroundWave;
                        presentationTechnique = EarthTechniqueId.WebWave;
                        focus = transform.position + transform.forward * 3f;
                        return true;
                    case EarthActionOwner.Resonance:
                        technique = EarthTechniqueKind.GroundWave;
                        presentationTechnique = EarthTechniqueId.Resonance;
                        focus = transform.position + transform.forward * 3f;
                        return true;
                    case EarthActionOwner.Surf:
                        technique = EarthTechniqueKind.Platform;
                        presentationTechnique = EarthTechniqueId.Surf;
                        focus = transform.position + transform.forward * 2f;
                        return true;
                    case EarthActionOwner.Pillar:
                    case EarthActionOwner.LandingCushion:
                        technique = EarthTechniqueKind.Pillar;
                        presentationTechnique = EarthTechniqueId.PillarJump;
                        focus = transform.position - motor.LocalUp;
                        return true;
                }
            }
            if (executor != null && executor.IsRepairActive)
            {
                technique = EarthTechniqueKind.Repair;
                presentationTechnique = EarthTechniqueId.Repair;
                focus = executor.GravityWellFocus;
                return true;
            }
            if (executor != null && executor.HeldBody != null)
            {
                technique = EarthTechniqueKind.Grip;
                presentationTechnique = EarthTechniqueId.PullStone;
                focus = executor.HeldBody.worldCenterOfMass;
                return true;
            }
            if (executor != null && executor.IsGravityWellActive)
            {
                technique = EarthTechniqueKind.Repair;
                presentationTechnique = EarthTechniqueId.GravityGrip;
                focus = executor.GravityWellFocus;
                return true;
            }
            if (executor != null && executor.IsVectorFieldActive)
            {
                technique = EarthTechniqueKind.Grip;
                presentationTechnique = EarthTechniqueId.VectorPush;
                focus = executor.VectorFieldPoint;
                return true;
            }
            if (input != null && input.CurrentBendPhase != BendPhase.Idle &&
                input.CurrentBendPhase != BendPhase.Cancelled)
            {
                presentationTechnique = MagicPresentationSemanticResolver.ResolveTechnique(
                    input.SelectedElement,
                    input.SelectedAbility);
                technique = MagicPresentationSemanticResolver.ResolveKind(presentationTechnique);
                focus = input.BendTargetPosition;
            }
            return false;
        }

        private void BeginAuthoritative(
            EarthTechniqueKind technique,
            EarthTechniqueId presentationTechnique,
            uint tick,
            Vector3 target,
            float mass,
            float acceleration,
            bool immediateActionBoundary = false)
        {
            RequestSemanticPresentation(
                technique,
                presentationTechnique,
                tick,
                target,
                mass,
                acceleration,
                immediateActionBoundary);
        }

        public void RequestSemanticPresentation(
            EarthTechniqueKind technique,
            EarthTechniqueId presentationTechnique,
            uint tick,
            Vector3 target,
            float mass,
            float acceleration,
            bool immediateActionBoundary = false)
        {
            if (_presentationSuppressed) return;
            var request = new QueuedPresentation
            {
                Technique = technique,
                PresentationTechnique = presentationTechnique,
                Tick = tick,
                Target = target,
                Mass = Mathf.Max(0f, mass),
                Acceleration = Mathf.Max(0f, acceleration),
                EntryAtContact = immediateActionBoundary
            };
            if (_authoritativeTransient)
            {
                if (_authoritativeTick == tick && _presentationTechnique == presentationTechnique)
                {
                    // Some executors publish the concrete world event while
                    // Execute is still on the stack, then MagicInputController
                    // publishes the accepted command with the same tick. Promote
                    // that already-started anticipation to the confirmed contact
                    // boundary instead of discarding the stronger admission.
                    if (immediateActionBoundary && !_authoritativeStartsAtContact)
                    {
                        StartAuthoritative(in request);
                        UpdatePoseIntent();
                    }
                    return;
                }
                if (immediateActionBoundary)
                {
                    bool repeatsCurrentAction =
                        _presentationTechnique == presentationTechnique;
                    if (repeatsCurrentAction)
                    {
                        // The current strike already supplies anticipation. A
                        // same-action follow-up must restart from zero after that
                        // strike becomes visible so it retracts and extends again.
                        request.EntryAtContact = false;
                    }
                    if (repeatsCurrentAction && !_renderedContactReached)
                    {
                        SetLatestPending(in request);
                        return;
                    }
                    // A committed gameplay release/contact must be visible now,
                    // not after an earlier anticipation clip reaches contact.
                    // The inactive A/B buffer preserves the outgoing rendered
                    // pose while this accepted boundary starts at time zero.
                    if (_hasPendingPresentation)
                    {
                        _supersededPresentationRequests++;
                        _pendingPresentation = default;
                        _hasPendingPresentation = false;
                    }
                    StartAuthoritative(in request);
                    UpdatePoseIntent();
                    return;
                }
                if (_renderedContactReached)
                {
                    if (_hasPendingPresentation)
                    {
                        _supersededPresentationRequests++;
                        _pendingPresentation = default;
                        _hasPendingPresentation = false;
                    }
                    StartAuthoritative(in request);
                    UpdatePoseIntent();
                    return;
                }
                SetLatestPending(in request);
                return;
            }
            StartAuthoritative(in request);
            UpdatePoseIntent();
        }

        private void StartAuthoritative(in QueuedPresentation request)
        {
            unchecked
            {
                _authoritativePresentationGeneration++;
                if (_authoritativePresentationGeneration == 0u)
                    _authoritativePresentationGeneration = 1u;
            }
            _technique = request.Technique;
            _presentationTechnique = request.PresentationTechnique;
            _authoritativeTick = request.Tick;
            _target = request.Target;
            _eventMass = request.Mass;
            _eventAcceleration = request.Acceleration;
            _timing = ResolveTiming(request.Technique);
            // Normal accepted commands play complete anticipation. A committed
            // physical release uses a semantic phase offset: its preparation was
            // already visible in the preceding held/load action, so the new A/B
            // buffer begins at authored contact without delaying gameplay.
            _castStartTick = _presentationTick;
            _castPhaseOffsetTicks = request.EntryAtContact ? _timing.ContactTick : 0u;
            _authoritativeStartsAtContact = request.EntryAtContact;
            _authoritativeTransient = true;
            _renderedContactReached = false;
            _lastRenderedMagicTime = 0f;
            _lastRenderedRecoveryTime = 0f;
            _lastRenderedSemanticWeight = 0f;
            _lastRenderedMagicLayerWeight = 0f;
            _renderContactElapsedSeconds = 0f;
            _renderContactBudgetSeconds =
                Mathf.Max(0.75f, _timing.TotalTicks / 60f + 0.35f);
            _authoritativePhase = EarthCastPhase.Idle;
            ReportAuthoritativePhase(
                request.EntryAtContact ? EarthCastPhase.Strike : EarthCastPhase.Acquire,
                true);
        }

        private void SetLatestPending(in QueuedPresentation request)
        {
            if (_hasPendingPresentation)
            {
                if (_pendingPresentation.Tick == request.Tick &&
                    _pendingPresentation.PresentationTechnique == request.PresentationTechnique)
                    return;
                _supersededPresentationRequests++;
            }
            _pendingPresentation = request;
            _hasPendingPresentation = true;
        }

        /// <summary>
        /// Called by the rendered animation owner after its continuous clip clock
        /// reaches the authored contact marker. Fixed phase labels alone are not
        /// sufficient: under a hitch they can advance before the pose was drawn.
        /// </summary>
        public void NotifyRenderedMagicSample(
            uint sequence,
            float normalizedTime,
            float contactTime,
            float recoveryTime,
            float semanticWeight,
            float layerWeight)
        {
            if (!_authoritativeTransient || sequence != _authoritativeTick) return;
            _lastRenderedMagicTime = Mathf.Clamp01(normalizedTime);
            _lastRenderedRecoveryTime = Mathf.Max(
                Mathf.Clamp01(contactTime),
                Mathf.Clamp01(recoveryTime));
            _lastRenderedSemanticWeight = Mathf.Clamp01(semanticWeight);
            _lastRenderedMagicLayerWeight = Mathf.Clamp01(layerWeight);
            if (_lastRenderedMagicTime + 0.0005f < Mathf.Clamp01(contactTime)) return;
            // A shared Direct BlendTree clock can already be parked at contact
            // while a new one-hot child is still fading in. Do not admit another
            // request until this sequence has contributed a readable rendered
            // pose; otherwise rapid alternation can replace every slot before any
            // of them becomes visible.
            if (_lastRenderedSemanticWeight < 0.70f ||
                _lastRenderedMagicLayerWeight < 0.35f) return;
            _renderedContactReached = true;
        }

        /// <summary>
        /// Extends the fixed-phase fallback to the actual visual clip budget.
        /// Repeated calls never restart elapsed time, so a malformed/non-rendering
        /// clip still expires. FixedUpdate owns elapsed time, which naturally
        /// freezes while scaled animation is paused.
        /// </summary>
        public void EnsureRenderedContactBudget(
            float contactNormalized,
            float maximumNormalizedSpeedPerSecond,
            float renderBlendAllowanceSeconds = .35f)
        {
            if (!_authoritativeTransient || _renderedContactReached) return;
            if (float.IsNaN(contactNormalized) || float.IsInfinity(contactNormalized) ||
                float.IsNaN(maximumNormalizedSpeedPerSecond) ||
                float.IsInfinity(maximumNormalizedSpeedPerSecond) ||
                maximumNormalizedSpeedPerSecond <= .0001f)
                return;
            float required = RequiredRenderedContactBudget(
                contactNormalized,
                maximumNormalizedSpeedPerSecond,
                renderBlendAllowanceSeconds);
            _renderContactBudgetSeconds = Mathf.Max(_renderContactBudgetSeconds, required);
        }

        public static float RequiredRenderedContactBudget(
            float contactNormalized,
            float maximumNormalizedSpeedPerSecond,
            float renderBlendAllowanceSeconds)
        {
            if (float.IsNaN(contactNormalized) || float.IsInfinity(contactNormalized) ||
                float.IsNaN(maximumNormalizedSpeedPerSecond) ||
                float.IsInfinity(maximumNormalizedSpeedPerSecond) ||
                maximumNormalizedSpeedPerSecond <= .0001f)
                return 0f;
            return Mathf.Clamp01(contactNormalized) /
                   maximumNormalizedSpeedPerSecond +
                   Mathf.Max(0f, renderBlendAllowanceSeconds);
        }

        public static bool RequiresRenderedRecovery(float renderedTime, float recoveryTime) =>
            Mathf.Clamp01(renderedTime) + 0.0005f < Mathf.Clamp01(recoveryTime);

        private void ReportAuthoritativePhase(EarthCastPhase phase, bool force = false)
        {
            if (!force && phase == _authoritativePhase) return;
            _authoritativePhase = phase;
            PresentationPhaseChanged?.Invoke(_authoritativeTick, _presentationTechnique, phase);
        }

        private EarthCastTiming ResolveTiming(EarthTechniqueKind technique)
        {
            const float tickRate = 60f;
            if (profile != null && profile.TryGet(technique, out EarthTechniquePresentation presentation))
            {
                EarthTechniqueTiming timing = presentation.Timing;
                return new EarthCastTiming(
                    SecondsToTicks(timing.Anticipation, tickRate),
                    SecondsToTicks(timing.Release + timing.Impact, tickRate),
                    SecondsToTicks(timing.Settle, tickRate),
                    0.35f);
            }
            return new EarthCastTiming(10, 6, 16, 0.35f);
        }

        // Retained temporarily as a source-level rollback while the new pair-wise
        // controller is verified. The Unity callback name is deliberately removed:
        // EarthFootContactController is the sole writer of feet, knees and pelvis.
        private void LegacyFootPlacementDisabled(int layerIndex)
        {
            // Foot placement has one owner and is evaluated after the base
            // locomotion layer. Re-applying bodyPosition from every IK-enabled upper
            // layer compounds pelvis offsets and is the source of visible hovering.
            if (layerIndex != 0) return;
            if (animator == null || motor == null || _leftFoot == null || _rightFoot == null) return;
            bool supported = motor.HasStableSupport;
            bool surfLock = surfController != null && surfController.IsActive;
            float tangentSpeed = rootBody != null
                ? Vector3.ProjectOnPlane(rootBody.linearVelocity, motor.LocalUp).magnitude
                : 0f;
            float2 moveInput = motor.LastCommand.Move;
            bool requestLock = EarthFootPlantMotionGate.ShouldLock(
                supported,
                surfLock,
                CurrentIntent.LocksFeet,
                CurrentIntent.Brace01,
                tangentSpeed,
                moveInput);
            _feetPoseLocked = requestLock;
            float targetFootWeight = EarthFootPlantMotionGate.TargetContactWeight(
                supported,
                surfLock,
                requestLock,
                tangentSpeed,
                moveInput);
            bool locomoting = EarthFootPlantMotionGate.IsLocomoting(
                moveInput,
                tangentSpeed) && !surfLock;
            float blendSeconds = requestLock ? 0.13f : locomoting ? 0.035f : supported ? 0.09f : 0.07f;
            _footIkWeight = Mathf.MoveTowards(
                _footIkWeight,
                targetFootWeight,
                Time.deltaTime / Mathf.Max(0.01f, blendSeconds));
            if (!supported)
            {
                _leftAppliedFootIkWeight = EarthFootIkWeightBlend.Step(
                    _leftAppliedFootIkWeight, 0f, Time.deltaTime, 0.06f);
                _rightAppliedFootIkWeight = EarthFootIkWeightBlend.Step(
                    _rightAppliedFootIkWeight, 0f, Time.deltaTime, 0.06f);
                ApplyFoot(
                    AvatarIKGoal.LeftFoot, _leftFoot, in _leftPlant, LeftFootIkWeight);
                ApplyFoot(
                    AvatarIKGoal.RightFoot, _rightFoot, in _rightPlant, RightFootIkWeight);
                ApplyKneeHints(LeftFootIkWeight, RightFootIkWeight);
                if (_footIkWeight <= 0.001f)
                {
                    _leftPlant = default;
                    _rightPlant = default;
                    ClearFootSupportLocks();
                }
                _pelvisOffset = Mathf.SmoothDamp(
                    _pelvisOffset, 0f, ref _pelvisVelocity, _pelvisResponseSeconds,
                    _pelvisMaximumSpeed, Mathf.Max(0.0001f, Time.deltaTime));
                LeftAnchorErrorMeters = 0f;
                RightAnchorErrorMeters = 0f;
                return;
            }
            SupportFrameSnapshot presentationSupport = ResolvePresentationSupport();
            UpdateFootPlant(
                true,
                _leftFoot,
                _leftHits,
                ref _leftPlant,
                ref _leftStanceState,
                requestLock,
                locomoting,
                _rightPlant.Locked,
                -1f,
                in presentationSupport);
            UpdateFootPlant(
                false,
                _rightFoot,
                _rightHits,
                ref _rightPlant,
                ref _rightStanceState,
                requestLock,
                locomoting,
                _leftPlant.Locked,
                1f,
                in presentationSupport);
            float perFootResponse = requestLock ? 0.04f : 0.055f;
            _leftAppliedFootIkWeight = EarthFootIkWeightBlend.Step(
                _leftAppliedFootIkWeight,
                _leftPlant.Weight01 * _footIkWeight,
                Time.deltaTime,
                perFootResponse);
            _rightAppliedFootIkWeight = EarthFootIkWeightBlend.Step(
                _rightAppliedFootIkWeight,
                _rightPlant.Weight01 * _footIkWeight,
                Time.deltaTime,
                perFootResponse);
            ApplyFoot(AvatarIKGoal.LeftFoot, _leftFoot, in _leftPlant, LeftFootIkWeight);
            ApplyFoot(AvatarIKGoal.RightFoot, _rightFoot, in _rightPlant, RightFootIkWeight);
            ApplyKneeHints(LeftFootIkWeight, RightFootIkWeight);

            Vector3 up = motor.LocalUp;
            float leftError = LeftFootIkWeight > 0.05f
                ? Vector3.Dot(ToVector3(_leftPlant.Position) - _leftFoot.position, up)
                : 0f;
            float rightError = RightFootIkWeight > 0.05f
                ? Vector3.Dot(ToVector3(_rightPlant.Position) - _rightFoot.position, up)
                : 0f;
            float allowedPelvisDrop = requestLock
                ? maximumPelvisDrop
                : Mathf.Min(0.045f, maximumPelvisDrop) * _footIkWeight;
            float pelvisOffset = EarthPelvisCompensation.Solve(
                leftError,
                rightError,
                requestLock ? CurrentIntent.PelvisCompression01 : 0f,
                allowedPelvisDrop);
            _pelvisOffset = Mathf.SmoothDamp(
                _pelvisOffset,
                pelvisOffset,
                ref _pelvisVelocity,
                _pelvisResponseSeconds,
                _pelvisMaximumSpeed,
                Mathf.Max(0.0001f, Time.deltaTime));
            animator.bodyPosition += up * _pelvisOffset;
            LeftAnchorErrorMeters = _leftPlant.Locked
                ? Vector3.Distance(ToVector3(_leftPlant.Position), _leftFoot.position)
                : 0f;
            RightAnchorErrorMeters = _rightPlant.Locked
                ? Vector3.Distance(ToVector3(_rightPlant.Position), _rightFoot.position)
                : 0f;
            // The upper-body AvatarMask and arm rig own aiming. Editing Mecanim's
            // bodyRotation here also rotates the pelvis and twists both legs while
            // the player walks with a sustained MMB/RMB technique.
        }

        private EarthFootPlantResult ProbeFoot(
            Transform foot,
            RaycastHit[] hits,
            EarthFootPlantResult previous,
            bool requestLock,
            float side)
        {
            Vector3 up = motor.LocalUp;
            Vector3 animated = foot.position;
            Vector3 stanceOffset = transform.right * side * CurrentIntent.StanceWidth01 * 0.11f;
            Vector3 origin = animated + stanceOffset + up * footProbeLift;
            int count = UnityEngine.Physics.RaycastNonAlloc(
                origin, -up, hits, footProbeLift + footProbeDistance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            RaycastHit selected = default;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.collider == null || hit.distance >= nearest) continue;
                if (rootBody != null && hit.collider.transform.IsChildOf(rootBody.transform)) continue;
                nearest = hit.distance;
                selected = hit;
            }
            float3 animatedPosition = ToFloat3(animated + stanceOffset);
            bool hasGround = selected.collider != null;
            float3 point = ToFloat3(selected.point);
            float3 normal = ToFloat3(hasGround ? selected.normal : up);
            return requestLock
                ? EarthFootPlantSolver.Solve(
                    animatedPosition,
                    hasGround,
                    point,
                    normal,
                    ToFloat3(up),
                    motor.HasStableSupport,
                    true,
                    previous.Locked,
                    previous.Position,
                    soleOffset)
                : EarthFootPlantSolver.SolveContact(
                    animatedPosition,
                    hasGround,
                    point,
                    normal,
                    ToFloat3(up),
                    motor.HasStableSupport,
                    soleOffset);
        }

        private void ApplyFoot(
            AvatarIKGoal goal,
            Transform animatedFoot,
            in EarthFootPlantResult plant,
            float weight)
        {
            float appliedWeight = Mathf.Clamp01(weight);
            animator.SetIKPositionWeight(goal, appliedWeight);
            animator.SetIKRotationWeight(goal, appliedWeight);
            if (appliedWeight <= 0.001f) return;
            animator.SetIKPosition(goal, ToVector3(plant.Position));
            Vector3 forward = Vector3.ProjectOnPlane(animatedFoot.forward, ToVector3(plant.Normal)).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.ProjectOnPlane(transform.forward, ToVector3(plant.Normal));
            animator.SetIKRotation(goal, Quaternion.LookRotation(forward, ToVector3(plant.Normal)));
        }

        private void ResolveFeet()
        {
            if (animator == null || !animator.isHuman) return;
            _leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        }

        private void ApplyKneeHints(float leftWeight, float rightWeight)
        {
            float leftApplied = Mathf.Clamp01(leftWeight) * 0.86f;
            float rightApplied = Mathf.Clamp01(rightWeight) * 0.86f;
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, leftApplied);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, rightApplied);
            if (Mathf.Max(leftApplied, rightApplied) <= 0.001f ||
                _leftUpperLeg == null || _rightUpperLeg == null) return;
            float3 forward = ToFloat3(Vector3.ProjectOnPlane(transform.forward, motor.LocalUp).normalized);
            float3 right = ToFloat3(Vector3.Cross(motor.LocalUp, ToVector3(forward)).normalized);
            float3 up = ToFloat3(motor.LocalUp);
            float3 left = EarthStableKneeHintSolver.Solve(
                ToFloat3(_leftUpperLeg.position), forward, right, up, -1f, _leftKneeDirection);
            float3 rightHint = EarthStableKneeHintSolver.Solve(
                ToFloat3(_rightUpperLeg.position), forward, right, up, 1f, _rightKneeDirection);
            _leftKneeDirection = math.normalizesafe(left - ToFloat3(_leftUpperLeg.position), forward);
            _rightKneeDirection = math.normalizesafe(rightHint - ToFloat3(_rightUpperLeg.position), forward);
            animator.SetIKHintPosition(AvatarIKHint.LeftKnee, ToVector3(left));
            animator.SetIKHintPosition(AvatarIKHint.RightKnee, ToVector3(rightHint));
        }

        private SupportFrameSnapshot ResolvePresentationSupport()
        {
            if (surfController != null && surfController.IsActive)
                return surfController.PresentationSupportFrame;
            SupportFrameSnapshot fixedSupport = motor.CurrentSupportFrame;
            float renderLead = Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime);
            return EarthPresentationSupportSolver.Extrapolate(in fixedSupport, renderLead);
        }

        private void UpdateFootPlant(
            bool left,
            Transform foot,
            RaycastHit[] hits,
            ref EarthFootPlantResult plant,
            ref EarthFootStanceState stanceState,
            bool poseLock,
            bool locomoting,
            bool otherLocomotionFootLocked,
            float side,
            in SupportFrameSnapshot support)
        {
            uint supportId = left ? _leftLockedSupportId : _rightLockedSupportId;
            uint supportGeneration = left
                ? _leftLockedSupportGeneration
                : _rightLockedSupportGeneration;
            bool sameSupport = plant.Locked && EarthSupportFootLockSolver.SameSupport(
                supportId,
                supportGeneration,
                in support);
            Vector3 up = motor.LocalUp;
            EarthFootPlantResult candidate = ProbeFoot(foot, hits, default, false, side);
            bool hasContact = candidate.Weight01 > 0f;
            float soleClearance = plant.Locked
                ? Vector3.Dot(foot.position - ToVector3(plant.Position), up)
                : hasContact
                    ? Vector3.Dot(foot.position - ToVector3(candidate.Position), up)
                    : float.PositiveInfinity;
            EarthFootStanceDecision decision = EarthFootStanceGate.Step(
                in stanceState,
                motor.HasStableSupport,
                locomoting,
                poseLock,
                sameSupport,
                hasContact,
                soleClearance,
                otherLocomotionFootLocked);
            stanceState = decision.State;
            if (decision.Maintained)
            {
                float3 local = left ? _leftSupportLocal : _rightSupportLocal;
                plant = new EarthFootPlantResult(
                    EarthSupportFootLockSolver.ResolveWorld(local, in support),
                    support.IsValid ? support.Up : ToFloat3(up),
                    1f,
                    true);
                return;
            }

            plant = decision.Locked && hasContact
                ? new EarthFootPlantResult(candidate.Position, candidate.Normal, 1f, true)
                : new EarthFootPlantResult(
                    candidate.Position,
                    candidate.Normal,
                    EarthFootStanceGate.ContactWeight(locomoting, false, soleClearance) *
                    candidate.Weight01,
                    false);
            if (plant.Locked) CaptureSupportRelativeLock(left, in support, in plant);
            else ClearFootSupportLock(left);
        }

        private void CaptureSupportRelativeLock(
            bool left,
            in SupportFrameSnapshot support,
            in EarthFootPlantResult plant)
        {
            uint id = support.IsValid ? support.SurfaceId : 0u;
            uint generation = support.IsValid ? support.Generation : 0u;
            float3 local = EarthSupportFootLockSolver.CaptureLocal(plant.Position, in support);
            if (left)
            {
                _leftLockedSupportId = id;
                _leftLockedSupportGeneration = generation;
                _leftSupportLocal = local;
            }
            else
            {
                _rightLockedSupportId = id;
                _rightLockedSupportGeneration = generation;
                _rightSupportLocal = local;
            }
        }

        private void ClearFootSupportLock(bool left)
        {
            if (left)
            {
                _leftLockedSupportId = 0u;
                _leftLockedSupportGeneration = 0u;
            }
            else
            {
                _rightLockedSupportId = 0u;
                _rightLockedSupportGeneration = 0u;
            }
        }

        private void ClearFootSupportLocks()
        {
            ClearFootSupportLock(true);
            ClearFootSupportLock(false);
            _leftStanceState = default;
            _rightStanceState = default;
            _leftAppliedFootIkWeight = 0f;
            _rightAppliedFootIkWeight = 0f;
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            ResolvePresentationSources();
            if (input != null) input.MagicCommandExecuted += OnMagicCommandExecuted;
            if (executor != null)
            {
                executor.Events.WallRaised += OnWallRaised;
                executor.Events.FragmentSpawned += OnFragmentSpawned;
                executor.Events.FragmentLaunched += OnFragmentLaunched;
                executor.Events.EarthBodyGrabbed += OnBodyGrabbed;
                executor.Events.EarthBodyReleased += OnBodyReleased;
                executor.Events.MagicPushed += OnMagicPushed;
            }
            if (pillarMobility != null) pillarMobility.PillarRaised += OnPillarRaised;
            if (dualMouseAbilities != null)
                dualMouseAbilities.PresentationRequested += OnDualMousePresentationRequested;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (input != null) input.MagicCommandExecuted -= OnMagicCommandExecuted;
            if (executor != null)
            {
                executor.Events.WallRaised -= OnWallRaised;
                executor.Events.FragmentSpawned -= OnFragmentSpawned;
                executor.Events.FragmentLaunched -= OnFragmentLaunched;
                executor.Events.EarthBodyGrabbed -= OnBodyGrabbed;
                executor.Events.EarthBodyReleased -= OnBodyReleased;
                executor.Events.MagicPushed -= OnMagicPushed;
            }
            if (pillarMobility != null) pillarMobility.PillarRaised -= OnPillarRaised;
            if (dualMouseAbilities != null)
                dualMouseAbilities.PresentationRequested -= OnDualMousePresentationRequested;
            _subscribed = false;
        }

        private void ResolvePresentationSources()
        {
            if (input == null) input = GetComponentInParent<MagicInputController>();
            if (dualMouseAbilities == null)
                dualMouseAbilities = GetComponentInParent<EarthDualMouseAbilityController>();
        }

        private void ClearPresentationQueue()
        {
            CancelPresentationForAnimationOwnership();
            _presentationSuppressed = false;
            _droppedPresentationRequests = 0;
            _supersededPresentationRequests = 0;
        }

        /// <summary>
        /// Ends presentation-only work when a higher-priority authored owner
        /// (mantle, ragdoll or knockdown recovery) takes the skeleton. Gameplay
        /// state is untouched and a still-held field may be adopted again after
        /// that protected animation finishes.
        /// </summary>
        public void CancelPresentationForAnimationOwnership()
        {
            if (_authoritativeTransient || CurrentRequest.IsActive || _hasPendingPresentation)
                ReportAuthoritativePhase(EarthCastPhase.Idle, true);
            _pendingPresentation = default;
            _hasPendingPresentation = false;
            _renderedContactReached = false;
            _lastRenderedMagicTime = 0f;
            _lastRenderedRecoveryTime = 0f;
            _lastRenderedSemanticWeight = 0f;
            _lastRenderedMagicLayerWeight = 0f;
            _renderContactElapsedSeconds = 0f;
            _renderContactBudgetSeconds = 0f;
            _authoritativeTransient = false;
            _authoritativeStartsAtContact = false;
            _castPhaseOffsetTicks = 0u;
            _authoritativePhase = EarthCastPhase.Idle;
            _technique = EarthTechniqueKind.None;
            _presentationTechnique = EarthTechniqueId.None;
            _eventMass = 0f;
            _eventAcceleration = 0f;
            _target = Vector3.zero;
            CurrentIntent = default;
            CurrentRequest = default;
        }

        public void SetPresentationSuppressed(bool suppressed)
        {
            if (_presentationSuppressed == suppressed) return;
            _presentationSuppressed = suppressed;
            if (suppressed) CancelPresentationForAnimationOwnership();
        }

        private void OnMagicCommandExecuted(MagicCommand command)
        {
            EarthTechniqueId semantic = MagicPresentationSemanticResolver.ResolveTechnique(
                command.Element,
                command.Ability);
            Vector3 origin = ToVector3(command.Origin);
            Vector3 target = origin + ToVector3(command.Aim) * Mathf.Lerp(2f, 6f, command.Intensity);
            BeginAuthoritative(
                MagicPresentationSemanticResolver.ResolveKind(semantic),
                semantic,
                command.Tick,
                target,
                0f,
                Mathf.Lerp(2f, 12f, command.Intensity),
                true);
        }

        private void OnDualMousePresentationRequested(
            EarthTechniqueId technique,
            uint sequence,
            Vector3 target,
            float mass,
            float acceleration) => BeginAuthoritative(
                MagicPresentationSemanticResolver.ResolveKind(technique),
                technique,
                sequence,
                target,
                mass,
                acceleration,
                technique == EarthTechniqueId.QuickStonePunch);

        private void OnWallRaised(WallRaisedEvent value) => BeginAuthoritative(
            EarthTechniqueKind.Wall, EarthTechniqueId.RaiseWall, value.Tick,
            ToVector3((value.Start + value.End) * 0.5f),
            value.Height * value.Thickness * math.distance(value.Start, value.End) * 1800f, 8f,
            true);
        private void OnFragmentSpawned(FragmentSpawnedEvent value) => BeginAuthoritative(
            EarthTechniqueKind.Grip, EarthTechniqueId.PullStone,
            value.Tick, ToVector3(value.Position), value.Mass, 5f, true);
        private void OnFragmentLaunched(FragmentLaunchedEvent value) => BeginAuthoritative(
            EarthTechniqueKind.Grip,
            value.PresentationStyle == EarthLaunchPresentationStyle.QuickPunch
                ? EarthTechniqueId.QuickStonePunch
                : EarthTechniqueId.ThrowStone,
            value.Tick, ToVector3(value.Position), value.Mass, value.VelocityChange,
            true);
        private void OnBodyGrabbed(EarthBodyGrabbedEvent value) => BeginAuthoritative(
            EarthTechniqueKind.Grip, EarthTechniqueId.PullStone,
            value.Tick, ToVector3(value.Position), value.Mass, 4f, true);
        private void OnBodyReleased(EarthBodyReleasedEvent value) => BeginAuthoritative(
            EarthTechniqueKind.Grip, EarthTechniqueId.ThrowStone, value.Tick,
            rootBody != null ? rootBody.worldCenterOfMass + ToVector3(value.Velocity) : transform.position,
            value.Mass, math.length(value.Velocity), true);
        private void OnMagicPushed(MagicPushEvent value) => BeginAuthoritative(
            EarthTechniqueKind.Grip, EarthTechniqueId.VectorPush, value.Tick,
            ToVector3(value.Point), value.TargetMass, value.VelocityChange, true);
        private void OnPillarRaised(EarthPillarLaunchEvent value) => BeginAuthoritative(
            EarthTechniqueKind.Pillar, EarthTechniqueId.PillarJump, value.Tick,
            ToVector3(value.SurfaceBase), rootBody != null ? rootBody.mass : 80f,
            value.VelocityChange, true);

        private static ushort SecondsToTicks(float seconds, float tickRate) =>
            (ushort)Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(0f, seconds) * tickRate), 1, ushort.MaxValue);
        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);

        private static EarthTechniqueId TechniqueId(EarthTechniqueKind technique) => technique switch
        {
            EarthTechniqueKind.Grip => EarthTechniqueId.PullStone,
            EarthTechniqueKind.Wall => EarthTechniqueId.RaiseWall,
            EarthTechniqueKind.Platform => EarthTechniqueId.RaisePlatform,
            EarthTechniqueKind.Pillar => EarthTechniqueId.PillarJump,
            EarthTechniqueKind.GroundWave => EarthTechniqueId.WebWave,
            EarthTechniqueKind.Repair => EarthTechniqueId.Repair,
            _ => EarthTechniqueId.None
        };

        private static float Precision(EarthTechniqueKind technique) => technique switch
        {
            EarthTechniqueKind.Repair => 1f,
            EarthTechniqueKind.Grip => 0.82f,
            EarthTechniqueKind.Platform => 0.66f,
            EarthTechniqueKind.Wall => 0.48f,
            EarthTechniqueKind.Pillar => 0.28f,
            EarthTechniqueKind.GroundWave => 0.22f,
            _ => 0.5f
        };
    }

}
