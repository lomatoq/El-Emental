using Elemental.Simulation.Capabilities;
using UnityEngine;

namespace Elemental.Runtime.Capabilities
{
    [CreateAssetMenu(menuName = "Elemental/Capability Profile", fileName = "CapabilityProfile")]
    public sealed class CapabilityProfileAsset : ScriptableObject
    {
        [SerializeField] private CapabilityProfileKind kind = CapabilityProfileKind.NativeHigh;
        [SerializeField, Min(1)] private int activeChunks = 512;
        [SerializeField, Min(1)] private int meshJobsPerFrame = 8;
        [SerializeField, Min(1)] private int colliderJobsPerFrame = 4;
        [SerializeField, Min(1)] private int fieldRegions = 128;
        [SerializeField, Min(1)] private int fluidProxies = 128;
        [SerializeField, Min(0)] private int vfxParticles = 12000;
        [SerializeField, Min(1)] private int ragdollBodies = 18;
        [SerializeField, Min(128)] private int memoryMegabytes = 4096;
        [SerializeField] private bool supportsAir = true;
        [SerializeField] private bool supportsCompute = true;
        [SerializeField] private bool supportsThreadedJobs = true;

        public void Configure(in CapabilityProfileData data)
        {
            kind = data.Kind; activeChunks = data.Budgets.ActiveChunks; meshJobsPerFrame = data.Budgets.MeshJobsPerFrame;
            colliderJobsPerFrame = data.Budgets.ColliderJobsPerFrame; fieldRegions = data.Budgets.FieldRegions;
            fluidProxies = data.Budgets.FluidProxies; vfxParticles = data.Budgets.VfxParticles;
            ragdollBodies = data.Budgets.RagdollBodies; memoryMegabytes = data.Budgets.MemoryMegabytes;
            supportsAir = data.SupportsAir; supportsCompute = data.SupportsCompute; supportsThreadedJobs = data.SupportsThreadedJobs;
        }

        public CapabilityProfileData Bake() => new CapabilityProfileData(
            kind, new CapabilityBudgets(activeChunks, meshJobsPerFrame, colliderJobsPerFrame, fieldRegions, fluidProxies, vfxParticles, ragdollBodies, memoryMegabytes),
            supportsAir, supportsCompute, supportsThreadedJobs);
    }
}
