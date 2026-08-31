using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    public enum EarthWorldResponseKind : byte
    {
        CharacterImpact = 0,
        Knockdown = 1,
        Knockout = 2
    }

    /// <summary>
    /// One presentation fact produced after gameplay damage/outcome resolution.
    /// Consumers may fan it out to dust, debris, scar, audio and camera, but must
    /// never feed it back into damage or outcome authority.
    /// </summary>
    public readonly struct EarthWorldResponseEvent
    {
        public EarthWorldResponseEvent(
            uint responseId,
            uint tick,
            uint sourceStableId,
            uint targetStableId,
            EarthWorldResponseKind kind,
            EarthCharacterImpactSourceKind sourceKind,
            EarthCharacterImpactResponse response,
            float3 point,
            float3 normal,
            float3 direction,
            float impulse,
            float kineticEnergy,
            float intensity01)
        {
            ResponseId = responseId != 0u ? responseId : 1u;
            Tick = tick;
            SourceStableId = sourceStableId;
            TargetStableId = targetStableId;
            Kind = kind;
            SourceKind = sourceKind;
            Response = response;
            Point = point;
            Normal = math.normalizesafe(normal, new float3(0f, 1f, 0f));
            Direction = math.normalizesafe(direction, Normal);
            Impulse = math.max(0f, impulse);
            KineticEnergy = math.max(0f, kineticEnergy);
            Intensity01 = math.saturate(intensity01);
        }

        public uint ResponseId { get; }
        public uint Tick { get; }
        public uint SourceStableId { get; }
        public uint TargetStableId { get; }
        public EarthWorldResponseKind Kind { get; }
        public EarthCharacterImpactSourceKind SourceKind { get; }
        public EarthCharacterImpactResponse Response { get; }
        public float3 Point { get; }
        public float3 Normal { get; }
        public float3 Direction { get; }
        public float Impulse { get; }
        public float KineticEnergy { get; }
        public float Intensity01 { get; }
    }

    public static class EarthWorldResponseId
    {
        public static uint Compose(
            uint targetStableId,
            uint sourceStableId,
            uint tick,
            EarthCharacterImpactResponse response)
        {
            uint value = targetStableId ^ RotateLeft(sourceStableId, 11) ^
                         RotateLeft(tick, 21) ^ ((uint)response * 0x9E3779B9u);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value != 0u ? value : 1u;
        }

        private static uint RotateLeft(uint value, int bits) =>
            (value << bits) | (value >> (32 - bits));
    }
}
