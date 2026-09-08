using Elemental.Runtime.Fire;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class FireSurfaceResolverTests
    {
        private GameObject _surface;
        private BoxCollider _box;
        private FireSurfaceBinding _binding;
        private FireSurfaceResolver _resolver;
        [SetUp] public void SetUp()
        {
            _surface = new GameObject("Fire finite face test");
            _box = _surface.AddComponent<BoxCollider>(); _box.size = new Vector3(4, 4, 1);
            _binding = _surface.AddComponent<FireSurfaceBinding>(); _binding.Configure(91);
            _resolver = new FireSurfaceResolver();
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_surface);
        [Test] public void FootprintShrinksBeforeTheActualBoxEdge()
        {
            Assert.That(_resolver.TryResolve(_box, new Vector3(1.95f, 0, 0.5f), 1, out _, out var patch));
            Assert.That(patch.Radius, Is.LessThan(0.05f));
            Assert.That(_resolver.TryResolve(_box, new Vector3(2, 0, 0.5f), 1, out _, out _), Is.False);
        }
        [Test] public void CornerFacesStaySeparateAndDoNotAverageNormals()
        {
            _resolver.TryResolve(_box, new Vector3(0, 0, 0.5f), 0.3f, out var front, out var frontPatch);
            _resolver.TryResolve(_box, new Vector3(2, 0, 0), 0.3f, out var side, out var sidePatch);
            Assert.That(front.Handle.Face, Is.Not.EqualTo(side.Handle.Face));
            Assert.That(Unity.Mathematics.math.dot(frontPatch.Normal, sidePatch.Normal), Is.Zero.Within(0.00001f));
            var cache = new FireContactCache(); cache.Add(front, 0); cache.Add(side, 0);
            var output = new FireContactPatch[8]; Assert.That(cache.CopyCurrent(_resolver, 0, output), Is.EqualTo(2));
        }
        [Test] public void GeometryRevisionInvalidatesImmediatelyBeforePublication()
        {
            _resolver.TryResolve(_box, new Vector3(0, 0, 0.5f), 1, out var anchor, out _);
            var cache = new FireContactCache(); cache.Add(anchor, 0);
            _binding.InvalidateGeometry();
            Assert.That(cache.CopyCurrent(_resolver, 0, new FireContactPatch[8]), Is.Zero);
        }
        [Test] public void PooledReuseAndBoxResizeCannotReviveOldContact()
        {
            _resolver.TryResolve(_box, new Vector3(0, 0, 0.5f), 1, out var anchor, out _);
            _binding.BeginGeneration(); Assert.That(_resolver.TryRefresh(anchor, out _), Is.False);
            _resolver.TryResolve(_box, new Vector3(0, 0, 0.5f), 1, out anchor, out _);
            _box.size *= 0.5f; Assert.That(_resolver.TryRefresh(anchor, out _), Is.False);
        }
        [Test] public void MovedAnchorTracksPoseAndAngularPointVelocity()
        {
            Rigidbody body = _surface.AddComponent<Rigidbody>(); body.useGravity = false;
            body.linearVelocity = new Vector3(1, 0, 0); body.angularVelocity = new Vector3(0, 2, 0);
            _resolver.TryResolve(_box, new Vector3(0, 0, 0.5f), 1, out var anchor, out _);
            _surface.transform.position = new Vector3(10, 0, 0);
            Assert.That(_resolver.TryRefresh(anchor, out var patch));
            Assert.That(patch.Point.x, Is.EqualTo(10).Within(0.0001f));
            Assert.That(patch.AngularVelocity.y, Is.EqualTo(2).Within(0.0001f));
            Vector3 expected = body.GetPointVelocity(new Vector3(patch.Point.x, patch.Point.y, patch.Point.z));
            Assert.That(patch.SurfaceVelocity.x, Is.EqualTo(expected.x).Within(0.0001f));
        }
    }
}
