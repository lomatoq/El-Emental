using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Simulation.Materials
{
    public readonly struct WaterVolumeId : IEquatable<WaterVolumeId>
    {
        public WaterVolumeId(uint value) => Value = value;
        public uint Value { get; }
        public bool IsValid => Value != 0;
        public bool Equals(WaterVolumeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is WaterVolumeId other && Equals(other);
        public override int GetHashCode() => unchecked((int)Value);
    }

    public readonly struct WaterVolume
    {
        public WaterVolume(WaterVolumeId id, uint owner, float3 center, float3 velocity, float radius, PhaseState state)
        {
            if (!id.IsValid || radius <= 0f || !math.all(math.isfinite(center)) || !math.all(math.isfinite(velocity)))
            {
                throw new ArgumentOutOfRangeException();
            }
            Id = id; Owner = owner; Center = center; Velocity = velocity; Radius = radius; State = state;
        }
        public WaterVolumeId Id { get; }
        public uint Owner { get; }
        public float3 Center { get; }
        public float3 Velocity { get; }
        public float Radius { get; }
        public PhaseState State { get; }
        public WaterVolume WithState(PhaseState state) => new WaterVolume(Id, Owner, Center, Velocity, Radius, state);
        public WaterVolume WithMotion(float3 center, float3 velocity) => new WaterVolume(Id, Owner, center, velocity, Radius, State);
    }

    public readonly struct ConservationTelemetry
    {
        public ConservationTelemetry(float initialMass, float currentMass, float requestedEnergy, float appliedEnergy)
        {
            InitialMass = initialMass;
            CurrentMass = currentMass;
            RequestedEnergy = requestedEnergy;
            AppliedEnergy = appliedEnergy;
        }
        public float InitialMass { get; }
        public float CurrentMass { get; }
        public float RequestedEnergy { get; }
        public float AppliedEnergy { get; }
        public float MassError => CurrentMass - InitialMass;
        public float EnergyError => RequestedEnergy - AppliedEnergy;
    }

    public enum WaterOperatorKind : byte
    {
        AddHeat = 1,
        RemoveHeat = 2,
        TransferMass = 3,
        Freeze = 4,
        Melt = 5,
        Vaporize = 6,
        Condense = 7,
        ApplyPressureImpulse = 8
    }

    public sealed class WaterWorld
    {
        private readonly List<WaterVolume> _volumes;
        private readonly int _capacity;
        private float _initialMass;
        private float _requestedEnergy;
        private float _appliedEnergy;

        public WaterWorld(int capacity = 64)
        {
            _capacity = math.max(1, capacity);
            _volumes = new List<WaterVolume>(_capacity);
        }

        public int Count => _volumes.Count;
        public WaterVolume GetVolume(int index) => _volumes[index];
        public ConservationTelemetry Telemetry => new ConservationTelemetry(
            _initialMass, ComputeMass(), _requestedEnergy, _appliedEnergy);

        public bool Register(in WaterVolume volume)
        {
            for (int index = 0; index < _volumes.Count; index++)
            {
                if (_volumes[index].Id.Equals(volume.Id))
                {
                    _volumes[index] = volume;
                    return true;
                }
            }
            if (_volumes.Count >= _capacity) return false;
            _volumes.Add(volume);
            _initialMass += volume.State.Mass;
            return true;
        }

        public bool TryFindNearest(float3 position, float maximumDistance, out int index)
        {
            index = -1;
            float best = maximumDistance * maximumDistance;
            for (int candidate = 0; candidate < _volumes.Count; candidate++)
            {
                float distance = math.distancesq(position, _volumes[candidate].Center);
                if (distance > best) continue;
                best = distance;
                index = candidate;
            }
            return index >= 0;
        }

        public PhaseTransitionResult ApplyEnergy(int index, in MaterialDefinition material, float energy)
        {
            WaterVolume volume = _volumes[index];
            PhaseState state = volume.State;
            PhaseTransitionResult result = PhaseTransitionMath.ApplyEnergy(in state, in material, energy);
            _volumes[index] = volume.WithState(result.State);
            _requestedEnergy += energy;
            _appliedEnergy += result.AppliedEnergy;
            return result;
        }

        public bool TransferMass(int sourceIndex, int targetIndex, float amount)
        {
            if (sourceIndex == targetIndex || sourceIndex < 0 || targetIndex < 0 ||
                sourceIndex >= _volumes.Count || targetIndex >= _volumes.Count || amount <= 0f)
            {
                return false;
            }
            WaterVolume source = _volumes[sourceIndex];
            WaterVolume target = _volumes[targetIndex];
            float moved = math.min(amount, source.State.Mass);
            _volumes[sourceIndex] = source.WithState(source.State.WithMass(source.State.Mass - moved));
            _volumes[targetIndex] = target.WithState(target.State.WithMass(target.State.Mass + moved));
            return moved > 0f;
        }

        public float3 ApplyPressureImpulse(int index, float3 direction, float impulse)
        {
            WaterVolume volume = _volumes[index];
            float3 velocityDelta = math.normalizesafe(direction) * math.clamp(impulse / math.max(0.01f, volume.State.Mass), 0f, 40f);
            _volumes[index] = volume.WithMotion(volume.Center, volume.Velocity + velocityDelta);
            return velocityDelta;
        }

        public int TickMotion(float deltaTime, int budget)
        {
            int count = math.min(_volumes.Count, math.max(0, budget));
            for (int index = 0; index < count; index++)
            {
                WaterVolume volume = _volumes[index];
                _volumes[index] = volume.WithMotion(volume.Center + (volume.Velocity * deltaTime), volume.Velocity * 0.985f);
            }
            return count;
        }

        private float ComputeMass()
        {
            float mass = 0f;
            for (int index = 0; index < _volumes.Count; index++) mass += _volumes[index].State.Mass;
            return mass;
        }
    }
}
