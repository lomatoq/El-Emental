using System.Collections.Generic;
using Elemental.Core.Math;
using Unity.Mathematics;

namespace Elemental.Simulation.Missions
{
    public sealed class CrisisDirector
    {
        private readonly MissionDefinition _definition;
        private readonly List<CrisisEvent> _active;
        private readonly List<CrisisEvent> _timeline;
        private readonly float[] _cooldowns;
        private DeterministicRandom _random;
        private uint _nextId = 1u;

        public CrisisDirector(in MissionDefinition definition)
        {
            _definition = definition;
            _active = new List<CrisisEvent>(definition.CrisisBudget);
            _timeline = new List<CrisisEvent>(128);
            _cooldowns = new float[definition.SpawnRules.Length];
            _random = new DeterministicRandom(definition.Seed);
        }
        public int ActiveCount => _active.Count;
        public int TimelineCount => _timeline.Count;
        public int DeferredSpawnCount { get; private set; }
        public CrisisEvent GetActive(int index) => _active[index];
        public CrisisEvent GetTimeline(int index) => _timeline[index];

        public int Tick(uint tick, float elapsed, float deltaTime, int spawnBudget)
        {
            for (int index = _active.Count - 1; index >= 0; index--)
            {
                CrisisEvent stepped = _active[index].Step(deltaTime);
                if (stepped.RemainingLifetime <= 0f) _active.RemoveAt(index);
                else _active[index] = stepped;
            }
            int spawned = 0;
            int eligible = 0;
            for (int index = 0; index < _definition.SpawnRules.Length; index++)
            {
                _cooldowns[index] -= deltaTime;
                SpawnRule rule = _definition.SpawnRules[index];
                if (_cooldowns[index] > 0f || Count(rule.Kind) >= rule.MaximumActive) continue;
                eligible++;
                if (spawned >= spawnBudget || _active.Count >= _definition.CrisisBudget) continue;
                float severity = _definition.Escalation.Evaluate(elapsed) * math.lerp(0.8f, 1.2f, _random.NextFloat01());
                float angle = _random.NextFloat01() * math.PI * 2f;
                float3 position = new float3(math.cos(angle) * 8f, 25f + (_random.NextFloat01() * 3f), math.sin(angle) * 8f);
                CrisisEvent crisis = new CrisisEvent(
                    new MissionEntityId(_nextId++), tick, rule.Kind, severity,
                    math.lerp(4f, 9f, _random.NextFloat01()), position, rule.Priority);
                _active.Add(crisis);
                _timeline.Add(crisis);
                _cooldowns[index] = rule.Cooldown;
                spawned++;
            }
            DeferredSpawnCount = math.max(0, eligible - spawned);
            return spawned;
        }

        public float Severity(CrisisKind kind)
        {
            float value = 0f;
            for (int index = 0; index < _active.Count; index++)
                if (_active[index].Kind == kind) value += _active[index].Severity;
            return math.min(2f, value);
        }

        private int Count(CrisisKind kind)
        {
            int count = 0;
            for (int index = 0; index < _active.Count; index++) if (_active[index].Kind == kind) count++;
            return count;
        }
    }
}
