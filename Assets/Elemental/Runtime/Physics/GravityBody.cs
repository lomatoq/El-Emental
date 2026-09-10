using Elemental.Simulation.Gravity;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GravityBody : MonoBehaviour
    {
        private static readonly ProfilerMarker FixedTickMarker = new ProfilerMarker("Elemental.GravityBody.FixedTick");

        [SerializeField] private GravityWorldBehaviour gravityWorld;
        [SerializeField] private Rigidbody targetBody;

        private uint _tick;
        private Collider _supportCollider;
        private Mesh _supportMesh;
        private Bounds _supportMeshBounds;
        private Vector3 _supportPointLocal, _supportPointWorld;
        private Vector3 _supportNormalLocal, _supportNormalWorld;
        private EarthBodyRestState _rest;
        private float _lastSupportTime = float.NegativeInfinity;
        public Vector3 LastAcceleration { get; private set; }
        public GravityWorldBehaviour GravityWorld => gravityWorld;
        public Rigidbody TargetBody => targetBody;
        public bool IsOperational => enabled && gravityWorld != null &&
                                     gravityWorld.IsReady && targetBody != null &&
                                     !targetBody.isKinematic;

        public void Configure(GravityWorldBehaviour world, Rigidbody body)
        {
            gravityWorld = world;
            targetBody = body;
            _rest = default;
            _supportCollider = null;
            _lastSupportTime = float.NegativeInfinity;
            if (targetBody != null)
            {
                targetBody.useGravity = false;
            }
        }

        private void Awake()
        {
            if (targetBody == null)
            {
                targetBody = GetComponent<Rigidbody>();
            }

            targetBody.useGravity = false;
        }

        private void FixedUpdate()
        {
            if (targetBody == null || targetBody.isKinematic ||
                gravityWorld == null || !gravityWorld.IsReady)
            {
                return;
            }

            using (FixedTickMarker.Auto())
            {
                Vector3 centerOfMass = targetBody.worldCenterOfMass;
                GravitySample sample = gravityWorld.World.Sample(
                    new float3(centerOfMass.x, centerOfMass.y, centerOfMass.z),
                    _tick++);

                float3 acceleration = sample.Acceleration;
                Vector3 previousAcceleration = LastAcceleration;
                LastAcceleration = new Vector3(acceleration.x, acceleration.y, acceleration.z);
                // AddForce wakes sleeping bodies. Resting stones must keep their
                // contact solution until an impact/grab/support change wakes them.
                if (targetBody.IsSleeping())
                {
                    if ((LastAcceleration-previousAcceleration).sqrMagnitude < .0025f &&
                        (LastAcceleration.sqrMagnitude < .0001f || SleepingSupportUnchanged()))
                        return;
                    // Removing/moving a static collider does not reliably wake a sleeping island.
                    _rest = default;
                    _lastSupportTime = float.NegativeInfinity;
                    targetBody.WakeUp();
                }
                bool supported = Time.fixedTime-_lastSupportTime <= Time.fixedDeltaTime*1.5f;
                if (_rest.Step(supported, targetBody.linearVelocity, targetBody.angularVelocity, Time.fixedDeltaTime))
                {
                    targetBody.Sleep();
                    return;
                }
                targetBody.AddForce(
                    LastAcceleration,
                    ForceMode.Acceleration);
            }
        }

        private void OnCollisionEnter(Collision collision) => RecordSupport(collision);
        private void OnCollisionStay(Collision collision) => RecordSupport(collision);
        private void OnDisable() { _rest=default; _supportCollider=null; _lastSupportTime=float.NegativeInfinity; }
        private void OnCollisionExit(Collision collision)
        {
            if (collision != null && collision.collider == _supportCollider)
            { _supportCollider=null; _lastSupportTime=float.NegativeInfinity; _rest=default; }
        }
        private bool SleepingSupportUnchanged()
        {
            if (_supportCollider == null || !_supportCollider.enabled || !_supportCollider.gameObject.activeInHierarchy)
                return false;
            if (_supportCollider is MeshCollider mesh)
            {
                if (mesh.sharedMesh != _supportMesh || mesh.sharedMesh == null ||
                    mesh.sharedMesh.bounds != _supportMeshBounds) return false;
            }
            Transform support = _supportCollider.transform;
            if ((support.TransformPoint(_supportPointLocal)-_supportPointWorld).sqrMagnitude > .000001f ||
                Vector3.Dot(support.TransformDirection(_supportNormalLocal),_supportNormalWorld) < .99999f)
                return false;
            Rigidbody supportBody = _supportCollider.attachedRigidbody;
            return supportBody == null || supportBody.isKinematic || supportBody.IsSleeping() ||
                (supportBody.linearVelocity.sqrMagnitude <= .0049f && supportBody.angularVelocity.sqrMagnitude <= .0144f);
        }

        private void RecordSupport(Collision collision)
        {
            if (LastAcceleration.sqrMagnitude < .001f || collision == null) return;
            Rigidbody other = collision.rigidbody;
            if (other != null && !other.isKinematic && !other.IsSleeping() &&
                (other.linearVelocity.sqrMagnitude > .0049f || other.angularVelocity.sqrMagnitude > .0144f)) return;
            Vector3 up = -LastAcceleration.normalized;
            for (int i=0;i<collision.contactCount;i++)
                if (Vector3.Dot(collision.GetContact(i).normal,up) > .65f)
                {
                    ContactPoint contact=collision.GetContact(i);
                    _supportCollider=collision.collider;
                    _supportMesh=(_supportCollider as MeshCollider)?.sharedMesh;
                    _supportMeshBounds=_supportMesh!=null?_supportMesh.bounds:default;
                    _supportPointWorld=contact.point;
                    _supportNormalWorld=contact.normal;
                    _supportPointLocal=_supportCollider.transform.InverseTransformPoint(contact.point);
                    _supportNormalLocal=_supportCollider.transform.InverseTransformDirection(contact.normal);
                    _lastSupportTime=Time.fixedTime;
                    return;
                }
        }
    }
}
