using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    /// <summary>
    /// Explicit scene boundary for duel coverage and its directional key light.
    /// Hero rocks and large active fragments extend this base coverage through
    /// registered caster bounds; camera state is intentionally not an input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelShadowBoundsProvider : MonoBehaviour
    {
        [SerializeField] private Light directionalLight = null;
        [SerializeField] private Transform player = null;
        [SerializeField] private Transform opponent = null;
        [SerializeField] private Transform arenaCenter = null;
        [SerializeField, Min(0.1f)] private float duelistRadius = 1.25f;
        [SerializeField, Min(0.1f)] private float arenaRadius = 12f;
        [SerializeField, Min(0.1f)] private float arenaVerticalExtent = 4f;

        private static DuelShadowBoundsProvider s_Active;

        public static DuelShadowBoundsProvider Active => s_Active;

        private void OnEnable()
        {
            if (s_Active != null && s_Active != this)
            {
                Debug.LogError(
                    "Only one active DuelShadowBoundsProvider may own duel coverage.",
                    this);
                enabled = false;
                return;
            }

            s_Active = this;
        }

        private void OnDisable()
        {
            if (s_Active == this)
                s_Active = null;
        }

        public bool TryGetCoverage(
            out Bounds coverage,
            out Vector3 lightDirection,
            out Vector3 referenceUp)
        {
            coverage = default;
            lightDirection = Vector3.zero;
            referenceUp = Vector3.up;
            if (directionalLight == null ||
                directionalLight.type != LightType.Directional ||
                player == null ||
                opponent == null ||
                arenaCenter == null)
                return false;

            lightDirection = directionalLight.transform.forward;
            referenceUp = arenaCenter.up;
            coverage = new Bounds(
                arenaCenter.position,
                new Vector3(
                    arenaRadius * 2f,
                    arenaVerticalExtent * 2f,
                    arenaRadius * 2f));
            EncapsulateSphere(ref coverage, player.position, duelistRadius);
            EncapsulateSphere(ref coverage, opponent.position, duelistRadius);
            return DuelShadowMath.IsFinite(coverage.center) &&
                DuelShadowMath.IsFinite(coverage.extents) &&
                DuelShadowMath.IsFinite(lightDirection) &&
                lightDirection.sqrMagnitude > 0.000001f;
        }

        private static void EncapsulateSphere(
            ref Bounds bounds,
            Vector3 center,
            float radius)
        {
            Vector3 extent = Vector3.one * Mathf.Max(0.01f, radius);
            bounds.Encapsulate(center - extent);
            bounds.Encapsulate(center + extent);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            duelistRadius = Mathf.Max(0.1f, duelistRadius);
            arenaRadius = Mathf.Max(0.1f, arenaRadius);
            arenaVerticalExtent = Mathf.Max(0.1f, arenaVerticalExtent);
        }
#endif
    }
}
