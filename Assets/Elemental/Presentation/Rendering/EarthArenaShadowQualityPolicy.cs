using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    internal readonly struct EarthArenaShadowQualitySettings
    {
        public EarthArenaShadowQualitySettings(
            ShadowQuality shadowQuality,
            int cascadeCount,
            float shadowDistance,
            ShadowResolution shadowResolution)
        {
            ShadowQuality = shadowQuality;
            CascadeCount = cascadeCount;
            ShadowDistance = shadowDistance;
            ShadowResolution = shadowResolution;
        }

        public ShadowQuality ShadowQuality { get; }
        public int CascadeCount { get; }
        public float ShadowDistance { get; }
        public ShadowResolution ShadowResolution { get; }
    }

    /// <summary>
    /// Pure policy for the legacy URP shadow controls. EarthCore disables the
    /// atlas, its cascade partitioning, and its camera distance together; custom
    /// duel and capsule shadows remain independent renderer features.
    /// </summary>
    internal static class EarthArenaShadowQualityPolicy
    {
        public static EarthArenaShadowQualitySettings Resolve(bool shadowFreeArena)
        {
            return shadowFreeArena
                ? new EarthArenaShadowQualitySettings(
                    ShadowQuality.Disable,
                    0,
                    0f,
                    ShadowResolution.High)
                : new EarthArenaShadowQualitySettings(
                    ShadowQuality.All,
                    4,
                    90f,
                    ShadowResolution.High);
        }
    }
}
