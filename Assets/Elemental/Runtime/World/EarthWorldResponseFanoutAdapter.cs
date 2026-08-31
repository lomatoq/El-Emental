using Elemental.Simulation.Combat;
using Elemental.Simulation.Magic;

namespace Elemental.Runtime.World
{
    /// <summary>
    /// Scoped presentation fan-out. It maps one canonical response id into the
    /// existing EarthImpactOccurred stream consumed by dust, indirect debris,
    /// decals, audio and camera. It never calls a damage target or gameplay solver.
    /// </summary>
    public sealed class EarthWorldResponseFanoutAdapter
    {
        private const int SeenCapacity = 16;
        private readonly MagicWorldEvents _events;
        private readonly uint[] _seen = new uint[SeenCapacity];
        private int _cursor;

        public EarthWorldResponseFanoutAdapter(MagicWorldEvents events)
        {
            _events = events;
        }

        public bool Publish(in EarthWorldResponseEvent response)
        {
            if (_events == null || response.ResponseId == 0u || WasSeen(response.ResponseId))
                return false;
            _seen[_cursor] = response.ResponseId;
            _cursor = (_cursor + 1) % SeenCapacity;
            _events.Emit(new EarthImpactEvent(
                response.Tick,
                // Every presentation consumer sees the same response id. The
                // gameplay source id remains available on the canonical event.
                response.ResponseId,
                response.Impulse,
                response.KineticEnergy,
                0f,
                0f,
                response.Point,
                response.Normal,
                ResolveMaterial(response.SourceKind)));
            return true;
        }

        private bool WasSeen(uint responseId)
        {
            for (int index = 0; index < SeenCapacity; index++)
                if (_seen[index] == responseId) return true;
            return false;
        }

        private static EarthImpactMaterialKind ResolveMaterial(
            EarthCharacterImpactSourceKind source) => source switch
        {
            EarthCharacterImpactSourceKind.LooseStone or
                EarthCharacterImpactSourceKind.ArmorProjectile or
                EarthCharacterImpactSourceKind.BotProjectile or
                EarthCharacterImpactSourceKind.StonePunch => EarthImpactMaterialKind.HeavyBlock,
            EarthCharacterImpactSourceKind.PillarWave or
                EarthCharacterImpactSourceKind.PillarCrest or
                EarthCharacterImpactSourceKind.SurfNose => EarthImpactMaterialKind.Structure,
            _ => EarthImpactMaterialKind.Terrain
        };
    }
}
