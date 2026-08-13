using System;
using Unity.Collections;

namespace Elemental.Simulation.Magic
{
    public enum MagicSelectorKind : byte
    {
        PlanetSurface = 1,
        HeldFragment = 2
    }

    public enum MagicGeometryKind : byte
    {
        WallSpline = 1,
        AnchorSphere = 2,
        Direction = 3
    }

    public enum MagicOperatorKind : byte
    {
        AddSolid = 1,
        SubtractSolid = 2,
        SpawnFragment = 3,
        ApplyImpulse = 4,
        SpawnField = 5,
        AddHeat = 6,
        RemoveHeat = 7,
        TransferMass = 8,
        Freeze = 9,
        Melt = 10,
        Vaporize = 11,
        Condense = 12,
        ApplyPressureImpulse = 13
    }

    public readonly struct AbilityRecipeData
    {
        public AbilityRecipeData(
            AbilityId id,
            MagicSelectorKind selector,
            MagicGeometryKind geometry,
            MagicOperatorKind[] operators,
            float radius,
            float strength)
        {
            Id = id;
            Selector = selector;
            Geometry = geometry;
            Operators = operators ?? throw new ArgumentNullException(nameof(operators));
            Radius = radius;
            Strength = strength;
        }

        public AbilityId Id { get; }
        public MagicSelectorKind Selector { get; }
        public MagicGeometryKind Geometry { get; }
        public MagicOperatorKind[] Operators { get; }
        public float Radius { get; }
        public float Strength { get; }
    }

    public readonly struct CompiledAbilityRecipe
    {
        public CompiledAbilityRecipe(
            AbilityId id,
            MagicSelectorKind selector,
            MagicGeometryKind geometry,
            FixedList64Bytes<MagicOperatorKind> operators,
            float radius,
            float strength)
        {
            Id = id;
            Selector = selector;
            Geometry = geometry;
            Operators = operators;
            Radius = radius;
            Strength = strength;
        }

        public AbilityId Id { get; }
        public MagicSelectorKind Selector { get; }
        public MagicGeometryKind Geometry { get; }
        public FixedList64Bytes<MagicOperatorKind> Operators { get; }
        public float Radius { get; }
        public float Strength { get; }
    }

    public interface IAbilityCompiler
    {
        CompiledAbilityRecipe Compile(AbilityRecipeData data);
    }

    public sealed class AbilityCompiler : IAbilityCompiler
    {
        public CompiledAbilityRecipe Compile(AbilityRecipeData data)
        {
            if (!data.Id.IsValid)
            {
                throw new InvalidOperationException("Ability ID must be valid.");
            }

            if (!float.IsFinite(data.Radius) || data.Radius <= 0f)
            {
                throw new InvalidOperationException("Ability radius must be finite and positive.");
            }

            if (!float.IsFinite(data.Strength) || data.Strength <= 0f)
            {
                throw new InvalidOperationException("Ability strength must be finite and positive.");
            }

            FixedList64Bytes<MagicOperatorKind> operators = default;
            if (data.Operators.Length == 0 || data.Operators.Length > operators.Capacity)
            {
                throw new InvalidOperationException("Ability must contain a bounded operator list.");
            }

            for (int index = 0; index < data.Operators.Length; index++)
            {
                operators.Add(data.Operators[index]);
            }

            return new CompiledAbilityRecipe(
                data.Id,
                data.Selector,
                data.Geometry,
                operators,
                data.Radius,
                data.Strength);
        }
    }
}
