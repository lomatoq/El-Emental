using System;
using Elemental.Core.IDs;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Gravity;
using Elemental.Simulation.Magic;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ActiveRagdollPuppet : MonoBehaviour
    {
        private static readonly ProfilerMarker FixedTickMarker = new ProfilerMarker("Elemental.ActiveRagdoll.FixedTick");
        private const int GroundHitCapacity = 8;

        [Header("Authority")]
        [SerializeField, Min(1)] private uint actorId = 1u;
        [SerializeField] private GravityWorldBehaviour gravityWorld;
        [SerializeField] private Rigidbody rootBody;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private PhysicalImpactTarget impactTarget;

        [Header("Puppet")]
        [SerializeField] private Transform chest;
        [SerializeField] private ActiveRagdollJoint[] joints = Array.Empty<ActiveRagdollJoint>();
        [SerializeField] private Collider[] selfColliders = Array.Empty<Collider>();
        [SerializeField] private Behaviour[] disabledDuringRagdoll = Array.Empty<Behaviour>();

        [Header("Balance")]
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 2.2f;
        [SerializeField, Min(0f)] private float balanceGain = 160f;
        [SerializeField, Min(0f)] private float maximumBalanceTorque = 220f;
        [SerializeField] private LayerMask groundMask = ~0;

        private readonly RaycastHit[] _groundHits = new RaycastHit[GroundHitCapacity];
        private CharacterPhysicalController _controller;
        private Vector3 _gravityUp = Vector3.up;
        private Vector3 _supportCenter;
        private int _contactCount;
        private CharacterPhysicalMode _lastPublishedMode;
        private bool _hasPublishedMode;
        private float _suppressImpactsUntil;

        public CharacterPhysicalState CurrentState { get; private set; }
        public Vector3 LastBalanceTorque { get; private set; }
        public float MaximumJointError { get; private set; }
        public event Action<CharacterPhysicalState> StateChanged;
        public event Action<Vector3, float> ImpactObserved;

        public bool OwnsCollider(Collider candidate) => IsSelfCollider(candidate);

        public int CopySelfCollidersNonAlloc(Collider[] destination)
        {
            if (destination == null) return 0;
            int count = Mathf.Min(destination.Length, selfColliders.Length);
            for (int index = 0; index < count; index++) destination[index] = selfColliders[index];
            return count;
        }

        public void SuppressImpacts(float seconds)
        {
            _suppressImpactsUntil = Mathf.Max(_suppressImpactsUntil, Time.time + Mathf.Max(0f, seconds));
        }

        public void ApplyUniformVelocityChange(Vector3 velocityChange)
        {
            if (!IsFinite(velocityChange) || velocityChange.sqrMagnitude <= 0f) return;
            if (rootBody != null && !rootBody.isKinematic)
                rootBody.AddForce(velocityChange, ForceMode.VelocityChange);
            for (int index = 0; index < joints.Length; index++)
            {
                Rigidbody body = joints[index] != null ? joints[index].Body : null;
                if (body != null && !body.isKinematic)
                    body.AddForce(velocityChange, ForceMode.VelocityChange);
            }
        }

        public void Configure(
            uint configuredActorId,
            GravityWorldBehaviour configuredGravityWorld,
            Rigidbody configuredRootBody,
            PlanetMotor configuredMotor,
            PhysicalImpactTarget configuredImpactTarget,
            Transform configuredChest,
            ActiveRagdollJoint[] configuredJoints,
            Collider[] configuredSelfColliders)
        {
            actorId = Math.Max(1u, configuredActorId);
            gravityWorld = configuredGravityWorld;
            rootBody = configuredRootBody;
            motor = configuredMotor;
            impactTarget = configuredImpactTarget;
            chest = configuredChest;
            joints = configuredJoints ?? Array.Empty<ActiveRagdollJoint>();
            selfColliders = configuredSelfColliders ?? Array.Empty<Collider>();
            Initialize();
        }

        public void ConfigureControlBehaviours(params Behaviour[] behaviours)
        {
            disabledDuringRagdoll = behaviours ?? Array.Empty<Behaviour>();
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (impactTarget != null)
            {
                impactTarget.ImpactApplied += HandleImpact;
            }
        }

        private void OnDisable()
        {
            if (impactTarget != null)
            {
                impactTarget.ImpactApplied -= HandleImpact;
            }
        }

        public void InjectImpact(float impulse)
        {
            if (Time.time < _suppressImpactsUntil) return;
            EnsureController();
            _controller.ApplyImpact(Mathf.Max(0f, impulse), Mathf.Max(rootBody.mass, 0.01f));
        }

        public void ReceiveImpact(in ImpactEvent impact)
        {
            InjectImpact(impact.Impulse);
            ImpactObserved?.Invoke(ToVector3(impact.Point), impact.Impulse);
        }

        private void FixedUpdate()
        {
            if (rootBody == null)
            {
                return;
            }

            using (FixedTickMarker.Auto())
            {
                EnsureController();
                SampleGravity();
                UpdateSupport();
                Transform chestTransform = chest != null ? chest : transform;
                var frame = new CharacterPhysicalFrame(
                    Time.fixedDeltaTime,
                    ToFloat3(_gravityUp),
                    ToFloat3(rootBody.worldCenterOfMass),
                    ToFloat3(_supportCenter),
                    _contactCount,
                    ToFloat3(rootBody.linearVelocity),
                    ToFloat3(rootBody.angularVelocity),
                    ToFloat3(chestTransform.up),
                    ToFloat3(chestTransform.right));
                CurrentState = _controller.Step(in frame);
                PublishStateIfChanged();
                ApplyControl(CurrentState);
            }
        }

        private void ApplyControl(CharacterPhysicalState state)
        {
            bool motorAllowed = state.Mode == CharacterPhysicalMode.AnimatedMotor ||
                                state.Mode == CharacterPhysicalMode.PhysicalAssist ||
                                state.Mode == CharacterPhysicalMode.Stagger;
            if (motor != null)
            {
                motor.enabled = motorAllowed;
            }
            for (int index = 0; index < disabledDuringRagdoll.Length; index++)
            {
                Behaviour behaviour = disabledDuringRagdoll[index];
                if (behaviour != null && behaviour.enabled != motorAllowed)
                    behaviour.enabled = motorAllowed;
            }

            MaximumJointError = 0f;
            for (int index = 0; index < joints.Length; index++)
            {
                ActiveRagdollJoint joint = joints[index];
                if (joint == null)
                {
                    continue;
                }

                joint.ApplyPose(state.MuscleStrength);
                MaximumJointError = Mathf.Max(MaximumJointError, joint.JointErrorDegrees);
            }

            LastBalanceTorque = Vector3.zero;
            if (_contactCount <= 0 ||
                (state.Mode != CharacterPhysicalMode.PhysicalAssist && state.Mode != CharacterPhysicalMode.Stagger))
            {
                return;
            }

            float3 torque = BalanceControllerMath.ComputeCorrectiveTorque(
                ToFloat3(rootBody.worldCenterOfMass),
                ToFloat3(_supportCenter),
                ToFloat3(_gravityUp),
                balanceGain,
                maximumBalanceTorque);
            LastBalanceTorque = ToVector3(torque);
            rootBody.AddTorque(LastBalanceTorque, ForceMode.Acceleration);
        }

        private void SampleGravity()
        {
            if (gravityWorld == null || !gravityWorld.IsReady)
            {
                _gravityUp = transform.up;
                return;
            }

            Vector3 position = rootBody.worldCenterOfMass;
            GravitySample sample = gravityWorld.World.Sample(ToFloat3(position), 0u);
            if (sample.IsFinite && math.lengthsq(sample.Up) > 0.5f)
            {
                _gravityUp = ToVector3(math.normalizesafe(sample.Up, new float3(0f, 1f, 0f)));
            }
        }

        private void UpdateSupport()
        {
            Vector3 origin = rootBody.worldCenterOfMass + (_gravityUp * 0.15f);
            int count = UnityEngine.Physics.RaycastNonAlloc(
                origin,
                -_gravityUp,
                _groundHits,
                groundProbeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;
            _contactCount = 0;
            _supportCenter = rootBody.worldCenterOfMass - (_gravityUp * 0.9f);
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = _groundHits[index];
                if (hit.rigidbody == rootBody || IsSelfCollider(hit.collider) || hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                _supportCenter = hit.point;
                _contactCount = 1;
            }
        }

        private bool IsSelfCollider(Collider candidate)
        {
            for (int index = 0; index < selfColliders.Length; index++)
            {
                if (selfColliders[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleImpact(Vector3 point, float impulse)
        {
            InjectImpact(impulse);
            ImpactObserved?.Invoke(point, impulse);
        }

        private void PublishStateIfChanged()
        {
            if (_hasPublishedMode && CurrentState.Mode == _lastPublishedMode)
            {
                return;
            }

            _hasPublishedMode = true;
            _lastPublishedMode = CurrentState.Mode;
            StateChanged?.Invoke(CurrentState);
        }

        private void Initialize()
        {
            if (rootBody == null)
            {
                rootBody = GetComponent<Rigidbody>();
            }

            if (motor == null)
            {
                motor = GetComponent<PlanetMotor>();
            }

            if (impactTarget == null)
            {
                impactTarget = GetComponent<PhysicalImpactTarget>();
            }

            EnsureController();
            ConfigureSelfCollisionFiltering();
        }

        private void EnsureController()
        {
            if (_controller == null)
            {
                _controller = new CharacterPhysicalController(
                    new ActorId(Math.Max(1u, actorId)),
                    CharacterPhysicalTuning.Default);
            }
        }

        private void ConfigureSelfCollisionFiltering()
        {
            for (int first = 0; first < selfColliders.Length; first++)
            {
                Collider a = selfColliders[first];
                if (a == null)
                {
                    continue;
                }

                for (int second = first + 1; second < selfColliders.Length; second++)
                {
                    Collider b = selfColliders[second];
                    if (b != null)
                    {
                        UnityEngine.Physics.IgnoreCollision(a, b, true);
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (rootBody == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(rootBody.worldCenterOfMass, 0.08f);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_supportCenter, 0.08f);
            Gizmos.DrawLine(rootBody.worldCenterOfMass, _supportCenter);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(rootBody.worldCenterOfMass, _gravityUp);

            for (int index = 0; index < joints.Length; index++)
            {
                ActiveRagdollJoint joint = joints[index];
                if (joint == null || joint.TargetPose == null)
                {
                    continue;
                }

                Gizmos.color = Color.Lerp(Color.green, Color.red, Mathf.Clamp01(joint.JointErrorDegrees / 90f));
                Gizmos.DrawLine(joint.transform.position, joint.TargetPose.position);
            }
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
