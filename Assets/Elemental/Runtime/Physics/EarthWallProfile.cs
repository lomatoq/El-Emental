using UnityEngine;
using UnityEngine.Serialization;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Wall Profile", fileName = "EarthWallProfile")]
    public sealed class EarthWallProfile : ScriptableObject
    {
        [Header("Lifecycle")]
        [SerializeField, Min(0.05f)] private float minimumEmergenceSeconds = 0.36f;
        [SerializeField, Min(0.05f)] private float maximumEmergenceSeconds = 0.92f;
        [FormerlySerializedAs("automaticCollapseDelaySeconds")]
        [Tooltip("Seconds before an undamaged wall begins cracking. Zero disables spontaneous cracking (MVP default).")]
        [SerializeField, Min(0f)] private float automaticCrackDelaySeconds;
        [SerializeField, Min(0f)] private float fractureWaveSeconds = 0.26f;
        [SerializeField, Min(0.05f)] private float cohesionDecaySeconds = 2.8f;
        [SerializeField, Min(0f)] private float debrisRestSeconds = 1.35f;
        [SerializeField, Min(0.05f)] private float debrisShrinkSeconds = 1.25f;
        [Tooltip("Cleanup-only compatibility path. Keep disabled for repairable structural pieces.")]
        [SerializeField] private bool shrinkDetachedStructuralPieces;

        [Header("Physical response")]
        [SerializeField, Min(0f)] private float minimumRockImpactImpulse = 55f;
        [SerializeField, Min(0f)] private float wallSlideDrag = 0.72f;
        [SerializeField, Min(0.1f)] private float maximumSlideSpeed = 14f;
        [SerializeField, Min(0.001f)] private float cohesionImpulsePerMass = 0.12f;
        [SerializeField, Min(0f)] private float impactDamageMultiplier = 0.92f;
        [SerializeField, Min(0f)] private float excessImpulseRelease = 0.18f;
        [SerializeField, Min(0f)] private float foundationStrengthMultiplier = 1.45f;
        [SerializeField, Min(0f)] private float planetaryDebrisAcceleration = 11.5f;
        [SerializeField, Min(0f)] private float minimumChordEmbedDepth = 0.42f;
        [SerializeField, Min(0f)] private float surfaceTolerance = 0.06f;
        [Tooltip("Extra inward margin for the visible noisy voxel surface, beyond the analytic sphere chord solve.")]
        [SerializeField, Min(0f)] private float visibleVoxelSafetyDepth = 0.55f;
        [SerializeField, Min(0f)] private float magicFieldSlideDrag = 0.16f;

        public float MinimumEmergenceSeconds => minimumEmergenceSeconds;
        public float MaximumEmergenceSeconds => Mathf.Max(minimumEmergenceSeconds, maximumEmergenceSeconds);
        public float AutomaticCrackDelaySeconds => automaticCrackDelaySeconds;
        public float AutomaticCollapseDelaySeconds => automaticCrackDelaySeconds;
        public float FractureWaveSeconds => fractureWaveSeconds;
        public float CohesionDecaySeconds => cohesionDecaySeconds;
        public float DebrisRestSeconds => debrisRestSeconds;
        public float DebrisShrinkSeconds => debrisShrinkSeconds;
        public bool ShrinkDetachedStructuralPieces => shrinkDetachedStructuralPieces;
        public float MinimumRockImpactImpulse => minimumRockImpactImpulse;
        public float WallSlideDrag => wallSlideDrag;
        public float MaximumSlideSpeed => maximumSlideSpeed;
        public float CohesionImpulsePerMass => cohesionImpulsePerMass;
        public float ImpactDamageMultiplier => impactDamageMultiplier;
        public float ExcessImpulseRelease => excessImpulseRelease;
        public float FoundationStrengthMultiplier => foundationStrengthMultiplier;
        public float PlanetaryDebrisAcceleration => planetaryDebrisAcceleration;
        public float MinimumChordEmbedDepth => minimumChordEmbedDepth;
        public float SurfaceTolerance => surfaceTolerance;
        public float VisibleVoxelSafetyDepth => visibleVoxelSafetyDepth;
        public float MagicFieldSlideDrag => magicFieldSlideDrag;
    }
}
