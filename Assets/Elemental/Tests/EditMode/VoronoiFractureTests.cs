using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class VoronoiFractureTests
    {
        [Test]
        public void SolverProducesDeterministicGapFreeIrregularPartition()
        {
            VoronoiFractureCell[] first = VoronoiFractureSolver.Build(0xE17F1234u, 13);
            VoronoiFractureCell[] second = VoronoiFractureSolver.Build(0xE17F1234u, 13);

            Assert.That(first.Length, Is.EqualTo(13));
            Assert.That(second.Length, Is.EqualTo(first.Length));
            float totalArea = 0f;
            int diagonalEdges = 0;
            for (int cellIndex = 0; cellIndex < first.Length; cellIndex++)
            {
                VoronoiFractureCell cell = first[cellIndex];
                Assert.That(cell.Vertices.Length, Is.GreaterThanOrEqualTo(3));
                Assert.That(cell.Area, Is.GreaterThan(0.001f));
                Assert.That(math.distance(cell.Centroid, second[cellIndex].Centroid), Is.LessThan(0.000001f));
                Assert.That(second[cellIndex].Vertices.Length, Is.EqualTo(cell.Vertices.Length));
                totalArea += cell.Area;
                for (int vertexIndex = 0; vertexIndex < cell.Vertices.Length; vertexIndex++)
                {
                    float2 a = cell.Vertices[vertexIndex];
                    float2 b = cell.Vertices[(vertexIndex + 1) % cell.Vertices.Length];
                    Assert.That(math.distance(a, second[cellIndex].Vertices[vertexIndex]), Is.LessThan(0.000001f));
                    float2 edge = b - a;
                    bool boundary = math.abs(math.abs(a.x) - 0.5f) < 0.0001f &&
                                    math.abs(math.abs(b.x) - 0.5f) < 0.0001f ||
                                    math.abs(math.abs(a.y) - 0.5f) < 0.0001f &&
                                    math.abs(math.abs(b.y) - 0.5f) < 0.0001f;
                    if (!boundary && math.abs(edge.x) > 0.01f && math.abs(edge.y) > 0.01f) diagonalEdges++;
                }
            }

            Assert.That(totalArea, Is.EqualTo(1f).Within(0.001f));
            Assert.That(diagonalEdges, Is.GreaterThan(12), "The fracture must not collapse into a rectilinear grid.");
        }

        [Test]
        public void DifferentSeedsProduceDifferentFractureCenters()
        {
            VoronoiFractureCell[] first = VoronoiFractureSolver.Build(11u, 13);
            VoronoiFractureCell[] second = VoronoiFractureSolver.Build(12u, 13);
            float accumulatedDistance = 0f;
            for (int index = 0; index < first.Length; index++)
                accumulatedDistance += math.distance(first[index].Centroid, second[index].Centroid);
            Assert.That(accumulatedDistance, Is.GreaterThan(0.2f));
        }

        [Test]
        public void AspectCompensatedPatternKeepsWideWallChunksChunkyInWorldSpace()
        {
            const float aspect = 3.5f;
            VoronoiFractureCell[] cells = VoronoiFractureSolver.BuildNormalizedForAspect(
                0xE17F5678u, 18, aspect);

            float totalArea = 0f;
            int chunkyCells = 0;
            for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                VoronoiFractureCell cell = cells[cellIndex];
                totalArea += cell.Area;
                float minX = float.PositiveInfinity;
                float maxX = float.NegativeInfinity;
                float minY = float.PositiveInfinity;
                float maxY = float.NegativeInfinity;
                for (int vertexIndex = 0; vertexIndex < cell.Vertices.Length; vertexIndex++)
                {
                    float2 vertex = cell.Vertices[vertexIndex];
                    minX = math.min(minX, vertex.x);
                    maxX = math.max(maxX, vertex.x);
                    minY = math.min(minY, vertex.y);
                    maxY = math.max(maxY, vertex.y);
                }

                float worldWidth = (maxX - minX) * aspect;
                float worldHeight = maxY - minY;
                float ratio = worldWidth / math.max(0.0001f, worldHeight);
                if (ratio >= 0.35f && ratio <= 2.85f) chunkyCells++;
            }

            Assert.That(cells.Length, Is.EqualTo(18));
            Assert.That(totalArea, Is.EqualTo(1f).Within(0.001f));
            Assert.That(chunkyCells, Is.GreaterThanOrEqualTo(14),
                "Most pieces should be rock-like chunks after the wall's non-uniform scale is applied.");
        }

        [Test]
        public void HierarchicalPatternContainsLargeAndSmallRockFamilies()
        {
            VoronoiFractureCell[] cells =
                VoronoiFractureSolver.BuildHierarchicalNormalizedForAspect(0xC0E51001u, 1.65f);
            float minimumArea = float.PositiveInfinity;
            float maximumArea = 0f;
            float totalArea = 0f;
            int smallCells = 0;
            int largeCells = 0;
            for (int index = 0; index < cells.Length; index++)
            {
                float area = cells[index].Area;
                minimumArea = math.min(minimumArea, area);
                maximumArea = math.max(maximumArea, area);
                totalArea += area;
                if (area < 0.025f) smallCells++;
                if (area > 0.065f) largeCells++;
            }

            Assert.That(cells.Length, Is.EqualTo(24));
            Assert.That(totalArea, Is.EqualTo(1f).Within(0.001f));
            Assert.That(maximumArea / minimumArea, Is.GreaterThan(3f));
            Assert.That(smallCells, Is.GreaterThanOrEqualTo(4));
            Assert.That(largeCells, Is.GreaterThanOrEqualTo(3));
        }
    }
}
