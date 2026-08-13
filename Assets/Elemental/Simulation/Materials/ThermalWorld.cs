using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Simulation.Materials
{
    public readonly struct ThermalRegionId
    {
        public ThermalRegionId(uint value) => Value = value;
        public uint Value { get; }
        public bool IsValid => Value != 0;
    }

    public readonly struct ThermalRegion
    {
        public ThermalRegion(
            ThermalRegionId id,
            uint owner,
            float3 center,
            float radius,
            float temperatureDelta,
            float transferCoefficient,
            float lifetime,
            MaterialTags tags,
            byte priority)
        {
            if (!id.IsValid || radius <= 0f || transferCoefficient < 0f || lifetime <= 0f ||
                !math.all(math.isfinite(center)) || !float.IsFinite(temperatureDelta))
            {
                throw new ArgumentOutOfRangeException();
            }
            Id = id;
            Owner = owner;
            Center = center;
            Radius = radius;
            TemperatureDelta = temperatureDelta;
            TransferCoefficient = transferCoefficient;
            RemainingLifetime = lifetime;
            Tags = tags;
            Priority = priority;
        }

        private ThermalRegion(ThermalRegion source, float lifetime)
        {
            Id = source.Id; Owner = source.Owner; Center = source.Center; Radius = source.Radius;
            TemperatureDelta = source.TemperatureDelta; TransferCoefficient = source.TransferCoefficient;
            RemainingLifetime = lifetime; Tags = source.Tags; Priority = source.Priority;
        }

        public ThermalRegionId Id { get; }
        public uint Owner { get; }
        public float3 Center { get; }
        public float Radius { get; }
        public float TemperatureDelta { get; }
        public float TransferCoefficient { get; }
        public float RemainingLifetime { get; }
        public MaterialTags Tags { get; }
        public byte Priority { get; }
        public ThermalRegion Step(float deltaTime) => new ThermalRegion(this, math.max(0f, RemainingLifetime - deltaTime));
    }

    public readonly struct ThermalSample
    {
        public ThermalSample(float temperatureDelta, float transferCoefficient, MaterialTags tags, int checks)
        {
            TemperatureDelta = temperatureDelta;
            TransferCoefficient = transferCoefficient;
            Tags = tags;
            RegionChecks = checks;
        }
        public float TemperatureDelta { get; }
        public float TransferCoefficient { get; }
        public MaterialTags Tags { get; }
        public int RegionChecks { get; }
    }

    public sealed class ThermalWorld
    {
        private readonly List<ThermalRegion> _regions;
        private readonly int _capacity;
        private int _cursor;

        public ThermalWorld(int capacity = 64, int maximumRegionsPerQuery = 16)
        {
            _capacity = math.max(1, capacity);
            MaximumRegionsPerQuery = math.max(1, maximumRegionsPerQuery);
            _regions = new List<ThermalRegion>(_capacity);
        }
        public int Count => _regions.Count;
        public int MaximumRegionsPerQuery { get; }
        public int DeferredUpdateCount { get; private set; }
        public int LastQueryDebt { get; private set; }
        public ThermalRegion GetRegion(int index) => _regions[index];

        public bool Register(in ThermalRegion region)
        {
            for (int index = 0; index < _regions.Count; index++)
            {
                if (_regions[index].Id.Value == region.Id.Value)
                {
                    _regions[index] = region;
                    return true;
                }
            }
            if (_regions.Count >= _capacity)
            {
                int lowest = 0;
                for (int index = 1; index < _regions.Count; index++)
                {
                    if (_regions[index].Priority < _regions[lowest].Priority) lowest = index;
                }
                if (_regions[lowest].Priority > region.Priority) return false;
                _regions.RemoveAt(lowest);
            }
            _regions.Add(region);
            return true;
        }

        public int Tick(float deltaTime, int budget)
        {
            int initial = _regions.Count;
            int processed = 0;
            while (processed < budget && processed < initial && _regions.Count > 0)
            {
                if (_cursor >= _regions.Count) _cursor = 0;
                ThermalRegion next = _regions[_cursor].Step(deltaTime);
                if (next.RemainingLifetime <= 0f) _regions.RemoveAt(_cursor);
                else { _regions[_cursor] = next; _cursor++; }
                processed++;
            }
            DeferredUpdateCount = math.max(0, initial - processed);
            return processed;
        }

        public ThermalSample Sample(float3 position)
        {
            float delta = 0f;
            float coefficient = 0f;
            MaterialTags tags = MaterialTags.None;
            int checks = math.min(_regions.Count, MaximumRegionsPerQuery);
            for (int index = 0; index < checks; index++)
            {
                ThermalRegion region = _regions[index];
                float distance = math.distance(position, region.Center);
                if (distance > region.Radius) continue;
                float falloff = 1f - math.saturate(distance / region.Radius);
                delta += region.TemperatureDelta * falloff;
                coefficient += region.TransferCoefficient * falloff;
                tags |= region.Tags;
            }
            LastQueryDebt = math.max(0, _regions.Count - checks);
            return new ThermalSample(delta, coefficient, tags, checks);
        }
    }
}
