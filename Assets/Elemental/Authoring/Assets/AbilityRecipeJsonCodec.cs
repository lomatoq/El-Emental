using System;
using Elemental.Simulation.Magic;
using UnityEngine;

namespace Elemental.Authoring.Assets
{
    [Serializable]
    public sealed class AbilityRecipeSchema
    {
        public int schemaVersion;
        public ushort abilityId;
        public string selector;
        public string geometry;
        public string[] operators;
        public float radius;
        public float strength;
    }

    public static class AbilityRecipeJsonCodec
    {
        public const int CurrentSchemaVersion = 1;

        public static string Export(AbilityRecipeAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            AbilityRecipeData data = asset.Bake();
            string[] operators = new string[data.Operators.Length];
            for (int index = 0; index < operators.Length; index++)
                operators[index] = data.Operators[index].ToString();
            return JsonUtility.ToJson(new AbilityRecipeSchema
            {
                schemaVersion = CurrentSchemaVersion,
                abilityId = data.Id.Value,
                selector = data.Selector.ToString(),
                geometry = data.Geometry.ToString(),
                operators = operators,
                radius = data.Radius,
                strength = data.Strength
            }, true);
        }

        public static bool TryImport(string json, AbilityRecipeAsset target, out string error)
        {
            if (target == null)
            {
                error = "Target AbilityRecipeAsset is required.";
                return false;
            }

            AbilityRecipeSchema schema;
            try { schema = JsonUtility.FromJson<AbilityRecipeSchema>(json); }
            catch (Exception exception)
            {
                error = "Invalid JSON: " + exception.Message;
                return false;
            }

            if (schema == null || schema.schemaVersion != CurrentSchemaVersion)
            {
                error = $"Unsupported ability schema {schema?.schemaVersion ?? 0}; expected {CurrentSchemaVersion}.";
                return false;
            }
            if (schema.abilityId == 0 || !float.IsFinite(schema.radius) || schema.radius <= 0f ||
                !float.IsFinite(schema.strength) || schema.strength <= 0f)
            {
                error = "Ability ID, radius, and strength must be positive and finite.";
                return false;
            }
            if (!Enum.TryParse(schema.selector, true, out MagicSelectorKind selector) ||
                !Enum.IsDefined(typeof(MagicSelectorKind), selector) ||
                !Enum.TryParse(schema.geometry, true, out MagicGeometryKind geometry) ||
                !Enum.IsDefined(typeof(MagicGeometryKind), geometry))
            {
                error = "Selector or geometry name is unknown.";
                return false;
            }
            if (schema.operators == null || schema.operators.Length == 0 || schema.operators.Length > 15)
            {
                error = "Operator list must contain between 1 and 15 entries.";
                return false;
            }

            var operators = new MagicOperatorKind[schema.operators.Length];
            for (int index = 0; index < operators.Length; index++)
            {
                if (!Enum.TryParse(schema.operators[index], true, out operators[index]) ||
                    !Enum.IsDefined(typeof(MagicOperatorKind), operators[index]))
                {
                    error = $"Unknown operator '{schema.operators[index]}'.";
                    return false;
                }
            }

            try
            {
                var data = new AbilityRecipeData(
                    new AbilityId(schema.abilityId), selector, geometry, operators, schema.radius, schema.strength);
                new AbilityCompiler().Compile(data);
                target.Configure(data.Id, data.Selector, data.Geometry, data.Operators, data.Radius, data.Strength);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
