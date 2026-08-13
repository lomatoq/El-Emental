using System;

namespace Elemental.Core.Time
{
    public readonly struct SimulationTick : IEquatable<SimulationTick>, IComparable<SimulationTick>
    {
        public SimulationTick(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public SimulationTick Next()
        {
            return new SimulationTick(unchecked(Value + 1u));
        }

        public int CompareTo(SimulationTick other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(SimulationTick other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is SimulationTick other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(SimulationTick left, SimulationTick right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SimulationTick left, SimulationTick right)
        {
            return !left.Equals(right);
        }
    }
}
