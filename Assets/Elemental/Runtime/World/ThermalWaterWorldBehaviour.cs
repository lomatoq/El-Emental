using Elemental.Simulation.Materials;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.World
{
    [DisallowMultipleComponent]
    public sealed class ThermalWaterWorldBehaviour : MonoBehaviour
    {
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("Elemental.ThermalWaterWorld.Tick");

        [SerializeField, Min(1)] private int thermalCapacity = 64;
        [SerializeField, Min(1)] private int waterCapacity = 64;
        [SerializeField, Min(1)] private int maximumThermalQueries = 16;
        [SerializeField, Min(1f)] private float updateRate = 10f;
        [SerializeField, Min(1)] private int updateBudget = 16;
        private float _accumulator;

        public ThermalWorld Thermal { get; private set; }
        public WaterWorld Water { get; private set; }
        public bool IsReady => Thermal != null && Water != null;

        public void Configure(int configuredThermalCapacity, int configuredWaterCapacity, int configuredMaximumQueries, float configuredRate, int configuredBudget)
        {
            thermalCapacity = Mathf.Max(1, configuredThermalCapacity);
            waterCapacity = Mathf.Max(1, configuredWaterCapacity);
            maximumThermalQueries = Mathf.Max(1, configuredMaximumQueries);
            updateRate = Mathf.Max(1f, configuredRate);
            updateBudget = Mathf.Max(1, configuredBudget);
            Rebuild();
        }

        private void Awake() => EnsureWorld();

        private void FixedUpdate()
        {
            EnsureWorld();
            float interval = 1f / updateRate;
            _accumulator = Mathf.Min(_accumulator + Time.fixedDeltaTime, interval * 4f);
            using (TickMarker.Auto())
            {
                while (_accumulator >= interval)
                {
                    Thermal.Tick(interval, updateBudget);
                    Water.TickMotion(interval, updateBudget);
                    ApplyThermalSamples(interval);
                    _accumulator -= interval;
                }
            }
        }

        private void ApplyThermalSamples(float interval)
        {
            int count = Mathf.Min(Water.Count, updateBudget);
            MaterialDefinition water = MaterialDefinition.Water;
            for (int index = 0; index < count; index++)
            {
                WaterVolume volume = Water.GetVolume(index);
                ThermalSample sample = Thermal.Sample(volume.Center);
                float energy = sample.TemperatureDelta * sample.TransferCoefficient * volume.State.Mass * interval;
                if (Mathf.Abs(energy) > 0.0001f)
                {
                    Water.ApplyEnergy(index, in water, energy);
                }
            }
        }

        private void Rebuild()
        {
            Thermal = new ThermalWorld(thermalCapacity, maximumThermalQueries);
            Water = new WaterWorld(waterCapacity);
            _accumulator = 0f;
        }

        private void EnsureWorld()
        {
            if (!IsReady) Rebuild();
        }
    }
}
