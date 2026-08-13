using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Materials;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class ThermalReactionFeedback : MonoBehaviour
    {
        [SerializeField] private ThermalWaterMagicExecutor executor;
        [SerializeField] private ParticleSystem steamBurst;
        [SerializeField] private Light heatLight;

        public void Configure(ThermalWaterMagicExecutor configuredExecutor, ParticleSystem configuredSteamBurst, Light configuredHeatLight)
        {
            executor = configuredExecutor; steamBurst = configuredSteamBurst; heatLight = configuredHeatLight;
        }

        private void OnEnable()
        {
            if (executor == null) return;
            executor.Events.PhaseChanged += OnPhaseChanged;
            executor.Events.ReactionTriggered += OnReaction;
        }

        private void OnDisable()
        {
            if (executor == null) return;
            executor.Events.PhaseChanged -= OnPhaseChanged;
            executor.Events.ReactionTriggered -= OnReaction;
        }

        private void OnPhaseChanged(PhaseChangedEvent value)
        {
            if (heatLight != null)
            {
                heatLight.intensity = value.Current == PhaseKind.Gas ? 2.5f : 0.5f;
            }
        }

        private void OnReaction(ReactionTriggeredEvent value)
        {
            if (steamBurst == null) return;
            float3 position = value.Position;
            steamBurst.transform.position = new Vector3(position.x, position.y, position.z);
            steamBurst.Emit(Mathf.Clamp(Mathf.RoundToInt(12f + (value.Severity * 30f)), 8, 48));
        }
    }
}
