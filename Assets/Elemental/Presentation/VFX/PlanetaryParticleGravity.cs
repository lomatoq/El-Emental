using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class PlanetaryParticleGravity : MonoBehaviour
    {
        private static readonly ProfilerMarker UpdateMarker =
            new ProfilerMarker("Elemental.VFX.PlanetaryParticleGravity");

        [SerializeField] private Transform planetCenter;
        [SerializeField] private ParticleSystem[] systems;
        [SerializeField] private float[] accelerations;
        private ParticleSystem.Particle[][] _buffers;

        public void Configure(
            Transform configuredPlanetCenter,
            ParticleSystem[] configuredSystems,
            float[] configuredAccelerations)
        {
            planetCenter = configuredPlanetCenter;
            systems = configuredSystems;
            accelerations = configuredAccelerations;
            _buffers = null;
            EnsureBuffers();
        }

        private void LateUpdate()
        {
            EnsureBuffers();
            if (systems == null || _buffers == null) return;
            using (UpdateMarker.Auto())
            {
                Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
                float delta = Time.deltaTime;
                for (int systemIndex = 0; systemIndex < systems.Length; systemIndex++)
                {
                    ParticleSystem system = systems[systemIndex];
                    if (system == null) continue;
                    ParticleSystem.Particle[] buffer = _buffers[systemIndex];
                    int count = system.GetParticles(buffer);
                    float acceleration = accelerations != null && systemIndex < accelerations.Length
                        ? Mathf.Max(0f, accelerations[systemIndex])
                        : 0f;
                    for (int index = 0; index < count; index++)
                    {
                        Vector3 inward = center - buffer[index].position;
                        if (inward.sqrMagnitude > 0.01f)
                            buffer[index].velocity += inward.normalized * (acceleration * delta);
                    }
                    if (count > 0) system.SetParticles(buffer, count);
                }
            }
        }

        private void EnsureBuffers()
        {
            if (_buffers != null || systems == null) return;
            _buffers = new ParticleSystem.Particle[systems.Length][];
            for (int index = 0; index < systems.Length; index++)
            {
                int capacity = systems[index] != null ? systems[index].main.maxParticles : 1;
                _buffers[index] = new ParticleSystem.Particle[Mathf.Max(1, capacity)];
            }
        }
    }
}
