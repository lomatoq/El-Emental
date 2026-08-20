using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using UnityEngine;
using UnityEngine.VFX;

namespace Elemental.Presentation.VFX
{
    /// <summary>
    /// Typed, visual-only adapter for optional VFX Graph assets. The graph receives
    /// consequences after simulation has committed; it never reports hits or edits
    /// canonical matter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthVfxGraphBridge : MonoBehaviour
    {
        private static readonly int PositionId = Shader.PropertyToID("ImpactPosition");
        private static readonly int NormalId = Shader.PropertyToID("ImpactNormal");
        private static readonly int EnergyId = Shader.PropertyToID("ImpactEnergy");
        private static readonly int CountId = Shader.PropertyToID("FragmentCount");

        [SerializeField] private MagicExecutor executor;
        [SerializeField] private VisualEffect impactGraph;
        [SerializeField] private VisualEffect returnGraph;

        public void Configure(MagicExecutor configuredExecutor, VisualEffect impact, VisualEffect earthReturn)
        {
            if (isActiveAndEnabled) Unsubscribe();
            executor = configuredExecutor;
            impactGraph = impact;
            returnGraph = earthReturn;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (executor == null) return;
            executor.Events.EarthImpactOccurred += OnImpact;
            executor.Events.EarthReturnOccurred += OnReturn;
        }

        private void Unsubscribe()
        {
            if (executor == null) return;
            executor.Events.EarthImpactOccurred -= OnImpact;
            executor.Events.EarthReturnOccurred -= OnReturn;
        }

        private void OnImpact(EarthImpactEvent value)
        {
            if (impactGraph == null || impactGraph.visualEffectAsset == null) return;
            SetVectorIfPresent(impactGraph, PositionId, value.Point);
            SetVectorIfPresent(impactGraph, NormalId, value.Normal);
            SetFloatIfPresent(impactGraph, EnergyId, value.KineticEnergy);
            SetIntIfPresent(impactGraph, CountId, Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Log10(1f + value.KineticEnergy) * 7f), 2, 32));
            impactGraph.Play();
        }

        private void OnReturn(EarthReturnEvent value)
        {
            if (returnGraph == null || returnGraph.visualEffectAsset == null) return;
            SetVectorIfPresent(returnGraph, PositionId, value.Position);
            SetFloatIfPresent(returnGraph, EnergyId, value.Mass);
            returnGraph.Play();
        }

        private static void SetVectorIfPresent(VisualEffect effect, int property, Unity.Mathematics.float3 value)
        {
            if (effect.HasVector3(property)) effect.SetVector3(property, new Vector3(value.x, value.y, value.z));
        }

        private static void SetFloatIfPresent(VisualEffect effect, int property, float value)
        {
            if (effect.HasFloat(property)) effect.SetFloat(property, value);
        }

        private static void SetIntIfPresent(VisualEffect effect, int property, int value)
        {
            if (effect.HasInt(property)) effect.SetInt(property, value);
        }
    }
}
