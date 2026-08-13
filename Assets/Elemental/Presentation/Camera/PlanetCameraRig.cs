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

        public Vector3 LocalUp => _smoothedUp;
        public Vector3 TangentForward => _orbitForward.sqrMagnitude > 0.5f ? _orbitForward : transform.forward;
        public Vector3 SmoothedFocus => _smoothedFocus;
        public float LastAppliedImpulseMagnitude { get; private set; }
        public float PeakRequestedImpulseAmplitude { get; private set; }

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
                Vector3 desiredFocus = ToVector3(framing.Focus);
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
                    transform.rotation = Quaternion.LookRotation(lookDirection.normalized, _smoothedUp);
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
            Vector3 offset = ((transform.right * Mathf.Sin(phase)) +
                              (_smoothedUp * Mathf.Sin((phase * 1.73f) + 1.1f) * 0.58f)) *
                             (_impulseAmplitude * envelope);
            _lastImpulseOffset = offset;
            LastAppliedImpulseMagnitude = offset.magnitude;
            transform.position += offset;
            transform.rotation *= Quaternion.Euler(
                Mathf.Sin(phase * 1.31f) * envelope * _impulseAmplitude * 8f,
                0f,
                Mathf.Sin(phase * 0.91f) * envelope * _impulseAmplitude * 10f);
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

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _occlusionHits[index];
                if (hit.collider == null || hit.transform == target || hit.rigidbody == targetBody ||
                    (_targetPuppet != null && _targetPuppet.OwnsCollider(hit.collider)))
                {
                    continue;
                }

                nearest = Mathf.Min(nearest, hit.distance);
            }

            return anchor + (direction * Mathf.Max(0.05f, nearest - occlusionRadius));
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
