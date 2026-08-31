using Elemental.Simulation.Capabilities;
using Elemental.Simulation.Characters;
using Unity.Mathematics;

namespace Elemental.Simulation.Rendering
{
    public enum EarthDepthOfFieldTier : byte
    {
        Off = 0,
        Gaussian = 1,
        Bokeh = 2
    }

    public readonly struct EarthVisualClarityInput
    {
        public EarthVisualClarityInput(
            EarthCameraState cameraState,
            CapabilityProfileKind capability,
            float charge01,
            float focusDistance,
            float daylight01,
            bool depthOfFieldWasActive)
        {
            CameraState = cameraState;
            Capability = capability;
            Charge01 = math.saturate(charge01);
            FocusDistance = math.max(0.1f, focusDistance);
            Daylight01 = math.saturate(daylight01);
            DepthOfFieldWasActive = depthOfFieldWasActive;
        }

        public EarthCameraState CameraState { get; }
        public CapabilityProfileKind Capability { get; }
        public float Charge01 { get; }
        public float FocusDistance { get; }
        public float Daylight01 { get; }
        public bool DepthOfFieldWasActive { get; }
    }

    public readonly struct EarthVisualClarityOutput
    {
        public EarthVisualClarityOutput(
            EarthDepthOfFieldTier depthOfFieldTier,
            float focusDistance,
            float aperture,
            float focalLength,
            float gaussianStart,
            float gaussianEnd,
            float gaussianMaxRadius,
            float bloomIntensity,
            float vignetteIntensity,
            float dustRate,
            int dustCapacity)
        {
            DepthOfFieldTier = depthOfFieldTier;
            FocusDistance = focusDistance;
            Aperture = aperture;
            FocalLength = focalLength;
            GaussianStart = gaussianStart;
            GaussianEnd = gaussianEnd;
            GaussianMaxRadius = gaussianMaxRadius;
            BloomIntensity = bloomIntensity;
            VignetteIntensity = vignetteIntensity;
            DustRate = dustRate;
            DustCapacity = dustCapacity;
        }

        public EarthDepthOfFieldTier DepthOfFieldTier { get; }
        public bool DepthOfFieldActive => DepthOfFieldTier != EarthDepthOfFieldTier.Off;
        public float FocusDistance { get; }
        public float Aperture { get; }
        public float FocalLength { get; }
        public float GaussianStart { get; }
        public float GaussianEnd { get; }
        public float GaussianMaxRadius { get; }
        public float BloomIntensity { get; }
        public float VignetteIntensity { get; }
        public float DustRate { get; }
        public int DustCapacity { get; }
    }

    /// <summary>
    /// Pure presentation policy. Native gameplay keeps a bounded lens active
    /// through locomotion, jumps, impacts and recovery; the presentation layer
    /// expands one sharp envelope around both fighters. Web remains the only
    /// capability tier without a depth-of-field backend.
    /// </summary>
    public static class EarthVisualClaritySolver
    {
        public static EarthVisualClarityOutput Solve(in EarthVisualClarityInput input)
        {
            float stateFocus = ResolveStateFocus(input.CameraState);
            float focusIntent = math.max(stateFocus, input.Charge01);
            bool deliberateFocus = focusIntent >= 0.24f;
            EarthDepthOfFieldTier tier;
            if (input.Capability == CapabilityProfileKind.WebLab)
                tier = EarthDepthOfFieldTier.Off;
            else if (input.Capability == CapabilityProfileKind.NativeHigh)
                tier = EarthDepthOfFieldTier.Bokeh;
            else
                tier = EarthDepthOfFieldTier.Gaussian;

            float focusDistance = math.clamp(input.FocusDistance, 1.25f, 36f);
            float lens = deliberateFocus
                ? math.saturate((focusIntent - 0.18f) / 0.82f)
                : 0.22f;
            float aperture = tier == EarthDepthOfFieldTier.Bokeh
                ? 5.6f
                : math.lerp(6.8f, 3.4f, lens);
            float focalLength = tier == EarthDepthOfFieldTier.Bokeh
                ? 50f
                : math.lerp(52f, 65f, lens);
            float gaussianStart = focusDistance + math.lerp(0.18f, 0.09f, lens);
            float gaussianEnd = gaussianStart + math.lerp(1.4f, 0.95f, lens);
            float gaussianMaxRadius = input.Capability == CapabilityProfileKind.NativeHigh
                ? 1.5f
                : 1.25f;

            float bloom = math.clamp(input.Charge01 * 0.07f, 0f, 0.07f);
            float vignette = math.clamp(0.09f + lens * 0.05f, 0.09f, 0.14f);
            int dustCapacity;
            float baseDustRate;
            switch (input.Capability)
            {
                case CapabilityProfileKind.NativeHigh:
                    dustCapacity = 64;
                    baseDustRate = 18f;
                    break;
                case CapabilityProfileKind.NativeLow:
                    dustCapacity = 32;
                    baseDustRate = 8f;
                    break;
                default:
                    dustCapacity = 0;
                    baseDustRate = 0f;
                    break;
            }
            float dustRate = baseDustRate * input.Daylight01 * math.lerp(0.78f, 1f, lens);

            return new EarthVisualClarityOutput(
                tier,
                focusDistance,
                aperture,
                focalLength,
                gaussianStart,
                gaussianEnd,
                gaussianMaxRadius,
                bloom,
                vignette,
                dustRate,
                dustCapacity);
        }

        private static float ResolveStateFocus(EarthCameraState state)
        {
            return state switch
            {
                EarthCameraState.BendLight => 0.34f,
                EarthCameraState.BendHeavy => 0.78f,
                EarthCameraState.DrawStructure => 0.20f,
                EarthCameraState.HoldMass => 0.72f,
                _ => 0f
            };
        }
    }
}
