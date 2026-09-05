using System;
using System.Collections.Generic;
using Elemental.Simulation.Structures;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Profiling;

namespace Elemental.Runtime.Geometry
{
    /// <summary>Owner-local cold mesh/cooking cache. Each child is a clipped part of its parent.</summary>
    public sealed class EarthConvexFragmentCache : IDisposable
    {
        public readonly struct Child
        {
            public readonly Mesh ColliderMesh, RenderMesh;
            public readonly Vector3 Center;
            public readonly float Volume;
            public Child(Mesh collider, Mesh render, Vector3 center, float volume)
            { ColliderMesh=collider; RenderMesh=render; Center=center; Volume=volume; }
        }
        private readonly Dictionary<(Mesh,int),Child[]> _plans = new();
        private readonly Dictionary<(string,int),Child[]> _plansBySignature = new();
        private readonly Dictionary<(int,Vector3,Vector3),Mesh> _primitives = new();
        private readonly List<Mesh> _owned = new();
        private static readonly ProfilerMarker PrepareMarker = new("Elemental.Earth.Fracture.PrepareConvexCells");
        public int PreparationCount { get; private set; }
        public int OwnedMeshCount => _owned.Count;
        private readonly Queue<Mesh> _pendingCooking = new();
        private readonly HashSet<Mesh> _cookingQueued = new();
        private static readonly ProfilerMarker CookMarker = new("Elemental.Earth.Fracture.StartupCooking");
        private NativeArray<EntityId> _scheduledMeshIds;
        private JobHandle _cookingHandle;
        private bool _cookingScheduled;
        private int _scheduledCookingCount;
        private double _cookingScheduledAt;
        private bool _bakedCacheAccepted;
        public int PendingCookingCount => _pendingCooking.Count + (_cookingScheduled ? _scheduledCookingCount : 0);
        public int CookedBakedMeshCount { get; private set; }
        public int ScheduledBakedMeshCount { get; private set; }
        public bool BackgroundCookingActive => _cookingScheduled;
        public int BakedPlanMissCount { get; private set; }
        public double PeakCookingSliceMilliseconds { get; private set; }
        public double BackgroundCookingWallMilliseconds { get; private set; }

        private struct BakeConvexMeshesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<EntityId> MeshIds;

            public void Execute(int index) => UnityEngine.Physics.BakeMesh(MeshIds[index], true);
        }

        public void PrepareBakedPhysics(double budgetMilliseconds)
        {
            double start = Time.realtimeSinceStartupAsDouble;
            using (CookMarker.Auto())
            {
                if (!_cookingScheduled) ScheduleBakedPhysics();
                if (_cookingScheduled && _cookingHandle.IsCompleted) CompleteScheduledCooking();
            }
            PeakCookingSliceMilliseconds = Math.Max(PeakCookingSliceMilliseconds, (Time.realtimeSinceStartupAsDouble-start)*1000);
        }

        private void ScheduleBakedPhysics()
        {
            if (_cookingScheduled || _pendingCooking.Count == 0) return;
            _scheduledCookingCount = _pendingCooking.Count;
            _scheduledMeshIds = new NativeArray<EntityId>(_scheduledCookingCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int index = 0; index < _scheduledCookingCount; index++)
                _scheduledMeshIds[index] = _pendingCooking.Dequeue().GetEntityId();
            _cookingScheduledAt = Time.realtimeSinceStartupAsDouble;
            _cookingHandle = new BakeConvexMeshesJob { MeshIds = _scheduledMeshIds }.Schedule(_scheduledCookingCount, 1);
            _cookingScheduled = true;
            ScheduledBakedMeshCount += _scheduledCookingCount;
            JobHandle.ScheduleBatchedJobs();
        }

