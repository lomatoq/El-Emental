using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.Rendering
{
    public enum RumbleRockFamily : byte
    {
        Boulder = 0,
        Slab = 1,
        Wedge = 2,
        Pebble = 3,
        Pillar = 4
    }

    [Serializable]
    public readonly struct RumbleRockRecipe
    {
        public RumbleRockRecipe(
            int seed,
            RumbleRockFamily family,
            Vector3 size,
            int cutCount,
            float bevelWidth,
            float silhouetteBias = 0.5f)
        {
            Seed = seed;
            Family = family;
            Size = new Vector3(
                Mathf.Max(0.2f, size.x),
                Mathf.Max(0.2f, size.y),
                Mathf.Max(0.2f, size.z));
            CutCount = Mathf.Clamp(cutCount, 4, 18);
            BevelWidth = Mathf.Clamp(bevelWidth, 0.008f, Mathf.Min(Size.x, Size.y, Size.z) * 0.22f);
            SilhouetteBias = Mathf.Clamp01(silhouetteBias);
        }

        public int Seed { get; }
        public RumbleRockFamily Family { get; }
        public Vector3 Size { get; }
        public int CutCount { get; }
        public float BevelWidth { get; }
        public float SilhouetteBias { get; }
    }

    /// <summary>
    /// Deterministic, editor-bake-friendly stylized rock generator. It deliberately
    /// builds a convex form from large clipping planes, then creates real inset face,
    /// edge and vertex bevel polygons. Runtime gameplay consumes ordinary Mesh assets;
    /// it does not generate hero art every time a stone is spawned.
    /// </summary>
    public static class RumbleRockMeshFactory
    {
        private const float GeometryEpsilon = 0.00008f;
        private const float MinimumTriangleAreaSq = 0.00000001f;
        private const float QuantizeScale = 10000f;

        private sealed class PolyFace
        {
            public readonly List<Vector3> Vertices;
            public Vector3 Normal;

            public PolyFace(IEnumerable<Vector3> vertices, Vector3 normal)
            {
                Vertices = new List<Vector3>(vertices);
                Normal = normal.normalized;
                EnsureWinding(Vertices, Normal);
            }
        }

        private readonly struct CutPlane
        {
            public CutPlane(Vector3 normal, float distance)
            {
                Normal = normal.normalized;
                Distance = distance;
            }

            public Vector3 Normal { get; }
            public float Distance { get; }
            public float SignedDistance(Vector3 point) => Vector3.Dot(Normal, point) - Distance;
        }

        private readonly struct VertexKey : IEquatable<VertexKey>
        {
            public VertexKey(Vector3 value)
            {
                X = Mathf.RoundToInt(value.x * QuantizeScale);
                Y = Mathf.RoundToInt(value.y * QuantizeScale);
                Z = Mathf.RoundToInt(value.z * QuantizeScale);
            }

            public readonly int X;
            public readonly int Y;
            public readonly int Z;

            public bool Equals(VertexKey other) => X == other.X && Y == other.Y && Z == other.Z;
            public override bool Equals(object obj) => obj is VertexKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = X;
                    hash = (hash * 397) ^ Y;
                    hash = (hash * 397) ^ Z;
                    return hash;
                }
            }
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            public EdgeKey(int a, int b)
            {
                if (a <= b)
                {
                    A = a;
                    B = b;
                }
                else
                {
                    A = b;
                    B = a;
                }
            }

            public readonly int A;
            public readonly int B;
            public bool Equals(EdgeKey other) => A == other.A && B == other.B;
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode() => (A * 397) ^ B;
        }

        private sealed class EdgeInset
        {
            public int OriginalA;
            public int OriginalB;
            public Vector3 InsetA;
            public Vector3 InsetB;
            public Vector3 FaceNormal;
        }

        private sealed class VertexCap
        {
            public readonly List<Vector3> Points = new List<Vector3>(8);
            public Vector3 NormalSum;
        }

        public static RumbleRockRecipe CreateDefaultRecipe(int seed, RumbleRockFamily family, float scale = 1f)
        {
            scale = Mathf.Max(0.2f, scale);
            var random = new System.Random(seed ^ ((int)family * 73856093));
            Vector3 size;
            int cuts;
            float bias;
            switch (family)
            {
                case RumbleRockFamily.Slab:
                    size = new Vector3(
                        Next(random, 1.25f, 2.15f),
                        Next(random, 0.38f, 0.72f),
                        Next(random, 0.95f, 1.75f));
                    cuts = random.Next(7, 11);
                    bias = 0.68f;
                    break;
                case RumbleRockFamily.Wedge:
                    size = new Vector3(
                        Next(random, 0.9f, 1.65f),
                        Next(random, 0.75f, 1.35f),
                        Next(random, 1.15f, 2.0f));
                    cuts = random.Next(7, 12);
                    bias = 0.78f;
                    break;
                case RumbleRockFamily.Pebble:
                    size = new Vector3(
                        Next(random, 0.42f, 0.82f),
                        Next(random, 0.30f, 0.62f),
                        Next(random, 0.42f, 0.88f));
                    cuts = random.Next(8, 13);
                    bias = 0.36f;
                    break;
                case RumbleRockFamily.Pillar:
                    size = new Vector3(
                        Next(random, 0.68f, 1.08f),
                        Next(random, 1.65f, 2.65f),
                        Next(random, 0.68f, 1.12f));
                    cuts = random.Next(7, 11);
                    bias = 0.74f;
                    break;
                default:
                    size = new Vector3(
                        Next(random, 0.95f, 1.65f),
                        Next(random, 0.82f, 1.48f),
                        Next(random, 0.95f, 1.75f));
                    cuts = random.Next(8, 13);
                    bias = 0.52f;
                    break;
            }

            size *= scale;
            float bevel = Mathf.Min(size.x, size.y, size.z) * Next(random, 0.055f, 0.105f);
            return new RumbleRockRecipe(seed, family, size, cuts, bevel, bias);
        }

        public static Mesh Build(in RumbleRockRecipe recipe, string meshName = null)
        {
            var random = new System.Random(recipe.Seed);
            Vector3 halfExtents = recipe.Size * 0.5f;
            List<PolyFace> faces = CreateBox(halfExtents);

            if (recipe.Family == RumbleRockFamily.Wedge)
            {
                Vector3 side = random.NextDouble() > 0.5 ? Vector3.right : Vector3.left;
                Vector3 wedgeNormal = (side * Next(random, 0.35f, 0.58f) +
                                       Vector3.up * Next(random, 0.58f, 0.82f) +
                                       Vector3.forward * Next(random, -0.18f, 0.18f)).normalized;
                float support = SupportDistance(halfExtents, wedgeNormal);
                Clip(faces, new CutPlane(wedgeNormal, support * Next(random, 0.50f, 0.67f)));
            }

            for (int index = 0; index < recipe.CutCount; index++)
            {
                Vector3 normal = RandomCutNormal(random, recipe.Family, recipe.SilhouetteBias, index);
                float support = SupportDistance(halfExtents, normal);
                float minimum = recipe.Family == RumbleRockFamily.Pebble ? 0.52f : 0.57f;
                float maximum = recipe.Family == RumbleRockFamily.Slab ? 0.88f : 0.84f;
                float threshold = support * Next(random, minimum, maximum);
                Clip(faces, new CutPlane(normal, threshold));
                if (faces.Count < 5) break;
            }

            // Preserve a useful, stable base for environment dressing and physics.
            float lowest = float.PositiveInfinity;
            for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
            for (int vertexIndex = 0; vertexIndex < faces[faceIndex].Vertices.Count; vertexIndex++)
                lowest = Mathf.Min(lowest, faces[faceIndex].Vertices[vertexIndex].y);
            Vector3 lift = Vector3.up * -lowest;
            for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
            for (int vertexIndex = 0; vertexIndex < faces[faceIndex].Vertices.Count; vertexIndex++)
                faces[faceIndex].Vertices[vertexIndex] += lift;

            Mesh mesh = BuildBeveledMesh(faces, recipe.BevelWidth, recipe.Seed);
            GroundAtZero(mesh);
            mesh.name = string.IsNullOrWhiteSpace(meshName)
                ? $"RumbleRock_{recipe.Family}_{recipe.Seed}"
                : meshName;
            return mesh;
        }

        private static void GroundAtZero(Mesh mesh)
        {
            mesh.RecalculateBounds();
            float minimumY = mesh.bounds.min.y;
            if (!float.IsFinite(minimumY) || Mathf.Abs(minimumY) <= 0.000001f) return;
            Vector3[] vertices = mesh.vertices;
            for (int index = 0; index < vertices.Length; index++)
                vertices[index].y -= minimumY;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        public static bool Validate(Mesh mesh, out string reason)
        {
            if (mesh == null)
            {
                reason = "Mesh is null.";
                return false;
            }
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            if (vertices.Length < 12 || triangles.Length < 24)
            {
                reason = "Mesh does not contain enough geometry.";
                return false;
            }
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 value = vertices[index];
                if (!float.IsFinite(value.x) || !float.IsFinite(value.y) || !float.IsFinite(value.z))
                {
                    reason = $"Vertex {index} is non-finite.";
                    return false;
                }
            }

            double signedVolume = 0.0;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                int ia = triangles[index];
                int ib = triangles[index + 1];
                int ic = triangles[index + 2];
                if ((uint)ia >= vertices.Length || (uint)ib >= vertices.Length || (uint)ic >= vertices.Length)
                {
                    reason = "Triangle references an invalid vertex.";
                    return false;
                }
                Vector3 a = vertices[ia];
                Vector3 b = vertices[ib];
                Vector3 c = vertices[ic];
                float areaSq = Vector3.Cross(b - a, c - a).sqrMagnitude;
                if (areaSq <= MinimumTriangleAreaSq)
                {
                    reason = $"Triangle {index / 3} is degenerate.";
                    return false;
                }
                signedVolume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
            }
            if (Math.Abs(signedVolume) <= 0.00001)
            {
                reason = "Mesh volume is effectively zero.";
                return false;
            }
            if (mesh.bounds.size.x <= 0.05f || mesh.bounds.size.y <= 0.05f || mesh.bounds.size.z <= 0.05f)
            {
                reason = "Mesh bounds are collapsed.";
                return false;
            }
            reason = null;
            return true;
        }

        private static List<PolyFace> CreateBox(Vector3 e)
        {
            Vector3 p000 = new Vector3(-e.x, -e.y, -e.z);
            Vector3 p001 = new Vector3(-e.x, -e.y, e.z);
            Vector3 p010 = new Vector3(-e.x, e.y, -e.z);
            Vector3 p011 = new Vector3(-e.x, e.y, e.z);
            Vector3 p100 = new Vector3(e.x, -e.y, -e.z);
            Vector3 p101 = new Vector3(e.x, -e.y, e.z);
            Vector3 p110 = new Vector3(e.x, e.y, -e.z);
            Vector3 p111 = new Vector3(e.x, e.y, e.z);
            return new List<PolyFace>
            {
                new PolyFace(new[] { p100, p101, p111, p110 }, Vector3.right),
                new PolyFace(new[] { p001, p000, p010, p011 }, Vector3.left),
                new PolyFace(new[] { p010, p110, p111, p011 }, Vector3.up),
                new PolyFace(new[] { p000, p001, p101, p100 }, Vector3.down),
                new PolyFace(new[] { p001, p011, p111, p101 }, Vector3.forward),
                new PolyFace(new[] { p000, p100, p110, p010 }, Vector3.back)
            };
        }

        private static void Clip(List<PolyFace> faces, in CutPlane plane)
        {
            var clippedFaces = new List<PolyFace>(faces.Count + 1);
            var capPoints = new List<Vector3>(32);
            for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
            {
                PolyFace face = faces[faceIndex];
                List<Vector3> clipped = ClipPolygon(face.Vertices, plane, capPoints);
                CleanPolygon(clipped);
                if (clipped.Count >= 3)
                    clippedFaces.Add(new PolyFace(clipped, face.Normal));
            }

            Unique(capPoints);
            if (capPoints.Count >= 3)
            {
                Vector3 center = Average(capPoints);
                BuildPlaneBasis(plane.Normal, out Vector3 axisX, out Vector3 axisY);
                capPoints.Sort((a, b) =>
                {
                    Vector3 da = a - center;
                    Vector3 db = b - center;
                    float aa = Mathf.Atan2(Vector3.Dot(da, axisY), Vector3.Dot(da, axisX));
                    float ab = Mathf.Atan2(Vector3.Dot(db, axisY), Vector3.Dot(db, axisX));
                    return aa.CompareTo(ab);
                });
                clippedFaces.Add(new PolyFace(capPoints, plane.Normal));
            }

            if (clippedFaces.Count >= 4)
            {
                faces.Clear();
                faces.AddRange(clippedFaces);
            }
        }

        private static List<Vector3> ClipPolygon(
            List<Vector3> input,
            in CutPlane plane,
            List<Vector3> capPoints)
        {
            var output = new List<Vector3>(input.Count + 2);
            if (input.Count == 0) return output;
            Vector3 previous = input[input.Count - 1];
            float previousDistance = plane.SignedDistance(previous);
            bool previousInside = previousDistance <= GeometryEpsilon;
            for (int index = 0; index < input.Count; index++)
            {
                Vector3 current = input[index];
                float currentDistance = plane.SignedDistance(current);
                bool currentInside = currentDistance <= GeometryEpsilon;
                if (previousInside && currentInside)
                {
                    output.Add(current);
                }
                else if (previousInside != currentInside)
                {
                    float denominator = previousDistance - currentDistance;
                    float amount = Mathf.Abs(denominator) > GeometryEpsilon
                        ? Mathf.Clamp01(previousDistance / denominator)
                        : 0.5f;
                    Vector3 intersection = Vector3.LerpUnclamped(previous, current, amount);
                    output.Add(intersection);
                    capPoints.Add(intersection);
                    if (currentInside) output.Add(current);
                }
                previous = current;
                previousDistance = currentDistance;
                previousInside = currentInside;
            }
            return output;
        }

        private static Mesh BuildBeveledMesh(List<PolyFace> faces, float bevelWidth, int seed)
        {
            var originalVertexIds = new Dictionary<VertexKey, int>(128);
            var originalPositions = new List<Vector3>(128);
            int GetOriginalId(Vector3 point)
            {
                var key = new VertexKey(point);
                if (originalVertexIds.TryGetValue(key, out int id)) return id;
                id = originalPositions.Count;
                originalVertexIds.Add(key, id);
                originalPositions.Add(point);
                return id;
            }

            var faceInsets = new List<Vector3[]>(faces.Count);
            var faceOriginalIds = new List<int[]>(faces.Count);
            for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
            {
                PolyFace face = faces[faceIndex];
                Vector3 centroid = Average(face.Vertices);
                var inset = new Vector3[face.Vertices.Count];
                var ids = new int[face.Vertices.Count];
                for (int vertexIndex = 0; vertexIndex < face.Vertices.Count; vertexIndex++)
                {
                    Vector3 vertex = face.Vertices[vertexIndex];
                    Vector3 toCenter = centroid - vertex;
                    float distance = toCenter.magnitude;
                    float insetDistance = Mathf.Min(bevelWidth, distance * 0.34f);
                    inset[vertexIndex] = distance > GeometryEpsilon
                        ? vertex + toCenter / distance * insetDistance
                        : vertex;
                    ids[vertexIndex] = GetOriginalId(vertex);
                }
                faceInsets.Add(inset);
                faceOriginalIds.Add(ids);
            }

            var vertices = new List<Vector3>(512);
            var normals = new List<Vector3>(512);
            var colors = new List<Color>(512);
            var triangles = new List<int>(1024);
            var edgeMap = new Dictionary<EdgeKey, EdgeInset>(256);
            var vertexCaps = new Dictionary<int, VertexCap>(128);
            Vector3 meshCenter = Average(originalPositions);

            for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
            {
                PolyFace face = faces[faceIndex];
                Vector3[] inset = faceInsets[faceIndex];
                int[] ids = faceOriginalIds[faceIndex];
                Color faceColor = FaceColor(seed, faceIndex, false);
                AppendPolygon(vertices, normals, colors, triangles, inset, face.Normal, faceColor, meshCenter);

                for (int index = 0; index < inset.Length; index++)
                {
                    int next = (index + 1) % inset.Length;
                    int originalA = ids[index];
                    int originalB = ids[next];
                    var key = new EdgeKey(originalA, originalB);
                    if (!edgeMap.TryGetValue(key, out EdgeInset first))
                    {
                        edgeMap.Add(key, new EdgeInset
                        {
                            OriginalA = originalA,
                            OriginalB = originalB,
                            InsetA = inset[index],
                            InsetB = inset[next],
                            FaceNormal = face.Normal
                        });
                    }
                    else
                    {
                        Vector3 secondA = originalA == first.OriginalA ? inset[index] : inset[next];
                        Vector3 secondB = originalB == first.OriginalB ? inset[next] : inset[index];
                        Vector3 bevelNormal = (first.FaceNormal + face.Normal).normalized;
                        Vector3[] quad =
                        {
                            first.InsetA,
                            first.InsetB,
                            secondB,
                            secondA
                        };
                        AppendPolygon(
                            vertices,
                            normals,
                            colors,
                            triangles,
                            quad,
                            bevelNormal,
                            FaceColor(seed, faceIndex + 101, true),
                            meshCenter);
                    }

                    if (!vertexCaps.TryGetValue(ids[index], out VertexCap cap))
                    {
                        cap = new VertexCap();
                        vertexCaps.Add(ids[index], cap);
                    }
                    cap.Points.Add(inset[index]);
                    cap.NormalSum += face.Normal;
                }
            }

            foreach (KeyValuePair<int, VertexCap> pair in vertexCaps)
            {
                VertexCap cap = pair.Value;
                Unique(cap.Points);
                if (cap.Points.Count < 3) continue;
                Vector3 normal = cap.NormalSum.sqrMagnitude > GeometryEpsilon
                    ? cap.NormalSum.normalized
                    : (originalPositions[pair.Key] - meshCenter).normalized;
                Vector3 center = Average(cap.Points);
                BuildPlaneBasis(normal, out Vector3 axisX, out Vector3 axisY);
                cap.Points.Sort((a, b) =>
                {
                    Vector3 da = a - center;
                    Vector3 db = b - center;
                    return Mathf.Atan2(Vector3.Dot(da, axisY), Vector3.Dot(da, axisX))
                        .CompareTo(Mathf.Atan2(Vector3.Dot(db, axisY), Vector3.Dot(db, axisX)));
                });
                AppendPolygon(
                    vertices,
                    normals,
                    colors,
                    triangles,
                    cap.Points,
                    normal,
                    FaceColor(seed, pair.Key + 211, true),
                    meshCenter);
            }

            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendPolygon(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            IReadOnlyList<Vector3> points,
            Vector3 desiredNormal,
            Color color,
            Vector3 meshCenter)
        {
            if (points == null || points.Count < 3) return;
            var ordered = new List<Vector3>(points.Count);
            for (int index = 0; index < points.Count; index++) ordered.Add(points[index]);
            CleanPolygon(ordered);
            if (ordered.Count < 3) return;
            Vector3 geometricNormal = PolygonNormal(ordered);
            Vector3 centroid = Average(ordered);
            Vector3 outward = desiredNormal.sqrMagnitude > GeometryEpsilon
                ? desiredNormal.normalized
                : (centroid - meshCenter).normalized;
            if (Vector3.Dot(geometricNormal, outward) < 0f) ordered.Reverse();

            int start = vertices.Count;
            for (int index = 0; index < ordered.Count; index++)
            {
                vertices.Add(ordered[index]);
                normals.Add(outward);
                colors.Add(color);
            }
            for (int index = 1; index < ordered.Count - 1; index++)
            {
                Vector3 a = ordered[0];
                Vector3 b = ordered[index];
                Vector3 c = ordered[index + 1];
                if (Vector3.Cross(b - a, c - a).sqrMagnitude <= MinimumTriangleAreaSq)
                    continue;
                triangles.Add(start);
                triangles.Add(start + index);
                triangles.Add(start + index + 1);
            }
        }

        private static Vector3 RandomCutNormal(
            System.Random random,
            RumbleRockFamily family,
            float silhouetteBias,
            int index)
        {
            Vector3 direction;
            do
            {
                direction = new Vector3(
                    Next(random, -1f, 1f),
                    Next(random, -1f, 1f),
                    Next(random, -1f, 1f));
            } while (direction.sqrMagnitude < 0.08f);
            direction.Normalize();

            switch (family)
            {
                case RumbleRockFamily.Slab:
                    direction.y *= Mathf.Lerp(0.22f, 0.55f, silhouetteBias);
                    break;
                case RumbleRockFamily.Pillar:
                    direction.y *= Mathf.Lerp(0.18f, 0.48f, silhouetteBias);
                    break;
                case RumbleRockFamily.Wedge:
                    if ((index & 1) == 0) direction.y = Mathf.Abs(direction.y) * 1.4f;
                    break;
                case RumbleRockFamily.Pebble:
                    direction.y *= 0.85f;
                    break;
            }
            if (direction.sqrMagnitude < GeometryEpsilon) direction = Vector3.right;
            return direction.normalized;
        }

        private static float SupportDistance(Vector3 extents, Vector3 normal) =>
            Mathf.Abs(normal.x) * extents.x +
            Mathf.Abs(normal.y) * extents.y +
            Mathf.Abs(normal.z) * extents.z;

        private static Color FaceColor(int seed, int faceIndex, bool bevel)
        {
            uint value = unchecked((uint)(seed * 747796405 + faceIndex * 2891336453));
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            float variation = (value & 0xFFFFu) / 65535f;
            float tone = Mathf.Lerp(0.88f, 1.08f, variation);
            if (bevel) tone *= 1.045f;
            return new Color(tone, tone, tone, bevel ? 0.72f : 0.38f);
        }

        private static void CleanPolygon(List<Vector3> points)
        {
            if (points.Count < 2) return;
            for (int index = points.Count - 1; index >= 0; index--)
            {
                int previous = (index - 1 + points.Count) % points.Count;
                if ((points[index] - points[previous]).sqrMagnitude <= GeometryEpsilon * GeometryEpsilon)
                    points.RemoveAt(index);
            }
            if (points.Count < 3) return;
            bool removed;
            do
            {
                removed = false;
                for (int index = 0; index < points.Count && points.Count >= 3; index++)
                {
                    Vector3 a = points[(index - 1 + points.Count) % points.Count];
                    Vector3 b = points[index];
                    Vector3 c = points[(index + 1) % points.Count];
                    if (Vector3.Cross(b - a, c - b).sqrMagnitude <= MinimumTriangleAreaSq)
                    {
                        points.RemoveAt(index);
                        removed = true;
                        break;
                    }
                }
            } while (removed);
        }

        private static void Unique(List<Vector3> points)
        {
            var keys = new HashSet<VertexKey>();
            for (int index = points.Count - 1; index >= 0; index--)
            {
                if (!keys.Add(new VertexKey(points[index]))) points.RemoveAt(index);
            }
        }

        private static void EnsureWinding(List<Vector3> points, Vector3 desiredNormal)
        {
            if (points.Count < 3) return;
            if (Vector3.Dot(PolygonNormal(points), desiredNormal) < 0f) points.Reverse();
        }

        private static Vector3 PolygonNormal(IReadOnlyList<Vector3> points)
        {
            Vector3 normal = Vector3.zero;
            for (int index = 0; index < points.Count; index++)
            {
                Vector3 current = points[index];
                Vector3 next = points[(index + 1) % points.Count];
                normal.x += (current.y - next.y) * (current.z + next.z);
                normal.y += (current.z - next.z) * (current.x + next.x);
                normal.z += (current.x - next.x) * (current.y + next.y);
            }
            return normal.sqrMagnitude > GeometryEpsilon ? normal.normalized : Vector3.up;
        }

        private static Vector3 Average(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            for (int index = 0; index < points.Count; index++) sum += points[index];
            return sum / points.Count;
        }

        private static void BuildPlaneBasis(Vector3 normal, out Vector3 axisX, out Vector3 axisY)
        {
            Vector3 reference = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.92f
                ? Vector3.up
                : Vector3.right;
            axisX = Vector3.Cross(reference, normal).normalized;
            if (axisX.sqrMagnitude < GeometryEpsilon) axisX = Vector3.right;
            axisY = Vector3.Cross(normal, axisX).normalized;
        }

        private static float Next(System.Random random, float minimum, float maximum) =>
            Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }
}
