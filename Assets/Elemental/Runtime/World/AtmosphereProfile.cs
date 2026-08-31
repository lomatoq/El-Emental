using UnityEngine;

namespace Elemental.Runtime.World
{
    [CreateAssetMenu(menuName = "Elemental/World/Atmosphere Profile", fileName = "AtmosphereProfile")]
    public sealed class AtmosphereProfile : ScriptableObject
    {
        [SerializeField, Range(1.005f, 1.3f)] private float outerRadiusMultiplier = 1.2f;
        [SerializeField] private Color rayleighColor = new Color(0.36f, 0.54f, 0.76f);
        [SerializeField] private Color mieColor = new Color(1f, 0.72f, 0.42f);
        [SerializeField, Range(0f, 8f)] private float rayleighStrength = 1.15f;
        [SerializeField, Range(0f, 8f)] private float mieStrength = 0.42f;
        [SerializeField, Range(0f, 8f)] private float horizonStrength = 1.65f;
        [SerializeField, Range(0f, 1f)] private float nightOpacity = 0.10f;
        [Header("Aerial perspective")]
        [SerializeField, Range(0f, 2f)] private float aerialPerspectiveStrength = 1.28f;
        [SerializeField, Min(1f)] private float aerialPerspectiveDistance = 22f;
        [SerializeField, Range(0.1f, 8f)] private float heightFalloff = 1.05f;
        [SerializeField, Range(0f, 0.75f)] private float maximumAerialOpacity = 0.50f;
        [Header("Lightweight clouds")]
        [SerializeField, Range(0f, 1f)] private float cloudCoverage = 0.48f;
        [SerializeField, Range(0f, 0.5f)] private float cloudOpacity = 0.22f;
        [SerializeField, Range(0.1f, 4f)] private float cloudScale = 1.35f;
        [SerializeField, Range(0f, 0.1f)] private float cloudSpeed = 0.014f;

        public float OuterRadiusMultiplier => outerRadiusMultiplier;
        public Color RayleighColor => rayleighColor;
        public Color MieColor => mieColor;
        public float RayleighStrength => rayleighStrength;
        public float MieStrength => mieStrength;
        public float HorizonStrength => horizonStrength;
        public float NightOpacity => nightOpacity;
        public float AerialPerspectiveStrength => aerialPerspectiveStrength;
        public float AerialPerspectiveDistance => aerialPerspectiveDistance;
        public float HeightFalloff => heightFalloff;
        public float MaximumAerialOpacity => maximumAerialOpacity;
        public float CloudCoverage => cloudCoverage;
        public float CloudOpacity => cloudOpacity;
        public float CloudScale => cloudScale;
        public float CloudSpeed => cloudSpeed;
    }
}