        private void CompleteScheduledCooking()
        {
            _cookingHandle.Complete();
            CookedBakedMeshCount += _scheduledCookingCount;
            BackgroundCookingWallMilliseconds += (Time.realtimeSinceStartupAsDouble - _cookingScheduledAt) * 1000.0;
            _scheduledMeshIds.Dispose();
            _scheduledCookingCount = 0;
            _cookingScheduled = false;
        }
        public int BakedPlanCount { get; private set; }
        public int BakedRejectedPlanCount { get; private set; }
        public Mesh[] GetOwnedMeshesForBake() => _owned.ToArray();
        public void TransferMeshOwnershipToAsset() => _owned.Clear();
        public EarthConvexFractureCacheAsset.Plan[] ExportPlans()
        {
            var result = new EarthConvexFractureCacheAsset.Plan[_plans.Count];
            int index = 0;
            foreach (var pair in _plans)
            {
                var pieces = new EarthConvexFractureCacheAsset.Piece[pair.Value.Length];
                for (int i = 0; i < pieces.Length; i++)
                {
                    Child child = pair.Value[i];
                    pieces[i] = new EarthConvexFractureCacheAsset.Piece { Collider = child.ColliderMesh,
                        Render = child.RenderMesh, Center = child.Center, Volume = child.Volume };
                }
                result[index++] = new EarthConvexFractureCacheAsset.Plan { Source = pair.Key.Item1,
                    Count = pair.Key.Item2, SourceSignature = EarthConvexFractureCacheAsset.Signature(pair.Key.Item1), Pieces = pieces };
            }
            return result;
        }
        public void LoadBaked(EarthConvexFractureCacheAsset asset)
        {
            if (asset == null) return;
            if (!asset.Current) { Debug.LogWarning("Convex fracture cache revision is stale; using canonical cold preparation. Rebake startup caches."); return; }
            _bakedCacheAccepted = true;
            var signatures = new Dictionary<Mesh, string>();
            foreach (var plan in asset.Plans)
            {
                if (plan.Source == null || plan.Pieces == null || plan.Count < 1 || plan.Count > 4 || plan.Count != plan.Pieces.Length) { BakedRejectedPlanCount++; continue; }
                if (!signatures.TryGetValue(plan.Source, out string signature))
                { signature = EarthConvexFractureCacheAsset.Signature(plan.Source); signatures.Add(plan.Source, signature); }
                if (signature != plan.SourceSignature) { BakedRejectedPlanCount++; continue; }
                bool valid = true;
                foreach (var piece in plan.Pieces) valid &= piece.Collider != null && piece.Render != null && piece.Volume > 0f && float.IsFinite(piece.Volume) && float.IsFinite(piece.Center.x) && float.IsFinite(piece.Center.y) && float.IsFinite(piece.Center.z);
                if (!valid) { BakedRejectedPlanCount++; continue; }
                var children = new Child[plan.Count];
                for (int i = 0; i < children.Length; i++)
                {
                    var p = plan.Pieces[i]; children[i] = new Child(p.Collider, p.Render, p.Center, p.Volume);
                }
                if (_plans.TryAdd((plan.Source, plan.Count), children))
                {
                    BakedPlanCount++;
                    _plansBySignature.TryAdd((plan.SourceSignature, plan.Count), children);
                    foreach (var child in children)
                        if (_cookingQueued.Add(child.ColliderMesh)) _pendingCooking.Enqueue(child.ColliderMesh);
                }
            }
            if (BakedRejectedPlanCount > 0) Debug.LogWarning($"Rejected {BakedRejectedPlanCount} stale/invalid convex fracture plans; canonical cold preparation retained. Rebake startup caches.");
            ScheduleBakedPhysics();
        }

        public bool HasPlan(Mesh source, int count) => source != null && _plans.ContainsKey((source, count));

        public Mesh SourceMesh(Collider collider)
        {
            if (collider is MeshCollider mc && mc.convex && mc.sharedMesh != null && mc.sharedMesh.isReadable) return mc.sharedMesh;
            int kind; Vector3 center, size;
            if(collider is BoxCollider box) { kind=0; center=box.center; size=box.size; }
            else if(collider is SphereCollider sphere) { kind=1; center=sphere.center; size=Vector3.one*sphere.radius; }
            else if(collider is CapsuleCollider capsule) { kind=2+capsule.direction; center=capsule.center; size=new Vector3(capsule.radius,capsule.height,0); }
            else throw new InvalidOperationException("Fracture requires a readable convex mesh, box, sphere or capsule collider.");
            var key=(kind,center,size);
            if(_primitives.TryGetValue(key,out Mesh cached)) return cached;
            Mesh result;
            if(kind==0) result=EarthSafeMeshFactory.CreateBox("Fracture Box Source",new Bounds(center,size));
            else
            {
                var vertices=new List<Vector3>();
                // Inscribed sphere/capsule sampling stays inside the true primitive and
                // retains >90% of its volume before the small fracture seams.
                for(int ring=0;ring<=10;ring++) for(int segment=0;segment<20;segment++)
                {
                    float latitude=Mathf.PI*ring/10f, longitude=Mathf.PI*2f*segment/20f;
                    float radius=kind==1?size.x:size.x;
                    Vector3 p=new Vector3(Mathf.Sin(latitude)*Mathf.Cos(longitude),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(longitude))*radius;
                    if(kind>=2)
                    {
                        p.y+=Mathf.Sign(p.y)*Mathf.Max(0f,size.y*.5f-radius);
                        if(kind==2) p=new Vector3(p.y,p.x,p.z);
                        else if(kind==4) p=new Vector3(p.x,p.z,p.y);
                    }
                    vertices.Add(center+p);
                }
                result=new Mesh { name="Fracture Primitive Convex Source" };
                result.SetVertices(vertices); result.RecalculateBounds();
            }
            _primitives.Add(key,result); _owned.Add(result); return result;
        }

