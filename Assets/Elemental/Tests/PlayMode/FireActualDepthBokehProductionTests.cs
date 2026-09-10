using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Presentation.Fire;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(240000)]public IEnumerator ActualFireDepthBokehKeepsFocusedFlameSharpAndSoftensDistantFlame()
  {
   GameObject blocker=null;FireProtectionSphereRenderer near=null,far=null;FireVisualCaptureCamera capture=null;Camera camera=null;EarthCinematicDepthOfFieldController focus=null;Transform primary=null,secondary=null;int mask=0;float savedWeight=1;bool savedCapture=false;EarthCinematicDepthOfFieldDebugView savedDebug=default;float saved=Time.captureDeltaTime;System.IDisposable safe=null;
   string folder="BuildReports/HardPolish/G05/FireDepthBokeh-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();safe=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    camera=All<CelestialSystemBehaviour>().Single().TargetCamera;mask=camera.cullingMask;focus=camera.GetComponent<EarthCinematicDepthOfFieldController>();Assert.That(focus,Is.Not.Null);
    primary=focus.PrimarySubject;secondary=focus.SecondarySubject;savedWeight=focus.PresentationWeight;savedCapture=focus.HasCaptureOverride;savedDebug=focus.CaptureDebugView;focus.ConfigureSubjects(flightMotor.transform,flightMotor.transform);focus.SetCaptureOverride(true);focus.SetPresentationWeight(1);
    Vector3 up=flightMotor.LocalUp,target=flightMotor.Body.position+up;Vector3 at=target-flightMotor.FacingForward*6+up;
    capture=camera.gameObject.AddComponent<FireVisualCaptureCamera>();capture.Place(at,target);yield return new WaitForEndOfFrame();
    Vector3 forward=(target-at).normalized,right=Vector3.Cross(up,forward).normalized;Vector3 nearPoint=target-right*1.5f,farPoint=at+forward*30+right*7.5f;
    near=new FireProtectionSphereRenderer(binding.transform);far=new FireProtectionSphereRenderer(binding.transform);near.SetRenderingLayerForQa(31);far.SetRenderingLayerForQa(31);camera.cullingMask=1<<31;
    near.Step(true,nearPoint,up,.38f,1,3,filled:true);far.Step(true,farPoint,up,1.9f,1,3,filled:true);
    Time.captureDeltaTime=1f/1000000;yield return new WaitForEndOfFrame();
    Assert.That(focus.TryGetRenderSettings(out var settings),Is.True);Assert.That(AtmosphereFullscreenFeature.FireDofActive,Is.True);
    float nearEye=Vector3.Dot(nearPoint-camera.transform.position,camera.transform.forward);
    Assert.That(nearEye-.38f*1.08f,Is.GreaterThan(settings.SharpNearDistance));Assert.That(nearEye+.38f*1.08f,Is.LessThan(settings.SharpFarDistance));
    var on=RenderFireDofSameFrame(camera);AtmosphereFullscreenFeature.DisableFireDofForQa=true;var off=RenderFireDofSameFrame(camera);
    try
    {
     File.WriteAllBytes(folder+"/actual-depth-on.png",on.EncodeToPNG());File.WriteAllBytes(folder+"/fire-only-bokeh-off.png",off.EncodeToPNG());
     Vector3 nearPixel=camera.WorldToScreenPoint(nearPoint),farPixel=camera.WorldToScreenPoint(farPoint);
     float nearDiff=FireDofDifference(on,off,nearPixel,90),farDiff=FireDofDifference(on,off,farPixel,90);
     File.WriteAllText(folder+"/metrics.csv","nearDiff,farDiff,sharpNear,sharpFar,maxRadius\n"+nearDiff+","+farDiff+","+settings.SharpNearDistance+","+settings.SharpFarDistance+","+settings.MaxRadiusPixels);
     Assert.That(farDiff,Is.GreaterThan(.0001f),"Distant flame must receive depth-derived bokeh while opaque DOF stays unchanged.");
     Assert.That(nearDiff,Is.LessThan(farDiff*.2f+.00001f),"Focused flame cannot inherit the depth of sky behind it.");
    }
    finally{Object.Destroy(on);Object.Destroy(off);}
    // Keep opaque DOF enabled while a real foreground surface covers the far flame.
    blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Fire bokeh foreground rejection";blocker.layer=31;
    blocker.transform.position=at+(farPoint-at)*(8f/30f);blocker.transform.rotation=Quaternion.LookRotation(forward,up);blocker.transform.localScale=new Vector3(2.4f,2.4f,.15f);
    AtmosphereFullscreenFeature.DisableFireDofForQa=false;var blockedOn=RenderFireDofSameFrame(camera);
    AtmosphereFullscreenFeature.DisableFireDofForQa=true;var blockedOff=RenderFireDofSameFrame(camera);
    try{File.WriteAllBytes(folder+"/foreground-depth-rejection.png",blockedOn.EncodeToPNG());float difference=FireDofDifference(blockedOn,blockedOff,camera.WorldToScreenPoint(blocker.transform.position),45);Assert.That(difference,Is.LessThan(.0002f),"Defocused far fire must not spread over a nearer opaque surface.");}
    finally{Object.Destroy(blockedOn);Object.Destroy(blockedOff);}
    foreach(string shaderName in new[]{"FireDepthBokeh","FireFlowParcel","FireProtectionSphere","FireRingRibbon","FireBoltTrail","FireMeteorBow","FireFlipbookAccent","FireSmokeDust"}){var shader=Resources.Load<Shader>(shaderName);Assert.That(shader,Is.Not.Null);Assert.That(shader.isSupported,Is.True);Assert.That(UnityEditor.ShaderUtil.GetShaderMessages(shader).Count(x=>x.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),Is.Zero,shaderName);}
   }
   finally{if(blocker!=null)Object.Destroy(blocker);AtmosphereFullscreenFeature.DisableFireDofForQa=false;near?.Dispose();far?.Dispose();if(camera!=null)camera.cullingMask=mask;if(focus!=null){focus.ConfigureSubjects(primary,secondary);focus.SetPresentationWeight(savedWeight);focus.SetCaptureOverride(savedCapture,savedDebug);}if(capture!=null)Object.Destroy(capture);safe?.Dispose();Time.captureDeltaTime=saved;ReleaseFireAbilityFixture();}
  }
  private static Texture2D RenderFireDofSameFrame(Camera camera)
  {
   int width=camera.pixelWidth,height=camera.pixelHeight;
   var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGBHalf);var image=new Texture2D(width,height,TextureFormat.RGB24,false);
   var previous=RenderTexture.active;var previousTarget=camera.targetTexture;bool savedSrgb=GL.sRGBWrite;RenderTexture display=null;
   var data=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();bool dither=data!=null&&data.dithering;
   try
   {
    if(data!=null)data.dithering=false;
    var request=new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target};Assert.That(UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera,request),Is.True);
    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
    display=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
    GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;Graphics.Blit(target,display);RenderTexture.active=display;
    image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply(false,false);return image;
   }
   catch{Object.Destroy(image);throw;}
   finally{GL.sRGBWrite=savedSrgb;if(display!=null)RenderTexture.ReleaseTemporary(display);if(data!=null)data.dithering=dither;camera.targetTexture=previousTarget;RenderTexture.active=previous;target.Release();Object.Destroy(target);}
  }
  private static float FireDofDifference(Texture2D a,Texture2D b,Vector3 center,int radius)
  {float sum=0;int count=0;int x0=Mathf.Max(0,(int)center.x-radius),x1=Mathf.Min(a.width,(int)center.x+radius),y0=Mathf.Max(0,(int)center.y-radius),y1=Mathf.Min(a.height,(int)center.y+radius);for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){Color p=a.GetPixel(x,y),q=b.GetPixel(x,y);sum+=Mathf.Abs(p.r-q.r)+Mathf.Abs(p.g-q.g)+Mathf.Abs(p.b-q.b);count++;}return sum/Mathf.Max(1,count);}
 }
}
