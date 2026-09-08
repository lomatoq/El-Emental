using Elemental.Runtime.Physics;
using Elemental.Simulation.Fire;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

namespace Elemental.Runtime.Fire
{
    public struct FireSurfaceAnchor
    {
        public FireSurfaceHandle Handle;
        public Collider Collider;
        public FireSurfaceBinding Binding;
        public EarthWall Wall;
        public EarthPlatform Platform;
        public EarthArenaStructure Arena;
        public EarthPieceRuntime WallPiece;
        public EarthArenaPiece ArenaPiece;
        public EarthPlatformPiece PlatformPiece;
        public FireConvexMeshGeometry Geometry;
        public uint GeometryVersion;
        public Vector3 LocalPoint, LocalNormal, BoxSize, BoxCenter;
        public float RequestedRadius;
    }

    // Existing Earth owners supply identity and lifecycle. Collider instance IDs are never canonical.
    // Boxes and proven closed convex meshes admit finite discs bounded by their actual supporting planes.
    public sealed class FireSurfaceResolver
    {
        private readonly FireConvexMeshGeometry[] _geometry = new FireConvexMeshGeometry[32];
        private readonly List<Vector3> _vertices = new List<Vector3>(FireConvexMeshGeometry.MaximumVertices);
        private readonly List<int> _indices = new List<int>(FireConvexMeshGeometry.MaximumIndices);
        private readonly List<int> _subIndices = new List<int>(FireConvexMeshGeometry.MaximumIndices);
        private int _geometryCursor;
        public int MeshCacheRebuilds { get; private set; }
        public FireSurfaceResolver()
        { for (int i = 0; i < _geometry.Length; i++) _geometry[i] = new FireConvexMeshGeometry(); }
        public int UnsupportedGeometry { get; private set; }
        public int MissingIdentity { get; private set; }
        public bool TryResolve(Collider collider, Vector3 point, float radius, out FireSurfaceAnchor anchor,
            out FireContactPatch patch)
        {
            anchor = default; patch = default;
            if (collider == null || !(collider is BoxCollider) && !(collider is MeshCollider))
            { UnsupportedGeometry++; return false; }
            Collider box = collider;
            var wall = box.GetComponentInParent<EarthWall>();
            var platform = box.GetComponentInParent<EarthPlatform>();
            var arena = box.GetComponentInParent<EarthArenaStructure>();
            var binding = box.GetComponentInParent<FireSurfaceBinding>();
            var wallPiece = box.GetComponentInParent<EarthPieceRuntime>();
            var arenaPiece = box.GetComponentInParent<EarthArenaPiece>();
            var platformPiece = box.GetComponentInParent<EarthPlatformPiece>();
            uint space, id, generation, revision, piece = 0;
            // Earth authority wins over an optional authored binding on the same hierarchy.
            if (wallPiece != null)
            {
                if (!wallPiece.IsEarthTargetValid || wallPiece.Owner == null || wallPiece.PieceIndex < 0) return false;
                space = 5; id = wallPiece.Owner.WallId; generation = wallPiece.Owner.Generation;
                revision = 1; piece = (uint)wallPiece.PieceIndex + 1;
            }
            else if (arenaPiece != null)
            {
                if (!arenaPiece.IsEarthTargetValid || arenaPiece.Owner == null || arenaPiece.PieceIndex < 0) return false;
                space = 6; id = arenaPiece.Owner.StructureId; generation = arenaPiece.Owner.Generation;
                revision = 1; piece = (uint)arenaPiece.PieceIndex + 1;
            }
            else if (platformPiece != null)
            {
                if (!platformPiece.IsEarthTargetValid || platformPiece.Owner == null || platformPiece.PieceIndex < 0) return false;
                space = 7; id = platformPiece.Owner.PlatformId; generation = platformPiece.Owner.Generation;
                revision = 1; piece = (uint)platformPiece.PieceIndex + 1;
            }
            else if (wall != null)
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
            anchor = new FireSurfaceAnchor
            {
                Collider = collider, Binding = binding, Wall = wall, Platform = platform, Arena = arena,
                WallPiece = wallPiece, ArenaPiece = arenaPiece, PlatformPiece = platformPiece, RequestedRadius = radius
            };
            if (collider is MeshCollider meshCollider)
            {
                if (!meshCollider.convex || !TryGeometry(meshCollider.sharedMesh, out FireConvexMeshGeometry geometry) ||
                    !geometry.TryFace(collider.transform, point, radius, out int face, out Vector3 meshPoint, out Vector3 meshNormal, out _))
                { UnsupportedGeometry++; return false; }
                anchor.Handle = new FireSurfaceHandle(space, id, generation, revision, (uint)face, piece);
                anchor.Geometry = geometry; anchor.GeometryVersion = geometry.Version;
                anchor.LocalPoint = collider.transform.InverseTransformPoint(meshPoint);
                anchor.LocalNormal = collider.transform.InverseTransformDirection(meshNormal);
                return TryRefresh(in anchor, out patch);
            }
            BoxCollider primitive = (BoxCollider)collider;
            Vector3 local = box.transform.InverseTransformPoint(point) - primitive.center;
            Vector3 half = primitive.size * 0.5f;
            Vector3 delta = new Vector3(Mathf.Abs(Mathf.Abs(local.x) - half.x),
                Mathf.Abs(Mathf.Abs(local.y) - half.y), Mathf.Abs(Mathf.Abs(local.z) - half.z));
            int axis = delta.x <= delta.y && delta.x <= delta.z ? 0 : delta.y <= delta.z ? 1 : 2;
            float sign = local[axis] >= 0 ? 1 : -1;
            Vector3 normal = Vector3.zero; normal[axis] = sign;
            local[axis] = half[axis] * sign;
            anchor.Handle = new FireSurfaceHandle(space, id, generation, revision, (uint)(axis * 2 + (sign > 0 ? 0 : 1)), piece);
            anchor.LocalPoint = local + primitive.center; anchor.LocalNormal = normal;
            anchor.BoxSize = primitive.size; anchor.BoxCenter = primitive.center;
            return TryRefresh(in anchor, out patch);
        }

