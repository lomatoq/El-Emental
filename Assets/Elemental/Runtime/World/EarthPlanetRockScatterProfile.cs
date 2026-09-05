using UnityEngine;

namespace Elemental.Runtime.World
{
    [CreateAssetMenu(menuName = "Elemental/World/Planet Rock Scatter", fileName = "EarthPlanetRockScatterProfile")]
    public sealed class EarthPlanetRockScatterProfile : ScriptableObject
    {
        [SerializeField] private uint seed = 20260904u;
        [SerializeField, Range(0, 128)] private int largeCount = 24;
        [SerializeField, Range(0, 512)] private int mediumCount = 160;
        [SerializeField, Range(0, 256)] private int clusterCount = 128;
        [SerializeField] private Vector2 largeDiameter = new Vector2(3f, 6f);
        [SerializeField] private Vector2 mediumDiameter = new Vector2(.7f, 1.8f);
        [SerializeField] private Vector2 smallDiameter = new Vector2(.10f, .38f);
        [SerializeField, Range(1, 24)] private int clusterMinimumStones = 8;
        [SerializeField, Range(1, 24)] private int clusterMaximumStones = 16;
        [SerializeField, Range(.5f, 4f)] private float clusterRadius = 1.8f;
        [SerializeField, Range(0f, .05f)] private float surfaceInset = .02f;
        [SerializeField, Range(0f, 3f)] private float spacing = .35f;
        [SerializeField, Range(1, 4)] private int gameplayObjectsPerFrame = 4;
        [SerializeField, Range(1, 8)] private int placementAttempts = 8;
        [SerializeField, Min(1f)] private float startupWaitSeconds = 20f;

        public uint Seed => seed;
        public int LargeCount => Mathf.Clamp(largeCount, 0, 128);
        public int MediumCount => Mathf.Clamp(mediumCount, 0, 512);
        public int ClusterCount => Mathf.Clamp(clusterCount, 0, 256);
        public Vector2 LargeDiameter => Sorted(largeDiameter, .3f);
        public Vector2 MediumDiameter => Sorted(mediumDiameter, .1f);
        public Vector2 SmallDiameter => Sorted(smallDiameter, .03f);
        public int ClusterMinimumStones => Mathf.Clamp(clusterMinimumStones, 1, 24);
        public int ClusterMaximumStones => Mathf.Clamp(clusterMaximumStones, ClusterMinimumStones, 24);
        public float ClusterRadius => Mathf.Clamp(clusterRadius, .5f, 4f);
        public float SurfaceInset => Mathf.Clamp(surfaceInset, 0f, .05f);
        public float Spacing => Mathf.Max(0f, spacing);
        public int GameplayObjectsPerFrame => Mathf.Clamp(gameplayObjectsPerFrame, 1, 4);
        public int PlacementAttempts => Mathf.Clamp(placementAttempts, 1, 8);
        public float StartupWaitSeconds => Mathf.Max(1f, startupWaitSeconds);

        private static Vector2 Sorted(Vector2 range, float minimum) => new Vector2(
            Mathf.Max(minimum, Mathf.Min(range.x, range.y)), Mathf.Max(minimum, Mathf.Max(range.x, range.y)));
    }
}
