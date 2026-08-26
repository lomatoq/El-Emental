using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class RumbleRockVariation : MonoBehaviour
    {
        [SerializeField] private Color baseColor = new Color(0.50f, 0.34f, 0.23f, 1f);
        [SerializeField] private Color shadowColor = new Color(0.20f, 0.15f, 0.13f, 1f);
        [SerializeField] private Color edgeColor = new Color(0.64f, 0.47f, 0.34f, 1f);
        [SerializeField] private float macroScale = 3.2f;
        [SerializeField] private float macroStrength = 0.10f;
        [SerializeField] private float textureScale = 0.24f;
        [SerializeField] private bool usePlanetFrame;
        [SerializeField] private Vector3 planetCenter;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _properties;

        public void Configure(
            Color configuredBase,
            Color configuredShadow,
            Color configuredEdge,
            float configuredMacroScale,
            float configuredMacroStrength,
            float configuredTextureScale,
            bool configuredPlanetFrame,
            Vector3 configuredPlanetCenter)
        {
            baseColor = configuredBase;
            shadowColor = configuredShadow;
            edgeColor = configuredEdge;
            macroScale = configuredMacroScale;
            macroStrength = configuredMacroStrength;
            textureScale = configuredTextureScale;
            usePlanetFrame = configuredPlanetFrame;
            planetCenter = configuredPlanetCenter;
            Apply();
        }

        private void OnEnable() => Apply();

        private void Apply()
        {
            _renderers ??= GetComponentsInChildren<Renderer>(true);
            _properties ??= new MaterialPropertyBlock();
            for (int index = 0; index < _renderers.Length; index++)
            {
                Renderer renderer = _renderers[index];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(_properties);
                _properties.SetColor("_BaseColor", baseColor);
                _properties.SetColor("_ShadowColor", shadowColor);
                _properties.SetColor("_EdgeColor", edgeColor);
                _properties.SetFloat("_MacroScale", macroScale);
                _properties.SetFloat("_MacroStrength", macroStrength);
                _properties.SetFloat("_TextureScale", textureScale);
                _properties.SetFloat("_UsePlanetFrame", usePlanetFrame ? 1f : 0f);
                _properties.SetVector("_PlanetCenter", planetCenter);
                renderer.SetPropertyBlock(_properties);
            }
        }
    }
}
