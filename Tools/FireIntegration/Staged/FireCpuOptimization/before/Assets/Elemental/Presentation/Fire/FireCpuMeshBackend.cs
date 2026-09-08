using System;
using System.Runtime.InteropServices;
using Elemental.Simulation.Fire;
using Unity.Collections;
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
            public int NodeCount,ContactCount,Substeps,Redirected;
            public float3 Origin,FreeUp;
            public float Delta,Offset,FullDelta,Time,FreeLift,FreeDrag,Radius,MaxSpeed,AgeBefore,AgeAfter;
            public uint RedirectedId;
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MoveFunction(StepContext* context);
        private static MoveFunction compiledMove;
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
                    for(int i=AliveCount-1;i>=0;i--)
                    {
                        var particle=particles[i]; Move(ref particle,snapshot,delta,0,delta);
                        if(particle.Age>=particle.Lifetime) { AliveCount--; particles[i]=particles[AliveCount]; }
                        else particles[i]=particle;
                    }
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
            FireCpuField.Sample(snapshot,position,snapshot.Time,out var velocity,out _);
            particle=new Particle {Id=++nextId,Position=position,Velocity=velocity,Lifetime=math.lerp(profile.MinLifetime,profile.MaxLifetime,Random01()),
                Phase=Random01()*math.PI*2,Heat=math.lerp(0.75f,1,Random01()),Width=math.lerp(profile.FlameMinWidth,profile.FlameMaxWidth,math.pow(Random01(),1.4f)),Aspect=math.lerp(profile.FlameMinAspect,profile.FlameMaxAspect,Random01())};
            return true;
        }
        private float Random01()
        { randomState^=randomState<<13; randomState^=randomState>>17; randomState^=randomState<<5; return (randomState>>8)*(1f/16777216f); }
        private void Upload(FirePresentationSnapshot snapshot,UnityEngine.Camera camera)
        {
            int count=AliveCount; renderer.enabled=count>0; if(count==0) return;
            Vector3 eye=camera!=null?camera.transform.position:Vector3.zero,forward=camera!=null?camera.transform.forward:Vector3.forward;
            for(int i=0;i<count;i++){order[i]=i; depth[i]=Vector3.Dot(Vec(particles[i].Position)-eye,forward);}
            Sort(0,count-1);
            Vector3 minimum=Vector3.one*float.MaxValue,maximum=Vector3.one*float.MinValue;
            for(int i=0;i<count;i++)
            {
                var p=particles[order[i]]; var centre=Vec(p.Position); var fire=new Vector4(p.Phase,math.saturate(p.Age/p.Lifetime),p.Heat,p.Width);
                var extent=new Vector2(p.Aspect,0);
                for(int q=0;q<4;q++) vertices[i*4+q]=new Vertex {Centre=centre,UV=new Vector2(q==1||q==2?1:0,q>=2?1:0),Fire=fire,Extent=extent,Direction=Vec(p.Velocity)};
                float padding=p.Width*p.Aspect; minimum=Vector3.Min(minimum,centre-Vector3.one*padding); maximum=Vector3.Max(maximum,centre+Vector3.one*padding);
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
        private void Sort(int left,int right)
        {
            int i=left,j=right; float pivot=depth[order[(left+right)/2]];
            while(i<=j){while(depth[order[i]]>pivot)i++;while(depth[order[j]]<pivot)j--;if(i<=j){int tmp=order[i];order[i]=order[j];order[j]=tmp;i++;j--;}}
            if(left<j)Sort(left,j);if(i<right)Sort(i,right);
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




