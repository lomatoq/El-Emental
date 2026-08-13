using Elemental.Simulation.Fields;
using Unity.Mathematics;

namespace Elemental.Simulation.Magic
{
    public readonly struct WallRaisedEvent
    {
        public WallRaisedEvent(uint tick, uint wallId, float3 start, float3 end, float height, float thickness)
        {
            Tick = tick;
            WallId = wallId;
            Start = start;
            End = end;
            Height = height;
            Thickness = thickness;
        }

        public uint Tick { get; }
        public uint WallId { get; }
        public float3 Start { get; }
        public float3 End { get; }
        public float Height { get; }
        public float Thickness { get; }
    }

    public readonly struct WallCollapsedEvent
    {
        public WallCollapsedEvent(uint tick, uint wallId, float3 start, float3 end, float height)
        {
            Tick = tick;
            WallId = wallId;
            Start = start;
            End = end;
            Height = height;
        }

        public uint Tick { get; }
        public uint WallId { get; }
        public float3 Start { get; }
        public float3 End { get; }
        public float Height { get; }
    }

    public readonly struct TerrainEditedEvent
    {
        public TerrainEditedEvent(uint tick, AbilityId ability, float3 center, float radius)
        {
            Tick = tick;
            Ability = ability;
            Center = center;
            Radius = radius;
        }

        public uint Tick { get; }
        public AbilityId Ability { get; }
        public float3 Center { get; }
        public float Radius { get; }
    }

    public readonly struct FragmentSpawnedEvent
    {
        public FragmentSpawnedEvent(
            uint tick,
            uint fragmentId,
            float mass,
            float3 position,
            float3 surfaceAnchor,
            float3 sourceCenter,
            float radius)
        {
            Tick = tick;
            FragmentId = fragmentId;
            Mass = mass;
            Position = position;
            SurfaceAnchor = surfaceAnchor;
            SourceCenter = sourceCenter;
            Radius = radius;
        }

        public uint Tick { get; }
        public uint FragmentId { get; }
        public float Mass { get; }
        public float3 Position { get; }
        public float3 SurfaceAnchor { get; }
        public float3 SourceCenter { get; }
        public float Radius { get; }
    }

    public readonly struct FragmentLaunchedEvent
    {
        public FragmentLaunchedEvent(
            uint tick,
            uint fragmentId,
            float mass,
            float3 position,
            float3 direction,
            float velocityChange)
        {
            Tick = tick;
            FragmentId = fragmentId;
            Mass = mass;
            Position = position;
            Direction = direction;
            VelocityChange = velocityChange;
        }

        public uint Tick { get; }
        public uint FragmentId { get; }
        public float Mass { get; }
        public float3 Position { get; }
        public float3 Direction { get; }
        public float VelocityChange { get; }
    }

    public readonly struct EarthBodyGrabbedEvent
    {
        public EarthBodyGrabbedEvent(uint tick, uint bodyId, float mass, float3 position)
        {
            Tick = tick; BodyId = bodyId; Mass = mass; Position = position;
        }
        public uint Tick { get; }
        public uint BodyId { get; }
        public float Mass { get; }
        public float3 Position { get; }
    }

    public readonly struct EarthBodyReleasedEvent
    {
        public EarthBodyReleasedEvent(uint tick, uint bodyId, float mass, float3 velocity)
        {
            Tick = tick; BodyId = bodyId; Mass = mass; Velocity = velocity;
        }
        public uint Tick { get; }
        public uint BodyId { get; }
        public float Mass { get; }
        public float3 Velocity { get; }
    }

    public readonly struct MagicPreviewMetrics
    {
        public MagicPreviewMetrics(AbilityId ability, float radius, float estimatedMass)
        {
            Ability = ability;
            Radius = radius;
            EstimatedMass = estimatedMass;
        }

        public AbilityId Ability { get; }
        public float Radius { get; }
        public float EstimatedMass { get; }
    }

    public readonly struct ImpactEvent
    {
        public ImpactEvent(uint tick, uint fragmentId, float impulse, float3 point, float3 normal)
        {
            Tick = tick;
            FragmentId = fragmentId;
            Impulse = impulse;
            Point = point;
            Normal = normal;
        }

        public uint Tick { get; }
        public uint FragmentId { get; }
        public float Impulse { get; }
        public float3 Point { get; }
        public float3 Normal { get; }
    }

