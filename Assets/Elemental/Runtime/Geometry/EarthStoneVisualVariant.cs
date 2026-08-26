using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    /// <summary>
    /// Deterministic, allocation-free material variation for pooled stone bodies.
    /// Geometry owns the silhouette; this layer makes neighbouring bodies read as
    /// distinct geological specimens without cloning their shared material.
    /// </summary>
    public static class EarthStoneVisualVariant
    {
        public static void Apply(Renderer renderer, uint stableId, MaterialPropertyBlock properties)
        {
            if (renderer == null || renderer.sharedMaterial == null || properties == null) return;
            Material shared = renderer.sharedMaterial;
            renderer.GetPropertyBlock(properties);

            if (IsRumbleMaterial(shared))
            {
                ApplyRestrainedRumbleVariation(renderer, shared, stableId, properties);
                return;
            }

            float familyRoll = Hash01(stableId ^ 0x2C9277B5u);
            float family = familyRoll < 0.16f ? 0f : familyRoll < 0.70f ? 1f : familyRoll < 0.91f ? 2f : 3f;
            Color source = shared.HasProperty("_ExteriorColor")
                ? shared.GetColor("_ExteriorColor")
                : new Color(0.42f, 0.27f, 0.15f, 1f);
            Color geological = family switch
            {
                < 0.5f => new Color(0.19f, 0.205f, 0.22f, 1f),
                < 1.5f => new Color(0.48f, 0.292f, 0.142f, 1f),
                < 2.5f => new Color(0.375f, 0.39f, 0.415f, 1f),
                _ => new Color(0.41f, 0.185f, 0.10f, 1f)
            };
            float familyBlend = Mathf.Lerp(0.30f, 0.62f, Hash01(stableId ^ 0x8E5D2A11u));
            float exposure = Mathf.Lerp(0.78f, 1.20f, Hash01(stableId ^ 0xD1B54A35u));
            Color exterior = Color.Lerp(source, geological, familyBlend) * exposure;
            exterior.a = 1f;

            properties.SetColor("_ExteriorColor", exterior);
            properties.SetColor("_BaseColor", exterior);
            properties.SetFloat("_StoneFamily", family);
            properties.SetFloat("_MacroFrequency", Mathf.Lerp(0.045f, 0.135f, Hash01(stableId ^ 0x94D049BBu)));
            properties.SetFloat("_MacroVariation", Mathf.Lerp(0.10f, 0.27f, Hash01(stableId ^ 0x369DEA0Fu)));
            properties.SetFloat("_StrataScale", Mathf.Lerp(0.55f, 4.8f, Hash01(stableId ^ 0xDB4F0B91u)));
            properties.SetFloat("_GrainScale", Mathf.Lerp(2.2f, 9.5f, Hash01(stableId ^ 0xBBE05633u)));
            properties.SetFloat("_MineralAmount", Mathf.Lerp(0.012f, 0.095f, Hash01(stableId ^ 0xA0F2EC75u)));
            properties.SetFloat("_ProceduralNormalStrength", Mathf.Lerp(0.32f, 0.82f, Hash01(stableId ^ 0x89E18285u)));
            renderer.SetPropertyBlock(properties);
        }

        private static void ApplyRestrainedRumbleVariation(
            Renderer renderer,
            Material shared,
            uint stableId,
            MaterialPropertyBlock properties)
        {
            float exposure = Mathf.Lerp(0.94f, 1.06f, Hash01(stableId ^ 0xD1B54A35u));
            Color baseColor = shared.GetColor("_BaseColor") * exposure;
            baseColor.a = 1f;
            properties.SetColor("_BaseColor", baseColor);

            if (shared.HasProperty("_ShadowColor"))
            {
                Color shadow = shared.GetColor("_ShadowColor") * Mathf.Lerp(0.97f, exposure, 0.55f);
                shadow.a = 1f;
                properties.SetColor("_ShadowColor", shadow);
            }
            if (shared.HasProperty("_EdgeColor"))
            {
                Color edge = shared.GetColor("_EdgeColor") * Mathf.Lerp(0.98f, exposure, 0.45f);
                edge.a = 1f;
                properties.SetColor("_EdgeColor", edge);
            }
            if (shared.HasProperty("_MacroScale"))
                properties.SetFloat(
                    "_MacroScale",
                    shared.GetFloat("_MacroScale") *
                    Mathf.Lerp(0.92f, 1.08f, Hash01(stableId ^ 0x94D049BBu)));
            if (shared.HasProperty("_MacroStrength"))
                properties.SetFloat(
                    "_MacroStrength",
                    shared.GetFloat("_MacroStrength") *
                    Mathf.Lerp(0.88f, 1.12f, Hash01(stableId ^ 0x369DEA0Fu)));
            renderer.SetPropertyBlock(properties);
        }

        private static bool IsRumbleMaterial(Material material) =>
            material.shader != null &&
            material.shader.name == "Elemental/Graphics V5/Rumble Rock Lit" &&
            material.HasProperty("_BaseColor");

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
