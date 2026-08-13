using Elemental.Runtime.World;
using Elemental.Simulation.Materials;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class WaterVolumeVisualProxy : MonoBehaviour
    {
        [SerializeField] private ThermalWaterWorldBehaviour world;
        [SerializeField] private uint volumeId;
        [SerializeField] private MeshRenderer targetRenderer;
        [SerializeField] private ParticleSystem phaseParticles;
        [SerializeField] private Material liquidMaterial;
        [SerializeField] private Material iceMaterial;
        [SerializeField] private Material steamMaterial;

        public PhaseKind LastPhase { get; private set; }

        public void Configure(
            ThermalWaterWorldBehaviour configuredWorld,
            WaterVolumeId configuredId,
            MeshRenderer configuredRenderer,
            ParticleSystem configuredParticles,
            Material configuredLiquid,
            Material configuredIce,
            Material configuredSteam)
        {
            world = configuredWorld; volumeId = configuredId.Value; targetRenderer = configuredRenderer;
            phaseParticles = configuredParticles; liquidMaterial = configuredLiquid;
            iceMaterial = configuredIce; steamMaterial = configuredSteam;
        }

        private void LateUpdate()
        {
            if (world == null || !world.IsReady) return;
            for (int index = 0; index < world.Water.Count; index++)
            {
                WaterVolume volume = world.Water.GetVolume(index);
                if (volume.Id.Value != volumeId) continue;
                transform.position = ToVector3(volume.Center);
                float massScale = Mathf.Max(0.15f, Mathf.Pow(Mathf.Max(0f, volume.State.Mass), 1f / 3f));
                transform.localScale = Vector3.one * volume.Radius * 2f * massScale;
                ApplyPhase(volume.State.Phase);
                return;
            }
        }

        private void ApplyPhase(PhaseKind phase)
        {
            if (targetRenderer != null)
            {
                targetRenderer.sharedMaterial = phase == PhaseKind.Solid ? iceMaterial :
                    phase == PhaseKind.Gas ? steamMaterial : liquidMaterial;
                targetRenderer.enabled = phase != PhaseKind.Gas;
            }
            if (phaseParticles != null)
            {
                ParticleSystem.EmissionModule emission = phaseParticles.emission;
                emission.rateOverTime = phase == PhaseKind.Gas ? 35f : phase == PhaseKind.Liquid ? 4f : 0f;
            }
            LastPhase = phase;
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
