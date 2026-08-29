using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    // Gameplay motion remains legacy; premium motion is presentation-only.
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

        [Header("Premium visual-only motion")]
        [SerializeField, Range(0.04f, 0.08f)] private float precompressionSeconds = 0.055f;
        [SerializeField, Range(0.02f, 0.03f)] private float precompressionDepth01 = 0.025f;
        [SerializeField, Range(0.20f, 0.24f)] private float premiumRiseSeconds = 0.22f;
        [SerializeField, Range(0.035f, 0.05f)] private float premiumOvershoot01 = 0.045f;
        [SerializeField, Range(0.12f, 0.17f)] private float premiumSettleSeconds = 0.145f;
        [SerializeField, Range(0.04f, 0.07f)] private float premiumHoldSeconds = 0.055f;
        [SerializeField, Range(0.26f, 0.34f)] private float premiumRetreatSeconds = 0.30f;
        [SerializeField, Range(5f, 7f)] private float premiumTiltDegrees = 6f;
        [SerializeField, Range(4.5f, 5.5f)] private float premiumSettleFrequencyHz = 5f;
        [SerializeField, Range(0.68f, 0.78f)] private float premiumSettleDamping = 0.73f;
        [SerializeField, Range(0f, 0.07f)] private float seededVariation01 = 0.07f;

        public WaveMotionMode MotionMode => motionMode;
        public float FullSectorChargeSeconds => fullSectorChargeSeconds;
        public float FullPowerChargeSeconds => fullPowerChargeSeconds;
        public float ColumnRiseSeconds => columnRiseSeconds;
        public float ColumnHoldSeconds => columnHoldSeconds;
        public float ColumnRetreatSeconds => columnRetreatSeconds;
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
