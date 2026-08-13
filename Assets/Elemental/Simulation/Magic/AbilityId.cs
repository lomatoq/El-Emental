using System;

namespace Elemental.Simulation.Magic
{
    public readonly struct AbilityId : IEquatable<AbilityId>
    {
        public AbilityId(ushort value)
        {
            Value = value;
        }

        public ushort Value { get; }
        public bool IsValid => Value != 0;

        public bool Equals(AbilityId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is AbilityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(AbilityId left, AbilityId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AbilityId left, AbilityId right)
        {
            return !left.Equals(right);
        }
    }

    public static class EarthAbilityIds
    {
        public static readonly AbilityId LineWall = new AbilityId(1);
        public static readonly AbilityId PullRock = new AbilityId(2);
        public static readonly AbilityId FlickThrow = new AbilityId(3);
        // Internal verbs selected by the unified input grammar. They intentionally do
        // not occupy hotbar slots.
        public static readonly AbilityId RaisePlatform = new AbilityId(4);
        public static readonly AbilityId VectorFieldPush = new AbilityId(5);
        public static readonly AbilityId LandingCushion = new AbilityId(6);
    }
}
