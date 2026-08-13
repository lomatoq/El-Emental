using UnityEngine;

namespace Elemental.Runtime.World
{
    [CreateAssetMenu(menuName = "Elemental/World/Meteor Shower Profile", fileName = "MeteorShowerProfile")]
    public sealed class MeteorShowerProfile : ScriptableObject
    {
        [SerializeField] private bool enabled = true;
        [SerializeField, Min(0f)] private float distantRatePerSecond = 0.15f;
        [SerializeField, Range(8, 64)] private int distantPoolSize = 64;
        [SerializeField, Min(1f)] private float physicalIntervalMin = 35f;
        [SerializeField, Min(1f)] private float physicalIntervalMax = 55f;
        [SerializeField, Range(1, 4)] private int maximumPhysical = 4;
        [SerializeField] private Vector2 speedRange = new Vector2(24f, 42f);
        [SerializeField] private Vector2 radiusRange = new Vector2(0.25f, 0.9f);
        [SerializeField] private Vector2 craterRadiusRange = new Vector2(0.4f, 2f);
        [SerializeField, Min(1f)] private float density = 1800f;
        [SerializeField, Range(0, 2)] private int maximumTerrainEditsPerSecond = 2;

        public bool Enabled => enabled;
        public float DistantRatePerSecond => distantRatePerSecond;
        public int DistantPoolSize => distantPoolSize;
        public float PhysicalIntervalMin => Mathf.Min(physicalIntervalMin, physicalIntervalMax);
        public float PhysicalIntervalMax => Mathf.Max(physicalIntervalMin, physicalIntervalMax);
        public int MaximumPhysical => maximumPhysical;
        public Vector2 SpeedRange => Sorted(speedRange, 1f);
        public Vector2 RadiusRange => Sorted(radiusRange, 0.05f);
        public Vector2 CraterRadiusRange => Sorted(craterRadiusRange, 0f);
        public float Density => density;
        public int MaximumTerrainEditsPerSecond => maximumTerrainEditsPerSecond;

        private static Vector2 Sorted(Vector2 value, float minimum)
        {
            float x = Mathf.Max(minimum, Mathf.Min(value.x, value.y));
            float y = Mathf.Max(x, Mathf.Max(value.x, value.y));
            return new Vector2(x, y);
        }
    }
}
