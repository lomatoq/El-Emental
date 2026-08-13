using Elemental.Simulation.Materials;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.World
{
    [DisallowMultipleComponent]
    public sealed class WaterVolumeBootstrap : MonoBehaviour
    {
        [SerializeField] private ThermalWaterWorldBehaviour world;
        [SerializeField] private Vector3[] centers;
        [SerializeField] private float[] radii;
        [SerializeField] private float[] masses;
        [SerializeField] private float[] temperatures;
        [SerializeField] private uint owner = 91u;

        public void Configure(ThermalWaterWorldBehaviour configuredWorld, WaterVolume[] volumes)
        {
            world = configuredWorld;
            int count = volumes?.Length ?? 0;
            centers = new Vector3[count]; radii = new float[count]; masses = new float[count]; temperatures = new float[count];
            for (int index = 0; index < count; index++)
            {
                WaterVolume volume = volumes[index];
                centers[index] = new Vector3(volume.Center.x, volume.Center.y, volume.Center.z);
                radii[index] = volume.Radius; masses[index] = volume.State.Mass; temperatures[index] = volume.State.Temperature;
                owner = volume.Owner;
            }
        }

        private void Awake()
        {
            if (world == null || !world.IsReady || centers == null) return;
            MaterialDefinition water = MaterialDefinition.Water;
            for (int index = 0; index < centers.Length; index++)
            {
                Vector3 center = centers[index];
                WaterVolume volume = new WaterVolume(
                    new WaterVolumeId((uint)(index + 1)), owner,
                    new float3(center.x, center.y, center.z), float3.zero,
                    radii[index], new PhaseState(water.Id, PhaseKind.Liquid, temperatures[index], masses[index]));
                world.Water.Register(in volume);
            }
        }
    }
}
