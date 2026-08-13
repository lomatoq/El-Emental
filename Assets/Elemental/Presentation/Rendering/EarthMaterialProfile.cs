using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public enum EarthMaterialQuality : byte
    {
        Low = 0,
        High = 1
    }

    [CreateAssetMenu(menuName = "Elemental/Rendering/Earth Material Profile", fileName = "EarthMaterialProfile")]
    public sealed class EarthMaterialProfile : ScriptableObject
    {
        [Header("Material hierarchy")]
        [SerializeField] private Color exteriorTint = new Color(0.38f, 0.245f, 0.155f, 1f);
        [SerializeField] private Color freshInteriorTint = new Color(0.205f, 0.19f, 0.165f, 1f);
        [SerializeField] private Color dustTint = new Color(0.52f, 0.405f, 0.285f, 1f);
        [SerializeField] private Color magicTint = new Color(1.15f, 0.42f, 0.075f, 1f);
        [SerializeField] private Color mineralTint = new Color(0.18f, 0.24f, 0.29f, 1f);
        [Header("Scale in metres")]
        [SerializeField, Range(0.02f, 0.25f)] private float macroFrequency = 0.075f;
        [SerializeField, Range(0.2f, 2f)] private float midTiling = 0.48f;
        [SerializeField, Range(1f, 12f)] private float microTiling = 4.2f;
        [SerializeField, Min(1f)] private float microFadeStart = 9f;
        [SerializeField, Min(2f)] private float microFadeEnd = 24f;
        [Header("Surface response")]
        [SerializeField, Range(0f, 0.5f)] private float macroVariation = 0.16f;
        [SerializeField, Range(0f, 1f)] private float cavityStrength = 0.46f;
        [SerializeField, Range(0f, 1f)] private float dustAmount = 0.16f;
        [SerializeField, Range(0f, 1f)] private float exteriorSmoothness = 0.07f;
        [SerializeField, Range(0f, 1f)] private float interiorSmoothness = 0.025f;
        [SerializeField, Range(0f, 1f)] private float normalStrength = 0.72f;
        [SerializeField] private EarthMaterialQuality quality = EarthMaterialQuality.High;

        public EarthMaterialQuality Quality => quality;
        public Color ExteriorTint => exteriorTint;
        public Color FreshInteriorTint => freshInteriorTint;
        public Color DustTint => dustTint;
        public Color MagicTint => magicTint;

        public void Apply(Material material, bool freshInterior)
        {
            if (material == null) return;
            SetColor(material, "_BaseColor", exteriorTint);
            SetColor(material, "_ExteriorColor", exteriorTint);
            SetColor(material, "_InteriorColor", freshInteriorTint);
            SetColor(material, "_DustColor", dustTint);
            SetColor(material, "_MagicColor", magicTint);
            SetColor(material, "_MineralColor", mineralTint);
            SetFloat(material, "_InteriorAmount", freshInterior ? 1f : 0f);
            SetFloat(material, "_MacroFrequency", macroFrequency);
            SetFloat(material, "_WorldTiling", midTiling);
            SetFloat(material, "_MicroTiling", microTiling);
            SetFloat(material, "_MicroFadeStart", microFadeStart);
            SetFloat(material, "_MicroFadeEnd", Mathf.Max(microFadeStart + 0.01f, microFadeEnd));
            SetFloat(material, "_MacroVariation", macroVariation);
            SetFloat(material, "_CavityStrength", cavityStrength);
            SetFloat(material, "_DustAmount", dustAmount);
            SetFloat(material, "_Smoothness", freshInterior ? interiorSmoothness : exteriorSmoothness);
            SetFloat(material, "_NormalStrength", normalStrength);
            if (quality == EarthMaterialQuality.Low) material.EnableKeyword("_EARTH_DETAIL_LOW");
            else material.DisableKeyword("_EARTH_DETAIL_LOW");
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}
