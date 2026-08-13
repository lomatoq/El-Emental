using Elemental.Simulation.Bending;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthTelekinesisController : MonoBehaviour
    {
        private static readonly ProfilerMarker ControlMarker =
            new ProfilerMarker("Elemental.Bending.ExistingEarthBodyControl");

        [SerializeField, Min(1f)] private float maximumControllableMass = 5000f;
        [SerializeField] private EarthHoverProfile hoverProfile;
        [SerializeField] private Transform planetCenter;

        private Rigidbody _body;
        private IEarthPhysicalTarget _earthTarget;
        private GravityBody _gravityBody;
        private BendTuning _tuning = BendTuning.Default;
        private Vector3 _targetPosition;
        private Vector3 _targetVelocity;
        private float _charge01;
        private uint _nextBodyId = 1u;
        private EarthHoverFrame _hoverFrame;

        public Rigidbody Body => _body != null && !_body.isKinematic ? _body : null;
        public uint BodyId { get; private set; }
        public Vector3 LastControlError { get; private set; }
        public Vector3 LastAppliedControlForce { get; private set; }
        public bool LastControlForceWasClamped { get; private set; }

        public void ConfigureHover(EarthHoverProfile profile, Transform configuredPlanetCenter)
        {
            hoverProfile = profile;
            planetCenter = configuredPlanetCenter;
        }

        public bool TryAcquire(
            Rigidbody body,
            Vector3 targetPosition,
            in BendTuning tuning,
            IEarthPhysicalTarget earthTarget = null)
        {
            earthTarget?.OnEarthMagicGrabbed(EarthMagicGripKind.Telekinesis);
            if (body == null || body.isKinematic || body.mass <= 0f || body.mass > maximumControllableMass)
            {
                earthTarget?.OnEarthMagicReleased(EarthMagicGripKind.Telekinesis);
                return false;
            }
            _body = body;
            _earthTarget = earthTarget;
            _gravityBody = body.GetComponent<GravityBody>();
            _tuning = tuning;
            _targetPosition = targetPosition;
            _targetVelocity = Vector3.zero;
            _charge01 = 0f;
            BodyId = _nextBodyId++;
            _hoverFrame = EarthHoverPhysics.Capture(body, CurrentLocalUp(body), BodyId);
            body.WakeUp();
            return true;
        }

        public void UpdateTarget(Vector3 targetPosition, Vector3 targetVelocity, float charge01)
        {
            if (Body == null) return;
            _targetPosition = targetPosition;
            _targetVelocity = targetVelocity;
            _charge01 = Mathf.Clamp01(charge01);
        }

        public bool Release(
            Vector3 aimDirection,
            Vector3 gestureVelocity,
            float charge01,
            out Vector3 releaseVelocity)
        {
            releaseVelocity = Vector3.zero;
            Rigidbody body = Body;
            if (body == null) return false;
            float3 solved = BendForceSolver.SolveReleaseVelocity(
                ToFloat3(body.linearVelocity),
                ToFloat3(aimDirection),
                ToFloat3(gestureVelocity),
                charge01,
                _tuning);
            releaseVelocity = ToVector3(solved);
            body.linearVelocity = releaseVelocity;
            Clear(true);
            return true;
        }

        public void Clear(bool notifyReleased = true)
        {
            if (notifyReleased) _earthTarget?.OnEarthMagicReleased(EarthMagicGripKind.Telekinesis);
            _body = null;
            _gravityBody = null;
            _earthTarget = null;
            LastControlError = Vector3.zero;
            LastAppliedControlForce = Vector3.zero;
            LastControlForceWasClamped = false;
        }

        private void FixedUpdate()
        {
            Rigidbody body = Body;
            if (body == null)
            {
                if (_body != null) Clear();
                return;
            }

            using (ControlMarker.Auto())
            {
                Vector3 localUp = CurrentLocalUp(body);
                Vector3 hoverTarget = _targetPosition + EarthHoverPhysics.BobOffset(
                    in _hoverFrame, localUp, Time.fixedTime, hoverProfile);
                BendForceResult result = BendForceSolver.SolvePdForce(
                    ToFloat3(body.worldCenterOfMass),
                    ToFloat3(body.linearVelocity),
                    ToFloat3(hoverTarget),
                    ToFloat3(_targetVelocity),
                    body.mass,
                    _gravityBody != null ? ToFloat3(_gravityBody.LastAcceleration) : float3.zero,
                    _charge01,
                    _tuning);
                LastControlError = ToVector3(result.PositionError);
                LastAppliedControlForce = ToVector3(result.AppliedForce);
                LastControlForceWasClamped = result.WasClamped;
                body.AddForce(LastAppliedControlForce, ForceMode.Force);
                EarthHoverPhysics.Stabilize(
                    body, in _hoverFrame, localUp, Time.fixedTime, hoverProfile);
            }
        }

        private Vector3 CurrentLocalUp(Rigidbody body)
        {
            Vector3 gravity = _gravityBody != null ? _gravityBody.LastAcceleration : Vector3.zero;
            if (gravity.sqrMagnitude > 0.01f) return -gravity.normalized;
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            Vector3 radial = body != null ? body.worldCenterOfMass - center : Vector3.up;
            return radial.sqrMagnitude > 0.01f ? radial.normalized : Vector3.up;
        }

        private void OnDisable() => Clear();
        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
