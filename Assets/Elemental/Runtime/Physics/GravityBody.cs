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
                if (targetBody.IsSleeping() && (LastAcceleration-previousAcceleration).sqrMagnitude < .0025f)
                    return;
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
        private void OnDisable() { _rest=default; _lastSupportTime=float.NegativeInfinity; }

        private void RecordSupport(Collision collision)
        {
            if (LastAcceleration.sqrMagnitude < .001f || collision == null) return;
            Rigidbody other = collision.rigidbody;
            if (other != null && !other.isKinematic && !other.IsSleeping() &&
                (other.linearVelocity.sqrMagnitude > .0049f || other.angularVelocity.sqrMagnitude > .0144f)) return;
            Vector3 up = -LastAcceleration.normalized;
            for (int i=0;i<collision.contactCount;i++)
                if (Vector3.Dot(collision.GetContact(i).normal,up) > .65f)
                { _lastSupportTime=Time.fixedTime; return; }
        }
    }
}
