using System;
using Elemental.Simulation.Missions;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Missions
{
    [DisallowMultipleComponent]
    public sealed class MissionDirectorBehaviour : MonoBehaviour
    {
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("Elemental.MissionDirector.Tick");

        [SerializeField] private uint deterministicSeed = 0xC0FFEEu;
        [SerializeField] private MissionStrategyKind initialStrategy = MissionStrategyKind.EarthFortify;
        [SerializeField] private CivilianProxyBehaviour[] civilianViews;
        [SerializeField] private CrisisPresentationPool crisisPresentation;
        private float _accumulator;
        private int _lastTimelineCount;

        public MissionSimulation Simulation { get; private set; }
        public uint DeterministicSeed => deterministicSeed;
        public event Action<MissionOutcome, ScoreBreakdown> MissionFinished;

        public void Configure(uint seed, MissionStrategyKind strategy, CivilianProxyBehaviour[] views, CrisisPresentationPool presentation)
        {
            deterministicSeed = seed; initialStrategy = strategy; civilianViews = views; crisisPresentation = presentation;
            Rebuild();
        }

        public void SelectStrategy(MissionStrategyKind strategy)
        {
            initialStrategy = strategy;
            if (Simulation != null) Simulation.SetStrategy(Profile(strategy));
        }

        public void ApplyTerrainChange(bool opensRoute, bool damagesStructure) => Simulation?.ApplyTerrainChange(opensRoute, damagesStructure);

        private void Awake()
        {
            if (Simulation == null) Rebuild();
        }

        private void FixedUpdate()
        {
            if (Simulation == null || Simulation.Outcome != MissionOutcome.Running) return;
            _accumulator += Time.fixedDeltaTime;
            using (TickMarker.Auto())
            {
                while (_accumulator >= 0.1f)
                {
                    Simulation.Tick(0.1f);
                    _accumulator -= 0.1f;
                }
            }
            SynchronizePresentation();
            if (Simulation.Outcome != MissionOutcome.Running)
                MissionFinished?.Invoke(Simulation.Outcome, Simulation.BuildScore());
        }

        private void Rebuild()
        {
            MissionDefinition definition = MissionSimulation.VolcanoVillage(deterministicSeed);
            MissionStrategyProfile profile = Profile(initialStrategy);
            Simulation = new MissionSimulation(in definition, in profile);
            _lastTimelineCount = 0; _accumulator = 0f;
        }

        private void SynchronizePresentation()
        {
            if (civilianViews != null)
            {
                int count = Mathf.Min(civilianViews.Length, Simulation.CivilianCount);
                for (int index = 0; index < count; index++) civilianViews[index]?.Apply(Simulation.GetCivilian(index));
            }
            while (crisisPresentation != null && _lastTimelineCount < Simulation.Director.TimelineCount)
            {
                crisisPresentation.Show(Simulation.Director.GetTimeline(_lastTimelineCount++));
            }
        }

        private static MissionStrategyProfile Profile(MissionStrategyKind strategy)
        {
            switch (strategy)
            {
                case MissionStrategyKind.AirEvacuate: return MissionStrategyProfile.Air;
                case MissionStrategyKind.WaterCool: return MissionStrategyProfile.Water;
                default: return MissionStrategyProfile.Earth;
            }
        }
    }
}
