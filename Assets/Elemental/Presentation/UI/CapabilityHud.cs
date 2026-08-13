using Elemental.Runtime.Capabilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class CapabilityHud : MonoBehaviour
    {
        [SerializeField] private CapabilityRuntimeBehaviour runtime;
        private Label _profile; private Label _budgets; private Label _degradation; private Label _telemetry;
        public void Configure(CapabilityRuntimeBehaviour configuredRuntime) => runtime = configuredRuntime;
        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _profile = root.Q<Label>("profile"); _budgets = root.Q<Label>("budgets");
            _degradation = root.Q<Label>("degradation"); _telemetry = root.Q<Label>("telemetry");
        }
        private void Update()
        {
            if (runtime == null) return;
            if (_profile != null) _profile.text = $"{runtime.Profile.Kind} · WebGL2 baseline";
            if (_budgets != null) _budgets.text = $"chunks {runtime.Profile.Budgets.ActiveChunks} · fields {runtime.Profile.Budgets.FieldRegions} · fluid {runtime.Profile.Budgets.FluidProxies} · VFX {runtime.Profile.Budgets.VfxParticles}";
            if (_degradation != null) _degradation.text = $"{runtime.Decision.Kind} · {runtime.Decision.Reason}";
            if (_telemetry != null) _telemetry.text = $"startup {runtime.StartupSeconds:0.00}s · managed {runtime.MemoryMegabytes:0.0} MB · rejected {runtime.RejectedDistantWork}";
        }
    }
}
