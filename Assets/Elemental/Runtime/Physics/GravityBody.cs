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
        public Vector3 LastAcceleration { get; private set; }

        public void Configure(GravityWorldBehaviour world, Rigidbody body)
        {
            gravityWorld = world;
            targetBody = body;
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
            if (gravityWorld == null || !gravityWorld.IsReady)
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
                LastAcceleration = new Vector3(acceleration.x, acceleration.y, acceleration.z);
                targetBody.AddForce(
                    LastAcceleration,
                    ForceMode.Acceleration);
            }
        }
    }
}
