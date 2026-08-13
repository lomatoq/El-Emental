using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Vector Field Profile", fileName = "EarthVectorFieldProfile")]
    public sealed class EarthVectorFieldProfile : ScriptableObject
    {
        [SerializeField, Min(0f)] private float continuousForce = 4200f;
        [SerializeField, Min(0f)] private float minimumReleaseImpulse = 260f;
        [SerializeField, Min(0f)] private float maximumReleaseImpulse = 2400f;
        [SerializeField, Min(0.1f)] private float rockSpeedLimit = 32f;
        [SerializeField, Min(0.1f)] private float wallSpeedLimit = 14f;
        [SerializeField, Min(0.1f)] private float wallForceMultiplier = 3.4f;
        [SerializeField, Min(0.1f)] private float fullChargeSeconds = 1.35f;

        public float ContinuousForce => continuousForce;
        public float MinimumReleaseImpulse => minimumReleaseImpulse;
        public float MaximumReleaseImpulse => Mathf.Max(minimumReleaseImpulse, maximumReleaseImpulse);
        public float RockSpeedLimit => rockSpeedLimit;
        public float WallSpeedLimit => wallSpeedLimit;
        public float WallForceMultiplier => wallForceMultiplier;
        public float FullChargeSeconds => fullChargeSeconds;
    }
}