        public Child[] Get(Mesh source,int count)
        {
            var key=(source,count);
            if(_plans.TryGetValue(key,out var cached)) return cached;
            if (_bakedCacheAccepted && source != null && source.isReadable &&
                _plansBySignature.TryGetValue((EarthConvexFractureCacheAsset.Signature(source), count), out cached))
            {
                _plans.Add(key, cached);
                return cached;
            }
            if (_bakedCacheAccepted) BakedPlanMissCount++;
            using(PrepareMarker.Auto())
            {
                if(source==null || !source.isReadable) throw new InvalidOperationException("Unreadable convex fracture source.");
                Vector3[] raw=source.vertices;
                var input=new float3[raw.Length]; for(int i=0;i<raw.Length;i++) input[i]=raw[i];
                EarthConvexPartitionCell[] cells=EarthConvexPartitionSolver.Build(input,count);
                var plan=new Child[count];
                for(int i=0;i<count;i++)
                {
                    var cell=cells[i];
                    var mesh=new Mesh { name=source.name+" Convex Fracture Cell "+i };
                    var vertices=new Vector3[cell.Vertices.Length];
                    for(int v=0;v<vertices.Length;v++) vertices[v]=cell.Vertices[v];
                    mesh.vertices=vertices; mesh.triangles=cell.Triangles;
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    _owned.Add(mesh);
                    // Broad chipped edges soften brick-like arena boundaries while
                    // retaining the exact collider partition and containing the render.
                    float width=Mathf.Min(mesh.bounds.size.x,Mathf.Min(mesh.bounds.size.y,mesh.bounds.size.z))*.18f;
                    Mesh render=EarthFractureBevelMeshBuilder.Create(mesh,width,.22f);
                    if(render!=mesh)
                    {
                        _owned.Add(render);
                        ContainBevel(render, cell);
                    }
                    UnityEngine.Physics.BakeMesh(mesh.GetEntityId(),true);
                    plan[i]=new Child(mesh,render,cell.Center,cell.Volume);
                }
                _plans.Add(key,plan); PreparationCount++; return plan;
            }
        }

        private static void ContainBevel(Mesh render, EarthConvexPartitionCell collider)
        {
            // The shared bevel builder can extend acute corners beyond their source
            // plane. Clip only those vertices radially back into THIS child convex.
            // The interior origin is the cell's vertex barycenter, so every ray is safe.
            Vector3[] vertices=render.vertices;
            for(int v=0;v<vertices.Length;v++)
            {
                float3 point=vertices[v];
                float scale=1f;
                for(int t=0;t<collider.Triangles.Length;t+=3)
                {
                    float3 a=collider.Vertices[collider.Triangles[t]], b=collider.Vertices[collider.Triangles[t+1]], c=collider.Vertices[collider.Triangles[t+2]];
                    float3 normal=math.cross(b-a,c-a);
                    float projection=math.dot(normal,point);
                    if(projection>1e-12f) scale=math.min(scale,math.max(0f,math.dot(normal,a))/projection);
                }
                if(scale<1f) vertices[v]*=scale*.999f;
            }
            render.vertices=vertices;
            render.RecalculateNormals(); render.RecalculateBounds();
        }
        public void Dispose()
        {
            if (_cookingScheduled) CompleteScheduledCooking();
            foreach(var mesh in _owned) if(mesh!=null)
            { if(Application.isPlaying) UnityEngine.Object.Destroy(mesh); else UnityEngine.Object.DestroyImmediate(mesh); }
            _owned.Clear(); _plans.Clear(); _plansBySignature.Clear(); _primitives.Clear(); _pendingCooking.Clear(); _cookingQueued.Clear();
        }
    }
}
