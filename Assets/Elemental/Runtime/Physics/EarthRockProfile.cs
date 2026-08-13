using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Rock Profile", fileName = "EarthRockProfile")]
    public sealed class EarthRockProfile : ScriptableObject
    {
        [Header("Impact fracture")]
        [SerializeField, Min(0f)] private float minimumShatterImpulse = 45f;
        [SerializeField, Min(0.1f)] private float shatterSpecificImpulse = 7.5f;
        [SerializeField, Range(3, 16)] private int shatterPieceCount = 9;
        [SerializeField, Min(0f)] private float shatterSpreadSpeed = 3.8f;
        [SerializeField, Min(0f)] private float craterRadiusPerImpulse = 0.0032f;
        [SerializeField, Min(0.05f)] private float minimumCraterRadius = 0.35f;
        [SerializeField, Min(0.1f)] private float maximumCraterRadius = 1.5f;

        [Header("Accretion near terrain")]
        [SerializeField, Min(0.05f)] private float accretionSurfaceDistance = 0.45f;
        [SerializeField, Min(0.1f)] private float maximumAccretionSpeed = 1.5f;
        [SerializeField, Min(0.05f)] private float accretionIntervalSeconds = 0.25f;
        [SerializeField, Min(0.001f)] private float accretionVolumePerPulse = 0.12f;
        [SerializeField, Min(0.1f)] private float maximumRadius = 2.4f;
        [SerializeField, Min(1f)] private float materialDensity = 120f;
        [SerializeField, Range(2, 8)] private int accretionChipCount = 4;

        [Header("Debris lifecycle")]
        [SerializeField, Min(0f)] private float debrisRestSeconds = 1.15f;
        [SerializeField, Min(0.05f)] private float debrisShrinkSeconds = 0.9f;

        public float MinimumShatterImpulse => minimumShatterImpulse;
        public float ShatterSpecificImpulse => shatterSpecificImpulse;
        public int ShatterPieceCount => shatterPieceCount;
        public float ShatterSpreadSpeed => shatterSpreadSpeed;
        public float CraterRadiusPerImpulse => craterRadiusPerImpulse;
        public float MinimumCraterRadius => minimumCraterRadius;
        public float MaximumCraterRadius => Mathf.Max(minimumCraterRadius, maximumCraterRadius);
        public float AccretionSurfaceDistance => accretionSurfaceDistance;
        public float MaximumAccretionSpeed => maximumAccretionSpeed;
        public float AccretionIntervalSeconds => accretionIntervalSeconds;
        public float AccretionVolumePerPulse => accretionVolumePerPulse;
        public float MaximumRadius => maximumRadius;
        public float MaterialDensity => materialDensity;
        public int AccretionChipCount => accretionChipCount;
        public float DebrisRestSeconds => debrisRestSeconds;
        public float DebrisShrinkSeconds => debrisShrinkSeconds;
    }
}