        public bool TryRefresh(in FireSurfaceAnchor anchor, out FireContactPatch patch)
        {
            patch = default;
            Collider box = anchor.Collider;
            if (box == null || !box.enabled || !box.gameObject.activeInHierarchy || !anchor.Handle.IsValid) return false;
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
                case 5:
                    if (anchor.WallPiece == null || !anchor.WallPiece.IsEarthTargetValid || anchor.WallPiece.Owner == null ||
                        anchor.WallPiece.Owner.WallId != anchor.Handle.Id || anchor.WallPiece.Owner.Generation != anchor.Handle.Generation ||
                        (uint)anchor.WallPiece.PieceIndex + 1 != anchor.Handle.Piece) return false;
                    break;
                case 6:
                    if (anchor.ArenaPiece == null || !anchor.ArenaPiece.IsEarthTargetValid || anchor.ArenaPiece.Owner == null ||
                        anchor.ArenaPiece.Owner.StructureId != anchor.Handle.Id || anchor.ArenaPiece.Owner.Generation != anchor.Handle.Generation ||
                        (uint)anchor.ArenaPiece.PieceIndex + 1 != anchor.Handle.Piece) return false;
                    break;
                case 7:
                    if (anchor.PlatformPiece == null || !anchor.PlatformPiece.IsEarthTargetValid || anchor.PlatformPiece.Owner == null ||
                        anchor.PlatformPiece.Owner.PlatformId != anchor.Handle.Id || anchor.PlatformPiece.Owner.Generation != anchor.Handle.Generation ||
                        (uint)anchor.PlatformPiece.PieceIndex + 1 != anchor.Handle.Piece) return false;
                    break;
                default: return false;
            }
            Transform transform = box.transform;
            Vector3 point, normal;
            float radius;
            if (box is MeshCollider meshCollider)
            {
                if (!meshCollider.convex || anchor.Geometry == null || anchor.Geometry.Version != anchor.GeometryVersion ||
                    !ReadMesh(meshCollider.sharedMesh) || !anchor.Geometry.Matches(meshCollider.sharedMesh, _vertices, _indices) ||
                    !anchor.Geometry.RefreshFace(transform, (int)anchor.Handle.Face, anchor.LocalPoint, anchor.RequestedRadius,
                        out point, out normal, out radius)) return false;
                return BuildPatch(box, point, normal, radius, out patch);
            }
            BoxCollider primitive = box as BoxCollider;
            if (primitive == null || primitive.size != anchor.BoxSize || primitive.center != anchor.BoxCenter) return false;
            Vector3 x = transform.TransformVector(Vector3.right), y = transform.TransformVector(Vector3.up), z = transform.TransformVector(Vector3.forward);
            // Sheared hierarchies cannot be treated as orthogonal box faces.
            if (Mathf.Abs(Vector3.Dot(x.normalized, y.normalized)) > 0.001f ||
                Mathf.Abs(Vector3.Dot(x.normalized, z.normalized)) > 0.001f ||
                Mathf.Abs(Vector3.Dot(y.normalized, z.normalized)) > 0.001f) return false;
            Vector3 scales = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            if (Mathf.Min(scales.x, Mathf.Min(scales.y, scales.z)) < 0.0001f) return false;
            Vector3 p = anchor.LocalPoint - primitive.center, half = primitive.size * 0.5f;
            int axis = (int)(anchor.Handle.Face / 2);
            radius = anchor.RequestedRadius;
            for (int i = 0; i < 3; i++) if (i != axis)
                radius = Mathf.Min(radius, (half[i] - Mathf.Abs(p[i])) * scales[i] - 0.002f);
            if (!math.isfinite(radius) || radius < 0.005f) return false;
            point = transform.TransformPoint(anchor.LocalPoint);
            normal = transform.TransformVector(anchor.LocalNormal).normalized;
            return BuildPatch(box, point, normal, radius, out patch);
        }

