using System;
using Elemental.Runtime.Fire;
using Elemental.Simulation.Fire;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;
namespace Elemental.Presentation.Fire
{
    // One draw for all existing projectile slots; gas transport remains the source of shape.
    public sealed class FireBoltTrailRenderer:IDisposable
    {
        private const int Groups=FireAbilityTuning.MaximumProjectiles,Capacity=Groups*FireBoltTrailMath.MaximumSpans;
        private static readonly ProfilerMarker Marker=new("Fire.ContinuousBoltTrail");
        private readonly FireFlowCollisionAdapter collision;private readonly GameObject host;private readonly Mesh mesh;private readonly MeshRenderer renderer;private readonly Material material;
        private readonly Vector3[] positions=new Vector3[Groups*8];
        private readonly Vector4[] centers=new Vector4[Groups*8],boundsData=new Vector4[Groups*8],groupData=new Vector4[Groups*8];
        private readonly Vector4[] spanA=new Vector4[Capacity],spanB=new Vector4[Capacity],spanAge=new Vector4[Capacity];
        private int groups;
        private readonly FireBoltTrailSpan[] spans=new FireBoltTrailSpan[FireBoltTrailMath.MaximumSpans];
        private Vector3 minimum,maximum;private bool disposed;
        public int VisibleSpans {get;private set;}public int Queries {get;private set;}public int Saturations {get;private set;}public double LastStepMilliseconds {get;private set;}
        public FireBoltTrailRenderer(Transform owner,Transform actor,int mask)
        {
            var shader=Resources.Load<Shader>("FireBoltTrail");if(shader==null||!shader.isSupported)throw new NotSupportedException("Continuous bolt tail requires Resources/FireBoltTrail shader.");
            collision=new FireFlowCollisionAdapter(mask,actor);material=new Material(shader){name="Dense transported projectile flame tail"};
            host=new GameObject("Swept continuous fireball tails");host.transform.SetParent(owner,false);renderer=host.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.enabled=false;
            mesh=new Mesh{name="Forty-eight validated hot flame tail spans"};mesh.MarkDynamic();mesh.vertices=positions;
            int[] faces={0,2,1,1,2,3,4,5,6,5,7,6,0,4,2,2,4,6,1,3,5,3,7,5,0,1,4,1,5,4,2,6,3,3,6,7};var triangles=new int[Groups*36];
            for(int i=0;i<Groups;i++)for(int j=0;j<36;j++)triangles[i*36+j]=i*8+faces[j];mesh.triangles=triangles;host.AddComponent<MeshFilter>().sharedMesh=mesh;
        }
        public void BeginFrame(){groups=0;VisibleSpans=Queries=Saturations=0;LastStepMilliseconds=0;minimum=Vector3.one*float.PositiveInfinity;maximum=Vector3.one*float.NegativeInfinity;}
        public void Append(bool emitting,Vector3 head,float radius,Vector3 up,uint id,FireFlowParticleSolver solver)
        {
            using var marker=Marker.Auto();long start=System.Diagnostics.Stopwatch.GetTimestamp();int saturation=collision.Saturations,recovery=collision.MeshRecoveryRays;
            int count=FireBoltTrailMath.Build(emitting,head,radius,solver.Particles,solver.Count,collision,spans,out int queries);
            Queries+=queries+collision.MeshRecoveryRays-recovery;Saturations+=collision.Saturations-saturation;
            if(count>0&&groups<Groups)
            {
                Vector3 groupMin=Vector3.one*float.PositiveInfinity,groupMax=Vector3.one*float.NegativeInfinity;
                for(int i=0;i<count;i++)
                {
                    var span=spans[i];int index=groups*FireBoltTrailMath.MaximumSpans+i;
                    spanA[index]=new Vector4(span.A.x,span.A.y,span.A.z,span.RadiusA);spanB[index]=new Vector4(span.B.x,span.B.y,span.B.z,span.RadiusB);
                    spanAge[index]=new Vector4(span.AgeA,span.AgeB,0,0);
                    float radiusBound=Mathf.Max(span.RadiusA,span.RadiusB);
                    groupMin=Vector3.Min(groupMin,Vector3.Min(span.A,span.B)-Vector3.one*radiusBound);groupMax=Vector3.Max(groupMax,Vector3.Max(span.A,span.B)+Vector3.one*radiusBound);
                }
                Vector3 mid=(groupMin+groupMax)*.5f,half=(groupMax-groupMin)*.5f;
                for(int k=0;k<8;k++)
                {
                    int v=groups*8+k;positions[v]=mid+new Vector3((k&1)==0?-half.x:half.x,(k&2)==0?-half.y:half.y,(k&4)==0?-half.z:half.z);
                    centers[v]=new Vector4(mid.x,mid.y,mid.z,0);boundsData[v]=new Vector4(half.x,half.y,half.z,groups*FireBoltTrailMath.MaximumSpans);groupData[v]=new Vector4(count,id*.731f,0,0);
                }
                minimum=Vector3.Min(minimum,groupMin);maximum=Vector3.Max(maximum,groupMax);VisibleSpans+=count;groups++;
            }
            LastStepMilliseconds+=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency;
        }
        public void EndFrame(float time)
        {
            using var marker=Marker.Auto();long start=System.Diagnostics.Stopwatch.GetTimestamp();
            if(VisibleSpans==0){renderer.enabled=false;return;}
            mesh.SetVertices(positions);mesh.SetUVs(0,centers);mesh.SetUVs(1,boundsData);mesh.SetUVs(2,groupData);
            material.SetVectorArray("_SpanA",spanA);material.SetVectorArray("_SpanB",spanB);material.SetVectorArray("_SpanAge",spanAge);
            mesh.SetSubMesh(0,new SubMeshDescriptor(0,groups*36),MeshUpdateFlags.DontRecalculateBounds);
            var bounds=new Bounds((minimum+maximum)*.5f,maximum-minimum);mesh.bounds=bounds;renderer.bounds=bounds;material.SetFloat("_FlowTime",time);renderer.enabled=true;
            LastStepMilliseconds+=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency;
        }
        public void Clear(){VisibleSpans=Queries=Saturations=0;LastStepMilliseconds=0;renderer.enabled=false;}
        public void Dispose(){if(disposed)return;disposed=true;collision.Dispose();UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(material);}
    }
}
