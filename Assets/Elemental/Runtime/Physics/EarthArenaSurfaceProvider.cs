using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>
    /// Adapts an intact Broken Crown structure to the existing surface-query contract.
    /// Fracture/pluck authority remains in <see cref="EarthArenaStructure"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthArenaSurfaceProvider : MonoBehaviour, IEarthSurfaceProvider
    {
        [SerializeField] private EarthArenaStructure structure;
        [SerializeField] private Collider surfaceCollider;
        [SerializeField] private EarthSurfaceQueryService queryService;
        [SerializeField] private Vector3 surfaceUp = Vector3.up;
        [SerializeField] private bool supportsLocomotion;
        private readonly RaycastHit[] _assistHits = new RaycastHit[16];

        public void Configure(
            EarthArenaStructure configuredStructure,
            Collider configuredCollider,
            EarthSurfaceQueryService configuredService,
            Vector3 configuredUp,
            bool configuredSupportsLocomotion)
        {
            if (queryService != null) queryService.Unregister(this);
            structure = configuredStructure;
            surfaceCollider = configuredCollider;
            queryService = configuredService;
            surfaceUp = configuredUp.sqrMagnitude > 0.5f ? configuredUp.normalized : Vector3.up;
            supportsLocomotion = configuredSupportsLocomotion;
            if (isActiveAndEnabled) queryService?.Register(this);
        }

        public bool TrySample(in EarthSurfaceQuery query, out EarthSurfaceSample sample)
        {
            sample = default;
            if (structure == null || structure.IsFractured || structure.CameraSuppressed || surfaceCollider == null ||
                !surfaceCollider.enabled) return false;

            var ray = new Ray(ToVector3(query.Origin), ToVector3(query.Direction));
            if (!TryRaycastSurface(ray, in query, out RaycastHit hit)) return false;

            float upDot = Vector3.Dot(hit.normal, surfaceUp);
            if (upDot < -0.55f) return false;
            bool top = upDot >= 0.72f;
            Vector3 normal = top ? surfaceUp : hit.normal.normalized;
            EarthSurfaceCapabilities capabilities = EarthSurfaceCapabilities.Draw |
                                                    EarthSurfaceCapabilities.Destructible;
            if (supportsLocomotion && top)
                capabilities |= EarthSurfaceCapabilities.Support | EarthSurfaceCapabilities.Pillar |
                                EarthSurfaceCapabilities.LandingCushion;

            sample = new EarthSurfaceSample(
                new EarthSurfaceHandle(
                    top ? EarthSurfaceKind.Platform : EarthSurfaceKind.PlatformSide,
                    structure.StructureId,
                    structure.Generation,
                    top ? (byte)0 : FaceId(normal)),
                EarthSurfaceQueryService.ToFloat3(hit.point),
                EarthSurfaceQueryService.ToFloat3(normal),
                EarthSurfaceQueryService.ToFloat3(transform.right),
                default,
                hit.distance,
                EarthSurfaceMaterial.ConstructedEarth,
                EarthSurfaceProvenance.RaisedPlatform,
                capabilities);
            return true;
        }

        public bool IsCurrent(in EarthSurfaceHandle handle) =>
            structure != null && !structure.IsFractured && surfaceCollider != null &&
            surfaceCollider.enabled &&
            (handle.Kind == EarthSurfaceKind.Platform || handle.Kind == EarthSurfaceKind.PlatformSide) &&
            handle.StableId == structure.StructureId && handle.Generation == structure.Generation;

        public bool TryGetCharacterSupport(
            Collider candidate,
            out uint surfaceId,
            out uint generation)
        {
            surfaceId = 0u;
            generation = 0u;
            if (!supportsLocomotion || structure == null || structure.IsFractured ||
                surfaceCollider == null || candidate != surfaceCollider ||
                !surfaceCollider.enabled)
                return false;
            surfaceId = structure.StructureId;
            generation = structure.Generation;
            return surfaceId != 0u && generation != 0u;
        }

        private byte FaceId(Vector3 normal)
        {
            float right = Vector3.Dot(normal, transform.right);
            float forward = Vector3.Dot(normal, transform.forward);
            if (Mathf.Abs(right) >= Mathf.Abs(forward)) return right >= 0f ? (byte)1 : (byte)2;
            return forward >= 0f ? (byte)3 : (byte)4;
        }

        private void OnEnable() => queryService?.Register(this);
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
        private static Vector3 ToVector3(Unity.Mathematics.float3 value) =>
            new Vector3(value.x, value.y, value.z);
    }
}
