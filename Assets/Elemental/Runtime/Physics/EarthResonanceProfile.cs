using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Resonance Profile", fileName = "EarthResonanceProfile")]
    public sealed class EarthResonanceProfile : ScriptableObject
    {
        [SerializeField, Min(0.05f)] private float thresholdSeconds = 0.55f;
        [SerializeField, Min(0.1f)] private float fullChargeSeconds = 2.6f;
        [SerializeField, Range(4, 28)] private int minimumStoneCount = 8;
        [SerializeField, Range(8, 28)] private int maximumStoneCount = 28;
        [SerializeField, Min(0.2f)] private float minimumRadius = 1.2f;
        [SerializeField, Min(0.2f)] private float maximumRadius = 6.5f;
        [SerializeField, Min(0.1f)] private float minimumLifetime = 1.5f;
        [SerializeField, Min(0.1f)] private float maximumLifetime = 6f;
        [Header("Physical hover")]
        [SerializeField, Min(0.1f)] private float stiffness = 24f;
        [SerializeField, Min(0.1f)] private float damping = 9f;
        [SerializeField, Min(0.1f)] private float maximumAcceleration = 62f;
        [SerializeField, Min(0.1f)] private float maximumSpeed = 11f;
        [Header("Readable formation")]
        [SerializeField, Range(1.6f, 3.2f)] private float minimumFormationRadius = 2.15f;
        [SerializeField, Range(180f, 360f)] private float forwardArcDegrees = 360f;
        [SerializeField, Range(0.12f, 0.75f)] private float largestStoneRadius = 0.58f;
        [SerializeField, Range(0.10f, 0.48f)] private float smallestStoneRadius = 0.26f;
        [SerializeField, Range(18f, 38f)] private float projectileSpeed = 34f;
        [SerializeField, Range(0.04f, 0.25f)] private float automaticFireInterval = 0.11f;

        public EarthResonanceProfileData Data => new EarthResonanceProfileData(
            thresholdSeconds, fullChargeSeconds,
            minimumStoneCount, maximumStoneCount,
            minimumRadius, maximumRadius,
            minimumLifetime, maximumLifetime);
        public float Stiffness => stiffness;
        public float Damping => damping;
        public float MaximumAcceleration => maximumAcceleration;
        public float MaximumSpeed => maximumSpeed;
        public float MinimumFormationRadius => minimumFormationRadius;
        public float ForwardArcDegrees => forwardArcDegrees;
        public float LargestStoneRadius => largestStoneRadius;
        public float SmallestStoneRadius => smallestStoneRadius;
        public float ProjectileSpeed => projectileSpeed;
        public float AutomaticFireInterval => automaticFireInterval;
    }
}
