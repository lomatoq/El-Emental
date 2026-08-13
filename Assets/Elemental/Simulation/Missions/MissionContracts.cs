using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Simulation.Missions
{
    public readonly struct MissionEntityId : IEquatable<MissionEntityId>
    {
        public MissionEntityId(uint value) => Value = value;
        public uint Value { get; }
        public bool IsValid => Value != 0u;
        public bool Equals(MissionEntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is MissionEntityId other && Equals(other);
        public override int GetHashCode() => unchecked((int)Value);
    }

    public enum ObjectiveState : byte { Locked, Active, Completed, Failed }
    public enum MissionOutcome : byte { Running, Win, PartialSuccess, Failure }
    public enum CrisisKind : byte
    {
        LavaAdvance = 1,
        StructuralFailure = 2,
        SmokeHazard = 3,
        CivilianPanic = 4,
        BlockedRoute = 5,
        TimedEvacuation = 6
    }

    public readonly struct ObjectiveNode
    {
        public ObjectiveNode(ushort id, ushort prerequisite, int target, bool required)
        {
            Id = id; Prerequisite = prerequisite; Target = math.max(1, target); Required = required;
        }
        public ushort Id { get; }
        public ushort Prerequisite { get; }
        public int Target { get; }
        public bool Required { get; }
    }

    public sealed class ObjectiveGraph
    {
        private readonly ObjectiveNode[] _nodes;
        private readonly ObjectiveState[] _states;
        private readonly int[] _progress;

        public ObjectiveGraph(ObjectiveNode[] nodes)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _states = new ObjectiveState[nodes.Length];
            _progress = new int[nodes.Length];
            RefreshLocks();
        }
        public int Count => _nodes.Length;
        public ObjectiveNode GetNode(int index) => _nodes[index];
        public ObjectiveState GetState(int index) => _states[index];
        public int GetProgress(int index) => _progress[index];

        public void SetProgress(ushort id, int value)
        {
            int index = Find(id);
            if (index < 0 || _states[index] == ObjectiveState.Failed) return;
            _progress[index] = math.clamp(value, 0, _nodes[index].Target);
            if (_progress[index] >= _nodes[index].Target) _states[index] = ObjectiveState.Completed;
            RefreshLocks();
        }

        public void Fail(ushort id)
        {
            int index = Find(id);
            if (index >= 0 && _states[index] != ObjectiveState.Completed) _states[index] = ObjectiveState.Failed;
        }

        public bool RequiredComplete
        {
            get
            {
                for (int index = 0; index < _nodes.Length; index++)
                    if (_nodes[index].Required && _states[index] != ObjectiveState.Completed) return false;
                return true;
            }
        }

        private void RefreshLocks()
        {
            for (int index = 0; index < _nodes.Length; index++)
            {
                if (_states[index] == ObjectiveState.Completed || _states[index] == ObjectiveState.Failed) continue;
                ushort prerequisite = _nodes[index].Prerequisite;
                _states[index] = prerequisite == 0 || IsCompleted(prerequisite) ? ObjectiveState.Active : ObjectiveState.Locked;
            }
        }

        private bool IsCompleted(ushort id)
        {
            int index = Find(id);
            return index >= 0 && _states[index] == ObjectiveState.Completed;
        }

        private int Find(ushort id)
        {
            for (int index = 0; index < _nodes.Length; index++) if (_nodes[index].Id == id) return index;
            return -1;
        }
    }

    public readonly struct EscalationCurve
    {
        public EscalationCurve(float startSeverity, float endSeverity, float duration)
        {
            StartSeverity = math.saturate(startSeverity); EndSeverity = math.saturate(endSeverity); Duration = math.max(1f, duration);
        }
        public float StartSeverity { get; }
        public float EndSeverity { get; }
        public float Duration { get; }
        public float Evaluate(float elapsed) => math.lerp(StartSeverity, EndSeverity, math.saturate(elapsed / Duration));
    }

    public readonly struct SpawnRule
    {
        public SpawnRule(CrisisKind kind, float cooldown, int maximumActive, byte priority)
        {
            Kind = kind; Cooldown = math.max(0.1f, cooldown); MaximumActive = math.max(1, maximumActive); Priority = priority;
        }
        public CrisisKind Kind { get; }
        public float Cooldown { get; }
        public int MaximumActive { get; }
        public byte Priority { get; }
    }

    public readonly struct ScoreRule
    {
        public ScoreRule(int rescuedPoints, int lostPenalty, int structurePoints, int timeBonusPerSecond)
        {
            RescuedPoints = rescuedPoints; LostPenalty = lostPenalty;
            StructurePoints = structurePoints; TimeBonusPerSecond = timeBonusPerSecond;
        }
        public int RescuedPoints { get; }
        public int LostPenalty { get; }
        public int StructurePoints { get; }
        public int TimeBonusPerSecond { get; }
    }

    public readonly struct MissionDefinition
    {
        public MissionDefinition(uint seed, float duration, int civilianCount, int requiredRescues, int crisisBudget, EscalationCurve escalation, SpawnRule[] spawnRules, ScoreRule scoreRule)
        {
            Seed = seed; Duration = math.max(10f, duration); CivilianCount = math.max(1, civilianCount);
            RequiredRescues = math.clamp(requiredRescues, 1, CivilianCount); CrisisBudget = math.max(1, crisisBudget);
            Escalation = escalation; SpawnRules = spawnRules ?? throw new ArgumentNullException(nameof(spawnRules)); ScoreRule = scoreRule;
        }
        public uint Seed { get; }
        public float Duration { get; }
        public int CivilianCount { get; }
        public int RequiredRescues { get; }
        public int CrisisBudget { get; }
        public EscalationCurve Escalation { get; }
        public SpawnRule[] SpawnRules { get; }
        public ScoreRule ScoreRule { get; }
    }

    public readonly struct CrisisEvent
    {
        public CrisisEvent(MissionEntityId id, uint tick, CrisisKind kind, float severity, float lifetime, float3 position, byte priority)
        {
            Id = id; Tick = tick; Kind = kind; Severity = math.saturate(severity);
            RemainingLifetime = math.max(0f, lifetime); Position = position; Priority = priority;
        }
        private CrisisEvent(CrisisEvent value, float lifetime)
        {
            Id = value.Id; Tick = value.Tick; Kind = value.Kind; Severity = value.Severity;
            RemainingLifetime = lifetime; Position = value.Position; Priority = value.Priority;
        }
        public MissionEntityId Id { get; }
        public uint Tick { get; }
        public CrisisKind Kind { get; }
        public float Severity { get; }
        public float RemainingLifetime { get; }
        public float3 Position { get; }
        public byte Priority { get; }
        public CrisisEvent Step(float deltaTime) => new CrisisEvent(this, math.max(0f, RemainingLifetime - deltaTime));
    }

    public readonly struct ScoreBreakdown
    {
        public ScoreBreakdown(int rescued, int lost, int structures, int timeBonus, int total)
        {
            Rescued = rescued; Lost = lost; Structures = structures; TimeBonus = timeBonus; Total = total;
        }
        public int Rescued { get; }
        public int Lost { get; }
        public int Structures { get; }
        public int TimeBonus { get; }
        public int Total { get; }
    }
}
