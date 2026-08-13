namespace Elemental.Core.Math
{
    public struct DeterministicRandom
    {
        private const uint ZeroSeedReplacement = 0x6D2B79F5u;
        private uint _state;

        public DeterministicRandom(uint seed)
        {
            _state = seed == 0u ? ZeroSeedReplacement : seed;
        }

        public uint State => _state;

        public uint NextUInt()
        {
            uint value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }

        public float NextFloat01()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }
    }
}
