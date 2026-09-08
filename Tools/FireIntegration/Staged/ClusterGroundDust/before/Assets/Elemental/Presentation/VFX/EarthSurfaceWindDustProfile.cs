using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [CreateAssetMenu(menuName = "Elemental/VFX/Surface Wind Dust", fileName = "EarthSurfaceWindDustProfile")]
    public sealed class EarthSurfaceWindDustProfile : ScriptableObject
    {
        [Header("Low wind across the arena")]
        public bool enabledEffect = true;
        [Range(32, 256)] public int maximumParticles = 192;
        [Range(2f, 30f), Tooltip("Radius around the arena anchor, metres.")] public float areaRadius = 16f;
        [Range(0f, 48f), Tooltip("Particles per second across open ground.")] public float groundRate = 12f;
        [Range(0f, 48f), Tooltip("Additional particles per second beside settled stones.")] public float stoneRate = 36f;
        public Vector3 worldWind = new Vector3(.9f, .08f, .4f);
        [Range(.1f, 4f), Tooltip("Tangential travel speed, metres per second.")] public float speed = 1.45f;
        public Vector2 sizeMetres = new Vector2(.55f, 1.25f);
        public Vector2 lifetimeSeconds = new Vector2(2.1f, 3.6f);
        [Range(.03f, .6f)] public float opacity = .32f;
        [Range(.02f, .4f), Tooltip("Centre height above the supporting surface, metres.")] public float hoverHeight = .14f;
        [Header("Settled stone sampling")]
        [Range(.2f, 3f)] public float stoneScanSeconds = .8f;
        [Range(.1f, 3f)] public float maximumSettledSpeed = .7f;
        [Range(.1f, 2f)] public float stoneMargin = .45f;
    }
}
