using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Combat;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DefaultExecutionOrder(1550)]
    [DisallowMultipleComponent]
    public sealed class EarthDualMouseAbilityController : MonoBehaviour
    {
        private static readonly ProfilerMarker StompMarker =
            new ProfilerMarker("Elemental.DualMouse.StompStone");
        private static readonly ProfilerMarker CrestMarker =
            new ProfilerMarker("Elemental.DualMouse.PillarCrest");

        [SerializeField] private MagicExecutor executor;
        [SerializeField] private EarthPillarWavePool wavePool;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private UnityEngine.Camera castCamera;
        [SerializeField] private Rigidbody casterBody;

        private EarthFragment _punchStone;
        private Vector3 _punchStart;
        private Vector3 _punchContact;
        private Vector3 _punchUp;
        private Vector2 _punchAimScreen;
        private Vector3 _punchAimPoint;
        private float _punchStartedAt;
        private bool _punchPoseStarted;
        private float _castPresentationUntil;
        private int _castPresentationKind;
        private Animator _animator;
        private int _magicLayerIndex = -1;
        private bool _liveAimEnabled;
        private readonly RaycastHit[] _aimHits = new RaycastHit[12];
        private LineRenderer _aimPreview;

        public bool IsStompStoneActive => _punchStone != null && _punchStone.gameObject.activeInHierarchy;

        public void Configure(
            MagicExecutor configuredExecutor,
            EarthPillarWavePool configuredWavePool,
            PlanetMotor configuredMotor,
            UnityEngine.Camera configuredCamera,
            Rigidbody configuredCasterBody)
        {
            executor = configuredExecutor;
            wavePool = configuredWavePool;
            motor = configuredMotor;
            castCamera = configuredCamera;
            casterBody = configuredCasterBody;
            Resolve();
        }

        public bool CastStompStone() => BeginStompStone(
            castCamera != null
                ? new Vector2(castCamera.pixelWidth * 0.5f, castCamera.pixelHeight * 0.5f)
                : Vector2.zero,
            false);

        public bool CastStompStone(Vector2 initialAim) => BeginStompStone(initialAim, true);

        private bool BeginStompStone(Vector2 initialAim, bool liveAim)
        {
            using (StompMarker.Auto())
            {
                Resolve();
                if (executor == null || executor.FragmentPool == null || casterBody == null ||
                    IsStompStoneActive) return false;
                Vector3 up = LocalUp;
                Vector3 forward = AimForward(up);
                if (!TryFindSupport(
                        casterBody.worldCenterOfMass + forward * 1.2f + up * 3f,
                        -up,
                        8f,
                        out Vector3 surface,
                        out Vector3 surfaceUp))
                    surface = ProjectToPlanet(casterBody.worldCenterOfMass + forward * 1.2f, out surfaceUp);

                const float radius = 0.26f;
                float mass = Mathf.Clamp(
                    (4f / 3f) * Mathf.PI * radius * radius * radius * executor.EarthMaterialDensity,
                    24f,
                    95f);
                _punchStone = executor.FragmentPool.Acquire(
                    executor,
                    surface - surfaceUp * 0.34f,
                    radius,
                    mass);
                if (_punchStone == null) return false;
                _punchStart = surface - surfaceUp * 0.34f;
                _punchAimScreen = initialAim;
                _liveAimEnabled = liveAim;
                _punchContact = ResolvePunchSocket();
                _punchAimPoint = ResolveAimPoint(_punchAimScreen, _punchContact);
                _punchUp = surfaceUp;
                _punchStartedAt = Time.fixedTime;
                _punchPoseStarted = false;
                _castPresentationUntil = Time.time + EarthStompStoneSequenceSolver.RiseSeconds +
                                         EarthStompStoneSequenceSolver.HoverSeconds +
                                         EarthStompStoneSequenceSolver.RecoverySeconds;
                _punchStone.BeginBendControl(
                    _punchStart,
                    Vector3.zero,
                    1f,
                    BendTuning.Default);
                SetCastPresentation(true, 8);
                return true;
            }
        }

        public void UpdateStompAim(Vector2 currentAim)
        {
            if (!IsStompStoneActive || !_liveAimEnabled) return;
            _punchAimScreen = currentAim;
            _punchAimPoint = ResolveAimPoint(currentAim, _punchStone.transform.position);
        }

        public void CancelStompStone()
        {
            if (_punchStone != null)
            {
                _punchStone.CompleteReintegration();
                _punchStone = null;
            }
            _liveAimEnabled = false;
            SetAimPreview(false);
            _castPresentationUntil = 0f;
            SetCastPresentation(false, 0);
        }

        public bool CastPillarCrest(Vector2 screenPoint, int requestedCount)
        {
            Vector2 viewport = castCamera != null
                ? new Vector2(
                    screenPoint.x / Mathf.Max(1f, castCamera.pixelWidth),
                    screenPoint.y / Mathf.Max(1f, castCamera.pixelHeight))
                : new Vector2(0.5f, 0.5f);
            return CastPillarCrest(viewport, viewport + Vector2.up * 0.12f, requestedCount);
        }

        public bool CastPillarCrest(Vector2 startViewport, Vector2 endViewport, int requestedCount)
        {
            using (CrestMarker.Auto())
            {
                Resolve();
                if (wavePool == null || casterBody == null) return false;
                Vector3 up = LocalUp;
                Vector3 forward = AimForward(up);
                Vector3 surfaceStart;
                Vector3 startUp;
                if (!TrySampleViewportSurface(startViewport, out surfaceStart, out startUp))
                    surfaceStart = ProjectToPlanet(casterBody.worldCenterOfMass + forward * 2.2f, out startUp);
                Vector3 tangent = Vector3.ProjectOnPlane(surfaceStart - casterBody.worldCenterOfMass, up);
                float distance = Mathf.Clamp(tangent.magnitude, 1.2f, 5.5f);
                if (tangent.sqrMagnitude < 0.1f) tangent = forward;
                surfaceStart = ProjectToPlanet(
                    casterBody.worldCenterOfMass + tangent.normalized * distance,
                    out startUp);
                Vector3 surfaceEnd;
                if (!TrySampleViewportSurface(endViewport, out surfaceEnd, out _))
                {
                    Vector2 pointerDirection = endViewport - startViewport;
                    Vector3 lateral = castCamera != null
                        ? castCamera.transform.right * pointerDirection.x + castCamera.transform.up * pointerDirection.y
                        : forward;
                    lateral = Vector3.ProjectOnPlane(lateral, startUp).normalized;
                    if (lateral.sqrMagnitude < 0.1f) lateral = forward;
                    surfaceEnd = ProjectToPlanet(surfaceStart + lateral * 3f, out _);
                }
                int count = requestedCount <= 1 ? 1 : requestedCount <= 3 ? 3 : requestedCount <= 5 ? 5 : 7;
                var crestPath = new EarthCrestPath(
                    new float3(surfaceStart.x, surfaceStart.y, surfaceStart.z),
                    new float3(surfaceEnd.x, surfaceEnd.y, surfaceEnd.z));
                int launched = wavePool.LaunchCrest(
                    in crestPath,
                    count,
                    casterBody);
                if (launched <= 0) return false;
                _castPresentationUntil = Time.time + 0.36f;
                SetCastPresentation(true, 6);
                return true;
            }
        }

        private void FixedUpdate()
        {
            if (_punchStone == null || !_punchStone.gameObject.activeInHierarchy) return;
            float elapsed = Time.fixedTime - _punchStartedAt;
            EarthStompStoneSequenceSample sequence =
                EarthStompStoneSequenceSolver.Evaluate(elapsed);
            float t = sequence.Rise01;
            float eased = t * t * (3f - 2f * t);
            _punchContact = ResolvePunchSocket();
            Vector3 target = Vector3.Lerp(_punchStart, _punchContact, eased);
            Vector3 riseVelocity = t < 1f
                ? (_punchContact - _punchStart) / EarthStompStoneSequenceSolver.RiseSeconds
                : Vector3.zero;
            _punchStone.UpdateBendTarget(target, riseVelocity, 1f);
            if (sequence.Phase == EarthStompStonePhase.Rising)
            {
                SetAimPreview(false);
                return;
            }

            if (!_punchPoseStarted)
            {
                _punchPoseStarted = true;
                SetCastPresentation(true, 3);
            }
            if (sequence.Phase == EarthStompStonePhase.Hovering)
            {
                _punchAimPoint = _liveAimEnabled
                    ? ResolveAimPoint(_punchAimScreen, _punchStone.transform.position)
                    : _punchStone.transform.position +
                      (castCamera != null ? castCamera.transform.forward : AimForward(_punchUp)) * 80f;
                SetAimPreview(true);
                return;
            }

            Vector3 aim;
            if (_liveAimEnabled)
            {
                _punchAimPoint = ResolveAimPoint(_punchAimScreen, _punchStone.transform.position);
                aim = _punchAimPoint - _punchStone.transform.position;
            }
            else
            {
                aim = castCamera != null ? castCamera.transform.forward : AimForward(_punchUp);
                _punchAimPoint = _punchStone.transform.position + aim * 80f;
            }
            Vector3 launch = aim.sqrMagnitude > 0.0001f ? aim.normalized : AimForward(_punchUp);
            SetAimPreview(false);
            _punchStone.StopBendControl();
            EarthTypedCombatProjectile typed = _punchStone.GetComponent<EarthTypedCombatProjectile>();
            if (typed != null)
                typed.Arm(_punchStone, EarthCharacterImpactSourceKind.StonePunch);
            else
                Debug.LogError("[Elemental] Pooled punch stone is missing its prewarmed typed projectile.", _punchStone);
            Collider casterCollider = casterBody.GetComponent<Collider>();
            _punchStone.LaunchProjectile(launch, 26f, casterCollider, 0.34f);
            _punchStone = null;
            _liveAimEnabled = false;
        }

        private void Update()
        {
            if (IsStompStoneActive && _aimPreview != null && _aimPreview.enabled)
            {
                _punchAimPoint = ResolveAimPoint(_punchAimScreen, _punchStone.transform.position);
                UpdateAimPreview();
            }
            if (_animator == null) return;
            bool active = Time.time < _castPresentationUntil;
            SetCastPresentation(active, _castPresentationKind);
        }

        private bool TrySampleViewportSurface(Vector2 viewport, out Vector3 point, out Vector3 normal)
        {
            Vector3 up = LocalUp;
            Vector3 forward = AimForward(up);
            Ray ray = castCamera != null
                ? castCamera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f))
                : new Ray(casterBody.worldCenterOfMass + up * 2f, forward - up * 0.2f);
            return TryFindSupport(ray.origin, ray.direction, 200f, out point, out normal);
        }

        private Vector3 ResolvePunchSocket()
        {
            if (casterBody == null) return _punchContact;
            Vector3 up = LocalUp;
            Vector3 aimForward = AimForward(up);
            Vector3 velocityLead = Vector3.ClampMagnitude(casterBody.linearVelocity * 0.055f, 0.45f);
            return casterBody.worldCenterOfMass + up * 0.62f + aimForward * 0.66f + velocityLead;
        }

        private Vector3 ResolveAimPoint(Vector2 screenPoint, Vector3 origin)
        {
            Vector3 fallback = origin + AimForward(LocalUp) * 80f;
            if (castCamera == null) return fallback;
            Ray ray = castCamera.ScreenPointToRay(screenPoint);
            int count = UnityEngine.Physics.RaycastNonAlloc(
                ray,
                _aimHits,
                200f,
                ~0,
                QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;
            Vector3 best = ray.GetPoint(80f);
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = _aimHits[index];
                if (hit.collider == null || hit.distance >= bestDistance) continue;
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                if (_punchStone != null && hitTransform.IsChildOf(_punchStone.transform)) continue;
                bestDistance = hit.distance;
                best = hit.point;
            }
            return best;
        }

        private void SetAimPreview(bool visible)
        {
            if (_aimPreview == null) return;
            _aimPreview.enabled = visible && IsStompStoneActive;
            if (_aimPreview.enabled) UpdateAimPreview();
        }

        private void UpdateAimPreview()
        {
            if (_aimPreview == null || _punchStone == null) return;
            Vector3 origin = _punchStone.transform.position;
            Vector3 endpoint = origin + Vector3.ClampMagnitude(_punchAimPoint - origin, 12f);
            _aimPreview.SetPosition(0, origin);
            _aimPreview.SetPosition(1, endpoint);
        }

        private bool TryFindSupport(
            Vector3 origin,
            Vector3 direction,
            float distance,
            out Vector3 point,
            out Vector3 normal)
        {
            point = default;
            normal = LocalUp;
            EarthSurfaceQueryService surfaces = FindAnyObjectByType<EarthSurfaceQueryService>(
                FindObjectsInactive.Exclude);
            if (surfaces == null) return false;
            var query = new EarthSurfaceQuery(
                new float3(origin.x, origin.y, origin.z),
                new float3(direction.x, direction.y, direction.z),
                distance,
                EarthSurfaceCapabilities.Support | EarthSurfaceCapabilities.Pillar);
            if (!surfaces.TrySample(in query, out EarthSurfaceSample sample)) return false;
            point = new Vector3(sample.Point.x, sample.Point.y, sample.Point.z);
            normal = new Vector3(sample.Normal.x, sample.Normal.y, sample.Normal.z);
            return true;
        }

        private Vector3 ProjectToPlanet(Vector3 candidate, out Vector3 up)
        {
            Vector3 center = executor != null && executor.VoxelPlanet != null
                ? executor.VoxelPlanet.transform.position
                : Vector3.zero;
            float radius = executor != null && executor.VoxelPlanet != null
                ? executor.VoxelPlanet.Radius
                : Mathf.Max(1f, Vector3.Distance(casterBody.position, center));
            up = (candidate - center).normalized;
            if (up.sqrMagnitude < 0.5f) up = LocalUp;
            return center + up * radius;
        }

        private Vector3 AimForward(Vector3 up)
        {
            Vector3 value = castCamera != null ? castCamera.transform.forward : transform.forward;
            value = Vector3.ProjectOnPlane(value, up).normalized;
            if (value.sqrMagnitude < 0.5f && motor != null) value = motor.FacingForward;
            return value.sqrMagnitude > 0.5f ? value.normalized : transform.forward;
        }

        private Vector3 LocalUp => motor != null && motor.LocalUp.sqrMagnitude > 0.5f
            ? motor.LocalUp.normalized
            : transform.position.sqrMagnitude > 0.1f
                ? transform.position.normalized
                : transform.up;

        private void SetCastPresentation(bool active, int kind)
        {
            if (_animator == null) return;
            if (active) _castPresentationKind = kind;
            int appliedKind = active ? kind : 0;
            _animator.SetInteger("CastKind", appliedKind);
            _animator.SetFloat("EarthPose", appliedKind);
            _animator.SetBool("Cast", active);
            if (_magicLayerIndex >= 0)
                _animator.SetLayerWeight(_magicLayerIndex, active ? 1f : 0f);
        }

        private void Resolve()
        {
            if (executor == null) executor = GetComponent<MagicExecutor>();
            if (wavePool == null) wavePool = FindAnyObjectByType<EarthPillarWavePool>(FindObjectsInactive.Include);
            if (motor == null) motor = GetComponent<PlanetMotor>();
            if (casterBody == null) casterBody = GetComponent<Rigidbody>();
            if (castCamera == null) castCamera = UnityEngine.Camera.main;
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
                _magicLayerIndex = _animator != null
                    ? _animator.GetLayerIndex("Earth Magic Upper Body")
                    : -1;
            }
            if (_aimPreview == null)
            {
                _aimPreview = GetComponent<LineRenderer>();
                if (_aimPreview == null) _aimPreview = gameObject.AddComponent<LineRenderer>();
                _aimPreview.useWorldSpace = true;
                _aimPreview.positionCount = 2;
                _aimPreview.widthMultiplier = 0.018f;
                _aimPreview.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _aimPreview.receiveShadows = false;
                _aimPreview.enabled = false;
            }
            if (_aimPreview.sharedMaterial == null && executor != null && executor.FragmentPool != null)
                _aimPreview.sharedMaterial = executor.FragmentPool.SharedMaterial;
        }

        private void Awake() => Resolve();
        private void OnDisable() => CancelStompStone();
    }
}
