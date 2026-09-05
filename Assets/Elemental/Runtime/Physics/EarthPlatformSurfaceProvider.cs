using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthPlatformSurfaceProvider : MonoBehaviour, IEarthSurfaceProvider
    {
        [SerializeField] private EarthPlatform platform;
        [SerializeField] private EarthSurfaceQueryService queryService;

        public void Configure(EarthPlatform configuredPlatform, EarthSurfaceQueryService configuredService)
        {
            if (queryService != null) queryService.Unregister(this);
            platform = configuredPlatform;
            queryService = configuredService;
            if (isActiveAndEnabled) queryService?.Register(this);
        }

        public bool TrySample(in EarthSurfaceQuery query, out EarthSurfaceSample sample)
        {
            sample = default;
            if (platform == null || !platform.IsSurfaceAvailable) return false;
            var ray = new Ray(ToVector3(query.Origin), ToVector3(query.Direction));
            Collider surfaceCollider = platform.SurfaceCollider;
            Vector3 up = platform.SurfaceUp;
            bool requiresWalkableTop = (query.RequiredCapabilities &
                                        (EarthSurfaceCapabilities.Support |
                                         EarthSurfaceCapabilities.Pillar |
                                         EarthSurfaceCapabilities.LandingCushion)) != 0;
            Vector3 analyticPoint = default;
            float analyticDistance = 0f;
            bool hasAnalyticTop = requiresWalkableTop &&
                                  platform.TrySampleTopSurface(
                                      ray,
                                      query.MaximumDistance,
                                      out analyticPoint,
                                      out analyticDistance);
            RaycastHit hit = default;
            bool hasColliderHit = surfaceCollider != null &&
                                  surfaceCollider.Raycast(
                                      ray,
                                      out hit,
                                      query.MaximumDistance);
            if (!hasAnalyticTop && !hasColliderHit) return false;

            // Moving-support queries need a stable semantic top surface even while
            // the kinematic collider is being restored or PhysX returns a side
            // triangle first. The same authored polygon used to build the prism is
            // authoritative here, so this does not invent support outside the mesh.
            bool top = hasAnalyticTop || Vector3.Dot(hit.normal, up) >= 0.72f;
            float upDot = top ? 1f : Vector3.Dot(hit.normal, up);
            if (upDot < -0.55f) return false;
            Vector3 point = hasAnalyticTop ? analyticPoint : hit.point;
            float distance = hasAnalyticTop ? analyticDistance : hit.distance;
            Vector3 normal = top ? up : hit.normal.normalized;
            EarthSurfaceCapabilities capabilities = EarthSurfaceCapabilities.Draw |
                                                    EarthSurfaceCapabilities.Destructible;
            if (top)
                capabilities |= EarthSurfaceCapabilities.Support | EarthSurfaceCapabilities.Pillar |
                                EarthSurfaceCapabilities.LandingCushion;
            if (platform.IsEmerging || platform.SurfaceVelocity.sqrMagnitude > 0.0001f)
                capabilities |= EarthSurfaceCapabilities.Moving;
            sample = new EarthSurfaceSample(
                new EarthSurfaceHandle(
                    top ? EarthSurfaceKind.Platform : EarthSurfaceKind.PlatformSide,
                    platform.PlatformId,
                    platform.Generation,
                    top ? (byte)0 : FaceId(normal)),
                EarthSurfaceQueryService.ToFloat3(point),
                EarthSurfaceQueryService.ToFloat3(normal),
                EarthSurfaceQueryService.ToFloat3(platform.transform.right),
                EarthSurfaceQueryService.ToFloat3(platform.SurfaceVelocity),
                distance,
                EarthSurfaceMaterial.RaisedEarth,
                EarthSurfaceProvenance.RaisedPlatform,
                capabilities);
            return true;
        }

        public bool IsCurrent(in EarthSurfaceHandle handle) =>
            platform != null && platform.IsSurfaceAvailable &&
            (handle.Kind == EarthSurfaceKind.Platform || handle.Kind == EarthSurfaceKind.PlatformSide) &&
            handle.StableId == platform.PlatformId && handle.Generation == platform.Generation;

        private byte FaceId(Vector3 normal)
        {
            float right = Vector3.Dot(normal, platform.transform.right);
            float forward = Vector3.Dot(normal, platform.transform.forward);
            if (Mathf.Abs(right) >= Mathf.Abs(forward)) return right >= 0f ? (byte)1 : (byte)2;
            return forward >= 0f ? (byte)3 : (byte)4;
        }

        private void OnEnable() => queryService?.Register(this);
        private void OnDisable() => queryService?.Unregister(this);
        private static Vector3 ToVector3(Unity.Mathematics.float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
