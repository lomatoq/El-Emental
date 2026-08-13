using Elemental.Simulation.Magic;
using UnityEngine;

namespace Elemental.Authoring.Assets
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Ability Recipe", fileName = "AbilityRecipe")]
    public sealed class AbilityRecipeAsset : ScriptableObject
    {
        [SerializeField, Min(1)] private ushort abilityId = 1;
        [SerializeField] private MagicSelectorKind selector = MagicSelectorKind.PlanetSurface;
        [SerializeField] private MagicGeometryKind geometry = MagicGeometryKind.WallSpline;
        [SerializeField] private MagicOperatorKind[] operators = { MagicOperatorKind.AddSolid };
        [SerializeField, Min(0.05f)] private float radius = 0.45f;
        [SerializeField, Min(0.05f)] private float strength = 8f;

        public void Configure(
            AbilityId id,
            MagicSelectorKind configuredSelector,
            MagicGeometryKind configuredGeometry,
            MagicOperatorKind[] configuredOperators,
            float configuredRadius,
            float configuredStrength)
        {
            abilityId = id.Value;
            selector = configuredSelector;
            geometry = configuredGeometry;
            operators = configuredOperators;
            radius = configuredRadius;
            strength = configuredStrength;
        }

        public AbilityRecipeData Bake()
        {
            return new AbilityRecipeData(
                new AbilityId(abilityId),
                selector,
                geometry,
                operators,
                radius,
                strength);
        }
    }
}
