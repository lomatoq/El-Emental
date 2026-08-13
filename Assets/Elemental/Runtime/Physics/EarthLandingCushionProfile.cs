using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Landing Cushion Profile", fileName = "EarthLandingCushionProfile")]
    public sealed class EarthLandingCushionProfile : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float predictionSeconds = 4f;
        [SerializeField, Min(0.1f)] private float activationHeight = 3.2f;
        [SerializeField, Min(0.1f)] private float maximumLandingSpeed = 4f;
        [SerializeField, Min(0.1f)] private float pillarHeight = 2.4f;
        [SerializeField, Min(0.1f)] private float pillarWidth = 1.7f;
        [SerializeField, Min(0.05f)] private float compressionSeconds = 0.28f;
        [SerializeField, Min(0.05f)] private float retreatSeconds = 0.42f;
        [SerializeField, Min(0.1f)] private float gravityMagnitude = 14f;

        public float PredictionSeconds => predictionSeconds;
        public float ActivationHeight => activationHeight;
        public float MaximumLandingSpeed => maximumLandingSpeed;
        public float PillarHeight => pillarHeight;
        public float PillarWidth => pillarWidth;
        public float CompressionSeconds => compressionSeconds;
        public float RetreatSeconds => retreatSeconds;
        public float GravityMagnitude => gravityMagnitude;
    }
}
