using Elemental.Runtime.World;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Runtime.Geometry
{
    /// <summary>Cold-prepared chipped chamfers on the matching structural cell.</summary>
    public static class EarthWallFractureVisual
    {
        // A shallow solid backing closes the daylight holes where several wide
        // chamfers meet. Every cell uses the same wall-space depth contraction,
        // so the backing is itself an exact partition and travels with its cell.
        public static Mesh SealChamferJunctions(Mesh beveled, Mesh source,
            Matrix4x4 cellToWall, float thicknessMeters, float bevelMeters)
        {
            Mesh backing = Object.Instantiate(source);
            Vector3[] points = backing.vertices;
            Matrix4x4 wallToCell = cellToWall.inverse;
            float depth = Mathf.Clamp(1f - 2f * bevelMeters / Mathf.Max(.01f, thicknessMeters), .5f, .98f);
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 wallPoint = cellToWall.MultiplyPoint3x4(points[i]);
                wallPoint.z *= depth;
                points[i] = wallToCell.MultiplyPoint3x4(wallPoint);
            }
            backing.vertices = points;
            backing.RecalculateNormals();
            backing.RecalculateBounds();
            var combined = new Mesh { name = beveled.name + " Sealed", indexFormat = IndexFormat.UInt32 };
            var parts = new List<CombineInstance>();
            for (int i = 0; i < beveled.subMeshCount; i++)
                parts.Add(new CombineInstance { mesh = beveled, subMeshIndex = i, transform = Matrix4x4.identity });
            for (int i = 0; i < backing.subMeshCount; i++)
                parts.Add(new CombineInstance { mesh = backing, subMeshIndex = i, transform = Matrix4x4.identity });
            // Wall cells use the same shipping sandstone on every face.
            combined.CombineMeshes(parts.ToArray(), true, false);
            if (Application.isPlaying) Object.Destroy(backing); else Object.DestroyImmediate(backing);
            return combined;
        }

        public static Mesh CombineIntact(Transform[] pieces, Transform wallFrame)
        {
            int submeshCount = 0;
            foreach (Transform piece in pieces)
                submeshCount = Mathf.Max(submeshCount, piece.GetComponent<MeshFilter>().sharedMesh.subMeshCount);
            var groups = new CombineInstance[submeshCount];
            var temporary = new Mesh[submeshCount];
            for (int submesh = 0; submesh < submeshCount; submesh++)
            {
                var instances = new List<CombineInstance>(pieces.Length);
                foreach (Transform piece in pieces)
                {
                    Mesh mesh = piece.GetComponent<MeshFilter>().sharedMesh;
                    if (submesh >= mesh.subMeshCount) continue;
                    instances.Add(new CombineInstance
                    {
                        mesh = mesh, subMeshIndex = submesh,
                        transform = wallFrame.worldToLocalMatrix * piece.localToWorldMatrix
                    });
                }
                temporary[submesh] = new Mesh { indexFormat = IndexFormat.UInt32 };
                temporary[submesh].CombineMeshes(instances.ToArray(), true, true, false);
                groups[submesh] = new CombineInstance { mesh = temporary[submesh], transform = Matrix4x4.identity };
            }
            var intact = new Mesh { name = "Earth Wall Exact Cell Assembly", indexFormat = IndexFormat.UInt32 };
            intact.CombineMeshes(groups, false, false, false);
            intact.RecalculateBounds();
            foreach (Mesh mesh in temporary)
            {
                if (Application.isPlaying) Object.Destroy(mesh);
                else Object.DestroyImmediate(mesh);
            }
            return intact;
        }

        public static Mesh Create(Mesh source, Mesh collider, EarthStoneBevelProfile profile, uint seed,
            Vector3 metricScale = default)
        {
            if (metricScale == Vector3.zero) metricScale = Vector3.one;
            metricScale = new Vector3(Mathf.Max(.001f, Mathf.Abs(metricScale.x)),
                Mathf.Max(.001f, Mathf.Abs(metricScale.y)), Mathf.Max(.001f, Mathf.Abs(metricScale.z)));
            Mesh metricSource = Object.Instantiate(source);
            Vector3[] metricVertices = metricSource.vertices;
            Vector3[] metricNormals = metricSource.normals;
            for (int i = 0; i < metricVertices.Length; i++)
            {
                metricVertices[i] = Vector3.Scale(metricVertices[i], metricScale);
                if (i < metricNormals.Length) metricNormals[i] = new Vector3(
                    metricNormals[i].x / metricScale.x, metricNormals[i].y / metricScale.y,
                    metricNormals[i].z / metricScale.z).normalized;
            }
            metricSource.vertices = metricVertices; metricSource.normals = metricNormals;
            Mesh render = EarthFractureBevelMeshBuilder.Create(metricSource,
                profile != null ? profile.WallWidthMeters : EarthStoneBevelProfile.DefaultWallWidthMeters,
                profile != null ? profile.WallMaxLocalEdgeFraction : EarthStoneBevelProfile.DefaultWallMaxLocalEdgeFraction,
                seed, .25f, 1f);
            if (Application.isPlaying) Object.Destroy(metricSource); else Object.DestroyImmediate(metricSource);
            render.name = source.name + " Chipped Cell";
            // Acute-corner chamfers may overshoot another face. Bound every
            // generated point by the original convex cell; collision never changes.
            Vector3[] hull = collider.vertices;
            for (int i = 0; i < hull.Length; i++) hull[i] = Vector3.Scale(hull[i], metricScale);
            int[] triangles = collider.triangles;
            Vector3 center = Vector3.zero;
            for (int index = 0; index < hull.Length; index++) center += hull[index];
            center /= Mathf.Max(1, hull.Length);
            Vector3[] vertices = render.vertices;
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 ray = vertices[index] - center;
                float fraction = 1f;
                for (int face = 0; face < triangles.Length; face += 3)
                {
                    Vector3 a = hull[triangles[face]];
                    Vector3 normal = Vector3.Cross(hull[triangles[face + 1]] - a,
                        hull[triangles[face + 2]] - a).normalized;
                    if (Vector3.Dot(normal, center - a) > 0f) normal = -normal;
                    float distance = Vector3.Dot(normal, ray);
                    if (distance > 0.000001f)
                        fraction = Mathf.Min(fraction, Mathf.Max(0f, Vector3.Dot(normal, a - center)) / distance);
                }
                Vector3 metricVertex = center + ray * fraction;
                vertices[index] = new Vector3(metricVertex.x / metricScale.x,
                    metricVertex.y / metricScale.y, metricVertex.z / metricScale.z);
            }
            render.vertices = vertices;
            // Clipping changes the actual triangle planes. Rebuild normals after
            // the final geometry, preserving hard seams through duplicated corners.
            render.RecalculateNormals();
            render.RecalculateBounds();
            render.RecalculateTangents();
            return render;
        }
    }
}
