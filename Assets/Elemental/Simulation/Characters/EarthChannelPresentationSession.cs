using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>Presentation admission token only; never owns fire lifetime or gameplay.</summary>
    public struct EarthChannelPresentationSession
    {
        public uint Generation { get; private set; }
        public bool Active { get; private set; }
        public float3 Focus { get; private set; }
        public bool Begin(uint generation, float3 focus)
        {
            if (generation == 0 || !math.all(math.isfinite(focus))) return false;
            if (generation == Generation) return Update(generation, focus);
            // Sequence arithmetic permits wrapping, but rejects delayed prior-life commands.
            if (Generation != 0 && unchecked((int)(generation - Generation)) <= 0) return false;
            Generation = generation; Focus = focus; Active = true; return true;
        }
        public bool Update(uint generation, float3 focus)
        {
            if (!Active || generation != Generation || !math.all(math.isfinite(focus))) return false;
            Focus = focus; return true;
        }
        public bool End(uint generation)
        {
            if (!Active || generation != Generation) return false;
            Active = false; return true;
        }
        public void Cancel() => Active = false;
    }
}
