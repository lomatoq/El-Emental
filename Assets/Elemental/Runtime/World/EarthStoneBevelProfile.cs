using UnityEngine;

namespace Elemental.Runtime.World
{
    [CreateAssetMenu(menuName = "Elemental/World/Stone Bevel", fileName = "EarthStoneBevelProfile")]
    public sealed class EarthStoneBevelProfile : ScriptableObject
    {
        public const float DefaultWidth = .02f;
        public const float DefaultMaxLocalEdgeFraction = .08f;

        [Tooltip("Render-only chamfer width in source-mesh local units. Cached meshes update on the next build / Play restart; collider geometry is unchanged.")]
        [SerializeField, Range(0f, .25f)] private float width = DefaultWidth;
        [Tooltip("Maximum chamfer width as a fraction of the shortest edge touching each corner. Protects small fragments from oversized bevels.")]
        [SerializeField, Range(0f, .25f)] private float maxLocalEdgeFraction = DefaultMaxLocalEdgeFraction;

        public float Width => float.IsFinite(width) ? Mathf.Clamp(width, 0f, .25f) : DefaultWidth;
        public float MaxLocalEdgeFraction => float.IsFinite(maxLocalEdgeFraction)
            ? Mathf.Clamp(maxLocalEdgeFraction, 0f, .25f) : DefaultMaxLocalEdgeFraction;
    }
}
