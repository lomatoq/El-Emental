using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Vector Field Profile", fileName = "EarthVectorFieldProfile")]
    public sealed class EarthVectorFieldProfile : ScriptableObject
    {
        [SerializeField, Min(0f)] private float continuousForce = 4200f;
        [SerializeField, Min(0f)] private float minimumReleaseImpulse = 260f;
        [Tooltip("Minimum release pulse for a whole structure. This keeps a quick RMB tap useful without making light rocks explosive.")]
        [SerializeField, Min(0f)] private float minimumWallReleaseImpulse = 650f;
        [SerializeField, Min(0f)] private float maximumReleaseImpulse = 2400f;
        [SerializeField, Min(0.1f)] private float rockSpeedLimit = 32f;
        [SerializeField, Min(0.1f)] private float wallSpeedLimit = 14f;
        [Tooltip("Precision speed while RMB remains held. Flick release still uses the full projectile speed cap.")]
        [SerializeField, Min(0.1f)] private float controlledRockSpeedLimit = 9f;
        [SerializeField, Min(0.1f)] private float controlledWallSpeedLimit = 6.5f;
        [Tooltip("Extra leverage for rooted structures. The high acceleration makes a wide production wall respond immediately; the controlled speed cap prevents explosive motion and inverse mass still makes small walls faster.")]
        [SerializeField, Min(0.1f)] private float wallForceMultiplier = 72f;
        [SerializeField, Min(0.1f)] private float fullChargeSeconds = 1.35f;

        public float ContinuousForce => continuousForce;
        public float MinimumReleaseImpulse => minimumReleaseImpulse;
        public float MinimumWallReleaseImpulse => Mathf.Max(minimumReleaseImpulse, minimumWallReleaseImpulse);
        public float MaximumReleaseImpulse => Mathf.Max(minimumReleaseImpulse, maximumReleaseImpulse);
        public float RockSpeedLimit => rockSpeedLimit;
        public float WallSpeedLimit => wallSpeedLimit;
        public float ControlledRockSpeedLimit => Mathf.Min(rockSpeedLimit, controlledRockSpeedLimit);
        public float ControlledWallSpeedLimit => Mathf.Min(wallSpeedLimit, controlledWallSpeedLimit);
        public float WallForceMultiplier => wallForceMultiplier;
        public float FullChargeSeconds => fullChargeSeconds;
    }
}
