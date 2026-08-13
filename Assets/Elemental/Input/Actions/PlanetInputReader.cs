using Elemental.Simulation.Characters;
using Elemental.Runtime.Characters;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Elemental.Input.Actions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlanetInputReader : MonoBehaviour, IPlanetMotorInputSource
    {
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private string actionMapName = "Gameplay";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private EarthPillarMobility earthPillarMobility;
        [SerializeField] private EarthPillarWaveAbility earthPillarWave;
        [SerializeField] private EarthLandingCushion earthLandingCushion;

        private InputAction _moveAction;
        private InputAction _jumpAction;
        private bool _jumpQueued;
        private bool _shiftHeld;
        private float _shiftStartedAt;
        private JumpCastMode _jumpCastMode;

        public bool UsesEarthPillarMobility => earthPillarMobility != null;

        public void Configure(
            PlayerInput configuredPlayerInput,
            EarthPillarMobility configuredEarthPillarMobility = null,
            EarthPillarWaveAbility configuredEarthPillarWave = null,
            EarthLandingCushion configuredLandingCushion = null)
        {
            playerInput = configuredPlayerInput;
            earthPillarMobility = configuredEarthPillarMobility;
            earthPillarWave = configuredEarthPillarWave;
            earthLandingCushion = configuredLandingCushion;
        }

        private void Awake()
        {
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }
        }

        private void OnEnable()
        {
            InputActionMap map = playerInput.actions?.FindActionMap(actionMapName, true);
            _moveAction = map?.FindAction(moveActionName, true);
            _jumpAction = map?.FindAction(jumpActionName, true);

            if (_moveAction == null || _jumpAction == null)
            {
                Debug.LogError("[Elemental] Gameplay Move/Jump actions are not configured.", this);
                enabled = false;
                return;
            }

            _jumpAction.performed += HandleJumpPerformed;
            _jumpAction.started += HandleJumpStarted;
            _jumpAction.canceled += HandleJumpCanceled;
            map.Enable();
        }

        private void OnDisable()
        {
            if (_jumpAction != null)
            {
                _jumpAction.performed -= HandleJumpPerformed;
                _jumpAction.started -= HandleJumpStarted;
                _jumpAction.canceled -= HandleJumpCanceled;
            }
            earthPillarMobility?.CancelCharge();
            earthPillarWave?.CancelCharge();
            earthLandingCushion?.EndHold();
            _jumpCastMode = JumpCastMode.None;
        }

        private void Update()
        {
            bool shift = Keyboard.current != null &&
                         (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            if (shift && !_shiftHeld) _shiftStartedAt = Time.unscaledTime;
            _shiftHeld = shift;
            if (_jumpCastMode == JumpCastMode.Wave && shift)
                earthPillarWave?.SetShiftHeldSeconds(Time.unscaledTime - _shiftStartedAt);
        }

        public PlanetMotorCommand SampleCommand(uint tick)
        {
            Vector2 move = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            bool jump = _jumpQueued;
            _jumpQueued = false;
            return new PlanetMotorCommand(tick, new float2(move.x, move.y), jump);
        }

        private void HandleJumpPerformed(InputAction.CallbackContext context)
        {
            if (earthPillarMobility != null || earthPillarWave != null) return;
            _jumpQueued = true;
        }

        private void HandleJumpStarted(InputAction.CallbackContext context)
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

        private void HandleJumpCanceled(InputAction.CallbackContext context)
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
