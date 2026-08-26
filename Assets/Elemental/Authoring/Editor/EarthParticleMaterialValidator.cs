using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class EarthParticleMaterialValidator
    {
        [MenuItem("Elemental/QA/Validate Earth Dust Materials")]
        public static void ValidateActiveSceneMenu()
        {
            if (!ValidateScene(SceneManager.GetActiveScene(), out string error))
                throw new InvalidOperationException(error);
            Debug.Log("[Elemental] Earth dust materials are transparent, textured and soft-particle ready.");
        }

        public static bool ValidateScene(Scene scene, out string error)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = "Earth dust validation requires a loaded scene.";
                return false;
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                ParticleSystemRenderer[] renderers =
                    root.GetComponentsInChildren<ParticleSystemRenderer>(true);
                for (int index = 0; index < renderers.Length; index++)
                    if (!ValidateRenderer(renderers[index], out error)) return false;
            }
            error = string.Empty;
            return true;
        }

        public static bool ValidateRenderer(ParticleSystemRenderer renderer, out string error)
        {
            error = string.Empty;
            if (renderer == null || !RequiresSoftDust(renderer)) return true;
            Material material = renderer.sharedMaterial;
            if (material == null)
            {
                error = $"Dust particle '{renderer.name}' has no material.";
                return false;
            }
            bool transparent = material.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent ||
                               material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f;
            Texture alphaTexture = material.HasProperty("_BaseMap")
                ? material.GetTexture("_BaseMap")
                : material.mainTexture;
            bool soft = !material.HasProperty("_SoftParticlesEnabled") ||
                        material.GetFloat("_SoftParticlesEnabled") > 0.5f;
            if (transparent && alphaTexture != null && soft) return true;
            error =
                $"Dust particle '{renderer.name}' uses invalid material '{material.name}': " +
                $"transparent={transparent}, alphaTexture={alphaTexture != null}, softParticles={soft}.";
            return false;
        }

        private static bool RequiresSoftDust(ParticleSystemRenderer renderer)
        {
            if (renderer.renderMode is not (ParticleSystemRenderMode.Billboard or
                ParticleSystemRenderMode.HorizontalBillboard or
                ParticleSystemRenderMode.VerticalBillboard)) return false;
            string value = renderer.gameObject.name;
            return value.IndexOf("dust", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("plough", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
