using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class EarthFragment : MonoBehaviour, IEarthPhysicalTarget
    {
        private static readonly ProfilerMarker BendControlMarker =
            new ProfilerMarker("Elemental.Bending.EarthFragmentControl");

        [SerializeField] private Rigidbody targetBody;
        [SerializeField, Range(0.5f, 1f)] private float visualDiameterFactor = 0.86f;
        [SerializeField] private EarthHoverProfile hoverProfile;

        private MagicExecutor _executor;
        private EarthFragmentPool _sourcePool;
        private EarthRockProfile _profile;
        private GravityBody _gravityBody;
        private Collider _bodyCollider;
        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;
        private Collider _ignoredSourceCollider;
        private Collider _ignoredControllerCollider;
        private Transform _holdTarget;
        private BendTuning _tuning = BendTuning.Default;
        private Vector3 _bendTargetPosition;
        private Vector3 _bendTargetVelocity;
        private Vector3 _previousTargetPosition;
        private float _charge01;
        private bool _isControlled;
        private Vector3 _emergenceSurface;
        private Vector3 _emergenceUp;
        private float _emergenceClearance;
        private float _emergenceCollisionRestoreAt;
        private int _lastImpactFrame = -100;
        private float _radius;
        private float _nextAccretionAt;
        private EarthHoverFrame _hoverFrame;
        private uint _generation;

        public uint FragmentId { get; private set; }
        public Rigidbody Body => targetBody;
        public float Mass => targetBody != null ? targetBody.mass : 0f;
        public float Radius => _radius;
        public EarthRockProfile Profile => _profile;
        public bool IsHeld => targetBody != null && _isControlled && gameObject.activeSelf;
        public Vector3 BendTargetPosition => _bendTargetPosition;
        public Vector3 BendTargetVelocity => _bendTargetVelocity;
        public Vector3 LastControlError { get; private set; }
        public Vector3 LastAppliedControlForce { get; private set; }
        public bool LastControlForceWasClamped { get; private set; }
        public uint StableEarthId => FragmentId;
        public EarthPhysicalTargetHandle TargetHandle => new EarthPhysicalTargetHandle(FragmentId, _generation);
        public float EarthMass => Mass;
        public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.Rock;
        public bool IsEarthTargetValid => targetBody != null && !targetBody.isKinematic && gameObject.activeSelf;

        public void ConfigureHover(EarthHoverProfile profile) => hoverProfile = profile;

        public void SetShape(Mesh shape)
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshCollider == null) _meshCollider = GetComponent<MeshCollider>();
            if (_meshFilter != null) _meshFilter.sharedMesh = shape;
            if (_meshCollider != null)
            {
                _meshCollider.sharedMesh = null;
                _meshCollider.sharedMesh = shape;
                _meshCollider.convex = true;
            }
        }

        public void Initialize(
            uint id,
            MagicExecutor executor,
            Vector3 position,
            float radius,
            float mass,
            Transform holdTarget = null,
            EarthFragmentPool sourcePool = null,
            EarthRockProfile profile = null)
        {
            FragmentId = id;
            _generation = NextGeneration(_generation);
            _executor = executor;
            _sourcePool = sourcePool;
            _profile = profile;
            _radius = Mathf.Max(0.05f, radius);
            _nextAccretionAt = Time.fixedTime + 0.65f;
            if (_gravityBody == null) _gravityBody = GetComponent<GravityBody>();
            RestoreSourceCollision();
            RestoreControllerCollision();
            _holdTarget = holdTarget;
            _isControlled = false;
            transform.position = position;
            // The selected SDF sphere remains the source of truth for mass. The authored
            // fragment is intentionally a little more compact so it reads as a controllable
            // broken rock instead of a perfect sphere matching the excavation envelope.
            float diameter = _radius * 2f * visualDiameterFactor;
            float width = Mathf.Lerp(0.86f, 1.18f, Hash01(id ^ 0x91A3u));
            float heightScale = Mathf.Lerp(0.72f, 1.08f, Hash01(id ^ 0xC44Fu));
            float depth = 1f / Mathf.Max(0.62f, width * heightScale);
            depth = Mathf.Clamp(depth, 0.76f, 1.22f);
            transform.localScale = new Vector3(width, heightScale, depth) * diameter;
            Vector3 localUp = position.sqrMagnitude > 0.001f ? position.normalized : Vector3.up;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, localUp) *
                                 Quaternion.Euler(
                                     Mathf.Lerp(-9f, 9f, Hash01(id ^ 0x312Du)),
                                     Hash01(id ^ 0xE71Fu) * 360f,
                                     Mathf.Lerp(-8f, 8f, Hash01(id ^ 0x7A55u)));
            targetBody.mass = Mathf.Max(0.01f, mass);
            targetBody.useGravity = false;
            targetBody.isKinematic = false;
            targetBody.linearVelocity = Vector3.zero;
            targetBody.angularVelocity = Vector3.zero;
            gameObject.SetActive(true);
            if (_bodyCollider == null) _bodyCollider = GetComponent<Collider>();
            _ignoredControllerCollider = holdTarget != null
                ? holdTarget.GetComponentInParent<Collider>()
                : null;
            if (_bodyCollider != null && _ignoredControllerCollider != null)
                UnityEngine.Physics.IgnoreCollision(
                    _bodyCollider, _ignoredControllerCollider, true);
            if (holdTarget != null)
            {
                BeginBendControl(holdTarget.position, Vector3.zero, 0f, BendTuning.Default);
                _holdTarget = holdTarget;
            }
        }

        private static uint NextGeneration(uint value) => value == uint.MaxValue ? 1u : value + 1u;

        public void Launch(Vector3 direction, float velocityChange)
        {
            StopBendControl();
            targetBody.AddForce(direction.normalized * (velocityChange * targetBody.mass), ForceMode.Impulse);
            targetBody.AddTorque(Vector3.Cross(direction.normalized, transform.up) * (targetBody.mass * 0.45f), ForceMode.Impulse);
        }

        public void BeginBendControl(
            Vector3 targetPosition,
            Vector3 targetVelocity,
            float charge01,
            in BendTuning tuning)
        {
            // An explicit world-space bending target supersedes the legacy transform
            // anchor. Otherwise a child anchor silently drags the rock with the caster.
            _holdTarget = null;
            _tuning = tuning;
            _bendTargetPosition = targetPosition;
            _previousTargetPosition = targetPosition;
            _bendTargetVelocity = targetVelocity;
            _charge01 = Mathf.Clamp01(charge01);
            _isControlled = true;
            targetBody.isKinematic = false;
            _hoverFrame = EarthHoverPhysics.Capture(targetBody, CurrentLocalUp(), FragmentId);
            targetBody.WakeUp();
        }

        public void BeginSurfaceEmergence(
            Collider sourceCollider,
            Vector3 surfacePoint,
            Vector3 localUp,
            float radius)
        {
            RestoreSourceCollision();
            if (_bodyCollider == null) _bodyCollider = GetComponent<Collider>();
            _ignoredSourceCollider = sourceCollider;
            _emergenceSurface = surfacePoint;
            _emergenceUp = localUp.sqrMagnitude > 0.0001f ? localUp.normalized : transform.up;
            _emergenceClearance = Mathf.Max(0.05f, radius * 0.92f);
            _emergenceCollisionRestoreAt = Time.fixedTime + 0.65f;
            if (_bodyCollider != null && _ignoredSourceCollider != null)
                UnityEngine.Physics.IgnoreCollision(_bodyCollider, _ignoredSourceCollider, true);
        }

        public void UpdateBendTarget(Vector3 position, Vector3 velocity, float charge01)
        {
            if (!_isControlled) return;
            _bendTargetPosition = position;
            _bendTargetVelocity = velocity;
            _charge01 = Mathf.Clamp01(charge01);
        }

        public Vector3 ReleaseBend(Vector3 aimDirection, Vector3 gestureVelocity, float charge01)
        {
            float3 solved = BendForceSolver.SolveReleaseVelocity(
                ToFloat3(targetBody.linearVelocity),
                ToFloat3(aimDirection),
                ToFloat3(gestureVelocity),
                charge01,
                _tuning);
            Vector3 releaseVelocity = ToVector3(solved);
            StopBendControl();
            targetBody.linearVelocity = releaseVelocity;
            return releaseVelocity;
        }

        public void StopBendControl()
        {
            _holdTarget = null;
            _isControlled = false;
            LastControlError = Vector3.zero;
            LastAppliedControlForce = Vector3.zero;
            LastControlForceWasClamped = false;
            if (targetBody != null) targetBody.isKinematic = false;
            RestoreControllerCollision();
        }

        public bool TryShatter(Vector3 point, Vector3 normal, float impulse)
        {
            return _sourcePool != null && _sourcePool.TryShatter(this, point, normal, impulse);
        }

        public float ComputeCraterRadius(float impulse)
        {
            if (_profile == null) return Mathf.Clamp(impulse * 0.0025f, 0.25f, 1.25f);
            return Mathf.Clamp(
                impulse * _profile.CraterRadiusPerImpulse,
                _profile.MinimumCraterRadius,
                _profile.MaximumCraterRadius);
        }

        public bool TryReserveAccretionPulse(float now, out float volume)
        {
            volume = 0f;
            if (!IsHeld || _profile == null || now < _nextAccretionAt ||
                _radius >= _profile.MaximumRadius) return false;
            float currentVolume = (4f / 3f) * Mathf.PI * _radius * _radius * _radius;
            float maximumVolume = (4f / 3f) * Mathf.PI *
                                  _profile.MaximumRadius * _profile.MaximumRadius * _profile.MaximumRadius;
            volume = Mathf.Min(_profile.AccretionVolumePerPulse, maximumVolume - currentVolume);
            if (volume <= 0.0001f) return false;
            _nextAccretionAt = now + _profile.AccretionIntervalSeconds;
            return true;
        }

        public void AccreteVolume(float volume)
        {
            if (volume <= 0f) return;
            float currentVolume = (4f / 3f) * Mathf.PI * _radius * _radius * _radius;
            float maximumRadius = _profile != null ? _profile.MaximumRadius : 2.4f;
            float maximumVolume = (4f / 3f) * Mathf.PI *
                                  maximumRadius * maximumRadius * maximumRadius;
            float nextVolume = Mathf.Min(maximumVolume, currentVolume + volume);
            _radius = Mathf.Pow((nextVolume * 3f) / (4f * Mathf.PI), 1f / 3f);
            Vector3 proportions = transform.localScale.normalized;
            float average = Mathf.Max(0.0001f,
                (transform.localScale.x + transform.localScale.y + transform.localScale.z) / 3f);
            proportions = transform.localScale / average;
            transform.localScale = proportions * (_radius * 2f * visualDiameterFactor);
            float density = _profile != null ? _profile.MaterialDensity : 120f;
            targetBody.mass = Mathf.Max(0.01f, targetBody.mass + (volume * density));
        }

        private void FixedUpdate()
        {
            UpdateEmergenceCollision();
            if (!IsHeld) return;
            _executor?.TryAccreteHeldFragment(this);
            using (BendControlMarker.Auto())
            {
                if (_holdTarget != null)
                {
                    Vector3 current = _holdTarget.position;
                    _bendTargetVelocity = (current - _previousTargetPosition) /
                                          Mathf.Max(0.0001f, Time.fixedDeltaTime);
                    _bendTargetPosition = current;
                    _previousTargetPosition = current;
                }

                Vector3 hoverTarget = _bendTargetPosition + EarthHoverPhysics.BobOffset(
                    in _hoverFrame, CurrentLocalUp(), Time.fixedTime, hoverProfile);
                BendForceResult result = BendForceSolver.SolvePdForce(
                    ToFloat3(targetBody.worldCenterOfMass),
                    ToFloat3(targetBody.linearVelocity),
                    ToFloat3(hoverTarget),
                    ToFloat3(_bendTargetVelocity),
                    targetBody.mass,
                    _gravityBody != null ? ToFloat3(_gravityBody.LastAcceleration) : float3.zero,
                    _charge01,
                    _tuning);
                LastControlError = ToVector3(result.PositionError);
                LastAppliedControlForce = ToVector3(result.AppliedForce);
                LastControlForceWasClamped = result.WasClamped;
                targetBody.AddForce(LastAppliedControlForce, ForceMode.Force);
                EarthHoverPhysics.Stabilize(
                    targetBody, in _hoverFrame, CurrentLocalUp(), Time.fixedTime, hoverProfile);
            }
        }

        private Vector3 CurrentLocalUp()
        {
            Vector3 gravity = _gravityBody != null ? _gravityBody.LastAcceleration : Vector3.zero;
            if (gravity.sqrMagnitude > 0.01f) return -gravity.normalized;
            return transform.position.sqrMagnitude > 0.01f ? transform.position.normalized : transform.up;
        }

        private void Awake()
        {
            if (targetBody == null)
            {
                targetBody = GetComponent<Rigidbody>();
            }
            _gravityBody = GetComponent<GravityBody>();
            _bodyCollider = GetComponent<Collider>();
            _meshFilter = GetComponent<MeshFilter>();
            _meshCollider = GetComponent<MeshCollider>();
        }

        private void OnDisable()
        {
            RestoreSourceCollision();
            RestoreControllerCollision();
        }

        private void UpdateEmergenceCollision()
        {
            if (_ignoredSourceCollider == null) return;
            float clearance = Vector3.Dot(targetBody.worldCenterOfMass - _emergenceSurface, _emergenceUp);
            if (clearance >= _emergenceClearance || Time.fixedTime >= _emergenceCollisionRestoreAt)
                RestoreSourceCollision();
        }

        private void RestoreSourceCollision()
        {
            if (_bodyCollider != null && _ignoredSourceCollider != null)
                UnityEngine.Physics.IgnoreCollision(_bodyCollider, _ignoredSourceCollider, false);
            _ignoredSourceCollider = null;
        }

        private void RestoreControllerCollision()
        {
            if (_bodyCollider != null && _ignoredControllerCollider != null)
                UnityEngine.Physics.IgnoreCollision(
                    _bodyCollider, _ignoredControllerCollider, false);
            _ignoredControllerCollider = null;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_executor == null || Time.frameCount - _lastImpactFrame < 3)
            {
                return;
            }

            float impulse = collision.impulse.magnitude;
            if (impulse < 1f)
            {
                return;
            }

            _lastImpactFrame = Time.frameCount;
            _executor.HandleFragmentImpact(this, collision, impulse);
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);

        public void OnEarthMagicGrabbed(EarthMagicGripKind grip)
        {
            if (targetBody != null) targetBody.WakeUp();
        }

        public void OnEarthMagicReleased(EarthMagicGripKind grip)
        {
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
