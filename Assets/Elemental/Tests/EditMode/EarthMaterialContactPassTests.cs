using Elemental.Runtime.Geometry;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Structures;
using Elemental.Simulation.Matter;
using Unity.Mathematics;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMaterialContactPassTests
    {
        [Test]
        public void RockProfileControlsSharedSizeCountsAndSplitDepth()
        {
            EarthRockProfile profile = ScriptableObject.CreateInstance<EarthRockProfile>();
            GameObject host = new GameObject("Shared rock policy test");
            host.SetActive(false);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"smallShatterRadius\":0.5,\"hugeShatterRadius\":1.5," +
                    "\"mediumPieceCount\":2,\"hugePieceCount\":2,\"maximumSplitDepth\":1," +
                    "\"smallImpactSpeed\":2,\"minimumShatterImpulse\":120,\"shatterSpecificImpulse\":10}", profile);
                EarthRockDebrisPool pool = host.AddComponent<EarthRockDebrisPool>();
                pool.Configure(16, null, null, null, profile);
                Assert.That(profile.ResolveBreak(0.4f, 10f, 15f).Breaks, Is.False);
                Assert.That(pool.ResolveBreak(0.4f, 10f, 25f).PhysicalPieces, Is.Zero);
                Assert.That(pool.ResolveBreak(1f, 20f, 180f).Breaks, Is.False);
                Assert.That(profile.ResolveBreak(1f, 20f, 240f).PhysicalPieces, Is.EqualTo(2));
                Assert.That(pool.ResolveBreak(2f, 20f, 240f).PhysicalPieces, Is.EqualTo(2));
                Assert.That(pool.ResolveBreak(1f, 20f, 240f, false, 1).PhysicalPieces, Is.Zero);
                JsonUtility.FromJsonOverwrite("{\"maximumSplitDepth\":999}", profile);
                Assert.That(profile.MaximumSplitDepth, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(profile);
            }
        }

        [TestCase(3)]
        [TestCase(4)]
        public void CanonicalSplitKeepsMassOwnerAndOriginalCavity(int count)
        {
            var registry = new EarthMatterRegistry(32);
            var source = new EarthSourceProvenance(EarthSourceKind.TerrainEdit, 77, 2, 9, 13,
                new float3(1, 2, 3), 12f, EarthProvenanceFlags.ExactReturnSupported |
                EarthProvenanceFlags.SourceCavityValid | EarthProvenanceFlags.VolumeReserved);
            var parent = new EarthMatterRecord { Phase = EarthMatterPhase.FreeDynamic,
                Representation = EarthRepresentationTier.HeroPhysical, Volume = 12f, Mass = 48f,
                Integrity = 1f, Source = source, Owner = new EarthOwnerId(45, 3),
                CurrentPose = EarthMatterPose.Identity, RestPose = EarthMatterPose.Identity };
            Assert.That(registry.TryRegister(parent, out EarthMatterId parentId), Is.True);
            var children = new EarthMatterRecord[count];
            var ids = new EarthMatterId[count];
            for (int i = 0; i < count; i++)
                children[i] = EarthRockBreakPolicy.PartitionChild(parent, count, EarthMatterPose.Identity, float3.zero);
            Assert.That(registry.TrySplit(parentId, children, count, ids), Is.True);
            float volume = 0f, mass = 0f, reserved = 0f;
            for (int i = 0; i < count; i++)
            {
                Assert.That(registry.TryGet(ids[i], out EarthMatterRecord child), Is.True);
                volume += child.Volume; mass += child.Mass; reserved += child.Source.ReservedVolume;
                Assert.That(child.Owner, Is.EqualTo(parent.Owner));
                Assert.That(child.Source.SourceStableId, Is.EqualTo(77));
                Assert.That(child.Source.SourceLocalPoint, Is.EqualTo(source.SourceLocalPoint));
                Assert.That(child.Source.SourceCellIndex, Is.EqualTo(9));
                Assert.That(child.Source.Flags, Is.EqualTo(source.Flags));
                Assert.That(child.Phase, Is.EqualTo(EarthMatterPhase.FreeDynamic));
                Assert.That(child.Representation, Is.EqualTo(EarthRepresentationTier.SecondaryPhysical));
            }
            Assert.That(volume, Is.EqualTo(parent.Volume).Within(0.0001f));
            Assert.That(mass, Is.EqualTo(parent.Mass).Within(0.0001f));
            Assert.That(reserved, Is.EqualTo(source.ReservedVolume).Within(0.0001f));
            Assert.That(registry.TryGet(parentId, out EarthMatterRecord consumed), Is.True);
            Assert.That(consumed.Phase, Is.EqualTo(EarthMatterPhase.Consumed));
        }

        [TestCase(0.2f, 0, 24, 8)]
        [TestCase(0.8f, 4, 64, 16)]
        [TestCase(2f, 3, 140, 28)]
        public void LooseStoneBreakUsesSize(float radius, int pieces, int dust, int chips)
        {
            EarthRockBreakDecision result = EarthRockBreakPolicy.Resolve(radius, 10f, 120f, false);
            Assert.That(result.Breaks, Is.True);
            Assert.That(result.PhysicalPieces, Is.EqualTo(pieces));
            Assert.That(result.DustCount, Is.EqualTo(dust));
            Assert.That(result.ChipCount, Is.EqualTo(chips));
            Assert.That(EarthRockBreakPolicy.Resolve(radius, 10f, 120f, true).Breaks, Is.False);
            Assert.That(EarthRockBreakPolicy.Resolve(radius, 10f, 120f, false, 2).PhysicalPieces, Is.Zero);
        }

        [Test]
        public void DirectedArmorVolleyMaintainsEnergyAndNarrowCone()
        {
            for (int i = 0; i < 96; i++)
            {
                Vector3 velocity = EarthArmorController.ResolveDirectedShotVelocity(Vector3.forward,
                    Vector3.up, i, 44f, 2.5f);
                Assert.That(velocity.magnitude, Is.EqualTo(44f).Within(0.001f));
                Assert.That(Vector3.Angle(velocity, Vector3.forward), Is.LessThanOrEqualTo(2.501f));
                Assert.That(velocity.sqrMagnitude / (31f * 31f), Is.GreaterThan(2f));
            }
        }

        [Test]
        public void BevelChangesOnlyRenderGeometryAndPreservesFaceNormals()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh source = cube.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] before = source.vertices;
            Mesh bevel = EarthFractureBevelMeshBuilder.Create(source);
            try
            {
                Assert.That(bevel.vertexCount, Is.GreaterThan(source.vertexCount));
                CollectionAssert.AreEqual(before, source.vertices);
                Assert.That(bevel.subMeshCount, Is.EqualTo(source.subMeshCount));
                foreach (Vector3 n in bevel.normals)
                    Assert.That(n.magnitude, Is.EqualTo(1f).Within(0.002f));
                Assert.That(bevel.bounds.size.x, Is.LessThanOrEqualTo(source.bounds.size.x + 0.001f));
                Assert.That(bevel.triangles.Length, Is.GreaterThan(source.triangles.Length));
            }
            finally
            {
                Object.DestroyImmediate(bevel);
                Object.DestroyImmediate(cube);
            }
        }
    }
}
