using Elemental.Runtime.World;
using Elemental.Simulation.Materials;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class ElementLabHud : MonoBehaviour
    {
        [SerializeField] private ThermalWaterWorldBehaviour world;
        private Label _phase;
        private Label _mass;
        private Label _energy;
        private Label _budget;

        public void Configure(ThermalWaterWorldBehaviour configuredWorld) => world = configuredWorld;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _phase = root.Q<Label>("phase-value");
            _mass = root.Q<Label>("mass-value");
            _energy = root.Q<Label>("energy-value");
            _budget = root.Q<Label>("budget-value");
        }

        private void Update()
        {
            if (world == null || !world.IsReady || world.Water.Count == 0) return;
            WaterVolume volume = world.Water.GetVolume(0);
            ConservationTelemetry telemetry = world.Water.Telemetry;
            if (_phase != null) _phase.text = $"{volume.State.Phase} · {volume.State.Temperature:0.0} °C";
            if (_mass != null) _mass.text = $"{telemetry.CurrentMass:0.000} kg · error {telemetry.MassError:+0.000;-0.000;0.000}";
            if (_energy != null) _energy.text = $"energy error {telemetry.EnergyError:+0.00;-0.00;0.00} kJ";
            if (_budget != null) _budget.text = $"thermal {world.Thermal.Count} · water {world.Water.Count} · debt {world.Thermal.DeferredUpdateCount}";
        }
    }
}
