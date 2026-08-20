using System;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthVolumetricFractureTests
    {
        private static readonly float2[] WallBoundary =
        {
            new float2(-4.5f, -0.38f),
            new float2(4.5f, -0.38f),
            new float2(4.5f, 0.38f),
            new float2(-4.5f, 0.38f)
        };

        [Test]
        public void ConvexCellsPartitionTheWholeSourceVolume()
        {
            EarthVolumetricFracturePlan plan = EarthVolumetricFractureSolver.BuildConvexPrism(
                0xE17E0003u,
                WallBoundary,
                -0.18f,
                4.2f,
                40);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Cells, Has.Length.EqualTo(40));
            Assert.That(plan.RelativeVolumeError, Is.LessThan(0.02f));
            foreach (EarthVolumetricFractureCell cell in plan.Cells)
            {
                Assert.That(cell.Volume, Is.GreaterThan(0f));
                Assert.That(cell.Vertices.Length, Is.GreaterThanOrEqualTo(4));
                Assert.That(cell.TriangleCount, Is.InRange(4, 255));
                Assert.That(IsClosedByIndex(cell.Triangles), Is.True,
                    $"Cell {cell.Id} contains a T-junction or open edge.");
            }
        }

        [Test]
        public void ThinProductionWallCellsRemainClosed()
        {
            float2[] boundary =
            {
                new float2(-4f, -0.275f), new float2(4f, -0.275f),
                new float2(4f, 0.275f), new float2(-4f, 0.275f)
            };
            EarthVolumetricFracturePlan plan = EarthVolumetricFractureSolver.BuildConvexPrism(
                0xE17F1002u, boundary, -2f, 2f, 40);
            for (int index = 0; index < plan.Cells.Length; index++)
                Assert.That(IsClosedByIndex(plan.Cells[index].Triangles), Is.True,
                    $"Thin production cell {index} is open before mesh cooking.");
        }

        [Test]
        public void CellsOccupyThreeDimensionsAndHaveAHeavyTail()
        {
            EarthVolumetricFracturePlan plan = EarthVolumetricFractureSolver.BuildConvexPrism(
                0x51A7C0DEu,
                WallBoundary,
                -0.18f,
                4.2f,
                40);
            var volumes = new float[plan.Cells.Length];
            var aspects = new float[plan.Cells.Length];
            bool[] heightLayers = new bool[3];
            bool[] depthLayers = new bool[3];
            for (int index = 0; index < plan.Cells.Length; index++)
            {
                EarthVolumetricFractureCell cell = plan.Cells[index];
                volumes[index] = cell.Volume;
                aspects[index] = cell.AspectRatio;
                heightLayers[math.clamp((int)math.floor(math.unlerp(-0.18f, 4.2f, cell.Site.y) * 3f), 0, 2)] = true;
                depthLayers[math.clamp((int)math.floor(math.unlerp(-0.38f, 0.38f, cell.Site.z) * 3f), 0, 2)] = true;
            }
            Array.Sort(volumes);
            Array.Sort(aspects);

            Assert.That(heightLayers, Is.All.True, "Fracture needs at least three real height layers.");
            Assert.That(depthLayers, Is.All.True, "Sites must vary through wall depth, not only over its face.");
            float p10 = volumes[MathfIndex(volumes.Length, 0.10f)];
            float p90 = volumes[MathfIndex(volumes.Length, 0.90f)];
            Assert.That(p90 / math.max(0.000001f, p10), Is.GreaterThanOrEqualTo(3f));
            Assert.That(aspects[MathfIndex(aspects.Length, 0.50f)], Is.LessThanOrEqualTo(3.5f));
            Assert.That(aspects[aspects.Length - 1], Is.LessThanOrEqualTo(6f));
        }

        [Test]
        public void InteriorFacesArePairedWithTheOppositeSharedPlane()
        {
            EarthVolumetricFracturePlan plan = EarthVolumetricFractureSolver.BuildConvexPrism(
                0xCA551F1Eu,
                WallBoundary,
                -0.18f,
                4.2f,
                36);
            int paired = 0;
            for (int cellIndex = 0; cellIndex < plan.Cells.Length; cellIndex++)
            {
                EarthVolumetricFractureCell cell = plan.Cells[cellIndex];
                foreach (EarthVolumetricFractureFace face in cell.Faces)
                {
                    if (face.NeighbourCellIndex < 0) continue;
                    EarthVolumetricFractureCell neighbour = plan.Cells[face.NeighbourCellIndex];
                    bool found = false;
                    foreach (EarthVolumetricFractureFace opposite in neighbour.Faces)
                    {
                        if (opposite.NeighbourCellIndex != cellIndex) continue;
                        if (math.dot(face.Normal, opposite.Normal) > -0.999f) continue;
                        if (math.abs(face.Area - opposite.Area) > math.max(0.0005f, face.Area * 0.005f)) continue;
                        found = true;
                        paired++;
                        break;
                    }
                    Assert.That(found, Is.True, $"Cell {cellIndex} has an unpaired interior face.");
                }
            }
            Assert.That(paired, Is.GreaterThan(plan.Cells.Length));
        }

        [Test]
        public void CantileverOutlineProducesOnlyClosedCellsAcrossSeedSweep()
        {
            float2[] cantilever =
            {
                new float2(-2.1f, -1.18f),
                new float2(2.1f, -1.18f),
                new float2(2.38f, 0.21f),
                new float2(1.95f, 1.18f),
                new float2(-1.98f, 1.18f),
                new float2(-2.30f, 0.09f)
            };
            for (uint seed = 1u; seed <= 128u; seed++)
            {
                EarthVolumetricFracturePlan plan = EarthVolumetricFractureSolver.BuildClosedConvexPrism(
                    seed ^ 0xC011AB1Eu,
                    cantilever,
                    -0.14f,
                    0.72f,
                    36);
                Assert.That(plan.IsValid, Is.True, $"Seed {seed} did not preserve source volume.");
                Assert.That(EarthVolumetricFractureSolver.HasClosedTopology(in plan), Is.True,
                    $"Seed proposal {seed} did not find a publishable closed topology.");
                for (int index = 0; index < plan.Cells.Length; index++)
                {
                    Assert.That(IsClosedByIndex(plan.Cells[index].Triangles), Is.True,
                        $"Cantilever seed {seed}, cell {index} has an open edge: " +
                        DescribeOpenEdges(plan.Cells[index].Triangles));
                    Assert.That(plan.Cells[index].TriangleCount, Is.InRange(4, 255));
                }
            }
        }

        private static int MathfIndex(int length, float percentile) =>
            math.clamp((int)math.floor((length - 1) * percentile), 0, length - 1);

        private static bool IsClosedByIndex(int[] triangles)
        {
            var edges = new System.Collections.Generic.Dictionary<ulong, int>();
            for (int index = 0; index < triangles.Length; index += 3)
            {
                CountEdge(edges, triangles[index], triangles[index + 1]);
                CountEdge(edges, triangles[index + 1], triangles[index + 2]);
                CountEdge(edges, triangles[index + 2], triangles[index]);
            }
            foreach (int count in edges.Values)
                if (count != 2) return false;
            return true;
        }

        private static void CountEdge(
            System.Collections.Generic.Dictionary<ulong, int> edges,
            int a,
            int b)
        {
            uint low = (uint)math.min(a, b);
            uint high = (uint)math.max(a, b);
            ulong key = ((ulong)low << 32) | high;
            edges.TryGetValue(key, out int count);
            edges[key] = count + 1;
        }

        private static string DescribeOpenEdges(int[] triangles)
        {
            var edges = new System.Collections.Generic.Dictionary<ulong, int>();
            for (int index = 0; index < triangles.Length; index += 3)
            {
                CountEdge(edges, triangles[index], triangles[index + 1]);
                CountEdge(edges, triangles[index + 1], triangles[index + 2]);
                CountEdge(edges, triangles[index + 2], triangles[index]);
            }
            var open = new System.Collections.Generic.List<string>();
            foreach (var pair in edges)
            {
                if (pair.Value == 2) continue;
                open.Add($"{pair.Key >> 32}-{pair.Key & 0xFFFFFFFFu}x{pair.Value}");
            }
            var involved = new System.Collections.Generic.List<string>();
            for (int index = 0; index < triangles.Length; index += 3)
            {
                int a = triangles[index];
                int b = triangles[index + 1];
                int c = triangles[index + 2];
                ulong ab = ((ulong)(uint)math.min(a, b) << 32) | (uint)math.max(a, b);
                ulong bc = ((ulong)(uint)math.min(b, c) << 32) | (uint)math.max(b, c);
                ulong ca = ((ulong)(uint)math.min(c, a) << 32) | (uint)math.max(c, a);
                if (edges[ab] != 2 || edges[bc] != 2 || edges[ca] != 2)
                    involved.Add($"[{a},{b},{c}]");
            }
            return string.Join(",", open) + " tris=" + string.Join("", involved);
        }
    }
}
