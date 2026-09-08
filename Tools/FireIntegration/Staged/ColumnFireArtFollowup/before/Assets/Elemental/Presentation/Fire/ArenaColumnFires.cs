using UnityEngine;
using Unity.Profiling;
using Unity.Mathematics;
using Elemental.Simulation.Fire;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
namespace Elemental.Presentation.Fire
{
 public sealed class ArenaColumnFires:MonoBehaviour
 {
  private static int owners;
  public static bool HasActive=>owners>0;
  private void OnEnable(){owners++;}
  public const string OwnedName="Arena Column Fires";
  public const int MaximumFires=7,MaximumLights=4;
  [System.Serializable] public struct Seat {public MeshRenderer Source;public Vector3 LocalPoint;public Vector3 Up;}
  [SerializeField] private Seat[] seats;
  [SerializeField] private FireVisualProfile profile;
  [SerializeField] private CelestialSystemBehaviour sky;
  [SerializeField] private FrontendFlowController frontend;
  [SerializeField] private Light[] lights;
  private FireCpuMeshBackend[] fire;
  private FirePresentationSnapshot[] snapshots;
  private bool[] emitting;
  private float time;
  private static readonly ProfilerMarker Marker=new ProfilerMarker("ArenaColumnFires.Update");
  public static float NightIntensity(float night,float flicker)=>Mathf.Lerp(.3f,14f,Mathf.SmoothStep(0,1,Mathf.Clamp01(night)))*Mathf.Clamp(flicker,.9f,1.1f);
  public void Configure(Seat[] source,FireVisualProfile settings,CelestialSystemBehaviour celestial,FrontendFlowController flow)
  {
   if(source==null || source.Length<1 || source.Length>MaximumFires || settings==null || celestial==null)throw new System.ArgumentException("One to seven authored seats, profile and celestial owner required.");
   seats=source;profile=settings;sky=celestial;frontend=flow;lights=new Light[seats.Length];
   for(int i=0;i<seats.Length;i++)
   {
    string name="Column Flame "+i;var child=transform.Find(name);if(child==null){child=new GameObject(name).transform;child.SetParent(transform,false);}
    var lamp=child.GetComponent<Light>();if(lamp==null)lamp=child.gameObject.AddComponent<Light>();
    lamp.type=LightType.Point;lamp.color=new Color(1,.52f,.18f);lamp.range=12;lamp.shadows=LightShadows.None;lamp.enabled=false;lights[i]=lamp;
   }
  }
  private void Start()=>Initialize();
  private void Initialize()
  {
   if(fire!=null || profile==null || seats==null)return;
   fire=new FireCpuMeshBackend[seats.Length];snapshots=new FirePresentationSnapshot[seats.Length];emitting=new bool[seats.Length];
   for(int i=0;i<seats.Length;i++)
   {
    fire[i]=new FireCpuMeshBackend(lights[i].transform,profile);fire[i].Begin((uint)(91231+i*613));
    snapshots[i]=new FirePresentationSnapshot{Group=new FireGroupHandle(i,1),Seed=(uint)(91231+i*613),Lifecycle=FireLifecycle.Active,Energy=1,NodeCount=1};
   }
  }
  private void Update()
  {
   using(Marker.Auto())
   {
    Initialize();if(fire==null || sky==null)return;
    bool reduced=frontend!=null && frontend.Preferences.ReducedMotion;
    float dt=reduced?0:Mathf.Min(Time.unscaledDeltaTime,.05f);time+=dt;var camera=sky.TargetCamera;
    for(int i=0;i<seats.Length;i++)
    {
     bool active=seats[i].Source!=null && seats[i].Source.enabled && seats[i].Source.gameObject.activeInHierarchy;
     if(!active){if(emitting[i])fire[i].Clear();emitting[i]=false;lights[i].enabled=false;continue;}
     Vector3 point=seats[i].Source.transform.TransformPoint(seats[i].LocalPoint),up=seats[i].Up.normalized;
     // Seat the lamp just inside the arena-facing lip, so it illuminates the shaft instead of only the upward cap.
     Vector3 inward=Vector3.ProjectOnPlane(transform.position-point,up).normalized;
     lights[i].transform.position=point-up*.25f+inward*.75f;
     Vector3 side=Vector3.Cross(up,Vector3.forward);if(side.sqrMagnitude<.1f)side=Vector3.Cross(up,Vector3.right);side.Normalize();
     Vector3 wind=side*(.22f+.12f*Mathf.Sin(time*.41f+i));
     var snapshot=snapshots[i];snapshot.Origin=(float3)point;snapshot.FreeUp=(float3)up;snapshot.Time=time;
     var node=FireFieldNode.Stream((float3)(point-up*.12f),(float3)(point+up*.30f),(float3)(up*1.2f+wind),(float3)up);
     node.Radius=.32f;node.Swirl=.3f;node.NoiseSpeed=.3f;node.Lift=1.2f;node.MaxTargetSpeed=3;
     snapshot.Nodes[0]=node;snapshot.BoundsMin=(float3)(point-Vector3.one*3);snapshot.BoundsMax=(float3)(point+Vector3.one*3);
     if(!emitting[i]){for(int warm=0;warm<24;warm++){snapshot.Time=time-(24-warm)*.025f;fire[i].Step(snapshot,.025f,profile.SpawnRate,camera);}emitting[i]=true;snapshot.Time=time;}
     fire[i].Step(snapshot,dt,profile.SpawnRate,camera);
     int closer=0;float distance=LightPriority(camera,point,i);
     for(int j=0;j<seats.Length;j++)if(j!=i && seats[j].Source!=null && seats[j].Source.enabled && seats[j].Source.gameObject.activeInHierarchy)
     {float other=LightPriority(camera,seats[j].Source.transform.TransformPoint(seats[j].LocalPoint),j);if(other<distance || (other==distance && j<i))closer++;}
     lights[i].enabled=closer<MaximumLights;
     lights[i].intensity=NightIntensity(sky.Snapshot.Night01,reduced?1:1+.07f*Mathf.Sin(time*5.1f+i)*Mathf.Sin(time*3.2f+i));
    }
   }
  }
  private void OnDisable(){owners=Mathf.Max(0,owners-1);if(fire!=null){foreach(var item in fire)item?.Dispose();fire=null;}if(lights!=null)foreach(var lamp in lights)if(lamp!=null)lamp.enabled=false;}
  private static float LightPriority(UnityEngine.Camera camera,Vector3 point,int fallback)
  {
   if(camera==null)return fallback;
   var offset=point-camera.transform.position;
   return offset.sqrMagnitude+(Vector3.Dot(offset,camera.transform.forward)<0?1000000f:0f);
  }
 }
}
