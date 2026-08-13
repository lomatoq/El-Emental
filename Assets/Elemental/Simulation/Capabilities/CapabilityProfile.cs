using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Capabilities
{
    public enum CapabilityProfileKind : byte { NativeHigh = 1, NativeLow = 2, WebLab = 3 }

    public readonly struct CapabilityBudgets
    {
        public CapabilityBudgets(
            int activeChunks,
            int meshJobsPerFrame,
            int colliderJobsPerFrame,
            int fieldRegions,
            int fluidProxies,
            int vfxParticles,
            int ragdollBodies,
            int memoryMegabytes)
        {
            ActiveChunks = math.max(1, activeChunks); MeshJobsPerFrame = math.max(1, meshJobsPerFrame);
            ColliderJobsPerFrame = math.max(1, colliderJobsPerFrame); FieldRegions = math.max(1, fieldRegions);
            FluidProxies = math.max(1, fluidProxies); VfxParticles = math.max(0, vfxParticles);
            RagdollBodies = math.max(1, ragdollBodies); MemoryMegabytes = math.max(128, memoryMegabytes);
        }
        public int ActiveChunks { get; }
        public int MeshJobsPerFrame { get; }
        public int ColliderJobsPerFrame { get; }
        public int FieldRegions { get; }
        public int FluidProxies { get; }
        public int VfxParticles { get; }
        public int RagdollBodies { get; }
        public int MemoryMegabytes { get; }
    }

    public readonly struct CapabilityProfileData
    {
        public CapabilityProfileData(CapabilityProfileKind kind, CapabilityBudgets budgets, bool supportsAir, bool supportsCompute, bool supportsThreadedJobs)
        {
            Kind = kind; Budgets = budgets; SupportsAir = supportsAir;
            SupportsCompute = supportsCompute; SupportsThreadedJobs = supportsThreadedJobs;
        }
        public CapabilityProfileKind Kind { get; }
        public CapabilityBudgets Budgets { get; }
        public bool SupportsAir { get; }
        public bool SupportsCompute { get; }
        public bool SupportsThreadedJobs { get; }

        public static CapabilityProfileData NativeHigh => new CapabilityProfileData(
            CapabilityProfileKind.NativeHigh, new CapabilityBudgets(512, 8, 4, 128, 128, 12000, 18, 4096), true, true, true);
        public static CapabilityProfileData NativeLow => new CapabilityProfileData(
            CapabilityProfileKind.NativeLow, new CapabilityBudgets(192, 4, 2, 64, 48, 4500, 10, 2048), true, false, true);
        public static CapabilityProfileData WebLab => new CapabilityProfileData(
            CapabilityProfileKind.WebLab, new CapabilityBudgets(64, 2, 1, 24, 16, 1600, 6, 768), false, false, false);
    }

    public enum DegradationKind : byte
    {
        None = 0,
        ReducePresentation = 1,
        ReduceDistantSimulation = 2,
        RejectNewDistantWork = 3
    }

    public readonly struct BudgetPressure
    {
        public BudgetPressure(float presentation01, float distantSimulation01, float activeGameplay01, float memory01)
        {
            Presentation01 = math.max(0f, presentation01); DistantSimulation01 = math.max(0f, distantSimulation01);
            ActiveGameplay01 = math.max(0f, activeGameplay01); Memory01 = math.max(0f, memory01);
        }
        public float Presentation01 { get; }
        public float DistantSimulation01 { get; }
        public float ActiveGameplay01 { get; }
        public float Memory01 { get; }
    }

    public readonly struct DegradationDecision
    {
        public DegradationDecision(DegradationKind kind, float presentationScale, float distantScale, bool canonicalActiveRulesChanged, string reason)
        {
            Kind = kind; PresentationScale = math.saturate(presentationScale); DistantScale = math.saturate(distantScale);
            CanonicalActiveRulesChanged = canonicalActiveRulesChanged; Reason = reason;
        }
        public DegradationKind Kind { get; }
        public float PresentationScale { get; }
        public float DistantScale { get; }
        public bool CanonicalActiveRulesChanged { get; }
        public string Reason { get; }
    }

    public sealed class AdaptiveBudgetScheduler
    {
        private readonly CapabilityProfileData _profile;
        public AdaptiveBudgetScheduler(in CapabilityProfileData profile) => _profile = profile;
        public CapabilityProfileData Profile => _profile;

        public DegradationDecision Evaluate(in BudgetPressure pressure)
        {
            float highest = math.max(math.max(pressure.Presentation01, pressure.DistantSimulation01), math.max(pressure.ActiveGameplay01, pressure.Memory01));
            if (highest <= 1f) return new DegradationDecision(DegradationKind.None, 1f, 1f, false, "Within profile budgets.");
            if (pressure.Presentation01 > 1f || pressure.Memory01 > 1f)
            {
                float scale = math.clamp(1f / math.max(pressure.Presentation01, pressure.Memory01), 0.25f, 1f);
                return new DegradationDecision(DegradationKind.ReducePresentation, scale, 1f, false, "Presentation reduced before gameplay.");
            }
            if (pressure.DistantSimulation01 > 1f)
            {
                return new DegradationDecision(
                    DegradationKind.ReduceDistantSimulation, 1f,
                    math.clamp(1f / pressure.DistantSimulation01, 0.2f, 1f), false,
                    "Distant simulation cadence reduced outside active gameplay radius.");
            }
            return new DegradationDecision(
                DegradationKind.RejectNewDistantWork, 1f, 0.2f, false,
                "Active gameplay is protected; new distant work rejected and debt exposed.");
        }
    }
}
