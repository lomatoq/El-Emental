using Elemental.Runtime.World;
using Elemental.Simulation.Fields;
using Elemental.Simulation.Gravity;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AirFieldBody : MonoBehaviour
    {
        private static readonly ProfilerMarker SampleMarker = new ProfilerMarker("Elemental.AirFieldBody.SampleAndApply");

        [SerializeField] private FieldWorldBehaviour fieldWorld;
        [SerializeField] private GravityWorldBehaviour gravityWorld;
        [SerializeField] private Rigidbody targetBody;
        [SerializeField, Min(0.01f)] private float projectedArea = 0.8f;
        [SerializeField, Min(0f)] private float dragCoefficient = 0.9f;
        [SerializeField, Min(0f)] private float liftCoefficient = 0.15f;
        [SerializeField, Min(0.1f)] private float maximumAcceleration = 45f;

        private uint _tick;

        public FieldSample LastSample { get; private set; }
        public Vector3 LastAcceleration { get; private set; }

        public void Configure(
            FieldWorldBehaviour configuredFieldWorld,
            Rigidbody configuredBody,
            GravityWorldBehaviour configuredGravityWorld = null,
            float configuredArea = 0.8f,
            float configuredDrag = 0.9f,
            float configuredLift = 0.15f,
            float configuredMaximumAcceleration = 45f)
        {
            fieldWorld = configuredFieldWorld;
            targetBody = configuredBody;
            gravityWorld = configuredGravityWorld;
            projectedArea = Mathf.Max(0.01f, configuredArea);
            dragCoefficient = Mathf.Max(0f, configuredDrag);
            liftCoefficient = Mathf.Max(0f, configuredLift);
            maximumAcceleration = Mathf.Max(0.1f, configuredMaximumAcceleration);
        }

        private void Awake()
        {
            if (targetBody == null)
            {
                targetBody = GetComponent<Rigidbody>();
            }
        }

        private void FixedUpdate()
        {
            if (fieldWorld == null || !fieldWorld.IsReady || targetBody == null)
            {
                return;
            }

            using (SampleMarker.Auto())
            {
                Vector3 center = targetBody.worldCenterOfMass;
                float3 point = new float3(center.x, center.y, center.z);
                FieldSample sample = fieldWorld.World.Sample(point);
                LastSample = sample;
                float3 localUp = ResolveLocalUp(point);
                var profile = new AerodynamicResponseProfile(
                    projectedArea,
                    dragCoefficient,
                    liftCoefficient,
                    maximumAcceleration);
                Vector3 velocity = targetBody.linearVelocity;
                float3 acceleration = AerodynamicMath.ComputeAcceleration(
                    in sample,
                    new float3(velocity.x, velocity.y, velocity.z),
                    Mathf.Max(0.01f, targetBody.mass),
                    in profile,
                    localUp);
                LastAcceleration = new Vector3(acceleration.x, acceleration.y, acceleration.z);
                targetBody.AddForce(LastAcceleration, ForceMode.Acceleration);
                _tick++;
            }
        }

        private float3 ResolveLocalUp(float3 position)
        {
            if (gravityWorld != null && gravityWorld.IsReady)
            {
                GravitySample sample = gravityWorld.World.Sample(position, _tick);
                return math.normalizesafe(-sample.Acceleration, new float3(0f, 1f, 0f));
            }

            return new float3(transform.up.x, transform.up.y, transform.up.z);
        }
    }
}
