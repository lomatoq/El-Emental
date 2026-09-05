using System;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DefaultExecutionOrder(2000)]
    [DisallowMultipleComponent]
    public sealed class EarthPillarMobility : MonoBehaviour
    {
        private static readonly ProfilerMarker LaunchMarker =
            new ProfilerMarker("Elemental.Bending.EarthPillarLaunch");

        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private ActiveRagdollPuppet puppet;
        [SerializeField] private EarthSurfaceQueryService surfaceQueries;

        [Header("Charged launch feel")]
        [SerializeField, Min(0.1f)] private float fullChargeSeconds = 1.45f;
        [SerializeField, Min(0.1f)] private float minimumHeight = 2.2f;
        [SerializeField, Min(0.1f)] private float maximumHeight = 8.8f;
        [SerializeField, Min(0.1f)] private float minimumVelocityChange = 12f;
        [SerializeField, Min(0.1f)] private float maximumVelocityChange = 25f;
        [SerializeField, Min(0.1f)] private float minimumRadius = 0.76f;
        [SerializeField, Min(0.1f)] private float maximumRadius = 1.4f;
        [SerializeField, Min(0.05f)] private float minimumRiseSeconds = 0.20f;
        [SerializeField, Min(0.05f)] private float maximumRiseSeconds = 0.46f;
        [SerializeField, Range(0.25f, 4f)] private float chargeExponent = 1.55f;

        private EarthPillarLaunchProfile _profile = EarthPillarLaunchProfile.Default;
        private float _chargeStartedAt;
        private float _riseElapsed;
        private float _previousLift;
        private EarthPillarLaunchResult _pendingLaunch;
        private Vector3 _pendingUp;
        private bool _charging;
        private bool _launchPending;
        private uint _tick;
        private EarthSurfaceSample _launchSurface;

        public event Action<EarthPillarLaunchEvent> PillarRaised;
        public bool IsCharging => _charging;
        public bool IsLaunchPending => _launchPending;
        public bool CanLaunch => !_charging && !_launchPending && targetBody != null && motor != null &&
                                 motor.HasStableSupport;
        public float Charge01 => _charging
            ? EarthPillarLaunchSolver.Charge01(Time.unscaledTime - _chargeStartedAt, in _profile)
            : 0f;
        public EarthPillarLaunchResult LastLaunch { get; private set; }
        public EarthSurfaceSample LastLaunchSurface => _launchSurface;

        public bool TryResolveSupport(Vector3 origin, Vector3 up, out EarthSurfaceSample sample)
        {
            sample = default;
            if (surfaceQueries == null) return false;
            Vector3 safeUp = up.sqrMagnitude > 0.5f ? up.normalized : transform.up;
            var query = new EarthSurfaceQuery(
                ToFloat3(origin + (safeUp * 0.35f)),
                ToFloat3(-safeUp),
                4.25f,
                EarthSurfaceCapabilities.Support | EarthSurfaceCapabilities.Pillar,
                0.18f);
            return surfaceQueries.TrySample(in query, out sample);
        }

        public void Configure(
            Rigidbody body,
            PlanetMotor configuredMotor,
            EarthSurfaceQueryService configuredSurfaceQueries = null)
        {
            targetBody = body;
            motor = configuredMotor;
            surfaceQueries = configuredSurfaceQueries;
        }

        public bool BeginCharge()
        {
            if (_charging || _launchPending || targetBody == null || motor == null || !motor.HasStableSupport)
                return false;
            _charging = true;
            _chargeStartedAt = Time.unscaledTime;
            return true;
        }

        public bool ReleaseCharge()
        {
            if (!_charging) return false;
            float heldSeconds = Mathf.Max(0f, Time.unscaledTime - _chargeStartedAt);
            _charging = false;
            if (targetBody == null || motor == null || !motor.HasStableSupport) return false;

            LastLaunch = EarthPillarLaunchSolver.Solve(heldSeconds, in _profile);
            return ScheduleLaunch(LastLaunch);
        }

        /// <summary>
        /// Schedules the existing authored launch pillar at an explicit charge. This
        /// is the one-shot mobility seam used by the surf jump: it preserves the
        /// same support query, typed event, motor ownership and rise physics as a
        /// held Space release without manufacturing a same-frame input hold.
        /// </summary>
        public bool TryLaunchAtCharge(float charge01)
        {
            if (!CanLaunch) return false;
            LastLaunch = EarthPillarLaunchSolver.SolveCharge01(charge01, in _profile);
            return ScheduleLaunch(LastLaunch);
        }

        private bool ScheduleLaunch(EarthPillarLaunchResult launch)
        {
            if (targetBody == null || motor == null || !motor.HasStableSupport || _launchPending)
                return false;
            _pendingLaunch = launch;
            _pendingUp = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : transform.up;
            Vector3 surfaceBase = targetBody.worldCenterOfMass - (_pendingUp * 1.25f);
            _launchSurface = default;
            if (TryResolveSupport(targetBody.worldCenterOfMass, _pendingUp, out EarthSurfaceSample sample))
            {
                _launchSurface = sample;
                surfaceBase = ToVector3(sample.Point);
                _pendingUp = ToVector3(sample.Normal);
                InheritSurfaceVelocity(ToVector3(sample.Velocity), _pendingUp);
            }
            _riseElapsed = 0f;
            _previousLift = 0f;
            _launchPending = true;
            motor.BeginExternalLaunch(Mathf.CeilToInt(launch.RiseSeconds / Time.fixedDeltaTime) + 12);
            var raised = new EarthPillarLaunchEvent(
                _tick++,
                ToFloat3(surfaceBase),
                ToFloat3(_pendingUp),
                in _pendingLaunch);
            PillarRaised?.Invoke(raised);
            return true;
        }

        public void CancelCharge()
        {
            _charging = false;
        }

        private void Awake()
        {
            if (targetBody == null) targetBody = GetComponent<Rigidbody>();
            if (motor == null) motor = GetComponent<PlanetMotor>();
            if (puppet == null) puppet = GetComponent<ActiveRagdollPuppet>();
            if (surfaceQueries == null)
                surfaceQueries = FindFirstObjectByType<EarthSurfaceQueryService>();
            RebuildProfile();
        }

        private void OnValidate() => RebuildProfile();

        private void RebuildProfile()
        {
            _profile = new EarthPillarLaunchProfile(
                fullChargeSeconds,
                minimumHeight,
                maximumHeight,
                minimumVelocityChange,
                maximumVelocityChange,
                minimumRadius,
                maximumRadius,
                minimumRiseSeconds,
                maximumRiseSeconds,
                chargeExponent);
        }

        private void FixedUpdate()
        {
            if (!_launchPending || targetBody == null) return;

            using (LaunchMarker.Auto())
            {
                _riseElapsed = Mathf.Min(_pendingLaunch.RiseSeconds, _riseElapsed + Time.fixedDeltaTime);
                float rise01 = Mathf.Clamp01(_riseElapsed / Mathf.Max(0.05f, _pendingLaunch.RiseSeconds));
                float eased = rise01 * rise01 * (3f - (2f * rise01));
                float lift = _pendingLaunch.Height * eased;
                float desiredUpSpeed = (lift - _previousLift) / Mathf.Max(0.0001f, Time.fixedDeltaTime);
                float currentUpSpeed = Vector3.Dot(targetBody.linearVelocity, _pendingUp);
                ApplyVelocityChange(
                    _pendingUp * Mathf.Clamp(desiredUpSpeed - currentUpSpeed, -25f, 25f));
                _previousLift = lift;
                if (rise01 < 1f) return;

                _launchPending = false;
                currentUpSpeed = Vector3.Dot(targetBody.linearVelocity, _pendingUp);
                float gravityMagnitude = motor != null
                    ? motor.GravityAcceleration.magnitude
                    : 0f;
                if (!float.IsFinite(gravityMagnitude) || gravityMagnitude < 0.1f)
                    gravityMagnitude = 9.81f;
                // The eased pillar rise has already delivered the authored height.
                // Release adds only a compact airborne follow-through; using the
                // full height again doubles the arc and reads as zero gravity.
                float followThroughHeight = Mathf.Clamp(
                    _pendingLaunch.Height * 0.25f,
                    0.45f,
                    1.25f);
                float ballisticSpeed = Mathf.Sqrt(
                    2f * gravityMagnitude * followThroughHeight);
                float targetLaunchSpeed = Mathf.Min(
                    _pendingLaunch.VelocityChange,
                    ballisticSpeed);
                ApplyVelocityChange(
                    _pendingUp * Mathf.Clamp(
                        targetLaunchSpeed - currentUpSpeed,
                        -25f,
                        25f));
            }
        }

        private void ApplyVelocityChange(Vector3 velocityChange)
        {
            if (targetBody == null || !float.IsFinite(velocityChange.x) ||
                !float.IsFinite(velocityChange.y) || !float.IsFinite(velocityChange.z))
                return;
            targetBody.linearVelocity += velocityChange;
            puppet?.ApplyUniformVelocityChange(velocityChange, targetBody);
        }

        private void InheritSurfaceVelocity(Vector3 surfaceVelocity, Vector3 up)
        {
            Vector3 inherited = Vector3.ProjectOnPlane(surfaceVelocity, up);
            float speed = inherited.magnitude;
            if (speed <= 0.001f) return;
            Vector3 direction = inherited / speed;
            float currentAlong = Vector3.Dot(targetBody.linearVelocity, direction);
            if (currentAlong >= speed) return;
            ApplyVelocityChange(direction * (speed - currentAlong));
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
