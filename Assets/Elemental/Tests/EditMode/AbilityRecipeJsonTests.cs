using Elemental.Authoring.Assets;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class AbilityRecipeJsonTests
    {
        [Test]
        public void SchemaJsonRoundTripPreservesCompiledRecipe()
        {
            AbilityRecipeAsset source = ScriptableObject.CreateInstance<AbilityRecipeAsset>();
            AbilityRecipeAsset target = ScriptableObject.CreateInstance<AbilityRecipeAsset>();
            source.Configure(
                new AbilityId(701), MagicSelectorKind.PlanetSurface, MagicGeometryKind.AnchorSphere,
                new[] { MagicOperatorKind.AddHeat, MagicOperatorKind.Vaporize }, 2.5f, 12f);

            string json = AbilityRecipeJsonCodec.Export(source);
            bool imported = AbilityRecipeJsonCodec.TryImport(json, target, out string error);
            CompiledAbilityRecipe compiled = new AbilityCompiler().Compile(target.Bake());

            Assert.That(imported, Is.True, error);
            Assert.That(json, Does.Contain("\"schemaVersion\": 1"));
            Assert.That(compiled.Id.Value, Is.EqualTo(701));
            Assert.That(compiled.Operators.Length, Is.EqualTo(2));
            Assert.That(compiled.Radius, Is.EqualTo(2.5f));
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void ImportRejectsMissingOrFutureSchemaWithoutMutatingTarget()
        {
            AbilityRecipeAsset target = ScriptableObject.CreateInstance<AbilityRecipeAsset>();
            AbilityRecipeData before = target.Bake();

            bool missing = AbilityRecipeJsonCodec.TryImport("{\"abilityId\":9}", target, out string missingError);
            bool future = AbilityRecipeJsonCodec.TryImport("{\"schemaVersion\":99,\"abilityId\":9}", target, out string futureError);

            Assert.That(missing, Is.False);
            Assert.That(future, Is.False);
            Assert.That(missingError, Does.Contain("schema"));
            Assert.That(futureError, Does.Contain("schema"));
            Assert.That(target.Bake().Id, Is.EqualTo(before.Id));
            Object.DestroyImmediate(target);
        }
    }
}
