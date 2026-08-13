using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Simulation.Fields
{
    public interface IFieldWorldQuery
    {
        FieldSample Sample(float3 worldPosition);
    }

    public sealed class FieldWorld : IFieldWorldQuery
    {
        private readonly List<FieldRegion> _regions;
        private readonly int _capacity;
        private int _roundRobinIndex;

        public FieldWorld(int capacity = 64, int maximumRegionsPerQuery = 24)
        {
            if (capacity <= 0 || maximumRegionsPerQuery <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            _capacity = capacity;
            MaximumRegionsPerQuery = maximumRegionsPerQuery;
            _regions = new List<FieldRegion>(capacity);
        }

        public int Count => _regions.Count;
        public int MaximumRegionsPerQuery { get; }
        public int DeferredRegionUpdateCount { get; private set; }
        public int LastQueryRegionChecks { get; private set; }
        public int LastQueryDebt { get; private set; }

        public FieldRegion GetRegion(int index) => _regions[index];

        public bool Register(in FieldRegion region)
        {
            for (int index = 0; index < _regions.Count; index++)
            {
                if (_regions[index].Id.Equals(region.Id))
                {
                    _regions[index] = region;
                    return true;
                }
            }

            if (_regions.Count >= _capacity)
            {
                int lowest = FindLowestPriorityIndex();
                if (_regions[lowest].Priority > region.Priority)
                {
                    return false;
                }

                _regions.RemoveAt(lowest);
            }

            int insertion = _regions.Count;
            while (insertion > 0 && _regions[insertion - 1].Priority < region.Priority)
            {
                insertion--;
            }

            _regions.Insert(insertion, region);
            return true;
        }

        public int Tick(float deltaTime, int updateBudget)
        {
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f || updateBudget <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            int processed = 0;
            int initialCount = _regions.Count;
            while (processed < updateBudget && _regions.Count > 0 && processed < initialCount)
            {
                if (_roundRobinIndex >= _regions.Count)
                {
                    _roundRobinIndex = 0;
                }

                FieldRegion stepped = _regions[_roundRobinIndex].StepLifetime(deltaTime);
                if (stepped.IsExpired)
                {
                    _regions.RemoveAt(_roundRobinIndex);
                }
                else
                {
                    _regions[_roundRobinIndex] = stepped;
                    _roundRobinIndex++;
                }

                processed++;
            }

            DeferredRegionUpdateCount = math.max(0, initialCount - processed);
            return processed;
        }

        public FieldSample Sample(float3 worldPosition)
        {
            float3 velocity = float3.zero;
            float pressure = 0f;
            float dragMultiplier = 1f;
            float totalWeight = 0f;
            int active = 0;
            int checks = math.min(_regions.Count, MaximumRegionsPerQuery);
            for (int index = 0; index < checks; index++)
            {
                if (!_regions[index].TrySample(worldPosition, out FieldContribution contribution))
                {
                    continue;
                }

                velocity += contribution.Velocity;
                pressure += contribution.Pressure;
                dragMultiplier = math.max(dragMultiplier, contribution.DragMultiplier);
                totalWeight += contribution.Weight;
                active++;
            }

            if (active > 1 && totalWeight > 1f)
            {
                velocity /= math.max(1f, totalWeight * 0.65f);
            }

            LastQueryRegionChecks = checks;
            LastQueryDebt = math.max(0, _regions.Count - checks);
            return new FieldSample(velocity, pressure, dragMultiplier, active, checks);
        }

        private int FindLowestPriorityIndex()
        {
            int lowest = 0;
            for (int index = 1; index < _regions.Count; index++)
            {
                FieldRegion candidate = _regions[index];
                FieldRegion current = _regions[lowest];
                if (candidate.Priority < current.Priority ||
                    (candidate.Priority == current.Priority && candidate.RemainingLifetime < current.RemainingLifetime))
                {
                    lowest = index;
                }
            }

            return lowest;
        }
    }
}
