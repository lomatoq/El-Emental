using System.Collections.Generic;
using Elemental.Simulation.Structures;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    public static class EarthStoneColliderMesh
    {
        // A convex hull of <=64 points has <=124 triangular faces. Keep PhysX
        // below its 255-face limit instead of allowing it to produce a partial hull.
        public static Mesh Create(Mesh visual)
        {
            Vector3[] vertices = visual.vertices;
            var selected = new List<float3>(64);
            Bounds bounds = visual.bounds;
            Vector3 size = bounds.size;
            for (int axis = 0; axis < 3; axis++) for (int sign = -1; sign <= 1; sign += 2)
            {
                int best = 0;
                for (int i = 1; i < vertices.Length; i++) if (vertices[i][axis] * sign > vertices[best][axis] * sign) best = i;
                float3 point = vertices[best]; if (!selected.Contains(point)) selected.Add(point);
            }
            var normalized = new float3[vertices.Length];
            var nearest = new float[vertices.Length];
            float3 safeSize = math.max((float3)size, new float3(.00001f));
            for (int i = 0; i < vertices.Length; i++)
            {
                normalized[i] = (float3)vertices[i] / safeSize;
                nearest[i] = float.PositiveInfinity;
                foreach (var point in selected) nearest[i] = math.min(nearest[i],math.distancesq(normalized[i],point / safeSize));
            }
            while (selected.Count < 64)
            {
                float farthest = 0f; int best = -1;
                for (int i = 0; i < nearest.Length; i++) if (nearest[i] > farthest) { farthest = nearest[i]; best = i; }
                if (best < 0 || farthest < 1e-8f) break;
                selected.Add(vertices[best]);
                // Only the newly selected point can lower cached nearest distances.
                for (int i = 0; i < nearest.Length; i++) nearest[i] = math.min(nearest[i],math.distancesq(normalized[i],normalized[best]));
            }
            var hull = EarthConvexPartitionSolver.BuildHull(selected.ToArray());
            var output = new Mesh { name = visual.name + " Matched Convex" };
            var cooked = new Vector3[hull.Vertices.Length];
            for (int i = 0; i < cooked.Length; i++) cooked[i] = hull.Vertices[i] + hull.Center;
            output.vertices = cooked; output.triangles = hull.Triangles; output.RecalculateBounds();
            return output;
        }
    }
}
