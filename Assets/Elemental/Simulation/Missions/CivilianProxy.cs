using Unity.Mathematics;

namespace Elemental.Simulation.Missions
{
    public enum CivilianRescueState : byte { Waiting, Evacuating, Rescued, Lost }

    public readonly struct CivilianProxy
    {
        public CivilianProxy(MissionEntityId id, float routeProgress, float danger, int routeIndex, CivilianRescueState state)
        {
            Id = id; RouteProgress = math.saturate(routeProgress); Danger = math.max(0f, danger);
            RouteIndex = math.clamp(routeIndex, 0, 1); State = state;
        }
        public MissionEntityId Id { get; }
        public float RouteProgress { get; }
        public float Danger { get; }
        public int RouteIndex { get; }
        public CivilianRescueState State { get; }

        public CivilianProxy Step(float deltaTime, float dangerRate, float speed, bool routeBlocked)
        {
            if (State == CivilianRescueState.Rescued || State == CivilianRescueState.Lost) return this;
            int route = routeBlocked ? 1 - RouteIndex : RouteIndex;
            float danger = math.max(0f, Danger + (dangerRate * deltaTime));
            if (danger >= 1.25f) return new CivilianProxy(Id, RouteProgress, danger, route, CivilianRescueState.Lost);
            float effectiveSpeed = speed * math.saturate(1f - (danger * 0.45f));
            float progress = math.saturate(RouteProgress + (effectiveSpeed * deltaTime));
            CivilianRescueState state = progress >= 1f ? CivilianRescueState.Rescued : CivilianRescueState.Evacuating;
            return new CivilianProxy(Id, progress, danger, route, state);
        }
    }

    public enum MissionStrategyKind : byte { EarthFortify, AirEvacuate, WaterCool }

    public readonly struct MissionStrategyProfile
    {
        public MissionStrategyProfile(MissionStrategyKind kind, float evacuationSpeed, float lavaMitigation, float smokeMitigation, float structureProtection, float routeClearing)
        {
            Kind = kind; EvacuationSpeed = evacuationSpeed; LavaMitigation = lavaMitigation;
            SmokeMitigation = smokeMitigation; StructureProtection = structureProtection; RouteClearing = routeClearing;
        }
        public MissionStrategyKind Kind { get; }
        public float EvacuationSpeed { get; }
        public float LavaMitigation { get; }
        public float SmokeMitigation { get; }
        public float StructureProtection { get; }
        public float RouteClearing { get; }

        public static MissionStrategyProfile Earth => new MissionStrategyProfile(MissionStrategyKind.EarthFortify, 0.026f, 0.35f, 0.2f, 0.9f, 0.9f);
        public static MissionStrategyProfile Air => new MissionStrategyProfile(MissionStrategyKind.AirEvacuate, 0.044f, 0.15f, 0.95f, 0.25f, 0.55f);
        public static MissionStrategyProfile Water => new MissionStrategyProfile(MissionStrategyKind.WaterCool, 0.034f, 0.95f, 0.45f, 0.55f, 0.85f);
    }
}
