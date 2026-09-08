using UnityEngine;

namespace Elemental.Runtime.World
{
    [CreateAssetMenu(menuName = "Elemental/World/Stone Bevel", fileName = "EarthStoneBevelProfile")]
    public sealed class EarthStoneBevelProfile : ScriptableObject
    {
        public const float DefaultWidth = .02f;
        public const float DefaultMaxLocalEdgeFraction = .08f;
        public const float DefaultWallWidthMeters = .0525f;
        public const float DefaultWallMaxLocalEdgeFraction = .25f;

        [Tooltip("Render-only chamfer width in source-mesh local units. Cached meshes update on the next build / Play restart; collider geometry is unchanged.")]
        [SerializeField, Range(0f, .25f)] private float width = DefaultWidth;
        [Tooltip("Maximum chamfer width as a fraction of the shortest edge touching each corner. Protects small fragments from oversized bevels.")]
        [SerializeField, Range(0f, .25f)] private float maxLocalEdgeFraction = DefaultMaxLocalEdgeFraction;
        [Header("Wall and matching detached cells")]
        [Tooltip("World-space chamfer width for the exact intact/fractured wall cell assembly. Ordinary rock bevels above are independent.")]
        [SerializeField, Range(0f, .15f)] private float wallWidthMeters = DefaultWallWidthMeters;
        [Tooltip("Bounds the wider wall chamfer on short cell edges, preventing inverted corners.")]
        [SerializeField, Range(0f, .25f)] private float wallMaxLocalEdgeFraction = DefaultWallMaxLocalEdgeFraction;

        public float Width => float.IsFinite(width) ? Mathf.Clamp(width, 0f, .25f) : DefaultWidth;
        public float MaxLocalEdgeFraction => float.IsFinite(maxLocalEdgeFraction)
            ? Mathf.Clamp(maxLocalEdgeFraction, 0f, .25f) : DefaultMaxLocalEdgeFraction;
        public float WallWidthMeters => float.IsFinite(wallWidthMeters)
            ? Mathf.Clamp(wallWidthMeters, 0f, .15f) : DefaultWallWidthMeters;
        public float WallMaxLocalEdgeFraction => float.IsFinite(wallMaxLocalEdgeFraction)
            ? Mathf.Clamp(wallMaxLocalEdgeFraction, 0f, .25f) : DefaultWallMaxLocalEdgeFraction;
    }
}
