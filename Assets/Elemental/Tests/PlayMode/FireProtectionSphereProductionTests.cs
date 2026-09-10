using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Simulation.Fire;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(240000)]public IEnumerator ActualProtectiveSphereIsVolumetricInsideAndOpaqueDepthClipped()
  {
   float savedStep=Time.captureDeltaTime;FireVisualCaptureCamera capture=null;System.IDisposable safeRival=null;GameObject wall=null;
   Camera view=null;int savedMask=0;FireProtectionSphereRenderer sphere=null;
   string folder="BuildReports/HardPolish/G05/ProtectionSphere-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();safeRival=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    var shader=Resources.Load<Shader>("FireProtectionSphere");Assert.That(shader,Is.Not.Null);Assert.That(shader.isSupported,Is.True);
    Assert.That(UnityEditor.ShaderUtil.GetShaderMessages(shader).Count(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),Is.Zero);
    var effects=binding.GetComponent<FireAbilityEffects>();sphere=(FireProtectionSphereRenderer)typeof(FireAbilityEffects).GetField("protectionSphere",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(effects);
    Assert.That(sphere,Is.Not.Null);flightAbility.SetWeaveHeld(true,FireWeaveForm.Sphere,1);yield return new WaitForSeconds(.45f);
    Assert.That(effects.SphereVisible,Is.True);Assert.That(effects.SphereRaySteps,Is.EqualTo(24));
    view=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;savedMask=view.cullingMask;capture=view.gameObject.AddComponent<FireVisualCaptureCamera>();
    Vector3 up=flightMotor.LocalUp,side=Vector3.Cross(up,flightMotor.FacingForward).normalized,across=Vector3.Cross(up,side);
    Directory.CreateDirectory(folder);
    for(int frame=0;frame<4;frame++)
    {
     Vector3 center=sphere.Center,offset=frame==0?side*5+up:frame==1?across*5+up:frame==2?side*2+up*4:side*.9f+up*.4f;
     capture.Place(center+offset,frame==3?center+across*3:center);
     yield return new WaitForSeconds(.12f);yield return new WaitForEndOfFrame();
     var ownHeat=new MaterialPropertyBlock();
     foreach(var ownRenderer in duel.PlayerTransform.GetComponentsInChildren<Renderer>(true))
     {
      ownRenderer.GetPropertyBlock(ownHeat);
      Assert.That(ownHeat.GetFloat("_FireHeat"),Is.LessThanOrEqualTo(.0001f),"The owner's protective sphere must not ignite its own rig: "+ownRenderer.name);
      Assert.That(ownHeat.GetFloat("_FireChar"),Is.LessThanOrEqualTo(.0001f),"The owner's protective sphere must not char its own rig: "+ownRenderer.name);
     }
     var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(folder+"/"+(frame==3?"inside":"outside-"+frame)+".png",image.EncodeToPNG());}finally{Object.Destroy(image);}
    }
    // Isolate this actual sphere for a strict foreground-depth A/B, retaining the real camera pipeline.
    Time.captureDeltaTime=1f/1000000;view.cullingMask=1<<31;sphere.SetRenderingLayerForQa(31);
    Vector3 target=sphere.Center,at=target+side*5;capture.Place(at,target);
    yield return new WaitForEndOfFrame();var isolatedOn=ScreenCapture.CaptureScreenshotAsTexture();
    sphere.SetRenderingForQa(false);yield return new WaitForEndOfFrame();var isolatedOff=ScreenCapture.CaptureScreenshotAsTexture();
    try{int changed=0;var a=isolatedOn.GetPixels32();var b=isolatedOff.GetPixels32();for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>5)changed++;Assert.That(changed,Is.GreaterThan(200),"The continuous volume itself must visibly render, independent of surface cards.");}
    finally{Object.Destroy(isolatedOn);Object.Destroy(isolatedOff);sphere.SetRenderingForQa(true);}
    wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Protective sphere opaque foreground proof";wall.layer=31;
    wall.transform.position=at-side*1.2f;wall.transform.rotation=Quaternion.LookRotation(side,up);wall.transform.localScale=new Vector3(2,2,.15f);
    yield return new WaitForEndOfFrame();var visible=ScreenCapture.CaptureScreenshotAsTexture();
    sphere.SetRenderingForQa(false);yield return new WaitForEndOfFrame();var hidden=ScreenCapture.CaptureScreenshotAsTexture();
    try
    {
     File.WriteAllBytes(folder+"/foreground-on.png",visible.EncodeToPNG());File.WriteAllBytes(folder+"/foreground-off.png",hidden.EncodeToPNG());
     double delta=0;int count=0;var a=visible.GetPixels32();var b=hidden.GetPixels32();
     for(int y=visible.height/2-20;y<visible.height/2+20;y++)for(int x=visible.width/2-20;x<visible.width/2+20;x++){int i=y*visible.width+x;delta+=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b);count++;}
     Assert.That(delta/count,Is.LessThan(2),"Foreground opaque pixels must not receive sphere emission.");
    }
    finally{Object.Destroy(visible);Object.Destroy(hidden);sphere.SetRenderingForQa(true);}
    File.WriteAllText(folder+"/metrics.json",JsonUtility.ToJson(ReadFireEffectsFrame(),true));
    flightAbility.SetWeaveHeld(false,FireWeaveForm.Jet,1);yield return null;yield return new WaitForEndOfFrame();Assert.That(effects.SphereVisible,Is.False);
    Assert.That(UnityEditor.ShaderUtil.GetShaderMessages(shader).Count(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),Is.Zero);
   }
   finally
   {
    if(sphere!=null)sphere.SetRenderingLayerForQa(0);if(view!=null)view.cullingMask=savedMask;if(wall!=null)Object.Destroy(wall);if(capture!=null)Object.Destroy(capture);
    safeRival?.Dispose();Time.captureDeltaTime=savedStep;ReleaseFireAbilityFixture();
   }
  }
 }
}
