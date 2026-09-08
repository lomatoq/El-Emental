namespace Elemental.Online
{
    /// <summary>Host simulation ticks; replicas acknowledge observed time, never invent host progress.</summary>
    public sealed class OnlineAuthorityClock
    {
        public uint Tick { get; private set; } = 1;
        public void Reset(uint tick) => Tick = tick;
        public void Advance(bool authority) { if (authority) ++Tick; }
        public void Observe(uint tick) { if (tick > Tick) Tick = tick; }
    }
}
