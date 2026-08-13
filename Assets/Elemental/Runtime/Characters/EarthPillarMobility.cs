using System;
using Elemental.Simulation.Bending;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DisallowMultipleComponent]
    public sealed class EarthPillarMobility : MonoBehaviour
    {
        private static readonly ProfilerMarker LaunchMarker =
            new ProfilerMarker("Elemental.Bending.EarthPillarLaunch");

        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private ActiveRagdollPuppet puppet;

        private EarthPillarLaunchProfile _profile = EarthPillarLaunchProfile.Default;
        private float _chargeStartedAt;
        private float _riseElapsed;
        private float _previousLift;
        private EarthPillarLaunchResult _pendingLaunch;
        private Vector3 _pendingUp;
        private bool _charging;
        private bool _launchPending;
        private uint _tick;

        public event Action<EarthPillarLaunchEvent> PillarRaised;
        public bool IsCharging => _charging;
        public float Charge01 => _charging
            ? EarthPillarLaunchSolver.Charge01(Time.unscaledTime - _chargeStartedAt, in _profile)
            : 0f;
        public EarthPillarLaunchResult LastLaunch { get; private set; }

        public void Configure(Rigidbody body, PlanetMotor configuredMotor)
        {
            targetBody = body;
            motor = configuredMotor;
        }

        public bool BeginCharge()
        {
            if (_charging || _launchPending || targetBody == null || motor == null || !motor.IsGrounded)
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
            if (targetBody == null || motor == null || !motor.IsGrounded) return false;

            LastLaunch = EarthPillarLaunchSolver.Solve(heldSeconds, in _profile);
            _pendingLaunch = LastLaunch;
            _pendingUp = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : transform.up;
            _riseElapsed = 0f;
            _previousLift = 0f;
            _launchPending = true;
            motor.BeginExternalLaunch(Mathf.CeilToInt(LastLaunch.RiseSeconds / Time.fixedDeltaTime) + 12);
            Vector3 surfaceBase = targetBody.worldCenterOfMass - (_pendingUp * 1.25f);
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
                ApplyVelocityChange(
                    _pendingUp * Mathf.Max(0f, _pendingLaunch.VelocityChange - currentUpSpeed));
            }
        }

        private void ApplyVelocityChange(Vector3 velocityChange)
        {
            if (puppet != null)
                puppet.ApplyUniformVelocityChange(velocityChange);
            else
                targetBody.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }
}
