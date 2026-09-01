using System;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public enum CapsuleContactShadowQualityTier
    {
        Low = 0,
        Balanced = 1,
        High = 2
    }

    public enum CapsuleContactShadowDebugView
    {
        None = 0,
        ShadowOnly = 1
    }

    public readonly struct CapsuleContactShadowQuality
    {
        public readonly int MaximumCapsuleCount;

        public CapsuleContactShadowQuality(int maximumCapsuleCount)
        {
            MaximumCapsuleCount = Mathf.Clamp(
                maximumCapsuleCount,
                1,
                CapsuleShadowBuffer.MaximumProxyCount);
        }

        public static CapsuleContactShadowQuality Resolve(
            CapsuleContactShadowQualityTier tier)
        {
            switch (tier)
            {
                case CapsuleContactShadowQualityTier.Low:
                    return new CapsuleContactShadowQuality(12);
                case CapsuleContactShadowQualityTier.High:
                    return new CapsuleContactShadowQuality(32);
                default:
                    return new CapsuleContactShadowQuality(20);
            }
        }
    }

    public readonly struct CapsuleContactShadowRuntimeSettings
    {
        public readonly CapsuleContactShadowQuality Quality;
        public readonly int MaximumCasterCount;
        public readonly float ShadowStrength;
        public readonly float MaximumContactDistance;
        public readonly float SurfaceBias;
        public readonly float NormalBias;
        public readonly float MinimumHeroRockDiameter;
        public readonly float MinimumActiveFragmentDiameter;
        public readonly CapsuleContactShadowDebugView DebugView;

        public CapsuleContactShadowRuntimeSettings(
            CapsuleContactShadowQuality quality,
            int maximumCasterCount,
            float shadowStrength,
            float maximumContactDistance,
            float surfaceBias,
            float normalBias,
            float minimumHeroRockDiameter,
            float minimumActiveFragmentDiameter,
            CapsuleContactShadowDebugView debugView)
        {
            Quality = quality;
            MaximumCasterCount = Mathf.Clamp(
                maximumCasterCount,
                1,
                CapsuleShadowBuffer.MaximumCasterCount);
            ShadowStrength = Mathf.Clamp01(shadowStrength);
            MaximumContactDistance = Mathf.Max(0.05f, maximumContactDistance);
            SurfaceBias = Mathf.Clamp(surfaceBias, 0.001f, MaximumContactDistance * 0.25f);
            NormalBias = Mathf.Clamp(normalBias, 0f, MaximumContactDistance * 0.25f);
            MinimumHeroRockDiameter = Mathf.Max(0f, minimumHeroRockDiameter);
            MinimumActiveFragmentDiameter = Mathf.Max(0f, minimumActiveFragmentDiameter);
            DebugView = debugView;
        }
    }

    [Serializable]
    public sealed class CapsuleContactShadowSettings
    {
        [SerializeField] private CapsuleContactShadowQualityTier qualityTier =
            CapsuleContactShadowQualityTier.Balanced;
        [SerializeField, Range(1, CapsuleShadowBuffer.MaximumCasterCount)]
        private int maximumCasterCount = 12;
        [SerializeField, Range(0f, 1f)] private float shadowStrength = 0.58f;
        [SerializeField, Min(0.05f)] private float maximumContactDistance = 1.25f;
        [SerializeField, Min(0.001f)] private float surfaceBias = 0.025f;
        [SerializeField, Min(0f)] private float normalBias = 0.02f;
        [SerializeField, Min(0f)] private float minimumHeroRockDiameter = 0.4f;
        [SerializeField, Min(0f)] private float minimumActiveFragmentDiameter = 0.75f;
        [SerializeField] private CapsuleContactShadowDebugView debugView =
            CapsuleContactShadowDebugView.None;

        public CapsuleContactShadowQualityTier QualityTier => qualityTier;

        public CapsuleContactShadowRuntimeSettings CreateRuntimeSettings()
        {
            return new CapsuleContactShadowRuntimeSettings(
                CapsuleContactShadowQuality.Resolve(qualityTier),
                maximumCasterCount,
                shadowStrength,
                maximumContactDistance,
                surfaceBias,
                normalBias,
                minimumHeroRockDiameter,
                minimumActiveFragmentDiameter,
                debugView);
        }
    }
}
