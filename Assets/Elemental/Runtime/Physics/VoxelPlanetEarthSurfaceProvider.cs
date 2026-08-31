using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class VoxelPlanetEarthSurfaceProvider : MonoBehaviour, IEarthSurfaceProvider
    {
        private const uint PlanetSurfaceId = 1u;
        private const uint PlanetGeneration = 1u;

        [SerializeField] private Collider surfaceCollider;
        [SerializeField] private VoxelPlanetBehaviour voxelPlanet;
        [SerializeField] private EarthSurfaceQueryService queryService;
        private readonly RaycastHit[] _assistHits = new RaycastHit[8];

        public void Configure(
            Collider configuredCollider,
            VoxelPlanetBehaviour configuredPlanet,
            EarthSurfaceQueryService configuredService)
        {
            if (queryService != null) queryService.Unregister(this);
            surfaceCollider = configuredCollider;
            voxelPlanet = configuredPlanet;
            queryService = configuredService;
            if (isActiveAndEnabled) queryService?.Register(this);
        }

        public bool TrySample(in EarthSurfaceQuery query, out EarthSurfaceSample sample)
        {
            sample = default;
            if (!isActiveAndEnabled || surfaceCollider == null || !surfaceCollider.enabled) return false;
            var ray = new Ray(ToVector3(query.Origin), ToVector3(query.Direction));
            if (!TryRaycastSurface(ray, in query, out RaycastHit hit)) return false;
            Vector3 center = voxelPlanet != null ? voxelPlanet.transform.position : surfaceCollider.bounds.center;
            Vector3 normal = (hit.point - center).normalized;
            if (normal.sqrMagnitude < 0.5f) normal = hit.normal;
            sample = new EarthSurfaceSample(
                new EarthSurfaceHandle(EarthSurfaceKind.Planet, PlanetSurfaceId, PlanetGeneration),
                EarthSurfaceQueryService.ToFloat3(hit.point),
                EarthSurfaceQueryService.ToFloat3(normal),
                EarthSurfaceQueryService.ToFloat3(transform.right),
                default,
                hit.distance,
                EarthSurfaceMaterial.PlanetStone,
                EarthSurfaceProvenance.VoxelPlanet,
                EarthSurfaceCapabilities.Support | EarthSurfaceCapabilities.Pillar |
                EarthSurfaceCapabilities.LandingCushion | EarthSurfaceCapabilities.Draw);
            return true;
        }

        public bool IsCurrent(in EarthSurfaceHandle handle) =>
            isActiveAndEnabled && handle.Kind == EarthSurfaceKind.Planet &&
            handle.StableId == PlanetSurfaceId && handle.Generation == PlanetGeneration;

        public bool TryGetCharacterSupport(
            Collider candidate,
            out uint surfaceId,
            out uint generation)
        {
            bool valid = isActiveAndEnabled && surfaceCollider != null &&
                         surfaceCollider.enabled && candidate == surfaceCollider;
            surfaceId = valid ? PlanetSurfaceId : 0u;
            generation = valid ? PlanetGeneration : 0u;
            return valid;
        }

        private void OnEnable()
        {
            if (surfaceCollider == null) surfaceCollider = GetComponent<Collider>();
            queryService?.Register(this);
        }

        private void OnDisable() => queryService?.Unregister(this);
        private bool TryRaycastSurface(
            Ray ray,
            in EarthSurfaceQuery query,
            out RaycastHit selected)
        {
            if (surfaceCollider.Raycast(ray, out selected, query.MaximumDistance)) return true;
            selected = default;
            if (query.CastRadius <= 0.0001f) return false;
            int count = UnityEngine.Physics.SphereCastNonAlloc(
                ray,
                query.CastRadius,
                _assistHits,
                query.MaximumDistance,
                ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit candidate = _assistHits[index];
                if (candidate.collider != surfaceCollider || candidate.distance >= nearest) continue;
                selected = candidate;
                nearest = candidate.distance;
            }
            return selected.collider != null;
        }
        private static Vector3 ToVector3(Unity.Mathematics.float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
