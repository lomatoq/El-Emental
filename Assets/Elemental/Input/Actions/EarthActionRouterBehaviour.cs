using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Bending;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Input.Actions
{
    [DefaultExecutionOrder(-800)]
    [DisallowMultipleComponent]
    public sealed class EarthActionRouterBehaviour : MonoBehaviour
    {
        [SerializeField] private EarthInputAdapter inputAdapter;
        [SerializeField] private PlanetInputReader motorInput;
        [SerializeField] private MagicInputController magicInput;
        [SerializeField] private EarthPillarWaveAbility waveAbility;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private Rigidbody casterBody;
        [SerializeField] private UnityEngine.Camera castCamera;
        [SerializeField] private EarthResonanceController resonanceController;
        [SerializeField] private EarthSurfController surfController;
        [SerializeField] private EarthDualMouseAbilityController dualMouseAbilities;

        private readonly EarthActionRouter _router = new EarthActionRouter();
        private readonly DualMouseEarthGestureSolver _dualMouse = new DualMouseEarthGestureSolver();
        private EarthActionRoute _current;
        private float _waveChargeStartedAt;
        private Vector2 _bufferedPrimaryPointer;
        private Vector2 _bufferedForcePointer;
        private readonly Vector2[] _bufferedPrimaryPath = new Vector2[16];
        private int _bufferedPrimaryPathCount;

        public EarthActionRoute Current => _current;
        public EarthActionOwner Owner => _dualMouse.OwnsInput ||
                                         _current.Owner == EarthActionOwner.DualMouseEarth
            ? EarthActionOwner.DualMouseEarth
            : _router.Owner;
        public EarthInputChordState ChordState => _router.ChordState;
        public float ResonanceCharge01 => resonanceController != null ? resonanceController.Charge01 : 0f;
        public int ResonanceStoneCount => resonanceController != null ? resonanceController.ActiveStoneCount : 0;
        public bool ResonanceVolleyActive => resonanceController != null && resonanceController.IsVolleyActive;
        public float SurfSpeed => surfController != null ? surfController.Speed : 0f;
        public bool AllowsPrimaryMagic => _current.Owner == EarthActionOwner.Primary ||
                                          _router.Owner == EarthActionOwner.Primary;
        public bool AllowsVectorField => _current.Owner == EarthActionOwner.VectorField ||
                                         _router.Owner == EarthActionOwner.VectorField;
        public bool AllowsGravity => _current.Owner == EarthActionOwner.Gravity ||
                                     _router.Owner == EarthActionOwner.Gravity;
        public bool AllowsArmor => _current.Owner == EarthActionOwner.Armor ||
                                   _router.Owner == EarthActionOwner.Armor;

        public bool Consumes(EarthInputConsumption input) => _current.Consumes(input);

        public void Configure(
            EarthInputAdapter configuredInput,
            PlanetInputReader configuredMotorInput,
            MagicInputController configuredMagic,
            EarthPillarWaveAbility configuredWave)
        {
            inputAdapter = configuredInput;
            motorInput = configuredMotorInput;
            magicInput = configuredMagic;
            waveAbility = configuredWave;
            if (motor == null) motor = GetComponent<PlanetMotor>();
            if (casterBody == null) casterBody = GetComponent<Rigidbody>();
            if (castCamera == null) castCamera = UnityEngine.Camera.main;
            if (resonanceController == null) resonanceController = GetComponent<EarthResonanceController>();
            if (surfController == null) surfController = GetComponent<EarthSurfController>();
            if (dualMouseAbilities == null)
                dualMouseAbilities = GetComponent<EarthDualMouseAbilityController>() ??
                                     gameObject.AddComponent<EarthDualMouseAbilityController>();
            dualMouseAbilities.Configure(
                magicInput != null ? magicInput.EarthExecutor : null,
                FindAnyObjectByType<EarthPillarWavePool>(FindObjectsInactive.Include),
                motor,
                castCamera,
                casterBody);
        }

        private void Awake()
        {
            if (inputAdapter == null) inputAdapter = GetComponent<EarthInputAdapter>();
            if (motorInput == null) motorInput = GetComponent<PlanetInputReader>();
            if (magicInput == null) magicInput = GetComponent<MagicInputController>();
            if (waveAbility == null) waveAbility = GetComponent<EarthPillarWaveAbility>();
            if (motor == null) motor = GetComponent<PlanetMotor>();
            if (casterBody == null) casterBody = GetComponent<Rigidbody>();
            if (dualMouseAbilities == null)
                dualMouseAbilities = GetComponent<EarthDualMouseAbilityController>();
        }

        private void OnDisable()
        {
            waveAbility?.CancelCharge();
            motorInput?.RouteCancel();
            _router.Reset();
            _dualMouse.Reset();
            _bufferedPrimaryPointer = default;
            _bufferedForcePointer = default;
            _bufferedPrimaryPathCount = 0;
            dualMouseAbilities?.CancelStompStone();
            _current = default;
        }

        private void Update()
        {
            if (inputAdapter == null) return;
            Vector2 move = inputAdapter.Move;
            Vector3 up = motor != null && motor.LocalUp.sqrMagnitude > 0.5f
                ? motor.LocalUp.normalized
                : transform.up;
            bool descending = casterBody != null && Vector3.Dot(casterBody.linearVelocity, up) < -0.15f;
            bool stableSupport = motor != null && motor.HasStableSupport;
            if (!stableSupport && surfController != null && inputAdapter.BendModifierHeld &&
                move.y >= EarthActionRouter.DefaultSurfForwardThreshold &&
                !inputAdapter.BendPrimaryHeld && !inputAdapter.BendForceHeld && !inputAdapter.BendFieldHeld)
                stableSupport = surfController.HasNearbyStartSurface();
            Vector2 pointerViewport = inputAdapter.PointerViewport01;
            if (dualMouseAbilities != null && dualMouseAbilities.IsStompStoneActive)
                dualMouseAbilities.UpdateStompAim(inputAdapter.PointerPixels);
            bool handsAvailable = !_router.HasActiveSession &&
                                  magicInput != null &&
                                  magicInput.SelectedElement == Elemental.Simulation.Magic.ElementId.Earth &&
                                  (magicInput.CurrentBendPhase == BendPhase.Idle ||
                                   magicInput.CurrentBendPhase == BendPhase.Cancelled) &&
                                  !magicInput.IsArmorActive &&
                                  !magicInput.IsQuickStonePrimed;
            DualMouseEarthGestureResult dual = default;
            if (_dualMouse.OwnsInput || handsAvailable)
            {
                if (!_dualMouse.OwnsInput)
                {
                    if (inputAdapter.BendPrimaryPressed)
                    {
                        _bufferedPrimaryPointer = inputAdapter.PointerPixels;
                        BeginBufferedPrimaryPath(_bufferedPrimaryPointer);
                    }
                    if (inputAdapter.BendForcePressed)
                        _bufferedForcePointer = inputAdapter.PointerPixels;
                }
                var dualFrame = new DualMouseEarthGestureFrame(
                    Time.unscaledTime,
                    inputAdapter.BendPrimaryPressed,
                    inputAdapter.BendPrimaryHeld,
                    inputAdapter.BendPrimaryReleased,
                    inputAdapter.BendForcePressed,
                    inputAdapter.BendForceHeld,
                    inputAdapter.BendForceReleased,
                    new float2(pointerViewport.x, pointerViewport.y),
                    inputAdapter.CancelPressed);
                // Capture the current pointer before Step can resolve and reset a
                // pending single-button session. In a fast press-drag-release the
                // release frame may contain the only meaningful motion sample; if
                // it is appended after Step, the wall path collapses to a tap.
                if (_dualMouse.OwnsInput && _bufferedPrimaryPathCount > 0)
                    AppendBufferedPrimaryPath(inputAdapter.PointerPixels);
                dual = _dualMouse.Step(in dualFrame);
            }

            if (dual.Kind is DualMouseEarthResultKind.Pending or DualMouseEarthResultKind.Tracking or
                DualMouseEarthResultKind.StompStone or DualMouseEarthResultKind.PillarCrest or
                DualMouseEarthResultKind.Cancel)
            {
                EarthActionIntentKind intent = dual.Kind switch
                {
                    DualMouseEarthResultKind.StompStone => EarthActionIntentKind.StompStone,
                    DualMouseEarthResultKind.PillarCrest => EarthActionIntentKind.PillarCrest,
                    DualMouseEarthResultKind.Cancel => EarthActionIntentKind.Cancel,
                    _ => EarthActionIntentKind.None
                };
                EarthActionRoutePhase phase = dual.Kind is DualMouseEarthResultKind.StompStone or
                    DualMouseEarthResultKind.PillarCrest
                    ? EarthActionRoutePhase.Commit
                    : dual.Kind == DualMouseEarthResultKind.Cancel
                        ? EarthActionRoutePhase.Cancel
                        : EarthActionRoutePhase.Continue;
                _current = new EarthActionRoute(
                    EarthActionOwner.DualMouseEarth,
                    phase,
                    intent,
                    EarthInputConsumption.Primary | EarthInputConsumption.Force,
                    dual.CrestCount / 7f);
                if (dual.Kind == DualMouseEarthResultKind.StompStone)
                    dualMouseAbilities?.CastStompStone(inputAdapter.PointerPixels);
                else if (dual.Kind == DualMouseEarthResultKind.PillarCrest)
                    dualMouseAbilities?.CastPillarCrest(
                        new Vector2(dual.StartPointer.x, dual.StartPointer.y),
                        new Vector2(dual.EndPointer.x, dual.EndPointer.y),
                        dual.CrestCount);
                // Pending/Tracking are non-terminal ownership states. Clearing the
                // path here erased the very first LMB sample every frame, so the
                // eventual single-button fallback replayed an empty gesture and
                // walls, platforms, extraction and LMB grabbing never began.
                if (dual.Kind is DualMouseEarthResultKind.StompStone or
                    DualMouseEarthResultKind.PillarCrest or
                    DualMouseEarthResultKind.Cancel)
                    _bufferedPrimaryPathCount = 0;
                magicInput?.ProcessRoutedInput();
                return;
            }

            bool replayPrimary = dual.Kind == DualMouseEarthResultKind.FallbackPrimary;
            bool replayForce = dual.Kind == DualMouseEarthResultKind.FallbackForce;
            bool replayPrimaryReleased = replayPrimary &&
                                         (inputAdapter.BendPrimaryReleased ||
                                          !inputAdapter.BendPrimaryHeld);
            bool replayForceReleased = replayForce &&
                                       (inputAdapter.BendForceReleased ||
                                        !inputAdapter.BendForceHeld);
            if (replayPrimary)
            {
                magicInput?.ReplayBufferedPrimaryPath(_bufferedPrimaryPath, _bufferedPrimaryPathCount);
                _bufferedPrimaryPathCount = 0;
                if (replayPrimaryReleased)
                    magicInput?.ReplayBufferedPrimaryRelease(inputAdapter.PointerPixels);
            }
            if (replayForce)
            {
                magicInput?.ReplayBufferedForcePress(_bufferedForcePointer);
                if (replayForceReleased)
                    magicInput?.ReplayBufferedForceRelease(inputAdapter.PointerPixels);
            }

            var frame = new EarthActionRouterFrame(
                Time.unscaledTime,
                cancelPressed: inputAdapter.CancelPressed,
                grounded: motor != null && motor.IsGrounded,
                stableSupport: stableSupport,
                descending: descending,
                moveForward: Mathf.Max(0f, move.y),
                modifierHeld: inputAdapter.BendModifierHeld,
                jumpPressed: inputAdapter.JumpPressed,
                jumpHeld: inputAdapter.JumpHeld,
                jumpReleased: inputAdapter.JumpReleased,
                primaryPressed: replayPrimary || inputAdapter.BendPrimaryPressed,
                primaryHeld: inputAdapter.BendPrimaryHeld,
                primaryReleased: inputAdapter.BendPrimaryReleased,
                forcePressed: replayForce || inputAdapter.BendForcePressed,
                forceHeld: inputAdapter.BendForceHeld,
                forceReleased: inputAdapter.BendForceReleased,
                fieldPressed: inputAdapter.BendFieldPressed,
                fieldHeld: inputAdapter.BendFieldHeld,
                fieldReleased: inputAdapter.BendFieldReleased,
                hasRepairTarget: magicInput != null && magicInput.EarthExecutor != null &&
                                 magicInput.EarthExecutor.IsRepairActive,
                hasPrimedQuickStone: magicInput != null && magicInput.IsQuickStonePrimed);
            frame = new EarthActionRouterFrame(
                frame.Time, frame.CancelPressed, frame.Grounded, frame.StableSupport, frame.Descending,
                frame.MoveForward, frame.ModifierHeld, frame.JumpPressed, frame.JumpHeld, frame.JumpReleased,
                frame.PrimaryPressed, frame.PrimaryHeld, frame.PrimaryReleased,
                frame.ForcePressed, frame.ForceHeld, frame.ForceReleased,
                frame.FieldPressed, frame.FieldHeld, frame.FieldReleased,
                frame.HasRepairTarget, frame.HasPrimedQuickStone,
                resonanceController != null && resonanceController.IsVolleyActive);
            _current = _router.Step(in frame);
            ExecuteRoute(in _current);
            ExecuteResonanceVolleyInput();
            // A complete single-button tap can begin and end inside the 80 ms
            // dual-button window. Its buffered release was already replayed through
            // the canonical magic path above, so processing the physical release a
            // second time would commit an empty gesture.
            if (!replayPrimaryReleased && !replayForceReleased)
                magicInput?.ProcessRoutedInput();
        }

        private void BeginBufferedPrimaryPath(Vector2 pointer)
        {
            _bufferedPrimaryPathCount = 1;
            _bufferedPrimaryPath[0] = pointer;
            // The dual-button disambiguation window delays single-LMB replay by a
            // few frames. Capture the authoritative surface now, before a moving
            // gameplay camera can shift the same screen pixel off an arena face.
            magicInput?.BufferPrimarySurface(pointer);
        }

        private void AppendBufferedPrimaryPath(Vector2 pointer)
        {
            if (_bufferedPrimaryPathCount <= 0) return;
            Vector2 previous = _bufferedPrimaryPath[_bufferedPrimaryPathCount - 1];
            if ((pointer - previous).sqrMagnitude < 0.25f) return;
            if (_bufferedPrimaryPathCount < _bufferedPrimaryPath.Length)
            {
                _bufferedPrimaryPath[_bufferedPrimaryPathCount++] = pointer;
                return;
            }
            for (int index = 2; index < _bufferedPrimaryPath.Length; index += 2)
                _bufferedPrimaryPath[index / 2] = _bufferedPrimaryPath[index];
            _bufferedPrimaryPathCount = (_bufferedPrimaryPath.Length / 2);
            _bufferedPrimaryPath[_bufferedPrimaryPathCount++] = pointer;
        }

        private void ExecuteRoute(in EarthActionRoute route)
        {
            if (route.Intent == EarthActionIntentKind.Cancel || route.Phase == EarthActionRoutePhase.Cancel)
            {
                waveAbility?.CancelCharge();
                resonanceController?.Cancel();
                surfController?.Cancel();
                motorInput?.RouteCancel();
                return;
            }

            switch (route.Owner)
            {
                case EarthActionOwner.ShiftSpaceChord:
                    ExecuteSpeculativeWavePreview(in route);
                    break;
                case EarthActionOwner.Wave:
                    ExecuteWave(in route);
                    break;
                case EarthActionOwner.Pillar:
                case EarthActionOwner.LandingCushion:
                    if (route.Phase == EarthActionRoutePhase.Begin) motorInput?.RouteJumpStarted();
                    else if (route.Phase == EarthActionRoutePhase.Commit)
                    {
                        motorInput?.RouteJumpCanceled();
                        if (route.Owner == EarthActionOwner.Pillar)
                            RecordTechnique(EarthTechniqueId.PillarJump, default,
                                EarthEventTag.Launched | EarthEventTag.Airborne, route.Charge01, AimDirection());
                    }
                    break;
                case EarthActionOwner.Resonance:
                    ExecuteResonance(in route);
                    break;
                case EarthActionOwner.Surf:
                    ExecuteSurf(in route);
                    break;
            }
        }

        private void ExecuteSpeculativeWavePreview(in EarthActionRoute route)
        {
            if (waveAbility == null) return;
            if (route.Phase == EarthActionRoutePhase.Begin)
            {
                _waveChargeStartedAt = Time.unscaledTime;
                waveAbility.BeginCharge(0f);
            }
            else if (route.Phase == EarthActionRoutePhase.Continue)
            {
                waveAbility.SetShiftHeldSeconds(
                    Mathf.Max(0f, Time.unscaledTime - _waveChargeStartedAt));
            }
        }

        private void ExecuteSurf(in EarthActionRoute route)
        {
            if (surfController == null) return;
            Vector3 forward = motor != null ? motor.FacingForward : transform.forward;
            if (route.Phase == EarthActionRoutePhase.Begin)
            {
                if (surfController.Begin(Time.unscaledTime, forward))
                    RecordTechnique(EarthTechniqueId.Surf, surfController.MatterId,
                        EarthEventTag.MovingSurface, 0.35f, forward);
            }
            else if (route.Phase == EarthActionRoutePhase.Continue)
                surfController.Continue(
                    inputAdapter != null ? inputAdapter.Move : Vector2.up,
                    forward,
                    inputAdapter != null ? inputAdapter.BendParameter : 0f,
                    inputAdapter != null && inputAdapter.BendForcePressed,
                    inputAdapter != null && inputAdapter.BendForceHeld);
            else if (route.Phase == EarthActionRoutePhase.Commit)
                surfController.Release(Time.unscaledTime);
            else if (route.Phase == EarthActionRoutePhase.Cancel)
                surfController.Cancel();
        }

        private void ExecuteResonance(in EarthActionRoute route)
        {
            if (resonanceController == null) return;
            Vector3 aim = AimDirection();
            if (route.Phase == EarthActionRoutePhase.Begin)
            {
                // The same-frame chord acknowledgement starts as a wave preview.
                // Morphing the chord into resonance must remove that preview before
                // the resonance stones are allowed to claim the input session.
                waveAbility?.CancelCharge();
                resonanceController.BeginCharge(Time.unscaledTime);
            }
            else if (route.Phase == EarthActionRoutePhase.Continue &&
                     route.Intent == EarthActionIntentKind.ResonanceCharge)
                resonanceController.ContinueCharge(Time.unscaledTime, aim);
            else if (route.Phase == EarthActionRoutePhase.Commit)
            {
                if (resonanceController.ReleaseCharge(Time.unscaledTime, aim))
                    RecordTechnique(EarthTechniqueId.Resonance, resonanceController.PrimaryMatterId,
                        EarthEventTag.Formed, route.Charge01, aim);
            }
            else if (route.Phase == EarthActionRoutePhase.Cancel)
                resonanceController.Cancel();
        }

        private void ExecuteResonanceVolleyInput()
        {
            if (resonanceController == null || !resonanceController.IsVolleyActive || inputAdapter == null) return;
            if (inputAdapter.BendForcePressed)
            {
                resonanceController.FireAll(AimDirection());
                return;
            }
            if (inputAdapter.BendPrimaryPressed || inputAdapter.BendPrimaryHeld)
                resonanceController.FireNearest(AimDirection(), Time.unscaledTime);
        }

        private Vector3 AimDirection()
        {
            if (castCamera != null) return castCamera.transform.forward;
            if (motor != null) return motor.FacingForward;
            return transform.forward;
        }

        private void ExecuteWave(in EarthActionRoute route)
        {
            if (waveAbility == null) return;
            if (route.Phase == EarthActionRoutePhase.Begin)
            {
                _waveChargeStartedAt = Time.unscaledTime;
                waveAbility.BeginCharge(0f);
                return;
            }
            if (route.Phase == EarthActionRoutePhase.Continue)
            {
                waveAbility.SetShiftHeldSeconds(
                    Mathf.Max(0f, Time.unscaledTime - _waveChargeStartedAt));
                return;
            }
            if (route.Phase != EarthActionRoutePhase.Commit) return;
            if (waveAbility.IsCharging)
            {
                if (waveAbility.ReleaseCharge())
                    RecordTechnique(EarthTechniqueId.FaultLine, waveAbility.PrimaryMatterId,
                        EarthEventTag.Formed, route.Charge01, AimDirection());
                magicInput?.ReportStatus(
                    $"FAULT LINE — {waveAbility.LastColumnCount} rising cells; one destructible target maximum.");
                return;
            }
            Vector3 forward = motor != null ? motor.FacingForward : transform.forward;
            if (waveAbility.TryCast(forward, 0.18f, 0.12f, out _))
                RecordTechnique(EarthTechniqueId.FaultLine, waveAbility.PrimaryMatterId,
                    EarthEventTag.Formed, route.Charge01, forward);
            magicInput?.ReportStatus(
                $"FAULT LINE — {waveAbility.LastColumnCount} rising cells; one destructible target maximum.");
        }

        private void RecordTechnique(
            EarthTechniqueId technique,
            EarthMatterId matter,
            EarthEventTag result,
            float energy,
            Vector3 direction)
        {
            magicInput?.EarthExecutor?.ComboRuntime?.RecordTechnique(
                technique,
                matter,
                result,
                unchecked((uint)Time.frameCount),
                energy,
                direction);
        }
    }
}
