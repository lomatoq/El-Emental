using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ReactionImpulseBody : MonoBehaviour
    {
        [SerializeField] private ThermalWaterMagicExecutor executor;
        [SerializeField] private Rigidbody targetBody;
        [SerializeField, Min(0f)] private float maximumImpulse = 30f;

        public int AppliedReactionCount { get; private set; }

        public void Configure(ThermalWaterMagicExecutor configuredExecutor, Rigidbody configuredBody, float configuredMaximumImpulse = 30f)
        {
            executor = configuredExecutor; targetBody = configuredBody; maximumImpulse = Mathf.Max(0f, configuredMaximumImpulse);
        }

        private void Awake()
        {
            if (targetBody == null) targetBody = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (executor != null) executor.Events.ReactionTriggered += OnReaction;
        }

        private void OnDisable()
        {
            if (executor != null) executor.Events.ReactionTriggered -= OnReaction;
        }

        private void OnReaction(ReactionTriggeredEvent value)
        {
            Vector3 point = ToVector3(value.Position);
            Vector3 direction = targetBody.worldCenterOfMass - point;
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.up;
            float impulse = Mathf.Min(maximumImpulse, Mathf.Max(0f, value.PressureImpulse));
            targetBody.AddForceAtPosition(direction.normalized * impulse, point, ForceMode.Impulse);
            AppliedReactionCount++;
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
