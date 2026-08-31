using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public readonly struct DuelShadowDiagnosticsSnapshot
    {
        public readonly bool FeatureRequested;
        public readonly bool FrameValid;
        public readonly bool MapRendered;
        public readonly int FrameIndex;
        public readonly int Resolution;
        public readonly int PcfKernelWidth;
        public readonly int RegisteredCasterCount;
        public readonly int DrawnCasterCount;
        public readonly int RejectedCasterCount;
        public readonly int RegistryCapacityRejectCount;
        public readonly int GenerationRejectCount;
        public readonly float TexelWorldSize;
        public readonly Bounds WorldCoverage;
        public readonly Matrix4x4 WorldToShadow;

        public DuelShadowDiagnosticsSnapshot(
            bool featureRequested,
            bool frameValid,
            bool mapRendered,
            int frameIndex,
            int resolution,
            int pcfKernelWidth,
            int registeredCasterCount,
            int drawnCasterCount,
            int rejectedCasterCount,
            int registryCapacityRejectCount,
            int generationRejectCount,
            float texelWorldSize,
            Bounds worldCoverage,
            Matrix4x4 worldToShadow)
        {
            FeatureRequested = featureRequested;
            FrameValid = frameValid;
            MapRendered = mapRendered;
            FrameIndex = frameIndex;
            Resolution = resolution;
            PcfKernelWidth = pcfKernelWidth;
            RegisteredCasterCount = registeredCasterCount;
            DrawnCasterCount = drawnCasterCount;
            RejectedCasterCount = rejectedCasterCount;
            RegistryCapacityRejectCount = registryCapacityRejectCount;
            GenerationRejectCount = generationRejectCount;
            TexelWorldSize = texelWorldSize;
            WorldCoverage = worldCoverage;
            WorldToShadow = worldToShadow;
        }

        public DuelShadowDiagnosticsSnapshot WithRenderedMap()
        {
            return new DuelShadowDiagnosticsSnapshot(
                FeatureRequested,
                FrameValid,
                true,
                FrameIndex,
                Resolution,
                PcfKernelWidth,
                RegisteredCasterCount,
                DrawnCasterCount,
                RejectedCasterCount,
                RegistryCapacityRejectCount,
                GenerationRejectCount,
                TexelWorldSize,
                WorldCoverage,
                WorldToShadow);
        }
    }

    [DisallowMultipleComponent]
    public sealed class DuelShadowDiagnostics : MonoBehaviour
    {
        private static DuelShadowDiagnosticsSnapshot s_Current;

        public static DuelShadowDiagnosticsSnapshot Current => s_Current;

        internal static void Publish(in DuelShadowDiagnosticsSnapshot snapshot)
        {
            s_Current = snapshot;
        }

        internal static void MarkMapRendered()
        {
            s_Current = s_Current.WithRenderedMap();
        }

        internal static void PublishDisabled(bool requested)
        {
            DuelShadowCasterRegistry registry = DuelShadowCasterRegistry.Shared;
            s_Current = new DuelShadowDiagnosticsSnapshot(
                requested,
                false,
                false,
                Time.frameCount,
                0,
                0,
                registry.Count,
                0,
                0,
                registry.CapacityRejectCount,
                registry.GenerationRejectCount,
                0f,
                default,
                Matrix4x4.identity);
        }
    }
}
