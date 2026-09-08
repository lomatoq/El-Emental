using Elemental.Runtime.Physics;
using Elemental.Simulation.Fire;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Fire
{
    public struct FireSurfaceAnchor
    {
        public FireSurfaceHandle Handle;
        public BoxCollider Collider;
        public FireSurfaceBinding Binding;
        public EarthWall Wall;
        public EarthPlatform Platform;
        public EarthArenaStructure Arena;
        public Vector3 LocalPoint, LocalNormal, BoxSize, BoxCenter;
        public float RequestedRadius;
    }

    // Existing Earth owners supply identity and lifecycle. Collider instance IDs are never canonical.
    // A box face admits an exact inscribed disc; arbitrary mesh footprints are deliberately rejected.
    public sealed class FireSurfaceResolver
    {
        public int UnsupportedGeometry { get; private set; }
        public int MissingIdentity { get; private set; }
        public bool TryResolve(Collider collider, Vector3 point, float radius, out FireSurfaceAnchor anchor,
            out FireContactPatch patch)
        {
            anchor = default; patch = default;
            if (!(collider is BoxCollider box)) { UnsupportedGeometry++; return false; }
            var wall = box.GetComponentInParent<EarthWall>();
            var platform = box.GetComponentInParent<EarthPlatform>();
            var arena = box.GetComponentInParent<EarthArenaStructure>();
            var binding = box.GetComponentInParent<FireSurfaceBinding>();
            uint space, id, generation, revision;
            // Earth authority wins over an optional authored binding on the same hierarchy.
            if (wall != null)
            {
                if (!wall.IsSurfaceAvailable || wall.SurfaceCollider != box) return false;
                space = 2; id = wall.WallId; generation = wall.Generation; revision = 1;
            }
            else if (platform != null)
            {
                if (!platform.IsSurfaceAvailable || platform.SurfaceCollider != box) return false;
                space = 3; id = platform.PlatformId; generation = platform.Generation; revision = 1;
            }
            else if (arena != null)
            {
                if (arena.IsFractured || arena.CameraSuppressed) return false;
                space = 4; id = arena.StructureId; generation = arena.Generation; revision = 1;
            }
            else if (binding != null && binding.IsCurrent)
            { space = 1; id = binding.StableId; generation = binding.Generation; revision = binding.Revision; }
            else { MissingIdentity++; return false; }
            Vector3 local = box.transform.InverseTransformPoint(point) - box.center;
            Vector3 half = box.size * 0.5f;
            Vector3 delta = new Vector3(Mathf.Abs(Mathf.Abs(local.x) - half.x),
                Mathf.Abs(Mathf.Abs(local.y) - half.y), Mathf.Abs(Mathf.Abs(local.z) - half.z));
            int axis = delta.x <= delta.y && delta.x <= delta.z ? 0 : delta.y <= delta.z ? 1 : 2;
            float sign = local[axis] >= 0 ? 1 : -1;
            Vector3 normal = Vector3.zero; normal[axis] = sign;
            local[axis] = half[axis] * sign;
            anchor = new FireSurfaceAnchor
            {
                Handle = new FireSurfaceHandle(space, id, generation, revision, (uint)(axis * 2 + (sign > 0 ? 0 : 1))),
                Collider = box, Binding = binding, Wall = wall, Platform = platform, Arena = arena,
                LocalPoint = local + box.center, LocalNormal = normal, BoxSize = box.size,
                BoxCenter = box.center, RequestedRadius = radius
            };
            return TryRefresh(in anchor, out patch);
        }

        public bool TryRefresh(in FireSurfaceAnchor anchor, out FireContactPatch patch)
        {
            patch = default;
            BoxCollider box = anchor.Collider;
            if (box == null || !box.enabled || !box.gameObject.activeInHierarchy || !anchor.Handle.IsValid ||
                box.size != anchor.BoxSize || box.center != anchor.BoxCenter) return false;
            switch (anchor.Handle.Namespace)
            {
                case 1:
                    if (anchor.Binding == null || !anchor.Binding.IsCurrent || anchor.Binding.StableId != anchor.Handle.Id ||
                        anchor.Binding.Generation != anchor.Handle.Generation || anchor.Binding.Revision != anchor.Handle.Revision) return false;
                    break;
                case 2:
                    if (anchor.Wall == null || !anchor.Wall.IsSurfaceAvailable || anchor.Wall.WallId != anchor.Handle.Id ||
                        anchor.Wall.Generation != anchor.Handle.Generation || anchor.Wall.SurfaceCollider != box) return false;
                    break;
                case 3:
                    if (anchor.Platform == null || !anchor.Platform.IsSurfaceAvailable || anchor.Platform.PlatformId != anchor.Handle.Id ||
                        anchor.Platform.Generation != anchor.Handle.Generation || anchor.Platform.SurfaceCollider != box) return false;
                    break;
                case 4:
                    if (anchor.Arena == null || anchor.Arena.IsFractured || anchor.Arena.CameraSuppressed ||
                        anchor.Arena.StructureId != anchor.Handle.Id || anchor.Arena.Generation != anchor.Handle.Generation) return false;
                    break;
                default: return false;
            }
            Transform transform = box.transform;
            Vector3 x = transform.TransformVector(Vector3.right), y = transform.TransformVector(Vector3.up), z = transform.TransformVector(Vector3.forward);
            // Sheared hierarchies cannot be treated as orthogonal box faces.
            if (Mathf.Abs(Vector3.Dot(x.normalized, y.normalized)) > 0.001f ||
                Mathf.Abs(Vector3.Dot(x.normalized, z.normalized)) > 0.001f ||
                Mathf.Abs(Vector3.Dot(y.normalized, z.normalized)) > 0.001f) return false;
            Vector3 scales = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            if (Mathf.Min(scales.x, Mathf.Min(scales.y, scales.z)) < 0.0001f) return false;
            Vector3 p = anchor.LocalPoint - box.center, half = box.size * 0.5f;
            int axis = (int)(anchor.Handle.Face / 2);
            float radius = anchor.RequestedRadius;
            for (int i = 0; i < 3; i++) if (i != axis)
                radius = Mathf.Min(radius, (half[i] - Mathf.Abs(p[i])) * scales[i] - 0.002f);
            if (!math.isfinite(radius) || radius < 0.005f) return false;
            Vector3 point = transform.TransformPoint(anchor.LocalPoint);
            Vector3 normal = transform.TransformVector(anchor.LocalNormal).normalized;
            Rigidbody body = box.attachedRigidbody;
            patch = new FireContactPatch
            {
                Point = ToFloat(point), Normal = ToFloat(normal), Tangent = FireContactMath.Tangent(ToFloat(normal)),
                Radius = radius, FrontDepth = 0.2f, RecoveryDepth = 0.08f, Skin = 0.015f,
                SpreadFraction = 0.8f, ResponseRate = 18, Active = true,
                SurfaceVelocity = body != null ? ToFloat(body.GetPointVelocity(point)) : float3.zero,
                AngularVelocity = body != null ? ToFloat(body.angularVelocity) : float3.zero
            };
            return FireContactMath.IsValid(patch);
        }
        internal static float3 ToFloat(Vector3 value) => new float3(value.x, value.y, value.z);
        internal static Vector3 ToVector(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
