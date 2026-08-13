using System;

namespace Elemental.Core.IDs
{
    public readonly struct ActorId : IEquatable<ActorId>
    {
        public ActorId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }
        public bool IsValid => Value != 0u;

        public bool Equals(ActorId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ActorId other && Equals(other);
        public override int GetHashCode() => unchecked((int)Value);
        public static bool operator ==(ActorId left, ActorId right) => left.Equals(right);
        public static bool operator !=(ActorId left, ActorId right) => !left.Equals(right);
    }
}
