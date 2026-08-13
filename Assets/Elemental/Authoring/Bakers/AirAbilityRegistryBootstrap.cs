using Elemental.Authoring.Assets;
using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using UnityEngine;

namespace Elemental.Authoring.Bakers
{
    [DisallowMultipleComponent]
    public sealed class AirAbilityRegistryBootstrap : MonoBehaviour
    {
        [SerializeField] private AirMagicExecutor executor;
        [SerializeField] private AbilityRecipeAsset[] recipes;

        public void Configure(AirMagicExecutor configuredExecutor, AbilityRecipeAsset[] configuredRecipes)
        {
            executor = configuredExecutor;
            recipes = configuredRecipes;
        }

        private void Awake()
        {
            if (executor == null || recipes == null)
            {
                Debug.LogError("[Elemental] Air ability registry is not configured.", this);
                enabled = false;
                return;
            }

            AbilityCompiler compiler = new AbilityCompiler();
            CompiledAbilityRecipe[] compiled = new CompiledAbilityRecipe[recipes.Length];
            for (int index = 0; index < recipes.Length; index++)
            {
                if (recipes[index] == null)
                {
                    Debug.LogError($"[Elemental] Air ability recipe {index} is missing.", this);
                    enabled = false;
                    return;
                }

                compiled[index] = compiler.Compile(recipes[index].Bake());
            }

            executor.ConfigureRecipes(compiled);
        }
    }
}
