using Elemental.Runtime.Geometry;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthStoneBevelProfileTests
    {
        [Test]
        public void FourWideChamfersHaveSolidBackingAtTheirSharedJunction()
        {
            var objects = new System.Collections.Generic.List<GameObject>();
            var meshes = new System.Collections.Generic.List<Mesh>();
            try
            {
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                {
                    var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    objects.Add(cell);
                    Object.DestroyImmediate(cell.GetComponent<BoxCollider>());
                    cell.transform.position = new Vector3(x * .5f, y * .5f, 0);
                    Mesh source = cell.GetComponent<MeshFilter>().sharedMesh;
                    Mesh bevel = EarthWallFractureVisual.Create(source, source, null, 17u);
                    Mesh sealedMesh = EarthWallFractureVisual.SealChamferJunctions(bevel, source,
                        cell.transform.localToWorldMatrix, 1f, .0525f);
                    meshes.Add(bevel); meshes.Add(sealedMesh);
                    cell.AddComponent<MeshCollider>().sharedMesh = sealedMesh;
                }
                UnityEngine.Physics.SyncTransforms();
                bool covered = false;
                var ray = new Ray(new Vector3(.001f, .001f, -2f), Vector3.forward);
                foreach (GameObject cell in objects)
                    if (cell.GetComponent<MeshCollider>().Raycast(ray, out var hit, 4f))
                    {
                        covered = true;
                        Assert.That(hit.point.z, Is.InRange(-.5f, -.4f), "Backing must stay shallow, behind the chamfer face.");
                    }
                Assert.That(covered, Is.True, "The junction of four cells must not expose daylight through the wall.");
            }
            finally
            {
                foreach (GameObject cell in objects) Object.DestroyImmediate(cell);
                foreach (Mesh mesh in meshes) Object.DestroyImmediate(mesh);
            }
        }

        [TestCase(2f)]
        [TestCase(8f)]
        public void WallBevelRemainsWiderMetricScaleAcrossWallWidths(float width)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh source = cube.GetComponent<MeshFilter>().sharedMesh;
            Vector3 metric = new Vector3(width, 3f, .55f);
            Mesh beveled = EarthWallFractureVisual.Create(source, source, null, 17u, metric);
            try
            {
                float maximumCut = 0f;
                foreach (Vector3 local in beveled.vertices)
                {
                    Vector3 p = Vector3.Scale(local, metric);
                    Vector3 half = metric * .5f;
                    Assert.That(Mathf.Abs(p.x), Is.LessThanOrEqualTo(half.x + .00001f));
                    Assert.That(Mathf.Abs(p.y), Is.LessThanOrEqualTo(half.y + .00001f));
                    Assert.That(Mathf.Abs(p.z), Is.LessThanOrEqualTo(half.z + .00001f));
                    // Every vertex stays near an original box edge/corner; the
                    // second-largest distance to a boundary measures its chamfer.
                    float[] cuts = { half.x - Mathf.Abs(p.x), half.y - Mathf.Abs(p.y), half.z - Mathf.Abs(p.z) };
                    System.Array.Sort(cuts);
                    maximumCut = Mathf.Max(maximumCut, cuts[1]);
                }
                Assert.That(maximumCut, Is.InRange(.03f, .07f),
                    "Wall chamfers must be visibly wider than the former 15mm cut at either wall width.");
                foreach (Vector3 normal in beveled.normals)
                    Assert.That(normal.sqrMagnitude, Is.InRange(.99f, 1.01f));
            }
            finally { Object.DestroyImmediate(beveled); Object.DestroyImmediate(cube); }
        }

        [Test]
        public void ChippedWallCellsRetainVolumeAndVaryTheirChamfersDeterministically()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh source = cube.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] original = source.vertices;
            Mesh first = EarthWallFractureVisual.Create(source, source, null, 17u);
            Mesh repeated = EarthWallFractureVisual.Create(source, source, null, 17u);
            Mesh other = EarthWallFractureVisual.Create(source, source, null, 31u);
            try
            {
                CollectionAssert.AreEqual(original, source.vertices);
                CollectionAssert.AreEqual(first.vertices, repeated.vertices);
                CollectionAssert.AreNotEqual(first.vertices, other.vertices);
                Assert.That(first.vertexCount, Is.GreaterThan(source.vertexCount));
                Vector3[] vertices = first.vertices;
                foreach (Vector3 vertex in vertices)
                {
                    Assert.That(Mathf.Abs(vertex.x), Is.LessThanOrEqualTo(.50001f));
                    Assert.That(Mathf.Abs(vertex.y), Is.LessThanOrEqualTo(.50001f));
                    Assert.That(Mathf.Abs(vertex.z), Is.LessThanOrEqualTo(.50001f));
                }
                int[] triangles = first.triangles;
                float volume = 0f;
                for (int index = 0; index < triangles.Length; index += 3)
                    volume += Vector3.Dot(vertices[triangles[index]], Vector3.Cross(
                        vertices[triangles[index + 1]], vertices[triangles[index + 2]])) / 6f;
                Assert.That(Mathf.Abs(volume), Is.GreaterThan(.95f),
                    "Varied chips must not shrink the cell into a loose stone.");
            }
            finally
            {
                Object.DestroyImmediate(first); Object.DestroyImmediate(repeated);
                Object.DestroyImmediate(other); Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void RenderCacheReusesPreparedCopiesAndNeverChangesColliderSource()
        {
            var cache = new EarthStoneRenderBevelCache();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh original = cube.GetComponent<MeshFilter>().sharedMesh;
            var vertices = original.vertices;
            try
            {
                Mesh prepared = cache.Get(original, null);
                Assert.That(prepared, Is.Not.SameAs(original));
                Assert.That(prepared.vertexCount, Is.GreaterThan(original.vertexCount));
                Assert.That(cache.Get(original, null), Is.SameAs(prepared));
                Assert.That(cache.Get(prepared, null), Is.SameAs(prepared));
                CollectionAssert.AreEqual(vertices, original.vertices);
                Assert.That(cube.GetComponent<BoxCollider>().size, Is.EqualTo(Vector3.one));
            }
            finally { cache.Clear(); Object.DestroyImmediate(cube); }
        }

        [Test]
        public void DefaultProfilePreservesExistingBevelGeometry()
        {
            var profile = ScriptableObject.CreateInstance<EarthStoneBevelProfile>();
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh original = cube.GetComponent<MeshFilter>().sharedMesh;
            Mesh baseline = EarthFractureBevelMeshBuilder.Create(original);
            Mesh configured = EarthFractureBevelMeshBuilder.Create(original, profile);
            try
            {
                Assert.That(profile.Width, Is.EqualTo(.02f));
                Assert.That(profile.MaxLocalEdgeFraction, Is.EqualTo(.08f));
                CollectionAssert.AreEqual(baseline.vertices, configured.vertices);
                CollectionAssert.AreEqual(baseline.normals, configured.normals);
                CollectionAssert.AreEqual(baseline.triangles, configured.triangles);
            }
            finally
            {
                Object.DestroyImmediate(configured);
                Object.DestroyImmediate(baseline);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void LocalEdgeFractionCapsLargeRequestedWidthWithoutChangingSource()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh original = cube.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] before = original.vertices;
            Mesh narrow = EarthFractureBevelMeshBuilder.Create(original, 1f, .04f);
            Mesh wide = EarthFractureBevelMeshBuilder.Create(original, 1f, .08f);
            try
            {
                Vector3 corner = before[original.GetTriangles(0)[0]];
                Assert.That(Vector3.Distance(narrow.vertices[0], corner), Is.EqualTo(.04f).Within(.0001f));
                Assert.That(Vector3.Distance(wide.vertices[0], corner), Is.EqualTo(.08f).Within(.0001f));
                CollectionAssert.AreEqual(before, original.vertices);
            }
            finally
            {
                Object.DestroyImmediate(narrow);
                Object.DestroyImmediate(wide);
                Object.DestroyImmediate(cube);
            }
        }
    }
}
