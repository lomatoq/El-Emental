using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.Geometry;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Publishes centered, unit-sized physics copies of the approved base-centered
    /// Graphics V5 rocks. The lookdev source meshes remain untouched while pooled
    /// fragments keep the unit-mesh scale contract used by EarthFragment.
    /// </summary>
    internal static class RumblePhysicsRockAssetBuilder
    {
        private const string SourceFolder = "Assets/Elemental/Content/GraphicsV5/Rocks";
        private const string OutputFolder = "Assets/Elemental/Content/GraphicsV5/Physics";

        private static readonly string[] HeroSources =
        {
            "V5_Boulder_00",
            "V5_Boulder_01",
            "V5_Boulder_02",
            "V5_Boulder_03",
            "V5_Boulder_04",
            "V5_Boulder_05",
            "V5_Boulder_06",
            "V5_Boulder_07"
        };

        private static readonly string[] DebrisSources =
        {
            "V5_Pebble_17",
            "V5_Pebble_18",
            "V5_Pebble_19",
            "V5_Wedge_12"
        };

        public static Mesh[] CreateOrUpdateHeroLibrary() => CreateOrUpdateLibrary(HeroSources);

        public static Mesh[] CreateOrUpdateDebrisLibrary() => CreateOrUpdateLibrary(DebrisSources);

        private static Mesh[] CreateOrUpdateLibrary(string[] sourceNames)
        {
            EnsureFolder(OutputFolder);
            var results = new Mesh[sourceNames.Length];
            for (int index = 0; index < sourceNames.Length; index++)
                results[index] = CreateOrUpdatePhysicsCopy(sourceNames[index]);
            return results;
        }

        private static Mesh CreateOrUpdatePhysicsCopy(string sourceName)
        {
            string sourcePath = $"{SourceFolder}/{sourceName}.asset";
            Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath);
            if (source == null)
                throw new UnityEditor.Build.BuildFailedException(
                    $"Approved Graphics V5 rock is missing: {sourcePath}");
            if (!source.isReadable)
                throw new UnityEditor.Build.BuildFailedException(
                    $"Approved Graphics V5 rock must stay readable for physics baking: {sourcePath}");
            if (!RumbleRockMeshFactory.Validate(source, out string sourceReason))
                throw new UnityEditor.Build.BuildFailedException(
                    $"Approved Graphics V5 rock is invalid: {sourcePath}. {sourceReason}");

            string outputName = sourceName.Replace("V5_", "V5_Physics_") + "_CenteredUnit";
            string outputPath = $"{OutputFolder}/{outputName}.asset";
            Mesh generated = UnityEngine.Object.Instantiate(source);
            generated.name = outputName;

            Bounds sourceBounds = source.bounds;
            float normalization = Mathf.Max(
                sourceBounds.size.x,
                Mathf.Max(sourceBounds.size.y, sourceBounds.size.z));
            if (!float.IsFinite(normalization) || normalization <= 0.0001f)
            {
                UnityEngine.Object.DestroyImmediate(generated);
                throw new UnityEditor.Build.BuildFailedException(
                    $"Approved Graphics V5 rock has collapsed bounds: {sourcePath}");
            }

            Vector3[] vertices = generated.vertices;
            float inverseScale = 1f / normalization;
            for (int index = 0; index < vertices.Length; index++)
                vertices[index] = (vertices[index] - sourceBounds.center) * inverseScale;
            generated.vertices = vertices;
            generated.RecalculateBounds();
            OrientClosedMeshConsistently(generated, outputPath);

            EarthMeshIntegrityReport integrity = EarthMeshIntegrityValidator.Validate(
                generated,
                EarthMeshIntegrityPolicy.ConvexCollider);
            if (!integrity.IsValid)
            {
                UnityEngine.Object.DestroyImmediate(generated);
                throw new UnityEditor.Build.BuildFailedException(
                    $"Centered Graphics V5 physics rock failed the convex gate: {outputPath}. {integrity}");
            }

            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(generated, outputPath);
                asset = generated;
            }
            else
            {
                EditorUtility.CopySerialized(generated, asset);
                UnityEngine.Object.DestroyImmediate(generated);
                asset.name = outputName;
                EditorUtility.SetDirty(asset);
            }

            UnityEngine.Physics.BakeMesh(asset.GetEntityId(), true);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void OrientClosedMeshConsistently(Mesh mesh, string owner)
        {
            Vector3[] vertices = mesh.vertices;
            int[] welded = BuildWeldMap(vertices, 0.00001f);
            var triangles = new List<TriangleAddress>(mesh.triangles.Length / 3);
            var submeshIndices = new int[mesh.subMeshCount][];
            var edges = new Dictionary<EdgeKey, List<EdgeUse>>(triangles.Capacity * 2);

            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                    throw new UnityEditor.Build.BuildFailedException(
                        $"Centered Graphics V5 physics rock uses non-triangle topology: {owner}");
                int[] indices = mesh.GetIndices(submesh, true);
                submeshIndices[submesh] = indices;
                for (int offset = 0; offset + 2 < indices.Length; offset += 3)
                {
                    int triangleIndex = triangles.Count;
                    int a = welded[indices[offset]];
                    int b = welded[indices[offset + 1]];
                    int c = welded[indices[offset + 2]];
                    triangles.Add(new TriangleAddress(submesh, offset));
                    AddEdge(edges, new EdgeUse(triangleIndex, a < b ? 1 : -1), a, b);
                    AddEdge(edges, new EdgeUse(triangleIndex, b < c ? 1 : -1), b, c);
                    AddEdge(edges, new EdgeUse(triangleIndex, c < a ? 1 : -1), c, a);
                }
            }

            var adjacency = new List<OrientationConstraint>[triangles.Count];
            for (int index = 0; index < adjacency.Length; index++)
                adjacency[index] = new List<OrientationConstraint>(3);
            foreach (KeyValuePair<EdgeKey, List<EdgeUse>> pair in edges)
            {
                List<EdgeUse> uses = pair.Value;
                if (uses.Count != 2)
                    throw new UnityEditor.Build.BuildFailedException(
                        $"Centered Graphics V5 physics rock is not a closed two-manifold: {owner}; " +
                        $"edge {pair.Key} has {uses.Count} uses.");
                EdgeUse first = uses[0];
                EdgeUse second = uses[1];
                bool oppositeFlip = first.Direction == second.Direction;
                adjacency[first.Triangle].Add(new OrientationConstraint(second.Triangle, oppositeFlip));
                adjacency[second.Triangle].Add(new OrientationConstraint(first.Triangle, oppositeFlip));
            }

            var orientation = new sbyte[triangles.Count];
            for (int index = 0; index < orientation.Length; index++) orientation[index] = -1;
            var queue = new Queue<int>(triangles.Count);
            for (int seed = 0; seed < triangles.Count; seed++)
            {
                if (orientation[seed] >= 0) continue;
                orientation[seed] = 0;
                queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    List<OrientationConstraint> neighbours = adjacency[current];
                    for (int index = 0; index < neighbours.Count; index++)
                    {
                        OrientationConstraint constraint = neighbours[index];
                        sbyte expected = (sbyte)(orientation[current] ^ (constraint.OppositeFlip ? 1 : 0));
                        if (orientation[constraint.Triangle] < 0)
                        {
                            orientation[constraint.Triangle] = expected;
                            queue.Enqueue(constraint.Triangle);
                        }
                        else if (orientation[constraint.Triangle] != expected)
                        {
                            throw new UnityEditor.Build.BuildFailedException(
                                $"Centered Graphics V5 physics rock has non-orientable topology: {owner}");
                        }
                    }
                }
            }

            for (int index = 0; index < triangles.Count; index++)
            {
                if (orientation[index] == 0) continue;
                TriangleAddress triangle = triangles[index];
                int[] indices = submeshIndices[triangle.Submesh];
                (indices[triangle.Offset + 1], indices[triangle.Offset + 2]) =
                    (indices[triangle.Offset + 2], indices[triangle.Offset + 1]);
            }
            for (int submesh = 0; submesh < submeshIndices.Length; submesh++)
                mesh.SetIndices(submeshIndices[submesh], MeshTopology.Triangles, submesh, false, 0);
            mesh.RecalculateBounds();

            EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(
                mesh,
                EarthMeshIntegrityPolicy.ConvexCollider);
            if ((report.Issues & EarthMeshIntegrityIssue.InvertedClosedComponent) != 0)
            {
                if (!EarthMeshIntegrityValidator.TryRepairFullyInvertedClosedMesh(mesh, out report))
                    throw new UnityEditor.Build.BuildFailedException(
                        $"Centered Graphics V5 physics rock could not repair its globally inverted winding: " +
                        $"{owner}. {report}");
            }
            if (!report.IsValid)
                throw new UnityEditor.Build.BuildFailedException(
                    $"Centered Graphics V5 physics rock could not be oriented safely: {owner}. {report}");
        }

        private static int[] BuildWeldMap(Vector3[] vertices, float tolerance)
        {
            var lookup = new Dictionary<QuantizedPosition, int>(vertices.Length);
            var welded = new int[vertices.Length];
            int next = 0;
            for (int index = 0; index < vertices.Length; index++)
            {
                var key = new QuantizedPosition(vertices[index], tolerance);
                if (!lookup.TryGetValue(key, out int value))
                {
                    value = next++;
                    lookup.Add(key, value);
                }
                welded[index] = value;
            }
            return welded;
        }

        private static void AddEdge(
            Dictionary<EdgeKey, List<EdgeUse>> edges,
            EdgeUse use,
            int from,
            int to)
        {
            var key = new EdgeKey(from, to);
            if (!edges.TryGetValue(key, out List<EdgeUse> uses))
            {
                uses = new List<EdgeUse>(2);
                edges.Add(key, uses);
            }
            uses.Add(use);
        }

        private readonly struct QuantizedPosition : IEquatable<QuantizedPosition>
        {
            private readonly long _x;
            private readonly long _y;
            private readonly long _z;

            public QuantizedPosition(Vector3 value, float tolerance)
            {
                double inverse = 1.0 / tolerance;
                _x = (long)Math.Round(value.x * inverse);
                _y = (long)Math.Round(value.y * inverse);
                _z = (long)Math.Round(value.z * inverse);
            }

            public bool Equals(QuantizedPosition other) =>
                _x == other._x && _y == other._y && _z == other._z;
            public override bool Equals(object obj) => obj is QuantizedPosition other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _x.GetHashCode();
                    hash = (hash * 397) ^ _y.GetHashCode();
                    return (hash * 397) ^ _z.GetHashCode();
                }
            }
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly int _a;
            private readonly int _b;

            public EdgeKey(int a, int b)
            {
                if (a <= b) { _a = a; _b = b; }
                else { _a = b; _b = a; }
            }

            public bool Equals(EdgeKey other) => _a == other._a && _b == other._b;
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode() => unchecked((_a * 397) ^ _b);
            public override string ToString() => $"({_a},{_b})";
        }

        private readonly struct EdgeUse
        {
            public EdgeUse(int triangle, int direction)
            {
                Triangle = triangle;
                Direction = direction;
            }

            public int Triangle { get; }
            public int Direction { get; }
        }

        private readonly struct OrientationConstraint
        {
            public OrientationConstraint(int triangle, bool oppositeFlip)
            {
                Triangle = triangle;
                OppositeFlip = oppositeFlip;
            }

            public int Triangle { get; }
            public bool OppositeFlip { get; }
        }

        private readonly struct TriangleAddress
        {
            public TriangleAddress(int submesh, int offset)
            {
                Submesh = submesh;
                Offset = offset;
            }

            public int Submesh { get; }
            public int Offset { get; }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new InvalidOperationException($"Cannot create asset folder: {path}");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
