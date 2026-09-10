using UnityEngine;
using Unity.Mathematics;
using Elemental.Simulation.Combat;

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
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private EarthFragment _fragment;
        private Rigidbody _body;
        private Collider _collider;
        private EarthPhysicsFeelProfile _profile;
        private Vector3 _previousPosition;
        private bool _hasPrevious;
        private bool _armed;
        public int SaturatedQueries { get; private set; }
        public int AcceptedSweeps { get; private set; }
        public Vector3 LastSweepHalfExtents { get; private set; }
        public Quaternion LastSweepOrientation { get; private set; }
        public Collider LastSweptCollider { get; private set; }

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
            ResolveSweepBox(_collider, out Vector3 boxCenter, out Vector3 halfExtents, out Quaternion orientation);
            halfExtents = Vector3.Max(Vector3.one * .025f,
                halfExtents * (_profile != null ? _profile.ProjectileSweepExtentRatio : .82f));
            Vector3 startCenter = _previousPosition + (boxCenter-current);
            LastSweepHalfExtents=halfExtents;LastSweepOrientation=orientation;
            int count = UnityEngine.Physics.BoxCastNonAlloc(
                startCenter,
                halfExtents,
                direction,
                _hits,
                orientation,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);
            if(count==_hits.Length)
            {
                // Truncated broad-phase results cannot establish the nearest blocking surface.
                SaturatedQueries++;
                _body.position=_previousPosition;
                _body.linearVelocity=Vector3.ProjectOnPlane(_body.linearVelocity,direction);
                return;
            }
            while (_armed)
            {
                int selected = -1;
                float nearest = float.PositiveInfinity;
                for (int index = 0; index < count; index++)
                {
                    RaycastHit candidate = _hits[index];
                    if (candidate.collider == null || candidate.collider == _collider ||
                        candidate.rigidbody == _body || candidate.distance >= nearest || candidate.normal.sqrMagnitude < .5f ||
                        UnityEngine.Physics.GetIgnoreLayerCollision(_collider.gameObject.layer,candidate.collider.gameObject.layer) ||
                        UnityEngine.Physics.GetIgnoreCollision(_collider,candidate.collider)) continue;
                    EarthPlatform platform = candidate.collider.GetComponentInParent<EarthPlatform>();
                    if (platform == null)
                        platform = candidate.collider.GetComponentInParent<EarthPlatformPiece>()?.Owner;
                    EarthWall wall = candidate.collider.GetComponentInParent<EarthWall>();
                    if (wall == null)
                        wall = candidate.collider.GetComponentInParent<EarthWallPiece>()?.Owner;
                    EarthArenaStructure arena = candidate.collider.GetComponentInParent<EarthArenaStructure>();
                    if (arena == null)
                        arena = candidate.collider.GetComponentInParent<EarthArenaPiece>()?.Owner;
                    // Loose Earth bodies are physical blockers too. Preserve every explicit temporary ignore pair.
                    IEarthPhysicalTarget earth = candidate.rigidbody != null
                        ? candidate.rigidbody.GetComponent<IEarthPhysicalTarget>() : null;
                    if (platform == null && wall == null && arena == null &&
                        (earth == null || !earth.IsEarthTargetValid)) continue;
                    selected = index;
                    nearest = candidate.distance;
                }
                if (selected < 0) break;

                RaycastHit hit = _hits[selected];
                _hits[selected] = default;
                Rigidbody other=hit.rigidbody;
                Vector3 incoming=_body.linearVelocity;
                Vector3 targetVelocity=other!=null?other.linearVelocity:Vector3.zero;
                Vector3 incomingSurface=other!=null?other.GetPointVelocity(hit.point):Vector3.zero;
                bool dynamicTarget=other!=null&&!other.isKinematic;
                float rebound=_profile!=null?_profile.ProjectileSweepRebound:.06f;
                EarthStoneSweepResult response=EarthStoneSweepResponse.Resolve((float3)incoming,(float3)targetVelocity,
                    _body.mass,other!=null?other.mass:0f,dynamicTarget,(float3)hit.normal,rebound);
                if(!response.Accepted)continue;
                float skin=_profile!=null?_profile.ProjectileSweepSkin:.015f;
                Vector3 corrected=_previousPosition+direction*Mathf.Max(0f,hit.distance-skin);
                _body.position=corrected;
                _body.linearVelocity=(Vector3)response.SourceVelocity;
                if(dynamicTarget)other.linearVelocity=(Vector3)response.TargetVelocity;
                // Damage admission cannot undo the physical contact. Publish its incoming speed after
                // applying the pair response so a canonical split inherits the resolved velocity.
                _fragment.HandleSweptImpact(hit.collider,hit.point,hit.normal,0f,response.Impulse,out _,incoming,incomingSurface);
                LastSweptCollider=hit.collider;AcceptedSweeps++;
                current=corrected;
                break; // At most one contact correction per tick; keep insurance armed for the next flight segment.
            }
            _previousPosition = current;
        }
        public static void ResolveSweepBox(Collider shape,out Vector3 center,out Vector3 halfExtents,out Quaternion orientation)
        {
            // Mesh.bounds and BoxCollider.size are local; Collider.bounds is already world-aligned.
            // Applying the body's rotation to world-AABB extents rotates/inflates the envelope twice.
            Vector3 scale=shape.transform.lossyScale;
            scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            if(shape is MeshCollider mesh && mesh.sharedMesh!=null)
            {
                Bounds local=mesh.sharedMesh.bounds;
                center=shape.transform.TransformPoint(local.center);
                halfExtents=Vector3.Scale(local.extents,scale);orientation=shape.transform.rotation;
            }
            else if(shape is BoxCollider box)
            {
                center=shape.transform.TransformPoint(box.center);
                halfExtents=Vector3.Scale(box.size*.5f,scale);orientation=shape.transform.rotation;
            }
            else
            {center=shape.bounds.center;halfExtents=shape.bounds.extents;orientation=Quaternion.identity;}
        }
    }
}
