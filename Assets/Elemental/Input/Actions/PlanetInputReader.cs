using Elemental.Simulation.Characters;
using Elemental.Runtime.Characters;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Input.Actions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.InputSystem.PlayerInput))]
    public sealed class PlanetInputReader : MonoBehaviour, IPlanetMotorInputSource
    {
        [SerializeField] private EarthInputAdapter inputAdapter;
        [SerializeField] private EarthPillarMobility earthPillarMobility;
        [SerializeField] private EarthPillarWaveAbility earthPillarWave;
        [SerializeField] private EarthLandingCushion earthLandingCushion;

        private bool _jumpQueued;
        private bool _shiftHeld;
        private float _shiftStartedAt;
        private JumpCastMode _jumpCastMode;

        public bool UsesEarthPillarMobility => earthPillarMobility != null;

        public void Configure(
            EarthInputAdapter configuredInputAdapter,
            EarthPillarMobility configuredEarthPillarMobility = null,
            EarthPillarWaveAbility configuredEarthPillarWave = null,
            EarthLandingCushion configuredLandingCushion = null)
        {
            inputAdapter = configuredInputAdapter;
            earthPillarMobility = configuredEarthPillarMobility;
            earthPillarWave = configuredEarthPillarWave;
            earthLandingCushion = configuredLandingCushion;
        }

        private void Awake()
        {
            if (inputAdapter == null) inputAdapter = GetComponent<EarthInputAdapter>();
            if (inputAdapter == null) inputAdapter = gameObject.AddComponent<EarthInputAdapter>();
            inputAdapter.Configure(GetComponent<UnityEngine.InputSystem.PlayerInput>());
        }

        private void OnEnable()
        {
            if (inputAdapter == null)
            {
                Debug.LogError("[Elemental] Earth input adapter is not configured.", this);
                enabled = false;
                return;
            }
            inputAdapter.JumpPerformed += HandleJumpPerformed;
            inputAdapter.JumpStarted += HandleJumpStarted;
            inputAdapter.JumpCanceled += HandleJumpCanceled;
        }

        private void OnDisable()
        {
            if (inputAdapter != null)
            {
                inputAdapter.JumpPerformed -= HandleJumpPerformed;
                inputAdapter.JumpStarted -= HandleJumpStarted;
                inputAdapter.JumpCanceled -= HandleJumpCanceled;
            }
            earthPillarMobility?.CancelCharge();
            earthPillarWave?.CancelCharge();
            earthLandingCushion?.EndHold();
            _jumpCastMode = JumpCastMode.None;
        }

        private void Update()
        {
            bool shift = inputAdapter != null && inputAdapter.BendModifierHeld;
            if (shift && !_shiftHeld) _shiftStartedAt = Time.unscaledTime;
            _shiftHeld = shift;
            if (_jumpCastMode == JumpCastMode.Wave && shift)
                earthPillarWave?.SetShiftHeldSeconds(Time.unscaledTime - _shiftStartedAt);
        }

        public PlanetMotorCommand SampleCommand(uint tick)
        {
            Vector2 move = inputAdapter != null ? inputAdapter.Move : Vector2.zero;
            bool jump = _jumpQueued;
            _jumpQueued = false;
            return new PlanetMotorCommand(tick, new float2(move.x, move.y), jump);
        }

        private void HandleJumpPerformed()
        {
            if (earthPillarMobility != null || earthPillarWave != null) return;
            _jumpQueued = true;
        }

        private void HandleJumpStarted()
        {
            if (_shiftHeld && earthPillarWave != null)
            {
                _jumpCastMode = earthPillarWave.BeginCharge(Time.unscaledTime - _shiftStartedAt)
                    ? JumpCastMode.Wave
                    : JumpCastMode.None;
                return;
            }
            if (earthLandingCushion != null && earthLandingCushion.BeginHold())
            {
                _jumpCastMode = JumpCastMode.Cushion;
                return;
            }
            _jumpCastMode = earthPillarMobility != null && earthPillarMobility.BeginCharge()
                ? JumpCastMode.Pillar
                : JumpCastMode.None;
        }

        private void HandleJumpCanceled()
        {
            if (_jumpCastMode == JumpCastMode.Wave) earthPillarWave?.ReleaseCharge();
            else if (_jumpCastMode == JumpCastMode.Pillar) earthPillarMobility?.ReleaseCharge();
            else if (_jumpCastMode == JumpCastMode.Cushion) earthLandingCushion?.EndHold();
            _jumpCastMode = JumpCastMode.None;
        }

        private enum JumpCastMode : byte
        {
            None,
            Pillar,
            Wave,
            Cushion
        }
    }
}
