using Elemental.Simulation.Characters;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Voxel;
using Unity.Mathematics;

namespace Elemental.Simulation.Networking
{
    public readonly struct NetworkPeerId
    {
        public NetworkPeerId(byte value) => Value = value;
        public byte Value { get; }
        public bool IsValid => Value != 0;
    }

    public enum CommandDecisionKind : byte { Accepted, Corrected, Rejected }

    public readonly struct CommandDecision
    {
        public CommandDecision(uint sequence, uint clientCommandTick, CommandDecisionKind kind, uint authoritativeTick, string reason)
        {
            Sequence = sequence; ClientCommandTick = clientCommandTick; Kind = kind;
            AuthoritativeTick = authoritativeTick; Reason = reason;
        }
        public uint Sequence { get; }
        public uint ClientCommandTick { get; }
        public CommandDecisionKind Kind { get; }
        public uint AuthoritativeTick { get; }
        public string Reason { get; }
    }

    public readonly struct TerrainEditReplication
    {
        public TerrainEditReplication(uint authoritySequence, uint authoritativeTick, SdfEdit edit, uint chunkVersion, ulong chunkHash)
        {
            AuthoritySequence = authoritySequence; AuthoritativeTick = authoritativeTick; Edit = edit;
            ChunkVersion = chunkVersion; ChunkHash = chunkHash;
        }
        public uint AuthoritySequence { get; }
        public uint AuthoritativeTick { get; }
        public SdfEdit Edit { get; }
        public uint ChunkVersion { get; }
        public ulong ChunkHash { get; }
    }

    public readonly struct RigidbodySnapshot
    {
        public RigidbodySnapshot(uint entityId, uint tick, float3 position, quaternion rotation, float3 velocity, float3 angularVelocity, byte priority)
        {
            EntityId = entityId; Tick = tick; Position = position; Rotation = rotation;
            Velocity = velocity; AngularVelocity = angularVelocity; Priority = priority;
        }
        public uint EntityId { get; }
        public uint Tick { get; }
        public float3 Position { get; }
        public quaternion Rotation { get; }
        public float3 Velocity { get; }
        public float3 AngularVelocity { get; }
        public byte Priority { get; }
    }

    public readonly struct CharacterSnapshot
    {
        public CharacterSnapshot(uint actorId, uint tick, float3 rootPosition, quaternion rootRotation, CharacterPhysicalMode mode, float3 chestError, float3 headError, bool fullPose)
        {
            ActorId = actorId; Tick = tick; RootPosition = rootPosition; RootRotation = rootRotation;
            Mode = mode; ChestError = chestError; HeadError = headError; FullPose = fullPose;
        }
        public uint ActorId { get; }
        public uint Tick { get; }
        public float3 RootPosition { get; }
        public quaternion RootRotation { get; }
        public CharacterPhysicalMode Mode { get; }
        public float3 ChestError { get; }
        public float3 HeadError { get; }
        public bool FullPose { get; }
    }

    public readonly struct RegionStateSnapshot
    {
        public RegionStateSnapshot(uint regionId, uint tick, float3 center, float3 vector, float scalar, float lifetime, byte kind)
        {
            RegionId = regionId; Tick = tick; Center = center; Vector = vector;
            Scalar = scalar; Lifetime = lifetime; Kind = kind;
        }
        public uint RegionId { get; }
        public uint Tick { get; }
        public float3 Center { get; }
        public float3 Vector { get; }
        public float Scalar { get; }
        public float Lifetime { get; }
        public byte Kind { get; }
    }

    public readonly struct ObjectiveSnapshot
    {
        public ObjectiveSnapshot(uint tick, ushort objectiveId, byte state, int progress, int target)
        {
            Tick = tick; ObjectiveId = objectiveId; State = state; Progress = progress; Target = target;
        }
        public uint Tick { get; }
        public ushort ObjectiveId { get; }
        public byte State { get; }
        public int Progress { get; }
        public int Target { get; }
    }

    public readonly struct RelevanceFacts
    {
        public RelevanceFacts(float distance, bool samePlanetSide, bool lineOfSight, bool objectiveImportant, bool impendingCollision)
        {
            Distance = distance; SamePlanetSide = samePlanetSide; LineOfSight = lineOfSight;
            ObjectiveImportant = objectiveImportant; ImpendingCollision = impendingCollision;
        }
        public float Distance { get; }
        public bool SamePlanetSide { get; }
        public bool LineOfSight { get; }
        public bool ObjectiveImportant { get; }
        public bool ImpendingCollision { get; }
    }

    public static class RelevanceScorer
    {
        public static float Score(in RelevanceFacts facts)
        {
            float distanceScore = math.saturate(1f - (facts.Distance / 100f));
            return distanceScore + (facts.SamePlanetSide ? 0.35f : 0f) + (facts.LineOfSight ? 0.3f : 0f) +
                (facts.ObjectiveImportant ? 1f : 0f) + (facts.ImpendingCollision ? 2f : 0f);
        }
    }
}
