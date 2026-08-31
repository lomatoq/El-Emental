using System.Collections.Generic;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    public readonly struct EarthSurfacePlacementResult
    {
        public EarthSurfacePlacementResult(
            Vector3 rootPosition,
            Vector3 supportPoint,
            float supportError,
            float embed,
            EarthSurfaceHandle surfaceHandle,
            bool isValid)
        {
            RootPosition = rootPosition;
            SupportPoint = supportPoint;
            SupportError = supportError;
            Embed = embed;
            SurfaceHandle = surfaceHandle;
            IsValid = isValid;
        }

        public Vector3 RootPosition { get; }
        public Vector3 SupportPoint { get; }
        public float SupportError { get; }
        public float Embed { get; }
        public EarthSurfaceHandle SurfaceHandle { get; }
        public bool IsValid { get; }
    }

    /// <summary>
    /// Places the final transformed mesh by its real lowest vertex, never by pivot,
    /// nominal radius or bounds extents. Vertex data is cached once per mesh so a
    /// warmed pool performs no mesh reads or managed allocations when respawning.
    /// </summary>
    public static class EarthSurfacePlacementSolver
    {
        private static readonly ProfilerMarker PlacementMarker =
            new ProfilerMarker("Elemental.Earth.Placement");
        private static readonly Dictionary<EntityId, CachedMesh> MeshCache =
            new Dictionary<EntityId, CachedMesh>(64);

        public static EarthSurfacePlacementResult Solve(
            Mesh mesh,
            Vector3 surfacePoint,
            Vector3 surfaceNormal,
            Quaternion rotation,
            Vector3 scale,
            float embed = 0.035f,
            EarthSurfaceHandle surfaceHandle = default)
        {
            using (PlacementMarker.Auto())
            {
                if (mesh == null || !IsFinite(surfacePoint) || !IsFinite(surfaceNormal) ||
                    surfaceNormal.sqrMagnitude < 0.5f || !IsFinite(scale))
                    return default;

                Vector3[] vertices = GetVertices(mesh);
                if (vertices == null || vertices.Length == 0) return default;
                Vector3 normal = surfaceNormal.normalized;
                // Authored structures may choose a deeper hidden foundation, but
                // visible runtime contacts need the same one-centimetre gate as
                // arena props. Do not silently turn a requested 1 cm seat into the
                // historical 2 cm burial.
                float clampedEmbed = Mathf.Clamp(embed, 0f, 0.05f);
                float minimum = float.PositiveInfinity;
                Vector3 supportOffset = Vector3.zero;
                for (int index = 0; index < vertices.Length; index++)
                {
                    Vector3 scaled = Vector3.Scale(vertices[index], scale);
                    Vector3 offset = rotation * scaled;
                    float height = Vector3.Dot(offset, normal);
                    if (height >= minimum) continue;
                    minimum = height;
                    supportOffset = offset;
                }

                if (!float.IsFinite(minimum)) return default;
                Vector3 root = surfacePoint - normal * clampedEmbed - supportOffset;
                Vector3 support = root + supportOffset;
                float error = Vector3.Dot(support - surfacePoint, normal);
                return new EarthSurfacePlacementResult(
                    root,
                    support,
                    error,
                    clampedEmbed,
                    surfaceHandle,
                    true);
            }
        }

        public static bool TrySolve(
            EarthSurfaceQueryService surfaces,
            in EarthSurfaceQuery query,
            Mesh mesh,
            Quaternion rotation,
            Vector3 scale,
            float embed,
            out EarthSurfacePlacementResult result)
        {
            result = default;
            if (surfaces == null || !surfaces.TrySample(in query, out EarthSurfaceSample sample)) return false;
            result = Solve(
                mesh,
                ToVector3(sample.Point),
                ToVector3(sample.Normal),
                rotation,
                scale,
                embed,
                sample.Handle);
            return result.IsValid;
        }

        public static float MeasureSupportError(
            Mesh mesh,
            Vector3 rootPosition,
            Vector3 surfacePoint,
            Vector3 surfaceNormal,
            Quaternion rotation,
            Vector3 scale)
        {
            if (mesh == null || surfaceNormal.sqrMagnitude < 0.5f) return float.PositiveInfinity;
            Vector3[] vertices = GetVertices(mesh);
            if (vertices == null || vertices.Length == 0) return float.PositiveInfinity;
            Vector3 normal = surfaceNormal.normalized;
            float minimum = float.PositiveInfinity;
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 world = rootPosition + rotation * Vector3.Scale(vertices[index], scale);
                minimum = Mathf.Min(minimum, Vector3.Dot(world - surfacePoint, normal));
            }
            return minimum;
        }

        private static Vector3[] GetVertices(Mesh mesh)
        {
            EntityId id = mesh.GetEntityId();
            if (MeshCache.TryGetValue(id, out CachedMesh cached) &&
                cached.Mesh != null && cached.Mesh.TryGetTarget(out Mesh target) && target == mesh &&
                cached.VertexCount == mesh.vertexCount)
                return cached.Vertices;

            using Mesh.MeshDataArray dataArray = Mesh.AcquireReadOnlyMeshData(mesh);
            Mesh.MeshData data = dataArray[0];
            var native = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            data.GetVertices(native);
            Vector3[] vertices = native.ToArray();
            native.Dispose();
            MeshCache[id] = new CachedMesh(mesh, vertices);
            return vertices;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);

        private readonly struct CachedMesh
        {
            public CachedMesh(Mesh mesh, Vector3[] vertices)
            {
                Mesh = new System.WeakReference<Mesh>(mesh);
                Vertices = vertices;
                VertexCount = vertices.Length;
            }

            public System.WeakReference<Mesh> Mesh { get; }
            public Vector3[] Vertices { get; }
            public int VertexCount { get; }
        }
    }
}
