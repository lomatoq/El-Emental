using Elemental.Simulation.Magic;
using Unity.Mathematics;

namespace Elemental.Presentation.VFX
{
    public readonly struct EarthFeedbackBatchResult
    {
        public EarthFeedbackBatchResult(
            int eventCount, int dustCount, int chipCount, float3 point, float3 normal,
            float maximumKineticEnergy, uint seed)
        {
            EventCount = eventCount;
            DustCount = dustCount;
            ChipCount = chipCount;
            Point = point;
            Normal = math.normalizesafe(normal, new float3(0f, 1f, 0f));
            MaximumKineticEnergy = math.max(0f, maximumKineticEnergy);
            Seed = seed;
        }

        public int EventCount { get; }
        public int DustCount { get; }
        public int ChipCount { get; }
        public float3 Point { get; }
        public float3 Normal { get; }
        public float MaximumKineticEnergy { get; }
        public uint Seed { get; }
    }

    public struct EarthFeedbackBatchAccumulator
    {
        private int _eventCount;
        private int _dustCount;
        private int _chipCount;
        private float _totalWeight;
        private float3 _weightedPoint;
        private float3 _weightedNormal;
        private float _maximumKineticEnergy;
        private uint _seed;

        public int PendingCount => _eventCount;

        public void Add(
            in EarthImpactEvent impact,
            in EarthFeedbackSample sample,
            int maximumDustPerFrame,
            int maximumChipsPerFrame)
        {
            float weight = math.max(1f, math.max(impact.Impulse, math.sqrt(impact.KineticEnergy + 1f)));
            _eventCount++;
            _dustCount = math.min(math.max(0, maximumDustPerFrame), _dustCount + sample.DustCount);
            _chipCount = math.min(math.max(0, maximumChipsPerFrame), _chipCount + sample.ChipCount);
            _totalWeight += weight;
            _weightedPoint += impact.Point * weight;
            _weightedNormal += impact.Normal * weight;
            _maximumKineticEnergy = math.max(_maximumKineticEnergy, impact.KineticEnergy);
            _seed = Hash(_seed ^ impact.SourceId ^ (impact.Tick * 0x9E3779B9u));
        }

        public bool TryFlush(out EarthFeedbackBatchResult result)
        {
            if (_eventCount <= 0 || _totalWeight <= 0f)
            {
                result = default;
                return false;
            }

            result = new EarthFeedbackBatchResult(
                _eventCount,
                _dustCount,
                _chipCount,
                _weightedPoint / _totalWeight,
                _weightedNormal / _totalWeight,
                _maximumKineticEnergy,
                _seed);
            this = default;
            return true;
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            return value ^ (value >> 16);
        }
    }
}
