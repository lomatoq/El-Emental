using System;
using System.Collections.Generic;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Voxel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.EditMode
{
    public sealed class StartupCacheTests
    {
        [Test]
        public void PlanetManifestRejectsEveryCanonicalParameterChange()
        {
            var cache = ScriptableObject.CreateInstance<PlanetBaseMeshCache>();
            try
            {
                cache.Configure(new VoxelPlanetState(8, 42, 8, 1, .35f), Array.Empty<PlanetBaseMeshCache.Entry>());
                Assert.That(cache.Matches(new VoxelPlanetState(8, 42, 8, 1, .35f)), Is.True);
                Assert.That(cache.Matches(new VoxelPlanetState(9, 42, 8, 1, .35f)), Is.False);
                Assert.That(cache.Matches(new VoxelPlanetState(8, 43, 8, 1, .35f)), Is.False);
                Assert.That(cache.Matches(new VoxelPlanetState(8, 42, 4, 1, .35f)), Is.False);
                Assert.That(cache.Matches(new VoxelPlanetState(8, 42, 8, .5f, .35f)), Is.False);
                Assert.That(cache.Matches(new VoxelPlanetState(8, 42, 8, 1, .36f)), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(cache); }
        }

        [Test]
        public void BorrowedConvexPlansMatchColdOutputAndDisposeWithoutDestroyingMeshes()
        {
            Mesh source = EarthSafeMeshFactory.CreateBox("Startup test source", new Bounds(Vector3.zero, new Vector3(2,3,2)));
            var asset = ScriptableObject.CreateInstance<EarthConvexFractureCacheAsset>();
            using var cold = new EarthConvexFragmentCache();
            try
            {
                var expected = cold.Get(source, 3);
                cold.Get(expected[0].ColliderMesh, 3);
                asset.Configure(cold.ExportPlans());
                using (var cached = new EarthConvexFragmentCache())
                {
                    cached.LoadBaked(asset);
                    Assert.That(cached.BakedRejectedPlanCount, Is.Zero);
                    Assert.That(cached.BakedPlanCount, Is.EqualTo(2));
                    Assert.That(cached.ScheduledBakedMeshCount, Is.EqualTo(6));
                    Assert.That(cached.BackgroundCookingActive, Is.True,
                        "Baked collider cooking must be scheduled off the loading thread.");
                    var actual = cached.Get(source, 3);
                    Assert.That(cached.PreparationCount, Is.Zero, "Cache hits must not run convex preparation.");
                    Assert.That(cached.BakedPlanMissCount, Is.Zero);
                    for (int i=0; i<actual.Length; i++)
                    {
                        Assert.That(actual[i].ColliderMesh, Is.SameAs(expected[i].ColliderMesh));
                        Assert.That(actual[i].RenderMesh, Is.SameAs(expected[i].RenderMesh));
                        Assert.That(actual[i].Center, Is.EqualTo(expected[i].Center));
                        Assert.That(actual[i].Volume, Is.EqualTo(expected[i].Volume));
                    }
                    DrainBackgroundCooking(cached);
                    Assert.That(cached.CookedBakedMeshCount, Is.EqualTo(6));
                    Assert.That(cached.BackgroundCookingActive, Is.False);
                    Assert.That(cached.BackgroundCookingWallMilliseconds, Is.GreaterThanOrEqualTo(0));
                }
                Assert.That(expected[0].ColliderMesh != null, Is.True, "Borrowed cache disposal destroyed an asset mesh.");
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void PersistentSourcesAreDeduplicatedAcrossDebrisHeroAndScatterLibraries()
        {
            Mesh first = EarthSafeMeshFactory.CreateBox("Pool source A", new Bounds(Vector3.zero, Vector3.one));
            Mesh second = EarthSafeMeshFactory.CreateBox("Pool source B", new Bounds(Vector3.zero, Vector3.one * 2));
            Mesh third = EarthSafeMeshFactory.CreateBox("Scatter collider source", new Bounds(Vector3.zero, Vector3.one * 3));
            var debrisObject = new GameObject("Debris source coverage");
            var fragmentObject = new GameObject("Fragment source coverage");
            var scatterObject = new GameObject("Scatter source coverage");
            debrisObject.SetActive(false); fragmentObject.SetActive(false); scatterObject.SetActive(false);
            var debris = debrisObject.AddComponent<EarthRockDebrisPool>();
            var fragments = fragmentObject.AddComponent<EarthFragmentPool>();
            var scatter = scatterObject.AddComponent<EarthPlanetRockScatter>();
            try
            {
                debris.Configure(16, null, first, null, null);
                debris.ConfigureMeshVariants(first, second, first);
                fragments.Configure(1, null, null, second, null, debris);
                fragments.ConfigureMeshVariants(second, first, second);
                scatter.Configure(null, null, null, null, debris, null, null,
                    new[] { first, second, third }, new[] { third, first, third });
                var sources = new List<Mesh>();
                Assert.That(debris.AppendAuthoredFractureSources(sources), Is.EqualTo(2));
                Assert.That(fragments.AppendAuthoredFractureSources(sources), Is.Zero);
                Assert.That(scatter.AppendAuthoredFractureSources(sources), Is.EqualTo(1));
                Assert.That(sources, Is.EqualTo(new[] { first, second, third }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scatterObject);
                UnityEngine.Object.DestroyImmediate(fragmentObject);
                UnityEngine.Object.DestroyImmediate(debrisObject);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(third);
            }
        }

        [Test]
        public void PersistentPoolSourceCanCoverEverySupportedDescendantCountWithoutColdMiss()
        {
            Mesh source = EarthSafeMeshFactory.CreateBox("Pool recursive source", new Bounds(Vector3.zero, new Vector3(2,3,2)));
            var asset = ScriptableObject.CreateInstance<EarthConvexFractureCacheAsset>();
            using var cold = new EarthConvexFragmentCache();
            try
            {
                for (int count = 3; count <= 4; count++)
                    foreach (var child in cold.Get(source, count))
                        for (int descendantCount = 3; descendantCount <= 4; descendantCount++)
                            cold.Get(child.ColliderMesh, descendantCount);
                asset.Configure(cold.ExportPlans());
                using var cached = new EarthConvexFragmentCache();
                cached.LoadBaked(asset);
                Assert.That(cached.BakedPlanCount, Is.EqualTo(16));
                for (int count = 3; count <= 4; count++)
                    foreach (var child in cached.Get(source, count))
                        for (int descendantCount = 3; descendantCount <= 4; descendantCount++)
                            Assert.That(cached.Get(child.ColliderMesh, descendantCount), Is.Not.Empty);
                Assert.That(cached.PreparationCount, Is.Zero,
                    "Every supported pool impact branch must bind persistent plans instead of preparing on first impact.");
                Assert.That(cached.BakedPlanMissCount, Is.Zero);
                DrainBackgroundCooking(cached);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void EquivalentPrimitiveSourceBindsBakedPlanByValidatedGeometrySignature()
        {
            var authoredObject = new GameObject("Authored primitive source");
            var runtimeObject = new GameObject("Runtime primitive source");
            var authored = authoredObject.AddComponent<BoxCollider>();
            var runtime = runtimeObject.AddComponent<BoxCollider>();
            authored.center = runtime.center = new Vector3(.1f, .3f, -.2f);
            authored.size = runtime.size = new Vector3(1.2f, 2.4f, .8f);
            var asset = ScriptableObject.CreateInstance<EarthConvexFractureCacheAsset>();
            using var cold = new EarthConvexFragmentCache();
            try
            {
                Mesh authoredSource = cold.SourceMesh(authored);
                var expected = cold.Get(authoredSource, 3);
                asset.Configure(cold.ExportPlans());
                using var cached = new EarthConvexFragmentCache();
                cached.LoadBaked(asset);
                Mesh runtimeSource = cached.SourceMesh(runtime);
                var actual = cached.Get(runtimeSource, 3);
                Assert.That(actual[0].ColliderMesh, Is.SameAs(expected[0].ColliderMesh));
                Assert.That(cached.PreparationCount, Is.Zero);
                Assert.That(cached.BakedPlanMissCount, Is.Zero,
                    "A deterministic primitive instance must bind its baked geometry instead of preparing during loading.");
                DrainBackgroundCooking(cached);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtimeObject);
                UnityEngine.Object.DestroyImmediate(authoredObject);
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ChangedConvexSourceRejectsStalePlanBeforeUse()
        {
            Mesh source = EarthSafeMeshFactory.CreateBox("Stale source", new Bounds(Vector3.zero, Vector3.one * 2));
            var asset = ScriptableObject.CreateInstance<EarthConvexFractureCacheAsset>();
            using var cold = new EarthConvexFragmentCache();
            try
            {
                cold.Get(source, 3); asset.Configure(cold.ExportPlans());
                var vertices = source.vertices; vertices[0] += Vector3.up * .1f; source.vertices = vertices;
                using var cached = new EarthConvexFragmentCache();
                LogAssert.Expect(LogType.Warning, "Rejected 1 stale/invalid convex fracture plans; canonical cold preparation retained. Rebake startup caches.");
                cached.LoadBaked(asset);
                Assert.That(cached.BakedPlanCount, Is.Zero);
                Assert.That(cached.BakedRejectedPlanCount, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); UnityEngine.Object.DestroyImmediate(source); }
        }

        private static void DrainBackgroundCooking(EarthConvexFragmentCache cache)
        {
            DateTime timeout = DateTime.UtcNow.AddSeconds(10);
            while (cache.PendingCookingCount > 0 && DateTime.UtcNow < timeout)
            {
                cache.PrepareBakedPhysics(1);
                System.Threading.Thread.Yield();
            }
            Assert.That(cache.PendingCookingCount, Is.Zero,
                "Background collider cooking did not complete within the focused-test timeout.");
        }
    }
}
