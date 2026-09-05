using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    /// <summary>Prepares a loose-stone silhouette inside the actual authored convex cell.</summary>
    public static class EarthNaturalFractureVisual
    {
        public static Mesh Create(Mesh stone, Mesh cellConvex)
        {
            if (stone == null || !stone.isReadable)
                throw new System.InvalidOperationException("Natural fracture visuals need a readable loose-stone mesh.");
            if (cellConvex == null || !cellConvex.isReadable)
                throw new System.InvalidOperationException("Natural fracture visuals need the readable authored convex cell, not an AABB.");
            Bounds cellBounds = cellConvex.bounds;
            Vector3[] hull = cellConvex.vertices;
            int[] hullTriangles = cellConvex.triangles;
            Vector3 center = Vector3.zero;
            foreach (Vector3 point in hull) center += point;
            center /= Mathf.Max(1, hull.Length);
            // Use the same broad chamfer language as loose stones. This is cold preparation.
            Mesh rounded = EarthFractureBevelMeshBuilder.Create(stone, .12f, .22f);
            Mesh output = Object.Instantiate(rounded);
            output.name = stone.name + " Natural Fracture Stone";
            Bounds source = rounded.bounds;
            Vector3 scale = new Vector3(cellBounds.size.x / Mathf.Max(.001f, source.size.x),
                cellBounds.size.y / Mathf.Max(.001f, source.size.y),
                cellBounds.size.z / Mathf.Max(.001f, source.size.z));
            var vertices = output.vertices;
            var normals = output.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = center + Vector3.Scale(vertices[i] - source.center, scale);
                if (normals.Length == vertices.Length)
                    normals[i] = new Vector3(normals[i].x / Mathf.Max(.001f, scale.x),
                        normals[i].y / Mathf.Max(.001f, scale.y), normals[i].z / Mathf.Max(.001f, scale.z)).normalized;
            }
            float containedScale = 1f;
            for (int face = 0; face + 2 < hullTriangles.Length; face += 3)
            {
                Vector3 a = hull[hullTriangles[face]], b = hull[hullTriangles[face + 1]], c = hull[hullTriangles[face + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(normal, center - a) > 0f) normal = -normal;
                float clearance = Mathf.Max(0f, -Vector3.Dot(normal, center - a));
                foreach (Vector3 point in vertices)
                {
                    float projection = Vector3.Dot(normal, point - center);
                    if (projection > .000001f) containedScale = Mathf.Min(containedScale, clearance / projection);
                }
            }
            if (containedScale < .0001f)
                throw new System.InvalidOperationException($"Convex fracture cell '{cellConvex.name}' has no usable interior for its stone visual.");
            for (int i = 0; i < vertices.Length; i++) vertices[i] = center + (vertices[i] - center) * (containedScale * .98f);
            output.vertices = vertices;
            output.normals = normals;
            int[] triangles = output.triangles;
            output.subMeshCount = 1;
            output.SetTriangles(triangles, 0);
            output.RecalculateBounds();
            if (rounded != stone)
            {
                if (Application.isPlaying) Object.Destroy(rounded);
                else Object.DestroyImmediate(rounded);
            }
            return output;
        }
    }
}
