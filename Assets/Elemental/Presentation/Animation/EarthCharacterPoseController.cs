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
        [SerializeField] private MagicInputController input;
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private Rigidbody rootBody;
        [SerializeField] private EarthPillarMobility pillarMobility;
        [SerializeField] private EarthSurfController surfController;
        [SerializeField] private EarthTechniquePresentationProfile profile;
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
        private float _footIkWeight;
        private float3 _leftKneeDirection;
        private float3 _rightKneeDirection;
        private float3 _leftSupportLocal;
        private float3 _rightSupportLocal;
        private uint _lockedSupportId;
        private uint _lockedSupportGeneration;
        private uint _presentationTick;
        private uint _castStartTick;
        private uint _authoritativeTick;
        private EarthTechniqueKind _technique;
        private EarthCastTiming _timing;
        private float _eventMass;
        private float _eventAcceleration;
        private Vector3 _target;
        private bool _authoritativeTransient;
        private bool _subscribed;

        public EarthPoseIntent CurrentIntent { get; private set; }
        public BendingPoseRequest CurrentRequest { get; private set; }
        public uint LastAuthoritativeTick => _authoritativeTick;
        public uint PresentationTick => _presentationTick;
        public bool FeetLocked => _leftPlant.Locked && _rightPlant.Locked;

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
            input = configuredInput;
            executor = configuredExecutor;
            motor = configuredMotor;
            rootBody = configuredRootBody;
            pillarMobility = configuredPillar;
            if (surfController == null) surfController = GetComponentInParent<EarthSurfController>();
            profile = configuredProfile;
            ResolveFeet();
            if (isActiveAndEnabled) Subscribe();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (surfController == null) surfController = GetComponentInParent<EarthSurfController>();
            ResolveFeet();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void FixedUpdate()
        {
            _presentationTick++;
            UpdatePoseIntent();
        }

        private void UpdatePoseIntent()
        {
            if (motor == null || rootBody == null) return;
            bool sustained = ResolveSustainedState(out EarthTechniqueKind sustainedTechnique, out Vector3 focus);
            if (sustainedTechnique != EarthTechniqueKind.None)
            {
                _technique = sustainedTechnique;
                _target = focus;
                if (sustained && !_authoritativeTransient) _castStartTick = _presentationTick;
            }

            EarthCastPhase phase;
            if (_authoritativeTransient)
            {
                uint elapsed = _presentationTick - _castStartTick;
                phase = EarthCastPhaseSolver.Evaluate(elapsed, in _timing, sustained);
                if (phase == EarthCastPhase.Idle) _authoritativeTransient = false;
            }
            else phase = ResolveLivePhase(sustained);

            if (_technique == EarthTechniqueKind.None || phase == EarthCastPhase.Idle)
            {
                CurrentIntent = default;
                CurrentRequest = default;
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
                TechniqueId(_technique),
                phase,
                ToFloat3(actionAxis),
                ToFloat3(motor.LocalUp),
                controlledMass,
                CurrentIntent.Effort01,
                motor.HasStableSupport ? 1f : 0f,
                Precision(_technique),
                localDirection.x < 0f,
                focusMatter);
            if (animator != null) animator.SetInteger(CastKindHash, (int)CurrentIntent.Family);
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

        private bool ResolveSustainedState(out EarthTechniqueKind technique, out Vector3 focus)
        {
            technique = EarthTechniqueKind.None;
            focus = transform.position + transform.forward * 2f;
            if (executor != null && executor.IsRepairActive)
            {
                technique = EarthTechniqueKind.Repair;
                focus = executor.GravityWellFocus;
                return true;
            }
            if (executor != null && executor.HeldBody != null)
            {
                technique = EarthTechniqueKind.Grip;
                focus = executor.HeldBody.worldCenterOfMass;
                return true;
            }
            if (executor != null && executor.IsGravityWellActive)
            {
                technique = EarthTechniqueKind.Repair;
                focus = executor.GravityWellFocus;
                return true;
            }
            if (executor != null && executor.IsVectorFieldActive)
            {
                technique = EarthTechniqueKind.Grip;
                focus = executor.VectorFieldPoint;
                return true;
            }
            if (input != null && input.CurrentBendPhase != BendPhase.Idle &&
                input.CurrentBendPhase != BendPhase.Cancelled)
            {
                technique = input.SelectedAbility == EarthAbilityIds.RaisePlatform
                    ? EarthTechniqueKind.Platform
                    : input.SelectedAbility == EarthAbilityIds.LineWall
                        ? EarthTechniqueKind.Wall
                        : EarthTechniqueKind.Grip;
                focus = input.BendTargetPosition;
            }
            return false;
        }

        private void BeginAuthoritative(
            EarthTechniqueKind technique,
            uint tick,
            Vector3 target,
            float mass,
            float acceleration)
        {
            _technique = technique;
            _authoritativeTick = tick;
            _target = target;
            _eventMass = Mathf.Max(0f, mass);
            _eventAcceleration = Mathf.Max(0f, acceleration);
            _timing = ResolveTiming(technique);
            _castStartTick = _presentationTick >= _timing.StartupTicks
                ? _presentationTick - _timing.StartupTicks
                : 0u;
            _authoritativeTransient = true;
            UpdatePoseIntent();
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

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || motor == null || _leftFoot == null || _rightFoot == null) return;
            bool supported = motor.HasStableSupport;
            bool surfLock = surfController != null && surfController.IsActive;
            bool requestLock = supported &&
                               ((CurrentIntent.LocksFeet && CurrentIntent.Brace01 > 0.2f) || surfLock);
            if (!supported) _footIkWeight = 0f;
            else
            {
                float blendSeconds = requestLock ? 0.13f : 0.17f;
                _footIkWeight = Mathf.MoveTowards(
                    _footIkWeight,
                    requestLock ? 1f : 0f,
                    Time.deltaTime / blendSeconds);
            }
            if (!requestLock)
            {
                ApplyFoot(AvatarIKGoal.LeftFoot, _leftFoot, in _leftPlant, _footIkWeight);
                ApplyFoot(AvatarIKGoal.RightFoot, _rightFoot, in _rightPlant, _footIkWeight);
                ApplyKneeHints(_footIkWeight);
                if (_footIkWeight <= 0.001f)
                {
                    _leftPlant = default;
                    _rightPlant = default;
                    _lockedSupportId = 0u;
                    _lockedSupportGeneration = 0u;
                }
                return;
            }
            ResolveSupportRelativeLocks();
            _leftPlant = ProbeFoot(_leftFoot, _leftHits, _leftPlant, requestLock, -1f);
            _rightPlant = ProbeFoot(_rightFoot, _rightHits, _rightPlant, requestLock, 1f);
            CaptureSupportRelativeLocks();
            ApplyFoot(AvatarIKGoal.LeftFoot, _leftFoot, in _leftPlant, _footIkWeight);
            ApplyFoot(AvatarIKGoal.RightFoot, _rightFoot, in _rightPlant, _footIkWeight);
            ApplyKneeHints(_footIkWeight);

            Vector3 up = motor.LocalUp;
            float leftError = Vector3.Dot(ToVector3(_leftPlant.Position) - _leftFoot.position, up);
            float rightError = Vector3.Dot(ToVector3(_rightPlant.Position) - _rightFoot.position, up);
            float pelvisOffset = EarthPelvisCompensation.Solve(
                leftError, rightError, CurrentIntent.PelvisCompression01, maximumPelvisDrop);
            animator.bodyPosition += up * pelvisOffset;
            float twist = CurrentIntent.UpperBodyTwist01 * 15f;
            animator.bodyRotation = Quaternion.AngleAxis(twist, up) * animator.bodyRotation;
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
            return EarthFootPlantSolver.Solve(
                ToFloat3(animated + stanceOffset),
                selected.collider != null,
                ToFloat3(selected.point),
                ToFloat3(selected.collider != null ? selected.normal : up),
                ToFloat3(up),
                motor.HasStableSupport,
                requestLock,
                previous.Locked,
                previous.Position,
                soleOffset);
        }

        private void ApplyFoot(
            AvatarIKGoal goal,
            Transform animatedFoot,
            in EarthFootPlantResult plant,
            float weight)
        {
            float appliedWeight = plant.Weight01 * Mathf.Clamp01(weight);
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

        private void ApplyKneeHints(float weight)
        {
            float applied = Mathf.Clamp01(weight) * 0.86f;
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, applied);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, applied);
            if (applied <= 0.001f || _leftUpperLeg == null || _rightUpperLeg == null) return;
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

        private void ResolveSupportRelativeLocks()
        {
            SupportFrameSnapshot support = motor.CurrentSupportFrame;
            if (!EarthSupportFootLockSolver.SameSupport(
                    _lockedSupportId,
                    _lockedSupportGeneration,
                    in support)) return;
            if (_leftPlant.Locked)
                _leftPlant = new EarthFootPlantResult(
                    EarthSupportFootLockSolver.ResolveWorld(_leftSupportLocal, in support),
                    support.Up,
                    _leftPlant.Weight01,
                    true);
            if (_rightPlant.Locked)
                _rightPlant = new EarthFootPlantResult(
                    EarthSupportFootLockSolver.ResolveWorld(_rightSupportLocal, in support),
                    support.Up,
                    _rightPlant.Weight01,
                    true);
        }

        private void CaptureSupportRelativeLocks()
        {
            SupportFrameSnapshot support = motor.CurrentSupportFrame;
            if (!support.IsValid || !_leftPlant.Locked || !_rightPlant.Locked) return;
            _lockedSupportId = support.SurfaceId;
            _lockedSupportGeneration = support.Generation;
            _leftSupportLocal = EarthSupportFootLockSolver.CaptureLocal(_leftPlant.Position, in support);
            _rightSupportLocal = EarthSupportFootLockSolver.CaptureLocal(_rightPlant.Position, in support);
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            if (executor != null)
            {
                executor.Events.WallRaised += OnWallRaised;
                executor.Events.FragmentSpawned += OnFragmentSpawned;
                executor.Events.FragmentLaunched += OnFragmentLaunched;
                executor.Events.EarthBodyGrabbed += OnBodyGrabbed;
                executor.Events.EarthBodyReleased += OnBodyReleased;
            }
            if (pillarMobility != null) pillarMobility.PillarRaised += OnPillarRaised;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (executor != null)
            {
                executor.Events.WallRaised -= OnWallRaised;
                executor.Events.FragmentSpawned -= OnFragmentSpawned;
                executor.Events.FragmentLaunched -= OnFragmentLaunched;
                executor.Events.EarthBodyGrabbed -= OnBodyGrabbed;
                executor.Events.EarthBodyReleased -= OnBodyReleased;
            }
            if (pillarMobility != null) pillarMobility.PillarRaised -= OnPillarRaised;
            _subscribed = false;
        }

        private void OnWallRaised(WallRaisedEvent value) => BeginAuthoritative(
            EarthTechniqueKind.Wall, value.Tick, ToVector3((value.Start + value.End) * 0.5f),
            value.Height * value.Thickness * math.distance(value.Start, value.End) * 1800f, 8f);
        private void OnFragmentSpawned(FragmentSpawnedEvent value) => BeginAuthoritative(
            EarthTechniqueKind.Grip, value.Tick, ToVector3(value.Position), value.Mass, 5f);
        private void OnFragmentLaunched(FragmentLaunchedEvent value) => BeginAuthoritative(
            EarthTechniqueKind.Grip, value.Tick, ToVector3(value.Position), value.Mass, value.VelocityChange);
        private void OnBodyGrabbed(EarthBodyGrabbedEvent value) => BeginAuthoritative(
            EarthTechniqueKind.Grip, value.Tick, ToVector3(value.Position), value.Mass, 4f);
        private void OnBodyReleased(EarthBodyReleasedEvent value) => BeginAuthoritative(
            EarthTechniqueKind.Grip, value.Tick, rootBody != null ? rootBody.worldCenterOfMass + ToVector3(value.Velocity) : transform.position,
            value.Mass, math.length(value.Velocity));
        private void OnPillarRaised(EarthPillarLaunchEvent value) => BeginAuthoritative(
            EarthTechniqueKind.Pillar, value.Tick, ToVector3(value.SurfaceBase), rootBody != null ? rootBody.mass : 80f,
            value.VelocityChange);

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
