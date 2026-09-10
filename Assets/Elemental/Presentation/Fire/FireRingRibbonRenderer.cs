using System;
using Elemental.Runtime.Fire;
using Elemental.Simulation.Fire;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;
namespace Elemental.Presentation.Fire
{
 // One batched bridge of current, fully swept support spans. It never drives damage.
 public sealed class FireRingRibbonRenderer:IDisposable
 {
  private static int visibleGroups;public static bool HasVisibleGroups=>visibleGroups>0;private bool counted,disposed;
  private static readonly ProfilerMarker Marker=new("Fire.SupportedRingRibbon");
  private readonly FireFlowCollisionAdapter collision;private readonly GameObject host;private readonly Mesh mesh;private readonly MeshRenderer renderer;private readonly Material material;
  private readonly Vector3[] positions=new Vector3[192];private readonly Vector4[] centers=new Vector4[192],axes=new Vector4[192],ups=new Vector4[192];
  public int VisibleSpans {get;private set;}public int QueryCount {get;private set;}public int Saturations {get;private set;}public double LastStepMilliseconds {get;private set;}
  public FireRingRibbonRenderer(Transform owner,Transform actor,int mask)
  {
   var shader=Resources.Load<Shader>("FireRingRibbon");if(shader==null||!shader.isSupported)throw new NotSupportedException("Supported ring ribbon requires URP Resources/FireRingRibbon shader.");
   collision=new FireFlowCollisionAdapter(mask,actor);material=new Material(shader){name="Shared moving ring density"};host=new GameObject("Collision-validated continuous ring flame");host.transform.SetParent(owner,false);
   renderer=host.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.enabled=false;
   mesh=new Mesh{name="Twenty-four validated fire ribbon spans"};mesh.MarkDynamic();mesh.vertices=positions;
   int[] faces={0,2,1,1,2,3,4,5,6,5,7,6,0,4,2,2,4,6,1,3,5,3,7,5,0,1,4,1,5,4,2,6,3,3,6,7};var triangles=new int[24*36];for(int i=0;i<24;i++)for(int j=0;j<36;j++)triangles[i*36+j]=i*8+faces[j];mesh.triangles=triangles;host.AddComponent<MeshFilter>().sharedMesh=mesh;
  }
  public void Step(Vector3[] support,bool[] valid,int count,Vector3 center,Vector3 up,float energy,float time)
  {
   using(Marker.Auto())
   {
    double started=Time.realtimeSinceStartupAsDouble;QueryCount=VisibleSpans=Saturations=0;int priorSaturation=collision.Saturations,priorRecovery=collision.MeshRecoveryRays;
    if(count<3||count>24||energy<=.001f){Clear();LastStepMilliseconds=0;return;}
    Vector3 min=Vector3.one*float.PositiveInfinity,max=Vector3.one*float.NegativeInfinity;
    for(int span=0;span<count;span++)
    {
     int next=(span+1)%count;if(!valid[span]||!valid[next])continue;
     QueryCount++;
     if(!FireRingRibbonMath.TrySpan(support[span],support[next],collision,out var safeEnd))continue;
     Vector3 a=support[span],b=safeEnd,axis=(b-a).normalized,mid=(a+b)*.5f;
     float half=(b-a).magnitude*.5f,radius=FireRingRibbonMath.Radius;
     Vector3 rise=Vector3.ProjectOnPlane(up,axis).normalized;if(rise.sqrMagnitude<.1f)continue;Vector3 side=Vector3.Cross(rise,axis);
     for(int k=0;k<8;k++)
     {
      int v=VisibleSpans*8+k;positions[v]=mid+side*((k&1)==0?-radius:radius)+rise*((k&2)==0?-radius:radius)+axis*((k&4)==0?-(half+radius):half+radius);
      centers[v]=new Vector4(mid.x,mid.y,mid.z,radius);axes[v]=new Vector4(axis.x,axis.y,axis.z,half);ups[v]=new Vector4(rise.x,rise.y,rise.z,energy);
      min=Vector3.Min(min,positions[v]);max=Vector3.Max(max,positions[v]);
     }
     VisibleSpans++;
    }
    // Adapter calls are counted consistently with the other flow sweeps; report extra
    // nonconvex recovery rays too. Each sweep also performs bounded overlap/cast internally.
    QueryCount+=collision.MeshRecoveryRays-priorRecovery;Saturations=collision.Saturations-priorSaturation;
    if(VisibleSpans==0){Clear();LastStepMilliseconds=(Time.realtimeSinceStartupAsDouble-started)*1000;return;}
    for(int i=VisibleSpans*8;i<positions.Length;i++){positions[i]=min;centers[i]=axes[i]=ups[i]=Vector4.zero;}
    mesh.SetVertices(positions);mesh.SetUVs(0,centers);mesh.SetUVs(1,axes);mesh.SetUVs(2,ups);
    var bounds=new Bounds((min+max)*.5f,max-min);mesh.bounds=bounds;renderer.bounds=bounds;
    material.SetVector("_RingCenter",center);material.SetVector("_RingUp",up);material.SetFloat("_FlowTime",time);
    renderer.enabled=true;if(!counted){counted=true;visibleGroups++;}LastStepMilliseconds=(Time.realtimeSinceStartupAsDouble-started)*1000;
   }
  }
  public void Clear(){VisibleSpans=0;renderer.enabled=false;if(counted){counted=false;visibleGroups=Mathf.Max(0,visibleGroups-1);}}
  public void Dispose(){if(disposed)return;disposed=true;Clear();collision.Dispose();UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(material);}
 }
}