    public enum EarthImpactMaterialKind : byte
    {
        LooseStone = 0,
        HeavyBlock = 1,
        Structure = 2,
        Meteor = 3,
        Terrain = 4
    }

    public readonly struct EarthImpactEvent
    {
        public EarthImpactEvent(uint tick, uint sourceId, float impulse, float kineticEnergy, float mass, float relativeSpeed, float3 point, float3 normal, EarthImpactMaterialKind material)
        {
            Tick = tick;
            SourceId = sourceId;
            Impulse = math.max(0f, impulse);
            KineticEnergy = math.max(0f, kineticEnergy);
            Mass = math.max(0f, mass);
            RelativeSpeed = math.max(0f, relativeSpeed);
            Point = point;
            Normal = math.normalizesafe(normal, new float3(0f, 1f, 0f));
            Material = material;
        }

        public uint Tick { get; }
        public uint SourceId { get; }
        public float Impulse { get; }
        public float KineticEnergy { get; }
        public float Mass { get; }
        public float RelativeSpeed { get; }
        public float3 Point { get; }
        public float3 Normal { get; }
        public EarthImpactMaterialKind Material { get; }
    }

    public readonly struct MeteorImpactEvent
    {
        public MeteorImpactEvent(uint tick, uint meteorId, float3 point, float3 normal, float radius, float impulse, float craterRadius)
        {
            Tick = tick;
            MeteorId = meteorId;
            Point = point;
            Normal = math.normalizesafe(normal, new float3(0f, 1f, 0f));
            Radius = math.max(0f, radius);
            Impulse = math.max(0f, impulse);
            CraterRadius = math.max(0f, craterRadius);
        }

        public uint Tick { get; }
        public uint MeteorId { get; }
        public float3 Point { get; }
        public float3 Normal { get; }
        public float Radius { get; }
        public float Impulse { get; }
        public float CraterRadius { get; }
    }

    public readonly struct MagicPushEvent
    {
        public MagicPushEvent(uint tick, float3 point, float charge, float targetMass, float velocityChange, bool wall)
        {
            Tick = tick;
            Point = point;
            Charge = charge;
            TargetMass = targetMass;
            VelocityChange = velocityChange;
            Wall = wall;
        }

        public uint Tick { get; }
        public float3 Point { get; }
        public float Charge { get; }
        public float TargetMass { get; }
        public float VelocityChange { get; }
        public bool Wall { get; }
    }

    public readonly struct AbilityRejectedEvent
    {
        public AbilityRejectedEvent(uint tick, AbilityId ability, string reason)
        {
            Tick = tick;
            Ability = ability;
            Reason = reason;
        }

        public uint Tick { get; }
        public AbilityId Ability { get; }
        public string Reason { get; }
    }

    public readonly struct FieldSpawnedEvent
    {
        public FieldSpawnedEvent(uint tick, AbilityId ability, FieldRegion region)
        {
            Tick = tick;
            Ability = ability;
            Region = region;
        }

        public uint Tick { get; }
        public AbilityId Ability { get; }
        public FieldRegion Region { get; }
    }

    public readonly struct PhaseChangedEvent
    {
        public PhaseChangedEvent(uint tick, uint volumeId, Materials.PhaseKind previous, Materials.PhaseKind current, float temperature, float mass)
        {
            Tick = tick; VolumeId = volumeId; Previous = previous; Current = current;
            Temperature = temperature; Mass = mass;
        }
        public uint Tick { get; }
        public uint VolumeId { get; }
        public Materials.PhaseKind Previous { get; }
        public Materials.PhaseKind Current { get; }
        public float Temperature { get; }
        public float Mass { get; }
    }

    public readonly struct ReactionTriggeredEvent
    {
        public ReactionTriggeredEvent(uint tick, Materials.ReactionKind reaction, float3 position, float severity, float pressureImpulse)
        {
            Tick = tick; Reaction = reaction; Position = position; Severity = severity; PressureImpulse = pressureImpulse;
        }
        public uint Tick { get; }
        public Materials.ReactionKind Reaction { get; }
        public float3 Position { get; }
        public float Severity { get; }
        public float PressureImpulse { get; }
    }
}
