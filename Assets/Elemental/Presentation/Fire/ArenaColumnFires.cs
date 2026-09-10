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
  public const int MaximumFires=7,MaximumLights=7;
  [System.Serializable] public struct Seat {public MeshRenderer Source;public Vector3 LocalPoint;public Vector3 Up;}
  [SerializeField] private Seat[] seats;
  [SerializeField] private FireVisualProfile profile;
  [SerializeField] private CelestialSystemBehaviour sky;
  [SerializeField] private FrontendFlowController frontend;
  [SerializeField] private Light[] lights;
  private FireFlowVolumeBackend[] fire;
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
   fire=new FireFlowVolumeBackend[seats.Length];snapshots=new FirePresentationSnapshot[seats.Length];emitting=new bool[seats.Length];
   Shader shader=Resources.Load<Shader>("FireFlowParcel");
   Texture atlas=profile.CpuMaterial!=null?profile.CpuMaterial.GetTexture("_FlameAtlas"):null;
   if(atlas==null)throw new System.InvalidOperationException("Column fire requires its authored flame atlas.");
   for(int i=0;i<seats.Length;i++)
   {
    fire[i]=new FireFlowVolumeBackend(lights[i].transform,shader,Physics.DefaultRaycastLayers,atlas,false,64);
    fire[i].ConfigureDetailedAbilityFlame(Resources.Load<Texture2D>("EpicBurnTongues"));
    fire[i].ConfigureCoherentTower();
    fire[i].ConfigureSurfaceAccentDetail(true);
    // CoherentTower owns the .82 body support; do not overwrite it with the ring-tail preset.
    // Smaller overlapping gas structures form a rising plume; the cooling tip
    // separates into fine authored tongues instead of two inflated capsule lobes.
    float3 up=math.normalizesafe((float3)seats[i].Up,new float3(0,1,0));
    // Area injection is circular in the cap tangent plane from every camera angle.
    fire[i].Solver.Injection=new FireFlowInjection(float3.zero,4.2f,104,.44f,3,.85f,1f,developmentScale:1.2f,tailAgeScale:.9f,staggerBirths:true,birthDiscRadius:.20f,nozzleRadius:.18f,coolingTail:.40f,uniformBirthSpread:true,angularSpeed:1.2f,elongation:.3f,smokeExpansion:1.65f,smokeStride:4);fire[i].Begin((uint)(91231+i*613));
    snapshots[i]=new FirePresentationSnapshot{Group=new FireGroupHandle(i,1),Seed=(uint)(91231+i*613),Lifecycle=FireLifecycle.Active,Energy=1,NodeCount=1};
   }
  }
  private void Update()
  {
   using(Marker.Auto())
   {
    Initialize();if(fire==null || sky==null)return;
    bool reduced=frontend!=null && frontend.Preferences.ReducedMotion;
    float dt=reduced || Time.timeScale<=0?0:Mathf.Min(Time.unscaledDeltaTime,.05f);time+=dt;var camera=sky.TargetCamera;
    for(int i=0;i<seats.Length;i++)
    {
     bool active=seats[i].Source!=null && seats[i].Source.enabled && seats[i].Source.gameObject.activeInHierarchy;
     if(!active){if(emitting[i])fire[i].Clear();emitting[i]=false;lights[i].enabled=false;continue;}
     Vector3 point=seats[i].Source.transform.TransformPoint(seats[i].LocalPoint),up=seats[i].Up.normalized;
     // Seat the lamp just inside the arena-facing lip, so it illuminates the shaft instead of only the upward cap.
     Vector3 inward=Vector3.ProjectOnPlane(transform.position-point,up).normalized;
     lights[i].transform.position=point-up*.25f+inward*.75f;
     var snapshot=snapshots[i];snapshot.Origin=(float3)point;snapshot.FreeUp=(float3)up;snapshot.Time=time;
     // The same hot-gas/cooling-smoke pipeline, injected slowly above the authored cap.
     var origin=point+up*.25f;
     snapshot.Nodes[0]=FireFieldNode.Stream((float3)origin,(float3)(origin+up*.2f),(float3)up,(float3)up);
     snapshot.BoundsMin=(float3)(point-Vector3.one*3);snapshot.BoundsMax=(float3)(point+Vector3.one*3);
     if(!emitting[i]){for(int warm=0;warm<24;warm++){snapshot.Time=time-(24-warm)*.025f;fire[i].Step(snapshot,.025f,camera);}emitting[i]=true;snapshot.Time=time;}
     fire[i].Step(snapshot,dt,camera);
     int closer=0;float distance=LightPriority(camera,point,i);
     for(int j=0;j<seats.Length;j++)if(j!=i && seats[j].Source!=null && seats[j].Source.enabled && seats[j].Source.gameObject.activeInHierarchy)
     {float other=LightPriority(camera,seats[j].Source.transform.TransformPoint(seats[j].LocalPoint),j);if(other<distance || (other==distance && j<i))closer++;}
     lights[i].enabled=closer<MaximumLights;
     lights[i].intensity=NightIntensity(sky.Snapshot.Night01,reduced?1:Elemental.Simulation.Fire.FireLightEnvelope.Sample(time,i*1.71f));
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
