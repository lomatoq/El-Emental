using System.Collections.Generic;
using Elemental.Simulation.Fields;
using Elemental.Simulation.Magic;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.World
{
    [DisallowMultipleComponent]
    public sealed class WindLabDriver : MonoBehaviour
    {
        [SerializeField] private AirMagicExecutor executor;
        [SerializeField] private Rigidbody[] projectiles;
        [SerializeField] private Transform planetCenter;
        [SerializeField, Min(1f)] private float refreshInterval = 3.5f;

        private float _remaining;
        private uint _tick;

        public int SuiteSpawnCount { get; private set; }

        public void Configure(AirMagicExecutor configuredExecutor, Rigidbody[] configuredProjectiles, Transform configuredPlanetCenter)
        {
            executor = configuredExecutor;
            projectiles = configuredProjectiles;
            planetCenter = configuredPlanetCenter;
        }

        private void Start()
        {
            SpawnDemoSuite();
        }

        private void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                SpawnDemoSuite();
            }
        }

        public void SpawnDemoSuite()
        {
            if (executor == null)
            {
                return;
            }

            Cast(AirAbilityIds.GustCorridor, new float3(-8f, 25f, -2f), new float3(1f, 0.1f, 0.1f),
                new[] { new float3(-8f, 25f, -2f), new float3(8f, 26f, 0f) }, 0.75f);
            Cast(AirAbilityIds.Vortex, new float3(7f, 25f, 7f), new float3(0f, 1f, 0f), null, 0.7f);
            Cast(AirAbilityIds.LiftColumn, new float3(-7f, 23f, 7f), new float3(0f, 1f, 0f), null, 0.8f);
            Cast(AirAbilityIds.AirBrake, new float3(0f, 29f, -7f), new float3(0f, 1f, 0f), null, 0.85f);

            if (projectiles != null)
            {
                for (int index = 0; index < projectiles.Length; index++)
                {
                    Rigidbody body = projectiles[index];
                    if (body == null)
                    {
                        continue;
                    }

                    Vector3 radialUp = planetCenter != null
                        ? (body.position - planetCenter.position).normalized
                        : Vector3.up;
                    body.linearVelocity = (Vector3.right * (5f + (index % 4))) + (radialUp * (1f + (index % 3)));
                }
            }

            SuiteSpawnCount++;
            _remaining = refreshInterval;
        }

        private void Cast(AbilityId ability, float3 origin, float3 aim, IReadOnlyList<float3> path, float intensity)
        {
            var command = new MagicCommand(
                _tick++, 77u, ElementId.Air, ability, origin, aim, path, intensity, 0u, 0xA17u + _tick);
            executor.Execute(in command);
        }
    }
}
