using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [CreateAssetMenu(menuName = "Elemental/VFX/Surface Wind Dust", fileName = "EarthSurfaceWindDustProfile")]
    public sealed class EarthSurfaceWindDustProfile : ScriptableObject
    {
        [Header("Low wind across the arena")]
        public bool enabledEffect = true;
        [Header("Optional quiet clustered ground wisps")]
        public bool clusteredWisps;
        [Range(.5f,8f)] public float clusterRadius=3.5f;
        [Range(1,12)] public int saturatedNeighbours=4;
        [Range(.05f,1f)] public float isolatedStoneWeight=.2f;
        [Range(1f,2f)] public float clusteredRateMultiplier=1.6f;
        [Range(0f,1f)] public float gapEmissionChance=.65f;
        [Range(0f,1f)] public float reducedMotionRate=.3f;
        [Range(0f,1f)] public float reducedMotionSpeed=.35f;
        [Range(32, 512)] public int maximumParticles = 192;
        [Range(2f, 30f), Tooltip("Radius around the arena anchor, metres.")] public float areaRadius = 16f;
        [Range(0f, 48f), Tooltip("Particles per second across open ground.")] public float groundRate = 12f;
        [Range(0f, 128f), Tooltip("Additional particles per second beside settled stones.")] public float stoneRate = 36f;
        [Range(0f,1f)] public float seamVolumeFraction=.35f;
        public Vector3 worldWind = new Vector3(.9f, .08f, .4f);
        [Range(.1f, 4f), Tooltip("Tangential travel speed, metres per second.")] public float speed = 1.45f;
        [Header("Coherent travelling gusts")]
        [Range(0f, .4f)] public float gustStrength = .18f;
        [Range(2f, 30f)] public float gustPeriodSeconds = 10f;
        [Range(4f, 60f)] public float gustWavelengthMetres = 24f;
        [Range(0f, .2f), Tooltip("Small tangential meander; zero keeps the wind direction straight.")] public float turbulenceStrength = .075f;
        public Vector2 sizeMetres = new Vector2(.55f, 1.25f);
        [Header("Stone wake curls")]
        [Range(0f, 2f)] public float wakeStrength = .85f;
        [Range(.5f, 5f)] public float wakeRadius = 2.4f;
        public Vector2 lifetimeSeconds = new Vector2(2.1f, 3.6f);
        [Range(.03f, .8f)] public float opacity = .32f;
        [Range(.02f, .4f), Tooltip("Centre height above the supporting surface, metres.")] public float hoverHeight = .14f;
        [Header("Settled stone sampling")]
        [Range(.2f, 3f)] public float stoneScanSeconds = .8f;
        [Range(.1f, 3f)] public float maximumSettledSpeed = .7f;
        [Range(.1f, 2f)] public float stoneMargin = .45f;
    }
}
