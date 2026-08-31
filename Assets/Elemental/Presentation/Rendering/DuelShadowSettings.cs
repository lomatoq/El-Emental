using System;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public enum DuelShadowQualityTier
    {
        Low = 0,
        Balanced = 1,
        Cinematic = 2
    }

    public enum DuelShadowDebugView
    {
        None = 0,
        ShadowOnly = 1
    }

    public readonly struct DuelShadowQuality
    {
        public readonly int Resolution;
        public readonly int PcfKernelWidth;

        public DuelShadowQuality(int resolution, int pcfKernelWidth)
        {
            Resolution = resolution;
            PcfKernelWidth = pcfKernelWidth;
        }

        public static DuelShadowQuality Resolve(DuelShadowQualityTier tier)
        {
            switch (tier)
            {
                case DuelShadowQualityTier.Low:
                    return new DuelShadowQuality(1024, 3);
                case DuelShadowQualityTier.Cinematic:
                    return new DuelShadowQuality(4096, 7);
                default:
                    return new DuelShadowQuality(2048, 5);
            }
        }
    }

    public readonly struct DuelShadowClassificationSettings
    {
        public readonly float MinimumHeroRockDiameter;
        public readonly float MinimumActiveFragmentDiameter;

        public DuelShadowClassificationSettings(
            float minimumHeroRockDiameter,
            float minimumActiveFragmentDiameter)
        {
            MinimumHeroRockDiameter = Mathf.Max(0f, minimumHeroRockDiameter);
            MinimumActiveFragmentDiameter = Mathf.Max(0f, minimumActiveFragmentDiameter);
        }
    }

    public readonly struct DuelShadowStabilizationSettings
    {
        public readonly float MinimumCoverageDiameter;
        public readonly float MaximumCoverageDiameter;
        public readonly float WorldPadding;
        public readonly float DepthPadding;
        public readonly float CoverageQuantum;
        public readonly float DepthQuantum;
        public readonly float CenterHysteresis;
        public readonly float CoverageContractionHysteresis;
        public readonly float DepthContractionHysteresis;

        public DuelShadowStabilizationSettings(
            float minimumCoverageDiameter,
            float maximumCoverageDiameter,
            float worldPadding,
            float depthPadding,
            float coverageQuantum,
            float depthQuantum,
            float centerHysteresis,
            float coverageContractionHysteresis,
            float depthContractionHysteresis)
        {
            MinimumCoverageDiameter = Mathf.Max(0.25f, minimumCoverageDiameter);
            MaximumCoverageDiameter = Mathf.Max(
                MinimumCoverageDiameter,
                maximumCoverageDiameter);
            WorldPadding = Mathf.Max(0f, worldPadding);
            DepthPadding = Mathf.Max(0.05f, depthPadding);
            CoverageQuantum = Mathf.Max(0.01f, coverageQuantum);
            DepthQuantum = Mathf.Max(0.01f, depthQuantum);
            CenterHysteresis = Mathf.Max(0f, centerHysteresis);
            CoverageContractionHysteresis = Mathf.Max(
                CoverageQuantum,
                coverageContractionHysteresis);
            DepthContractionHysteresis = Mathf.Max(
                DepthQuantum,
                depthContractionHysteresis);
        }
    }

    public readonly struct DuelShadowRuntimeSettings
    {
        public readonly DuelShadowQuality Quality;
        public readonly DuelShadowClassificationSettings Classification;
        public readonly DuelShadowStabilizationSettings Stabilization;
        public readonly int MaximumCasterCount;
        public readonly float ShadowStrength;
        public readonly float ConstantDepthBias;
        public readonly float SlopeDepthBias;
        public readonly DuelShadowDebugView DebugView;

        public DuelShadowRuntimeSettings(
            DuelShadowQuality quality,
            DuelShadowClassificationSettings classification,
            DuelShadowStabilizationSettings stabilization,
            int maximumCasterCount,
            float shadowStrength,
            float constantDepthBias,
            float slopeDepthBias,
            DuelShadowDebugView debugView)
        {
            Quality = quality;
            Classification = classification;
            Stabilization = stabilization;
            MaximumCasterCount = Mathf.Clamp(
                maximumCasterCount,
                1,
                DuelShadowCasterRegistry.MaximumCapacity);
            ShadowStrength = Mathf.Clamp01(shadowStrength);
            ConstantDepthBias = Mathf.Max(0f, constantDepthBias);
            SlopeDepthBias = Mathf.Max(0f, slopeDepthBias);
            DebugView = debugView;
        }
    }

    [Serializable]
    public sealed class DuelShadowSettings
    {
        [SerializeField] private DuelShadowQualityTier qualityTier =
            DuelShadowQualityTier.Balanced;
        [SerializeField, Range(1, DuelShadowCasterRegistry.MaximumCapacity)]
        private int maximumCasterCount = 160;
        [SerializeField, Min(0f)] private float minimumHeroRockDiameter = 0.45f;
        [SerializeField, Min(0f)] private float minimumActiveFragmentDiameter = 0.8f;
        [SerializeField, Min(0.25f)] private float minimumCoverageDiameter = 12f;
        [SerializeField, Min(0.25f)] private float maximumCoverageDiameter = 160f;
        [SerializeField, Min(0f)] private float worldPadding = 1.5f;
        [SerializeField, Min(0.05f)] private float depthPadding = 4f;
        [SerializeField, Min(0.01f)] private float coverageQuantum = 0.5f;
        [SerializeField, Min(0.01f)] private float depthQuantum = 1f;
        [SerializeField, Min(0f)] private float centerHysteresis = 0.2f;
        [SerializeField, Min(0f)] private float coverageContractionHysteresis = 1f;
        [SerializeField, Min(0f)] private float depthContractionHysteresis = 1.5f;
        [SerializeField, Range(0f, 1f)] private float shadowStrength = 0.88f;
        [SerializeField, Min(0f)] private float constantDepthBias = 0.8f;
        [SerializeField, Min(0f)] private float slopeDepthBias = 1.8f;
        [SerializeField] private DuelShadowDebugView debugView = DuelShadowDebugView.None;

        public DuelShadowQualityTier QualityTier => qualityTier;

        public DuelShadowRuntimeSettings CreateRuntimeSettings()
        {
            return new DuelShadowRuntimeSettings(
                DuelShadowQuality.Resolve(qualityTier),
                new DuelShadowClassificationSettings(
                    minimumHeroRockDiameter,
                    minimumActiveFragmentDiameter),
                new DuelShadowStabilizationSettings(
                    minimumCoverageDiameter,
                    maximumCoverageDiameter,
                    worldPadding,
                    depthPadding,
                    coverageQuantum,
                    depthQuantum,
                    centerHysteresis,
                    coverageContractionHysteresis,
                    depthContractionHysteresis),
                maximumCasterCount,
                shadowStrength,
                constantDepthBias,
                slopeDepthBias,
                debugView);
        }
    }
}
