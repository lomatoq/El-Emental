using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Elemental.Presentation.DistantScenery;
using Elemental.Runtime.Geometry;
using Unity.Mathematics;

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

    /// <summary>Cold, deterministic convex authoring. Each bevel is a supporting-plane
    /// cut, so its cap is planar and the published solid remains closed.</summary>
    public static class RumbleRockMeshFactory
    {
        public readonly struct BuildDiagnostics
        {
            public BuildDiagnostics(float maximumCanonicalAdjustment, float minimumCross, float requiredCross,
                int rejectedShapeCuts, int reducedBevelCuts, int retainedEdges)
            {
                MaximumCanonicalAdjustment = maximumCanonicalAdjustment;
                MinimumCross = minimumCross;
                RequiredCross = requiredCross;
                RejectedShapeCuts = rejectedShapeCuts;
                ReducedBevelCuts = reducedBevelCuts;
                RetainedEdges = retainedEdges;
            }
            public float MaximumCanonicalAdjustment { get; }
            public float MinimumCross { get; }
            public float RequiredCross { get; }
            public int RejectedShapeCuts { get; }
            public int ReducedBevelCuts { get; }
            public int RetainedEdges { get; }
            public override string ToString() => $"canonicalDelta={MaximumCanonicalAdjustment:G9}, minCross={MinimumCross:G9}, " +
                $"requiredCross={RequiredCross:G9}, rejectedShapeCuts={RejectedShapeCuts}, reducedBevelCuts={ReducedBevelCuts}, retainedEdges={RetainedEdges}";
        }
        private const float GeometryEpsilon = 0.00008f;
        private static readonly Unity.Profiling.ProfilerMarker BuildMarker =
            new Unity.Profiling.ProfilerMarker("Elemental.Geometry.RumbleRock.Build");

        public static Mesh Build(in RumbleRockRecipe recipe, string meshName = null)
        {
            using (BuildMarker.Auto()) return BuildCore(recipe, meshName, out _);
        }

        public static Mesh Build(in RumbleRockRecipe recipe, out BuildDiagnostics diagnostics, string meshName = null)
        {
            using (BuildMarker.Auto()) return BuildCore(recipe, meshName, out diagnostics);
        }

        private static Mesh BuildCore(in RumbleRockRecipe recipe, string meshName, out BuildDiagnostics diagnostics)
        {
            diagnostics = default;
            int rejectedShapeCuts = 0, reducedBevelCuts = 0, retainedEdges = 0;
            var random = new System.Random(recipe.Seed);
            Vector3 halfExtents = recipe.Size * 0.5f;
            RockPolyhedron solid = RockPolyhedron.Box((float3)(-halfExtents), (float3)halfExtents);
            // Random cuts are proposals: Clip admits only complete valid convex solids.
            // Rejected/no-op proposals preserve the last validated shape, never a partial mesh.
            if (recipe.Family == RumbleRockFamily.Wedge)
            {
                Vector3 side = random.NextDouble() > 0.5 ? Vector3.right : Vector3.left;
                Vector3 normal = (side * Next(random, 0.35f, 0.58f) +
                    Vector3.up * Next(random, 0.58f, 0.82f) +
                    Vector3.forward * Next(random, -0.18f, 0.18f)).normalized;
                if (solid.Clip((float3)normal, SupportDistance(halfExtents, normal) *
                    Next(random, 0.50f, 0.67f), out RockPolyhedron wedge) &&
                    TryCanonicalFaces(wedge, out _, out _, out _, out _)) solid = wedge;
                else rejectedShapeCuts++;
            }
            for (int index = 0; index < recipe.CutCount; index++)
            {
                Vector3 normal = RandomCutNormal(random, recipe.Family, recipe.SilhouetteBias, index);
                float minimum = recipe.Family == RumbleRockFamily.Pebble ? 0.52f : 0.57f;
                float maximum = recipe.Family == RumbleRockFamily.Slab ? 0.88f : 0.84f;
                float threshold = SupportDistance(halfExtents, normal) * Next(random, minimum, maximum);
                if (solid.Clip((float3)normal, threshold, out RockPolyhedron cut) &&
                    TryCanonicalFaces(cut, out _, out _, out _, out _)) solid = cut;
                else rejectedShapeCuts++;
            }
            var baseFaces = new List<RockPolyhedron.Face>(solid.Faces);
            var edges = solid.Adjacencies();
            for (int edge = 0; edge < edges.Count; edge++)
            {
                var pair = edges[edge];
                float3 normal = math.normalize(pair.Item1.Normal + pair.Item2.Normal);
                float3 shared = default;
                bool found = false;
                foreach (float3 a in pair.Item1.Points)
                {
                    foreach (float3 b in pair.Item2.Points)
                        if (math.lengthsq(a - b) <= solid.Epsilon * solid.Epsilon * 9f)
                        { shared = a; found = true; break; }
                    if (found) break;
                }
                if (!found) throw new InvalidOperationException($"Rock {recipe.Seed}: adjacency has no shared edge.");
                float edgeDistance = math.dot(normal, shared);
                bool admitted = false;
                for (int attempt = 0; attempt < 9; attempt++)
                {
                    float distance = edgeDistance - recipe.BevelWidth / (1 << attempt);
                    float maximum = float.NegativeInfinity;
                    foreach (var face in solid.Faces) foreach (float3 point in face.Points)
                        maximum = math.max(maximum, math.dot(normal, point));
                    // Earlier bevels can already remove the entire original edge.
                    if (maximum <= distance) { admitted = true; break; }
                    if (!solid.Clip(normal, distance, out RockPolyhedron beveled) ||
                        !TryCanonicalFaces(beveled, out _, out _, out _, out _)) continue;
                    solid = beveled;
                    if (attempt > 0) reducedBevelCuts++;
                    admitted = true;
                    break;
                }
                // Retain this complete validated edge when every bounded bevel
                // proposal is too small to publish. Diagnostics expose the fallback.
                if (!admitted) retainedEdges++;
            }
            if (!solid.Validate(out _)) throw new InvalidOperationException($"Rock {recipe.Seed}: invalid final solid.");
            if (!TryCanonicalFaces(solid, out List<RockPolyhedron.Face> renderFaces,
                out float maximumAdjustment, out float minimumCross, out float requiredCross))
                throw new InvalidOperationException($"Rock {recipe.Seed}: final triangulation cannot be published; " +
                    $"canonicalDelta={maximumAdjustment:G9}, minCross={minimumCross:G9}, requiredCross={requiredCross:G9}.");
            diagnostics = new BuildDiagnostics(maximumAdjustment, minimumCross, requiredCross,
                rejectedShapeCuts, reducedBevelCuts, retainedEdges);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            float lowest = float.PositiveInfinity;
            foreach (var face in renderFaces) foreach (float3 point in face.Points) lowest = math.min(lowest, point.y);
            for (int f = 0; f < renderFaces.Count; f++)
            {
                var face = renderFaces[f];
                bool bevel = true;
                for (int original = 0; original < baseFaces.Count; original++)
                    if (math.dot(baseFaces[original].Normal, face.Normal) > 0.999999f &&
                        math.abs(math.dot(face.Normal, baseFaces[original].Points[0] - face.Points[0])) < solid.Epsilon * 2)
                    { bevel = false; break; }
                Color color = FaceColor(recipe.Seed, f, bevel);
                int start = vertices.Count;
                int fan = RockPolyhedron.FanStart(face);
                for (int i = 0; i < face.Points.Count; i++)
                {
                    float3 point = face.Points[(fan + i) % face.Points.Count];
                    vertices.Add(new Vector3(point.x, point.y - lowest, point.z));
                    normals.Add((Vector3)face.Normal);
                    colors.Add(color);
                }
                for (int i = 1; i < face.Points.Count - 1; i++)
                { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
            }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32,
                name = string.IsNullOrWhiteSpace(meshName) ? $"RumbleRock_{recipe.Family}_{recipe.Seed}" : meshName };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true); mesh.RecalculateBounds();
            if (Validate(mesh, out string reason)) return mesh;
            if (Application.isPlaying) UnityEngine.Object.Destroy(mesh); else UnityEngine.Object.DestroyImmediate(mesh);
            throw new InvalidOperationException($"Rock {recipe.Family}/{recipe.Seed} cannot be published: {reason}; {diagnostics}");
        }

        private static bool TryCanonicalFaces(RockPolyhedron solid, out List<RockPolyhedron.Face> faces,
            out float maximumAdjustment, out float minimumCross, out float requiredCross)
        {
            faces = new List<RockPolyhedron.Face>(solid.Faces.Count);
            var positions = new List<float3>();
            maximumAdjustment = 0;
            minimumCross = float.PositiveInfinity;
            float3 minimum = new float3(float.PositiveInfinity), maximum = new float3(float.NegativeInfinity);
            float toleranceSquared = solid.Epsilon * solid.Epsilon * 9f;
            // Use exactly the first-representative rule in RockPolyhedron.Validate.
            // Rendering keeps split vertices; mathematically shared corners receive
            // the same float position instead of independent intersection roundoff.
            foreach (var face in solid.Faces)
            {
                var points = new List<float3>(face.Points.Count);
                foreach (float3 point in face.Points)
                {
                    float3 canonical = point;
                    bool found = false;
                    foreach (float3 representative in positions)
                        if (math.lengthsq(representative - point) < toleranceSquared)
                        { canonical = representative; found = true; break; }
                    if (!found) positions.Add(point);
                    maximumAdjustment = math.max(maximumAdjustment, math.length(canonical - point));
                    points.Add(canonical);
                    minimum = math.min(minimum, canonical); maximum = math.max(maximum, canonical);
                }
                faces.Add(new RockPolyhedron.Face(face.Normal, points));
            }
            // Validator requires cross > 1e-7 * boundsDiagonal^2. The factor four
            // reserves room for final float grounding without relaxing that gate.
            requiredCross = 4e-7f * math.lengthsq(maximum - minimum);
            foreach (var face in faces)
            {
                int fan = RockPolyhedron.FanStart(face);
                float3 origin = face.Points[fan];
                for (int i = 1; i < face.Points.Count - 1; i++)
                {
                    float3 cross = math.cross(face.Points[(fan + i) % face.Points.Count] - origin,
                        face.Points[(fan + i + 1) % face.Points.Count] - origin);
                    float length = math.length(cross);
                    minimumCross = math.min(minimumCross, length);
                    if (!math.isfinite(length) || length <= requiredCross ||
                        math.dot(cross, face.Normal) < length * 0.9995f) return false;
                }
            }
            return true;
        }

        public static bool Validate(Mesh mesh, out string reason)
        {
            var policy = new EarthMeshIntegrityPolicy(true, true, false, 4096,
                weldTolerance: 0.000001f, strictFlatNormals: true);
            EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(mesh, policy);
            reason = report.IsValid ? null : report.ToString();
            return report.IsValid;
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

        private static float Next(System.Random random, float minimum, float maximum) =>
            Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }
}
