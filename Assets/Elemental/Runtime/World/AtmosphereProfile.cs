using UnityEngine;

namespace Elemental.Runtime.World
{
    [CreateAssetMenu(menuName = "Elemental/World/Atmosphere Profile", fileName = "AtmosphereProfile")]
    public sealed class AtmosphereProfile : ScriptableObject
    {
        [SerializeField, Range(1.005f, 1.2f)] private float outerRadiusMultiplier = 1.055f;
        [SerializeField] private Color rayleighColor = new Color(0.24f, 0.52f, 1f);
        [SerializeField] private Color mieColor = new Color(1f, 0.48f, 0.2f);
        [SerializeField, Range(0f, 8f)] private float rayleighStrength = 2.1f;
        [SerializeField, Range(0f, 8f)] private float mieStrength = 0.7f;
        [SerializeField, Range(0f, 8f)] private float horizonStrength = 2.4f;
        [SerializeField, Range(0f, 1f)] private float nightOpacity = 0.12f;

        public float OuterRadiusMultiplier => outerRadiusMultiplier;
        public Color RayleighColor => rayleighColor;
        public Color MieColor => mieColor;
        public float RayleighStrength => rayleighStrength;
        public float MieStrength => mieStrength;
        public float HorizonStrength => horizonStrength;
        public float NightOpacity => nightOpacity;
    }
}
