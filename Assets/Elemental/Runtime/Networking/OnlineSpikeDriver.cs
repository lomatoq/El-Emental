using Elemental.Simulation.Networking;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Networking
{
    [DisallowMultipleComponent]
    public sealed class OnlineSpikeDriver : MonoBehaviour
    {
        [SerializeField, Range(2, 4)] private int clients = 4;
        [SerializeField, Min(0)] private int latencyTicks = 6;
        [SerializeField, Range(0f, 0.5f)] private float loss = 0.08f;
        [SerializeField] private Transform[] authorityMarkers;
        [SerializeField] private Transform[] predictedMarkers;
        private OnlineSpikeHarness _harness;
        private uint _tick;

        public OnlineSpikeHarness Harness => _harness;
        public int CorrectionCount { get; private set; }
        public float MaximumCorrectionError { get; private set; }

        public void Configure(int configuredClients, int configuredLatencyTicks, float configuredLoss, Transform[] configuredAuthority, Transform[] configuredPredicted)
        {
            clients = Mathf.Clamp(configuredClients, 2, 4); latencyTicks = Mathf.Max(0, configuredLatencyTicks);
            loss = Mathf.Clamp01(configuredLoss); authorityMarkers = configuredAuthority; predictedMarkers = configuredPredicted;
            Rebuild();
        }

        private void Awake()
        {
            if (_harness == null) Rebuild();
        }

        private void FixedUpdate()
        {
            _harness.Tick(_tick);
            for (int index = 0; index < clients; index++)
            {
                float angle = (_tick * 0.015f) + (index * math.PI * 2f / clients);
                float3 authorityPosition = new float3(math.cos(angle) * 5f, 28f + math.sin(angle * 0.5f), math.sin(angle) * 5f);
                float3 predictedPosition = authorityPosition + new float3(math.sin(_tick * 0.11f + index) * 0.35f, 0f, 0f);
                var snapshot = new RigidbodySnapshot((uint)(index + 1), _tick, authorityPosition, quaternion.identity, float3.zero, float3.zero, 200);
                CorrectionResult correction = PredictionReconciler.Reconcile(predictedPosition, float3.zero, in snapshot);
                if (authorityMarkers != null && index < authorityMarkers.Length && authorityMarkers[index] != null)
                    authorityMarkers[index].position = ToVector3(authorityPosition);
                if (predictedMarkers != null && index < predictedMarkers.Length && predictedMarkers[index] != null)
                    predictedMarkers[index].position = ToVector3(correction.Position);
                if (correction.Error > 0.15f) CorrectionCount++;
                MaximumCorrectionError = Mathf.Max(MaximumCorrectionError, correction.Error);
            }
            _tick++;
        }

        private void Rebuild()
        {
            var profile = new TransportProfile(latencyTicks, 2, loss, 256);
            _harness = new OnlineSpikeHarness(clients, in profile, 0x0A11CEu);
            _tick = 0u;
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
