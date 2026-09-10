using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public sealed partial class PlanetMotor
    {
        public uint TeleportSequence { get; private set; }

        /// <summary>Clear motion history after an explicit authoritative relocation.</summary>
        public void ResetAfterTeleport()
        {
            SetFireLift(0f,false);SetFireLowFlight(false);
            ResolveReferences();
            ResetLocomotionMotion();
            CancelMantle();
            _movingSupport = default;
            _movingSupportTicks = 0;
            _lastCarrySurfaceId = _lastCarryGeneration = 0;
            _lastCarrySurfaceVelocity = Vector3.zero;
            _groundSupportSelection = CharacterSupportSelection.None;
            _groundContactCount = 0;
            _groundDistance = groundProbeDistance;
            IsGrounded = false;
            _ignoreGroundTicks = _directedExternalMotionTicks = 0;
            _jumpWindow = default;
            _landingRoll = default;
            _hasRollSample = _previousRollSupported = false;
            _rollJumpIntentUntil = 0f;
            _castBrace01 = 0f;
            LastCommand = default;
            _localUp = transform.up;
            _groundNormal = _localUp;
            _aimForward = transform.forward;
            _hasAimForward = true;
            TeleportSequence++;
        }
    }
}
