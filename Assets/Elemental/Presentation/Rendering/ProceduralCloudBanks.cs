using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;
using Elemental.Presentation.UI;
namespace Elemental.Presentation.Rendering
{
 [ExecuteAlways,DisallowMultipleComponent]
 public sealed class ProceduralCloudBanks:MonoBehaviour
 {
  public const string OwnedName="Valley Procedural Cloud Banks";
  public const int BankCount=22;
  private readonly Vector4[] overheadCenters=new Vector4[8],overheadRadii=new Vector4[8];
  public static Vector3 BankScale(int i)
  {
   if(i<6)return new Vector3(640+i*42,390+i*20,390+i*21);
   if(i<10){int s=i-6;return new Vector3(210+s*34,140+s*18,170+s*20);}
   if(i<14)return new Vector3(320+(i%3)*110,100+(i%2)*95,220+(i%3)*40);
   int n=i-14;return new Vector3(200+(n%3)*95,90+(n%4)*30,180+(n%3)*50);
  }
  private static int owners;
  public static bool HasActive=>owners>0;
  private static readonly ProfilerMarker Marker=new ProfilerMarker("ProceduralCloudBanks.Update");
  [SerializeField] private ValleyAtmosphereController atmosphere;
  [SerializeField] private FrontendFlowController frontend;
  [SerializeField] private Transform[] banks;
  [SerializeField] private MeshRenderer[] views;
  private float motionTime;
  public static Vector3 Centre(int i)
  {
   if(i>=14&&i<BankCount){int n=i-14;float a=n*Mathf.PI*.25f;return new Vector3(Mathf.Sin(a)*(180+n%3*60),260+n%3*85,Mathf.Cos(a)*(180+n%2*80));}
   if(i>=10&&i<14){int n=i-10;float a=(n*90+35)*Mathf.Deg2Rad;return new Vector3(Mathf.Sin(a)*800,180+n%2*100,Mathf.Cos(a)*800);}
   switch(i){case 0:return new Vector3(-760,290,1500);case 1:return new Vector3(680,440,2000);case 2:return new Vector3(70,590,2850);case 3:return new Vector3(30,300,-1550);case 4:return new Vector3(1050,450,-2050);case 5:return new Vector3(500,650,-2900);case 6:return new Vector3(-300,420,1750);case 7:return new Vector3(1100,570,2450);case 8:return new Vector3(520,470,-1850);case 9:return new Vector3(-600,420,-2200);default:throw new System.ArgumentOutOfRangeException(nameof(i));}
  }
  private void OnEnable(){owners++;}
  private void OnDisable(){owners=Mathf.Max(0,owners-1);Shader.SetGlobalFloat("_ElementalOverheadCloudCount",0);}
  public void Configure(ValleyAtmosphereController frame,FrontendFlowController flow,Mesh cube,Material material)
  {
   if(frame==null || cube==null || material==null)throw new System.ArgumentException("Explicit atmosphere, cube and cloud material required.");
   atmosphere=frame;frontend=flow;transform.SetParent(frame.transform,false);
   transform.localPosition=Vector3.zero;transform.localRotation=Quaternion.identity;transform.localScale=Vector3.one;
   banks=new Transform[BankCount];views=new MeshRenderer[BankCount];
   for(int i=0;i<BankCount;i++)
   {
    string name="Procedural Bank "+i;var child=transform.Find(name);
    if(child==null){child=new GameObject(name).transform;child.SetParent(transform,false);}
    child.localPosition=Centre(i);child.localRotation=Quaternion.Euler(0,i*37,0);child.localScale=new Vector3(640+i*42,390+i*20,390+i*21);
    if(i>=6){int small=i-6;child.localScale=new Vector3(210+small*34,140+small*18,170+small*20);}
    child.localScale=BankScale(i);
    var filter=child.GetComponent<MeshFilter>();if(filter==null)filter=child.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=cube;
    var view=child.GetComponent<MeshRenderer>();if(view==null)view=child.gameObject.AddComponent<MeshRenderer>();view.sharedMaterial=material;
    view.shadowCastingMode=ShadowCastingMode.Off;view.receiveShadows=false;view.lightProbeUsage=LightProbeUsage.Off;view.reflectionProbeUsage=ReflectionProbeUsage.Off;
    banks[i]=child;views[i]=view;
   }
  }
  private void Update()
  {
   using(Marker.Auto())
   {
    if(banks==null || views==null)return;
    bool show=atmosphere!=null && atmosphere.FogEnabled && atmosphere.CloudsEnabled;
    bool animate=Application.isPlaying && show && atmosphere.AnimateClouds && (frontend==null || !frontend.Preferences.ReducedMotion);
    if(animate)motionTime+=Time.unscaledDeltaTime;
    Shader.SetGlobalFloat("_ElementalCloudMotionTime",motionTime);
    for(int i=0;i<banks.Length;i++)
    {
     if(views[i]!=null && views[i].enabled!=show)views[i].enabled=show;
     if(banks[i]!=null)banks[i].localPosition=Centre(i)+new Vector3(Mathf.Sin(motionTime*.024f+i)*65,Mathf.Sin(motionTime*.019f+i)*12,Mathf.Sin(motionTime*.017f+i*1.7f)*24);
    }
    for(int i=0;i<8;i++)
    {
     int bank=i+14;
     overheadCenters[i]=bank<banks.Length&&banks[bank]!=null?(Vector4)banks[bank].localPosition:Vector4.zero;
     overheadRadii[i]=(Vector4)(BankScale(bank)*.42f);
    }
    Shader.SetGlobalFloat("_ElementalOverheadCloudCount",show&&banks.Length>=BankCount?8:0);
    Shader.SetGlobalMatrix("_ElementalOverheadWorldToFrame",transform.worldToLocalMatrix);
    Shader.SetGlobalVectorArray("_ElementalOverheadCloudCenters",overheadCenters);
    Shader.SetGlobalVectorArray("_ElementalOverheadCloudRadii",overheadRadii);
   }
  }
 }
}
