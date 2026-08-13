using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Platform Profile", fileName = "EarthPlatformProfile")]
    public sealed class EarthPlatformProfile : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float minimumArea = 0.8f;
        [SerializeField, Min(0.1f)] private float maximumArea = 75f;
        [SerializeField, Range(1, 12)] private int maximumActivePlatforms = 6;
        [SerializeField, Min(0.1f)] private float minimumHeight = 0.6f;
        [SerializeField, Min(0.1f)] private float maximumHeight = 3f;
        [SerializeField, Min(0.01f)] private float topThickness = 0.45f;
        [Header("Emergence and surface fit")]
        [SerializeField, Min(0.05f)] private float emergenceSeconds = 0.52f;
        [SerializeField, Min(0f)] private float minimumEmbedDepth = 0.24f;
        [SerializeField, Min(0f)] private float visibleVoxelSafetyDepth = 0.45f;
        [SerializeField, Min(1f)] private float fractureImpulse = 1150f;
        [SerializeField, Range(6, 20)] private int fracturePieceCount = 12;
        [SerializeField, Min(0f)] private float debrisRestSeconds = 2.2f;
        [SerializeField, Min(0.05f)] private float debrisShrinkSeconds = 1.4f;
        [Header("Moving support")]
        [SerializeField, Range(0f, 0.75f)] private float riderTolerance = 0.25f;
        [SerializeField, Min(0.1f)] private float carryMaximumSpeed = 8f;
        [SerializeField, Min(0.1f)] private float carryMaximumAcceleration = 55f;
        [SerializeField, Min(0f)] private float supportGraceSeconds = 0.35f;

        public float MinimumArea => minimumArea;
        public float MaximumArea => Mathf.Max(minimumArea, maximumArea);
        public int MaximumActivePlatforms => maximumActivePlatforms;
        public float MinimumHeight => minimumHeight;
        public float MaximumHeight => Mathf.Max(minimumHeight, maximumHeight);
        public float TopThickness => topThickness;
        public float EmergenceSeconds => emergenceSeconds;
        public float MinimumEmbedDepth => minimumEmbedDepth;
        public float VisibleVoxelSafetyDepth => visibleVoxelSafetyDepth;
        public float FractureImpulse => fractureImpulse;
        public int FracturePieceCount => fracturePieceCount;
        public float DebrisRestSeconds => debrisRestSeconds;
        public float DebrisShrinkSeconds => debrisShrinkSeconds;
        public float RiderTolerance => riderTolerance;
        public float CarryMaximumSpeed => carryMaximumSpeed;
        public float CarryMaximumAcceleration => carryMaximumAcceleration;
        public float SupportGraceSeconds => supportGraceSeconds;
    }
}
