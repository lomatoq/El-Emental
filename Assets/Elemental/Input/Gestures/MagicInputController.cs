using System;
using System.Collections.Generic;
using Elemental.Input.Actions;
using Elemental.Runtime.Characters;
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
        [SerializeField] private EarthInputAdapter inputAdapter;
        [SerializeField] private UnityEngine.Camera castCamera;
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private AirMagicExecutor airExecutor;
        [SerializeField] private ThermalWaterMagicExecutor thermalWaterExecutor;
        [SerializeField] private ElementId selectedElement = ElementId.Earth;
        [SerializeField] private Collider planetCollider;
        [SerializeField] private LineRenderer previewLine;
        [SerializeField] private EarthPreviewPresenter previewPresenter;
        [SerializeField] private EarthGestureProfile gestureProfile;
        [SerializeField] private EarthPillarWaveAbility pillarWaveAbility;
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
        private readonly EarthStrokeSampler _strokeSampler = new EarthStrokeSampler();
        private readonly EarthTemplateRecognizer _templateRecognizer = new EarthTemplateRecognizer();
        private readonly List<PointerStrokeSample> _normalizedExternalStroke =
            new List<PointerStrokeSample>(192);
        private readonly List<uint2> _quantizedResolvedGeometry = new List<uint2>(32);
        private readonly List<float2> _resampledScreen = new List<float2>(24);
        private readonly List<float3> _worldPath = new List<float3>(24);
        private readonly List<Vector3> _previewPoints = new List<Vector3>(96);
        private readonly RaycastHit[] _projectionHits = new RaycastHit[ProjectionHitCapacity];

        private AbilityId _selectedAbility = EarthAbilityIds.LineWall;
        private uint _tick;
        private bool _pushCharging;
        private float _pushStartedAt;
        private bool _pushTargetLocked;
        private BendSessionState _bendSession;
        private BendTuning _bendTuning;
        private bool _earthAcquirePending;
        private bool _wallGesturePending;
        private bool _groundWaveGesturePending;
        private float _platformHeight01 = 0.35f;
        private float _platformTilt01 = 0.5f;
        private float _wallHeight01 = 0.35f;
        private float _wallThickness01 = 0.5f;
        private float _waveSector01 = 0.32f;
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
        private PlanetMotor _motor;
        private bool _gravityWellHeld;
        private float _gravityWellFocusDistance;
        private EarthGestureResult _lastGestureResult;
        private EarthReticleState _reticleState = EarthReticleState.Invalid;
        private EarthResolvedInputCommand _lastResolvedInputCommand;
        private EarthTechniqueCommand _lastTechniqueCommand;

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
        public EarthGestureResult LastGestureResult => _lastGestureResult;
        public EarthReticleState ReticleState => _reticleState;
        public EarthResolvedInputCommand LastResolvedInputCommand => _lastResolvedInputCommand;
        public EarthTechniqueCommand LastTechniqueCommand => _lastTechniqueCommand;
        public string BendParameterLabel => executor != null && executor.HeldBody != null
            ? "HOLD DISTANCE"
            : _groundWaveGesturePending ? "WAVE WIDTH"
            : _selectedAbility == EarthAbilityIds.RaisePlatform
                ? inputAdapter != null && inputAdapter.BendModifierHeld ? "PLATFORM TILT" : "PLATFORM HEIGHT"
            : _selectedAbility == EarthAbilityIds.LineWall
                ? inputAdapter != null && inputAdapter.BendModifierHeld ? "WALL THICKNESS" : "WALL HEIGHT"
            : "FORM SCALE";
        public float BendParameter01 => executor != null && executor.HeldBody != null
            ? Mathf.InverseLerp(minimumHoldDistance, maximumHoldDistance, _holdDistance)
            : _groundWaveGesturePending ? _waveSector01
            : _selectedAbility == EarthAbilityIds.RaisePlatform
                ? inputAdapter != null && inputAdapter.BendModifierHeld ? _platformTilt01 : _platformHeight01
            : _selectedAbility == EarthAbilityIds.LineWall
                ? inputAdapter != null && inputAdapter.BendModifierHeld ? _wallThickness01 : _wallHeight01
            : _formingAmount01;
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
            ConfigureInputAdapter(configuredPlayerInput);
            castCamera = configuredCamera;
            executor = configuredExecutor;
            planetCollider = configuredPlanetCollider;
            previewLine = configuredPreview;
            ConfigurePreviewPresenter(configuredPreview);
            airExecutor = null;
            thermalWaterExecutor = null;
            selectedElement = ElementId.Earth;
            _selectedAbility = EarthAbilityIds.LineWall;
        }

        private void ConfigureInputAdapter(PlayerInput configuredPlayerInput)
        {
            playerInput = configuredPlayerInput != null
                ? configuredPlayerInput
                : GetComponent<PlayerInput>();
            inputAdapter = GetComponent<EarthInputAdapter>();
            if (inputAdapter == null) inputAdapter = gameObject.AddComponent<EarthInputAdapter>();
            inputAdapter.Configure(playerInput);
        }

        public void ConfigureGestureProfile(EarthGestureProfile configuredProfile) =>
            gestureProfile = configuredProfile;

        public void ConfigureEarthTechniques(EarthPillarWaveAbility configuredPillarWave) =>
            pillarWaveAbility = configuredPillarWave;

        private void ConfigurePreviewPresenter(LineRenderer configuredLine)
        {
            previewPresenter = GetComponent<EarthPreviewPresenter>();
            if (previewPresenter == null) previewPresenter = gameObject.AddComponent<EarthPreviewPresenter>();
            previewPresenter.Configure(configuredLine);
        }

        public void ConfigureAir(
            PlayerInput configuredPlayerInput,
            UnityEngine.Camera configuredCamera,
            AirMagicExecutor configuredExecutor,
            Collider configuredPlanetCollider,
            LineRenderer configuredPreview)
        {
            playerInput = configuredPlayerInput;
            ConfigureInputAdapter(configuredPlayerInput);
            castCamera = configuredCamera;
            airExecutor = configuredExecutor;
            executor = null;
            thermalWaterExecutor = null;
            planetCollider = configuredPlanetCollider;
            previewLine = configuredPreview;
            ConfigurePreviewPresenter(configuredPreview);
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
            ConfigureInputAdapter(configuredPlayerInput);
            castCamera = configuredCamera;
            thermalWaterExecutor = configuredExecutor;
            executor = null;
            airExecutor = null;
            planetCollider = configuredPlanetCollider;
            previewLine = configuredPreview;
            ConfigurePreviewPresenter(configuredPreview);
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
            _motor = GetComponent<PlanetMotor>();
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }
            if (inputAdapter == null) ConfigureInputAdapter(playerInput);
            if (pillarWaveAbility == null) pillarWaveAbility = GetComponent<EarthPillarWaveAbility>();
            if (previewPresenter == null)
            {
                previewPresenter = GetComponent<EarthPreviewPresenter>();
                if (previewPresenter != null) previewPresenter.Configure(previewLine);
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
            if (inputAdapter == null)
            {
                Debug.LogError("[Elemental] Earth input adapter is not configured.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            executor?.CancelHeldEarthControl();
            executor?.CancelVectorField();
            executor?.CancelGravityWell();
            _gravityWellHeld = false;
            _sampler.Cancel();
            _strokeSampler.Cancel();
            _earthAcquirePending = false;
            _wallGesturePending = false;
            _groundWaveGesturePending = false;
            _bendSession?.Cancel();
            _motor?.SetCastStance(0f);
            ClearPreview();
        }

        private void CancelInteraction()
        {
            executor?.CancelHeldEarthControl();
            executor?.CancelVectorField();
            executor?.CancelGravityWell();
            _gravityWellHeld = false;
            _pushCharging = false;
            _pushTargetLocked = false;
            _earthAcquirePending = false;
            _wallGesturePending = false;
            _groundWaveGesturePending = false;
            _bendSession?.Cancel();
            _sampler.Cancel();
            _strokeSampler.Cancel();
            _motor?.SetCastStance(0f);
            PushChargeChanged?.Invoke(0f);
            ClearPreview();
            StatusChanged?.Invoke("Earth gesture canceled.");
        }

        private void Update()
        {
            if (inputAdapter == null) return;
            if (inputAdapter.CancelPressed)
            {
                CancelInteraction();
                return;
            }
            UpdateElementSelection();
            UpdateAbilitySelection();
            Vector2 pointer = inputAdapter.PointerPixels;
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

            if (inputAdapter.BendPrimaryPressed)
            {
                _sampler.Begin(pointerFloat, Time.unscaledTime);
                Vector2 viewport = inputAdapter.PointerViewport01;
                _strokeSampler.Begin(new float2(viewport.x, viewport.y), Time.unscaledTime);
                if (selectedElement == ElementId.Earth)
                {
                    if (!TryBeginGroundWaveGesture(pointerFloat))
                        BeginEarthAcquireDecision(pointerFloat);
                }
            }

            if (_sampler.IsActive)
            {
                _sampler.Sample(pointerFloat);
                Vector2 viewport = inputAdapter.PointerViewport01;
                _strokeSampler.Sample(new float2(viewport.x, viewport.y), Time.unscaledTime);
                if (_groundWaveGesturePending)
                    UpdateGroundWavePreview();
                else if (selectedElement == ElementId.Earth && _bendSession.IsActive)
                    UpdateUnifiedEarthBend(pointerFloat);
                else
                    UpdatePreview(pointerFloat);
            }

            if (inputAdapter.BendPrimaryReleased)
            {
                if (_groundWaveGesturePending)
                    CommitGroundWave(pointerFloat);
                else if (selectedElement == ElementId.Earth && _bendSession.IsActive)
                    CommitUnifiedEarthBend(pointerFloat);
                else
                    Commit(pointerFloat);
            }
            else if (selectedElement == ElementId.Earth &&
                     _bendSession.IsActive && _sampler.IsActive && !inputAdapter.BendPrimaryHeld)
            {
                // Recover from a release event lost while the game window was unfocused.
                // Otherwise a body can remain controlled indefinitely beside the player.
                CommitUnifiedEarthBend(pointerFloat);
            }
            UpdateCastStance();
        }

        private void UpdateCastStance()
        {
            if (_motor == null) return;
            bool active = _groundWaveGesturePending || _gravityWellHeld || _pushCharging ||
                          (_bendSession != null && _bendSession.IsActive) ||
                          (executor != null && (executor.HeldBody != null || executor.IsRepairActive));
            if (!active)
            {
                _motor.SetCastStance(0f);
                return;
            }
            float mass = executor != null && executor.HeldBody != null ? executor.HeldBody.mass : 0f;
            float massBrace = 1f - Mathf.Exp(-Mathf.Max(0f, mass) / 220f);
            float charge = _bendSession != null ? Mathf.Max(_bendSession.Charge01, _bendSession.Amount01) : 0f;
            _motor.SetCastStance(Mathf.Clamp01(0.2f + (massBrace * 0.5f) + (charge * 0.3f)));
        }

        private void UpdateBendPowerInput()
        {
            if (inputAdapter.BendForcePressed)
                _bendSession.BeginCharge();
            if (inputAdapter.BendForceReleased)
                _bendSession.EndCharge();
            if (_wallGesturePending && _selectedAbility == EarthAbilityIds.RaisePlatform &&
                (inputAdapter == null || !inputAdapter.BendModifierHeld))
                _platformHeight01 = Mathf.Max(_platformHeight01, _bendSession.Charge01);
            if (!_wallGesturePending || Mathf.Abs(inputAdapter.BendParameter) <= 0.001f) return;
            float delta = inputAdapter.BendParameter * 0.001f;
            if (_selectedAbility == EarthAbilityIds.RaisePlatform)
            {
                if (inputAdapter.BendModifierHeld)
                    _platformTilt01 = Mathf.Clamp01(_platformTilt01 + delta);
                else
                    _platformHeight01 = Mathf.Clamp01(_platformHeight01 + delta);
            }
            else if (inputAdapter.BendModifierHeld)
                _wallThickness01 = Mathf.Clamp01(_wallThickness01 + delta);
            else
                _wallHeight01 = Mathf.Clamp01(_wallHeight01 + delta);
        }

        private void UpdateStandalonePush(float2 pointer)
        {
            if (inputAdapter.BendForcePressed)
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
            if (!_pushCharging || !inputAdapter.BendForceReleased) return;
            if (_pushTargetLocked) executor.ReleaseVectorField();
            _pushCharging = false;
            _pushTargetLocked = false;
            PushChargeChanged?.Invoke(0f);
        }

        private void UpdateGravityWellInput(float2 pointer)
        {
            if (inputAdapter == null || executor == null || castCamera == null) return;
            if (inputAdapter.BendFieldPressed)
            {
                _gravityWellHeld = TryBeginGravityWellAtScreenPoint(pointer);
                StatusChanged?.Invoke(_gravityWellHeld
                    ? "GRAVITY GRIP — hold MMB to pull shards and tear stressed Earth apart."
                    : "No earth surface or structure under the gravity grip.");
            }
            if (_gravityWellHeld && inputAdapter.BendFieldHeld)
                TryUpdateGravityWellAtScreenPoint(pointer);
            if (!_gravityWellHeld || !inputAdapter.BendFieldReleased) return;
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

        private bool TryBeginGroundWaveGesture(float2 pointer)
        {
            if (inputAdapter == null || !inputAdapter.BendForceHeld || pillarWaveAbility == null ||
                castCamera == null || planetCollider == null) return false;
            Ray ray = castCamera.ScreenPointToRay(new Vector2(pointer.x, pointer.y));
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                ray, _projectionHits, projectionDistance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Collider selected = null;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _projectionHits[index];
                if (hit.collider == null || hit.distance >= nearest) continue;
                if (_casterBody != null && hit.collider.transform.IsChildOf(_casterBody.transform)) continue;
                nearest = hit.distance;
                selected = hit.collider;
            }
            bool terrain = selected == planetCollider ||
                           (selected != null && selected.transform.IsChildOf(planetCollider.transform));
            if (!terrain) return false;

            executor?.CancelVectorField();
            _pushCharging = false;
            _pushTargetLocked = false;
            _groundWaveGesturePending = true;
            _waveSector01 = 0.32f;
            _bendStartedAt = Time.unscaledTime;
            PushChargeChanged?.Invoke(0f);
            StatusChanged?.Invoke("GROUND WAVE — sweep over the ground; wheel sets crest width.");
            return true;
        }

        private void UpdateGroundWavePreview()
        {
            if (inputAdapter != null && Mathf.Abs(inputAdapter.BendParameter) > 0.001f)
                _waveSector01 = Mathf.Clamp01(
                    _waveSector01 + (inputAdapter.BendParameter * 0.001f));
            _previewPoints.Clear();
            if (_sampler.Points.Count < 2)
            {
                ClearPreview();
                return;
            }
            GestureResampler.Resample(_sampler.Points, resampleCount, _resampledScreen);
            for (int index = 0; index < _resampledScreen.Count; index++)
                if (TryProject(_resampledScreen[index], out Vector3 point)) _previewPoints.Add(point);
            if (previewPresenter != null) previewPresenter.Present(_previewPoints);
            PreviewChanged?.Invoke(_previewPoints);
            PushChargeChanged?.Invoke(PushCharge(Time.unscaledTime - _bendStartedAt));
        }

        private void CommitGroundWave(float2 pointer)
        {
            _sampler.End(pointer);
            if (_strokeSampler.IsActive && inputAdapter != null)
            {
                Vector2 viewport = inputAdapter.PointerViewport01;
                _strokeSampler.End(new float2(viewport.x, viewport.y), Time.unscaledTime);
            }
            EarthGestureSettings settings = gestureProfile != null
                ? gestureProfile.Settings
                : EarthGestureSettings.Default;
            EarthInputContext context = new EarthInputContext(
                EarthSourceKind.Terrain, false, true, true, false,
                inputAdapter != null && inputAdapter.BendModifierHeld);
            _lastGestureResult = _templateRecognizer.Recognize(
                _strokeSampler.Samples,
                EarthIntentResolver.RelevantTemplates(in context),
                in settings);
            EarthResolvedIntent intent = EarthIntentResolver.Resolve(in context, in _lastGestureResult);
            _reticleState = intent.Reticle;
            float elapsed = Mathf.Max(0.001f, Time.unscaledTime - _bendStartedAt);
            Vector3 start = default;
            Vector3 end = default;
            bool projected = _sampler.Points.Count >= 2 &&
                             TryProject(_sampler.Points[0], out start) &&
                             TryProject(_sampler.Points[_sampler.Points.Count - 1], out end);
            if (!intent.Accepted || intent.Kind != EarthIntentKind.GroundWave || !projected)
            {
                StatusChanged?.Invoke("Ground wave needs a deliberate sweep across visible terrain.");
            }
            else
            {
                Vector3 center = planetCollider.bounds.center;
                Vector3 up = (_casterBody != null ? _casterBody.worldCenterOfMass : transform.position) - center;
                up = up.sqrMagnitude > 0.01f ? up.normalized : transform.up;
                Vector3 direction = Vector3.ProjectOnPlane(end - start, up).normalized;
                if (direction.sqrMagnitude < 0.5f)
                    direction = Vector3.ProjectOnPlane(castCamera.transform.forward, up).normalized;
                float power = PushCharge(elapsed);
                bool cast = pillarWaveAbility.TryCast(
                    direction, _waveSector01, power, out EarthTechniqueRejectReason rejection);
                if (cast)
                {
                    uint tick = _tick++;
                    EarthTechniqueModifierFlags modifiers =
                        EarthTechniqueModifierFlags.Primary | EarthTechniqueModifierFlags.Force;
                    if (inputAdapter != null && inputAdapter.BendModifierHeld)
                        modifiers |= EarthTechniqueModifierFlags.Modifier;
                    _lastTechniqueCommand = new EarthTechniqueCommand(
                        tick, 1u, EarthTechniqueKind.GroundWave, 0u, 0,
                        new float3(start.x, start.y, start.z),
                        new float3(direction.x, direction.y, direction.z),
                        power, _waveSector01, modifiers,
                        (tick + 1u) * 2654435761u,
                        _lastGestureResult.Features.GeometryDigest);
                    StatusChanged?.Invoke($"Ground wave released — {pillarWaveAbility.LastColumnCount} rising columns.");
                }
                else StatusChanged?.Invoke($"Ground wave rejected: {rejection}.");
            }

            _groundWaveGesturePending = false;
            _sampler.Cancel();
            _strokeSampler.Cancel();
            PushChargeChanged?.Invoke(0f);
            ClearPreview();
        }

        private void BeginEarthAcquireDecision(float2 pointer)
        {
            BendOriginMode origin = inputAdapter != null && inputAdapter.BendModifierHeld
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
            _platformHeight01 = 0.35f;
            _platformTilt01 = 0.5f;
            _wallHeight01 = 0.35f;
            _wallThickness01 = 0.5f;
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
                AbilityId next = ResolveStructureAbilityFromCurrentStroke();
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
            float wheel = readWheel && inputAdapter != null ? inputAdapter.BendParameter : 0f;
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
            if (inputAdapter != null && inputAdapter.BendForceHeld)
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
                if (_casterBody != null && hit.collider != null &&
                    hit.collider.transform.IsChildOf(_casterBody.transform)) continue;
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
            if (_strokeSampler.IsActive && inputAdapter != null)
            {
                Vector2 viewport = inputAdapter.PointerViewport01;
                _strokeSampler.End(new float2(viewport.x, viewport.y), Time.unscaledTime);
            }
            float elapsed = Mathf.Max(0.001f, Time.unscaledTime - _bendStartedAt);
            if (_wallGesturePending)
            {
                _bendSession.Cancel();
                _wallGesturePending = false;
                _earthAcquirePending = false;
                _selectedAbility = ResolveStructureAbilityFromCurrentStroke();
                TryCommitScreenPath(_sampler.Points, elapsed);
                ClearPreview();
                _sampler.Cancel();
                _strokeSampler.Cancel();
                return;
            }

            if (_earthAcquirePending && !TryAcquireEarthVolume(_bendStartPointer, elapsed))
            {
                _sampler.Cancel();
                _strokeSampler.Cancel();
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
            _strokeSampler.Cancel();
            PushChargeChanged?.Invoke(0f);
        }

        private AbilityId ResolveStructureAbilityFromCurrentStroke()
        {
            EarthGestureSettings settings = gestureProfile != null
                ? gestureProfile.Settings
                : EarthGestureSettings.Default;
            EarthInputContext context = new EarthInputContext(
                EarthSourceKind.Terrain,
                false,
                true,
                inputAdapter != null && inputAdapter.BendForceHeld,
                false,
                inputAdapter != null && inputAdapter.BendModifierHeld);
            _lastGestureResult = _templateRecognizer.Recognize(
                _strokeSampler.Samples,
                EarthIntentResolver.RelevantTemplates(in context),
                in settings);
            EarthResolvedIntent intent = EarthIntentResolver.Resolve(in context, in _lastGestureResult);
            _reticleState = intent.Reticle;
            if (intent.Accepted && intent.Kind == EarthIntentKind.RaisePlatform)
                return EarthAbilityIds.RaisePlatform;
            if (intent.Accepted && intent.Kind == EarthIntentKind.RaiseWall)
                return EarthAbilityIds.LineWall;

            // During the first few samples confidence is intentionally low. Preserve a
            // responsive preview using the deterministic topology fallback; release still
            // goes through confidence and ambiguity rejection.
            EarthStructureGestureResult fallback = EarthStructureGestureSolver.Classify(_sampler.Points);
            return fallback.Kind == EarthStructureGestureKind.Platform
                ? EarthAbilityIds.RaisePlatform
                : EarthAbilityIds.LineWall;
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
            if (inputAdapter.DebugAbilityPressed(1))
            {
                _selectedAbility = selectedElement == ElementId.Air ? AirAbilityIds.GustCorridor :
                    selectedElement == ElementId.Fire ? FireAbilityIds.HeatJet :
                    selectedElement == ElementId.Water ? WaterAbilityIds.GatherWater : EarthAbilityIds.LineWall;
            }
            else if (inputAdapter.DebugAbilityPressed(2))
            {
                _selectedAbility = selectedElement == ElementId.Air ? AirAbilityIds.Vortex :
                    selectedElement == ElementId.Fire ? FireAbilityIds.ThermalFocus :
                    selectedElement == ElementId.Water ? WaterAbilityIds.WaterJet : EarthAbilityIds.PullRock;
            }
            else if (inputAdapter.DebugAbilityPressed(3))
            {
                _selectedAbility = selectedElement == ElementId.Air ? AirAbilityIds.LiftColumn :
                    selectedElement == ElementId.Water ? WaterAbilityIds.FreezeBridge : EarthAbilityIds.FlickThrow;
            }
            else if ((selectedElement == ElementId.Air || selectedElement == ElementId.Water) &&
                     inputAdapter.DebugAbilityPressed(4))
            {
                _selectedAbility = selectedElement == ElementId.Air ? AirAbilityIds.AirBrake : WaterAbilityIds.SteamBurst;
            }
        }

        private void UpdateElementSelection()
        {
            if (inputAdapter.ElementFirePressed) SelectElement(ElementId.Fire);
            else if (inputAdapter.ElementWaterPressed) SelectElement(ElementId.Water);
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
            if (previewPresenter != null) previewPresenter.Present(_previewPoints);
            else if (previewLine != null)
            {
                previewLine.positionCount = _previewPoints.Count;
                for (int index = 0; index < _previewPoints.Count; index++)
                    previewLine.SetPosition(index, _previewPoints[index]);
            }
            PreviewChanged?.Invoke(_previewPoints);
            return _previewPoints.Count > 0;
        }

        private void Commit(float2 pointer)
        {
            _sampler.End(pointer);
            if (_strokeSampler.IsActive && inputAdapter != null)
            {
                Vector2 viewport = inputAdapter.PointerViewport01;
                _strokeSampler.End(new float2(viewport.x, viewport.y), Time.unscaledTime);
            }
            float duration = Time.unscaledTime - _sampler.StartTime;
            TryCommitScreenPath(_sampler.Points, duration);
            ClearPreview();
            _sampler.Cancel();
            _strokeSampler.Cancel();
        }

        private void ClearPreview()
        {
            if (previewPresenter != null) previewPresenter.Clear();
            else if (previewLine != null) previewLine.positionCount = 0;
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
            if (unifiedEarthWall)
            {
                EarthResolvedIntent resolved = ResolveStructureIntent(screenPath, durationSeconds);
                if (!resolved.Accepted)
                {
                    StatusChanged?.Invoke(resolved.Reticle == EarthReticleState.Ambiguous
                        ? "Ambiguous Earth shape - draw a straighter wall or a clearer platform outline."
                        : "Draw a deliberate wall line or platform outline over the planet.");
                    return false;
                }
                _selectedAbility = resolved.Kind == EarthIntentKind.RaisePlatform
                    ? EarthAbilityIds.RaisePlatform
                    : EarthAbilityIds.LineWall;
            }
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
                if (selectedElement == ElementId.Earth)
                    RecordResolvedInputCommand(in command);
                StatusChanged?.Invoke(_selectedAbility == EarthAbilityIds.LineWall
                    ? "Earth answered — chipped wall raised."
                    : _selectedAbility == EarthAbilityIds.RaisePlatform
                        ? "Earth answered — stable platform raised."
                        : "Earth answered — terrain edit committed.");
            }
            return executed;
        }

        private void RecordResolvedInputCommand(in MagicCommand command)
        {
            EarthInputCommandQuantizer.QuantizeViewportGeometry(
                _normalizedExternalStroke,
                _quantizedResolvedGeometry);
            uint2[] immutableGeometry = _quantizedResolvedGeometry.ToArray();
            EarthIntentKind intent = command.Ability == EarthAbilityIds.LineWall
                ? EarthIntentKind.RaiseWall
                : command.Ability == EarthAbilityIds.RaisePlatform
                    ? EarthIntentKind.RaisePlatform
                    : command.Ability == EarthAbilityIds.FlickThrow
                        ? EarthIntentKind.Throw
                        : EarthIntentKind.Acquire;
            EarthInputModifierFlags modifiers = EarthInputModifierFlags.None;
            if (inputAdapter != null && inputAdapter.BendModifierHeld)
                modifiers |= EarthInputModifierFlags.Modifier;
            if (inputAdapter != null && inputAdapter.BendForceHeld)
                modifiers |= EarthInputModifierFlags.Force;
            if (inputAdapter != null && inputAdapter.BendFieldHeld)
                modifiers |= EarthInputModifierFlags.Field;
            _lastResolvedInputCommand = new EarthResolvedInputCommand(
                intent,
                0u,
                0u,
                immutableGeometry,
                EarthInputCommandQuantizer.Quantize01(command.Intensity),
                EarthInputCommandQuantizer.Quantize01(BendParameter01),
                modifiers,
                command.Tick,
                command.Tick,
                command.Seed,
                _lastGestureResult.Features.GeometryDigest);
            _normalizedExternalStroke.Clear();
        }

        private EarthResolvedIntent ResolveStructureIntent(
            IReadOnlyList<float2> screenPath,
            float durationSeconds)
        {
            _normalizedExternalStroke.Clear();
            if (screenPath != null && screenPath.Count > 0)
            {
                Rect viewport = castCamera != null
                    ? castCamera.pixelRect
                    : new Rect(0f, 0f, Screen.width, Screen.height);
                float width = Mathf.Max(1f, viewport.width);
                float height = Mathf.Max(1f, viewport.height);
                float duration = Mathf.Max(0.001f, durationSeconds);
                for (int index = 0; index < screenPath.Count; index++)
                {
                    float t = screenPath.Count > 1 ? index / (float)(screenPath.Count - 1) : 0f;
                    float2 point = screenPath[index];
                    float2 normalized = new float2(
                        (point.x - viewport.xMin) / width,
                        (point.y - viewport.yMin) / height);
                    _normalizedExternalStroke.Add(new PointerStrokeSample(normalized, t * duration));
                }
            }

            EarthInputContext context = new EarthInputContext(
                EarthSourceKind.Terrain,
                false,
                true,
                false,
                false,
                inputAdapter != null && inputAdapter.BendModifierHeld);
            EarthGestureSettings settings = gestureProfile != null
                ? gestureProfile.Settings
                : EarthGestureSettings.Default;
            _lastGestureResult = EarthIntentResolver.NeedsGestureRecognition(in context)
                ? _templateRecognizer.Recognize(
                    _normalizedExternalStroke,
                    EarthIntentResolver.RelevantTemplates(in context),
                    in settings)
                : EarthGestureResult.Invalid();
            EarthResolvedIntent resolved = EarthIntentResolver.Resolve(in context, in _lastGestureResult);
            _reticleState = resolved.Reticle;
            return resolved;
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
                ? Mathf.Max(MagicGestureKinematics.WallHoldIntensity(durationSeconds), _wallHeight01)
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
            uint modifiers = ability == EarthAbilityIds.LineWall
                ? EarthTechniqueParameterCodec.Pack(_wallHeight01, _wallThickness01)
                : ability == EarthAbilityIds.RaisePlatform
                    ? EarthTechniqueParameterCodec.Pack(_platformHeight01, _platformTilt01)
                    : 0u;
            return new MagicCommand(
                tick,
                1u,
                selectedElement,
                ability,
                new float3(origin.x, origin.y, origin.z),
                new float3(aim.x, aim.y, aim.z),
                _worldPath,
                intensity,
                modifiers,
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
            return EarthTargetResolver.ResolvePhysicalTarget(collider);
        }

        public static float PushCharge(float heldSeconds) =>
            Mathf.Clamp01(Mathf.Lerp(0.18f, 1f, 1f - Mathf.Exp(-Mathf.Max(0f, heldSeconds) / 0.72f)));

    }
}
