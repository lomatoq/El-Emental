using Elemental.Runtime.World;
using Elemental.Simulation.Materials;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WaterPhaseCollider : MonoBehaviour
    {
        [SerializeField] private ThermalWaterWorldBehaviour world;
        [SerializeField] private uint volumeId;
        [SerializeField] private BoxCollider targetCollider;

        public void Configure(ThermalWaterWorldBehaviour configuredWorld, WaterVolumeId id, BoxCollider configuredCollider)
        {
            world = configuredWorld; volumeId = id.Value; targetCollider = configuredCollider;
        }

        private void Awake()
        {
            if (targetCollider == null) targetCollider = GetComponent<BoxCollider>();
        }

        private void FixedUpdate()
        {
            if (world == null || !world.IsReady) return;
            for (int index = 0; index < world.Water.Count; index++)
            {
                WaterVolume volume = world.Water.GetVolume(index);
                if (volume.Id.Value != volumeId) continue;
                bool solid = volume.State.Phase == PhaseKind.Solid;
                targetCollider.enabled = solid;
                transform.position = ToVector3(volume.Center);
                transform.rotation = Quaternion.LookRotation(
                    math.lengthsq(volume.Velocity) > 0.01f ? ToVector3(math.normalizesafe(volume.Velocity)) : Vector3.forward,
                    Vector3.up);
                targetCollider.size = new Vector3(volume.Radius * 6f, volume.Radius * 0.7f, volume.Radius * 2f);
                return;
            }
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
