using System.Collections.Generic;
using Elemental.Simulation.Fields;
using Elemental.Simulation.Magic;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.World
{
    [DisallowMultipleComponent]
    public sealed class AirMagicExecutor : MonoBehaviour, IMagicCommandSink
    {
        private static readonly ProfilerMarker ExecuteMarker = new ProfilerMarker("Elemental.AirMagic.Execute");

        [SerializeField] private FieldWorldBehaviour fieldWorld;
        private readonly Dictionary<AbilityId, CompiledAbilityRecipe> _recipes = new Dictionary<AbilityId, CompiledAbilityRecipe>();
        private uint _nextRegionId = 1u;

        public MagicWorldEvents Events { get; } = new MagicWorldEvents();
        public MagicReplayRecorder Recorder { get; } = new MagicReplayRecorder();
        public int SuccessfulCommandCount { get; private set; }

        public void Configure(FieldWorldBehaviour configuredFieldWorld)
        {
            fieldWorld = configuredFieldWorld;
        }

        public void ConfigureRecipes(CompiledAbilityRecipe[] recipes)
        {
            _recipes.Clear();
            if (recipes == null)
            {
                return;
            }

            for (int index = 0; index < recipes.Length; index++)
            {
                _recipes[recipes[index].Id] = recipes[index];
            }
        }

        public bool Execute(in MagicCommand command)
        {
            using (ExecuteMarker.Auto())
            {
                if (fieldWorld == null || !fieldWorld.IsReady)
                {
                    return Reject(command, "Air field runtime is not configured.");
                }

                if (command.Element != ElementId.Air)
                {
                    return Reject(command, "Air executor only accepts Air commands.");
                }

                if (!_recipes.TryGetValue(command.Ability, out CompiledAbilityRecipe recipe) ||
                    !ContainsSpawnField(in recipe))
                {
                    return Reject(command, "Air ability recipe is not registered or has no SpawnField operator.");
                }

                FieldRegionId id = new FieldRegionId(_nextRegionId++);
                if (!AirAbilityBuilder.TryBuild(in command, id, out FieldRegion region) || !fieldWorld.Register(in region))
                {
                    return Reject(command, "Air field could not be built or admitted to the field budget.");
                }

                Recorder.Record(in command);
                SuccessfulCommandCount++;
                FieldSpawnedEvent spawned = new FieldSpawnedEvent(command.Tick, command.Ability, region);
                Events.Emit(in spawned);
                return true;
            }
        }

        public void BuildPreview(in MagicCommand command, List<Vector3> output)
        {
            output.Clear();
            if (!_recipes.ContainsKey(command.Ability) ||
                !AirAbilityBuilder.TryBuild(in command, new FieldRegionId(uint.MaxValue), out FieldRegion region))
            {
                return;
            }

            Vector3 center = ToVector3(region.Center);
            Vector3 axis = ToVector3(region.Axis);
            output.Add(center);
            output.Add(center + (axis * Mathf.Max(region.Radius, region.Length)));
            Vector3 side = Vector3.Cross(axis, Vector3.right);
            if (side.sqrMagnitude < 0.001f)
            {
                side = Vector3.Cross(axis, Vector3.forward);
            }
            output.Add(center + (side.normalized * region.Radius));
        }

        private bool Reject(in MagicCommand command, string reason)
        {
            AbilityRejectedEvent rejected = new AbilityRejectedEvent(command.Tick, command.Ability, reason);
            Events.Emit(in rejected);
            return false;
        }

        private static bool ContainsSpawnField(in CompiledAbilityRecipe recipe)
        {
            for (int index = 0; index < recipe.Operators.Length; index++)
            {
                if (recipe.Operators[index] == MagicOperatorKind.SpawnField)
                {
                    return true;
                }
            }
            return false;
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
