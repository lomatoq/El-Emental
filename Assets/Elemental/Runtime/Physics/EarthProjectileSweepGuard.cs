using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>
    /// Convex MeshCollider CCD insurance for fast earth projectiles. It sweeps the
    /// oriented bounds across the last physical displacement and reports one impact.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class EarthProjectileSweepGuard : MonoBehaviour
    {
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private EarthFragment _fragment;
        private Rigidbody _body;
        private Collider _collider;
        private EarthPhysicsFeelProfile _profile;
        private Vector3 _previousPosition;
        private bool _hasPrevious;
        private bool _armed;

        public void Configure(EarthFragment fragment, EarthPhysicsFeelProfile profile)
        {
            _fragment = fragment;
            _body = fragment != null ? fragment.Body : GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _profile = profile;
            _previousPosition = _body != null ? _body.position : transform.position;
            _hasPrevious = true;
            _armed = true;
        }

        public void Arm()
        {
            _previousPosition = _body != null ? _body.position : transform.position;
            _hasPrevious = true;
            _armed = true;
        }

        private void OnEnable()
        {
            _body ??= GetComponent<Rigidbody>();
            _collider ??= GetComponent<Collider>();
            _previousPosition = _body != null ? _body.position : transform.position;
            _hasPrevious = true;
            _armed = true;
        }

        private void FixedUpdate()
        {
            if (!_armed || _fragment == null || _body == null || _collider == null ||
                !_fragment.gameObject.activeSelf || _body.isKinematic)
            {
                _hasPrevious = false;
                return;
            }
            Vector3 current = _body.position;
            if (!_hasPrevious)
            {
                _previousPosition = current;
                _hasPrevious = true;
                return;
            }
            Vector3 displacement = current - _previousPosition;
            float distance = displacement.magnitude;
            float minimumSpeed = _profile != null ? _profile.ProjectileSweepMinimumSpeed : 16f;
            if (distance <= minimumSpeed * Time.fixedDeltaTime * 0.35f)
            {
                _previousPosition = current;
                return;
            }
            Vector3 direction = displacement / distance;
            Bounds bounds = _collider.bounds;
            Vector3 halfExtents = Vector3.Max(
                Vector3.one * 0.025f,
                bounds.extents * (_profile != null ? _profile.ProjectileSweepExtentRatio : 0.82f));
            int count = UnityEngine.Physics.BoxCastNonAlloc(
                _previousPosition,
                halfExtents,
                direction,
                _hits,
                _body.rotation,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);
            while (_armed)
            {
                int selected = -1;
                float nearest = float.PositiveInfinity;
                for (int index = 0; index < count; index++)
                {
                    RaycastHit candidate = _hits[index];
                    if (candidate.collider == null || candidate.collider == _collider ||
                        candidate.rigidbody == _body || candidate.distance >= nearest) continue;
                    EarthPlatform platform = candidate.collider.GetComponentInParent<EarthPlatform>();
                    if (platform == null)
                        platform = candidate.collider.GetComponentInParent<EarthPlatformPiece>()?.Owner;
                    EarthWall wall = candidate.collider.GetComponentInParent<EarthWall>();
                    if (wall == null)
                        wall = candidate.collider.GetComponentInParent<EarthWallPiece>()?.Owner;
                    EarthArenaStructure arena = candidate.collider.GetComponentInParent<EarthArenaStructure>();
                    if (arena == null)
                        arena = candidate.collider.GetComponentInParent<EarthArenaPiece>()?.Owner;
                    if (platform == null && wall == null && arena == null) continue;
                    selected = index;
                    nearest = candidate.distance;
                }
                if (selected < 0) break;

                RaycastHit hit = _hits[selected];
                _hits[selected] = default;
                Vector3 surfaceVelocity = hit.rigidbody != null
                    ? hit.rigidbody.GetPointVelocity(hit.point)
                    : Vector3.zero;
                float normalSpeed = Vector3.Dot(_body.linearVelocity - surfaceVelocity, hit.normal);
                float impulse = Mathf.Abs(Mathf.Min(0f, normalSpeed)) * Mathf.Max(0.01f, _body.mass);
                if (!_fragment.HandleSweptImpact(
                        hit.collider,
                        hit.point,
                        hit.normal,
                        0f,
                        impulse,
                        out _))
                    continue;

                float skin = _profile != null ? _profile.ProjectileSweepSkin : 0.015f;
                Vector3 corrected = _previousPosition + direction * Mathf.Max(0f, hit.distance - skin);
                _body.position = corrected;
                float rebound = _profile != null ? _profile.ProjectileSweepRebound : 0.06f;
                if (normalSpeed < 0f)
                    _body.linearVelocity -= hit.normal * normalSpeed * (1f + rebound);
                _armed = false;
                current = corrected;
            }
            _previousPosition = current;
        }
    }
}
