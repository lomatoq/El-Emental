using System;

namespace Elemental.Simulation.Gravity
{
    public readonly struct GravityFieldId : IEquatable<GravityFieldId>
    {
        public GravityFieldId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }
        public bool IsValid => Value != 0u;

        public bool Equals(GravityFieldId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is GravityFieldId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(GravityFieldId left, GravityFieldId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GravityFieldId left, GravityFieldId right)
        {
            return !left.Equals(right);
        }
    }
}
