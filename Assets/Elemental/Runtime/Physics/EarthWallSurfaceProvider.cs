using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthWallSurfaceProvider : MonoBehaviour, IEarthSurfaceProvider
    {
        [SerializeField] private EarthWall wall;
        [SerializeField] private EarthSurfaceQueryService queryService;

        public void Configure(EarthWall configuredWall, EarthSurfaceQueryService configuredService)
        {
            if (queryService != null) queryService.Unregister(this);
            wall = configuredWall;
            queryService = configuredService;
            if (isActiveAndEnabled) queryService?.Register(this);
        }

        public bool TrySample(in EarthSurfaceQuery query, out EarthSurfaceSample sample)
        {
            sample = default;
            if (wall == null || !wall.IsSurfaceAvailable || wall.SurfaceCollider == null) return false;
            var ray = new Ray(ToVector3(query.Origin), ToVector3(query.Direction));
            if (!wall.SurfaceCollider.Raycast(ray, out RaycastHit hit, query.MaximumDistance)) return false;
            Vector3 up = wall.SurfaceUp;
            float upDot = Vector3.Dot(hit.normal, up);
            if (upDot < -0.55f) return false;
            bool top = upDot >= 0.72f;
            Vector3 normal = top ? up : hit.normal.normalized;
            byte faceId = top ? (byte)0 : FaceId(normal);
            EarthSurfaceCapabilities capabilities = EarthSurfaceCapabilities.Draw |
                                                    EarthSurfaceCapabilities.Destructible;
            if (top)
                capabilities |= EarthSurfaceCapabilities.Support | EarthSurfaceCapabilities.Pillar |
                                EarthSurfaceCapabilities.LandingCushion;
            if (wall.Body != null && !wall.Body.isKinematic)
                capabilities |= EarthSurfaceCapabilities.Moving;
            sample = new EarthSurfaceSample(
                new EarthSurfaceHandle(
                    top ? EarthSurfaceKind.WallTop : EarthSurfaceKind.WallSide,
                    wall.WallId,
                    wall.Generation,
                    faceId),
                EarthSurfaceQueryService.ToFloat3(hit.point),
                EarthSurfaceQueryService.ToFloat3(normal),
                EarthSurfaceQueryService.ToFloat3(wall.transform.right),
                EarthSurfaceQueryService.ToFloat3(wall.Body != null ? wall.Body.linearVelocity : Vector3.zero),
                hit.distance,
                EarthSurfaceMaterial.ConstructedEarth,
                EarthSurfaceProvenance.RaisedWall,
                capabilities);
            return true;
        }

        public bool IsCurrent(in EarthSurfaceHandle handle) =>
            wall != null && wall.IsSurfaceAvailable &&
            (handle.Kind == EarthSurfaceKind.WallTop || handle.Kind == EarthSurfaceKind.WallSide) &&
            handle.StableId == wall.WallId && handle.Generation == wall.Generation;

        private byte FaceId(Vector3 normal)
        {
            float right = Vector3.Dot(normal, wall.transform.right);
            float forward = Vector3.Dot(normal, wall.transform.forward);
            if (Mathf.Abs(right) >= Mathf.Abs(forward)) return right >= 0f ? (byte)1 : (byte)2;
            return forward >= 0f ? (byte)3 : (byte)4;
        }

        private void OnEnable() => queryService?.Register(this);
        private void OnDisable() => queryService?.Unregister(this);
        private static Vector3 ToVector3(Unity.Mathematics.float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
