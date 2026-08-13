using System;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DisallowMultipleComponent]
    public sealed class EarthPillarWaveAbility : MonoBehaviour
    {
        [SerializeField] private Rigidbody casterBody;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private EarthPillarWavePool wavePool;
        [SerializeField] private EarthPillarWaveProfile profile;

        private float _powerStartedAt;
        private float _sectorCharge01;
        private bool _charging;

        public event Action<float, float> ChargeChanged;
        public bool IsCharging => _charging;
        public float SectorCharge01 => _sectorCharge01;
        public float PowerCharge01 => !_charging ? 0f : Mathf.Clamp01(
            (Time.unscaledTime - _powerStartedAt) /
            (profile != null ? profile.FullPowerChargeSeconds : 1.1f));
        public int LastColumnCount { get; private set; }
        public EarthTechniqueRejectReason LastRejection { get; private set; }

        public void Configure(
            Rigidbody body,
            PlanetMotor configuredMotor,
            EarthPillarWavePool configuredPool,
            EarthPillarWaveProfile configuredProfile)
        {
            casterBody = body;
            motor = configuredMotor;
            wavePool = configuredPool;
            profile = configuredProfile;
        }

        public bool BeginCharge(float shiftHeldSeconds)
        {
            if (_charging || casterBody == null || motor == null || wavePool == null)
            {
                LastRejection = EarthTechniqueRejectReason.RuntimeUnavailable;
                return false;
            }
            if (!motor.IsGrounded)
            {
                LastRejection = EarthTechniqueRejectReason.NotGrounded;
                return false;
            }
            LastRejection = EarthTechniqueRejectReason.None;
            _charging = true;
            _powerStartedAt = Time.unscaledTime;
            SetShiftHeldSeconds(shiftHeldSeconds);
            ChargeChanged?.Invoke(_sectorCharge01, 0f);
            return true;
        }

        public void SetShiftHeldSeconds(float seconds)
        {
            if (!_charging) return;
            float duration = profile != null ? profile.FullSectorChargeSeconds : 1.4f;
            _sectorCharge01 = Mathf.Max(_sectorCharge01, Mathf.Clamp01(seconds / duration));
        }

        public bool ReleaseCharge()
        {
            if (!_charging) return false;
            float power = PowerCharge01;
            _charging = false;
            if (casterBody == null || motor == null || wavePool == null || !motor.IsGrounded) return false;
            Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : transform.up;
            Vector3 surface = casterBody.worldCenterOfMass - (up * 1.25f);
            LastColumnCount = wavePool.Launch(
                surface, up, motor.FacingForward, _sectorCharge01, power, casterBody);
            ChargeChanged?.Invoke(0f, 0f);
            return true;
        }

        public bool TryCast(
            Vector3 forward,
            float sector01,
            float power01,
            out EarthTechniqueRejectReason rejection)
        {
            if (casterBody == null || motor == null || wavePool == null)
            {
                rejection = LastRejection = EarthTechniqueRejectReason.RuntimeUnavailable;
                return false;
            }
            if (!motor.IsGrounded)
            {
                rejection = LastRejection = EarthTechniqueRejectReason.NotGrounded;
                return false;
            }

            Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : transform.up;
            Vector3 tangentForward = Vector3.ProjectOnPlane(forward, up).normalized;
            if (tangentForward.sqrMagnitude < 0.5f) tangentForward = motor.FacingForward;
            Vector3 surface = casterBody.worldCenterOfMass - (up * 1.25f);
            LastColumnCount = wavePool.Launch(
                surface,
                up,
                tangentForward,
                Mathf.Clamp01(sector01),
                Mathf.Clamp01(power01),
                casterBody);
            rejection = LastColumnCount > 0
                ? EarthTechniqueRejectReason.None
                : EarthTechniqueRejectReason.PoolExhausted;
            LastRejection = rejection;
            ChargeChanged?.Invoke(0f, 0f);
            return rejection == EarthTechniqueRejectReason.None;
        }

        public void CancelCharge()
        {
            _charging = false;
            ChargeChanged?.Invoke(0f, 0f);
        }

        private void Update()
        {
            if (_charging) ChargeChanged?.Invoke(_sectorCharge01, PowerCharge01);
        }

        private void Awake()
        {
            if (casterBody == null) casterBody = GetComponent<Rigidbody>();
            if (motor == null) motor = GetComponent<PlanetMotor>();
        }
    }
}
