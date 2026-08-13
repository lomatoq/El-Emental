using Elemental.Runtime.Missions;
using Elemental.Simulation.Missions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class MissionHud : MonoBehaviour
    {
        [SerializeField] private MissionDirectorBehaviour director;
        private Label _status;
        private Label _civilians;
        private Label _crisis;
        private Label _strategy;
        private Label _timeline;

        public void Configure(MissionDirectorBehaviour configuredDirector) => director = configuredDirector;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _status = root.Q<Label>("mission-status"); _civilians = root.Q<Label>("civilian-status");
            _crisis = root.Q<Label>("crisis-status"); _strategy = root.Q<Label>("strategy-status");
            _timeline = root.Q<Label>("timeline-status");
            root.Q<Button>("earth-strategy")?.RegisterCallback<ClickEvent>(_ => director?.SelectStrategy(MissionStrategyKind.EarthFortify));
            root.Q<Button>("air-strategy")?.RegisterCallback<ClickEvent>(_ => director?.SelectStrategy(MissionStrategyKind.AirEvacuate));
            root.Q<Button>("water-strategy")?.RegisterCallback<ClickEvent>(_ => director?.SelectStrategy(MissionStrategyKind.WaterCool));
        }

        private void Update()
        {
            MissionSimulation mission = director?.Simulation;
            if (mission == null) return;
            if (_status != null) _status.text = $"{mission.Outcome} · {mission.Elapsed:0.0}s · structure {mission.StructureIntegrity:P0}";
            if (_civilians != null) _civilians.text = $"rescued {mission.RescuedCount}/{mission.CivilianCount} · lost {mission.LostCount}";
            if (_crisis != null) _crisis.text = $"active {mission.Director.ActiveCount}/12 · deferred {mission.Director.DeferredSpawnCount}";
            if (_strategy != null) _strategy.text = mission.Strategy.ToString();
            if (_timeline != null) _timeline.text = $"seed {director.DeterministicSeed} · events {mission.Director.TimelineCount}";
        }
    }
}
