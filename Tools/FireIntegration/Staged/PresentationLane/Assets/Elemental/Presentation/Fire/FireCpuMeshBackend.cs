using System;
using System.Runtime.InteropServices;
using Elemental.Simulation.Fire;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.Fire
{
    public readonly struct FireParticleTrace
    {
        public readonly uint Id;
        public readonly float Age;
        public readonly Vector3 Position, Velocity;
        public FireParticleTrace(uint id,float age,Vector3 position,Vector3 velocity)
        { Id=id; Age=age; Position=position; Velocity=velocity; }
    }

    // A bounded cosmetic alternative for Gamma/Web/native non-compute rendering.
    // Exactly one mesh and renderer per group. Mesh quads face the camera in the vertex
    // shader; camera state never enters particle motion. All arrays allocate at admission.
    [BurstCompile]
    public sealed unsafe class FireCpuMeshBackend : IDisposable
    {
        private static readonly ProfilerMarker StepMarker=new ProfilerMarker("Fire.CpuMesh.Step");
        private static readonly int TimeId=Shader.PropertyToID("_FireTime");
        [StructLayout(LayoutKind.Sequential)]
        private struct Vertex
        {
            public Vector3 Centre;
            public Vector2 UV;
            public Vector4 Fire;
            public Vector2 Extent;
            public Vector3 Direction;
        }
        private struct Particle
        {
            public float3 Position,Velocity;
            public float Age,Lifetime,Phase,Heat,Width,Aspect;
            public uint Id;
            public bool Redirected;
        }
        private struct StepContext
        {
            public Particle* Particle;
            public FireFieldNode* Nodes;
            public FireContactPatch* Contacts;
            public int NodeCount,ContactCount,Substeps,Redirected,AliveCount;
            public float3 Origin,FreeUp;
            public float Delta,Offset,FullDelta,Time,FreeLift,FreeDrag,Radius,MaxSpeed,AgeBefore,AgeAfter;
            public uint RedirectedId;
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MoveFunction(StepContext* context);
        private static MoveFunction compiledMove,compiledMoveExisting,compiledBirthVelocity;
        private struct UploadContext
        {
            public Particle* Particles;
            public Vertex* Vertices;
            public int* Order;
            public float* Depth;
            public int Count;
            public float3 Eye,Forward,Minimum,Maximum;
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void UploadFunction(UploadContext* context);
        private static UploadFunction compiledUpload;
        private readonly FireVisualProfile profile;
        private readonly Particle[] particles;
        private readonly int[] order;
        private readonly float[] depth;
        private readonly float[] weights=new float[6];
        private readonly FireContactPatch[] contacts=new FireContactPatch[8];
        private NativeArray<Vertex> vertices;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly GameObject visual;
        private readonly Material material;
        private uint randomState, nextId;
        private float birthRemainder;
        private bool disposed;
        public int AliveCount { get; private set; }
        public double LastStepMilliseconds { get; private set; }
        public int Capacity => particles.Length;
        public int RejectedBirths { get; private set; }
        public int RedirectedExistingParticles { get; private set; }
        public uint LastRedirectedId { get; private set; }
        public float LastRedirectAgeBefore { get; private set; }
        public float LastRedirectAgeAfter { get; private set; }
        public FireCpuMeshBackend(Transform owner,FireVisualProfile settings)
        {
            if(compiledMove==null) compiledMove=BurstCompiler.CompileFunctionPointer<MoveFunction>(MoveCompiled).Invoke;
            if(compiledMoveExisting==null) compiledMoveExisting=BurstCompiler.CompileFunctionPointer<MoveFunction>(MoveExistingCompiled).Invoke;
            if(compiledUpload==null) compiledUpload=BurstCompiler.CompileFunctionPointer<UploadFunction>(UploadCompiled).Invoke;
            if(compiledBirthVelocity==null) compiledBirthVelocity=BurstCompiler.CompileFunctionPointer<MoveFunction>(BirthVelocityCompiled).Invoke;
            profile=settings; int capacity=settings.CpuCapacity>=512?512:256;
            particles=new Particle[capacity]; order=new int[capacity]; depth=new float[capacity];
            vertices=new NativeArray<Vertex>(capacity*4,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
            visual=new GameObject("Fire CPU mesh ("+capacity+")"); visual.transform.SetParent(owner,false);
            mesh=new Mesh {name="Fire bounded cosmetic quads"}; mesh.MarkDynamic();
            mesh.SetVertexBufferParams(capacity*4,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float32,2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord3,VertexAttributeFormat.Float32,3));
            var indices=new ushort[capacity*6];
            for(int i=0;i<capacity;i++) {int k=i*6,v=i*4; indices[k]=(ushort)v; indices[k+1]=(ushort)(v+1); indices[k+2]=(ushort)(v+2); indices[k+3]=(ushort)v; indices[k+4]=(ushort)(v+2); indices[k+5]=(ushort)(v+3);}
            mesh.SetIndexBufferParams(indices.Length,IndexFormat.UInt16); mesh.SetIndexBufferData(indices,0,0,indices.Length);
            mesh.subMeshCount=1; mesh.SetSubMesh(0,new SubMeshDescriptor(0,0));
            visual.AddComponent<MeshFilter>().sharedMesh=mesh;
            renderer=visual.AddComponent<MeshRenderer>(); renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off; renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            var shader=settings.CpuMaterial!=null?settings.CpuMaterial.shader:Shader.Find("Elemental/Fire/CpuMeshFlame");
            if(shader==null || !shader.isSupported) { Dispose(); throw new NotSupportedException("Fire CPU mesh shader is missing or unsupported. Run Build Graphs And Profile."); }
            material=settings.CpuMaterial!=null?new Material(settings.CpuMaterial):new Material(shader);
            material.name="Fire CPU per-group clock"; renderer.sharedMaterial=material; renderer.enabled=false;
        }
        public void Begin(uint seed)
        {
            AliveCount=0; birthRemainder=0; randomState=seed!=0?seed:0x9e3779b9u; nextId=0;
            RejectedBirths=0; RedirectedExistingParticles=0; LastRedirectedId=0;
        }
        public void Step(FirePresentationSnapshot snapshot,float delta,float emission,UnityEngine.Camera camera)
        {
            using(StepMarker.Auto())
            {
                long cpuStart=System.Diagnostics.Stopwatch.GetTimestamp();
                if(snapshot==null || snapshot.NodeCount<0 || snapshot.NodeCount>6 || snapshot.ContactCount<0 || snapshot.ContactCount>8 ||
                    !math.isfinite(delta) || delta<0 || !math.isfinite(snapshot.Time) || !math.isfinite(emission) || emission<0)
                    throw new ArgumentException("Fire CPU frame/counts must be finite and within 6 nodes / 8 contacts.");
                if(disposed) throw new ObjectDisposedException(nameof(FireCpuMeshBackend));
                for(int j=0;j<snapshot.ContactCount;j++) { contacts[j]=snapshot.Contacts[j]; contacts[j].Point-=contacts[j].SurfaceVelocity*delta; }
                if(delta>0)
                {
                    MoveExisting(snapshot,delta);
                    if(emission>0 && snapshot.NodeCount>0)
                    {
                        birthRemainder+=emission*delta; int requested=(int)math.min(math.floor(birthRemainder),1000000); birthRemainder-=requested;
                        int attempts=math.min(requested,Capacity); RejectedBirths+=requested-attempts;
                        for(int n=0;n<attempts;n++)
                        {
                            if(AliveCount>=Capacity) { RejectedBirths+=attempts-n; break; }
                            if(!TryBirth(snapshot,out var particle)) { RejectedBirths++; continue; }
                            float partial=Random01()*delta; Move(ref particle,snapshot,partial,delta-partial,delta);
                            particles[AliveCount++]=particle;
                        }
                    }
                }
                Upload(snapshot,camera);
                LastStepMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-cpuStart)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            }
        }
        private void MoveExisting(FirePresentationSnapshot snapshot,float delta)
        {
            if(AliveCount==0)return;
            // Pin once per group, not once per particle. The function is synchronous;
            // every pointer is invalidated when this fixed scope exits.
            fixed(Particle* items=particles) fixed(FireFieldNode* nodeData=snapshot.Nodes) fixed(FireContactPatch* contactData=contacts)
            {
                var context=new StepContext {Particle=items,AliveCount=AliveCount,Nodes=nodeData,Contacts=contactData,
                    NodeCount=snapshot.NodeCount,ContactCount=snapshot.ContactCount,Substeps=profile.Substeps,
                    Origin=snapshot.Origin,FreeUp=snapshot.FreeUp,Delta=delta,Offset=0,FullDelta=delta,Time=snapshot.Time,
                    FreeLift=profile.FreeLift,FreeDrag=profile.FreeDrag,Radius=profile.ParticleRadius,MaxSpeed=profile.MaximumSpeed};
                compiledMoveExisting(&context);AliveCount=context.AliveCount;
                if(context.Redirected!=0)
                {RedirectedExistingParticles+=context.Redirected;LastRedirectedId=context.RedirectedId;LastRedirectAgeBefore=context.AgeBefore;LastRedirectAgeAfter=context.AgeAfter;}
            }
        }
        [BurstCompile(CompileSynchronously=true)]
        [AOT.MonoPInvokeCallback(typeof(MoveFunction))]
        private static void MoveExistingCompiled(StepContext* context)
        {
            Particle* items=context->Particle;int count=context->AliveCount;
            for(int i=count-1;i>=0;i--)
            {
                context->Particle=items+i;MoveCompiled(context);
                if(items[i].Age>=items[i].Lifetime){count--;items[i]=items[count];}
            }
            context->Particle=items;context->AliveCount=count;
        }
        private void Move(ref Particle particle,FirePresentationSnapshot snapshot,float delta,float offset,float fullDelta)
        {
            fixed(Particle* item=&particle) fixed(FireFieldNode* nodeData=snapshot.Nodes) fixed(FireContactPatch* contactData=contacts)
            {
                var context=new StepContext {Particle=item,Nodes=nodeData,Contacts=contactData,NodeCount=snapshot.NodeCount,ContactCount=snapshot.ContactCount,
                    Substeps=profile.Substeps,Origin=snapshot.Origin,FreeUp=snapshot.FreeUp,Delta=delta,Offset=offset,FullDelta=fullDelta,Time=snapshot.Time,
                    FreeLift=profile.FreeLift,FreeDrag=profile.FreeDrag,Radius=profile.ParticleRadius,MaxSpeed=profile.MaximumSpeed};
                compiledMove(&context);
                if(context.Redirected!=0)
                { RedirectedExistingParticles+=context.Redirected;LastRedirectedId=context.RedirectedId;LastRedirectAgeBefore=context.AgeBefore;LastRedirectAgeAfter=context.AgeAfter; }
            }
        }
        [BurstCompile(CompileSynchronously=true)]
        [AOT.MonoPInvokeCallback(typeof(MoveFunction))]
        private static void MoveCompiled(StepContext* context)
        {
            ref Particle particle=ref *context->Particle;
            float ageBefore=particle.Age; int steps=math.clamp(context->Substeps,1,4); float h=context->Delta/steps;
            for(int step=0;step<steps;step++)
            {
                float localTime=context->Offset+step*h; float3 old=particle.Position;
                FireCpuField.Sample(context->Nodes,context->NodeCount,context->Origin,old,context->Time-context->FullDelta+localTime,out var target,out float response);
                if(response>0) particle.Velocity=math.lerp(particle.Velocity,target,1-math.exp(-response*h));
                else particle.Velocity=(particle.Velocity+context->FreeUp*math.max(context->FreeLift,0)*h)*math.exp(-math.max(context->FreeDrag,0)*h);
                for(int j=0;j<context->ContactCount;j++) FireCpuField.Steer(context->Contacts[j],particle.Position,localTime,context->Radius,particle.Phase,h,ref particle.Velocity);
                particle.Velocity=FireContactMath.Limit(particle.Velocity,context->MaxSpeed); particle.Position+=particle.Velocity*h;
                for(int pass=0;pass<3;pass++) for(int j=0;j<context->ContactCount;j++)
                {
                    float incoming=-math.dot(particle.Velocity-context->Contacts[j].SurfaceVelocity,context->Contacts[j].Normal);
                    if(FireContactMath.ResolveSwept(context->Contacts[j],old,localTime,h,context->Radius,particle.Phase,ref particle.Position,ref particle.Velocity)
                        && incoming>0.02f && ageBefore>0 && !particle.Redirected)
                    {
                        particle.Redirected=true; context->Redirected++; context->RedirectedId=particle.Id;
                        context->AgeBefore=ageBefore; context->AgeAfter=ageBefore+context->Delta;
                    }
                }
            }
            particle.Age+=context->Delta;
        }
        private bool TryBirth(FirePresentationSnapshot snapshot,out Particle particle)
        {
            particle=default; float total=0;
            for(int i=0;i<snapshot.NodeCount;i++)
            {
                var node=snapshot.Nodes[i]; float r=math.max(node.Radius,0.001f),volume;
                if(node.Shape==FireShape.Shell) {float lo=math.max(r-math.max(node.ShellHalfThickness,0.001f),0),hi=r+math.max(node.ShellHalfThickness,0.001f); volume=(hi*hi*hi-lo*lo*lo)*4f/3;}
                else volume=r*r*math.length(node.B-node.A)+4f/3*r*r*r;
                weights[i]=math.max(volume,1e-8f)*math.saturate(node.Density)*(node.Active?1:0); total+=weights[i];
            }
            if(total<=1e-8f) return false;
            float selection=Random01()*total; int selected=snapshot.NodeCount-1;
            for(int i=0;i<snapshot.NodeCount;i++){selection-=weights[i]; if(selection<0){selected=i;break;}}
            var chosen=snapshot.Nodes[selected]; float radius=math.max(chosen.Radius,0.001f);
            float phi=Random01()*math.PI*2,z=Random01()*2-1,radialRandom=Random01();
            float3 sphere=new float3(math.sqrt(math.max(1-z*z,0))*math.cos(phi),z,math.sqrt(math.max(1-z*z,0))*math.sin(phi));
            float3 position;
            if(chosen.Shape==FireShape.Shell)
            {
                float lo=math.max(radius-math.max(chosen.ShellHalfThickness,0.001f),0),hi=radius+math.max(chosen.ShellHalfThickness,0.001f);
                position=chosen.A+sphere*math.pow(math.lerp(lo*lo*lo,hi*hi*hi,radialRandom),1f/3);
            }
            else
            {
                float3 axis=FireContactMath.SafeNormal(chosen.B-chosen.A,new float3(0,1,0));
                float cylinder=radius*radius*math.length(chosen.B-chosen.A),caps=4f/3*radius*radius*radius;
                if(Random01()*(cylinder+caps)<cylinder)
                {
                    float3 tangent=FireContactMath.Tangent(axis);
                    position=math.lerp(chosen.A,chosen.B,radialRandom)+(tangent*math.cos(phi)+math.cross(axis,tangent)*math.sin(phi))*radius*math.sqrt(Random01());
                }
                else position=(math.dot(sphere,axis)>=0?chosen.B:chosen.A)+sphere*radius*math.pow(radialRandom,1f/3);
            }
            for(int i=0;i<snapshot.ContactCount;i++)
            {
                var patch=snapshot.Contacts[i]; if(!patch.Active) continue;
                float3 q=position-patch.Point; float distance=math.dot(q,patch.Normal); float3 lateral=q-patch.Normal*distance;
                if(math.lengthsq(lateral)<=patch.Radius*patch.Radius && distance<profile.ParticleRadius+patch.Skin && distance>-2*radius) return false;
            }
            float3 velocity;
            Particle birth=default;birth.Position=position;
            fixed(FireFieldNode* nodeData=snapshot.Nodes)
            {
                var context=new StepContext {Particle=&birth,Nodes=nodeData,NodeCount=snapshot.NodeCount,Origin=snapshot.Origin,Time=snapshot.Time};
                compiledBirthVelocity(&context);velocity=birth.Velocity;
            }
            particle=new Particle {Id=++nextId,Position=position,Velocity=velocity,Lifetime=math.lerp(profile.MinLifetime,profile.MaxLifetime,Random01()),
                Phase=Random01()*math.PI*2,Heat=math.lerp(0.75f,1,Random01()),Width=math.lerp(profile.FlameMinWidth,profile.FlameMaxWidth,math.pow(Random01(),1.4f)),Aspect=math.lerp(profile.FlameMinAspect,profile.FlameMaxAspect,Random01())};
            return true;
        }
        [BurstCompile(CompileSynchronously=true)]
        [AOT.MonoPInvokeCallback(typeof(MoveFunction))]
        private static void BirthVelocityCompiled(StepContext* context)
        {
            FireCpuField.Sample(context->Nodes,context->NodeCount,context->Origin,context->Particle->Position,context->Time,out var velocity,out _);
            context->Particle->Velocity=velocity;
        }
        private float Random01()
        { randomState^=randomState<<13; randomState^=randomState>>17; randomState^=randomState<<5; return (randomState>>8)*(1f/16777216f); }
        private void Upload(FirePresentationSnapshot snapshot,UnityEngine.Camera camera)
        {
            int count=AliveCount; renderer.enabled=count>0; if(count==0) return;
            Vector3 eye=camera!=null?camera.transform.position:Vector3.zero,forward=camera!=null?camera.transform.forward:Vector3.forward;
            Vector3 minimum,maximum;
            fixed(Particle* items=particles) fixed(int* sorted=order) fixed(float* distances=depth)
            {
                var context=new UploadContext {Particles=items,Vertices=(Vertex*)NativeArrayUnsafeUtility.GetUnsafePtr(vertices),
                    Order=sorted,Depth=distances,Count=count,Eye=new float3(eye.x,eye.y,eye.z),Forward=new float3(forward.x,forward.y,forward.z)};
                compiledUpload(&context);minimum=Vec(context.Minimum);maximum=Vec(context.Maximum);
            }
            mesh.SetVertexBufferData(vertices,0,0,count*4,0,MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);
            mesh.SetSubMesh(0,new SubMeshDescriptor(0,count*6),MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);
            // Vertices are world points. Renderer.localBounds is computed from all 8 corners,
            // so emitter/owner transforms cannot move or clip the already born tail.
            Vector3 localMin=Vector3.one*float.MaxValue,localMax=Vector3.one*float.MinValue;
            for(int corner=0;corner<8;corner++)
            {
                var p=new Vector3((corner&1)==0?minimum.x:maximum.x,(corner&2)==0?minimum.y:maximum.y,(corner&4)==0?minimum.z:maximum.z);
                p=visual.transform.InverseTransformPoint(p); localMin=Vector3.Min(localMin,p);localMax=Vector3.Max(localMax,p);
            }
            mesh.bounds=new Bounds((localMin+localMax)*0.5f,localMax-localMin); material.SetFloat(TimeId,snapshot.Time);
        }
        [BurstCompile(CompileSynchronously=true)]
        [AOT.MonoPInvokeCallback(typeof(UploadFunction))]
        private static void UploadCompiled(UploadContext* context)
        {
            int count=context->Count;
            for(int i=0;i<count;i++){context->Order[i]=i;context->Depth[i]=math.dot(context->Particles[i].Position-context->Eye,context->Forward);}
            // Iterative heap sort keeps a bounded O(n log n) worst case and needs
            // no recursion, allocation or managed comparer. Farther quads draw first.
            for(int i=count/2-1;i>=0;i--)SiftMin(context->Order,context->Depth,i,count);
            for(int end=count-1;end>0;end--)
            {int first=context->Order[0];context->Order[0]=context->Order[end];context->Order[end]=first;SiftMin(context->Order,context->Depth,0,end);}
            float3 minimum=new float3(float.MaxValue),maximum=new float3(float.MinValue);
            for(int i=0;i<count;i++)
            {
                Particle p=context->Particles[context->Order[i]];
                var vertex=new Vertex {Centre=new Vector3(p.Position.x,p.Position.y,p.Position.z),
                    Direction=new Vector3(p.Velocity.x,p.Velocity.y,p.Velocity.z),
                    Fire=new Vector4(p.Phase,math.saturate(p.Age/p.Lifetime),p.Heat,p.Width),Extent=new Vector2(p.Aspect,0)};
                vertex.UV=new Vector2(0,0);context->Vertices[i*4]=vertex;
                vertex.UV=new Vector2(1,0);context->Vertices[i*4+1]=vertex;
                vertex.UV=new Vector2(1,1);context->Vertices[i*4+2]=vertex;
                vertex.UV=new Vector2(0,1);context->Vertices[i*4+3]=vertex;
                float padding=p.Width*p.Aspect;minimum=math.min(minimum,p.Position-padding);maximum=math.max(maximum,p.Position+padding);
            }
            context->Minimum=minimum;context->Maximum=maximum;
        }
        private static void SiftMin(int* order,float* depth,int root,int count)
        {
            while(root<count/2)
            {
                int child=root*2+1;
                if(child+1<count && depth[order[child+1]]<depth[order[child]])child++;
                if(depth[order[root]]<=depth[order[child]])return;
                int swap=order[root];order[root]=order[child];order[child]=swap;root=child;
            }
        }
        public bool TryTrace(uint id,out FireParticleTrace trace)
        {
            for(int i=0;i<AliveCount;i++) if(particles[i].Id==id) {var p=particles[i];trace=new FireParticleTrace(p.Id,p.Age,Vec(p.Position),Vec(p.Velocity));return true;}
            trace=default;return false;
        }
        public void Clear(){AliveCount=0;birthRemainder=0;if(renderer!=null)renderer.enabled=false;}
        public void Dispose()
        {
            if(disposed)return;disposed=true;if(vertices.IsCreated)vertices.Dispose();
            if(visual!=null)UnityEngine.Object.Destroy(visual);if(mesh!=null)UnityEngine.Object.Destroy(mesh);if(material!=null)UnityEngine.Object.Destroy(material);
        }
        private static Vector3 Vec(float3 value)=>new Vector3(value.x,value.y,value.z);
    }
}




