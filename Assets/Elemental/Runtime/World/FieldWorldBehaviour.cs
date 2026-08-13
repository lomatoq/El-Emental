using Elemental.Simulation.Fields;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.World
{
    [DisallowMultipleComponent]
    public sealed class FieldWorldBehaviour : MonoBehaviour
    {
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("Elemental.FieldWorld.Tick");

        [SerializeField, Min(1)] private int capacity = 64;
        [SerializeField, Min(1)] private int maximumRegionsPerQuery = 24;
        [SerializeField, Min(1f)] private float updateRate = 20f;
        [SerializeField, Min(1)] private int updatesPerStep = 24;
        [SerializeField] private bool drawDebugFields = true;

        private float _accumulator;

        public FieldWorld World { get; private set; }
        public bool IsReady => World != null;
        public int DeferredRegionUpdateCount => World?.DeferredRegionUpdateCount ?? 0;
        public int LastQueryDebt => World?.LastQueryDebt ?? 0;

        public void Configure(int configuredCapacity, int configuredMaximumRegionsPerQuery, float configuredUpdateRate, int configuredUpdatesPerStep)
        {
            capacity = Mathf.Max(1, configuredCapacity);
            maximumRegionsPerQuery = Mathf.Max(1, configuredMaximumRegionsPerQuery);
            updateRate = Mathf.Max(1f, configuredUpdateRate);
            updatesPerStep = Mathf.Max(1, configuredUpdatesPerStep);
            Rebuild();
        }

        public bool Register(in FieldRegion region)
        {
            EnsureWorld();
            return World.Register(in region);
        }

        private void Awake()
        {
            EnsureWorld();
        }

        private void FixedUpdate()
        {
            EnsureWorld();
            float interval = 1f / Mathf.Max(1f, updateRate);
            _accumulator = Mathf.Min(_accumulator + Time.fixedDeltaTime, interval * 4f);
            using (TickMarker.Auto())
            {
                while (_accumulator >= interval)
                {
                    World.Tick(interval, updatesPerStep);
                    _accumulator -= interval;
                }
            }
        }

        private void Rebuild()
        {
            World = new FieldWorld(capacity, maximumRegionsPerQuery);
            _accumulator = 0f;
        }

        private void EnsureWorld()
        {
            if (World == null)
            {
                Rebuild();
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugFields || World == null)
            {
                return;
            }

            for (int index = 0; index < World.Count; index++)
            {
                FieldRegion region = World.GetRegion(index);
                Vector3 center = ToVector3(region.Center);
                Vector3 axis = ToVector3(region.Axis);
                Gizmos.color = ColorFor(region.Kind);
                Gizmos.DrawWireSphere(center, region.Radius);
                Gizmos.DrawLine(center, center + (axis * Mathf.Max(1f, region.Length)));
                Vector3 samplePosition = center + (Vector3.Cross(axis, Vector3.right).normalized * region.Radius * 0.5f);
                if (region.TrySample(ToFloat3(samplePosition), out FieldContribution contribution))
                {
                    Gizmos.DrawRay(samplePosition, ToVector3(contribution.Velocity) * 0.2f);
                }
            }
        }

        private static Color ColorFor(AirFieldKind kind)
        {
            switch (kind)
            {
                case AirFieldKind.GustCorridor: return new Color(0.2f, 0.85f, 1f, 0.8f);
                case AirFieldKind.Vortex: return new Color(0.65f, 0.35f, 1f, 0.8f);
                case AirFieldKind.LiftColumn: return new Color(0.25f, 1f, 0.55f, 0.8f);
                default: return new Color(1f, 0.9f, 0.25f, 0.8f);
            }
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }
}
