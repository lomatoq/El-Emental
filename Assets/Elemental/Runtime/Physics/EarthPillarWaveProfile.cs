using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Pillar Wave Profile", fileName = "EarthPillarWaveProfile")]
    public class EarthPillarWaveProfile : ScriptableObject
    {
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

        public float FullSectorChargeSeconds => fullSectorChargeSeconds;
        public float FullPowerChargeSeconds => fullPowerChargeSeconds;
        public float ColumnRiseSeconds => columnRiseSeconds;
        public float ColumnHoldSeconds => columnHoldSeconds;
        public float ColumnRetreatSeconds => columnRetreatSeconds;
        public float ColumnWidth => columnWidth;
        public float MinimumImpulse => minimumImpulse;
        public float MaximumImpulse => maximumImpulse;
        public float ImpactRadius => impactRadius;
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
    }
}
