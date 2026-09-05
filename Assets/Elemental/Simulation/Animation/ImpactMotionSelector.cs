using Unity.Mathematics;

namespace Elemental.Simulation.Animation
{
    public enum ImpactMotionLane : byte
    {
        None,
        LightAdditive,
        MediumStagger,
        HeavyRagdoll
    }

    public readonly struct ImpactMotionContext
    {
        public readonly float Severity01;
        public readonly float3 LocalDirection;
        public readonly bool HasStableSupport;
        public readonly bool RagdollRequested;

        public ImpactMotionContext(
            float severity01,
            float3 localDirection,
            bool hasStableSupport,
            bool ragdollRequested)
        {
            Severity01 = math.saturate(severity01);
            LocalDirection = math.normalizesafe(localDirection, new float3(0f, 0f, -1f));
            HasStableSupport = hasStableSupport;
            RagdollRequested = ragdollRequested;
        }
    }

    public static class ImpactMotionSelector
    {
        public static ImpactMotionLane Select(in ImpactMotionContext context)
        {
            if (context.RagdollRequested || context.Severity01 >= 0.78f)
                return ImpactMotionLane.HeavyRagdoll;
            if (context.HasStableSupport && context.Severity01 >= 0.32f)
                return ImpactMotionLane.MediumStagger;
            return context.Severity01 > 0.02f
                ? ImpactMotionLane.LightAdditive
                : ImpactMotionLane.None;
        }
    }
}
