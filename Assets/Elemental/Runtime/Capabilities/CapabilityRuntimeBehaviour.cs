using Elemental.Simulation.Capabilities;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Capabilities
{
    [DisallowMultipleComponent]
    public sealed class CapabilityRuntimeBehaviour : MonoBehaviour
    {
        private static readonly ProfilerMarker Marker = new ProfilerMarker("Elemental.Capability.Evaluate");
        [SerializeField] private CapabilityProfileAsset profileAsset;
        [SerializeField] private ParticleSystem[] presentationParticles;
        [SerializeField, Min(1)] private int simulatedDistantWork = 1;
        private AdaptiveBudgetScheduler _scheduler;

        public CapabilityProfileData Profile { get; private set; }
        public DegradationDecision Decision { get; private set; }
        public float StartupSeconds { get; private set; }
        public float MemoryMegabytes => System.GC.GetTotalMemory(false) / (1024f * 1024f);
        public int RejectedDistantWork { get; private set; }

        public void Configure(CapabilityProfileAsset configuredProfile, ParticleSystem[] particles)
        {
            profileAsset = configuredProfile; presentationParticles = particles;
            if (isActiveAndEnabled) Rebuild();
        }

        private void Awake()
        {
            Rebuild();
        }

        private void Start() => StartupSeconds = Time.realtimeSinceStartup;

        private void Update()
        {
            if (_scheduler == null) return;
            using (Marker.Auto())
            {
                int liveParticles = 0;
                if (presentationParticles != null)
                    for (int index = 0; index < presentationParticles.Length; index++)
                        if (presentationParticles[index] != null) liveParticles += presentationParticles[index].particleCount;
                float presentationPressure = liveParticles / (float)Mathf.Max(1, Profile.Budgets.VfxParticles);
                float memoryPressure = MemoryMegabytes / Mathf.Max(128f, Profile.Budgets.MemoryMegabytes);
                var pressure = new BudgetPressure(
                    presentationPressure,
                    simulatedDistantWork / (float)Mathf.Max(1, Profile.Budgets.ActiveChunks),
                    0.5f,
                    memoryPressure);
                Decision = _scheduler.Evaluate(in pressure);
                ApplyPresentationScale(Decision.PresentationScale);
                if (Decision.Kind == DegradationKind.RejectNewDistantWork) RejectedDistantWork++;
            }
        }

        private void Rebuild()
        {
            Profile = profileAsset != null ? profileAsset.Bake() : CapabilityProfileData.WebLab;
            CapabilityProfileData profile = Profile;
            _scheduler = new AdaptiveBudgetScheduler(in profile);
        }

        private void ApplyPresentationScale(float scale)
        {
            if (presentationParticles == null) return;
            for (int index = 0; index < presentationParticles.Length; index++)
            {
                ParticleSystem particles = presentationParticles[index];
                if (particles == null) continue;
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTimeMultiplier = scale;
            }
        }
    }
}
