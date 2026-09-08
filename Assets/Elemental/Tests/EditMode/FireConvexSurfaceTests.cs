using System.Collections.Generic;
using Elemental.Runtime.Fire;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class FireConvexSurfaceTests
    {
        private GameObject _object;
        private Mesh _mesh;
        private MeshCollider _collider;
        private FireSurfaceResolver _resolver;
        [SetUp] public void SetUp()
        {
            _object = new GameObject("Fire convex surface test");
            _mesh = Cube();
            _collider = _object.AddComponent<MeshCollider>(); _collider.sharedMesh = _mesh; _collider.convex = true;
            _object.AddComponent<FireSurfaceBinding>().Configure(152);
            _resolver = new FireSurfaceResolver();
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(_object); Object.DestroyImmediate(_mesh); }
        [Test] public void CoplanarTriangleDiagonalDoesNotSplitTheFiniteFace()
        {
            Assert.That(_resolver.TryResolve(_collider, new Vector3(0, 0, 1), 2, out _, out var patch));
            Assert.That(patch.Radius, Is.EqualTo(0.998f).Within(0.001f));
            Assert.That(patch.Normal.z, Is.GreaterThan(0.999f));
        }
        [Test] public void ConvexFootprintStopsAtEdgeUnderNonuniformScale()
        {
            _object.transform.localScale = new Vector3(2, 1, 0.5f);
            Assert.That(_resolver.TryResolve(_collider, new Vector3(1.9f, 0, 0.5f), 2, out _, out var patch));
            Assert.That(patch.Radius, Is.EqualTo(0.098f).Within(0.002f));
            Assert.That(_resolver.TryResolve(_collider, new Vector3(2, 0, 0.5f), 2, out _, out _), Is.False);
        }
        [Test] public void InPlaceVertexMutationInvalidatesBeforeAnotherDiscovery()
        {
            Assert.That(_resolver.TryResolve(_collider, new Vector3(0, 0, 1), 2, out var anchor, out _));
            Vector3[] vertices = _mesh.vertices; vertices[0].x += 0.1f; _mesh.vertices = vertices;
            Assert.That(_resolver.TryRefresh(anchor, out _), Is.False);
        }
        [Test] public void TopologyMutationWithSameCountsInvalidates()
        {
            Assert.That(_resolver.TryResolve(_collider, new Vector3(0, 0, 1), 2, out var anchor, out _));
            int[] triangles = _mesh.triangles; int index = triangles[0]; triangles[0] = triangles[1]; triangles[1] = index;
            _mesh.triangles = triangles;
            Assert.That(_resolver.TryRefresh(anchor, out _), Is.False);
        }
        [Test] public void OpenHullCannotCreatePlaneBeyondMissingGeometry()
        {
            var vertices = new List<Vector3>(_mesh.vertices);
            var triangles = new List<int>(_mesh.triangles); triangles.RemoveRange(0, 6);
            var geometry = new FireConvexMeshGeometry();
            Assert.That(geometry.Build(_mesh, vertices, triangles), Is.False);
        }
        [Test] public void PieceIdentityDoesNotAliasAnotherPieceOfSameOwner()
        {
            var first = new FireSurfaceHandle(5, 44, 3, 1, 0, 1);
            var second = new FireSurfaceHandle(5, 44, 3, 1, 0, 2);
            Assert.That(first.Equals(second), Is.False);
        }
        [Test] public void MovingConvexAnchorAndGenerationAreRevalidated()
        {
            Assert.That(_resolver.TryResolve(_collider, new Vector3(0, 0, 1), 2, out var anchor, out _));
            _object.transform.SetPositionAndRotation(new Vector3(5, 0, 0), Quaternion.Euler(0, 90, 0));
            Assert.That(_resolver.TryRefresh(anchor, out var patch));
            Assert.That(patch.Point.x, Is.EqualTo(6).Within(0.001f));
            Assert.That(patch.Normal.x, Is.GreaterThan(0.999f));
            _object.GetComponent<FireSurfaceBinding>().BeginGeneration();
            Assert.That(_resolver.TryRefresh(anchor, out _), Is.False);
        }
        private static Mesh Cube()
        {
            var mesh = new Mesh { name = "Fire closed convex test cube" };
            mesh.vertices = new[]
            {
                new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),
                new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)
            };
            mesh.triangles = new[] { 0,2,1,0,3,2, 4,5,6,4,6,7, 0,1,5,0,5,4,
                3,7,6,3,6,2, 0,4,7,0,7,3, 1,2,6,1,6,5 };
            mesh.RecalculateBounds(); return mesh;
        }
    }
}
