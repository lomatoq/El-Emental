using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    // Phase durations are shared; authored visual curves are evaluated at render time.
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Pillar Wave Profile", fileName = "EarthPillarWaveProfile")]
    public class EarthPillarWaveProfile : ScriptableObject
    {
        [Header("Motion mode")]
        [SerializeField] private WaveMotionMode motionMode = WaveMotionMode.Legacy;
        [SerializeField, Min(0.1f)] private float fullSectorChargeSeconds = 1.4f;
        [SerializeField, Min(0.1f)] private float fullPowerChargeSeconds = 1.1f;
        [SerializeField, Min(0.05f)] private float columnRiseSeconds = 0.30f;
        [SerializeField, Min(0f)] private float columnHoldSeconds = 0.04f;
        [SerializeField, Min(0.05f)] private float columnRetreatSeconds = 0.32f;
        [SerializeField, Min(0.1f)] private float columnWidth = 1.15f;
        [SerializeField, Min(0f)] private float minimumImpulse = 85f;
        [SerializeField, Min(0f)] private float maximumImpulse = 420f;
        [SerializeField, Min(0.1f)] private float impactRadius = 1.05f;
        [Header("Moving crest")]
        [Tooltip("Fraction of each pillar/cell height buried below its selected ground surface.")]
        [SerializeField, Range(0f, 0.5f)] private float foundationBurialRatio = 0.20f;
        [SerializeField, Range(5, 9)] private int minimumRows = 6;
        [SerializeField, Range(5, 9)] private int maximumRows = 8;
        [SerializeField, Min(0.5f)] private float minimumDistance = 2.0f;
        [SerializeField, Min(1f)] private float maximumDistance = 11.2f;
        [SerializeField, Min(0.2f)] private float minimumWidth = 0.64f;
        [SerializeField, Min(0.2f)] private float maximumWidth = 1.28f;
        [SerializeField, Min(0.1f)] private float minimumHeight = 0.28f;
        [SerializeField, Min(0.1f)] private float crestHeight = 3.4f;
        [SerializeField, Min(0.1f)] private float waveSpeed = 5.4f;
        [SerializeField, Min(0.03f)] private float crestHoldSeconds = 0.04f;
        [Tooltip("Small overlap between adjacent Voronoi-like ground cells. Prevents light leaks while the crest moves.")]
        [SerializeField, Range(0.02f, 0.16f)] private float cellOverlapRatio = 0.07f;

        [Header("Wave animation: anticipation, rise, settle, hold, retreat")]
        [SerializeField, Range(0.01f, .5f)] private float precompressionSeconds = 0.055f;
        [SerializeField, Range(0f, .15f)] private float precompressionDepth01 = 0.025f;
        [SerializeField, Range(.05f, 2f)] private float premiumRiseSeconds = 0.22f;
        [SerializeField, Range(0f, .25f)] private float premiumOvershoot01 = 0.045f;
        [SerializeField, Range(.01f, 1f)] private float premiumSettleSeconds = 0.145f;
        [SerializeField, Range(0f, 2f)] private float premiumHoldSeconds = 0.055f;
        [SerializeField, Range(.05f, 3f)] private float premiumRetreatSeconds = 0.30f;
        [SerializeField, Range(0f, 20f)] private float premiumTiltDegrees = 6f;
        [SerializeField, Range(.1f, 12f)] private float premiumSettleFrequencyHz = 5f;
        [SerializeField, Range(.1f, 3f)] private float premiumSettleDamping = 0.73f;
        [SerializeField, Range(0f, .3f)] private float seededVariation01 = 0.07f;
        [Header("Optional stone tremor (zero amplitude disables it)")]
        [SerializeField, Range(0f, .1f)] private float tremorDistance = .006f;
        [SerializeField, Range(0f, 4f)] private float tremorAngle = .2f;
        [SerializeField, Range(1f, 20f)] private float tremorFrequency = 8f;
        public float TremorDistance => motionMode == WaveMotionMode.PremiumVisual ? 0f : tremorDistance;
        public float TremorAngle => motionMode == WaveMotionMode.PremiumVisual ? 0f : tremorAngle;
        public float TremorFrequency => tremorFrequency;

        [SerializeField] private AnimationCurve anticipationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, .025f);
        [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, .025f, 1f, 1.045f);
        [SerializeField] private AnimationCurve settleCurve = AnimationCurve.EaseInOut(0f, 1.045f, 1f, 1f);
        [SerializeField] private AnimationCurve holdCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        [SerializeField] private AnimationCurve retreatCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private AnimationCurve tiltCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.35f, 1f), new Keyframe(.7f, -.15f), new Keyframe(1f, 0f));

        public EarthWaveAnimationTiming AnimationTiming => new EarthWaveAnimationTiming(
            precompressionSeconds, premiumRiseSeconds, premiumSettleSeconds, premiumHoldSeconds, premiumRetreatSeconds);

        public EarthPillarWaveVisualSample EvaluateVisualMotion(float time, uint seed)
        {
            var tuning = VisualTuning;
            if (motionMode != WaveMotionMode.PremiumVisual)
                return EarthPillarWaveSolver.EvaluateVisualMotion(time, columnRiseSeconds, columnHoldSeconds,
                    columnRetreatSeconds, motionMode, in tuning, seed);
            var timing = AnimationTiming;
            if (time <= -timing.Anticipation || time >= timing.Duration)
                return new EarthPillarWaveVisualSample(0f, 1f, 0f, 0f);
            int phase = timing.Locate(time, out float progress);
            AnimationCurve curve = phase == 0 ? anticipationCurve : phase == 1 ? riseCurve :
                phase == 2 ? settleCurve : phase == 3 ? holdCurve : retreatCurve;
            float start = phase == 0 ? 0f : phase == 1 ? .025f : phase == 2 ? 1.045f : 1f;
            float end = phase == 0 ? .025f : phase == 1 ? 1.045f : phase == 4 ? 0f : 1f;
            float height = EvaluateAnchored(curve, progress, start, end);
            float tiltTime = Mathf.Clamp01((time + timing.Anticipation) / timing.TotalDuration);
            float tilt = EvaluateAnchored(tiltCurve, tiltTime, 0f, 0f);
            return new EarthPillarWaveVisualSample(Mathf.Clamp(height, 0f, 1.25f), 1f,
                Mathf.Clamp(tilt, -1f, 1f) * Mathf.Clamp(premiumTiltDegrees, 0f, 20f), 0f);
        }

        // Keep neighbouring phases joined even when a curve's end keys are dragged.
        private static float EvaluateAnchored(AnimationCurve curve, float time, float start, float end)
        {
            float blend = time * time * time * (time * (time * 6f - 15f) + 10f);
            if (curve == null || curve.length == 0) return Mathf.Lerp(start, end, blend);
            float value = curve.Evaluate(time) + Mathf.Lerp(start - curve.Evaluate(0f), end - curve.Evaluate(1f), blend);
            return float.IsFinite(value) ? value : Mathf.Lerp(start, end, blend);
        }

        public WaveMotionMode MotionMode => motionMode;
        public float FoundationBurialRatio => foundationBurialRatio;
        public float FullSectorChargeSeconds => fullSectorChargeSeconds;
        public float FullPowerChargeSeconds => fullPowerChargeSeconds;
        public float ColumnRiseSeconds => motionMode == WaveMotionMode.PremiumVisual ? AnimationTiming.Rise : columnRiseSeconds;
        public float ColumnHoldSeconds => motionMode == WaveMotionMode.PremiumVisual ? AnimationTiming.Settle + AnimationTiming.Hold : columnHoldSeconds;
        public float ColumnRetreatSeconds => motionMode == WaveMotionMode.PremiumVisual ? AnimationTiming.Retreat : columnRetreatSeconds;
        public float ColumnWidth => columnWidth;
        public float MinimumImpulse => minimumImpulse;
        public float MaximumImpulse => maximumImpulse;
        public float ImpactRadius => impactRadius;
        public EarthPillarWaveVisualTuning VisualTuning => new EarthPillarWaveVisualTuning(
            precompressionSeconds,
            precompressionDepth01,
            premiumRiseSeconds,
            premiumOvershoot01,
            premiumSettleSeconds,
            premiumHoldSeconds,
            premiumRetreatSeconds,
            premiumTiltDegrees,
            premiumSettleFrequencyHz,
            premiumSettleDamping,
            seededVariation01);
        public EarthPillarWaveTuning Tuning => new EarthPillarWaveTuning(
            minimumRows,
            maximumRows,
            minimumDistance,
            maximumDistance,
            minimumWidth,
            maximumWidth,
            minimumHeight,
            crestHeight,
            waveSpeed,
            crestHoldSeconds,
            cellOverlapRatio);

        public void ConfigureMotionMode(WaveMotionMode mode) => motionMode = mode;
    }
}
