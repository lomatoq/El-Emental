using System.Collections;
using System.Collections.Generic;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class StartupCacheRuntimeTests
    {
        private static PlanetBaseMeshCache MakeCache()
        {
            var state = new VoxelPlanetState(4f,42,4,1,.35f);
            var cache = ScriptableObject.CreateInstance<PlanetBaseMeshCache>();
            var entries = new List<PlanetBaseMeshCache.Entry>();
            using var mesher = new SmoothSdfSurfaceMesher();
            using var buffers = new ChunkMeshBuffers();
            var settings = new VoxelMeshingSettings(4,1);
            for(int z=-1;z<=1;z++) for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++)
            {
                if(!PlanetChunkShellSolver.IntersectsSurfaceShell(new int3(x,y,z),4,4,1.85f)) continue;
                var coord = new ChunkCoord(x,y,z);
                mesher.Build(state,coord,settings,buffers);
                var mesh = new Mesh { name = "Test exact base "+coord };
                var vertices = new Vector3[buffers.Vertices.Length]; var normals = new Vector3[buffers.Normals.Length]; var triangles = new int[buffers.Indices.Length];
                for(int i=0;i<vertices.Length;i++) { vertices[i]=buffers.Vertices[i]; normals[i]=buffers.Normals[i]; }
                for(int i=0;i<triangles.Length;i++) triangles[i]=buffers.Indices[i];
                mesh.vertices=vertices; mesh.normals=normals; mesh.triangles=triangles; mesh.RecalculateBounds();
                entries.Add(new PlanetBaseMeshCache.Entry { X=x,Y=y,Z=z,Mesh=mesh,ContentHash=state.ComputeChunkHash(coord) });
            }
            cache.Configure(state,entries.ToArray());
            return cache;
        }

        private static VoxelPlanetBehaviour MakePlanet(PlanetBaseMeshCache cache, Vector3 position)
        {
            var go = new GameObject("Cached planet test"); go.SetActive(false); go.transform.position=position;
            var planet = go.AddComponent<VoxelPlanetBehaviour>();
            planet.Configure(4,42,4,1,4,4,null); planet.ConfigureBaseMeshCache(cache); go.SetActive(true);
            return planet;
        }

        [UnityTest]
        public IEnumerator CompleteBaseExistsBeforeFirstFrameAndEditsNeverMutateSharedAssets()
        {
            var cache = MakeCache();
            var original = new Dictionary<Mesh,string>();
            foreach(var entry in cache.Entries) original.Add(entry.Mesh,EarthConvexFractureCacheAsset.Signature(entry.Mesh));
            var first = MakePlanet(cache, Vector3.zero);
            var second = MakePlanet(cache, Vector3.right*100);
            try
            {
                Assert.That(first.BaseCacheUsed,Is.True); Assert.That(first.GeometryReady,Is.True);
                Assert.That(first.RuntimeChunkCount,Is.EqualTo(cache.Entries.Count));
                Assert.That(first.PendingRenderCount+first.PendingColliderCount,Is.Zero);
                for(int pass=0;pass<3;pass++)
                {
                    var receipt = first.ApplySphereEditTransactional(new Vector3(0,4,0),1.1f,pass==1);
                    for(int frame=0;frame<80&&!first.IsEditCommitted(receipt);frame++) yield return null;
                    Assert.That(first.IsEditCommitted(receipt),Is.True);
                    foreach(var entry in cache.Entries)
                        Assert.That(EarthConvexFractureCacheAsset.Signature(entry.Mesh),Is.EqualTo(original[entry.Mesh]),"A transaction mutated a borrowed base asset.");
                    Assert.That(second.State.EditCount,Is.Zero);
                    foreach(var filter in second.GetComponentsInChildren<MeshFilter>()) Assert.That(original.ContainsKey(filter.sharedMesh),Is.True);
                }
                Assert.That(first.State.EditCount,Is.EqualTo(3));
                Object.Destroy(first.gameObject); yield return null;
                foreach(var entry in cache.Entries) Assert.That(entry.Mesh!=null,Is.True,"Destroying edited instance destroyed borrowed mesh.");
            }
            finally
            {
                if(first!=null) Object.Destroy(first.gameObject);
                Object.Destroy(second.gameObject);
                foreach(var entry in cache.Entries) Object.Destroy(entry.Mesh);
                Object.Destroy(cache);
            }
        }

        [UnityTest]
        public IEnumerator OverlappingTransactionsCommitNewestGeometryWithoutChangingBakedBase()
        {
            var cache=MakeCache(); var planet=MakePlanet(cache,Vector3.zero);
            try
            {
                var first=planet.ApplySphereEditTransactional(new Vector3(0,4,0),1.2f,false);
                var second=planet.ApplySphereEditTransactional(new Vector3(.5f,4,0),.7f,true);
                for(int frame=0;frame<100&&(!planet.IsEditCommitted(first)||!planet.IsEditCommitted(second));frame++) yield return null;
                Assert.That(planet.IsEditCommitted(first)&&planet.IsEditCommitted(second),Is.True);
                Assert.That(planet.PendingEditTransactionCount,Is.Zero);
                Assert.That(planet.GeometryReady,Is.True);
                foreach(var entry in cache.Entries)
                    Assert.That(entry.ContentHash,Is.EqualTo(new VoxelPlanetState(4,42,4,1,.35f).ComputeChunkHash(entry.Coord)));
            }
            finally { Object.Destroy(planet.gameObject); foreach(var e in cache.Entries) Object.Destroy(e.Mesh); Object.Destroy(cache); }
        }

        [UnityTest]
        public IEnumerator MissingCacheKeepsCommandsPausedUntilCanonicalGroundAndPhysicsReady()
        {
            float previousTime=Time.timeScale;
            var controls=new GameObject("Loading control test"); var control=controls.AddComponent<Camera>(); control.enabled=true;
            var poolObject=new GameObject("Loading pool test"); poolObject.SetActive(false);
            var pool=poolObject.AddComponent<EarthRockDebrisPool>();
            Mesh source=EarthSafeMeshFactory.CreateBox("Loading pool source",new Bounds(Vector3.zero,Vector3.one));
            pool.Configure(16,null,source,null,null);
            var groundObject=new GameObject("Uncached ground test"); groundObject.SetActive(false);
            var planet=groundObject.AddComponent<VoxelPlanetBehaviour>(); planet.Configure(4,42,4,1,1,1,null);
            var gateObject=new GameObject("Loading gate test"); gateObject.SetActive(false);
            var gate=gateObject.AddComponent<EarthSceneReadinessGate>(); gate.Configure(planet,pool,new Behaviour[]{control});
            try
            {
                gateObject.SetActive(true); groundObject.SetActive(true); poolObject.SetActive(true);
                Assert.That(gate.IsReady,Is.False); Assert.That(control.enabled,Is.False); Assert.That(Time.timeScale,Is.Zero);
                for(int frame=0;frame<150&&!gate.IsReady;frame++) yield return null;
                Assert.That(gate.IsReady,Is.True,gate.Status); Assert.That(planet.GeometryReady&&pool.PhysicsPrepared,Is.True);
                Assert.That(control.enabled,Is.True); Assert.That(Time.timeScale,Is.EqualTo(previousTime));
            }
            finally
            {
                Object.Destroy(gateObject); Object.Destroy(groundObject); Object.Destroy(poolObject); Object.Destroy(controls); Object.Destroy(source);
                Time.timeScale=previousTime;
            }
        }
    }
}
