using UnityEngine;

namespace Elemental.Presentation.MotionMatching
{
    [CreateAssetMenu(fileName = "EAMMRuntimeProfile", menuName = "Elemental/Animation/EAMM Runtime Profile")]
    public sealed class EAMMRuntimeProfile : ScriptableObject
    {
        [SerializeField] private bool enabledForPlayer = true;
        [SerializeField] private bool enabledForBots = true;
        [SerializeField, Min(1f)] private float databaseRate = 30f;
        [SerializeField, Min(0.02f)] private float playerSearchSeconds = 0.10f;
        [SerializeField, Min(0.02f)] private float botSearchSeconds = 0.16f;
        [SerializeField, Min(0.1f)] private float predictionSeconds = 0.85f;
        [SerializeField, Min(0.1f)] private float obstacleRadius = 1.35f;
        [SerializeField, Range(0f, 1f)] private float basePoseWeight = 1f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        public bool EnabledForPlayer => enabledForPlayer;
        public bool EnabledForBots => enabledForBots;
        public float DatabaseRate => Mathf.Max(1f, databaseRate);
        public float PlayerSearchSeconds => Mathf.Max(0.02f, playerSearchSeconds);
        public float BotSearchSeconds => Mathf.Max(0.02f, botSearchSeconds);
        public float PredictionSeconds => Mathf.Max(0.1f, predictionSeconds);
        public float ObstacleRadius => Mathf.Max(0.1f, obstacleRadius);
        public float BasePoseWeight => Mathf.Clamp01(basePoseWeight);
        public LayerMask ObstacleMask => obstacleMask;
    }
}
