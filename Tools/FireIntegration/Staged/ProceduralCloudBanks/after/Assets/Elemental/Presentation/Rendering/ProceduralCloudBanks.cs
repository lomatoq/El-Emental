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
  public const int BankCount=6;
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
   switch(i){case 0:return new Vector3(-760,100,1300);case 1:return new Vector3(680,180,1900);case 2:return new Vector3(70,260,2500);case 3:return new Vector3(-500,90,-1700);case 4:return new Vector3(850,170,-2000);case 5:return new Vector3(200,300,-2800);default:throw new System.ArgumentOutOfRangeException(nameof(i));}
  }
  private void OnEnable(){owners++;}
  private void OnDisable(){owners=Mathf.Max(0,owners-1);}
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
    child.localPosition=Centre(i);child.localRotation=Quaternion.Euler(0,i*37,0);child.localScale=new Vector3(700+i*55,180+i*14,390+i*23);
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
    for(int i=0;i<banks.Length;i++)
    {
     if(views[i]!=null && views[i].enabled!=show)views[i].enabled=show;
     if(banks[i]!=null)banks[i].localPosition=Centre(i)+new Vector3(Mathf.Sin(motionTime*.007f+i)*18,Mathf.Sin(motionTime*.004f+i)*3,0);
    }
   }
  }
 }
}
