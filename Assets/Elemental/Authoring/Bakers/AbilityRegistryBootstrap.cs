using Elemental.Authoring.Assets;
using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using UnityEngine;

namespace Elemental.Authoring.Bakers
{
    [DisallowMultipleComponent]
    public sealed class AbilityRegistryBootstrap : MonoBehaviour
    {
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private AbilityRecipeAsset[] recipes;

        public void Configure(MagicExecutor configuredExecutor, AbilityRecipeAsset[] configuredRecipes)
        {
            executor = configuredExecutor;
            recipes = configuredRecipes;
        }

        private void Awake()
        {
            if (executor == null || recipes == null)
            {
                Debug.LogError("[Elemental] Ability registry is not configured.", this);
                enabled = false;
                return;
            }

            AbilityCompiler compiler = new AbilityCompiler();
            CompiledAbilityRecipe[] compiled = new CompiledAbilityRecipe[recipes.Length];
            for (int index = 0; index < recipes.Length; index++)
            {
                if (recipes[index] == null)
                {
                    Debug.LogError($"[Elemental] Ability recipe {index} is missing.", this);
                    enabled = false;
                    return;
                }

                compiled[index] = compiler.Compile(recipes[index].Bake());
            }

            executor.ConfigureRecipes(compiled);
        }
    }
}
