using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class EarthSurfaceScarPool : MonoBehaviour
    {
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private EarthFeedbackProfile profile;
        [SerializeField] private Material decalMaterial;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private DecalProjector[] projectors;

        private float[] _expiresAt;
        private int _cursor;

        public int Capacity => projectors?.Length ?? 0;
        public int ActiveCount { get; private set; }

        public void Configure(
            MagicExecutor configuredExecutor,
            EarthFeedbackProfile configuredProfile,
            Material configuredMaterial,
            Transform configuredPlanetCenter)
        {
            if (isActiveAndEnabled && executor != null) executor.Events.EarthImpactOccurred -= OnEarthImpact;
            executor = configuredExecutor;
            profile = configuredProfile;
            decalMaterial = configuredMaterial;
            planetCenter = configuredPlanetCenter;
            EnsurePool();
            if (isActiveAndEnabled && executor != null) executor.Events.EarthImpactOccurred += OnEarthImpact;
        }

        public void RebuildPool()
        {
            int count = profile != null ? profile.DecalCapacity : 24;
            projectors = new DecalProjector[count];
            _expiresAt = new float[count];
            _cursor = 0;
            ActiveCount = 0;
            for (int index = 0; index < count; index++)
            {
                GameObject decalObject = new GameObject($"Earth Surface Scar {index + 1:00}");
                decalObject.transform.SetParent(transform, false);
                DecalProjector projector = decalObject.AddComponent<DecalProjector>();
                projector.material = decalMaterial;
                projector.drawDistance = profile != null ? profile.DecalDrawDistance : 42f;
                projector.fadeFactor = 0f;
                projector.enabled = false;
                projectors[index] = projector;
            }
        }

        private void Awake() => EnsurePool();

        private void OnEnable()
        {
            EnsurePool();
            if (executor != null) executor.Events.EarthImpactOccurred += OnEarthImpact;
        }

        private void OnDisable()
        {
            if (executor != null) executor.Events.EarthImpactOccurred -= OnEarthImpact;
        }

        private void Update()
        {
            if (projectors == null || _expiresAt == null) return;
            float now = Time.time;
            float fadeDuration = profile != null ? profile.ScarFadeSeconds : 4f;
            int active = 0;
            for (int index = 0; index < projectors.Length; index++)
            {
                DecalProjector projector = projectors[index];
                if (projector == null || !projector.enabled) continue;
                float remaining = _expiresAt[index] - now;
                if (remaining <= 0f)
                {
                    projector.enabled = false;
                    projector.fadeFactor = 0f;
                    continue;
                }
                projector.fadeFactor = Mathf.Clamp01(remaining / Mathf.Max(0.01f, fadeDuration));
                active++;
            }
            ActiveCount = active;
        }

        private void OnEarthImpact(EarthImpactEvent impact)
        {
            if (profile == null || impact.Impulse < profile.MinimumScarImpulse ||
                projectors == null || projectors.Length == 0)
                return;
            EarthFeedbackSample sample = profile.Evaluate(in impact);
            int index = _cursor;
            _cursor = (_cursor + 1) % projectors.Length;
            DecalProjector projector = projectors[index];
            if (projector == null) return;

            Vector3 point = new Vector3(impact.Point.x, impact.Point.y, impact.Point.z);
            Vector3 normal = new Vector3(impact.Normal.x, impact.Normal.y, impact.Normal.z).normalized;
            if (normal.sqrMagnitude < 0.5f)
            {
                Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
                normal = (point - center).normalized;
            }
            Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.8f ? Vector3.up : Vector3.right).normalized;
            float roll = Hash01(impact.SourceId) * 360f;
            projector.transform.SetPositionAndRotation(
                point + normal * 0.035f,
                Quaternion.AngleAxis(roll, normal) * Quaternion.LookRotation(-normal, tangent));
            float diameter = sample.ScarRadius * 2f;
            projector.size = new Vector3(diameter, diameter, Mathf.Max(0.18f, sample.ScarRadius * 0.42f));
            projector.material = decalMaterial;
            projector.drawDistance = profile.DecalDrawDistance;
            projector.fadeFactor = 1f;
            projector.enabled = true;
            _expiresAt[index] = profile.PersistentSurfaceScars
                ? float.PositiveInfinity
                : Time.time + sample.Lifetime;
        }

        private void EnsurePool()
        {
            int count = profile != null ? profile.DecalCapacity : 24;
            if (projectors == null || projectors.Length != count)
            {
                RebuildPool();
                return;
            }
            if (_expiresAt == null || _expiresAt.Length != count) _expiresAt = new float[count];
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
