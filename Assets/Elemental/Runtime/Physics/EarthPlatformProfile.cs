using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Platform Profile", fileName = "EarthPlatformProfile")]
    public sealed class EarthPlatformProfile : ScriptableObject, ISerializationCallbackReceiver
    {
        private const int CurrentSerializedVersion = 4;

        [SerializeField, HideInInspector] private int serializedVersion;
        [SerializeField, Min(0.1f)] private float minimumArea = 0.8f;
        [SerializeField, Min(0.1f)] private float maximumArea = 75f;
        [SerializeField, Range(1, 12)] private int maximumActivePlatforms = 6;
        [SerializeField, Min(0.1f)] private float minimumHeight = 0.6f;
        [SerializeField, Min(0.1f)] private float maximumHeight = 22f;
        [SerializeField, Min(0.1f)] private float softHeightLimit = 8f;
        [SerializeField, Range(1f, 3f)] private float heightCostExponent = 1.65f;
        [SerializeField, Range(0f, 1f)] private float aspectCost = 0.18f;
        [SerializeField, Min(0.01f)] private float topThickness = 0.45f;
        [Header("Emergence and surface fit")]
        [SerializeField, Min(0.05f)] private float emergenceSeconds = 0.52f;
        [SerializeField, Min(0f)] private float minimumEmbedDepth = 0.24f;
        [SerializeField, Min(0f)] private float visibleVoxelSafetyDepth = 0.45f;
        [SerializeField, Min(1f)] private float fractureImpulse = 1150f;
        [SerializeField, Range(28, 48)] private int fracturePieceCount = 36;
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
        public float SoftHeightLimit => Mathf.Clamp(softHeightLimit, minimumHeight, MaximumHeight);
        public float HeightCostExponent => Mathf.Max(1f, heightCostExponent);
        public float AspectCost => Mathf.Max(0f, aspectCost);
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

        public void OnBeforeSerialize()
        {
            MigrateSerializedData();
        }

        public void OnAfterDeserialize()
        {
            MigrateSerializedData();
        }

        private void OnValidate() => MigrateSerializedData();

        private void MigrateSerializedData()
        {
            if (serializedVersion >= CurrentSerializedVersion) return;
            // V1 authored profiles were hard-capped at 3 m. Preserve every other
            // tuning value while moving them into the new 8 m soft / 22 m hard policy.
            if (maximumHeight <= 3.01f) maximumHeight = 22f;
            if (softHeightLimit <= 3.01f) softHeightLimit = 8f;
            heightCostExponent = heightCostExponent <= 0f ? 1.65f : heightCostExponent;
            aspectCost = aspectCost <= 0f ? 0.18f : aspectCost;
            if (fracturePieceCount < 28) fracturePieceCount = 36;
            serializedVersion = CurrentSerializedVersion;
        }
    }
}
