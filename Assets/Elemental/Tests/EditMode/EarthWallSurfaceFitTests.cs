using Elemental.Runtime.Physics;
using Elemental.Runtime.Geometry;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthWallSurfaceFitTests
    {
        [TestCase(0f)]
        [TestCase(90f)]
        public void ShippingUnevenArenaFloorRetainsLongDrawnWall(float angle)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Elemental/Content/Arena/BrokenCrown/BrokenCrownArena.fbx");
            Assert.That(model, Is.Not.Null);
            Transform importedFloor = null;
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                if (child.name == "Arena_FloorBase_INTACT") { importedFloor = child; break; }
            Assert.That(importedFloor, Is.Not.Null);
            Mesh original = importedFloor.GetComponent<MeshFilter>().sharedMesh;
            Mesh mesh = Object.Instantiate(original);
            GameObject owner = new GameObject("Shipping arena construction regression");
            try
            {
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = importedFloor.localToWorldMatrix.MultiplyPoint3x4(vertices[i]);
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                var collider = owner.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                var surfaces = owner.AddComponent<EarthSurfaceQueryService>();
                var structure = owner.AddComponent<EarthArenaStructure>();
                JsonUtility.FromJsonOverwrite("{\"structureId\":9001}", structure);
                var provider = owner.AddComponent<EarthArenaSurfaceProvider>();
                provider.Configure(structure, collider, surfaces, Vector3.up, true);
                Physics.SyncTransforms();
                var query = new EarthSurfaceQuery((float3)(collider.bounds.center + Vector3.up * 5f),
                    new float3(0f, -1f, 0f), 10f, EarthSurfaceCapabilities.Draw);
                Assert.That(provider.TrySample(in query, out EarthSurfaceSample source), Is.True);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.right;
                Vector3 start = (Vector3)source.Point - direction * 3f;
                Vector3 end = (Vector3)source.Point + direction * 3f;
                float thickness = 0.55f;
                TestContext.WriteLine($"Source floor angle={angle}, point={source.Point}, normal={source.Normal}, kind={source.Handle.Kind}, continuous={surfaces.AllowsTopConstructionContinuation(in source)}, bounds={collider.bounds}, determinant={importedFloor.localToWorldMatrix.determinant}");
                Assert.That(EarthWallSurfaceFit.TryFit(surfaces, in source, ref start, ref end, ref thickness), Is.True);
                TestContext.WriteLine($"Fitted length={Vector3.Distance(start,end):F4}, width={thickness:F4}, start={start}, end={end}");
                Assert.That(Vector3.Distance(start, end), Is.GreaterThan(5.8f), "A six metre stroke on the broad shipping arena must not become a short fragment.");
                Assert.That(thickness, Is.EqualTo(0.55f));
                Assert.That(EarthWallSurfaceFit.TryResolveEmbed(surfaces, in source, start, end, thickness, out float embed), Is.True);
                var containment = new EarthArenaMeshPicking(mesh);
                Vector3 across = Vector3.Cross((end - start).normalized, Vector3.up);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 point = (corner < 2 ? start : end) + across * (corner % 2 == 0 ? -0.5f : 0.5f) * thickness - Vector3.up * embed;
                    Assert.That(containment.SquaredDistance(point, Matrix4x4.identity, Matrix4x4.identity, out _), Is.LessThan(0.000001f));
                }
                TestContext.WriteLine($"Shipping floor angle={angle}, length={Vector3.Distance(start,end):F4}, embed={embed:F4}, vertices={vertices.Length}");
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(mesh); }
        }

        [TestCase(0f, 3.9f)]
        [TestCase(0.2f, 1.9f)]
        public void ContiguousArenaTilesContinueButAGapStopsTheOriginalStroke(float gap, float minimumLength)
        {
            var owner = new GameObject("Joined construction tiles");
            var neighbour = new GameObject("Neighbour construction tile");
            try
            {
                var surfaces = owner.AddComponent<EarthSurfaceQueryService>();
                var first = owner.AddComponent<BoxCollider>();
                first.size = new Vector3(2f, 1f, 2f);
                first.center = new Vector3(-1f, 0f, 0f);
                var second = neighbour.AddComponent<BoxCollider>();
                second.size = new Vector3(2f, 1f, 2f);
                second.center = new Vector3(1f + gap, 0f, 0f);
                var from = new BoxFace(first, 77, true);
                surfaces.Register(from);
                surfaces.Register(new BoxFace(second, 78, true));
                Physics.SyncTransforms();
                EarthSurfaceSample source = from.Sample(new Vector3(-1f, 0.5f, 0f));
                Vector3 start = new Vector3(-2f, 0.5f, 0f), end = new Vector3(2f + gap, 0.5f, 0f);
                float thickness = 0.4f;
                Assert.That(EarthWallSurfaceFit.TryFit(surfaces, in source, ref start, ref end, ref thickness), Is.True);
                Assert.That(Vector3.Distance(start, end), Is.GreaterThan(minimumLength));
                if (gap > 0f) Assert.That(end.x, Is.LessThan(0f), "A disconnected tile cannot steal or extend the selected stroke.");
                Assert.That(EarthWallSurfaceFit.TryResolveEmbed(surfaces, in source, start, end, thickness, out float embed), Is.True);
                Assert.That(embed, Is.GreaterThan(0f).And.LessThan(1f));
            }
            finally { Object.DestroyImmediate(neighbour); Object.DestroyImmediate(owner); }
        }

        [TestCase(0f, 0f, 1f)]
        [TestCase(70f, 25f, 1f)]
        [TestCase(90f, 0f, 1f)]
        [TestCase(70f, 0f, 0.02f)]
        public void LongStrokeStaysInsideFiniteFaceWithFourEmbeddedCorners(float tilt, float strokeAngle, float depth)
        {
            var owner = new GameObject("Finite construction face");
            try
            {
                owner.transform.SetPositionAndRotation(new Vector3(10f, 5f, -3f), Quaternion.Euler(tilt, 23f, 0f));
                var shape = owner.AddComponent<BoxCollider>();
                shape.size = new Vector3(1.5f, depth, 0.7f);
                var service = owner.AddComponent<EarthSurfaceQueryService>();
                var provider = new BoxFace(shape);
                service.Register(provider);
                Physics.SyncTransforms();
                Vector3 normal = owner.transform.up;
                Vector3 center = owner.transform.TransformPoint(new Vector3(0f, depth * 0.5f, 0f));
                EarthSurfaceSample surface = provider.Sample(center);
                Vector3 direction = owner.transform.TransformDirection(Quaternion.Euler(0f, strokeAngle, 0f) * Vector3.right);
                Vector3 start = center - direction * 4f, end = center + direction * 4f;
                float thickness = 1.1f;
                Assert.That(EarthWallSurfaceFit.TryFit(service, in surface, ref start, ref end, ref thickness), Is.True);
                Assert.That(EarthWallSurfaceFit.TryResolveEmbed(service, in surface, start, end, thickness, out float embed), Is.True);
                Assert.That(embed, Is.GreaterThan(0f).And.LessThan(depth));
                Assert.That(Vector3.Distance(start, end), Is.InRange(0.25f, 1.65f));
                if (strokeAngle == 0f) Assert.That(Vector3.Distance(start, end), Is.GreaterThan(1.45f));
                Vector3 across = Vector3.Cross((end - start).normalized, normal);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 point = (corner < 2 ? start : end) + across * (corner % 2 == 0 ? -0.5f : 0.5f) * thickness;
                    point -= normal * embed;
                    Assert.That(Vector3.Distance(shape.ClosestPoint(point), point), Is.LessThan(0.0001f), "Every base corner must be inside the supporting mesh.");
                }
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void AnotherFaceCannotSubstituteForMissingOrRecycledSource()
        {
            var owner = new GameObject("Rejected construction face");
            try
            {
                var shape = owner.AddComponent<BoxCollider>();
                var service = owner.AddComponent<EarthSurfaceQueryService>();
                var provider = new BoxFace(shape);
                service.Register(provider);
                Physics.SyncTransforms();
                EarthSurfaceSample surface = provider.Sample(Vector3.up * 0.5f);
                provider.Generation++;
                Vector3 start = new Vector3(-2f, 0.5f, 0f), end = new Vector3(2f, 0.5f, 0f);
                float thickness = 0.2f;
                Assert.That(EarthWallSurfaceFit.TryFit(service, in surface, ref start, ref end, ref thickness), Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        private sealed class BoxFace : IEarthSurfaceProvider, IEarthSurfaceColliderProvider, IEarthContinuousConstructionProvider
        {
            private readonly BoxCollider _shape;
            private readonly uint _id;
            public bool AllowsTopConstructionContinuation { get; }
            public uint Generation = 1;
            public BoxFace(BoxCollider shape, uint id = 77, bool continuous = false)
            { _shape = shape; _id = id; AllowsTopConstructionContinuation = continuous; }
            public Collider ConstructionCollider => _shape;
            private EarthSurfaceHandle Handle => new EarthSurfaceHandle(EarthSurfaceKind.Platform, _id, Generation);
            public EarthSurfaceSample Sample(Vector3 point) => new EarthSurfaceSample(Handle,
                (float3)point, (float3)_shape.transform.up, (float3)_shape.transform.right, default, 0.08f,
                EarthSurfaceMaterial.ConstructedEarth, EarthSurfaceProvenance.RaisedPlatform,
                EarthSurfaceCapabilities.Draw | (AllowsTopConstructionContinuation ? EarthSurfaceCapabilities.Support : EarthSurfaceCapabilities.None));
            public bool IsCurrent(in EarthSurfaceHandle handle) => handle == Handle;
            public bool TrySample(in EarthSurfaceQuery query, out EarthSurfaceSample sample)
            {
                sample = default;
                if (!_shape.Raycast(new Ray((Vector3)query.Origin, (Vector3)query.Direction), out RaycastHit hit, query.MaximumDistance) ||
                    Vector3.Dot(hit.normal, _shape.transform.up) < 0.99f) return false;
                sample = Sample(hit.point);
                return true;
            }
        }
    }
}
