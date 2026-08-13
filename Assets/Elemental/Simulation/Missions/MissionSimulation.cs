using Unity.Mathematics;

namespace Elemental.Simulation.Missions
{
    public sealed class MissionSimulation
    {
        private readonly MissionDefinition _definition;
        private readonly CivilianProxy[] _civilians;
        private MissionStrategyProfile _strategy;
        private float _structureIntegrity = 1f;
        private float _elapsed;
        private uint _tick;

        public MissionSimulation(in MissionDefinition definition, in MissionStrategyProfile strategy)
        {
            _definition = definition; _strategy = strategy;
            Director = new CrisisDirector(in definition);
            _civilians = new CivilianProxy[definition.CivilianCount];
            for (int index = 0; index < _civilians.Length; index++)
                _civilians[index] = new CivilianProxy(new MissionEntityId((uint)(1000 + index)), 0f, 0f, index % 2, CivilianRescueState.Waiting);
            Objectives = new ObjectiveGraph(new[]
            {
                new ObjectiveNode(1, 0, definition.RequiredRescues, true),
                new ObjectiveNode(2, 0, 1, true),
                new ObjectiveNode(3, 1, 1, false)
            });
        }

        public CrisisDirector Director { get; }
        public ObjectiveGraph Objectives { get; }
        public MissionOutcome Outcome { get; private set; }
        public int RescuedCount { get; private set; }
        public int LostCount { get; private set; }
        public float Elapsed => _elapsed;
        public float StructureIntegrity => _structureIntegrity;
        public MissionStrategyKind Strategy => _strategy.Kind;
        public CivilianProxy GetCivilian(int index) => _civilians[index];
        public int CivilianCount => _civilians.Length;

        public void SetStrategy(in MissionStrategyProfile strategy) => _strategy = strategy;

        public void ApplyTerrainChange(bool opensRoute, bool damagesStructure)
        {
            if (opensRoute) Objectives.SetProgress(3, 1);
            if (damagesStructure) _structureIntegrity = math.max(0f, _structureIntegrity - 0.2f);
        }

        public void Tick(float deltaTime)
        {
            if (Outcome != MissionOutcome.Running) return;
            _elapsed += deltaTime;
            Director.Tick(_tick++, _elapsed, deltaTime, 2);
            float lava = math.max(0f, Director.Severity(CrisisKind.LavaAdvance) - _strategy.LavaMitigation);
            float smoke = math.max(0f, Director.Severity(CrisisKind.SmokeHazard) - _strategy.SmokeMitigation);
            float panic = Director.Severity(CrisisKind.CivilianPanic) * 0.3f;
            bool blocked = Director.Severity(CrisisKind.BlockedRoute) > _strategy.RouteClearing;
            float structural = math.max(0f, Director.Severity(CrisisKind.StructuralFailure) - _strategy.StructureProtection);
            _structureIntegrity = math.max(0f, _structureIntegrity - (structural * deltaTime * 0.012f));
            float dangerRate = (lava * 0.026f) + (smoke * 0.02f) + (panic * 0.015f) - 0.004f;
            RescuedCount = 0; LostCount = 0;
            for (int index = 0; index < _civilians.Length; index++)
            {
                _civilians[index] = _civilians[index].Step(deltaTime, dangerRate, _strategy.EvacuationSpeed, blocked && _civilians[index].RouteIndex == 0);
                if (_civilians[index].State == CivilianRescueState.Rescued) RescuedCount++;
                else if (_civilians[index].State == CivilianRescueState.Lost) LostCount++;
            }
            Objectives.SetProgress(1, RescuedCount);
            Objectives.SetProgress(2, _structureIntegrity >= 0.25f ? 1 : 0);
            if (RescuedCount >= _definition.RequiredRescues && Objectives.RequiredComplete) Outcome = MissionOutcome.Win;
            else if (_elapsed >= _definition.Duration || RescuedCount + LostCount == _civilians.Length)
                Outcome = RescuedCount > 0 ? MissionOutcome.PartialSuccess : MissionOutcome.Failure;
        }

        public ScoreBreakdown BuildScore()
        {
            ScoreRule rule = _definition.ScoreRule;
            int rescued = RescuedCount * rule.RescuedPoints;
            int lost = LostCount * rule.LostPenalty;
            int structures = (int)math.round(_structureIntegrity * rule.StructurePoints);
            int time = Outcome == MissionOutcome.Win
                ? (int)math.round(math.max(0f, _definition.Duration - _elapsed)) * rule.TimeBonusPerSecond
                : 0;
            return new ScoreBreakdown(rescued, lost, structures, time, rescued - lost + structures + time);
        }

        public static MissionDefinition VolcanoVillage(uint seed = 0xC0FFEEu)
        {
            return new MissionDefinition(
                seed, 90f, 12, 8, 12,
                new EscalationCurve(0.18f, 0.85f, 90f),
                new[]
                {
                    new SpawnRule(CrisisKind.LavaAdvance, 7f, 2, 220),
                    new SpawnRule(CrisisKind.StructuralFailure, 11f, 2, 200),
                    new SpawnRule(CrisisKind.SmokeHazard, 6f, 3, 170),
                    new SpawnRule(CrisisKind.CivilianPanic, 9f, 2, 150),
                    new SpawnRule(CrisisKind.BlockedRoute, 13f, 1, 190),
                    new SpawnRule(CrisisKind.TimedEvacuation, 18f, 1, 240)
                },
                new ScoreRule(100, 80, 500, 4));
        }
    }
}
