using System;
using System.Collections.Generic;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Fields;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Materials;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Elemental.Input.Gestures
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class MagicInputController : MonoBehaviour
    {
        private const int ProjectionHitCapacity = 16;

        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private UnityEngine.Camera castCamera;
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private AirMagicExecutor airExecutor;
        [SerializeField] private ThermalWaterMagicExecutor thermalWaterExecutor;
        [SerializeField] private ElementId selectedElement = ElementId.Earth;
        [SerializeField] private Collider planetCollider;
        [SerializeField] private LineRenderer previewLine;
        [SerializeField, Range(4, 24)] private int resampleCount = 12;
        [SerializeField, Min(1f)] private float projectionDistance = 200f;
        [Header("Unified bending")]
        [SerializeField, Min(0.05f)] private float acquisitionDecisionSeconds = 0.15f;
        [SerializeField, Min(0.2f)] private float maximumFormSeconds = 1.15f;
        [SerializeField, Min(0.001f)] private float extractionMotionThreshold = 0.018f;
        [SerializeField, Min(1f)] private float initialHoldDistance = 7.5f;
        [SerializeField, Min(0.1f)] private float minimumHoldDistance = 2.5f;
        [SerializeField, Min(1f)] private float maximumHoldDistance = 16f;
        [SerializeField, Min(0.01f)] private float wheelDistanceStep = 0.012f;
        [SerializeField, Min(0.05f)] private float pushAssistRadius = 0.65f;

        private readonly PointerPathSampler _sampler = new PointerPathSampler();
        private readonly List<float2> _resampledScreen = new List<float2>(24);
        private readonly List<float3> _worldPath = new List<float3>(24);
        private readonly List<Vector3> _previewPoints = new List<Vector3>(96);
        private readonly RaycastHit[] _projectionHits = new RaycastHit[ProjectionHitCapacity];

        private InputAction _castAction;
        private InputAction _pushAction;
        private InputAction _pointerAction;
        private InputAction _ability1Action;
        private InputAction _ability2Action;
        private InputAction _ability3Action;
        private InputAction _ability4Action;
        private InputAction _elementFireAction;
        private InputAction _elementWaterAction;
        private AbilityId _selectedAbility = EarthAbilityIds.LineWall;
        private uint _tick;
        private bool _pushCharging;
        private float _pushStartedAt;
        private bool _pushTargetLocked;
        private BendSessionState _bendSession;
        private BendTuning _bendTuning;
        private bool _earthAcquirePending;
        private bool _wallGesturePending;
        private float _platformHeight01;
        private float2 _bendStartPointer;
        private float _bendStartedAt;
        private float _holdDistance;
        private Vector3 _previousBendTarget;
        private Vector3 _smoothedBendTargetVelocity;
        private float2 _lastBendPointer;
        private float _formingAmount01;
        private Vector3 _formingSourceWorld;
        private bool _formingSourceValid;
        private Vector2 _aimScreenPosition;
        private Rigidbody _casterBody;
        private bool _gravityWellHeld;
        private float _gravityWellFocusDistance;

        public AbilityId SelectedAbility => _selectedAbility;
        public ElementId SelectedElement => selectedElement;
        public BendPhase CurrentBendPhase => _bendSession != null
            ? _bendSession.Phase
            : Elemental.Simulation.Bending.BendPhase.Idle;
        public BendOriginMode BendOriginMode => _bendSession != null
            ? _bendSession.OriginMode
            : BendOriginMode.Aim;
        public float BendAmount01 => _bendSession != null
            ? Mathf.Max(_bendSession.Amount01, _formingAmount01)
            : _formingAmount01;
        public float BendCharge01 => _bendSession != null ? _bendSession.Charge01 : 0f;
        public float BendFocus01 => _bendSession != null ? _bendSession.Focus01 : 0f;
        public BendGestureIntent CurrentGestureIntent => _bendSession != null
            ? _bendSession.GestureIntent
            : BendGestureIntent.None;
        public Vector3 BendTargetPosition => _previousBendTarget;
        public Vector3 BendTargetVelocity => _smoothedBendTargetVelocity;
        public bool IsFormingEarth => _earthAcquirePending && _formingSourceValid;
        public Vector3 FormingSourceWorld => _formingSourceWorld;
        public Vector3 PlanetCenterWorld => planetCollider != null ? planetCollider.bounds.center : Vector3.zero;
        public Vector2 AimScreenPosition => _aimScreenPosition;
        public bool IsVectorFieldActive => executor != null && executor.IsVectorFieldActive;
        public bool IsGravityWellActive => executor != null && executor.IsGravityWellActive;
        public MagicExecutor EarthExecutor => executor;
        public Vector3 GravityWellFocus => executor != null ? executor.GravityWellFocus : Vector3.zero;
        public float GravityWellStrength => executor != null ? executor.GravityWellStrength : 0f;
        public Vector3 VectorFieldDirection => executor != null ? executor.VectorFieldDirection : Vector3.zero;
        public float PlatformPreviewHeight01 => _selectedAbility == EarthAbilityIds.RaisePlatform
            ? _platformHeight01
            : 0f;
        public event Action<string> StatusChanged;
        public event Action<IReadOnlyList<Vector3>> PreviewChanged;
        public event Action PreviewCleared;
        public event Action<float> PushChargeChanged;

        public bool TryBeginEarthBendAtScreenPoint(
            float2 sourcePointer,
            BendOriginMode originMode,
            float amount01)
        {
            EnsureBendSession();
            if (!_bendSession.BeginAcquire(originMode)) return false;
            _earthAcquirePending = true;
            _wallGesturePending = false;
            _bendStartPointer = sourcePointer;
            _bendStartedAt = Time.unscaledTime;
            _holdDistance = Mathf.Clamp(initialHoldDistance, minimumHoldDistance, maximumHoldDistance);
            _formingAmount01 = Mathf.Clamp01(amount01);
            _smoothedBendTargetVelocity = Vector3.zero;
            _lastBendPointer = sourcePointer;
            if (TryAcquireExistingEarthBody(sourcePointer)) return true;
            return TryAcquireEarthVolume(sourcePointer, 0f, Mathf.Clamp01(amount01));
        }

        public bool TrySetEarthBendTargetAtScreenPoint(float2 pointer, float deltaSeconds)
        {
            Rigidbody held = executor != null ? executor.HeldBody : null;
            if (held == null || castCamera == null) return false;
            UpdateHeldBendTarget(held, pointer, Mathf.Max(0.0001f, deltaSeconds), false);
            return true;
        }

        public bool TryReleaseEarthBendAtScreenPoint(
            float2 pointer,
            Vector3 gestureVelocity,
            BendGestureIntent intent,
            out Vector3 releaseVelocity)
        {
            releaseVelocity = Vector3.zero;
            Rigidbody held = executor != null ? executor.HeldBody : null;
            if (held == null || castCamera == null || _bendSession == null) return false;
            if (!_bendSession.Commit(intent)) return false;
            Ray ray = castCamera.ScreenPointToRay(new Vector2(pointer.x, pointer.y));
            bool released = executor.ReleaseHeldEarth(
                ray.direction,
                gestureVelocity,
                _bendSession.Charge01,
                _tick++,
                out releaseVelocity);
            if (!released) return false;
            _bendSession.BeginRecovery();
            _bendSession.CompleteRecovery();
            _earthAcquirePending = false;
            PushChargeChanged?.Invoke(0f);
            return true;
        }

        public void Configure(
            PlayerInput configuredPlayerInput,
            UnityEngine.Camera configuredCamera,
            MagicExecutor configuredExecutor,
            Collider configuredPlanetCollider,
            LineRenderer configuredPreview)
        {
            playerInput = configuredPlayerInput;
            castCamera = configuredCamera;
            executor = configuredExecutor;
            planetCollider = configuredPlanetCollider;
            previewLine = configuredPreview;
            airExecutor = null;
            thermalWaterExecutor = null;
            selectedElement = ElementId.Earth;
            _selectedAbility = EarthAbilityIds.LineWall;
        }

        public void ConfigureAir(
            PlayerInput configuredPlayerInput,
            UnityEngine.Camera configuredCamera,
            AirMagicExecutor configuredExecutor,
            Collider configuredPlanetCollider,
            LineRenderer configuredPreview)
        {
            playerInput = configuredPlayerInput;
            castCamera = configuredCamera;
            airExecutor = configuredExecutor;
            executor = null;
            thermalWaterExecutor = null;
            planetCollider = configuredPlanetCollider;
            previewLine = configuredPreview;
            selectedElement = ElementId.Air;
            _selectedAbility = AirAbilityIds.GustCorridor;
        }

        public void ConfigureThermalWater(
            PlayerInput configuredPlayerInput,
            UnityEngine.Camera configuredCamera,
            ThermalWaterMagicExecutor configuredExecutor,
            Collider configuredPlanetCollider,
            LineRenderer configuredPreview,
            ElementId initialElement = ElementId.Water)
        {
            playerInput = configuredPlayerInput;
            castCamera = configuredCamera;
            thermalWaterExecutor = configuredExecutor;
            executor = null;
            airExecutor = null;
            planetCollider = configuredPlanetCollider;
            previewLine = configuredPreview;
            SelectElement(initialElement);
        }

        public void SelectElement(ElementId element)
        {
            if (thermalWaterExecutor == null || (element != ElementId.Fire && element != ElementId.Water))
            {
                return;
            }
            selectedElement = element;
            _selectedAbility = element == ElementId.Fire ? FireAbilityIds.HeatJet : WaterAbilityIds.GatherWater;
        }

        public bool SelectEarthAbility(AbilityId ability)
        {
            if (selectedElement != ElementId.Earth) return false;
            if (ability != EarthAbilityIds.LineWall &&
                ability != EarthAbilityIds.RaisePlatform &&
                ability != EarthAbilityIds.PullRock &&
                ability != EarthAbilityIds.FlickThrow) return false;
            _selectedAbility = ability;
            ClearPreview();
            return true;
        }

        private void Awake()
        {
            EnsureBendSession();
            _holdDistance = initialHoldDistance;
            _casterBody = GetComponent<Rigidbody>();
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }
        }

        private void EnsureBendSession()
        {
            if (_bendSession != null) return;
            _bendTuning = BendTuning.Default;
            _bendSession = new BendSessionState(_bendTuning);
        }

        private void OnEnable()
        {
            InputActionMap map = playerInput.actions?.FindActionMap("Gameplay", true);
            _castAction = map?.FindAction("Cast", true);
            _pushAction = map?.FindAction("Push", false);
            _pointerAction = map?.FindAction("Pointer", true);
            _ability1Action = map?.FindAction("Ability1", true);
            _ability2Action = map?.FindAction("Ability2", true);
            _ability3Action = map?.FindAction("Ability3", true);
            _ability4Action = map?.FindAction("Ability4", false);
            _elementFireAction = map?.FindAction("ElementFire", false);
            _elementWaterAction = map?.FindAction("ElementWater", false);

            if (_castAction == null || _pointerAction == null)
            {
                Debug.LogError("[Elemental] Magic Cast/Pointer actions are not configured.", this);
                enabled = false;
                return;
            }

            map.Enable();
        }

        private void OnDisable()
        {
            executor?.CancelHeldEarthControl();
            executor?.CancelVectorField();
            executor?.CancelGravityWell();
            _gravityWellHeld = false;
            _sampler.Cancel();
            _earthAcquirePending = false;
            _wallGesturePending = false;
            _bendSession?.Cancel();
            ClearPreview();
        }

        private void Update()
        {
            UpdateElementSelection();
            UpdateAbilitySelection();
            Vector2 pointer = _pointerAction.ReadValue<Vector2>();
            _aimScreenPosition = pointer;
            float2 pointerFloat = new float2(pointer.x, pointer.y);
            if (selectedElement == ElementId.Earth) UpdateGravityWellInput(pointerFloat);

            bool activeEarthBend = selectedElement == ElementId.Earth &&
                                   _bendSession != null && _bendSession.IsActive;
            if (activeEarthBend)
            {
                UpdateBendPowerInput();
                _bendSession.Tick(Time.unscaledDeltaTime);
                PushChargeChanged?.Invoke(_bendSession.Charge01);
            }
            else
            {
                UpdateStandalonePush(pointerFloat);
            }

            if (_castAction.WasPressedThisFrame())
            {
                _sampler.Begin(pointerFloat, Time.unscaledTime);
                if (selectedElement == ElementId.Earth)
                    BeginEarthAcquireDecision(pointerFloat);
            }

            if (_sampler.IsActive)
            {
                _sampler.Sample(pointerFloat);
                if (selectedElement == ElementId.Earth && _bendSession.IsActive)
                    UpdateUnifiedEarthBend(pointerFloat);
                else
                    UpdatePreview(pointerFloat);
            }

            if (_castAction.WasReleasedThisFrame())
            {
                if (selectedElement == ElementId.Earth && _bendSession.IsActive)
                    CommitUnifiedEarthBend(pointerFloat);
                else
                    Commit(pointerFloat);
            }
            else if (selectedElement == ElementId.Earth &&
                     _bendSession.IsActive && _sampler.IsActive && !_castAction.IsPressed())
            {
                // Recover from a release event lost while the game window was unfocused.
                // Otherwise a body can remain controlled indefinitely beside the player.
                CommitUnifiedEarthBend(pointerFloat);
            }
        }

        private void UpdateBendPowerInput()
        {
            if (_pushAction?.WasPressedThisFrame() == true)
                _bendSession.BeginCharge();
            if (_pushAction?.WasReleasedThisFrame() == true)
                _bendSession.EndCharge();
            if (_wallGesturePending && _selectedAbility == EarthAbilityIds.RaisePlatform)
                _platformHeight01 = Mathf.Max(_platformHeight01, _bendSession.Charge01);
        }

        private void UpdateStandalonePush(float2 pointer)
        {
            if (_pushAction?.WasPressedThisFrame() == true)
            {
                _pushCharging = true;
                _pushStartedAt = Time.unscaledTime;
                _pushTargetLocked = TryBeginPushAtScreenPoint(pointer);
                if (!_pushTargetLocked)
                    StatusChanged?.Invoke("No pushable rock, fragment or wall near the cursor.");
            }
            if (_pushCharging)
            {
                float charge = PushCharge(Time.unscaledTime - _pushStartedAt);
                PushChargeChanged?.Invoke(charge);
                if (_pushTargetLocked && castCamera != null)
                {
                    Ray ray = castCamera.ScreenPointToRay(new Vector2(pointer.x, pointer.y));
                    executor.UpdateVectorField(ray.direction, charge);
                }
            }
            if (!_pushCharging || _pushAction?.WasReleasedThisFrame() != true) return;
            if (_pushTargetLocked) executor.ReleaseVectorField();
            _pushCharging = false;
            _pushTargetLocked = false;
            PushChargeChanged?.Invoke(0f);
        }

        private void UpdateGravityWellInput(float2 pointer)
        {
            if (Mouse.current == null || executor == null || castCamera == null) return;
            var middle = Mouse.current.middleButton;
            if (middle.wasPressedThisFrame)
            {
                _gravityWellHeld = TryBeginGravityWellAtScreenPoint(pointer);
                StatusChanged?.Invoke(_gravityWellHeld
                    ? "GRAVITY GRIP — hold MMB to pull shards and tear stressed Earth apart."
                    : "No earth surface or structure under the gravity grip.");
            }
            if (_gravityWellHeld && middle.isPressed)
                TryUpdateGravityWellAtScreenPoint(pointer);
            if (!_gravityWellHeld || !middle.wasReleasedThisFrame) return;
            EndGravityWell();
        }

        public bool TryBeginGravityWellAtScreenPoint(float2 screenPoint)
        {
            if (!TryFindGravityFocus(screenPoint, out RaycastHit hit)) return false;
            Vector3 center = planetCollider != null ? planetCollider.bounds.center : Vector3.zero;
            Vector3 up = hit.point - center;
            if (up.sqrMagnitude < 0.01f) up = hit.normal;
            up.Normalize();
            _gravityWellFocusDistance = hit.distance;
            Vector3 focus = hit.point + (up * executor.GravityWellFocusLift);
            return executor.TryBeginGravityWell(hit.collider, focus, up);
        }

        public bool TryUpdateGravityWellAtScreenPoint(float2 screenPoint)
        {
            if (executor == null || !executor.IsGravityWellActive || castCamera == null) return false;
            Vector3 center = planetCollider != null ? planetCollider.bounds.center : Vector3.zero;
            Vector3 point;
            Vector3 up;
            if (TryFindGravityFocus(screenPoint, out RaycastHit hit))
            {
                _gravityWellFocusDistance = hit.distance;
                point = hit.point;
                up = point - center;
                if (up.sqrMagnitude < 0.01f) up = hit.normal;
            }
            else
            {
                Ray ray = castCamera.ScreenPointToRay(new Vector2(screenPoint.x, screenPoint.y));
                point = ray.GetPoint(Mathf.Max(1f, _gravityWellFocusDistance));
                up = point - center;
            }
            up = up.sqrMagnitude > 0.01f ? up.normalized : Vector3.up;
            executor.UpdateGravityWell(point + (up * executor.GravityWellFocusLift), up);
            return true;
        }

        public void EndGravityWell()
        {
            executor?.CancelGravityWell();
            _gravityWellHeld = false;
        }

        private bool TryFindGravityFocus(float2 screenPoint, out RaycastHit selected)
        {
            selected = default;
            if (castCamera == null) return false;
            Ray ray = castCamera.ScreenPointToRay(new Vector2(screenPoint.x, screenPoint.y));
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                ray, _projectionHits, projectionDistance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _projectionHits[index];
                if (hit.collider == null || hit.distance >= nearest) continue;
                bool isPlanet = hit.collider == planetCollider ||
                                (planetCollider != null && hit.collider.transform.IsChildOf(planetCollider.transform));
                bool isEarth = ResolveEarthTarget(hit.collider) != null ||
                               hit.collider.GetComponentInParent<EarthPlatform>() != null;
                if (!isPlanet && !isEarth) continue;
                nearest = hit.distance;
                selected = hit;
            }
            return selected.collider != null;
        }

        private void BeginEarthAcquireDecision(float2 pointer)
        {
            BendOriginMode origin = Keyboard.current != null &&
                                    (Keyboard.current.leftShiftKey.isPressed ||
                                     Keyboard.current.rightShiftKey.isPressed)
                ? BendOriginMode.Self
                : BendOriginMode.Aim;
            if (!_bendSession.BeginAcquire(origin)) return;
            _earthAcquirePending = true;
            _wallGesturePending = false;
            _bendStartPointer = pointer;
            _bendStartedAt = Time.unscaledTime;
            _holdDistance = Mathf.Clamp(initialHoldDistance, minimumHoldDistance, maximumHoldDistance);
            _smoothedBendTargetVelocity = Vector3.zero;
            _lastBendPointer = pointer;
            _formingAmount01 = 0.18f;
            _platformHeight01 = 0f;
            if (TryAcquireExistingEarthBody(pointer)) return;
            _formingSourceValid = TryProject(pointer, out _formingSourceWorld);
            StatusChanged?.Invoke(origin == BendOriginMode.Self
                ? "SELF ORIGIN — hold still for mass, or sweep sideways for a wall."
                : "FORMING ROCK — hold still for mass, or drag sideways on ground for a wall.");
        }

        private void UpdateUnifiedEarthBend(float2 pointer)
        {
            float elapsed = Time.unscaledTime - _bendStartedAt;
            float2 normalizedDrag = NormalizePointerDelta(pointer - _bendStartPointer);
            if (_earthAcquirePending)
            {
                if (EarthWallGestureSolver.IsWallStroke(normalizedDrag))
                {
                    _earthAcquirePending = false;
                    _formingSourceValid = false;
                    _wallGesturePending = true;
                    _selectedAbility = EarthAbilityIds.LineWall;
                    _bendSession.SourceAcquired();
                    _bendSession.SetAmount(1f);
                    StatusChanged?.Invoke("WALL FOOTPRINT — draw on the ground in any direction, release to raise.");
                    UpdatePreview(pointer);
                    return;
                }

                _formingAmount01 = FormAmountFromSeconds(elapsed, maximumFormSeconds);
                bool startedManipulating = elapsed >= acquisitionDecisionSeconds &&
                                           math.length(normalizedDrag) >= extractionMotionThreshold;
                if (!startedManipulating && elapsed < maximumFormSeconds) return;
                TryAcquireEarthVolume(_bendStartPointer, elapsed, _formingAmount01);
            }

            if (_wallGesturePending)
            {
                EarthStructureGestureResult structure = EarthStructureGestureSolver.Classify(_sampler.Points);
                AbilityId next = structure.Kind == EarthStructureGestureKind.Platform
                    ? EarthAbilityIds.RaisePlatform
                    : EarthAbilityIds.LineWall;
                if (next != _selectedAbility)
                {
                    _selectedAbility = next;
                    StatusChanged?.Invoke(next == EarthAbilityIds.RaisePlatform
                        ? "PLATFORM — outline the area; hold RMB while drawing to raise it higher."
                        : "WALL — keep the stroke straight; release LMB to raise it.");
                }
                UpdatePreview(pointer);
                return;
            }

            Rigidbody held = executor != null ? executor.HeldBody : null;
            if (held == null) return;
            float delta = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            UpdateHeldBendTarget(held, pointer, delta, true, false);
        }

        private void UpdateHeldBendTarget(
            Rigidbody held,
            float2 pointer,
            float delta,
            bool readWheel,
            bool forceUpdate = true)
        {
            float wheel = readWheel && Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            float2 pointerDelta = pointer - _lastBendPointer;
            if (!forceUpdate && math.lengthsq(pointerDelta) < 0.25f && Mathf.Abs(wheel) < 0.001f)
            {
                // A stationary cursor means a stationary world-space target. Rebuilding
                // the ray from a moving camera made the rock look parented to the caster.
                executor.UpdateHeldEarthTarget(
                    _previousBendTarget, Vector3.zero, _bendSession.Charge01);
                _smoothedBendTargetVelocity = Vector3.zero;
                return;
            }

            _holdDistance = Mathf.Clamp(
                _holdDistance + (wheel * wheelDistanceStep),
                minimumHoldDistance,
                maximumHoldDistance);
            Ray ray = castCamera.ScreenPointToRay(new Vector2(pointer.x, pointer.y));
            Vector3 target = ray.GetPoint(_holdDistance);
            Vector3 rawVelocity = (target - _previousBendTarget) / delta;
            float blend = 1f - Mathf.Exp(-18f * delta);
            _smoothedBendTargetVelocity = Vector3.Lerp(
                _smoothedBendTargetVelocity,
                rawVelocity,
                blend);
            if (_pushAction?.IsPressed() == true)
            {
                float charge = _bendSession != null ? _bendSession.Charge01 : 0f;
                _smoothedBendTargetVelocity += ray.direction * Mathf.Lerp(3f, 32f, charge);
            }
            _previousBendTarget = target;
            _lastBendPointer = pointer;
            executor.UpdateHeldEarthTarget(target, _smoothedBendTargetVelocity, _bendSession.Charge01);
        }

        private bool TryAcquireExistingEarthBody(float2 pointer)
        {
            if (executor == null || castCamera == null) return false;
            Ray ray = castCamera.ScreenPointToRay(new Vector2(pointer.x, pointer.y));
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                ray,
                _projectionHits,
                projectionDistance,
                ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Rigidbody selected = null;
            IEarthPhysicalTarget selectedEarthTarget = null;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _projectionHits[index];
                Rigidbody body = hit.rigidbody;
                IEarthPhysicalTarget earthTarget = ResolveEarthTarget(hit.collider);
                bool releasablePiece = earthTarget is EarthWallPiece || earthTarget is EarthPlatformPiece;
                if (body == null || body == _casterBody || (body.isKinematic && !releasablePiece) ||
                    hit.distance >= nearest) continue;
                if (earthTarget == null && body.GetComponent<GravityBody>() == null &&
                    body.GetComponent<EarthFragment>() == null &&
                    body.GetComponent<PhysicalImpactTarget>() == null) continue;
                nearest = hit.distance;
                selected = body;
                selectedEarthTarget = earthTarget;
            }

            if (selected == null) return false;
            float cameraDistance = Vector3.Distance(
                castCamera.transform.position,
                selected.worldCenterOfMass);
            _holdDistance = Mathf.Clamp(cameraDistance, minimumHoldDistance, maximumHoldDistance);
            _previousBendTarget = selected.worldCenterOfMass;
            _smoothedBendTargetVelocity = Vector3.zero;
            _lastBendPointer = pointer;
            if (!executor.TryAcquireExistingEarthBody(
                    selected, _previousBendTarget, in _bendTuning, _tick++, selectedEarthTarget)) return false;

            _bendSession.SourceAcquired();
            _bendSession.SetAmount(Mathf.Clamp(
                Mathf.InverseLerp(10f, 500f, selected.mass), 0.18f, 1f));
            _selectedAbility = EarthAbilityIds.PullRock;
            _earthAcquirePending = false;
            _wallGesturePending = false;
            _formingAmount01 = 0f;
            _formingSourceValid = false;
            ClearPreview();
            StatusChanged?.Invoke(
                "ROCK GRIPPED â€” hold LMB and move the mouse; RMB stores release power.");
            return true;
        }

        private bool TryAcquireEarthVolume(
            float2 sourcePointer,
            float elapsed,
            float amountOverride = -1f)
        {
            _earthAcquirePending = false;
            _formingAmount01 = 0f;
            _formingSourceValid = false;
            if (executor == null || castCamera == null || !TryProject(sourcePointer, out Vector3 source))
            {
                _bendSession.Cancel();
                StatusChanged?.Invoke("No earth source under the pointer.");
                return false;
            }

            _bendSession.SourceAcquired();
            float amount = amountOverride >= 0f
                ? Mathf.Clamp01(amountOverride)
                : FormAmountFromSeconds(elapsed, maximumFormSeconds);
            _bendSession.SetAmount(amount);
            _selectedAbility = EarthAbilityIds.PullRock;
            Vector3 localUp = (source - planetCollider.bounds.center).normalized;
            _worldPath.Clear();
            _worldPath.Add(new float3(source.x, source.y, source.z));
            MagicCommand command = CreateCommand(
                EarthAbilityIds.PullRock,
                source,
                localUp,
                amount);
            if (!executor.Execute(in command) || executor.HeldFragment == null)
            {
                _bendSession.Cancel();
                return false;
            }

            Ray ray = castCamera.ScreenPointToRay(new Vector2(sourcePointer.x, sourcePointer.y));
            _previousBendTarget = ray.GetPoint(_holdDistance);
            _smoothedBendTargetVelocity = Vector3.zero;
            _lastBendPointer = sourcePointer;
            executor.BeginHeldEarthControl(
                _previousBendTarget,
                Vector3.zero,
                _bendSession.Charge01,
                in _bendTuning);
            ClearPreview();
            StatusChanged?.Invoke("Earth torn from the selected ground volume — keep LMB held.");
            return true;
        }

        private void CommitUnifiedEarthBend(float2 pointer)
        {
            _sampler.End(pointer);
            float elapsed = Mathf.Max(0.001f, Time.unscaledTime - _bendStartedAt);
            if (_wallGesturePending)
            {
                _bendSession.Cancel();
                _wallGesturePending = false;
                _earthAcquirePending = false;
                EarthStructureGestureResult structure = EarthStructureGestureSolver.Classify(_sampler.Points);
                _selectedAbility = structure.Kind == EarthStructureGestureKind.Platform
                    ? EarthAbilityIds.RaisePlatform
                    : EarthAbilityIds.LineWall;
                TryCommitScreenPath(_sampler.Points, elapsed);
                ClearPreview();
                _sampler.Cancel();
                return;
            }

            if (_earthAcquirePending && !TryAcquireEarthVolume(_bendStartPointer, elapsed))
            {
                _sampler.Cancel();
                return;
            }

            Rigidbody held = executor != null ? executor.HeldBody : null;
            if (held != null)
            {
                BendGestureIntent intent = ClassifyBendIntent(pointer, elapsed);
                TryReleaseEarthBendAtScreenPoint(
                    pointer,
                    _smoothedBendTargetVelocity,
                    intent,
                    out Vector3 velocity);
                _selectedAbility = EarthAbilityIds.PullRock;
                StatusChanged?.Invoke(velocity.magnitude < 2f
                    ? "Earth mass placed with its physical momentum preserved."
                    : "Earth mass released along the gesture trajectory.");
            }
            else
            {
                _bendSession.Cancel();
            }

            _earthAcquirePending = false;
            ClearPreview();
            _sampler.Cancel();
            PushChargeChanged?.Invoke(0f);
        }

        public static float FormAmountFromSeconds(float seconds, float fullFormSeconds = 1.15f)
        {
            float normalized = Mathf.Clamp01(
                Mathf.Max(0f, seconds) / Mathf.Max(0.05f, fullFormSeconds));
            float eased = normalized * normalized * (3f - (2f * normalized));
            return Mathf.Lerp(0.18f, 1f, eased);
        }

        private BendGestureIntent ClassifyBendIntent(float2 pointer, float elapsed)
        {
            float2 drag = NormalizePointerDelta(pointer - _bendStartPointer);
            float distance = math.length(drag);
            if (elapsed <= 0.18f && distance < 0.025f) return BendGestureIntent.Tap;
            if (distance < 0.02f) return BendGestureIntent.HoldStill;
            float speed = distance / Mathf.Max(0.001f, elapsed);
            if (speed >= 0.65f) return BendGestureIntent.Flick;
            if (math.abs(drag.x) > math.abs(drag.y) * 1.25f) return BendGestureIntent.SweepHorizontal;
            return drag.y >= 0f ? BendGestureIntent.DragUp : BendGestureIntent.DragDown;
        }

        private static float2 NormalizePointerDelta(float2 delta)
        {
            return new float2(
                delta.x / Mathf.Max(1f, Screen.width),
                delta.y / Mathf.Max(1f, Screen.height));
        }

        private void UpdateAbilitySelection()
        {
            // Earth no longer exposes three live spell slots. Its shape is inferred from
            // the same LMB gesture. SelectEarthAbility remains only as a replay/test bridge.
            if (selectedElement == ElementId.Earth) return;
            if (_ability1Action?.WasPressedThisFrame() == true)
            {
                _selectedAbility = selectedElement == ElementId.Air ? AirAbilityIds.GustCorridor :
                    selectedElement == ElementId.Fire ? FireAbilityIds.HeatJet :
                    selectedElement == ElementId.Water ? WaterAbilityIds.GatherWater : EarthAbilityIds.LineWall;
            }
            else if (_ability2Action?.WasPressedThisFrame() == true)
            {
                _selectedAbility = selectedElement == ElementId.Air ? AirAbilityIds.Vortex :
                    selectedElement == ElementId.Fire ? FireAbilityIds.ThermalFocus :
                    selectedElement == ElementId.Water ? WaterAbilityIds.WaterJet : EarthAbilityIds.PullRock;
            }
            else if (_ability3Action?.WasPressedThisFrame() == true)
            {
                _selectedAbility = selectedElement == ElementId.Air ? AirAbilityIds.LiftColumn :
                    selectedElement == ElementId.Water ? WaterAbilityIds.FreezeBridge : EarthAbilityIds.FlickThrow;
            }
            else if ((selectedElement == ElementId.Air || selectedElement == ElementId.Water) &&
                     _ability4Action?.WasPressedThisFrame() == true)
            {
                _selectedAbility = selectedElement == ElementId.Air ? AirAbilityIds.AirBrake : WaterAbilityIds.SteamBurst;
            }
        }

        private void UpdateElementSelection()
        {
            if (_elementFireAction?.WasPressedThisFrame() == true) SelectElement(ElementId.Fire);
            else if (_elementWaterAction?.WasPressedThisFrame() == true) SelectElement(ElementId.Water);
        }

        private void UpdatePreview(float2 currentPointer)
        {
            float previewDuration = Time.unscaledTime - _sampler.StartTime;
            if (!TryPreviewScreenPath(_sampler.Points, previewDuration))
            {
                ClearPreview();
            }
        }

        public bool TryPreviewScreenPath(IReadOnlyList<float2> screenPath, float durationSeconds)
        {
            if (screenPath == null || screenPath.Count < 2) return false;
            float2 currentPointer = screenPath[screenPath.Count - 1];
            if (!TryBuildCommand(screenPath, currentPointer, durationSeconds, out MagicCommand command)) return false;

            if (selectedElement == ElementId.Air)
            {
                airExecutor.BuildPreview(in command, _previewPoints);
            }
            else if (selectedElement == ElementId.Fire || selectedElement == ElementId.Water)
            {
                thermalWaterExecutor.BuildPreview(in command, _previewPoints);
            }
            else
            {
                executor.BuildPreview(in command, _previewPoints);
            }
            previewLine.positionCount = _previewPoints.Count;
            for (int index = 0; index < _previewPoints.Count; index++)
            {
                previewLine.SetPosition(index, _previewPoints[index]);
            }
            PreviewChanged?.Invoke(_previewPoints);
            return _previewPoints.Count > 0;
        }

        private void Commit(float2 pointer)
        {
            _sampler.End(pointer);
            float duration = Time.unscaledTime - _sampler.StartTime;
            TryCommitScreenPath(_sampler.Points, duration);
            ClearPreview();
            _sampler.Cancel();
        }

        private void ClearPreview()
        {
            if (previewLine != null) previewLine.positionCount = 0;
            PreviewCleared?.Invoke();
        }

        public bool TryCommitScreenPath(IReadOnlyList<float2> screenPath, float durationSeconds)
        {
            if (screenPath == null || screenPath.Count == 0)
            {
                StatusChanged?.Invoke("Hold LMB and drag over the planet.");
                return false;
            }

            float2 pointer = screenPath[screenPath.Count - 1];
            GestureKind gesture = screenPath.Count >= 2
                ? GestureRecognitionPipeline.Recognize(
                    screenPath, durationSeconds, resampleCount, _resampledScreen)
                : GestureKind.Invalid;
            bool directPullSelection = castCamera != null && planetCollider != null &&
                                       _selectedAbility == EarthAbilityIds.PullRock &&
                                       TryProject(pointer, out _);
            bool unifiedEarthWall = selectedElement == ElementId.Earth &&
                                    (_selectedAbility == EarthAbilityIds.LineWall ||
                                     _selectedAbility == EarthAbilityIds.RaisePlatform);
            if (!unifiedEarthWall && !directPullSelection &&
                !MagicGesturePolicy.Matches(gesture, _selectedAbility))
            {
                StatusChanged?.Invoke(gesture == GestureKind.Invalid
                    ? "Hold LMB and drag at least 40 px over the planet."
                    : "This ability needs a different stroke. Try the hint below.");
                return false;
            }

            if (!TryBuildCommand(screenPath, pointer, durationSeconds, out MagicCommand command))
            {
                StatusChanged?.Invoke("Draw directly over the planet surface.");
                return false;
            }

            bool executed;
            if (selectedElement == ElementId.Air) executed = airExecutor.Execute(in command);
            else if (selectedElement == ElementId.Fire || selectedElement == ElementId.Water)
                executed = thermalWaterExecutor.Execute(in command);
            else executed = executor.Execute(in command);

            if (executed)
            {
                StatusChanged?.Invoke(_selectedAbility == EarthAbilityIds.LineWall
                    ? "Earth answered — chipped wall raised."
                    : _selectedAbility == EarthAbilityIds.RaisePlatform
                        ? "Earth answered — stable platform raised."
                        : "Earth answered — terrain edit committed.");
            }
            return executed;
        }

        private bool TryBuildCommand(
            IReadOnlyList<float2> screenPath,
            float2 currentPointer,
            float durationSeconds,
            out MagicCommand command)
        {
            command = default;
            bool hasExecutor = selectedElement == ElementId.Air ? airExecutor != null :
                selectedElement == ElementId.Fire || selectedElement == ElementId.Water
                    ? thermalWaterExecutor != null
                    : executor != null;
            if (screenPath == null || screenPath.Count == 0 || castCamera == null || !hasExecutor || planetCollider == null)
            {
                return false;
            }

            if (_selectedAbility == EarthAbilityIds.PullRock &&
                TryProject(currentPointer, out Vector3 pullAnchor))
            {
                Vector3 pullUp = (pullAnchor - planetCollider.bounds.center).normalized;
                _worldPath.Clear();
                _worldPath.Add(new float3(pullAnchor.x, pullAnchor.y, pullAnchor.z));
                command = CreateCommand(pullAnchor, pullUp, 0.8f);
                return true;
            }

            if (screenPath.Count < 2) return false;

            float2 drag = currentPointer - screenPath[0];
            Vector3 aim = (castCamera.transform.right * drag.x) + (castCamera.transform.up * drag.y);
            if (_selectedAbility == EarthAbilityIds.FlickThrow)
            {
                if (aim.sqrMagnitude < 0.001f) return false;
                Vector3 heldOrigin = executor != null && executor.HeldBody != null
                    ? executor.HeldBody.worldCenterOfMass
                    : transform.position;
                _worldPath.Clear();
                _worldPath.Add(new float3(heldOrigin.x, heldOrigin.y, heldOrigin.z));
                float flickIntensity = MagicGestureKinematics.FlickIntensity(screenPath, durationSeconds);
                command = CreateCommand(heldOrigin, aim, flickIntensity);
                return true;
            }

            GestureResampler.Resample(screenPath, resampleCount, _resampledScreen);
            _worldPath.Clear();
            for (int index = 0; index < _resampledScreen.Count; index++)
            {
                if (!TryProject(_resampledScreen[index], out Vector3 point))
                {
                    continue;
                }

                _worldPath.Add(new float3(point.x, point.y, point.z));
            }

            int minimumWorldPoints = _selectedAbility == EarthAbilityIds.RaisePlatform
                ? 3
                : _selectedAbility == EarthAbilityIds.LineWall ? 2 : 1;
            if (_worldPath.Count < minimumWorldPoints)
            {
                return false;
            }

            if (aim.sqrMagnitude < 0.001f)
            {
                Vector3 anchor = new Vector3(_worldPath[0].x, _worldPath[0].y, _worldPath[0].z);
                aim = (anchor - planetCollider.bounds.center).normalized;
            }

            Vector3 origin = new Vector3(_worldPath[0].x, _worldPath[0].y, _worldPath[0].z);
            float intensity = _selectedAbility == EarthAbilityIds.LineWall
                ? MagicGestureKinematics.WallHoldIntensity(durationSeconds)
                : _selectedAbility == EarthAbilityIds.RaisePlatform
                    ? _platformHeight01
                : Mathf.Clamp01(math.length(drag) / 400f);
            command = CreateCommand(origin, aim, intensity);
            return true;
        }

        private MagicCommand CreateCommand(Vector3 origin, Vector3 aim, float intensity) =>
            CreateCommand(_selectedAbility, origin, aim, intensity);

        private MagicCommand CreateCommand(
            AbilityId ability,
            Vector3 origin,
            Vector3 aim,
            float intensity)
        {
            uint tick = _tick++;
            return new MagicCommand(
                tick,
                1u,
                selectedElement,
                ability,
                new float3(origin.x, origin.y, origin.z),
                new float3(aim.x, aim.y, aim.z),
                _worldPath,
                intensity,
                0u,
                (tick + 1u) * 2654435761u);
        }

        private bool TryProject(float2 screenPoint, out Vector3 point)
        {
            Ray ray = castCamera.ScreenPointToRay(new Vector2(screenPoint.x, screenPoint.y));
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                ray,
                _projectionHits,
                projectionDistance,
                ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            point = default;
            bool found = false;

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _projectionHits[index];
                if (hit.collider != planetCollider || hit.distance >= nearest)
                {
                    continue;
                }

                nearest = hit.distance;
                point = hit.point;
                found = true;
            }

            return found;
        }

        public bool TryReleasePushAtScreenPoint(float2 screenPoint, float heldSeconds)
        {
            if (castCamera == null || executor == null || heldSeconds < 0f) return false;
            Ray ray = castCamera.ScreenPointToRay(new Vector2(screenPoint.x, screenPoint.y));
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                ray,
                pushAssistRadius,
                _projectionHits,
                projectionDistance,
                ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            RaycastHit selected = default;
            Rigidbody selectedBody = null;
            EarthWall selectedWall = null;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _projectionHits[index];
                Rigidbody body = hit.rigidbody;
                EarthWall wall = hit.collider != null ? hit.collider.GetComponentInParent<EarthWall>() : null;
                if (wall == null && (body == null || body.isKinematic)) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                selected = hit;
                selectedBody = body;
                selectedWall = wall;
            }

            if (selectedBody == null && selectedWall == null) return false;
            return executor.TryApplyMagicPush(
                selectedBody,
                selectedWall,
                selected.point,
                ray.direction,
                PushCharge(heldSeconds));
        }

        public bool TryBeginPushAtScreenPoint(float2 screenPoint)
        {
            if (castCamera == null || executor == null) return false;
            Ray ray = castCamera.ScreenPointToRay(new Vector2(screenPoint.x, screenPoint.y));
            if (!TryFindPushTarget(ray, out RaycastHit selected, out Rigidbody selectedBody)) return false;
            return executor.TryBeginVectorField(
                selected.collider,
                selectedBody,
                selected.point,
                ray.direction);
        }

        private bool TryFindPushTarget(Ray ray, out RaycastHit selected, out Rigidbody selectedBody)
        {
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                ray,
                pushAssistRadius,
                _projectionHits,
                projectionDistance,
                ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            selected = default;
            selectedBody = null;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _projectionHits[index];
                Rigidbody body = hit.rigidbody;
                IEarthPhysicalTarget earthTarget = ResolveEarthTarget(hit.collider);
                EarthWall wall = hit.collider != null ? hit.collider.GetComponentInParent<EarthWall>() : null;
                bool validTarget = earthTarget != null
                    ? earthTarget.IsEarthTargetValid
                    : wall != null || (body != null && !body.isKinematic);
                if (!validTarget || hit.distance >= nearest) continue;
                nearest = hit.distance;
                selected = hit;
                selectedBody = body;
            }
            return selected.collider != null;
        }

        private static IEarthPhysicalTarget ResolveEarthTarget(Collider collider)
        {
            if (collider == null) return null;
            EarthWallPiece wallPiece = collider.GetComponentInParent<EarthWallPiece>();
            if (wallPiece != null) return wallPiece;
            EarthPlatformPiece platformPiece = collider.GetComponentInParent<EarthPlatformPiece>();
            if (platformPiece != null) return platformPiece;
            EarthFragment fragment = collider.GetComponentInParent<EarthFragment>();
            if (fragment != null) return fragment;
            EarthWall wall = collider.GetComponentInParent<EarthWall>();
            if (wall != null) return wall;
            PhysicalImpactTarget physical = collider.GetComponentInParent<PhysicalImpactTarget>();
            return physical;
        }

        public static float PushCharge(float heldSeconds) =>
            Mathf.Clamp01(Mathf.Lerp(0.18f, 1f, 1f - Mathf.Exp(-Mathf.Max(0f, heldSeconds) / 0.72f)));

    }
}
