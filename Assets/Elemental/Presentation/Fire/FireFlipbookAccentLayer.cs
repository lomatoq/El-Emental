using System;
using Elemental.Simulation.Fire;
using UnityEngine;
using UnityEngine.Rendering;
namespace Elemental.Presentation.Fire
{
    // Small atlas accent over the existing physical volume, never a gameplay emitter.
    public sealed class FireFlipbookAccentLayer:IDisposable
    {
        public const int MaximumCards=24;
        private readonly SubMeshDescriptor[] submeshes=new SubMeshDescriptor[2];
        private readonly GameObject visual;private readonly Mesh mesh;private readonly Material material;private readonly MeshRenderer renderer;
        private readonly Vector3[] positions=new Vector3[MaximumCards*4];private readonly Vector4[] data=new Vector4[MaximumCards*4],planeA=new Vector4[MaximumCards*4],planeB=new Vector4[MaximumCards*4];
        private readonly Vector2[] uv=new Vector2[MaximumCards*4];private readonly int[] indices=new int[MaximumCards*6];private readonly int[] selected=new int[MaximumCards];
        private readonly Vector4[] billboardCenter=new Vector4[MaximumCards*4],billboardAxis=new Vector4[MaximumCards*4];
        private readonly int[] hotSelectedBand=new int[MaximumCards];
        private readonly uint[] retainedHotIds=new uint[18];private readonly float[] hotAcquiredAge=new float[18],smokeAcquiredAge=new float[MaximumCards];
        private readonly uint[] retainedSmokeIds=new uint[MaximumCards];
        public float SmokeSpanWorld {get;private set;}
        private float hotStartAge=.08f,hotScale=1,smokeScale=1;private int hotBudget=12,smokeBudget=12;
        public void SetDetailBudget(int hot,int smoke,float hotSize,float smokeSize)
        {hotBudget=Mathf.Clamp(hot,1,18);smokeBudget=Mathf.Clamp(Mathf.Max(8,smoke),1,MaximumCards-hotBudget);hotScale=Mathf.Clamp(hotSize,.5f,1);smokeScale=Mathf.Clamp(smokeSize,.5f,1);}
        public void SetHotStartAge(float age)=>hotStartAge=Mathf.Clamp(age,.08f,.35f);
        public int ActiveCards {get;private set;}
        // Enabled submitted geometry; a native image comparison is still needed for pixel visibility.
        public int VisibleCards=>renderer.enabled?ActiveCards:0;
        public int SmokeCards {get;private set;}
        public int HotCards=>ActiveCards-SmokeCards;
        public FireFlipbookAccentLayer(Transform owner)
        {
            material=Resources.Load<Material>("FireFlipbookAccentMaterial");
            if(material==null||material.shader==null||!material.shader.isSupported||material.GetTexture("_FlameAtlas")==null||material.GetTexture("_SmokeAtlas")==null)
                throw new InvalidOperationException("Fire flipbook accents require their shared URP material and two RGBA atlases.");
            visual=new GameObject("Transported flame and smoke atlas accents");visual.transform.SetParent(owner,false);
            renderer=visual.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{material,Resources.Load<Material>("FireSmokeDustMaterial")};renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.enabled=false;
            mesh=new Mesh{name="Twenty-four bounded transported atlas cards"};mesh.MarkDynamic();visual.AddComponent<MeshFilter>().sharedMesh=mesh;
            for(int card=0;card<MaximumCards;card++)
            {
                int v=card*4,t=card*6;uv[v]=new Vector2(0,0);uv[v+1]=new Vector2(1,0);uv[v+2]=new Vector2(1,1);uv[v+3]=new Vector2(0,1);
                indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v;indices[t+4]=v+2;indices[t+5]=v+3;
            }
            mesh.vertices=positions;mesh.uv=uv;mesh.triangles=indices;mesh.SetSubMeshes(submeshes,0,2,MeshUpdateFlags.DontRecalculateBounds);
        }
        public void Step(FireFlowParticleSolver solver,UnityEngine.Camera camera)
        {
            ActiveCards=SmokeCards=0;if(camera==null||solver.Count==0){Clear();return;}
            for(int band=0;band<hotBudget;band++)
            {
                float target=.07f+band*(.814f/Mathf.Max(1,hotBudget-1)),best=float.MaxValue;int index=-1;
                for(int i=0;i<solver.Count;i++)
                {
                    var p=solver.Particles[i];float age=p.Age/p.HotLifetime;
                    if(p.Spark||p.Temperature<.45f||age<hotStartAge||age>.92f)continue;
                    bool used=false;for(int j=0;j<ActiveCards;j++)if(selected[j]==i)used=true;if(used)continue;
                    if(retainedHotIds[band]==p.Id){index=i;break;}
                    bool reserved=false;for(int j=0;j<hotBudget;j++)if(j!=band&&retainedHotIds[j]==p.Id){reserved=true;break;}if(reserved)continue;
                    float score=Mathf.Abs(age-target);if(score<best){best=score;index=i;}
                }
                if(index>=0){uint id=solver.Particles[index].Id;if(retainedHotIds[band]!=id)hotAcquiredAge[band]=solver.Particles[index].Age;
                    retainedHotIds[band]=id;hotSelectedBand[ActiveCards]=band;selected[ActiveCards++]=index;}else retainedHotIds[band]=0;
            }
            int firstSmoke=ActiveCards;
            for(int band=0;band<smokeBudget;band++)
            {
                int smokeIndex=-1;float best=-1;
                for(int i=0;i<solver.Count;i++)
                {
                    var p=solver.Particles[i];if(p.Spark||p.Temperature>.55f||p.Soot<.025f)continue;
                    bool used=false;for(int j=0;j<ActiveCards;j++)if(selected[j]==i)used=true;if(used)continue;
                    if(retainedSmokeIds[band]==p.Id){smokeIndex=i;break;}
                    bool reserved=false;for(int j=0;j<smokeBudget;j++)if(j!=band&&retainedSmokeIds[j]==p.Id){reserved=true;break;}if(reserved)continue;
                    float age=Mathf.InverseLerp(p.HotLifetime,p.Lifetime,p.Age),weight=p.Soot*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1,age)));
                    float spread=1;
                    if(ActiveCards>firstSmoke)
                    {
                        spread=float.MaxValue;
                        for(int j=firstSmoke;j<ActiveCards;j++)
                        {
                            var other=solver.Particles[selected[j]];float distance=((Vector3)p.Position-(Vector3)other.Position).sqrMagnitude;
                            spread=Mathf.Min(spread,distance/Mathf.Max(.01f,(p.Radius+other.Radius)*(p.Radius+other.Radius)));
                        }
                        if(spread<.01f)continue;
                    }
                    float score=spread*weight;if(score>best){best=score;smokeIndex=i;}
                }
                uint smokeId=smokeIndex>=0?solver.Particles[smokeIndex].Id:0;
                if(smokeId!=retainedSmokeIds[band]&&smokeIndex>=0)smokeAcquiredAge[band]=solver.Particles[smokeIndex].Age;
                retainedSmokeIds[band]=smokeId;
                if(smokeIndex>=0){selected[ActiveCards++]=smokeIndex;SmokeCards++;}
            }
            SmokeSpanWorld=0;
            if(SmokeCards>1)
            {
                Vector3 smokeMin=solver.Particles[selected[firstSmoke]].Position,smokeMax=smokeMin;
                for(int i=firstSmoke+1;i<ActiveCards;i++){Vector3 p=solver.Particles[selected[i]].Position;smokeMin=Vector3.Min(smokeMin,p);smokeMax=Vector3.Max(smokeMax,p);}
                SmokeSpanWorld=(smokeMax-smokeMin).magnitude;
            }
            if(ActiveCards==0){Clear();return;}
            Vector3 minimum=Vector3.one*float.PositiveInfinity,maximum=Vector3.one*float.NegativeInfinity;
            for(int card=0;card<MaximumCards;card++)
            {
                bool active=card<ActiveCards;var p=solver.Particles[selected[active?card:0]];bool cooling=active&&card>=firstSmoke;
                var evolution=FireTongueEvolution.Evaluate(p.Id,p.Phase,p.Age,p.HotLifetime,p.Temperature);
                Vector3 center=p.Position,facing=(camera.transform.position-center).normalized;
                Vector3 velocity=((Vector3)p.Velocity).normalized;
                Vector3 axis=Vector3.Lerp((Vector3)p.Up,velocity,cooling?.6f:Mathf.Lerp(.5f,.9f,Mathf.Repeat(p.Phase*.731f,1))).normalized;
                Vector3 tangent=Vector3.Cross(axis,Vector3.right);if(tangent.sqrMagnitude<.01f)tangent=Vector3.Cross(axis,Vector3.up);tangent.Normalize();
                axis=(axis+tangent*evolution.Bend.x+Vector3.Cross(axis,tangent)*evolution.Bend.y).normalized;
                Vector3 up=Vector3.ProjectOnPlane(axis,facing).normalized;if(up.sqrMagnitude<.1f)up=camera.transform.up;
                Vector3 right=Vector3.Cross(up,facing).normalized;float angle=evolution.Roll*.4f;
                Vector3 rotated=right*Mathf.Cos(angle)+up*Mathf.Sin(angle);up=up*Mathf.Cos(angle)-right*Mathf.Sin(angle);right=rotated;
                float radius=Mathf.Clamp(p.Radius*(cooling?1.65f:2.4f),cooling?.16f:.14f,cooling?.55f:.85f)*(cooling?smokeScale*evolution.SmokeScale:hotScale*evolution.RadiusScale);
                float age=cooling?(p.Lifetime>p.HotLifetime?Mathf.InverseLerp(p.HotLifetime,p.Lifetime,p.Age):Mathf.Clamp01(1-p.Temperature)):Mathf.Clamp01(p.Age/p.HotLifetime);
                if(cooling)age=.25f+age*.72f;
                float birth=Mathf.SmoothStep(0,1,Mathf.InverseLerp(hotStartAge,hotStartAge+.09f,p.Age/p.HotLifetime));
                birth*=Mathf.SmoothStep(0,1,Mathf.Clamp01((p.Age-hotAcquiredAge[hotSelectedBand[Mathf.Min(card,MaximumCards-1)]])/.09f));
                float smokeBirth=cooling?Mathf.SmoothStep(0,1,Mathf.Clamp01((p.Age-smokeAcquiredAge[card-firstSmoke])/.12f)):1;
                float opacity=active?(cooling?.68f*smokeBirth*Mathf.Sin(Mathf.Clamp01((age-.2f)/.8f)*Mathf.PI):.24f*birth*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,age)))):0;
                for(int k=0;k<4;k++)
                {
                    int v=card*4+k;Vector2 q=uv[v]*2-Vector2.one;
                    Vector3 world=center+right*q.x*radius+up*q.y*radius*(cooling?Mathf.Lerp(.9f,1.4f,Mathf.Repeat(p.Phase,1)):1.45f*evolution.AspectScale);
                    billboardCenter[v]=new Vector4(center.x,center.y,center.z,radius);billboardAxis[v]=new Vector4(axis.x,axis.y,axis.z,radius*(cooling?Mathf.Lerp(.9f,1.4f,Mathf.Repeat(p.Phase,1)):1.45f*evolution.AspectScale));
                    positions[v]=visual.transform.InverseTransformPoint(world);data[v]=new Vector4(age,cooling?1:0,p.Phase,opacity);planeA[v]=p.ClipPlaneA;planeB[v]=p.ClipPlaneB;
                    if(active){minimum=Vector3.Min(minimum,positions[v]);maximum=Vector3.Max(maximum,positions[v]);}
                }
            }
            mesh.SetVertices(positions);mesh.SetUVs(1,data);mesh.SetUVs(2,planeA);mesh.SetUVs(3,planeB);mesh.SetUVs(4,billboardCenter);mesh.SetUVs(5,billboardAxis);submeshes[0]=new SubMeshDescriptor(0,firstSmoke*6);submeshes[1]=new SubMeshDescriptor(firstSmoke*6,SmokeCards*6);mesh.SetSubMeshes(submeshes,0,2,MeshUpdateFlags.DontRecalculateBounds);mesh.bounds=new Bounds((minimum+maximum)*.5f,maximum-minimum+Vector3.one*4);
            renderer.enabled=true;
        }
        public void SetRenderingLayerForQa(int layer)=>visual.layer=Mathf.Clamp(layer,0,31);
        public void Clear(){ActiveCards=SmokeCards=0;SmokeSpanWorld=0;Array.Clear(retainedSmokeIds,0,retainedSmokeIds.Length);Array.Clear(retainedHotIds,0,retainedHotIds.Length);renderer.enabled=false;}
        public void Dispose(){UnityEngine.Object.Destroy(visual);UnityEngine.Object.Destroy(mesh);}
    }
}
