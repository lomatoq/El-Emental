using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public enum EarthMaterialQuality : byte
    {
        Low = 0,
        High = 1
    }

    public enum EarthStoneFamily : byte
    {
        Basalt = 0,
        Sandstone = 1,
        Granite = 2,
        Clay = 3
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
        [SerializeField] private EarthStoneFamily stoneFamily = EarthStoneFamily.Sandstone;
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
        [SerializeField, Range(0f, 1f)] private float proceduralDetail = 0.72f;
        [SerializeField, Range(0f, 1.5f)] private float proceduralNormalStrength = 0.5f;
        [SerializeField, Range(0.25f, 6f)] private float strataScale = 1.6f;
        [SerializeField, Range(0.5f, 12f)] private float grainScale = 4.2f;
        [SerializeField] private EarthMaterialQuality quality = EarthMaterialQuality.High;

        public EarthMaterialQuality Quality => quality;
        public EarthStoneFamily StoneFamily => stoneFamily;
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
            SetFloat(material, "_StoneFamily", (float)stoneFamily);
            SetFloat(material, "_ProceduralDetail", proceduralDetail);
            SetFloat(material, "_ProceduralNormalStrength", proceduralNormalStrength);
            SetFloat(material, "_StrataScale", strataScale);
            SetFloat(material, "_GrainScale", grainScale);
            if (quality == EarthMaterialQuality.Low) material.EnableKeyword("_EARTH_DETAIL_LOW");
            else material.DisableKeyword("_EARTH_DETAIL_LOW");
        }

        public void ConfigureLookDevPreset(EarthStoneFamily family)
        {
            stoneFamily = family;
            switch (family)
            {
                case EarthStoneFamily.Basalt:
                    exteriorTint = new Color(0.19f, 0.205f, 0.22f, 1f);
                    freshInteriorTint = new Color(0.105f, 0.115f, 0.125f, 1f);
                    dustTint = new Color(0.31f, 0.30f, 0.29f, 1f);
                    mineralTint = new Color(0.39f, 0.46f, 0.50f, 1f);
                    exteriorSmoothness = 0.13f;
                    normalStrength = 0.88f;
                    proceduralNormalStrength = 0.72f;
                    strataScale = 0.7f;
                    grainScale = 5.4f;
                    break;
                case EarthStoneFamily.Sandstone:
                    exteriorTint = new Color(0.47f, 0.285f, 0.14f, 1f);
                    freshInteriorTint = new Color(0.62f, 0.42f, 0.23f, 1f);
                    dustTint = new Color(0.72f, 0.54f, 0.34f, 1f);
                    mineralTint = new Color(0.27f, 0.18f, 0.12f, 1f);
                    exteriorSmoothness = 0.045f;
                    normalStrength = 0.66f;
                    proceduralNormalStrength = 0.46f;
                    strataScale = 2.2f;
                    grainScale = 3.8f;
                    break;
                case EarthStoneFamily.Granite:
                    exteriorTint = new Color(0.38f, 0.39f, 0.41f, 1f);
                    freshInteriorTint = new Color(0.50f, 0.49f, 0.47f, 1f);
                    dustTint = new Color(0.52f, 0.49f, 0.45f, 1f);
                    mineralTint = new Color(0.73f, 0.70f, 0.64f, 1f);
                    exteriorSmoothness = 0.10f;
                    normalStrength = 0.78f;
                    proceduralNormalStrength = 0.58f;
                    strataScale = 0.85f;
                    grainScale = 7.2f;
                    break;
                default:
                    exteriorTint = new Color(0.40f, 0.18f, 0.105f, 1f);
                    freshInteriorTint = new Color(0.53f, 0.27f, 0.16f, 1f);
                    dustTint = new Color(0.61f, 0.36f, 0.24f, 1f);
                    mineralTint = new Color(0.29f, 0.12f, 0.08f, 1f);
                    exteriorSmoothness = 0.18f;
                    normalStrength = 0.42f;
                    proceduralNormalStrength = 0.28f;
                    strataScale = 1.25f;
                    grainScale = 2.4f;
                    break;
            }
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
