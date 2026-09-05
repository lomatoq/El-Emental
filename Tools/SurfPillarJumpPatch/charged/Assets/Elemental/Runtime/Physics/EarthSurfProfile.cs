using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Surf Profile", fileName = "EarthSurfProfile")]
    public sealed class EarthSurfProfile : ScriptableObject
    {
        [SerializeField, Range(0.08f, 0.35f)] private float emergenceSeconds = 0.16f;
        [SerializeField, Range(0.4f, 3f)] private float accelerationSeconds = 1.2f;
        [SerializeField, Range(2f, 8f)] private float minimumSpeed = 4f;
        [SerializeField, Range(8f, 18f)] private float maximumSpeed = 13f;
        [SerializeField, Range(0.2f, 0.8f)] private float releaseSeconds = 0.45f;
        [SerializeField, Range(1f, 3f)] private float speedExponent = 1.65f;
        [SerializeField, Range(800f, 5000f)] private float noseImpactImpulse = 2400f;
        [SerializeField, Range(30f, 180f)] private float carryAcceleration = 95f;
        [Header("Plough geometry")]
        [SerializeField, Range(1.6f, 3.2f)] private float boardWidth = 2.35f;
        [SerializeField, Range(2.8f, 5f)] private float boardLength = 3.9f;
        [SerializeField, Range(0.5f, 1.2f)] private float noseHeight = 0.82f;
        [Header("Loose stone assembly and debris")]
        [SerializeField, Min(0.05f)] private float assemblySeconds = 0.30f;
        [SerializeField, Min(0.1f)] private float releaseDebrisSeconds = 1f;
        [SerializeField, Min(0f)] private float trailChipsPerMeter = 5f;
        [SerializeField, Range(0f, 75f)] private float noseSpreadDegrees = 35f;
        [SerializeField] private bool ribbonEnabled;
        [Header("Dust and stones around the board")]
        [SerializeField, Min(0f)] private float wakeDustPerMeter = 48f;
        [SerializeField, Range(0f, 5f)] private float wakeChipMultiplier = 2.4f;
        [SerializeField, Range(0f, 1f)] private float wakeFrontShare = 0.45f;
        [Header("Pillar jump trick")]
        [Tooltip("Forward lean of the surf launch pillar after a short Space tap.")]
        [SerializeField, Range(5f, 35f)] private float pillarJumpMinimumTiltDegrees = 18f;
        [Tooltip("Forward lean of the surf launch pillar at full Space charge.")]
        [SerializeField, Range(10f, 40f)] private float pillarJumpMaximumTiltDegrees = 28f;
        [Tooltip("Extra outward launch speed applied to every released board stone.")]
        [SerializeField, Range(0.5f, 8f)] private float pillarJumpScatterSpeed = 3.2f;

        public EarthSurfProfileData Data => new EarthSurfProfileData(
            emergenceSeconds, accelerationSeconds, minimumSpeed, maximumSpeed, releaseSeconds, speedExponent);
        public float NoseImpactImpulse => noseImpactImpulse;
        public float CarryAcceleration => carryAcceleration;
        public float BoardWidth => boardWidth;
        public float BoardLength => boardLength;
        public float NoseHeight => noseHeight;
        public float AssemblySeconds => assemblySeconds;
        public float ReleaseDebrisSeconds => releaseDebrisSeconds;
        public float TrailChipsPerMeter => trailChipsPerMeter;
        public float NoseSpreadDegrees => noseSpreadDegrees;
        public bool RibbonEnabled => ribbonEnabled;
        public float WakeDustPerMeter => Mathf.Clamp(wakeDustPerMeter, 0f, 150f);
        public float WakeChipMultiplier => Mathf.Clamp(wakeChipMultiplier, 0f, 5f);
        public float WakeFrontShare => Mathf.Clamp01(wakeFrontShare);
        public float PillarJumpMinimumTiltDegrees => Mathf.Clamp(
            pillarJumpMinimumTiltDegrees > 0.001f ? pillarJumpMinimumTiltDegrees : 18f,
            5f,
            35f);
        public float PillarJumpMaximumTiltDegrees => Mathf.Clamp(
            pillarJumpMaximumTiltDegrees > 0.001f ? pillarJumpMaximumTiltDegrees : 28f,
            PillarJumpMinimumTiltDegrees,
            40f);
        public float PillarJumpScatterSpeed => Mathf.Clamp(
            pillarJumpScatterSpeed > 0.001f ? pillarJumpScatterSpeed : 3.2f,
            0.5f,
            8f);
    }
}
