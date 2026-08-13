using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Simulation.Voxel;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class VoxelPlanetStateTests
    {
        [Test]
        public void AnalyticSphere_HasExpectedInsideSurfaceAndOutsideSigns()
        {
            VoxelPlanetState state = new VoxelPlanetState(10f, 123u);

            Assert.That(state.SampleDensityMaterial(float3.zero).Density, Is.LessThan(0f));
            Assert.That(state.SampleDensityMaterial(new float3(10f, 0f, 0f)).Density, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(state.SampleDensityMaterial(new float3(11f, 0f, 0f)).Density, Is.GreaterThan(0f));
        }

        [Test]
        public void OrderedCsg_SubtractThenAddRestoresBoundedSolid()
        {
            VoxelPlanetState state = new VoxelPlanetState(10f, 1u);
            state.Apply(new EditBatch(
                SphereEdit(1u, SdfEditKind.SubtractSphere, float3.zero, 3f),
                SphereEdit(2u, SdfEditKind.AddSphere, float3.zero, 1f)));

            Assert.That(state.SampleDensityMaterial(float3.zero).IsSolid, Is.True);
            Assert.That(state.SampleDensityMaterial(new float3(2f, 0f, 0f)).IsSolid, Is.False);
            Assert.That(state.EditCount, Is.EqualTo(2));
        }

        [Test]
        public void CapsuleEdit_CreatesSolidAlongSegment()
        {
            VoxelPlanetState state = new VoxelPlanetState(2f, 2u);
            SdfEdit capsule = new SdfEdit(
                1u,
                SdfEditKind.AddCapsule,
                new float3(3f, 0f, 0f),
                new float3(6f, 0f, 0f),
                0.75f,
                new VoxelMaterialId(1));
            state.Apply(new EditBatch(capsule));

            Assert.That(state.SampleDensityMaterial(new float3(4.5f, 0f, 0f)).IsSolid, Is.True);
            Assert.That(state.SampleDensityMaterial(new float3(4.5f, 1f, 0f)).IsSolid, Is.False);
        }

        [Test]
        public void DirtyBounds_CrossingChunkBorderMarksBothChunks()
        {
            VoxelPlanetState state = new VoxelPlanetState(10f, 3u, 16, 1f);
            state.Apply(new EditBatch(
                SphereEdit(1u, SdfEditKind.SubtractSphere, new float3(15.5f, 0f, 0f), 2f)));
            List<ChunkCoord> dirty = new List<ChunkCoord>();
            state.Chunks.CollectDirty(dirty);

            Assert.That(dirty, Does.Contain(new ChunkCoord(0, 0, 0)));
            Assert.That(dirty, Does.Contain(new ChunkCoord(1, 0, 0)));
        }

        [Test]
        public void SaveLoad_ReproducesSamplesAndChunkHash()
        {
            VoxelPlanetState original = new VoxelPlanetState(10f, 0xCAFEu, 8, 0.75f, 0.2f);
            original.Apply(new EditBatch(
                SphereEdit(1u, SdfEditKind.SubtractSphere, new float3(7f, 0f, 0f), 2f),
                new SdfEdit(
                    2u,
                    SdfEditKind.AddCapsule,
                    new float3(7f, -2f, 0f),
                    new float3(7f, 2f, 0f),
                    0.7f,
                    new VoxelMaterialId(1))));
            ChunkCoord coord = new ChunkCoord(1, 0, 0);
            ulong expectedHash = original.ComputeChunkHash(coord);

            using MemoryStream stream = new MemoryStream();
            VoxelSaveCodec.Write(stream, original);
            stream.Position = 0;
            VoxelPlanetState loaded = VoxelSaveCodec.Read(stream);

            Assert.That(loaded.EditCount, Is.EqualTo(original.EditCount));
            Assert.That(loaded.ComputeChunkHash(coord), Is.EqualTo(expectedHash));
            Assert.That(
                loaded.SampleDensityMaterial(new float3(7f, 0f, 0f)).Density,
                Is.EqualTo(original.SampleDensityMaterial(new float3(7f, 0f, 0f)).Density).Within(0.0001f));
        }

        [Test]
        public void VersionOneSave_MigratesToCurrentSchemaWithoutChangingState()
        {
            using MemoryStream legacy = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(legacy, System.Text.Encoding.UTF8, true))
            {
                writer.Write(0x31565045u);
                writer.Write((ushort)1);
                writer.Write(12f);
                writer.Write(0xBEEFu);
                writer.Write(8);
                writer.Write(0.5f);
                writer.Write(0.15f);
                writer.Write(0);
            }

            legacy.Position = 0;
            VoxelSaveLoadResult result = VoxelSaveCodec.ReadWithReport(legacy);

            Assert.That(result.SourceVersion, Is.EqualTo(1));
            Assert.That(result.TargetVersion, Is.EqualTo(VoxelSaveCodec.CurrentVersion));
            Assert.That(result.WasMigrated, Is.True);
            Assert.That(result.State.Radius, Is.EqualTo(12f));
            Assert.That(result.State.Seed, Is.EqualTo(0xBEEFu));
            Assert.That(result.State.EditCount, Is.Zero);
        }

        [Test]
        public void ThousandScriptedEdits_KeepSparseChunkSetBounded()
        {
            VoxelPlanetState state = new VoxelPlanetState(12f, 4u, 8, 1f);
            SdfEdit[] edits = new SdfEdit[1000];
            for (int index = 0; index < edits.Length; index++)
            {
                float angle = index * 0.173f;
                float3 center = new float3(math.cos(angle) * 10f, math.sin(angle * 0.7f) * 4f, math.sin(angle) * 10f);
                edits[index] = SphereEdit((uint)(index + 1), SdfEditKind.SubtractSphere, center, 0.25f);
            }

            state.Apply(new EditBatch(edits));

            Assert.That(state.EditCount, Is.EqualTo(1000));
            Assert.That(state.Chunks.Count, Is.LessThan(100));

            state.SampleDensityMaterial(new float3(10f, 0f, 0f));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1000; index++)
                state.SampleDensityMaterial(new float3(10f, index * 0.0001f, 0f));
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        [Test]
        public void BlockMesher_UsesSharedFieldSamplesAcrossChunkBorder()
        {
            VoxelPlanetState state = new VoxelPlanetState(8f, 5u, 8, 1f);
            BlockSurfaceMesher mesher = new BlockSurfaceMesher();
            VoxelMeshingSettings settings = new VoxelMeshingSettings(8, 1f);
            using ChunkMeshBuffers left = new ChunkMeshBuffers();
            using ChunkMeshBuffers right = new ChunkMeshBuffers();

            mesher.Build(state, new ChunkCoord(0, -1, -1), settings, left);
            mesher.Build(state, new ChunkCoord(1, -1, -1), settings, right);

            Assert.That(left.Vertices.Length, Is.GreaterThan(0));
            Assert.That(left.Indices.Length % 6, Is.EqualTo(0));
            Assert.That(right.Indices.Length % 6, Is.EqualTo(0));
            for (int index = 0; index < left.Normals.Length; index++)
            {
                Assert.That(math.all(math.isfinite(left.Normals[index])), Is.True);
            }
        }

        [Test]
        public void SmoothMesher_ExtractsContinuousCurvedSurfaceWithFiniteNormals()
        {
            VoxelPlanetState state = new VoxelPlanetState(10f, 15u, 8, 1f, 0f);
            var mesher = new SmoothSdfSurfaceMesher();
            var settings = new VoxelMeshingSettings(8, 1f);
            using ChunkMeshBuffers output = new ChunkMeshBuffers();

            mesher.Build(state, new ChunkCoord(0, -1, -1), settings, output);

            Assert.That(output.Vertices.Length, Is.GreaterThan(0));
            Assert.That(output.Vertices.Length, Is.EqualTo(output.Normals.Length));
            Assert.That(output.Indices.Length % 3, Is.Zero);
            bool foundCurvedNormal = false;
            bool foundInterpolatedVertex = false;
            for (int index = 0; index < output.Vertices.Length; index++)
            {
                float3 vertex = output.Vertices[index];
                float3 normal = output.Normals[index];
                Assert.That(math.all(math.isfinite(vertex)), Is.True);
                Assert.That(math.all(math.isfinite(normal)), Is.True);
                Assert.That(math.length(normal), Is.EqualTo(1f).Within(0.001f));
                bool3 curvedAxes = math.abs(normal) > 0.05f;
                int curvedAxisCount = (curvedAxes.x ? 1 : 0) + (curvedAxes.y ? 1 : 0) + (curvedAxes.z ? 1 : 0);
                foundCurvedNormal |= curvedAxisCount >= 2;
                foundInterpolatedVertex |= math.any(math.abs(vertex - math.round(vertex)) > 0.01f);
            }

            Assert.That(foundCurvedNormal, Is.True);
            Assert.That(foundInterpolatedVertex, Is.True);
        }

        [Test]
        public void ChunkBuildResult_IsRejectedWhenEditAdvancesVersion()
        {
            VoxelChunkState chunk = new VoxelChunkState(new ChunkCoord(0, 0, 0));
            uint requestedVersion = chunk.Version;

            chunk.MarkDirty();

            Assert.That(chunk.TryMarkBuilt(requestedVersion, 123UL), Is.False);
            Assert.That(chunk.IsDirty, Is.True);
            Assert.That(chunk.ContentHash, Is.EqualTo(0UL));
        }

        [Test]
        public void BurstScheduledMesherMatchesSynchronousTopologyAndRejectsStaleResult()
        {
            VoxelPlanetState state = new VoxelPlanetState(8f, 51u, 8, 1f);
            ChunkCoord coord = new ChunkCoord(0, -1, -1);
            VoxelChunkState chunk = state.Chunks.GetOrCreate(coord);
            var settings = new VoxelMeshingSettings(8, 1f);
            var synchronous = new BlockSurfaceMesher();
            var scheduled = new ScheduledBlockSurfaceMesher();
            using ChunkMeshBuffers expected = new ChunkMeshBuffers();
            using ChunkMeshBuffers actual = new ChunkMeshBuffers();
            synchronous.Build(state, coord, settings, expected);

            using (ScheduledChunkMeshBuild build = scheduled.Schedule(state, coord, settings, actual, chunk.Version))
                Assert.That(build.Complete(chunk.Version), Is.True);

            Assert.That(actual.Vertices.Length, Is.EqualTo(expected.Vertices.Length));
            Assert.That(actual.Indices.Length, Is.EqualTo(expected.Indices.Length));

            using (ScheduledChunkMeshBuild stale = scheduled.Schedule(state, coord, settings, actual, chunk.Version))
            {
                chunk.MarkDirty();
                Assert.That(stale.Complete(chunk.Version), Is.False);
            }
            Assert.That(actual.Vertices.Length, Is.Zero);
            Assert.That(actual.Indices.Length, Is.Zero);
        }

        [Test]
        public void ColliderDebt_ReportsVersionAgeAndRisk()
        {
            var debt = new ColliderDebt(new ChunkCoord(1, 2, 3), 5u, 3u, 0.4f, 0.5f);

            Assert.That(debt.IsOutstanding, Is.True);
            Assert.That(debt.VersionDebt, Is.EqualTo(2u));
            Assert.That(debt.RiskScore, Is.GreaterThan(20f));
            Assert.That(debt.IsWithin(2u, 0.5f), Is.True);
            Assert.That(debt.IsWithin(1u, 0.5f), Is.False);
        }

        private static SdfEdit SphereEdit(uint sequence, SdfEditKind kind, float3 center, float radius)
        {
            return new SdfEdit(sequence, kind, center, center, radius, new VoxelMaterialId(1));
        }
    }
}
