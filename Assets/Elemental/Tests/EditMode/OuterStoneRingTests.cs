using System.Collections.Generic;
using Elemental.Authoring.Editor;
using Elemental.Authoring.Fracture;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Tests.EditMode
{
    public sealed class OuterStoneRingTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";

        [Test]
        public void SevenAuthoredStructuresHaveValidDistinctFractureAssets()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EarthArenaFractureCatalog>(OuterStoneRingImporter.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.ImportedModel, Is.Not.Null);
            Assert.That(catalog.Structures, Has.Length.EqualTo(7));
            Assert.That(catalog.LooseRockObjectNames, Has.Length.EqualTo(8));
            var sidecar = JsonUtility.FromJson<Sidecar>(System.IO.File.ReadAllText(OuterStoneRingImporter.SidecarPath));
            Assert.That(catalog.LooseRockObjectNames, Is.EqualTo(sidecar.looseRocks));
            var ids = new HashSet<string>();
            var counts = new HashSet<int>();
            int total = 0;
            foreach (var entry in catalog.Structures)
            {
                Assert.That(ids.Add(entry.structureId), Is.True, entry.structureId);
                Assert.That(entry.ordinaryDamageEnabled && entry.repairable, Is.True, entry.structureId);
                Assert.That(entry.fractureAsset, Is.Not.Null, entry.structureId);
                var result = EarthFractureValidator.Validate(entry.fractureAsset);
                Assert.That(result.IsValid, Is.True, $"{entry.structureId}: {result.Error} at {result.Index}");
                AssertCurrentImportedIntactNormals(entry.structureId, entry.fractureAsset.IntactRenderMesh);
                Assert.That(entry.fractureAsset.IntactColliderMesh,
                    Is.SameAs(entry.fractureAsset.IntactRenderMesh),
                    entry.structureId + " must use the exact baked silhouette for its dormant intact collision proxy.");
                counts.Add(entry.fractureAsset.PieceCount);
                var authored = System.Array.Find(sidecar.structures, item => item.structure_id == entry.structureId);
                Assert.That(authored, Is.Not.Null, entry.structureId);
                Assert.That(entry.fractureAsset.PieceCount, Is.EqualTo(authored.piece_count), entry.structureId);
                total += entry.fractureAsset.PieceCount;
                for (int i = 0; i < entry.fractureAsset.PieceCount; i++)
                {
                    var mesh = entry.fractureAsset.GetPieceRenderMesh(i);
                    Assert.That(mesh, Is.Not.Null);
                    Assert.That(mesh.subMeshCount, Is.EqualTo(2), entry.structureId);
                    Assert.That(mesh.GetIndexCount(0), Is.GreaterThan(0));
                    Assert.That(mesh.GetIndexCount(1), Is.GreaterThan(0));
                    Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount));
                    Assert.That(entry.fractureAsset.GetPieceColliderMesh(i), Is.Not.Null);
                }
            }
            Assert.That(total, Is.EqualTo(85), "Approved export retains 85 standing cells; artist-moved cells are loose.");
            Assert.That(counts.Count, Is.GreaterThan(1), "Authored columns retain distinct fracture counts.");
        }

        [Test]
        public void SavedColumnsHaveBuriedFoundationsArenaMaterialsAndCompleteBindings()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject ring = null;
                GameObject arena = null;
                VoxelPlanetBehaviour planet = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == "Outer Stone Ring") ring = root;
                    if (root.name == "Broken Crown Arena") arena = root;
                    if (planet == null) planet = root.GetComponentInChildren<VoxelPlanetBehaviour>(true);
                }
                Assert.That(ring, Is.Not.Null);
                Assert.That(arena, Is.Not.Null);
                Assert.That(planet, Is.Not.Null);
                Assert.That(ring.transform.parent, Is.Null, "Columns remain separate from arena.");
                var exterior = AssetDatabase.LoadAssetAtPath<Material>(OuterStoneRingImporter.ExteriorPath);
                var interior = AssetDatabase.LoadAssetAtPath<Material>(OuterStoneRingImporter.InteriorPath);
                Assert.That(exterior, Is.Not.Null);
                Assert.That(interior, Is.Not.Null);
                var structures = ring.GetComponentsInChildren<EarthArenaStructure>(true);
                Assert.That(structures, Has.Length.EqualTo(7));
                var ids = new HashSet<uint>();
                foreach (var structure in structures)
                {
                    var floor = GameObject.Find("Arena_FloorBase_INTACT");
                    Assert.That(floor, Is.Not.Null);
                    float gap = OuterStoneRingImporter.PlanarClearance(structure.transform,
                        floor.transform, (arena.transform.position-planet.transform.position).normalized);
                    Assert.That(gap, Is.InRange(2.48f,2.52f), structure.name + " final arena clearance");
                    Assert.That(ids.Add(structure.StructureId), Is.True);
                    Assert.That(structure.HasMaterialFeedback, Is.True, structure.name);
                    var serialized = new SerializedObject(structure);
                    AssertBound(serialized, "rockDebrisPool");
                    AssertBound(serialized, "gravityWorld");
                    AssertBound(serialized, "fractureAssetObject");
                    var fractureAsset = serialized.FindProperty("fractureAssetObject").objectReferenceValue as EarthFractureAsset;
                    Assert.That(fractureAsset, Is.Not.Null, structure.name);
                    var frame = (Transform)serialized.FindProperty("coordinateRoot").objectReferenceValue;
                    Assert.That(frame, Is.Not.Null);
                    var provider = structure.GetComponent<EarthArenaSurfaceProvider>();
                    Assert.That(provider, Is.Not.Null);
                    AssertBound(new SerializedObject(provider), "queryService");
                    var filter = structure.GetComponent<MeshFilter>();
                    var intactRenderer = structure.GetComponent<Renderer>();
                    var intactCollider = structure.GetComponent<MeshCollider>();
                    Assert.That(filter.sharedMesh, Is.SameAs(fractureAsset.IntactRenderMesh),
                        structure.name + " saved intact proxy is stale relative to the imported fracture catalog.");
                    Assert.That(intactCollider.sharedMesh, Is.SameAs(fractureAsset.IntactColliderMesh), structure.name);
                    Assert.That(intactRenderer.enabled, Is.True, structure.name);
                    Assert.That(intactCollider.enabled, Is.True, structure.name);
                    Vector3[] vertices = filter.sharedMesh.vertices;
                    float minimum = float.PositiveInfinity;
                    foreach (var vertex in vertices)
                        minimum = Mathf.Min(minimum, frame.InverseTransformPoint(filter.transform.TransformPoint(vertex)).z);
                    int capSamples = 0;
                    float maximumRadius = 0f;
                    foreach (var vertex in vertices)
                    {
                        Vector3 world = filter.transform.TransformPoint(vertex);
                        if (frame.InverseTransformPoint(world).z > minimum + .07f) continue;
                        capSamples++;
                        maximumRadius = Mathf.Max(maximumRadius, Vector3.Distance(world, planet.transform.position));
                    }
                    Assert.That(capSamples, Is.GreaterThanOrEqualTo(3), structure.name);
                    Assert.That(maximumRadius, Is.LessThanOrEqualTo(planet.Radius - .06f),
                        structure.name + " foundation cap protrudes from curved ground.");
                    Assert.That(structure.GetComponent<Renderer>().sharedMaterials,
                        Is.EqualTo(new[] { exterior, interior }),
                        structure.name + " must use the exact ordered arena exterior/interior material pair.");
                    var pieces = serialized.FindProperty("pieces");
                    Assert.That(pieces.arraySize, Is.EqualTo(structure.PieceCount));
                    var fractureRoot = (Transform)serialized.FindProperty("fractureRoot").objectReferenceValue;
                    Assert.That(fractureRoot, Is.Not.Null, structure.name);
                    Assert.That(fractureRoot.gameObject.activeSelf, Is.False,
                        structure.name + " must save as one intact mesh, with its fracture hierarchy dormant.");
                    for (int i = 0; i < pieces.arraySize; i++)
                    {
                        var piece = (Transform)pieces.GetArrayElementAtIndex(i).objectReferenceValue;
                        Assert.That(piece, Is.Not.Null);
                        Assert.That(piece.gameObject.activeSelf, Is.False,
                            piece.name + " must remain hidden until the intact proxy receives an interaction.");
                        Assert.That(piece.GetComponent<EarthArenaPiece>().Owner, Is.SameAs(structure));
                        Assert.That(piece.GetComponent<Rigidbody>(), Is.Not.Null);
                        Assert.That(piece.GetComponent<GravityBody>(), Is.Not.Null);
                        Assert.That(piece.GetComponent<MeshCollider>().convex, Is.True);
                        Assert.That(piece.GetComponent<Renderer>().sharedMaterials, Is.EqualTo(new[] { exterior, interior }));
                    }
                }
                var rocks = ring.GetComponentsInChildren<EarthDestructibleDecorRock>(true);
                Assert.That(rocks, Has.Length.EqualTo(8));
                foreach (var rock in rocks)
                {
                    var serialized = new SerializedObject(rock);
                    AssertBound(serialized, "debrisPool");
                    AssertBound(serialized, "materialFeedback");
                    AssertBound(serialized, "gravityBody");
                    Assert.That(rock.GetComponent<Renderer>().sharedMaterials, Is.EqualTo(new[] { exterior, interior }));
                }
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void AssertBound(SerializedObject owner, string field)
        {
            var property = owner.FindProperty(field);
            Assert.That(property, Is.Not.Null, field);
            Assert.That(property.objectReferenceValue, Is.Not.Null, owner.targetObject.name + ": " + field);
        }

        private static void AssertCurrentImportedIntactNormals(string structureId, Mesh mesh)
        {
            Assert.That(mesh, Is.Not.Null, structureId);
            Assert.That(AssetDatabase.GetAssetPath(mesh), Is.EqualTo(OuterStoneRingImporter.ModelPath),
                structureId + " intact state must remain a baked FBX mesh rather than a runtime union.");

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] triangles = mesh.triangles;
            Assert.That(normals, Has.Length.EqualTo(vertices.Length), structureId);
            Assert.That(triangles.Length, Is.GreaterThan(0), structureId);
            Assert.That(triangles.Length % 3, Is.Zero, structureId);

            int significantTriangles = 0;
            int smoothlyShadedCorners = 0;
            float minimumCornerDot = 1f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 cross = Vector3.Cross(
                    vertices[triangles[i + 1]] - vertices[triangles[i]],
                    vertices[triangles[i + 2]] - vertices[triangles[i]]);
                // Twice a 1e-6 m2 triangle area, squared. Smaller authored cleanup
                // remnants are intentionally excluded, matching the Blender gate.
                if (cross.sqrMagnitude < 4e-12f) continue;
                significantTriangles++;
                // Vector3.normalized collapses vectors shorter than Unity's
                // internal epsilon to zero. Our Blender gate deliberately keeps
                // valid authored triangles down to 1e-6 m2, whose doubled area
                // can be below that epsilon after FBX unit conversion. Normalize
                // explicitly so those triangles are judged by their real winding.
                Vector3 faceNormal = cross / Mathf.Sqrt(cross.sqrMagnitude);
                for (int corner = 0; corner < 3; corner++)
                {
                    Vector3 normal = normals[triangles[i + corner]];
                    Assert.That(float.IsNaN(normal.x) || float.IsNaN(normal.y) || float.IsNaN(normal.z) ||
                                float.IsInfinity(normal.x) || float.IsInfinity(normal.y) || float.IsInfinity(normal.z),
                        Is.False, structureId + " contains a non-finite imported corner normal.");
                    Assert.That(normal.sqrMagnitude, Is.GreaterThan(.81f),
                        structureId + " contains a zero or non-unit imported corner normal.");
                    float dot = Vector3.Dot(faceNormal, normal.normalized);
                    minimumCornerDot = Mathf.Min(minimumCornerDot, dot);
                    if (dot < .999f) smoothlyShadedCorners++;
                }
            }

            Assert.That(significantTriangles, Is.GreaterThan(100), structureId);
            Assert.That(minimumCornerDot, Is.GreaterThan(.70f),
                structureId + " has a significant triangle whose imported normal opposes its winding.");
            Assert.That(smoothlyShadedCorners, Is.GreaterThan(10),
                structureId + " lost the approved smooth-by-angle normals during Unity import.");
        }

        [System.Serializable]
        private sealed class Sidecar
        {
            public SidecarStructure[] structures = System.Array.Empty<SidecarStructure>();
            public string[] looseRocks = System.Array.Empty<string>();
        }

        [System.Serializable]
        private sealed class SidecarStructure
        {
            public string structure_id = string.Empty;
            public int piece_count = 0;
        }
    }
}
