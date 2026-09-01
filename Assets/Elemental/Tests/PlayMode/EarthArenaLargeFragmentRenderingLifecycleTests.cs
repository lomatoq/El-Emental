using System.Collections;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthArenaLargeFragmentRenderingLifecycleTests
    {
        [UnityTest]
        public IEnumerator ReleasedLargePieceBindsRepairsAndReacquiresNewGeneration()
        {
            StructureFixture fixture = CreateStructure(1f, 0xF4100001u);
            try
            {
                yield return null;
                Assert.That(fixture.Structure.TryAcquirePiece(0), Is.True);
                CapsuleShadowCaster caster =
                    fixture.Piece.GetComponent<CapsuleShadowCaster>();
                Assert.That(caster, Is.Not.Null);
                Assert.That(caster.Classification,
                    Is.EqualTo(CapsuleShadowCasterClass.ActiveFragment));
                Assert.That(caster.StableGroupId,
                    Is.EqualTo(EarthArenaLargeFragmentCapsuleShadowPolicy.StableCohortGroupId));
                Assert.That(caster.IsActiveGeneration, Is.True);
                uint firstGeneration = caster.Generation;

                Assert.That(fixture.Structure.SetMagicRepairProgress(1f), Is.True);
                Assert.That(caster.HasRuntimeBinding, Is.False);
                Assert.That(fixture.Structure.TryAcquirePiece(0), Is.True);
                Assert.That(caster.IsActiveGeneration, Is.True);
                Assert.That(caster.Generation, Is.Not.EqualTo(firstGeneration));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ReleasedTinyPieceNeverCreatesCapsuleProducer()
        {
            StructureFixture fixture = CreateStructure(0.25f, 0xF4100002u);
            try
            {
                yield return null;
                Assert.That(fixture.Structure.TryAcquirePiece(0), Is.True);
                Assert.That(fixture.Piece.GetComponent<CapsuleShadowCaster>(), Is.Null);
                EarthArenaLargeFragmentCapsuleShadowDiagnostics diagnostics =
                    EarthArenaLargeFragmentCapsuleShadowPresenter.Current;
                Assert.That(diagnostics.TinyRejected, Is.GreaterThanOrEqualTo(1));
                Assert.That(diagnostics.RequiresRealtimeArenaShadows, Is.False);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static StructureFixture CreateStructure(float scale, uint structureId)
        {
            var root = new GameObject("Arena structure fixture");
            root.SetActive(false);
            GameObject intact = GameObject.CreatePrimitive(PrimitiveType.Cube);
            intact.transform.SetParent(root.transform, false);
            var fractureRoot = new GameObject("Fracture root");
            fractureRoot.transform.SetParent(root.transform, false);
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.transform.SetParent(fractureRoot.transform, false);
            Mesh mesh = piece.GetComponent<MeshFilter>().sharedMesh;
            var asset = ScriptableObject.CreateInstance<TestFractureAsset>();
            asset.Configure(mesh, scale);
            EarthArenaStructure structure = root.AddComponent<EarthArenaStructure>();
            Assert.That(structure.Configure(
                asset,
                root.transform,
                fractureRoot.transform,
                intact.GetComponent<Renderer>(),
                intact.GetComponent<Collider>(),
                new[] { piece.transform },
                null,
                null,
                null,
                structureId,
                true,
                true), Is.True);
            root.SetActive(true);
            return new StructureFixture(root, piece, asset, structure);
        }

        private sealed class TestFractureAsset : ScriptableObject,
            IEarthFractureAssetRuntimeData
        {
            private Mesh _mesh;
            private float _scale;

            public int SchemaVersion => 1;
            public Mesh IntactRenderMesh => _mesh;
            public Mesh IntactColliderMesh => _mesh;
            public int PieceCount => 1;
            public int BondCount => 0;

            public void Configure(Mesh mesh, float scale)
            {
                _mesh = mesh;
                _scale = scale;
            }

            public Mesh GetPieceRenderMesh(int index) => index == 0 ? _mesh : null;
            public Mesh GetPieceColliderMesh(int index) => index == 0 ? _mesh : null;
            public EarthPieceFaceMetadata GetPieceFaceMetadata(int index) => default;

            public bool CopyDefinitions(
                EarthPieceDefinition[] pieceDestination,
                EarthBondDefinition[] bondDestination)
            {
                if (pieceDestination == null || pieceDestination.Length < 1 ||
                    bondDestination == null)
                    return false;
                pieceDestination[0] = new EarthPieceDefinition
                {
                    Id = new EarthPieceId(1),
                    ParentPieceIndex = EarthBondGraph.WorldPieceIndex,
                    Flags = EarthPieceFlags.Structural | EarthPieceFlags.Repairable,
                    RestLocalPosition = float3.zero,
                    RestLocalRotation = quaternion.identity,
                    RestLocalScale = new float3(_scale),
                    Mass = 20f,
                    Volume = _scale * _scale * _scale,
                    LocalCenterOfMass = float3.zero
                };
                return true;
            }
        }

        private readonly struct StructureFixture
        {
            public StructureFixture(
                GameObject root,
                GameObject piece,
                TestFractureAsset asset,
                EarthArenaStructure structure)
            {
                Root = root;
                Piece = piece;
                Asset = asset;
                Structure = structure;
            }

            public GameObject Root { get; }
            public GameObject Piece { get; }
            public TestFractureAsset Asset { get; }
            public EarthArenaStructure Structure { get; }

            public void Dispose()
            {
                if (Root != null)
                    Object.Destroy(Root);
                if (Asset != null)
                    Object.Destroy(Asset);
            }
        }
    }
}
