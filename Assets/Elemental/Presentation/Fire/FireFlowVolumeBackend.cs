using System;
using System.Runtime.InteropServices;
using Elemental.Runtime.Fire;
using Elemental.Simulation.Fire;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.Fire
{
    // Real transported positions drive each raymarched parcel. There is no axial capsule in this renderer.
    public sealed class FireFlowVolumeBackend : IDisposable
    {
        private static readonly ProfilerMarker Marker=new ProfilerMarker("Fire.Flow.StepAndUpload");
        [StructLayout(LayoutKind.Sequential)]
        private struct Vertex
        {
            public Vector3 Position;
            public Vector4 CentreRadius;
            public Vector4 DirectionAge;
            public Vector4 UpPhase;
            public Vector4 ClipA,ClipB;
            public Vector4 Kind;
            public Vector3 Thermal;
        }
        public readonly FireFlowParticleSolver Solver;
        public readonly FireFlowCollisionAdapter Collision;
        private readonly GameObject visual;
        private readonly FireFlipbookAccentLayer accents;
        private bool accentRendering=true;private float tailVolumeScale=1;
        public int AccentCards=>accents.VisibleCards;
        public int AccentSmokeCards=>accents.SmokeCards;
        public float AccentSmokeSpan=>accents.SmokeSpanWorld;
        public void SetAccentRenderingForQa(bool enabled){accentRendering=enabled;if(!enabled)accents.Clear();}
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer,smokeRenderer;
        private readonly Material material,smokeMaterial;
        private NativeArray<Vertex> vertices;
        private readonly int[] order;
        private readonly float[] depth;
        private readonly Color defaultCore;private readonly float defaultCoreGlow;
        private bool disposed,countedVisible;
        private static int visibleGroups;
        public static bool HasVisibleGroups=>visibleGroups>0;
        public bool Visible=>renderer!=null&&renderer.enabled;
        public Bounds WorldBounds {get;private set;}
        public double LastStepMilliseconds {get;private set;}
        public int ProxyTriangles=>Solver.Count*12;
        public FireFlowVolumeBackend(Transform owner,Shader shader,int mask,Texture authoredAtlas=null,bool stationary=false,int particleCapacity=0)
        {
            if(shader==null||!shader.isSupported)throw new NotSupportedException("Fire flow requires supported Resources/FireFlowParcel shader.");
            Solver=new FireFlowParticleSolver(stationary,particleCapacity);order=new int[Solver.ParticleLimit];depth=new float[Solver.ParticleLimit];
            Collision=new FireFlowCollisionAdapter(mask);
            material=new Material(shader){name="Fire transported hot gas volume"};
            defaultCore=material.GetColor("_Core");defaultCoreGlow=material.GetFloat("_CoreGlowScale");
            material.SetFloat("_TailRefinement",stationary?0:1);
            if(authoredAtlas!=null){material.SetTexture("_FlameAtlas",authoredAtlas);material.SetFloat("_AuthoredShapeMix",.68f);}
            visual=new GameObject("Fire collision-driven flow");visual.transform.SetParent(owner,false);
            renderer=visual.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.enabled=false;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            // Same collision-driven mesh, separate ordinary alpha-blended cooling phase.
            // Smoke is not additive and cannot accumulate into another white fire layer.
            smokeMaterial=new Material(material){name="Fire transported cooling smoke",renderQueue=2997};
            smokeMaterial.SetFloat("_SmokeMode",1);smokeMaterial.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);smokeMaterial.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
            var smoke=new GameObject("Fire cooling smoke");smoke.transform.SetParent(visual.transform,false);
            smokeRenderer=smoke.AddComponent<MeshRenderer>();smokeRenderer.sharedMaterial=smokeMaterial;smokeRenderer.enabled=false;
            smokeRenderer.shadowCastingMode=ShadowCastingMode.Off;smokeRenderer.receiveShadows=false;
            smokeRenderer.lightProbeUsage=LightProbeUsage.Off;smokeRenderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            mesh=new Mesh{name="Fire transported parcel boxes"};mesh.MarkDynamic();visual.AddComponent<MeshFilter>().sharedMesh=mesh;smokeRenderer.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            int count=Solver.ParticleLimit;
            vertices=new NativeArray<Vertex>(count*8,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
            mesh.SetVertexBufferParams(count*8,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float32,4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord3,VertexAttributeFormat.Float32,4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord4,VertexAttributeFormat.Float32,4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord5,VertexAttributeFormat.Float32,4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord6,VertexAttributeFormat.Float32,3));
            int[] faces={0,2,1,1,2,3,4,5,6,5,7,6,0,4,2,2,4,6,1,3,5,3,7,5,0,1,4,1,5,4,2,6,3,3,6,7};
            var indices=new ushort[count*36];for(int i=0;i<count;i++)for(int j=0;j<36;j++)indices[i*36+j]=(ushort)(i*8+faces[j]);
            mesh.SetIndexBufferParams(indices.Length,IndexFormat.UInt16);mesh.SetIndexBufferData(indices,0,0,indices.Length);
            mesh.subMeshCount=1;mesh.SetSubMesh(0,new SubMeshDescriptor(0,0));
            accents=new FireFlipbookAccentLayer(visual.transform);
        }
        public void Begin(uint seed)=>Clear(seed);
        public void SetRenderingLayerForQa(int layer){visual.layer=Mathf.Clamp(layer,0,31);smokeRenderer.gameObject.layer=visual.layer;accents.SetRenderingLayerForQa(visual.layer);}
        public void SetTailAgeScale(float scale)
        {material.SetFloat("_TailAgeScale",Mathf.Clamp(scale,.5f,1));smokeMaterial.SetFloat("_TailAgeScale",Mathf.Clamp(scale,.5f,1));}
        public void SetDebugViewForQa(int mode)=>material.SetInt("_DebugView",Mathf.Clamp(mode,0,3));
        public void Step(FirePresentationSnapshot snapshot,float delta,UnityEngine.Camera camera)
        {
            using var marker=Marker.Auto();
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            if(disposed)throw new ObjectDisposedException(nameof(FireFlowVolumeBackend));
            if(snapshot.Lifecycle==FireLifecycle.Retired){Clear();return;}
            FireFieldNode node=default;bool found=false;
            for(int i=0;i<snapshot.NodeCount;i++)if(snapshot.Nodes[i].Active){if(found||snapshot.Nodes[i].Shape!=FireShape.Capsule)throw new NotSupportedException("Fluid demonstration accepts one stream capsule only.");node=snapshot.Nodes[i];found=true;}
            float3 direction=found?math.normalizesafe(node.Flow,math.normalizesafe(node.B-node.A,new float3(0,0,1))):new float3(0,0,1);
            // Runtime radius is .18*sqrt(power); default.18 reproduces the accepted hand.
            if(found&&!Solver.Injection.Enabled&&!Solver.Stationary&&node.Radius<=.3f)
            {
                float target=(node.Radius/.18f)*(node.Radius/.18f);
                float visualPower=Mathf.Lerp(Solver.HandPower,target,1-Mathf.Exp(-Mathf.Max(0,delta)/.12f));
                ConfigureStreamPower(Mathf.InverseLerp(.38f,2.5f,visualPower));
            }
            Solver.Step(delta,snapshot.Emits&&found,snapshot.Energy,found?node.A:snapshot.Origin,direction,found?node.Up:snapshot.FreeUp,Collision);
            Upload(camera);
            if(accentRendering)accents.Step(Solver,camera);else accents.Clear();
            LastStepMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        private void Upload(UnityEngine.Camera camera)
        {
            int count=Solver.Count;
            if(count==0){SetVisible(false);return;}
            float3 eye=camera!=null?(float3)camera.transform.position:float3.zero;
            float3 forward=camera!=null?(float3)camera.transform.forward:new float3(0,0,1);
            for(int i=0;i<count;i++)
            {
                float d=math.dot(Solver.Particles[i].Position-eye,forward);int j=i;
                while(j>0&&depth[j-1]<d){depth[j]=depth[j-1];order[j]=order[j-1];j--;}
                depth[j]=d;order[j]=i;
            }
            float3 min=new float3(float.PositiveInfinity),max=new float3(float.NegativeInfinity);
            for(int i=0;i<count;i++)
            {
                var p=Solver.Particles[order[i]];
                var evolution=FireTongueEvolution.Evaluate(p.Id,p.Phase,p.Age,p.HotLifetime,p.Temperature);
                float3 axis=math.normalizesafe(p.Velocity,new float3(0,0,1));
                float3 lateral=FireContactMath.Tangent(axis);
                if(!p.Spark)axis=math.normalizesafe(axis+lateral*evolution.Bend.x+math.cross(axis,lateral)*evolution.Bend.y,axis);
                float3 side=math.normalizesafe(math.cross(p.Up,axis),FireContactMath.Tangent(axis));
                float3 up=math.cross(axis,side);
                float spin=p.Spark?p.Phase+p.Age*p.Spin:evolution.Roll;float3 rotatedSide=side*math.cos(spin)+up*math.sin(spin);up=up*math.cos(spin)-side*math.sin(spin);side=rotatedSide;
                float aspect=p.Spark?p.Aspect:Mathf.Clamp(p.Aspect*evolution.AspectScale,.75f,2.2f);
                float radius=p.Radius*(p.Spark?1:evolution.RadiusScale);if(!p.Spark&&p.Age<p.HotLifetime)radius*=math.lerp(1,tailVolumeScale,math.smoothstep(.1f,.72f,p.Age/p.HotLifetime));
                // Elongated in physical travel direction; it never rotates to face the camera.
                for(int k=0;k<8;k++)
                {
                    float3 pos=p.Position+radius*1.3f*(side*((k&1)==0?-1:1)+up*((k&2)==0?-1:1)+axis*((k&4)==0?-aspect:aspect));
                    vertices[i*8+k]=new Vertex{Position=pos,CentreRadius=new Vector4(p.Position.x,p.Position.y,p.Position.z,radius),
                        DirectionAge=new Vector4(axis.x,axis.y,axis.z,math.max(p.Age/p.Lifetime,math.smoothstep(Solver.PathLimit*.75f,Solver.PathLimit,p.Distance))),UpPhase=new Vector4(up.x,up.y,up.z,p.Phase),ClipA=p.ClipPlaneA,ClipB=p.ClipPlaneB,Kind=new Vector4(p.Spark?1:0,p.Size,p.Age,aspect),Thermal=new Vector3(p.Temperature*(p.Spark?1:evolution.TemperatureScale),p.Soot,math.max(p.Age/p.HotLifetime,math.smoothstep(Solver.PathLimit*.75f,Solver.PathLimit,p.Distance)))};
                    min=math.min(min,pos);max=math.max(max,pos);
                }
            }
            mesh.SetVertexBufferData(vertices,0,0,count*8,0,MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);
            mesh.SetSubMesh(0,new SubMeshDescriptor(0,count*36),MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);
            WorldBounds=new Bounds((min+max)*.5f,max-min);mesh.bounds=WorldBounds;renderer.bounds=WorldBounds;smokeRenderer.bounds=WorldBounds;SetVisible(true);
        }
        // Compatibility name: sample actual hot gas centers for volumetric light,
        // without ground projection or additional physics queries.
        public void ConfigureFootStream()
        {
            tailVolumeScale=.58f;accents.SetHotStartAge(.22f);
            // Same hand shader, atlas, orange body and cooling edge. Only the
            // youngest nozzle core is brighter/whiter, as requested for foot lift.
            material.SetColor("_Core",new Color(7,6.7f,5.8f,1));
            material.SetFloat("_CoreGlowScale",1);
            material.SetFloat("_SmokeOpacity",.40f);smokeMaterial.SetFloat("_SmokeOpacity",.40f);
        }
        public void ConfigureDetailedAbilityFlame(Texture texture)
        {
            if(texture==null)throw new ArgumentNullException(nameof(texture));
            tailVolumeScale=.28f;
            material.SetTexture("_DetailTex",texture);smokeMaterial.SetTexture("_DetailTex",texture);
            material.SetFloat("_DetailMix",1);smokeMaterial.SetFloat("_DetailMix",1);
            material.SetFloat("_AuthoredShapeMix",1);smokeMaterial.SetFloat("_AuthoredShapeMix",1);
            // Remove the unmasked round pedestal underneath detailed atlas tongues.
            material.SetFloat("_ShapeFloor",.015f);smokeMaterial.SetFloat("_ShapeFloor",.015f);
            material.SetFloat("_SmokeOpacity",.40f);smokeMaterial.SetFloat("_SmokeOpacity",.40f);
        }
        public void ConfigureStreamPower(float normalized)
        {
            float power=Mathf.Lerp(.38f,2.5f,Mathf.Clamp01(normalized));Solver.ConfigureHandPower(power);
            material.SetFloat("_WhiteSource",1);
            float strong=Mathf.InverseLerp(1,2.5f,power),weak=Mathf.InverseLerp(1,.38f,power);
            material.SetFloat("_BlueSource",weak);
            Color core=Color.Lerp(defaultCore,new Color(7,6,4.5f,1),strong);
            core=Color.Lerp(core,new Color(3,1.2f,.16f,1),weak*.65f);
            material.SetColor("_Core",core);material.SetFloat("_CoreGlowScale",Mathf.Lerp(defaultCoreGlow,1,strong));
        }
        public void ConfigureRingMediumDetail(bool enabled){material.SetFloat("_MediumAuthoredShape",enabled?1:0);smokeMaterial.SetFloat("_MediumAuthoredShape",enabled?1:0);}
        public void ConfigureAbilitySupport(float scale)=>tailVolumeScale=Mathf.Clamp(scale,.28f,.9f);
        public void ConfigureChargeAccentDetail(int tongues)=>accents.SetDetailBudget(Mathf.Clamp(tongues,6,18),6,.82f,.88f);
        public void ConfigureSphereAccentDetail()=>accents.SetDetailBudget(18,6,.9f,.72f);
        public void ConfigureSurfaceAccentDetail(bool fine)=>accents.SetDetailBudget(fine?18:12,fine?6:10,fine?.65f:1,fine?.72f:.8f);
        public void ConfigureLeadingBolt(float power)
        {
            accents.SetHotStartAge(FireBoltTailProfile.DetailStartAge);accents.SetDetailBudget(16,8,.86f,1);
            // Emission only: the independent light owner keeps its existing weak lamp budget.
            material.SetColor("_Core",Color.Lerp(new Color(6,5.7f,4.8f,1),new Color(8,7.6f,6.8f,1),Mathf.Clamp01(power)));
            material.SetFloat("_CoreGlowScale",1);tailVolumeScale=.86f;
        }
        public void ConfigureCoherentTower()
        {
            // A coherent young volume connects the nozzle, with atlas-defined aging tongues.
            tailVolumeScale=.82f;material.SetFloat("_CoreGlowScale",1f);material.SetFloat("_SourceRadianceCap",1.3f);
            material.SetFloat("_WhitePhaseEnd",.38f);material.SetFloat("_WhiteCapEnd",.45f);
            material.SetFloat("_AuthoredShapeMix",.68f);smokeMaterial.SetFloat("_AuthoredShapeMix",.68f);material.SetFloat("_WhiteSource",1);
            material.SetColor("_Core",new Color(6,5.6f,4.6f,1));
        }
        public int SampleGroundLights(out Vector3 first,out Vector3 second,out Vector3 third)
        {
            first=second=third=default;float far=0;
            for(int i=0;i<Solver.Count;i++)if(!Solver.Particles[i].Spark&&Solver.Particles[i].Heat>.45f&&Solver.Particles[i].Age<Solver.Particles[i].HotLifetime)far=Mathf.Max(far,Solver.Particles[i].Distance);
            if(far<=0)return 0;
            int count=0;Vector3 previous=default;
            for(int sample=0;sample<3;sample++)
            {
                float target=far*(sample==0?.12f:sample==1?.5f:.88f);int selected=-1;float best=float.PositiveInfinity;
                for(int i=0;i<Solver.Count;i++)
                {
                    var p=Solver.Particles[i];if(p.Spark||p.Heat<=.45f||p.Age>=p.HotLifetime)continue;
                    float score=Mathf.Abs(p.Distance-target);if(score<best){best=score;selected=i;}
                }
                if(selected<0)continue;var chosen=Solver.Particles[selected];
                Vector3 position=chosen.Position;
                if(count>0&&Vector3.Distance(previous,position)<.6f)continue;
                if(count==0)first=position;else if(count==1)second=position;else third=position;
                previous=position;count++;
            }
            return count;
        }
        private void SetVisible(bool value)
        {
            if(value!=countedVisible){visibleGroups=Mathf.Max(0,visibleGroups+(value?1:-1));countedVisible=value;}
            if(renderer!=null)renderer.enabled=value;
            if(smokeRenderer!=null)smokeRenderer.enabled=value;
        }
        public void Clear(uint seed=1){Solver.Clear(seed);accents.Clear();SetVisible(false);}
        public void Dispose(){if(disposed)return;disposed=true;Clear();accents.Dispose();Collision.Dispose();if(vertices.IsCreated)vertices.Dispose();UnityEngine.Object.Destroy(visual);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(material);UnityEngine.Object.Destroy(smokeMaterial);}
    }
}
