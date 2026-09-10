using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(240000)]public IEnumerator ActualSavedRivalBurnStaysOnVisibleSkinAndMaskDuringPoseChange()
  {
   float saved=Time.captureDeltaTime;FireVisualCaptureCamera capture=null;UnityEngine.Animator animator=null;bool animatorWasEnabled=false;Transform chest=null;Quaternion originalChest=default;Renderer[] hidden=null;bool[] originalVisibility=null;int[] originalLayers=null;UnityEngine.Camera captureView=null;int originalMask=0;
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();
    // Only this capture fixture hides the foreground player; preserve exact states.
    hidden=duel.PlayerTransform.GetComponentsInChildren<Renderer>(true);originalVisibility=new bool[hidden.Length];
    originalLayers=new int[hidden.Length];for(int i=0;i<hidden.Length;i++){originalVisibility[i]=hidden[i].enabled;originalLayers[i]=hidden[i].gameObject.layer;}
    for(int i=0;i<hidden.Length;i++){hidden[i].enabled=false;hidden[i].gameObject.layer=31;}
    var root=duel.BotTransform;var motor=root.GetComponent<Elemental.Runtime.Characters.PlanetMotor>();
    animator=root.GetComponentsInChildren<Animator>().First(x=>x.isHuman);animatorWasEnabled=animator.enabled;
    chest=animator.GetBoneTransform(HumanBodyBones.Chest);if(chest==null)chest=animator.GetBoneTransform(HumanBodyBones.Spine);Assert.That(chest,Is.Not.Null);originalChest=chest.localRotation;
    var view=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
    captureView=view;originalMask=view.cullingMask;view.cullingMask&=~(1<<31);
    Vector3 normal=Vector3.ProjectOnPlane(duel.PlayerTransform.position-root.position,motor.LocalUp).normalized;
    if(normal.sqrMagnitude<.5f)normal=root.forward;
    Vector3 point=motor.Capsule.ClosestPoint(chest.position+normal*3);
    capture=view.gameObject.AddComponent<FireVisualCaptureCamera>();capture.Place(point+normal*3+motor.LocalUp*.4f,chest.position);
    var response=binding.PlayerSession.GetComponent<Elemental.Runtime.Fire.FireWorldImpact>();
    for(int i=0;i<18;i++)
    {
     Physics.SyncTransforms();Assert.That(response.ApplyContact(motor.Capsule,point,normal,-normal,.05f,1),Is.True);
     yield return new WaitForSeconds(.05f);
    }
    var burning=binding.GetComponent<Elemental.Presentation.Fire.FireSmolderPresentation>();
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    var seats=(System.Array)burning.GetType().GetField("seats",flags).GetValue(burning);object owned=null;
    foreach(var seat in seats)if(seat.GetType().GetField("Root").GetValue(seat) as Transform==root){owned=seat;break;}
    Assert.That(owned,Is.Not.Null);var type=owned.GetType();int count=(int)type.GetField("BurnPointsCount").GetValue(owned);
    Assert.That(count,Is.GreaterThan(0),"Actual saved receiver needs real visible-skin anchors.");
    var points=(Vector3[])type.GetField("BurnPoints").GetValue(owned);
    var links=(System.Array)type.GetField("Anchors").GetValue(owned);
    void CheckSkinAndMask()
    {
     for(int i=0;i<count;i++)
     {
      object link=links.GetValue(i);var skin=link.GetType().GetField("Renderer").GetValue(link) as SkinnedMeshRenderer;
      Assert.That(skin,Is.Not.Null);Vector3 world=root.TransformPoint(points[i]);
      Assert.That(skin.bounds.SqrDistance(world),Is.LessThan(.12f*.12f),"Burn root escaped the actual rendered character bounds.");
      var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block);
      Assert.That(skin.sharedMaterial.HasProperty("_FireChar"),Is.True);
      Assert.That(block.GetFloat("_FireChar"),Is.GreaterThan(0));
      Vector3 mask=block.GetVector("_FireBurnPoint");float radius=block.GetFloat("_FireBurnRadius");
      Assert.That(Vector3.Distance(mask,world),Is.LessThan(radius+.3f),"Visible flame area and mesh burn mask diverged.");
     }
    }
    CheckSkinAndMask();yield return new WaitForEndOfFrame();SaveFireAbilityFrame("actual-rival-burn-skin-before-pose");
    animator.enabled=false;chest.localRotation=originalChest*Quaternion.Euler(0,0,24);
    // Preserve the actual rig and presentation scale. No heat contact follows.
    yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();CheckSkinAndMask();SaveFireAbilityFrame("actual-rival-burn-skin-after-pose");
   }
   finally
   {
    if(captureView!=null)captureView.cullingMask=originalMask;
    if(hidden!=null)for(int i=0;i<hidden.Length;i++)if(hidden[i]!=null){hidden[i].enabled=originalVisibility[i];hidden[i].gameObject.layer=originalLayers[i];}
    if(chest!=null)chest.localRotation=originalChest;if(animator!=null)animator.enabled=animatorWasEnabled;if(capture!=null)Object.Destroy(capture);
    Time.captureDeltaTime=saved;ReleaseFireAbilityFixture();
   }
  }
  [UnityTest,Timeout(240000)]public IEnumerator ActualSavedRivalSkinBakeWorldScaleDiagnostic()
  {
   Mesh baked=null;float saved=Time.captureDeltaTime;
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();
    var root=duel.BotTransform;Assert.That(root,Is.Not.Null);
    var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>();Assert.That(skins.Length,Is.GreaterThan(0));
    baked=new Mesh();var report=new StringBuilder("renderer,bakeUseScale,worldTransform,rootScale,skinScale,skinBoundsCenter,skinBoundsSize,bakedWorldCenter,bakedWorldSize,centerDistance,sizeRatio\n");
    foreach(var skin in skins)
    {
     if(!skin.enabled||!skin.gameObject.activeInHierarchy||skin.sharedMesh==null)continue;
     for(int scale=0;scale<2;scale++)
     {
      skin.BakeMesh(baked,scale!=0);var vertices=baked.vertices;Assert.That(vertices.Length,Is.GreaterThan(0));
      for(int transformScale=0;transformScale<2;transformScale++)
      {
       var matrix=transformScale==0?Matrix4x4.TRS(skin.transform.position,skin.transform.rotation,Vector3.one):skin.transform.localToWorldMatrix;
       var bounds=new Bounds(matrix.MultiplyPoint3x4(vertices[0]),Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(matrix.MultiplyPoint3x4(v));
       string V(Vector3 value)=>value.ToString("F5").Replace(',', ';');
       report.AppendLine($"{skin.name},{scale},{transformScale},{V(root.lossyScale)},{V(skin.transform.lossyScale)},{V(skin.bounds.center)},{V(skin.bounds.size)},{V(bounds.center)},{V(bounds.size)},{Vector3.Distance(bounds.center,skin.bounds.center):F6},{bounds.size.magnitude/skin.bounds.size.magnitude:F6}");
      }
     }
    }
    string folder="BuildReports/HardPolish/G05/ActualRivalBurn-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);File.WriteAllText(folder+"/bake-space.csv",report.ToString());
    Debug.Log("Actual rival bake-space diagnostic: "+folder+"/bake-space.csv");
   }
   finally{if(baked!=null)Object.Destroy(baked);Time.captureDeltaTime=saved;ReleaseFireAbilityFixture();}
  }
 }
}
