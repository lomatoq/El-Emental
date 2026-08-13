using Elemental.Runtime.Physics;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Gravity;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Camera
{
    [DisallowMultipleComponent]
    public class PlanetCameraRig : MonoBehaviour
    {
        private static readonly ProfilerMarker LateTickMarker = new ProfilerMarker("Elemental.PlanetCamera.LateTick");
        private const int OcclusionHitCapacity = 8;

        [SerializeField] private Transform target;
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private GravityWorldBehaviour gravityWorld;
        [SerializeField, Min(0.1f)] private float distance = 8f;
        [SerializeField, Min(0f)] private float height = 2f;
        [SerializeField, Min(0f)] private float focusHeight = 1.25f;
        [SerializeField, Min(0f)] private float lookAheadDistance = 4.5f;
        [SerializeField, Min(0f)] private float speedLookAheadDistance = 2.4f;
        [SerializeField, Min(0.1f)] private float lookAheadReferenceSpeed = 6f;
        [SerializeField] private float shoulderOffset = 1.15f;
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.08f;
        [SerializeField, Min(0.01f)] private float focusSmoothTime = 0.1f;
        [SerializeField, Min(0.01f)] private float upSmoothing = 10f;
        [SerializeField, Min(0f)] private float occlusionRadius = 0.25f;
        [SerializeField] private LayerMask occlusionMask = ~0;
        [SerializeField, Min(0.1f)] private float headingFollowSpeed = 10f;

        private readonly RaycastHit[] _occlusionHits = new RaycastHit[OcclusionHitCapacity];
        private Vector3 _smoothedUp = Vector3.up;
        private Vector3 _positionVelocity;
        private Vector3 _smoothedFocus;
        private Vector3 _focusVelocity;
        private bool _hasSmoothedFocus;
        private Vector3 _lastImpulseOffset;
        private float _impulseAmplitude;
        private float _impulseDuration;
        private float _impulseElapsed;
        private uint _impulseSeed;
        private uint _tick;
        private Vector3 _orbitForward;
        private ActiveRagdollPuppet _targetPuppet;
        private UnityEngine.Camera _camera;
        private bool _directorActive;
        private Vector3 _directorFocus;
        private float _rotationDamping = 0.09f;
        private float _impulseGain = 1f;
        private float _maximumRoll = 3f;
        private float _motionScale = 1f;
        private float _occlusionPullInSpeed = 24f;
        private float _occlusionReleaseSpeed = 4.5f;
        private float _occlusionReleaseDelay = 0.12f;
        private EarthCameraOcclusionState _occlusionState;
        private Transform _ignoredOccluderRoot;

        public Vector3 LocalUp => _smoothedUp;
        public Vector3 TangentForward => _orbitForward.sqrMagnitude > 0.5f ? _orbitForward : transform.forward;
        public Vector3 SmoothedFocus => _smoothedFocus;
        public float LastAppliedImpulseMagnitude { get; private set; }
        public float PeakRequestedImpulseAmplitude { get; private set; }

        public void SetDirectorFrame(
            float configuredDistance,
            float configuredHeight,
            float configuredShoulderOffset,
            float configuredFieldOfView,
            Vector3 configuredFocus,
            float configuredPositionDamping,
            float configuredRotationDamping,
            float configuredOcclusionRadius,
            float configuredImpulseGain,
            float configuredMaximumRoll,
            float motionScale,
            float pullInSpeed,
            float releaseSpeed,
            float releaseDelay,
            Transform ignoredOccluderRoot = null)
        {
            _directorActive = true;
            distance = Mathf.Max(0.1f, configuredDistance);
            height = Mathf.Max(0f, configuredHeight);
            shoulderOffset = configuredShoulderOffset;
            _directorFocus = configuredFocus;
            positionSmoothTime = Mathf.Max(0.01f, configuredPositionDamping);
            _rotationDamping = Mathf.Max(0.01f, configuredRotationDamping);
            occlusionRadius = Mathf.Max(0f, configuredOcclusionRadius);
            _impulseGain = Mathf.Max(0f, configuredImpulseGain);
            _maximumRoll = Mathf.Max(0f, configuredMaximumRoll);
            _motionScale = Mathf.Clamp01(motionScale);
            _occlusionPullInSpeed = Mathf.Max(0.1f, pullInSpeed);
            _occlusionReleaseSpeed = Mathf.Max(0.1f, releaseSpeed);
            _occlusionReleaseDelay = Mathf.Max(0f, releaseDelay);
            _ignoredOccluderRoot = ignoredOccluderRoot;
            if (_camera == null) _camera = GetComponent<UnityEngine.Camera>();
            if (_camera != null) _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, configuredFieldOfView,
                1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        }

        public void AddPresentationImpulse(float amplitudeMeters, float durationSeconds, uint seed)
        {
            float amplitude = Mathf.Clamp(amplitudeMeters, 0f, 0.28f);
            float duration = Mathf.Clamp(durationSeconds, 0.05f, 1.2f);
            if (amplitude < _impulseAmplitude * 0.35f) return;
            PeakRequestedImpulseAmplitude = Mathf.Max(PeakRequestedImpulseAmplitude, amplitude);
            _impulseAmplitude = Mathf.Max(_impulseAmplitude, amplitude);
            _impulseDuration = Mathf.Max(_impulseDuration - _impulseElapsed, duration);
            _impulseElapsed = 0f;
            _impulseSeed = seed;
        }

        public void ConfigureFraming(
            float configuredDistance,
            float configuredHeight,
            float configuredFocusHeight = 1.25f,
            float configuredLookAheadDistance = 4.5f,
            float configuredShoulderOffset = 1.15f)
        {
            distance = Mathf.Max(0.1f, configuredDistance);
            height = Mathf.Max(0f, configuredHeight);
            focusHeight = Mathf.Max(0f, configuredFocusHeight);
            lookAheadDistance = Mathf.Max(0f, configuredLookAheadDistance);
            shoulderOffset = configuredShoulderOffset;
        }

        public void ConfigureFeel(float configuredPositionSmoothTime, float configuredUpSmoothing)
        {
            positionSmoothTime = Mathf.Max(0.01f, configuredPositionSmoothTime);
            upSmoothing = Mathf.Max(0.01f, configuredUpSmoothing);
        }

        public void Configure(
            Transform configuredTarget,
            Rigidbody configuredBody,
            GravityWorldBehaviour configuredWorld)
        {
            target = configuredTarget;
            targetBody = configuredBody;
            gravityWorld = configuredWorld;
            _targetPuppet = configuredTarget != null
                ? configuredTarget.GetComponent<ActiveRagdollPuppet>()
                : null;
            _orbitForward = Vector3.zero;
            _hasSmoothedFocus = false;
        }

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            _targetPuppet = target != null
                ? target.GetComponent<ActiveRagdollPuppet>()
                : null;
        }

        private void LateUpdate()
        {
            transform.position -= _lastImpulseOffset;
            _lastImpulseOffset = Vector3.zero;
            LastAppliedImpulseMagnitude = 0f;
            if (target == null || gravityWorld == null || !gravityWorld.IsReady)
            {
                return;
            }

            using (LateTickMarker.Auto())
            {
                GravitySample sample = gravityWorld.World.Sample(
                    new float3(target.position.x, target.position.y, target.position.z),
                    _tick++);
                Vector3 sampledUp = new Vector3(sample.Up.x, sample.Up.y, sample.Up.z).normalized;
                if (!IsFinite(sampledUp) || sampledUp.sqrMagnitude < 0.5f)
                {
                    return;
                }

                float upBlend = 1f - Mathf.Exp(-upSmoothing * Time.deltaTime);
                _smoothedUp = Vector3.Slerp(_smoothedUp, sampledUp, upBlend).normalized;
                if (_orbitForward.sqrMagnitude < 0.5f)
                {
                    Vector3 initial = Vector3.ProjectOnPlane(target.position - transform.position, _smoothedUp);
                    _orbitForward = initial.sqrMagnitude > 0.001f ? initial.normalized : target.forward;
                }
                Vector3 desiredHeading = ToVector3(PlanetFacingSolver.SolveTangentForward(
                    ToFloat3(_smoothedUp),
                    ToFloat3(target.forward),
                    ToFloat3(_orbitForward)));
                float headingBlend = 1f - Mathf.Exp(-headingFollowSpeed * Time.deltaTime);
                _orbitForward = Vector3.Slerp(_orbitForward, desiredHeading, headingBlend).normalized;
                Vector3 targetVelocity = targetBody != null ? targetBody.linearVelocity : Vector3.zero;
                PlanetCameraFramingResult framing = PlanetCameraFramingSolver.Solve(
                    ToFloat3(target.position),
                    ToFloat3(_smoothedUp),
                    ToFloat3(_orbitForward),
                    ToFloat3(targetVelocity),
                    distance,
                    height,
                    focusHeight,
                    lookAheadDistance,
                    speedLookAheadDistance,
                    lookAheadReferenceSpeed,
                    shoulderOffset);
                Vector3 desiredFocus = _directorActive ? _directorFocus : ToVector3(framing.Focus);
                Vector3 desiredPosition = ResolveOcclusion(
                    ToVector3(framing.OcclusionAnchor),
                    ToVector3(framing.Position));
                if (!_hasSmoothedFocus)
                {
                    _smoothedFocus = desiredFocus;
                    _hasSmoothedFocus = true;
                }
                _smoothedFocus = Vector3.SmoothDamp(
                    _smoothedFocus,
                    desiredFocus,
                    ref _focusVelocity,
                    focusSmoothTime);

                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    desiredPosition,
                    ref _positionVelocity,
                    positionSmoothTime);
                Vector3 lookDirection = _smoothedFocus - transform.position;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, _smoothedUp);
                    float rotationBlend = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, _rotationDamping));
                    transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
                }
                ApplyPresentationImpulse();
            }
        }

        private void OnDisable()
        {
            transform.position -= _lastImpulseOffset;
            _lastImpulseOffset = Vector3.zero;
            LastAppliedImpulseMagnitude = 0f;
        }

        private void ApplyPresentationImpulse()
        {
            if (_impulseElapsed >= _impulseDuration || _impulseAmplitude <= 0f) return;
            _impulseElapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(_impulseElapsed / Mathf.Max(_impulseDuration, 0.001f));
            float envelope = (1f - normalized) * (1f - normalized);
            float phase = (Time.unscaledTime * 39f) + ((_impulseSeed & 1023u) * 0.017f);
            float amplitude = _impulseAmplitude * _impulseGain * _motionScale;
            float high = Mathf.Sin(phase * 2.31f) * 0.24f;
            float medium = Mathf.Sin((phase * 1.17f) + 0.9f) * 0.48f;
            float low = Mathf.Sin((phase * 0.43f) + 2.1f) * 0.72f;
            Vector3 offset = ((transform.right * (high + medium)) +
                              (_smoothedUp * (medium * 0.38f + low * 0.62f))) *
                             (amplitude * envelope);
            _lastImpulseOffset = offset;
            LastAppliedImpulseMagnitude = offset.magnitude;
            transform.position += offset;
            transform.rotation *= Quaternion.Euler(
                Mathf.Sin(phase * 1.31f) * envelope * amplitude * 8f,
                0f,
                Mathf.Clamp(Mathf.Sin(phase * 0.91f) * envelope * amplitude * 10f,
                    -_maximumRoll, _maximumRoll));
        }

        protected virtual Vector3 ResolveOcclusion(Vector3 anchor, Vector3 desiredPosition)
        {
            Vector3 offset = desiredPosition - anchor;
            float castDistance = offset.magnitude;
            if (castDistance <= 0.001f || occlusionRadius <= 0f)
            {
                return desiredPosition;
            }

            Vector3 direction = offset / castDistance;
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                anchor,
                occlusionRadius,
                direction,
                _occlusionHits,
                castDistance,
                occlusionMask,
                QueryTriggerInteraction.Ignore);
            float nearest = castDistance;
            bool hasHit = false;

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _occlusionHits[index];
                if (hit.collider == null || hit.transform == target || hit.rigidbody == targetBody ||
                    (_targetPuppet != null && _targetPuppet.OwnsCollider(hit.collider)) ||
                    (_ignoredOccluderRoot != null && hit.transform.IsChildOf(_ignoredOccluderRoot)))
                {
                    continue;
                }

                nearest = Mathf.Min(nearest, hit.distance);
                hasHit = true;
            }
            float hitDistance = Mathf.Max(0.05f, nearest - occlusionRadius);
            _occlusionState = EarthCameraOcclusionSolver.Step(
                in _occlusionState,
                castDistance,
                hitDistance,
                hasHit,
                Time.unscaledDeltaTime,
                _occlusionPullInSpeed,
                _occlusionReleaseSpeed,
                _occlusionReleaseDelay);
            return anchor + direction * _occlusionState.Distance;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
