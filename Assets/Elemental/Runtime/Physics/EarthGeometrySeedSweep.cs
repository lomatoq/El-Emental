using Elemental.Runtime.Geometry;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public readonly struct EarthGeometrySeedSweepReport
    {
        public EarthGeometrySeedSweepReport(
            int requestedSeedCount,
            int meshCount,
            int invalidPublicationCount,
            int fallbackCount,
            int maximumTriangleCount,
            int firstFailureSeed,
            EarthMeshIntegrityIssue firstFailureIssues)
        {
            RequestedSeedCount = requestedSeedCount;
            MeshCount = meshCount;
            InvalidPublicationCount = invalidPublicationCount;
            FallbackCount = fallbackCount;
            MaximumTriangleCount = maximumTriangleCount;
            FirstFailureSeed = firstFailureSeed;
            FirstFailureIssues = firstFailureIssues;
        }

        public int RequestedSeedCount { get; }
        public int MeshCount { get; }
        public int InvalidPublicationCount { get; }
        public int FallbackCount { get; }
        public int MaximumTriangleCount { get; }
        public int FirstFailureSeed { get; }
        public EarthMeshIntegrityIssue FirstFailureIssues { get; }
        public bool Passed => RequestedSeedCount > 0 && InvalidPublicationCount == 0 && FallbackCount == 0;

        public override string ToString() =>
            $"seeds={RequestedSeedCount}, meshes={MeshCount}, invalid={InvalidPublicationCount}, " +
            $"fallbacks={FallbackCount}, maxTriangles={MaximumTriangleCount}, " +
            $"firstFailure={FirstFailureSeed}:{FirstFailureIssues}";
    }

    public static class EarthGeometrySeedSweep
    {
        public static EarthGeometrySeedSweepReport Run(int seedCount)
        {
            int count = Mathf.Clamp(seedCount, 1, 10000);
            int meshCount = 0;
            int invalid = 0;
            int fallback = 0;
            int maximumTriangles = 0;
            int firstFailureSeed = -1;
            EarthMeshIntegrityIssue firstFailureIssues = EarthMeshIntegrityIssue.None;
            for (int seed = 0; seed < count; seed++)
            {
                Evaluate(EarthArmorPlateMeshFactory.Create(seed), seed, ref meshCount, ref invalid,
                    ref fallback, ref maximumTriangles, ref firstFailureSeed, ref firstFailureIssues);
                Evaluate(EarthWebWaveCellMeshFactory.Create(seed), seed, ref meshCount, ref invalid,
                    ref fallback, ref maximumTriangles, ref firstFailureSeed, ref firstFailureIssues);
            }
            return new EarthGeometrySeedSweepReport(
                count, meshCount, invalid, fallback, maximumTriangles, firstFailureSeed, firstFailureIssues);
        }

        private static void Evaluate(
            Mesh mesh,
            int seed,
            ref int meshCount,
            ref int invalid,
            ref int fallback,
            ref int maximumTriangles,
            ref int firstFailureSeed,
            ref EarthMeshIntegrityIssue firstFailureIssues)
        {
            meshCount++;
            if (mesh != null && mesh.name.Contains("IntegrityFallback")) fallback++;
            EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(
                mesh,
                EarthMeshIntegrityPolicy.ConvexCollider);
            maximumTriangles = Mathf.Max(maximumTriangles, report.TriangleCount);
            if (!report.IsValid)
            {
                invalid++;
                if (firstFailureSeed < 0)
                {
                    firstFailureSeed = seed;
                    firstFailureIssues = report.Issues;
                }
            }
            if (mesh == null) return;
            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);
        }
    }
}
