using System.Collections.Generic;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Materials;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.World
{
    [DisallowMultipleComponent]
    public sealed class ElementLabDriver : MonoBehaviour
    {
        [SerializeField] private ThermalWaterMagicExecutor executor;
        [SerializeField] private ThermalWaterWorldBehaviour world;
        [SerializeField] private Transform reactionTarget;
        private uint _tick;

        public int ScriptedCommandCount { get; private set; }
        public int ReactionCount { get; private set; }

        public void Configure(ThermalWaterMagicExecutor configuredExecutor, ThermalWaterWorldBehaviour configuredWorld, Transform configuredReactionTarget)
        {
            executor = configuredExecutor; world = configuredWorld; reactionTarget = configuredReactionTarget;
        }

        private void Start() => RunCrossElementReplay();

        public void RunCrossElementReplay()
        {
            if (executor == null || world == null || world.Water.Count == 0) return;
            float3 point = world.Water.GetVolume(0).Center;
            Cast(ElementId.Water, WaterAbilityIds.GatherWater, point, new float3(0f, 1f, 0f), 0.7f);
            Cast(ElementId.Water, WaterAbilityIds.WaterJet, point, new float3(1f, 0.2f, 0f), 0.75f);
            Cast(ElementId.Water, WaterAbilityIds.FreezeBridge, point, new float3(0f, 1f, 0f), 1f);
            Cast(ElementId.Fire, FireAbilityIds.HeatJet, point, new float3(0f, 1f, 0f), 1f);
            Cast(ElementId.Fire, FireAbilityIds.ThermalFocus, point, new float3(0f, 1f, 0f), 1f);
            Cast(ElementId.Water, WaterAbilityIds.SteamBurst, point, new float3(0f, 1f, 0f), 1f);

            MaterialDefinition rock = MaterialDefinition.BrittleRock;
            PhaseState hotRock = new PhaseState(rock.Id, PhaseKind.Solid, 420f, 12f);
            var context = new ReactionContext(hotRock, rock, -160f, 140f, 0f, 1f);
            float3 targetPosition = reactionTarget != null ? ToFloat3(reactionTarget.position) : point;
            if (executor.EvaluateReaction(in context, _tick++, targetPosition).Kind == ReactionKind.ThermalShock)
            {
                ReactionCount++;
            }
        }

        private void Cast(ElementId element, AbilityId ability, float3 point, float3 aim, float intensity)
        {
            var command = new MagicCommand(
                _tick++, 91u, element, ability, point, aim,
                new List<float3> { point, point + (aim * 5f) }, intensity, 0u, 0xE1E0u + _tick);
            if (executor.Execute(in command)) ScriptedCommandCount++;
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }
}
