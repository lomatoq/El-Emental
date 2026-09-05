using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public readonly struct EarthConvexPartitionCell
    {
        public readonly float3[] Vertices;
        public readonly int[] Triangles;
        public readonly float3 Center;
        public readonly float Volume;
        public EarthConvexPartitionCell(float3[] vertices, int[] triangles, float3 center, float volume)
        { Vertices = vertices; Triangles = triangles; Center = center; Volume = volume; }
    }

    /// <summary>Cold, deterministic convex-hull partition. Cuts retain the parent's actual volume.</summary>
    public static class EarthConvexPartitionSolver
    {
        private const float Epsilon = 0.000001f;
        public static EarthConvexPartitionCell BuildHull(float3[] input) => Build(input, 1, 1f)[0];
        private readonly struct Face
        {
            public readonly int A, B, C;
            public Face(int a, int b, int c) { A = a; B = b; C = c; }
        }
        private readonly struct Edge
        {
            public readonly int A, B;
            public Edge(int a, int b) { A = a; B = b; }
        }

        public static EarthConvexPartitionCell[] Build(float3[] input, int count, float seamScale = .992f)
        {
            if (input == null || input.Length < 4 || count < 1 || count > 4)
                throw new ArgumentException("Convex partition needs at least four vertices and one to four cells.");
            float3 minimum = input[0], maximum = input[0];
            foreach (float3 v in input) { minimum = math.min(minimum, v); maximum = math.max(maximum, v); }
            float unit = math.cmax(maximum - minimum);
            if (!math.isfinite(unit) || unit < 1e-7f) throw new ArgumentException("Degenerate convex source.");
            float3 origin = (minimum + maximum) * .5f;
            var points = new List<float3>();
            foreach (float3 v in input) AddUnique(points, (v - origin) / unit);
            if (points.Count < 4) throw new ArgumentException("Convex source needs four distinct vertices.");
            var faces = Hull(points);
            var leaves = new List<List<List<float3>>>();
            Partition(faces, count, leaves, 1u);
            var output = new EarthConvexPartitionCell[count];
            for (int i = 0; i < count; i++)
            {
                var vertices = new List<float3>();
                var triangles = new List<int>();
                foreach (var polygon in leaves[i])
                {
                    int start = vertices.Count;
                    vertices.AddRange(polygon);
                    for (int j = 1; j + 1 < polygon.Count; j++)
                    { triangles.Add(start); triangles.Add(start + j); triangles.Add(start + j + 1); }
                }
                float3 center = float3.zero;
                foreach (var v in vertices) center += v;
                center /= vertices.Count;
                float shrink = math.clamp(seamScale, .97f, 1f);
                for (int v = 0; v < vertices.Count; v++) vertices[v] = (vertices[v] - center) * unit * shrink;
                var array = vertices.ToArray();
                var indices = triangles.ToArray();
                float volume = MeshVolume(array, indices);
                if (volume <= 0f) throw new InvalidOperationException("Convex clipping produced an empty child.");
                output[i] = new EarthConvexPartitionCell(array, indices, origin + center * unit, volume);
            }
            return output;
        }

        public static float MeshVolume(float3[] vertices, int[] triangles)
        {
            double volume = 0;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
                volume += math.dot(vertices[triangles[i]], math.cross(vertices[triangles[i+1]], vertices[triangles[i+2]])) / 6.0;
            return (float)Math.Abs(volume);
        }

        private static void Partition(List<List<float3>> source, int count, List<List<List<float3>>> leaves, uint branch)
        {
            if (count == 1) { leaves.Add(source); return; }
            float3 min = source[0][0], max = min;
            foreach (var face in source) foreach (var v in face) { min = math.min(min, v); max = math.max(max, v); }
            float3 size = max - min;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            // Oblique, branch-dependent fault planes avoid turning rectangular
            // arena slabs into smaller rectangular bricks. Cuts still partition
            // the exact convex volume; no replacement templates or oversize rocks.
            float3 normal = float3.zero; normal[axis] = 1f;
            normal[(axis+1)%3] = (branch%2==0 ? -1f : 1f) * (.27f + (branch%3)*.09f);
            normal[(axis+2)%3] = (branch%3==0 ? -1f : 1f) * (.23f + (branch%4)*.07f);
            normal = math.normalize(normal);
            int leftCount = count / 2;
            float target = Volume(source) * leftCount / count;
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            foreach (var face in source) foreach (var v in face)
            { float d=math.dot(normal,v); low=math.min(low,d); high=math.max(high,d); }
            for (int step = 0; step < 18; step++)
            {
                float mid = (low + high) * .5f;
                if (Volume(Clip(source, normal, mid)) < target) low = mid; else high = mid;
            }
            float split = (low + high) * .5f;
            Partition(Clip(source, normal, split), leftCount, leaves, branch*2u);
            Partition(Clip(source, -normal, -split), count - leftCount, leaves, branch*2u+1u);
        }

        private static float Volume(List<List<float3>> faces)
        {
            double volume = 0;
            foreach (var f in faces)
                for (int i = 1; i + 1 < f.Count; i++) volume += math.dot(f[0], math.cross(f[i], f[i+1])) / 6.0;
            return (float)Math.Abs(volume);
        }

        private static List<List<float3>> Clip(List<List<float3>> faces, float3 normal, float distance)
        {
            var result = new List<List<float3>>();
            var cap = new List<float3>();
            foreach (var face in faces)
            {
                var clipped = new List<float3>();
                float3 previous = face[face.Count - 1];
                float previousDistance = math.dot(normal, previous) - distance;
                foreach (float3 current in face)
                {
                    float currentDistance = math.dot(normal, current) - distance;
                    bool previousInside = previousDistance <= 0f, currentInside = currentDistance <= 0f;
                    if (previousInside != currentInside)
                    {
                        float3 point = math.lerp(previous, current, previousDistance / (previousDistance - currentDistance));
                        AddUnique(clipped, point); AddUnique(cap, point);
                    }
                    if (currentInside) AddUnique(clipped, current);
                    previous = current; previousDistance = currentDistance;
                }
                if (clipped.Count >= 3) result.Add(clipped);
            }
            if (cap.Count >= 3) result.Add(OrderedFace(cap, normal));
            return result;
        }

        private static List<List<float3>> Hull(List<float3> points)
        {
            int a = 0, b = 1, c = -1, d = -1;
            for (int i = 1; i < points.Count; i++) if (math.distancesq(points[a], points[i]) > math.distancesq(points[a], points[b])) b = i;
            float best = 0;
            for (int i = 0; i < points.Count; i++)
            {
                float distance = math.lengthsq(math.cross(points[b] - points[a], points[i] - points[a]));
                if (distance > best) { best = distance; c = i; }
            }
            if (c < 0 || best < 1e-12f) throw new ArgumentException("Collinear convex source.");
            float3 n = math.normalizesafe(math.cross(points[b] - points[a], points[c] - points[a])); best = 0;
            for (int i = 0; i < points.Count; i++)
            {
                float distance = math.abs(math.dot(n, points[i] - points[a]));
                if (distance > best) { best = distance; d = i; }
            }
            if (d < 0 || best < Epsilon) throw new ArgumentException("Flat convex source.");
            float3 interior = (points[a] + points[b] + points[c] + points[d]) * .25f;
            var hull = new List<Face>();
            AddFace(hull, points, a,b,c,interior); AddFace(hull, points, a,d,b,interior);
            AddFace(hull, points, a,c,d,interior); AddFace(hull, points, b,d,c,interior);
            for (int i = 0; i < points.Count; i++)
            {
                if (i == a || i == b || i == c || i == d) continue;
                var horizon = new List<Edge>();
                for (int f = hull.Count - 1; f >= 0; f--)
                {
                    Face face = hull[f];
                    float3 normal = math.normalizesafe(math.cross(points[face.B]-points[face.A], points[face.C]-points[face.A]));
                    if (math.dot(normal, points[i]-points[face.A]) <= Epsilon) continue;
                    AddEdge(horizon,face.A,face.B); AddEdge(horizon,face.B,face.C); AddEdge(horizon,face.C,face.A);
                    hull.RemoveAt(f);
                }
                foreach (var edge in horizon) AddFace(hull,points,edge.A,edge.B,i,interior);
            }
            // Merge coplanar triangles into face polygons before clipping. This keeps caps
            // and adjacent faces on the same edges rather than creating diagonal T-junctions.
            var normals = new List<float3>(); var distances = new List<float>(); var polygons = new List<List<float3>>();
            foreach (var face in hull)
            {
                float3 normal = math.normalizesafe(math.cross(points[face.B]-points[face.A], points[face.C]-points[face.A]));
                float distance = math.dot(normal, points[face.A]);
                int group = -1;
                for (int g = 0; g < normals.Count; g++)
                    if (math.dot(normal,normals[g]) > .999999f && math.abs(distance-distances[g]) < Epsilon * 2f) { group = g; break; }
                if (group < 0) { group=polygons.Count; normals.Add(normal); distances.Add(distance); polygons.Add(new List<float3>()); }
                AddUnique(polygons[group],points[face.A]); AddUnique(polygons[group],points[face.B]); AddUnique(polygons[group],points[face.C]);
            }
            for (int i = 0; i < polygons.Count; i++) polygons[i] = OrderedFace(polygons[i],normals[i]);
            return polygons;
        }

        private static void AddFace(List<Face> faces, List<float3> points, int a, int b, int c, float3 inside)
        {
            if (math.dot(math.cross(points[b]-points[a],points[c]-points[a]),inside-points[a]) > 0f) faces.Add(new Face(a,c,b));
            else faces.Add(new Face(a,b,c));
        }
        private static void AddEdge(List<Edge> edges, int a, int b)
        {
            for (int i=0;i<edges.Count;i++) if (edges[i].A == b && edges[i].B == a) { edges.RemoveAt(i); return; }
            edges.Add(new Edge(a,b));
        }
        private static void AddUnique(List<float3> points, float3 point)
        {
            foreach (var existing in points) if (math.distancesq(existing,point) < 1e-12f) return;
            points.Add(point);
        }
        private static List<float3> OrderedFace(List<float3> points, float3 normal)
        {
            float3 tangent=math.normalizesafe(math.cross(normal, math.abs(normal.y)<.9f ? new float3(0,1,0) : new float3(1,0,0)));
            float3 bitangent=math.cross(normal,tangent);
            points.Sort((a,b)=> { int x=math.dot(a,tangent).CompareTo(math.dot(b,tangent)); return x!=0 ? x : math.dot(a,bitangent).CompareTo(math.dot(b,bitangent)); });
            var outline=new List<float3>();
            foreach(var p in points)
            {
                while(outline.Count>=2 && math.dot(math.cross(outline[outline.Count-1]-outline[outline.Count-2],p-outline[outline.Count-1]),normal)<=1e-9f) outline.RemoveAt(outline.Count-1);
                outline.Add(p);
            }
            int lower=outline.Count;
            for(int i=points.Count-2;i>=0;i--)
            {
                float3 p=points[i];
                while(outline.Count>lower && math.dot(math.cross(outline[outline.Count-1]-outline[outline.Count-2],p-outline[outline.Count-1]),normal)<=1e-9f) outline.RemoveAt(outline.Count-1);
                outline.Add(p);
            }
            if(outline.Count>1) outline.RemoveAt(outline.Count-1);
            return outline;
        }
    }
}