        private static bool BuildPatch(Collider box, Vector3 point, Vector3 normal, float radius, out FireContactPatch patch)
        {
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
        private bool ReadMesh(Mesh mesh)
        {
            if (mesh == null || !mesh.isReadable || mesh.subMeshCount < 1 || mesh.subMeshCount > 8 ||
                mesh.vertexCount > FireConvexMeshGeometry.MaximumVertices) return false;
            ulong total = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            { if (mesh.GetTopology(i) != MeshTopology.Triangles) return false; total += mesh.GetIndexCount(i); }
            if (total > FireConvexMeshGeometry.MaximumIndices) return false;
            _vertices.Clear(); _indices.Clear(); mesh.GetVertices(_vertices);
            for (int i = 0; i < mesh.subMeshCount; i++)
            { _subIndices.Clear(); mesh.GetTriangles(_subIndices, i); _indices.AddRange(_subIndices); }
            return true;
        }
        private bool TryGeometry(Mesh mesh, out FireConvexMeshGeometry geometry)
        {
            geometry = null; if (!ReadMesh(mesh)) return false;
            for (int i = 0; i < _geometry.Length; i++)
            {
                if (_geometry[i].Mesh != mesh) continue;
                geometry = _geometry[i];
                if (geometry.Matches(mesh, _vertices, _indices)) return geometry.Valid;
                MeshCacheRebuilds++; return geometry.Build(mesh, _vertices, _indices);
            }
            geometry = _geometry[_geometryCursor++ % _geometry.Length];
            MeshCacheRebuilds++; return geometry.Build(mesh, _vertices, _indices);
        }
        internal static float3 ToFloat(Vector3 value) => new float3(value.x, value.y, value.z);
        internal static Vector3 ToVector(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
