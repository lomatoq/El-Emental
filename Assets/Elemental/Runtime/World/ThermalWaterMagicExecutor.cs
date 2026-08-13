using System.Collections.Generic;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Materials;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.World
{
    [DisallowMultipleComponent]
    public sealed class ThermalWaterMagicExecutor : MonoBehaviour, IMagicCommandSink
    {
        private static readonly ProfilerMarker ExecuteMarker = new ProfilerMarker("Elemental.ThermalWaterMagic.Execute");

        [SerializeField] private ThermalWaterWorldBehaviour world;
        private readonly Dictionary<AbilityId, CompiledAbilityRecipe> _recipes = new Dictionary<AbilityId, CompiledAbilityRecipe>();
        private readonly ReactionResolver _reactions = new ReactionResolver();
        private uint _nextThermalRegion = 1u;
        private int _heldWaterIndex = -1;

        public MagicWorldEvents Events { get; } = new MagicWorldEvents();
        public MagicReplayRecorder Recorder { get; } = new MagicReplayRecorder();
        public int SuccessfulCommandCount { get; private set; }

        public void Configure(ThermalWaterWorldBehaviour configuredWorld) => world = configuredWorld;

        public void ConfigureRecipes(CompiledAbilityRecipe[] recipes)
        {
            _recipes.Clear();
            if (recipes == null) return;
            for (int index = 0; index < recipes.Length; index++) _recipes[recipes[index].Id] = recipes[index];
        }

        public bool Execute(in MagicCommand command)
        {
            using (ExecuteMarker.Auto())
            {
                if (world == null || !world.IsReady || !_recipes.ContainsKey(command.Ability))
                {
                    return Reject(command, "Thermal/water runtime or recipe is missing.");
                }

                bool executed = command.Element == ElementId.Fire
                    ? ExecuteFire(in command)
                    : command.Element == ElementId.Water && ExecuteWater(in command);
                if (!executed) return Reject(command, "Unsupported thermal/water ability or no water volume in range.");
                Recorder.Record(in command);
                SuccessfulCommandCount++;
                return true;
            }
        }

        public void BuildPreview(in MagicCommand command, List<Vector3> output)
        {
            output.Clear();
            if (!_recipes.ContainsKey(command.Ability)) return;
            float3 anchor = Anchor(in command);
            output.Add(ToVector3(anchor));
            output.Add(ToVector3(anchor + (command.Aim * (command.Element == ElementId.Fire ? 7f : 5f))));
        }

        public ReactionResult EvaluateReaction(in ReactionContext context, uint tick, float3 position)
        {
            ReactionResult result = _reactions.Resolve(in context);
            if (result.Kind != ReactionKind.None)
            {
                ReactionTriggeredEvent reactionEvent = new ReactionTriggeredEvent(
                    tick, result.Kind, position, result.Severity, result.PressureImpulse);
                Events.Emit(in reactionEvent);
            }
            return result;
        }

        public bool ApplyWaterOperator(
            WaterOperatorKind operation,
            int primaryIndex,
            float amount,
            float3 direction,
            uint tick,
            int secondaryIndex = -1)
        {
            if (world == null || !world.IsReady || primaryIndex < 0 || primaryIndex >= world.Water.Count) return false;
            WaterVolume volume = world.Water.GetVolume(primaryIndex);
            switch (operation)
            {
                case WaterOperatorKind.AddHeat:
                    ApplyEnergyWithEvents(tick, primaryIndex, math.abs(amount));
                    return true;
                case WaterOperatorKind.RemoveHeat:
                    ApplyEnergyWithEvents(tick, primaryIndex, -math.abs(amount));
                    return true;
                case WaterOperatorKind.TransferMass:
                    return world.Water.TransferMass(primaryIndex, secondaryIndex, math.abs(amount));
                case WaterOperatorKind.Freeze:
                    ApplyEnergyWithEvents(tick, primaryIndex, -450f * volume.State.Mass);
                    return true;
                case WaterOperatorKind.Melt:
                    ApplyEnergyWithEvents(tick, primaryIndex, 370f * volume.State.Mass);
                    return true;
                case WaterOperatorKind.Vaporize:
                    ApplyEnergyWithEvents(tick, primaryIndex, 2700f * volume.State.Mass);
                    return true;
                case WaterOperatorKind.Condense:
                    ApplyEnergyWithEvents(tick, primaryIndex, -2400f * volume.State.Mass);
                    return true;
                case WaterOperatorKind.ApplyPressureImpulse:
                    world.Water.ApplyPressureImpulse(primaryIndex, direction, amount);
                    return true;
                default:
                    return false;
            }
        }

        private bool ExecuteFire(in MagicCommand command)
        {
            float3 anchor = Anchor(in command);
            if (command.Ability == FireAbilityIds.HeatJet || command.Ability == FireAbilityIds.ThermalFocus)
            {
                bool focus = command.Ability == FireAbilityIds.ThermalFocus;
                float heat = (focus ? 240f : 90f) * math.lerp(0.5f, 1.5f, command.Intensity);
                ThermalRegion region = new ThermalRegion(
                    new ThermalRegionId(_nextThermalRegion++), command.CasterId, anchor,
                    focus ? 3f : 2f, heat, focus ? 2.5f : 1.2f, focus ? 5f : 2.5f,
                    MaterialTags.None, focus ? (byte)210 : (byte)170);
                world.Thermal.Register(in region);
                if (world.Water.TryFindNearest(anchor, focus ? 4f : 2.5f, out int index))
                {
                    ApplyEnergyWithEvents(command.Tick, index, heat * (focus ? 18f : 7f));
                }
                return true;
            }
            return false;
        }

        private bool ExecuteWater(in MagicCommand command)
        {
            float3 anchor = Anchor(in command);
            if (command.Ability == WaterAbilityIds.GatherWater)
            {
                if (!world.Water.TryFindNearest(anchor, 20f, out _heldWaterIndex)) return false;
                world.Water.ApplyPressureImpulse(_heldWaterIndex, command.Origin - world.Water.GetVolume(_heldWaterIndex).Center, 8f);
                return true;
            }
            int index = ResolveWaterIndex(anchor);
            if (index < 0) return false;
            if (command.Ability == WaterAbilityIds.WaterJet)
            {
                return ApplyWaterOperator(
                    WaterOperatorKind.ApplyPressureImpulse, index,
                    25f * math.lerp(0.5f, 1.5f, command.Intensity), command.Aim, command.Tick);
            }
            WaterVolume volume = world.Water.GetVolume(index);
            if (command.Ability == WaterAbilityIds.FreezeBridge)
            {
                return ApplyWaterOperator(WaterOperatorKind.Freeze, index, 0f, command.Aim, command.Tick);
            }
            if (command.Ability == WaterAbilityIds.SteamBurst)
            {
                ApplyWaterOperator(WaterOperatorKind.Vaporize, index, 0f, command.Aim, command.Tick);
                float3 velocity = world.Water.ApplyPressureImpulse(index, command.Aim, 30f);
                ReactionTriggeredEvent reactionEvent = new ReactionTriggeredEvent(
                    command.Tick, ReactionKind.Vaporize, volume.Center, 1f, math.length(velocity));
                Events.Emit(in reactionEvent);
                return true;
            }
            return false;
        }

        private void ApplyEnergyWithEvents(uint tick, int index, float energy)
        {
            WaterVolume before = world.Water.GetVolume(index);
            MaterialDefinition material = MaterialDefinition.Water;
            PhaseTransitionResult result = world.Water.ApplyEnergy(index, in material, energy);
            if (result.State.Phase != before.State.Phase)
            {
                PhaseChangedEvent phaseEvent = new PhaseChangedEvent(
                    tick, before.Id.Value, before.State.Phase, result.State.Phase,
                    result.State.Temperature, result.State.Mass);
                Events.Emit(in phaseEvent);
            }
        }

        private int ResolveWaterIndex(float3 anchor)
        {
            if (_heldWaterIndex >= 0 && _heldWaterIndex < world.Water.Count) return _heldWaterIndex;
            return world.Water.TryFindNearest(anchor, 20f, out int index) ? index : -1;
        }

        private bool Reject(in MagicCommand command, string reason)
        {
            AbilityRejectedEvent rejected = new AbilityRejectedEvent(command.Tick, command.Ability, reason);
            Events.Emit(in rejected);
            return false;
        }

        private static float3 Anchor(in MagicCommand command) => command.Path.Length > 0 ? command.Path[0] : command.Origin;
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
