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
using Elemental.Simulation.Matter;
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
        [SerializeField] private EarthSurfaceQueryService surfaceQueries;
        [SerializeField] private LineRenderer previewLine;
        [SerializeField] private EarthPreviewPresenter previewPresenter;
        [SerializeField] private EarthGestureProfile gestureProfile;
        [SerializeField] private EarthPillarWaveAbility pillarWaveAbility;
        [SerializeField] private EarthActionRouterBehaviour actionRouter;
        [SerializeField] private EarthQuickCastProfile quickCastProfile;
        [SerializeField] private EarthArmorProfile armorProfile;
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
        private readonly EarthTargetQueryService _targetQueryService = new EarthTargetQueryService();
        private readonly EarthGestureTokenizer _gestureTokenizer = new EarthGestureTokenizer();
        private EarthScrollAccumulator _scrollAccumulator =
            new EarthScrollAccumulator(EarthScrollDeviceProfile.DetentWheel);
        private readonly EarthIntentCandidate[] _rankedIntentCandidates = new EarthIntentCandidate[8];

        private AbilityId _selectedAbility = EarthAbilityIds.LineWall;
        private uint _tick;
        private bool _pushCharging;
        private float _pushStartedAt;
        private bool _pushTargetLocked;
        private float2 _pushPreviousPointer;
        private float _pushTravelViewport;
        private float2 _pushVelocityViewportPerSecond;
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
        private EarthCircularGestureState _gravityCircularState;
        private EarthCircularGestureSample _gravityCircularSample;
        private EarthGravityStructureIntent _gravityReportedIntent;
        private int _gravityReportedPhaseStep = -1;
        private bool _gravityThrowOwned;
        private EarthGestureResult _lastGestureResult;
        private EarthReticleState _reticleState = EarthReticleState.Invalid;
        private EarthResolvedInputCommand _lastResolvedInputCommand;
        private EarthTechniqueCommand _lastTechniqueCommand;
        private EarthActionIntent _lastActionIntent;
        private EarthQuickStoneSession _quickStoneSession;
        private Vector3 _quickStoneExtractionStart;
        private Vector3 _quickStoneExtractionEnd;
        private Vector3 _quickStoneBufferedDirection;
        private int _lastRoutedInputFrame = -1;
        private EarthArmorController _armorController;
        private MagicExecutor _armorConfiguredExecutor;
        private bool _armorOwnsField;
        private int _armorComboPhase = -1;
        private bool _armorBarrageRecorded;
        private float _nextArmorAutomaticFireAt;
        private bool _suppressPrimaryUntilReleased;
        private bool _drawSurfaceLocked;
        private EarthSurfaceSample _drawSurface;
        private IEarthPluckableStructure _pluckSource;
        private Vector3 _pluckPoint;
        private bool _pluckPending;
        private EarthScrollState _scrollState;
        private EarthGestureToken _lastGestureToken;
        private EarthGestureTargetContext _gesturePointerDownTarget;
        private int _rankedIntentCount;

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
        public EarthCircularGestureDirection GravityGestureDirection => _gravityCircularSample.Direction;
        public float GravityGesturePhase01 => _gravityCircularSample.Phase01;
        public Vector3 VectorFieldDirection => executor != null ? executor.VectorFieldDirection : Vector3.zero;
        public float PlatformPreviewHeight01 => _selectedAbility == EarthAbilityIds.RaisePlatform
            ? _platformHeight01
            : 0f;
        public EarthGestureResult LastGestureResult => _lastGestureResult;
        public EarthReticleState ReticleState => _reticleState;
        public EarthResolvedInputCommand LastResolvedInputCommand => _lastResolvedInputCommand;
        public EarthTechniqueCommand LastTechniqueCommand => _lastTechniqueCommand;
        public EarthActionIntent LastActionIntent => _lastActionIntent;
        public EarthGestureToken LastGestureToken => _lastGestureToken;
        public EarthScrollState ScrollState => _scrollState;
        public EarthScrollDeviceProfile ScrollDeviceProfile => gestureProfile != null
            ? gestureProfile.ScrollDeviceProfile
            : EarthScrollDeviceProfile.DetentWheel;
        public int RankedIntentCount => _rankedIntentCount;
        public EarthIntentCandidate GetRankedIntentCandidate(int index) =>
            index >= 0 && index < _rankedIntentCount ? _rankedIntentCandidates[index] : default;
        public bool IsQuickStonePrimed => _quickStoneSession != null && _quickStoneSession.IsPrimed;
        public float QuickStonePrime01 => _quickStoneSession != null
            ? _quickStoneSession.Remaining01(Time.unscaledTime)
            : 0f;
        public bool IsArmorActive => _armorController != null && _armorController.IsActive;
        public EarthActionOwner ActiveActionOwner => actionRouter != null ? actionRouter.Owner : EarthActionOwner.None;
        public float ResonanceCharge01 => actionRouter != null ? actionRouter.ResonanceCharge01 : 0f;
        public int ResonanceStoneCount => actionRouter != null ? actionRouter.ResonanceStoneCount : 0;
        public bool IsResonanceVolleyActive => actionRouter != null && actionRouter.ResonanceVolleyActive;
        public float SurfSpeed => actionRouter != null ? actionRouter.SurfSpeed : 0f;
        public float ArmorPhase01 => _armorController != null ? _armorController.Phase01 : 0f;
        public int ArmorOverscrollSteps => _armorController != null ? _armorController.OverscrollSteps : 0;
        public string BendParameterLabel => IsArmorActive
            ? "ARMOR PHASE"
            : IsQuickStonePrimed
                ? "QUICK WINDOW"
            : executor != null && executor.HeldBody != null
            ? "HOLD DISTANCE"
            : _groundWaveGesturePending ? "WAVE WIDTH"
            : _selectedAbility == EarthAbilityIds.RaisePlatform
                ? inputAdapter != null && inputAdapter.BendModifierHeld ? "PLATFORM TILT" : "PLATFORM HEIGHT"
            : _selectedAbility == EarthAbilityIds.LineWall
                ? inputAdapter != null && inputAdapter.BendModifierHeld ? "WALL THICKNESS" : "WALL HEIGHT"
            : "FORM SCALE";
        public float BendParameter01 => IsArmorActive
            ? ArmorPhase01
            : IsQuickStonePrimed
                ? QuickStonePrime01
            : executor != null && executor.HeldBody != null
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

        /// <summary>
        /// Executes the same short-LMB quick-stone grammar through an explicit
        /// screen point. Used by the editor golden path and end-to-end PlayMode
        /// coverage; normal gameplay still reaches this path through routed input.
        /// </summary>
        public bool TryQuickStoneTapAtScreenPoint(float2 pointer)
        {
            EnsureBendSession();
            EnsureEarthFeatureSessions();
            if (selectedElement != ElementId.Earth || executor == null || castCamera == null)
                return false;
            if (_quickStoneSession != null && _quickStoneSession.IsPrimed)
                return TryFirePrimedQuickStone(pointer);
            if (_bendSession.IsActive || executor.HeldBody != null) return false;
            if (!_bendSession.BeginAcquire(BendOriginMode.Aim)) return false;
            _bendStartPointer = pointer;
            _bendStartedAt = Time.unscaledTime;
            _earthAcquirePending = true;
            _wallGesturePending = false;
            _formingSourceValid = false;
            _formingAmount01 = quickCastProfile != null ? quickCastProfile.PrimeAmount01 : 0.18f;
            bool primed = TryPrimeQuickStone();
            if (!primed)
            {
                _earthAcquirePending = false;
                _bendSession.Cancel();
            }
            return primed;
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

        public void ConfigureEarthTechniques(EarthPillarWaveAbility configuredPillarWave)
        {
            pillarWaveAbility = configuredPillarWave;
            actionRouter?.Configure(
                inputAdapter,
                GetComponent<PlanetInputReader>(),
                this,
                pillarWaveAbility);
        }

        public void ConfigureEarthSurfaceQueries(EarthSurfaceQueryService configuredQueries) =>
            surfaceQueries = configuredQueries;

        public void ConfigureEarthFeatureProfiles(
            EarthQuickCastProfile configuredQuickCast,
            EarthArmorProfile configuredArmor)
        {
            quickCastProfile = configuredQuickCast;
            armorProfile = configuredArmor;
            _quickStoneSession = null;
            _armorConfiguredExecutor = null;
            EnsureEarthFeatureSessions();
        }

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
            _scrollAccumulator = new EarthScrollAccumulator(ScrollDeviceProfile);
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
            if (actionRouter == null) actionRouter = GetComponent<EarthActionRouterBehaviour>();
            if (actionRouter == null) actionRouter = gameObject.AddComponent<EarthActionRouterBehaviour>();
            actionRouter.Configure(
                inputAdapter,
                GetComponent<PlanetInputReader>(),
                this,
                pillarWaveAbility);
            GetComponent<PlanetInputReader>()?.ConfigureActionRouter(actionRouter);
            EnsureEarthFeatureSessions();
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

        private void EnsureEarthFeatureSessions()
        {
            if (_quickStoneSession == null)
            {
                EarthQuickCastProfileData data = quickCastProfile != null
                    ? quickCastProfile.Data
                    : EarthQuickCastProfileData.Default;
                _quickStoneSession = new EarthQuickStoneSession(in data);
            }
            if (_armorController == null)
            {
                _armorController = GetComponent<EarthArmorController>();
                if (_armorController == null) _armorController = gameObject.AddComponent<EarthArmorController>();
            }
            if (_armorController == null || executor == null || _armorConfiguredExecutor == executor) return;
            _armorController.Configure(
                _casterBody != null ? _casterBody : GetComponent<Rigidbody>(),
                executor.PlanetCenterTransform,
                executor.FragmentPool,
                armorProfile);
            _armorConfiguredExecutor = executor;
        }

        private void UpdateQuickStoneTimeout()
        {
            if (_quickStoneSession == null) return;
            float now = Time.unscaledTime;
            if (_quickStoneSession.IsPrimed && executor != null && executor.HeldFragment != null)
            {
                _quickStoneSession.Refresh(now);
                float extraction01 = _quickStoneSession.Extraction01(now);
                float eased = extraction01 * extraction01 * (3f - 2f * extraction01);
                Vector3 target = Vector3.Lerp(_quickStoneExtractionStart, _quickStoneExtractionEnd, eased);
                executor.UpdateHeldEarthTarget(target, Vector3.zero, Mathf.Lerp(0.18f, 0.42f, eased));
                if (_quickStoneSession.TryConsumeBufferedFire(now, out float bufferedSpeed))
                {
                    FireQuickStone(_quickStoneBufferedDirection, bufferedSpeed);
                    return;
                }
            }
            if (!_quickStoneSession.ExpireIfNeeded(now)) return;
            executor?.CancelHeldEarthControl();
            _bendSession?.Cancel();
            _earthAcquirePending = false;
            _quickStoneSession.Reset();
            StatusChanged?.Invoke("QUICK STONE released - second click window expired.");
        }

        private bool TryPrimeQuickStone()
        {
            float amount = quickCastProfile != null ? quickCastProfile.PrimeAmount01 : 0.18f;
            if (!TryAcquireEarthVolume(_bendStartPointer, 0f, amount) || executor == null ||
                executor.HeldFragment == null) return false;
            if (!_quickStoneSession.TryPrime(Time.unscaledTime, executor.HeldFragment.FragmentId))
            {
                executor.CancelHeldEarthControl();
                _bendSession.Cancel();
                return false;
            }
            Vector3 center = planetCollider != null ? planetCollider.bounds.center : Vector3.zero;
            Vector3 up = executor.HeldFragment.transform.position - center;
            up = up.sqrMagnitude > 0.01f ? up.normalized : transform.up;
            _quickStoneExtractionStart = executor.HeldFragment.Body.worldCenterOfMass;
            float lift = Mathf.Max(0.42f, executor.HeldFragment.GetComponent<Collider>()?.bounds.extents.magnitude ?? 0.42f);
            _quickStoneExtractionEnd = _quickStoneExtractionStart + up * Mathf.Min(0.85f, lift);
            executor.UpdateHeldEarthTarget(_quickStoneExtractionStart, Vector3.zero, 0.18f);
            StatusChanged?.Invoke("QUICK STONE EXTRACTING - click LMB again to buffer the shot.");
            return true;
        }

        private bool TryFirePrimedQuickStone(float2 pointer)
        {
            if (selectedElement != ElementId.Earth || _quickStoneSession == null ||
                !_quickStoneSession.IsPrimed || executor == null || castCamera == null) return false;
            EarthFragment fragment = executor.HeldFragment;
            if (fragment == null || fragment.FragmentId != _quickStoneSession.TargetId)
            {
                _quickStoneSession.Reset();
                return false;
            }
            Ray ray = castCamera.ScreenPointToRay(new Vector2(pointer.x, pointer.y));
            if (!_quickStoneSession.TryFire(Time.unscaledTime, out float speed))
            {
                _quickStoneSession.Reset();
                return false;
            }
            _quickStoneBufferedDirection = ray.direction;
            if (speed <= 0f)
            {
                _suppressPrimaryUntilReleased = true;
                StatusChanged?.Invoke("QUICK FIRE BUFFERED - launches as the stone clears the ground.");
                return true;
            }
            return FireQuickStone(ray.direction, speed);
        }

        private bool FireQuickStone(Vector3 direction, float speed)
        {
            if (executor == null) return false;
            bool fired = executor.ReleaseHeldEarthAtSpeed(
                direction,
                speed,
                _tick++,
                out Vector3 velocity);
            _bendSession.Cancel();
            _earthAcquirePending = false;
            _formingSourceValid = false;
            _quickStoneSession.Reset();
            _suppressPrimaryUntilReleased = fired;
            PushChargeChanged?.Invoke(0f);
            StatusChanged?.Invoke(fired
                ? $"QUICK FIRE - {velocity.magnitude:0.0} m/s."
                : "Quick stone lost before launch.");
            return fired;
        }

        private bool UpdateArmorInput()
        {
            if (inputAdapter == null || _armorController == null) return false;
            bool start = inputAdapter.BendModifierHeld && inputAdapter.BendFieldPressed;
            if (start)
            {
                EndGravityWell();
                executor?.CancelVectorField();
                executor?.CancelHeldEarthControl();
                _bendSession?.Cancel();
                _quickStoneSession?.Reset();
                _armorOwnsField = _armorController.Begin();
                if (_armorOwnsField)
                {
                    _armorComboPhase = 0;
                    _armorBarrageRecorded = false;
                    RecordCombo(EarthTechniqueId.Armor, _armorController.PrimaryMatterId,
                        EarthEventTag.Formed, 0.2f, transform.forward);
                    _nextArmorAutomaticFireAt = Time.unscaledTime;
                    _lastActionIntent = new EarthActionIntent(
                        EarthActionIntentKind.ArmorHold,
                        EarthInputConsumption.Modifier | EarthInputConsumption.Field);
                    StatusChanged?.Invoke("EARTH ARMOR - wheel opens body armor into dome and orbit.");
                }
            }
            bool ownsThisFrame = _armorOwnsField;
            if (!ownsThisFrame) return false;
            _armorController.SetAimDirection(ArmorAimDirection());
            if (_armorController.Phase01 > 0.30f && inputAdapter.BendForcePressed)
            {
                int launched = _armorController.FireAll(ArmorAimDirection());
                if (launched > 0)
                {
                    RecordCombo(EarthTechniqueId.ArmorBarrage, _armorController.PrimaryMatterId,
                        EarthEventTag.Propelled, 1f, ArmorAimDirection());
                    _lastActionIntent = new EarthActionIntent(
                        EarthActionIntentKind.ArmorRadialRelease,
                        EarthInputConsumption.Field | EarthInputConsumption.Force,
                        1f);
                    _armorOwnsField = false;
                    StatusChanged?.Invoke($"ARMOR VOLLEY - {launched} plates launched as a directed fan.");
                }
                return true;
            }
            bool automaticArmorShot = inputAdapter.BendPrimaryHeld &&
                                      Time.unscaledTime >= _nextArmorAutomaticFireAt;
            if (_armorController.Phase01 > 0.30f &&
                (inputAdapter.BendPrimaryPressed || automaticArmorShot))
            {
                if (_armorController.FireNearest(ArmorAimDirection()))
                {
                    if (!_armorBarrageRecorded)
                    {
                        _armorBarrageRecorded = true;
                        RecordCombo(EarthTechniqueId.ArmorBarrage, _armorController.PrimaryMatterId,
                            EarthEventTag.Propelled, _armorController.Phase01, ArmorAimDirection());
                    }
                    float interval = armorProfile != null ? armorProfile.AutomaticFireInterval : 0.13f;
                    _nextArmorAutomaticFireAt = Time.unscaledTime + interval;
                    _lastActionIntent = new EarthActionIntent(
                        EarthActionIntentKind.ArmorSpread,
                        EarthInputConsumption.Field | EarthInputConsumption.Primary,
                        _armorController.Phase01);
                    StatusChanged?.Invoke(
                        $"ARMOR SHOT - {_armorController.ControllablePieceCount} plates remain in formation.");
                    if (!_armorController.IsActive) _armorOwnsField = false;
                }
                return true;
            }
            if (inputAdapter.BendFieldHeld && Mathf.Abs(inputAdapter.BendParameter) > 0.001f)
            {
                EarthArmorInputResult result = _armorController.ApplyWheel(
                    _scrollState.NormalizedDelta,
                    Time.unscaledTime);
                if (result == EarthArmorInputResult.OverscrollArmed)
                {
                    _lastActionIntent = new EarthActionIntent(
                        EarthActionIntentKind.ArmorSpread,
                        EarthInputConsumption.Field | EarthInputConsumption.Parameter,
                        _armorController.Phase01);
                    StatusChanged?.Invoke("ARMOR BURST ARMED - one more quick scroll up confirms.");
                }
                else if (result == EarthArmorInputResult.RadialRelease)
                {
                    _lastActionIntent = new EarthActionIntent(
                        EarthActionIntentKind.ArmorRadialRelease,
                        EarthInputConsumption.Field | EarthInputConsumption.Parameter,
                        1f);
                    _armorController.ReleaseRadially();
                    Vector3 forward = _motor != null ? _motor.FacingForward : transform.forward;
                    EarthTechniqueRejectReason rejection = EarthTechniqueRejectReason.RuntimeUnavailable;
                    bool wave = pillarWaveAbility != null && pillarWaveAbility.TryCast(
                        forward, 1f, 1f, out rejection);
                    StatusChanged?.Invoke(wave
                        ? "ARMOR RADIAL RELEASE - full web wave launched."
                        : $"Armor burst launched; wave rejected: {rejection}.");
                    _armorOwnsField = false;
                    return true;
                }
                else if (result == EarthArmorInputResult.PhaseChanged)
                {
                    int nextPhase = _armorController.Phase01 <= 0.30f
                        ? 0
                        : _armorController.Phase01 <= 0.78f ? 1 : 2;
                    if (nextPhase != _armorComboPhase)
                    {
                        EarthTechniqueId technique = nextPhase == 0
                            ? EarthTechniqueId.ArmorRepack
                            : nextPhase == 1 ? EarthTechniqueId.ArmorDome : EarthTechniqueId.ArmorOrbit;
                        EarthEventTag comboResult = nextPhase == 0
                            ? EarthEventTag.Repaired
                            : EarthEventTag.Formed;
                        RecordCombo(technique, _armorController.PrimaryMatterId,
                            comboResult, _armorController.Phase01, transform.forward);
                        _armorComboPhase = nextPhase;
                    }
                    _lastActionIntent = new EarthActionIntent(
                        _armorController.Phase01 <= 0.3f
                            ? EarthActionIntentKind.ArmorHold
                            : EarthActionIntentKind.ArmorSpread,
                        EarthInputConsumption.Field | EarthInputConsumption.Parameter,
                        _armorController.Phase01);
                }
            }
            if (!inputAdapter.BendFieldReleased) return true;
            _armorController.ReleaseAsDebris();
            _armorOwnsField = false;
            StatusChanged?.Invoke("Earth armor released as physical debris.");
            return true;
        }

        private void RecordCombo(
            EarthTechniqueId technique,
            EarthMatterId matter,
            EarthEventTag result,
            float energy,
            Vector3 direction)
        {
            executor?.ComboRuntime?.RecordTechnique(
                technique,
                matter,
                result,
                _tick++,
                energy,
                direction);
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
            if (_armorController != null && _armorController.IsActive) _armorController.ReleaseAsDebris();
            _quickStoneSession?.Reset();
            _armorOwnsField = false;
            _gravityWellHeld = false;
            _sampler.Cancel();
            _strokeSampler.Cancel();
            _earthAcquirePending = false;
            _pluckPending = false;
            _pluckSource = null;
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
            if (_armorController != null && _armorController.IsActive) _armorController.ReleaseAsDebris();
            _quickStoneSession?.Reset();
            _armorOwnsField = false;
            _gravityWellHeld = false;
            _pushCharging = false;
            _pushTargetLocked = false;
            _earthAcquirePending = false;
            _pluckPending = false;
            _pluckSource = null;
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
            if (actionRouter == null || !actionRouter.isActiveAndEnabled)
                ProcessRoutedInput();
        }

        private void FixedUpdate()
        {
            // A buffered second tap is a physics promise: fire as soon as the
            // extraction clears, even when input routing has no Update between two
            // fixed steps (batch tests, a hitch, or a low rendering frame rate).
            if (_quickStoneSession != null && _quickStoneSession.HasBufferedFire)
                UpdateQuickStoneTimeout();
        }

        public void ProcessRoutedInput()
        {
            if (_lastRoutedInputFrame == Time.frameCount) return;
            _lastRoutedInputFrame = Time.frameCount;
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
            EnsureEarthFeatureSessions();
            _scrollState = _scrollAccumulator.Step(
                inputAdapter.BendParameter,
                Time.unscaledDeltaTime,
                Time.unscaledTime);
            if (selectedElement == ElementId.Earth &&
                !inputAdapter.BendModifierHeld &&
                HandleMatterReturnScroll(pointerFloat))
            {
                UpdateCastStance();
                return;
            }
            UpdateQuickStoneTimeout();
            UpdateSemanticActionIntent(pointerFloat);
            _armorOwnsField = selectedElement == ElementId.Earth &&
                              (actionRouter == null || actionRouter.AllowsArmor) &&
                              UpdateArmorInput();
            if (selectedElement == ElementId.Earth && !_armorOwnsField &&
                (actionRouter == null || actionRouter.AllowsGravity))
                UpdateGravityWellInput(pointerFloat);

            bool activeEarthBend = selectedElement == ElementId.Earth &&
                                   _bendSession != null && _bendSession.IsActive;
            if (activeEarthBend)
            {
                UpdateBendPowerInput();
                _bendSession.Tick(Time.unscaledDeltaTime);
                PushChargeChanged?.Invoke(_bendSession.Charge01);
            }
            else if (actionRouter == null || actionRouter.AllowsVectorField)
            {
                UpdateStandalonePush(pointerFloat);
            }

            bool primaryOwned = actionRouter == null || actionRouter.AllowsPrimaryMagic;
            bool quickFireConsumed = primaryOwned && inputAdapter.BendPrimaryPressed &&
                                     TryFirePrimedQuickStone(pointerFloat);
            if (primaryOwned && inputAdapter.BendPrimaryPressed && !quickFireConsumed)
            {
                _gesturePointerDownTarget = CaptureGestureTarget(pointerFloat);
                _sampler.Begin(pointerFloat, Time.unscaledTime);
                Vector2 viewport = inputAdapter.PointerViewport01;
                _strokeSampler.Begin(new float2(viewport.x, viewport.y), Time.unscaledTime);
                if (selectedElement == ElementId.Earth)
                {
                    BeginEarthAcquireDecision(pointerFloat);
                }
            }

            if (primaryOwned && _sampler.IsActive)
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

            if (primaryOwned && inputAdapter.BendPrimaryReleased)
            {
                if (_suppressPrimaryUntilReleased)
                {
                    _suppressPrimaryUntilReleased = false;
                }
                else if (_groundWaveGesturePending)
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

        private void UpdateSemanticActionIntent(float2 pointer)
        {
            bool grounded = _motor != null && _motor.IsGrounded;
            Vector3 localUp = _motor != null && _motor.LocalUp.sqrMagnitude > 0.5f
                ? _motor.LocalUp.normalized
                : transform.up;
            bool descending = _casterBody != null &&
                              Vector3.Dot(_casterBody.linearVelocity, localUp) < -0.15f;
            float heldSeconds = _sampler.IsActive
                ? Mathf.Max(0f, Time.unscaledTime - _sampler.StartTime)
                : 0f;
            Vector2 viewport = inputAdapter.PointerViewport01;
            Vector2 startViewport = EarthInputAdapter.ScreenToViewport(
                new Vector2(_bendStartPointer.x, _bendStartPointer.y));
            float travelViewport = _sampler.IsActive
                ? Vector2.Distance(viewport, startViewport)
                : 0f;
            bool pointerOverTarget = selectedElement == ElementId.Earth && castCamera != null &&
                                     TryFindPushTarget(
                                         castCamera.ScreenPointToRay(new Vector2(pointer.x, pointer.y)),
                                         out _, out _);
            EarthGestureFrame frame = inputAdapter.CaptureEarthGestureFrame(
                grounded,
                descending,
                !grounded && descending && inputAdapter.BendModifierHeld,
                pointerOverTarget,
                executor != null && executor.HeldBody != null,
                _quickStoneSession != null && _quickStoneSession.IsPrimed,
                executor != null && executor.IsRepairActive,
                heldSeconds,
                travelViewport);
            _lastActionIntent = EarthActionIntentResolver.Resolve(in frame);
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
            if (!_wallGesturePending || Mathf.Abs(_scrollState.NormalizedDelta) <= 0.0001f) return;
            float delta = _scrollState.NormalizedDelta * 0.12f;
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
                _pushPreviousPointer = pointer;
                _pushTravelViewport = 0f;
                _pushVelocityViewportPerSecond = float2.zero;
                _pushTargetLocked = TryBeginPushAtScreenPoint(pointer);
                if (!_pushTargetLocked)
                    StatusChanged?.Invoke("No pushable rock, fragment or wall near the cursor.");
            }
            if (_pushCharging)
            {
                float2 viewportSize = new float2(Mathf.Max(1f, Screen.width), Mathf.Max(1f, Screen.height));
                float2 pointerDeltaViewport = (pointer - _pushPreviousPointer) / viewportSize;
                float deltaSeconds = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
                float2 rawVelocity = pointerDeltaViewport / deltaSeconds;
                float velocityBlend = 1f - Mathf.Exp(-24f * deltaSeconds);
                _pushVelocityViewportPerSecond = math.lerp(
                    _pushVelocityViewportPerSecond, rawVelocity, velocityBlend);
                _pushTravelViewport += math.length(pointerDeltaViewport);
                _pushPreviousPointer = pointer;
                float charge = PushCharge(Time.unscaledTime - _pushStartedAt);
                PushChargeChanged?.Invoke(charge);
                if (_pushTargetLocked && castCamera != null)
                {
                    Ray ray = castCamera.ScreenPointToRay(new Vector2(pointer.x, pointer.y));
                    executor.UpdateVectorField(ray.direction, charge);
                }
            }
            if (!_pushCharging || !inputAdapter.BendForceReleased) return;
            if (_pushTargetLocked && castCamera != null)
            {
                float heldSeconds = Mathf.Max(0f, Time.unscaledTime - _pushStartedAt);
                EarthVectorGestureSample release = EarthVectorGestureSolver.Classify(
                    heldSeconds,
                    _pushTravelViewport,
                    _pushVelocityViewportPerSecond);
                Ray ray = castCamera.ScreenPointToRay(new Vector2(pointer.x, pointer.y));
                Vector3 releaseDirection = ray.direction;
                if (release.Intent == EarthVectorReleaseIntent.ProjectileFlick)
                {
                    Vector3 screenVector = castCamera.transform.right * release.ScreenDirection.x +
                                           castCamera.transform.up * release.ScreenDirection.y;
                    releaseDirection = (screenVector + ray.direction * 0.38f).normalized;
                }
                executor.ReleaseVectorField(release.Intent, releaseDirection, release.Strength01);
                StatusChanged?.Invoke(release.Intent switch
                {
                    EarthVectorReleaseIntent.ProjectileFlick => "PROJECTILE FLICK — Earth launched along the swipe.",
                    EarthVectorReleaseIntent.QuickPulse => "QUICK PULSE — compact reactive shove.",
                    _ => "VECTOR HOLD RELEASED — momentum preserved without a blast."
                });
            }
            _pushCharging = false;
            _pushTargetLocked = false;
            _pushTravelViewport = 0f;
            _pushVelocityViewportPerSecond = float2.zero;
            PushChargeChanged?.Invoke(0f);
        }

        private void UpdateGravityWellInput(float2 pointer)
        {
            if (inputAdapter == null || executor == null || castCamera == null) return;
            if (inputAdapter.BendFieldPressed)
            {
                Vector2 viewport = inputAdapter.PointerViewport01;
                _gravityCircularState = EarthCircularGestureSolver.Begin(new float2(viewport.x, viewport.y));
                _gravityCircularSample = default;
                _gravityReportedIntent = EarthGravityStructureIntent.Neutral;
                _gravityReportedPhaseStep = -1;
                _gravityWellHeld = TryBeginGravityWellAtScreenPoint(pointer);
                StatusChanged?.Invoke(_gravityWellHeld
                    ? "GRAVITY GRIP — circle CW to repair, CCW to unweave; phase sets the amount."
                    : "No earth surface or structure under the gravity grip.");
            }
            if (_gravityWellHeld && inputAdapter.BendFieldHeld)
            {
                if (inputAdapter.BendForcePressed && executor.GravityWellCapturedCount > 0 &&
                    !_gravityCircularSample.Recognized)
                {
                    Ray throwRay = castCamera.ScreenPointToRay(new Vector2(pointer.x, pointer.y));
                    _gravityThrowOwned = executor.BeginGravityClusterThrow(throwRay.direction);
                    if (_gravityThrowOwned)
                        StatusChanged?.Invoke("CLUSTER THROW — tap RMB to launch, hold to compress and blast.");
                }
                if (_gravityThrowOwned && inputAdapter.BendForceHeld)
                {
                    Ray throwRay = castCamera.ScreenPointToRay(new Vector2(pointer.x, pointer.y));
                    executor.UpdateGravityClusterThrow(throwRay.direction);
                    PushChargeChanged?.Invoke(executor.GravityClusterThrowCharge01);
                }
                if (_gravityThrowOwned && inputAdapter.BendForceReleased)
                {
                    Ray throwRay = castCamera.ScreenPointToRay(new Vector2(pointer.x, pointer.y));
                    int launched = executor.ReleaseGravityClusterThrow(throwRay.direction);
                    _gravityThrowOwned = false;
                    _gravityWellHeld = false;
                    PushChargeChanged?.Invoke(0f);
                    StatusChanged?.Invoke($"EARTH CLUSTER RELEASE — {launched} physical pieces launched.");
                    return;
                }
                if (_scrollState.FastFlick && _scrollState.NormalizedDelta < 0f &&
                    executor.GravityWellCapturedCount > 0)
                {
                    Vector3 fallback = executor.GravityWellFocus -
                                       (_motor != null ? _motor.LocalUp : transform.up) *
                                       executor.GravityWellFocusLift;
                    TryProject(pointer, out fallback);
                    int returning = executor.TryReturnGravityCaptured(fallback);
                    if (returning > 0)
                    {
                        _gravityWellHeld = false;
                        StatusChanged?.Invoke(
                            $"MASS RETURN — {returning} bodies remain physical until terrain commit.");
                        return;
                    }
                }
                Vector2 viewport = inputAdapter.PointerViewport01;
                _gravityCircularSample = EarthCircularGestureSolver.Step(
                    ref _gravityCircularState, new float2(viewport.x, viewport.y));
                if (_gravityCircularSample.Recognized && executor.HasGravityStructureTarget)
                {
                    EarthGravityStructureIntent intent = _gravityCircularSample.Direction ==
                                                         EarthCircularGestureDirection.Clockwise
                        ? EarthGravityStructureIntent.Repair
                        : EarthGravityStructureIntent.Disassemble;
                    executor.SetGravityStructureGesture(intent, _gravityCircularSample.Phase01);
                    int phaseStep = Mathf.FloorToInt(_gravityCircularSample.Phase01 * 4f);
                    if (intent != _gravityReportedIntent || phaseStep != _gravityReportedPhaseStep)
                    {
                        _gravityReportedIntent = intent;
                        _gravityReportedPhaseStep = phaseStep;
                        StatusChanged?.Invoke(intent == EarthGravityStructureIntent.Repair
                            ? $"REWEAVE {Mathf.RoundToInt(_gravityCircularSample.Phase01 * 100f)}% - clockwise"
                            : $"UNWEAVE {Mathf.RoundToInt(_gravityCircularSample.Phase01 * 100f)}% - counter-clockwise");
                    }
                }
                else if (!executor.HasGravityStructureTarget)
                {
                    TryUpdateGravityWellAtScreenPoint(pointer);
                }
            }
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
            return executor.TryBeginGravityWell(hit.collider, focus, up, true);
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
            executor.UpdateGravityWell(
                point + (up * executor.GravityWellFocusLift),
                up,
                castCamera.transform.forward);
            return true;
        }

        public void EndGravityWell()
        {
            executor?.CancelGravityWell();
            _gravityWellHeld = false;
            _gravityThrowOwned = false;
            _gravityCircularSample = default;
            _gravityReportedIntent = EarthGravityStructureIntent.Neutral;
            _gravityReportedPhaseStep = -1;
        }

        private bool TryFindGravityFocus(float2 screenPoint, out RaycastHit selected)
        {
            selected = default;
            if (castCamera == null) return false;
            Ray ray = castCamera.ScreenPointToRay(new Vector2(screenPoint.x, screenPoint.y));
            if (!_targetQueryService.TryQuery(
                    ray,
                    projectionDistance,
                    0f,
                    planetCollider,
                    _casterBody,
                    EarthTargetCapabilities.Gravity | EarthTargetCapabilities.Repair |
                    EarthTargetCapabilities.Surface,
                    out EarthTargetQueryHit result)) return false;
            selected = result.Hit;
            return true;
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
            if (inputAdapter != null && Mathf.Abs(_scrollState.NormalizedDelta) > 0.0001f)
                _waveSector01 = Mathf.Clamp01(
                    _waveSector01 + (_scrollState.NormalizedDelta * 0.12f));
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
            UpdateV4GestureToken(pointer);
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
            _drawSurfaceLocked = false;
            _drawSurface = default;
            _pluckSource = null;
            _pluckPending = false;
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
            if (TryBeginStructurePluck(pointer)) return;
            _formingSourceValid = TryCaptureDrawSurface(pointer, out _formingSourceWorld);
            StatusChanged?.Invoke(origin == BendOriginMode.Self
                ? "SELF ORIGIN — hold still for mass, or sweep sideways for a wall."
                : "FORMING ROCK — hold still for mass, or drag sideways on ground for a wall.");
        }

        private void UpdateUnifiedEarthBend(float2 pointer)
        {
            float elapsed = Time.unscaledTime - _bendStartedAt;
            float2 normalizedDrag = NormalizePointerDelta(pointer - _bendStartPointer);
            if (_pluckPending)
            {
                if (_drawSurfaceLocked &&
                    math.length(normalizedDrag) >= Mathf.Max(extractionMotionThreshold, 0.022f))
                {
                    _pluckPending = false;
                    _pluckSource = null;
                    _earthAcquirePending = false;
                    _formingSourceValid = false;
                    _wallGesturePending = true;
                    _selectedAbility = EarthAbilityIds.LineWall;
                    _bendSession.SourceAcquired();
                    _bendSession.SetAmount(1f);
                    StatusChanged?.Invoke(
                        "STRUCTURE DRAW - gesture locked to the selected wall or platform face.");
                    UpdatePreview(pointer);
                    return;
                }
                if (elapsed < 0.24f) return;
                TryCommitStructurePluck(pointer);
                return;
            }
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
            float wheel = readWheel && inputAdapter != null ? _scrollState.NormalizedDelta * 120f : 0f;
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

        private bool TryBeginStructurePluck(float2 pointer)
        {
            if (castCamera == null || _targetQueryService == null) return false;
            Ray ray = castCamera.ScreenPointToRay(new Vector2(pointer.x, pointer.y));
            if (!_targetQueryService.TryQuery(
                    ray,
                    projectionDistance,
                    0.08f,
                    planetCollider,
                    _casterBody,
                    EarthTargetCapabilities.Pluck,
                    out EarthTargetQueryHit hit)) return false;
            IEarthPluckableStructure source = hit.Target.FractureSource as IEarthPluckableStructure;
            if (source == null) return false;
            _pluckSource = source;
            _pluckPoint = hit.Hit.point;
            _pluckPending = true;
            _earthAcquirePending = false;
            _formingSourceValid = TryCaptureDrawSurface(pointer, out _formingSourceWorld);
            StatusChanged?.Invoke(
                "Hold LMB still to tear one cell; drag to draw on this structure face.");
            return true;
        }

        private bool TryCommitStructurePluck(float2 pointer)
        {
            if (!_pluckPending || _pluckSource == null || executor == null) return false;
            if (!_pluckSource.TryPluckCell(_pluckPoint, out IEarthPhysicalTarget target) ||
                target == null || target.Body == null)
            {
                _bendSession.Cancel();
                _pluckPending = false;
                _pluckSource = null;
                return false;
            }
            Rigidbody body = target.Body;
            _holdDistance = Mathf.Clamp(
                Vector3.Distance(castCamera.transform.position, body.worldCenterOfMass),
                minimumHoldDistance,
                maximumHoldDistance);
            _previousBendTarget = body.worldCenterOfMass;
            _smoothedBendTargetVelocity = Vector3.zero;
            _lastBendPointer = pointer;
            bool acquired = executor.TryAcquireExistingEarthBody(
                body,
                _previousBendTarget,
                in _bendTuning,
                _tick++,
                target);
            if (acquired)
            {
                _bendSession.SourceAcquired();
                _bendSession.SetAmount(Mathf.Clamp(
                    Mathf.InverseLerp(3f, 300f, body.mass), 0.18f, 1f));
                _selectedAbility = EarthAbilityIds.PullRock;
                StatusChanged?.Invoke("Structural cell plucked - keep LMB held to aim it.");
            }
            else _bendSession.Cancel();
            _pluckPending = false;
            _pluckSource = null;
            return acquired;
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
            UpdateV4GestureToken(pointer);
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

            float2 tapTravel = NormalizePointerDelta(pointer - _bendStartPointer);
            if (_pluckPending)
            {
                if (elapsed >= 0.24f) TryCommitStructurePluck(pointer);
                else _bendSession.Cancel();
                _pluckPending = false;
                _pluckSource = null;
                _sampler.Cancel();
                _strokeSampler.Cancel();
                ClearPreview();
                return;
            }
            bool quickTap = _earthAcquirePending &&
                            elapsed <= EarthActionIntentResolver.DefaultTapSeconds &&
                            math.length(tapTravel) <= EarthActionIntentResolver.DefaultTapTravelViewport;
            if (quickTap && TryPrimeQuickStone())
            {
                _sampler.Cancel();
                _strokeSampler.Cancel();
                ClearPreview();
                PushChargeChanged?.Invoke(0f);
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
            else if (_selectedAbility == EarthAbilityIds.LineWall && _drawSurfaceLocked &&
                     IsConstructedDrawSurface(_drawSurface.Handle.Kind))
            {
                executed = executor.TryRaiseWallOnSurface(
                    _worldPath,
                    ToVector3(_drawSurface.Normal),
                    command.Intensity,
                    command.Modifiers != 0u
                        ? EarthTechniqueParameterCodec.UnpackSecondary(command.Modifiers)
                        : 0.5f,
                    command.Tick,
                    out _,
                    _drawSurface.Handle.StableId,
                    _drawSurface.Handle.Kind,
                    _drawSurface.Handle.Generation,
                    ToVector3(_drawSurface.Tangent));
            }
            else if (_selectedAbility == EarthAbilityIds.RaisePlatform && _drawSurfaceLocked &&
                     IsConstructedDrawSurface(_drawSurface.Handle.Kind))
            {
                executed = executor.TryRaisePlatformOnSurface(
                    _worldPath,
                    ToVector3(_drawSurface.Normal),
                    ToVector3(_drawSurface.Tangent),
                    command.Intensity,
                    command.Tick,
                    out _,
                    _drawSurface.Handle.StableId,
                    _drawSurface.Handle.Generation,
                    _drawSurface.Handle.Kind);
            }
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
            if (_drawSurfaceLocked)
            {
                point = default;
                EarthSurfaceHandle lockedHandle = _drawSurface.Handle;
                if (surfaceQueries != null && !surfaceQueries.IsCurrent(in lockedHandle)) return false;
                Vector3 normal = ToVector3(_drawSurface.Normal);
                float denominator = Vector3.Dot(ray.direction, normal);
                if (Mathf.Abs(denominator) < 0.0001f) return false;
                float travel = Vector3.Dot(ToVector3(_drawSurface.Point) - ray.origin, normal) / denominator;
                if (travel < 0f || travel > projectionDistance) return false;
                point = ray.GetPoint(travel);
                return true;
            }
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

        private bool TryCaptureDrawSurface(float2 screenPoint, out Vector3 point)
        {
            point = default;
            if (surfaceQueries != null && castCamera != null)
            {
                Ray ray = castCamera.ScreenPointToRay(new Vector2(screenPoint.x, screenPoint.y));
                var query = new EarthSurfaceQuery(
                    new float3(ray.origin.x, ray.origin.y, ray.origin.z),
                    new float3(ray.direction.x, ray.direction.y, ray.direction.z),
                    projectionDistance,
                    EarthSurfaceCapabilities.Draw);
                if (surfaceQueries.TrySample(in query, out EarthSurfaceSample sample))
                {
                    _drawSurface = sample;
                    _drawSurfaceLocked = true;
                    point = ToVector3(sample.Point);
                    return true;
                }
            }
            return TryProject(screenPoint, out point);
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);

        private Vector3 ArmorAimDirection()
        {
            if (castCamera == null) return _motor != null ? _motor.FacingForward : transform.forward;
            Vector2 pointer = inputAdapter != null ? inputAdapter.PointerPixels :
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return castCamera.ScreenPointToRay(pointer).direction;
        }

        private static bool IsConstructedDrawSurface(EarthSurfaceKind kind) =>
            kind == EarthSurfaceKind.WallSide || kind == EarthSurfaceKind.WallTop ||
            kind == EarthSurfaceKind.PlatformSide || kind == EarthSurfaceKind.Platform;

        public bool TryReleasePushAtScreenPoint(float2 screenPoint, float heldSeconds)
        {
            if (castCamera == null || executor == null || heldSeconds < 0f) return false;
            Ray ray = castCamera.ScreenPointToRay(new Vector2(screenPoint.x, screenPoint.y));
            if (!TryFindPushTarget(ray, out RaycastHit selected, out Rigidbody selectedBody)) return false;
            EarthWall selectedWall = selected.collider != null
                ? selected.collider.GetComponentInParent<EarthWall>()
                : null;
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
            selected = default;
            selectedBody = null;
            if (!_targetQueryService.TryQuery(
                    ray,
                    projectionDistance,
                    pushAssistRadius,
                    planetCollider,
                    _casterBody,
                    EarthTargetCapabilities.Push,
                    out EarthTargetQueryHit result)) return false;
            selected = result.Hit;
            selectedBody = result.Target.Body != null
                ? result.Target.Body
                : result.Hit.rigidbody;
            return selectedBody != null || result.Target.FractureSource != null;
        }

        private bool IsCasterCollider(Collider candidate)
        {
            if (candidate == null) return false;
            if (_casterBody == null) _casterBody = GetComponent<Rigidbody>();
            if (_motor == null) _motor = GetComponent<PlanetMotor>();
            if (_casterBody != null)
            {
                if (candidate.attachedRigidbody == _casterBody ||
                    candidate.transform == _casterBody.transform ||
                    candidate.transform.IsChildOf(_casterBody.transform)) return true;
                ActiveRagdollPuppet puppet = _casterBody.GetComponent<ActiveRagdollPuppet>();
                if (puppet != null && puppet.OwnsCollider(candidate)) return true;
            }
            return _motor != null && candidate.GetComponentInParent<PlanetMotor>() == _motor;
        }

        private static IEarthPhysicalTarget ResolveEarthTarget(Collider collider)
        {
            return EarthTargetResolver.ResolvePhysicalTarget(collider);
        }

        private EarthGestureTargetContext CaptureGestureTarget(float2 screenPoint)
        {
            if (castCamera == null) return default;
            Ray ray = castCamera.ScreenPointToRay(new Vector2(screenPoint.x, screenPoint.y));
            if (!_targetQueryService.TryQuery(
                    ray, projectionDistance, pushAssistRadius, planetCollider, _casterBody,
                    EarthTargetCapabilities.None, out EarthTargetQueryHit hit)) return default;
            IEarthPhysicalTarget physical = hit.Target.PhysicalTarget;
            EarthPhysicalTargetHandle handle = physical != null ? physical.TargetHandle : default;
            uint stableId = handle.IsValid
                ? handle.StableId
                : hit.Target.FractureSource != null ? hit.Target.FractureSource.StructureId : 0u;
            uint generation = handle.IsValid ? handle.Generation : stableId != 0u ? 1u : 0u;
            return new EarthGestureTargetContext(
                stableId, generation, (ushort)hit.Target.Capabilities);
        }

        private void UpdateV4GestureToken(float2 pointer)
        {
            EarthGestureSettings settings = gestureProfile != null
                ? gestureProfile.Settings
                : EarthGestureSettings.Default;
            EarthGestureTargetContext commitTarget = CaptureGestureTarget(pointer);
            _lastGestureToken = _gestureTokenizer.Tokenize(
                _strokeSampler.Samples,
                Time.unscaledTime,
                in settings,
                in _gesturePointerDownTarget,
                in commitTarget);
            if (_lastGestureToken.IsValid && castCamera != null)
            {
                float2 screenDirection = _lastGestureToken.Features.Direction;
                Vector3 projected = castCamera.transform.right * screenDirection.x +
                                    castCamera.transform.up * screenDirection.y;
                Vector3 localUp = _motor != null && _motor.LocalUp.sqrMagnitude > 0.5f
                    ? _motor.LocalUp.normalized
                    : transform.up;
                projected = Vector3.ProjectOnPlane(projected, localUp);
                if (projected.sqrMagnitude > 0.0001f)
                    _lastGestureToken = _lastGestureToken.WithWorldProjectedDirection(
                        new float3(projected.x, projected.y, projected.z));
            }
            EarthGestureTargetContext contextTarget = commitTarget.IsValid
                ? commitTarget
                : _gesturePointerDownTarget;
            uint activeMatter = executor != null && executor.HeldFragment != null
                ? executor.HeldFragment.StableEarthId
                : 0u;
            var context = new EarthIntentContext(
                contextTarget.Capabilities,
                _motor != null && _motor.IsGrounded,
                castCamera != null,
                activeMatter != 0u,
                activeMatter);
            _rankedIntentCount = EarthRankedIntentResolver.ResolveNonAlloc(
                in _lastGestureToken, in context, _rankedIntentCandidates);
        }

        private bool HandleMatterReturnScroll(float2 pointer)
        {
            if (executor == null || Mathf.Abs(_scrollState.NormalizedDelta) < 0.0001f) return false;
            EarthGestureTargetContext targetContext = CaptureGestureTarget(pointer);
            _lastGestureToken = EarthGestureTokenizer.FromScroll(in _scrollState, in targetContext);
            if (_scrollState.DirectionReversal &&
                executor.MatterReturnController != null && executor.MatterReturnController.IsReturning)
            {
                bool reversed = executor.ReverseMatterReturnBeforeCommit();
                if (reversed) StatusChanged?.Invoke("RETURN REVERSED - earth remains physical and controllable.");
                return reversed;
            }
            if (!_scrollState.FastFlick || _scrollState.NormalizedDelta >= 0f ||
                executor.HeldFragment == null) return false;
            Vector3 fallback = executor.HeldFragment.transform.position;
            TryProject(pointer, out fallback);
            if (!executor.TryReturnMatter(executor.HeldFragment, fallback)) return false;
            _bendSession?.Cancel();
            _earthAcquirePending = false;
            _formingSourceValid = false;
            _sampler.Cancel();
            _strokeSampler.Cancel();
            ClearPreview();
            StatusChanged?.Invoke("STONE RETURN - physical body retained until terrain collider commit.");
            return true;
        }

        public static float PushCharge(float heldSeconds) =>
            Mathf.Clamp01(Mathf.Lerp(0.18f, 1f, 1f - Mathf.Exp(-Mathf.Max(0f, heldSeconds) / 0.72f)));

    }
}
