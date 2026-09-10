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
            Mesh metricCollider=Object.Instantiate(collider);
            Vector3[] hull=metricCollider.vertices;
            for(int i=0;i<hull.Length;i++)hull[i]=Vector3.Scale(hull[i],metricScale);
            metricCollider.vertices=hull;metricCollider.RecalculateBounds();
            Mesh contained=null;
            try
            {
                contained=EarthContainedRenderRepair.Create(metricSource,metricCollider,
                    profile!=null?profile.WallWidthMeters:EarthStoneBevelProfile.DefaultWallWidthMeters,
                    profile!=null?profile.WallMaxLocalEdgeFraction:EarthStoneBevelProfile.DefaultWallMaxLocalEdgeFraction,
                    seed,.25f,1f,out bool usedHull,out _);
                Vector3[] vertices=contained.vertices;
                for(int i=0;i<vertices.Length;i++)vertices[i]=new Vector3(vertices[i].x/metricScale.x,
                    vertices[i].y/metricScale.y,vertices[i].z/metricScale.z);
                contained.vertices=vertices;contained.RecalculateBounds();
                // Inverse metric transform changes face planes. Robust explicit
                // triangle normals retain the hard seams without smoothing.
                Mesh render=EarthContainedRenderRepair.FlatCopy(contained,out _);
                render.name=source.name+(usedHull?" Chipped Cell Closed Hull":" Chipped Cell Closed Bevel");
                return render;
            }
            finally
            {
                Release(metricSource);Release(metricCollider);if(contained!=null)Release(contained);
            }
            static void Release(Mesh mesh){if(Application.isPlaying)Object.Destroy(mesh);else Object.DestroyImmediate(mesh);}

        }
    }
}
