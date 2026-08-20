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

        public EarthSurfProfileData Data => new EarthSurfProfileData(
            emergenceSeconds, accelerationSeconds, minimumSpeed, maximumSpeed, releaseSeconds, speedExponent);
        public float NoseImpactImpulse => noseImpactImpulse;
        public float CarryAcceleration => carryAcceleration;
        public float BoardWidth => boardWidth;
        public float BoardLength => boardLength;
        public float NoseHeight => noseHeight;
    }
}
