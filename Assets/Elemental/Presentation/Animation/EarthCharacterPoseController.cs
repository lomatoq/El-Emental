using System;
using System.Collections.Generic;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Magic;
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
        [SerializeField] private EarthTechniquePresentationProfile profile;
        [SerializeField, Min(0.01f)] private float footProbeLift = 0.42f;
        [SerializeField, Min(0.05f)] private float footProbeDistance = 0.95f;
        [SerializeField, Min(0f)] private float soleOffset = 0.035f;
        [SerializeField, Min(0f)] private float maximumPelvisDrop = 0.22f;

        private readonly RaycastHit[] _leftHits = new RaycastHit[FootHitCapacity];
        private readonly RaycastHit[] _rightHits = new RaycastHit[FootHitCapacity];
        private Transform _leftFoot;
        private Transform _rightFoot;
        private EarthFootPlantResult _leftPlant;
        private EarthFootPlantResult _rightPlant;
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
            profile = configuredProfile;
            ResolveFeet();
            if (isActiveAndEnabled) Subscribe();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
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
            bool requestLock = CurrentIntent.LocksFeet && motor.IsGrounded && CurrentIntent.Brace01 > 0.2f;
            _leftPlant = ProbeFoot(_leftFoot, _leftHits, _leftPlant, requestLock, -1f);
            _rightPlant = ProbeFoot(_rightFoot, _rightHits, _rightPlant, requestLock, 1f);
            ApplyFoot(AvatarIKGoal.LeftFoot, _leftFoot, in _leftPlant);
            ApplyFoot(AvatarIKGoal.RightFoot, _rightFoot, in _rightPlant);

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
                motor.IsGrounded,
                requestLock,
                previous.Locked,
                previous.Position,
                soleOffset);
        }

        private void ApplyFoot(AvatarIKGoal goal, Transform animatedFoot, in EarthFootPlantResult plant)
        {
            animator.SetIKPositionWeight(goal, plant.Weight01);
            animator.SetIKRotationWeight(goal, plant.Weight01);
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
    }

}
