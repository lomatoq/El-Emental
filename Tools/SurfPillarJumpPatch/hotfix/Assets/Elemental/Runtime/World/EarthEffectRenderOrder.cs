using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Runtime.World
{
    // Cosmetic fragments composite before dust without entering scene depth.
    // Real stones, structures and characters retain their normal depth/shadows.
    public static class EarthEffectRenderOrder
    {
        public const int CosmeticFragmentQueue = 2990;
        public const int CosmeticFragmentOrder = 0;
        public const int DustOrder = 20;

        public static void ApplyDustRenderer(ParticleSystemRenderer renderer)
        {
            if (renderer == null) return;
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = DustOrder;
            renderer.sortMode = ParticleSystemSortMode.Distance;
        }

        public static void ApplyCosmeticRenderer(Renderer renderer, Material material)
        {
            if (renderer == null) return;
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = CosmeticFragmentOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Call only on a cosmetic asset or an owned copy, never a world material.
        public static void ConfigureCosmeticMaterial(Material material)
        {
            if (material == null) return;
            if (!SupportsCosmeticDepthControl(material))
                throw new InvalidOperationException($"Cosmetic fragment material '{material.name}' requires a shader with _ZWrite control.");
            material.renderQueue = CosmeticFragmentQueue;
            material.SetOverrideTag("RenderType", "Transparent");
            SetFloat(material, "_ZWrite", 0f);
            SetFloat(material, "_Surface", 1f);
            SetFloat(material, "_ZTest", (float)CompareFunction.LessEqual);
            SetFloat(material, "_QueueOffset", -10f);
            SetFloat(material, "_AlphaClip", 0f);
            // Fragments keep their solid appearance; only their compositing layer changes.
            SetFloat(material, "_SrcBlend", (float)BlendMode.One);
            SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
            SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloat(material, "_DstBlendAlpha", (float)BlendMode.Zero);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("DepthNormals", false);
            material.SetShaderPassEnabled("DepthNormalsOnly", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
        }

        public static bool SupportsCosmeticDepthControl(Material material) =>
            material != null && material.shader != null && (material.HasProperty("_ZWrite") ||
                                 material.shader.name == "Elemental/Earth Indirect Debris");

        private static void SetFloat(Material material, string name, float value)
        {
            if (material.HasProperty(name)) material.SetFloat(name, value);
        }
    }

    // Explicit owner-local cache: creation happens during setup, not emission/update.
    // Sources remain the editable profile materials, including shared sandstone.
    public sealed class EarthCosmeticMaterialCache : IDisposable
    {
        private readonly Dictionary<Material, Material> copies = new();

        public Material Get(Material source)
        {
            if (source == null || !Application.isPlaying) return source;
            if (copies.TryGetValue(source, out Material copy)) return copy;
            foreach (Material existing in copies.Values)
                if (existing == source) return source;
            copy = new Material(source) { name = source.name + " (Cosmetic FX)", hideFlags = HideFlags.DontSave };
            EarthEffectRenderOrder.ConfigureCosmeticMaterial(copy);
            copies.Add(source, copy);
            return copy;
        }

        public void Dispose()
        {
            foreach (Material copy in copies.Values)
                if (copy != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(copy);
                    else UnityEngine.Object.DestroyImmediate(copy);
                }
            copies.Clear();
        }
    }
}
