using Elemental.Runtime.Geometry;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthStoneBevelProfileTests
    {
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
