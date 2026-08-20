using System;
using Elemental.Simulation.Bending;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Elemental.Input.Actions
{
    /// <summary>
    /// The only gameplay component allowed to read Unity Input System actions.
    /// Consumers observe semantic bending controls and never query devices directly.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class EarthInputAdapter : MonoBehaviour
    {
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private string actionMapName = "Gameplay";

        private InputActionMap _map;
        private InputAction _move;
        private InputAction _jumpOrStomp;
        private InputAction _bendPrimary;
        private InputAction _bendForce;
        private InputAction _bendField;
        private InputAction _bendModifier;
        private InputAction _bendParameter;
        private InputAction _cancel;
        private InputAction _shoulderSwap;
        private InputAction _pointer;
        private InputAction _ability1;
        private InputAction _ability2;
        private InputAction _ability3;
        private InputAction _ability4;
        private InputAction _elementFire;
        private InputAction _elementWater;

        public event Action JumpStarted;
        public event Action JumpPerformed;
        public event Action JumpCanceled;

        public Vector2 Move => _move?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 PointerPixels => _pointer?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 PointerViewport01 => ScreenToViewport(PointerPixels);
        public float BendParameter => _bendParameter?.ReadValue<float>() ?? 0f;
        public bool BendPrimaryPressed => _bendPrimary?.WasPressedThisFrame() == true;
        public bool BendPrimaryReleased => _bendPrimary?.WasReleasedThisFrame() == true;
        public bool BendPrimaryHeld => _bendPrimary?.IsPressed() == true;
        public bool BendForcePressed => _bendForce?.WasPressedThisFrame() == true;
        public bool BendForceReleased => _bendForce?.WasReleasedThisFrame() == true;
        public bool BendForceHeld => _bendForce?.IsPressed() == true;
        public bool BendFieldPressed => _bendField?.WasPressedThisFrame() == true;
        public bool BendFieldReleased => _bendField?.WasReleasedThisFrame() == true;
        public bool BendFieldHeld => _bendField?.IsPressed() == true;
        public bool BendModifierHeld => _bendModifier?.IsPressed() == true;
        public bool JumpPressed => _jumpOrStomp?.WasPressedThisFrame() == true;
        public bool JumpReleased => _jumpOrStomp?.WasReleasedThisFrame() == true;
        public bool JumpHeld => _jumpOrStomp?.IsPressed() == true;
        public bool CancelPressed => _cancel?.WasPressedThisFrame() == true;
        public bool ShoulderSwapPressed => _shoulderSwap?.WasPressedThisFrame() == true;
        public bool ElementFirePressed => _elementFire?.WasPressedThisFrame() == true;
        public bool ElementWaterPressed => _elementWater?.WasPressedThisFrame() == true;

        public EarthGestureFrame CaptureEarthGestureFrame(
            bool grounded,
            bool descending,
            bool landingWaveArmed,
            bool pointerOverEarthTarget,
            bool hasControlledTarget,
            bool hasPrimedQuickStone,
            bool hasRepairTarget,
            float primaryHeldSeconds,
            float pointerTravelViewport)
        {
            Vector2 move = Move;
            return new EarthGestureFrame(
                CancelPressed,
                false,
                grounded,
                descending,
                move.magnitude,
                BendModifierHeld,
                _jumpOrStomp?.WasPressedThisFrame() == true,
                landingWaveArmed,
                BendPrimaryPressed,
                BendPrimaryHeld,
                BendPrimaryReleased,
                primaryHeldSeconds,
                pointerTravelViewport,
                BendForceHeld,
                BendFieldHeld,
                pointerOverEarthTarget,
                hasControlledTarget,
                hasPrimedQuickStone,
                hasRepairTarget);
        }

        public void Configure(PlayerInput configuredPlayerInput)
        {
            if (playerInput == configuredPlayerInput && _map != null) return;
            Unbind();
            playerInput = configuredPlayerInput != null
                ? configuredPlayerInput
                : GetComponent<PlayerInput>();
            if (isActiveAndEnabled) Bind();
        }

        public bool DebugAbilityPressed(int oneBasedSlot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            InputAction action = oneBasedSlot switch
            {
                1 => _ability1,
                2 => _ability2,
                3 => _ability3,
                4 => _ability4,
                _ => null
            };
            return action?.WasPressedThisFrame() == true;
#else
            return false;
#endif
        }

        public static Vector2 ScreenToViewport(Vector2 pixels)
        {
            return new Vector2(
                pixels.x / Mathf.Max(1f, Screen.width),
                pixels.y / Mathf.Max(1f, Screen.height));
        }

        public static Vector2 ViewportToScreen(Vector2 viewport01)
        {
            return new Vector2(viewport01.x * Screen.width, viewport01.y * Screen.height);
        }

        private void Awake()
        {
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable() => Bind();

        private void OnDisable() => Unbind();

        private void Bind()
        {
            if (_map != null) return;
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
            _map = playerInput != null
                ? playerInput.actions?.FindActionMap(actionMapName, false)
                : null;
            if (_map == null)
            {
                Debug.LogError("[Elemental] Gameplay input map is not configured.", this);
                return;
            }

            _move = Find("Move", true);
            _jumpOrStomp = Find("JumpOrStomp", true);
            _bendPrimary = Find("BendPrimary", true);
            _bendForce = Find("BendForce", true);
            _bendField = Find("BendField", true);
            _bendModifier = Find("BendModifier", true);
            _bendParameter = Find("BendParameter", true);
            _cancel = Find("Cancel", true);
            _shoulderSwap = Find("ShoulderSwap", true);
            _pointer = Find("Pointer", true);
            _ability1 = Find("Ability1", false);
            _ability2 = Find("Ability2", false);
            _ability3 = Find("Ability3", false);
            _ability4 = Find("Ability4", false);
            _elementFire = Find("ElementFire", false);
            _elementWater = Find("ElementWater", false);

            _jumpOrStomp.started += HandleJumpStarted;
            _jumpOrStomp.performed += HandleJumpPerformed;
            _jumpOrStomp.canceled += HandleJumpCanceled;
            _map.Enable();
        }

        private InputAction Find(string actionName, bool required)
        {
            InputAction action = _map.FindAction(actionName, false);
            if (required && action == null)
                Debug.LogError($"[Elemental] Required input action '{actionName}' is missing.", this);
            return action;
        }

        private void Unbind()
        {
            if (_jumpOrStomp != null)
            {
                _jumpOrStomp.started -= HandleJumpStarted;
                _jumpOrStomp.performed -= HandleJumpPerformed;
                _jumpOrStomp.canceled -= HandleJumpCanceled;
            }
            _map = null;
            _move = null;
            _jumpOrStomp = null;
            _bendPrimary = null;
            _bendForce = null;
            _bendField = null;
            _bendModifier = null;
            _bendParameter = null;
            _cancel = null;
            _shoulderSwap = null;
            _pointer = null;
            _ability1 = null;
            _ability2 = null;
            _ability3 = null;
            _ability4 = null;
            _elementFire = null;
            _elementWater = null;
        }

        private void HandleJumpStarted(InputAction.CallbackContext _) => JumpStarted?.Invoke();
        private void HandleJumpPerformed(InputAction.CallbackContext _) => JumpPerformed?.Invoke();
        private void HandleJumpCanceled(InputAction.CallbackContext _) => JumpCanceled?.Invoke();
    }
}
